import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient/node';
import { DiscoveredTest, DiscoverTestsParams } from './ReqnrollTestDiscoveryController.Models';


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
        // Watch for .feature file changes and discover tests
        const watcher = vscode.workspace.createFileSystemWatcher('**/*.feature');

        const discoverTestsForUri = async (uri: vscode.Uri) => {
            try {
                const tests = await this.sendDiscoverTestsRequest(uri.toString());
                this.updateTestItems(uri, tests);
            } catch (error) {
                console.error(`Test discovery failed for ${uri.toString()}: ${this.formatRequestError(error)}`);
            }
        };

        watcher.onDidCreate(discoverTestsForUri);
        watcher.onDidChange(discoverTestsForUri);
        watcher.onDidDelete((uri) => {
            this.removeTestItemsForFile(uri);
        });

        // Discover tests in all existing feature files
        vscode.workspace.findFiles('**/*.feature').then(async (uris) => {
            for (const uri of uris) {
                await discoverTestsForUri(uri);
            }
        });

        return watcher;
    }

    private updateTestItems(uri: vscode.Uri, tests: DiscoveredTest[]): void {
        const uriString = uri.toString();
        
        // Remove existing tests for this file (at any level in the hierarchy)
        this.removeTestItemsForFile(uri);

        // Add new tests for this file
        // Handle the special case where all files share the same root (namespace)
        for (const test of tests) {
            if (test.children && test.children.length > 0) {
                // This is a namespace node - check if it already exists
                const existingNamespace = this.controller.items.get(test.id);
                
                if (existingNamespace) {
                    // Namespace exists, add features as children to existing namespace
                    for (const feature of test.children) {
                        this.addTestItem(feature, existingNamespace.children);
                    }
                } else {
                    // Namespace doesn't exist, create it with all its children
                    this.addTestItem(test, this.controller.items);
                }
            } else {
                // Not a hierarchical structure, just add directly
                this.addTestItem(test, this.controller.items);
            }
        }
    }

    private addTestItem(test: DiscoveredTest, parent: vscode.TestItemCollection): void {
        const uri = vscode.Uri.parse(test.uri);
        const range = new vscode.Range(
            new vscode.Position(test.range.startLine, test.range.startCharacter),
            new vscode.Position(test.range.endLine, test.range.endCharacter)
        );

        // Check if item already exists, if so, delete it first to update
        const existingItem = parent.get(test.id);
        if (existingItem) {
            parent.delete(test.id);
        }

        const item = this.controller.createTestItem(test.id, test.label, uri);
        item.range = range;

        if (test.children && test.children.length > 0) {
            for (const child of test.children) {
                this.addTestItem(child, item.children);
            }
        }

        parent.add(item);
    }

    private async sendDiscoverTestsRequest(uri: string): Promise<DiscoveredTest[]> {
        return await this.client.sendRequest(
            'rotbarsch.reqnroll/discoverTests',
            { uri } as DiscoverTestsParams
        ) as DiscoveredTest[];
    }

    private removeTestItemsForFile(uri: vscode.Uri): void {
        const uriString = uri.toString();
        
        // Recursively find and remove all test items that belong to this file
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
