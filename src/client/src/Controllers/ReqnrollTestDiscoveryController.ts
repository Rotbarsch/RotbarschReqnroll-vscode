import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient/node';
import { DiscoveredTest, DiscoverTestsParams } from '../Models/ReqnrollTestDiscoveryController.Models';


export class ReqnrollTestDiscoveryController {
    private controller: vscode.TestController;

    public constructor(
        private readonly client: LanguageClient,
        id: string,
        label: string
    ) {
        this.controller = vscode.tests.createTestController(id, label);
    }

    public getController(): vscode.TestController {
        return this.controller;
    }

    public setupTestDiscovery(): vscode.FileSystemWatcher {
        // Watch for .feature and .feature.cs file changes and discover tests
        // The .feature.cs files are the generated C# files which contain the actual test methods
        const watcher = vscode.workspace.createFileSystemWatcher('**/*.feature{,.cs}');

        // OnFileAdd: Run discovery for that file
        watcher.onDidCreate(async (uri) => {
            await this.discoverTestsForFile(uri);
        });

        // OnFileUpdate: Remove all test items associated with that file, rediscover for that file
        watcher.onDidChange(async (uri) => {
            this.removeTestItemsForFile(uri);
            await this.discoverTestsForFile(uri);
        });

        // OnFileDelete: Delete all test items associated with that file
        watcher.onDidDelete((uri) => {
            this.removeTestItemsForFile(uri);
        });

        // Discover tests in all existing feature files
        vscode.workspace.findFiles('**/*.feature').then(async (uris) => {
            for (const uri of uris) {
                await this.discoverTestsForFile(uri);
            }
        });

        return watcher;
    }

    public async discoverTestsForFile(uri: vscode.Uri): Promise<void> {
        try {
            const tests = await this.sendDiscoverTestsRequest(uri.toString());

            // Add all discovered tests
            for (const test of tests) {
                this.addTestItem(test, this.controller.items);
            }
        } catch (error) {
            console.error(`Test discovery failed for ${uri.toString()}: ${this.formatRequestError(error)}`);
        }
    }

    private addTestItem(test: DiscoveredTest, parent: vscode.TestItemCollection): void {
        const uri = vscode.Uri.parse(test.uri);
        const range = new vscode.Range(
            new vscode.Position(test.range.startLine, test.range.startCharacter),
            new vscode.Position(test.range.endLine, test.range.endCharacter)
        );

        // Get or create the test item
        let item = parent.get(test.id);
        if (!item) {
            item = this.controller.createTestItem(test.id, test.label, uri);
            parent.add(item);
        }

        // Sanitize label for VS Code Test Explorer: replace all '$(...)' with '{...}'
        let sanitizedLabel = test.label.replace(/\$\(([^)]+)\)/g, '{$1}');
        item.label = sanitizedLabel;
        item.range = range;

        // Store ParentId and PickleIndex as tags for later retrieval
        const tags: vscode.TestTag[] = [];
        if (test.parentId !== undefined && test.parentId !== null) {
            tags.push(new vscode.TestTag(`parentId:${test.parentId}`));
        }
        if (test.pickleIndex !== undefined && test.pickleIndex !== null) {
            tags.push(new vscode.TestTag(`pickleIndex:${test.pickleIndex}`));
        }
        item.tags = tags;

        // Add children recursively
        if (test.children && test.children.length > 0) {
            for (const child of test.children) {
                this.addTestItem(child, item.children);
            }
        }
    }

    private async sendDiscoverTestsRequest(uri: string): Promise<DiscoveredTest[]> {
        return await this.client.sendRequest(
            'rotbarsch.reqnroll/discoverTests',
            { uri } as DiscoverTestsParams
        ) as DiscoveredTest[];
    }

    public removeTestItemsForFile(uri: vscode.Uri): void {
        const uriString = uri.toString();

        // Remove all test items that belong to this file
        const removeFromCollection = (collection: vscode.TestItemCollection) => {
            const itemsToRemove: string[] = [];

            collection.forEach((item) => {
                if (item.uri?.toString() === uriString) {
                    itemsToRemove.push(item.id);
                } else if (item.children.size > 0) {
                    // Recursively check children
                    removeFromCollection(item.children);
                }
            });

            itemsToRemove.forEach(id => collection.delete(id));
        };

        removeFromCollection(this.controller.items);
    }

    private formatRequestError(error: unknown): string {
        if (error && typeof error === 'object') {
            const jsonRpcError = error as { code?: number; message?: string; data?: unknown; };
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