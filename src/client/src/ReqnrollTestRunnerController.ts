import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient/node';
import { TestResult, RunTestsParams, JsonRpcErrorLike } from './ReqnrollTestRunnerController.Models';


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

        // Mark containers as started for UI feedback
        queue.forEach((testItem) => run.started(testItem));

        // Extract only leaf test cases (items without children) from the queue
        const testCases = this.extractLeafTestCases(queue);

        // Mark all leaf test cases as started to show loading indicator
        testCases.forEach((testItem) => run.started(testItem));

        // Run tests individually and update UI as each completes
        await Promise.all(testCases.map(async (testItem) => {
          try {
            const results = await this.sendRunTestsRequest([testItem]);
            
            for (const result of results) {
              // Append output message if available
              if (result.message) {
                run.appendOutput(result.message.replace(/\r?\n/g, '\r\n'));
              }

              if (result.passed) {
                run.passed(testItem);
              } else {
                const message = new vscode.TestMessage(result.message ?? 'Test failed');
                if (result.line !== undefined && testItem.uri) {
                  message.location = new vscode.Location(
                    testItem.uri,
                    new vscode.Position(result.line, 0)
                  );
                }

                run.failed(testItem, message);
              }
            }
          } catch (error) {
            const message = this.formatRequestError(error);
            run.errored(testItem, new vscode.TestMessage(`runTests request failed: ${message}`));
          }
        }));

        run.end();
      },
      true
    );
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

  private async sendRunTestsRequest(testItems: vscode.TestItem[]): Promise<TestResult[]> {
    const tests = testItems.map(item => ({
      id: item.id,
      filePath: this.getFilePath(item)
    }));

    return await this.client.sendRequest(
      'rotbarsch.reqnroll/runTests',
      { tests } as RunTestsParams
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
