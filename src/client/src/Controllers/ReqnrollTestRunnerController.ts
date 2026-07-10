import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient/node';
import { TestResult, TestResultNotification, RunTestsParams, TestInfo, JsonRpcErrorLike } from '../Models/ReqnrollTestRunnerController.Models';

const TEST_RESULT_NOTIFICATION = 'rotbarsch.reqnroll/testResult';

export class ReqnrollTestRunnerController {
  public constructor(
    private readonly client: LanguageClient
  ) {
  }

  public activate(controller: vscode.TestController): vscode.TestRunProfile {
    return controller.createRunProfile(
      'Run',
      vscode.TestRunProfileKind.Run,
      async (request) => {
        const run = controller.createTestRun(request);

        const queue: vscode.TestItem[] = [];
        if (request.include) {
          // Check for "Run All" special case: Test Explorer passes a test item with empty ID and no children
          // when the "Run All Tests" button is clicked in the Test Explorer toolbar
          const hasRunAllMarker = request.include.some(item => item.id === '' && item.children.size === 0);
          
          if (hasRunAllMarker) {
            // "Run All" was triggered - include all tests from the controller
            controller.items.forEach((testItem) => queue.push(testItem));
          } else {
            // Normal test run - include only the selected tests
            request.include.forEach((testItem) => queue.push(testItem));
          }
        } else {
          // No specific tests selected - run all tests
          controller.items.forEach((testItem) => queue.push(testItem));
        }

        // Mark selected parent items as started
        queue.forEach((testItem) => run.started(testItem));

        // Collect all leaf test cases for execution
        const allLeaves = this.extractLeafTestCases(queue);
        const leavesById = new Map(allLeaves.map((leaf) => [leaf.id, leaf]));

        // Mark all leaf tests as enqueued so users can see what's pending
        allLeaves.forEach((leaf) => run.enqueued(leaf));

        // Mark all leaf tests as started right before execution
        allLeaves.forEach((leaf) => run.started(leaf));

        // Listen for individual test results as they complete, so the UI updates
        // incrementally instead of waiting for the whole batch to finish.
        const runId = this.generateRunId();
        const reportedIds = new Set<string>();
        const disposable = this.client.onNotification(TEST_RESULT_NOTIFICATION, (notification: TestResultNotification) => {
          if (notification.runId !== runId) {
            return;
          }

          const leaf = leavesById.get(notification.result.id);
          if (!leaf) {
            return;
          }

          reportedIds.add(notification.result.id);
          this.applyTestResult(leaf, notification.result, run);
        });

        try {
          // Send a single runTests request containing all requested tests,
          // handled together by the language server. Individual results arrive
          // via the notification handler above as each test finishes; the
          // response below is only used as a fallback for any test that wasn't
          // already reported via notification (e.g. older server versions).
          const results = await this.sendRunTestsRequest(allLeaves, runId);
          const resultsById = new Map(results.map((result) => [result.id, result]));

          for (const leaf of allLeaves) {
            if (reportedIds.has(leaf.id)) {
              continue;
            }
            this.applyTestResult(leaf, resultsById.get(leaf.id), run);
          }
        } catch (error) {
          const message = this.formatRequestError(error);
          for (const leaf of allLeaves) {
            if (reportedIds.has(leaf.id)) {
              continue;
            }
            run.errored(leaf, new vscode.TestMessage(`runTests request failed: ${message}`));
          }
        } finally {
          disposable.dispose();
        }

        run.end();
      },
      true
    );
  }

  private generateRunId(): string {
    return `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }

  private applyTestResult(test: vscode.TestItem, result: TestResult | undefined, run: vscode.TestRun): void {
    if (!result) {
      run.errored(test, new vscode.TestMessage('No test result received'));
      return;
    }

    if (result.message) {
      // Associate the output with this specific test (3rd arg) so it shows up
      // in the Test Results view for that test, in addition to the run's output panel.
      run.appendOutput(result.message.replace(/\r?\n/g, '\r\n'), undefined, test);
    }

    if (result.passed) {
      run.passed(test);
    } else {
      const message = new vscode.TestMessage(result.message ?? 'Test failed');
      if (result.line !== undefined && test.uri) {
        message.location = new vscode.Location(
          test.uri,
          new vscode.Position(result.line, 0)
        );
      }
      run.failed(test, message);
    }
  }

  private extractLeafTestCases(testItems: vscode.TestItem[]): vscode.TestItem[] {
    const leafTestCases: vscode.TestItem[] = [];

    const collectLeaves = (item: vscode.TestItem) => {
      // If the item has no children, it's a leaf test case
      if (item.children.size === 0) {
        leafTestCases.push(item);
      } else {
        // Otherwise, recursively collect from all children
        item.children.forEach(child => collectLeaves(child));
      }
    };

    testItems.forEach(item => collectLeaves(item));
    return leafTestCases;
  }

  private async sendRunTestsRequest(testItems: vscode.TestItem[], runId: string): Promise<TestResult[]> {
    const tests = testItems.map(item => {
      const testInfo: TestInfo = {
        id: item.id,
        filePath: this.getFilePath(item),
        isContainer: item.children.size > 0
      };

      // Extract parentId from tags
      const parentIdTag = item.tags.find(tag => tag.id.startsWith('parentId:'));
      if (parentIdTag) {
        testInfo.parentId = parentIdTag.id.substring('parentId:'.length);
      }

      // Extract pickleIndex from tags
      const pickleIndexTag = item.tags.find(tag => tag.id.startsWith('pickleIndex:'));
      if (pickleIndexTag) {
        testInfo.pickleIndex = parseInt(pickleIndexTag.id.substring('pickleIndex:'.length));
      }

      return testInfo;
    });

    return await this.client.sendRequest(
      'rotbarsch.reqnroll/runTests',
      { tests, runId } as RunTestsParams
    ) as TestResult[];
  }

  private getFilePath(testItem: vscode.TestItem): string {
    // If this item has a URI, use it
    if (testItem.uri) {
      return testItem.uri.fsPath;
    }

    // Otherwise, search through children to find a URI
    const findUriInChildren = (item: vscode.TestItem): string | undefined => {
      if (item.uri) {
        return item.uri.fsPath;
      }

      let foundUri: string | undefined;
      item.children.forEach(child => {
        if (!foundUri) {
          foundUri = findUriInChildren(child);
        }
      });

      return foundUri;
    };

    return findUriInChildren(testItem) ?? '';
  }

  private findTestItemById(items: vscode.TestItem[], id: string): vscode.TestItem | undefined {
    for (const item of items) {
      if (item.id === id) {
        return item;
      }

      // Recursively search children
      const childItems: vscode.TestItem[] = [];
      item.children.forEach(child => childItems.push(child));
      const found = this.findTestItemById(childItems, id);
      if (found) {
        return found;
      }
    }

    return undefined;
  }

  private formatRequestError(error: unknown): string {
    if (error && typeof error === 'object') {
      const jsonRpcError = error as JsonRpcErrorLike;
      const codePart = typeof jsonRpcError.code === 'number' ? `code=${jsonRpcError.code}; ` : '';
      const messagePart = jsonRpcError.message ?? (error as Error).message ?? 'Unknown LSP request error';
      const dataPart = jsonRpcError.data !== undefined ? `; data=${JSON.stringify(jsonRpcError.data)}` : '';
      return `${codePart}${messagePart}${dataPart}`;
    }

    if (typeof error === 'string') {
      return error;
    }

    return 'Unknown LSP request error';
  }
}
