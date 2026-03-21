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

            // The server returns a root namespace node with features as children
            // Restructure to: Project → Folders → Features → Scenarios
            for (const namespaceRoot of tests) {
                this.addTestItemWithFolderHierarchy(namespaceRoot, this.controller.items, uri);
            }
        } catch (error) {
            console.error(`Test discovery failed for ${uri.toString()}: ${this.formatRequestError(error)}`);
        }
    }

    /**
     * Adds a test item while ensuring proper folder hierarchy.
     * Restructures the namespace-based structure into project/folder/feature hierarchy.
     */
    private addTestItemWithFolderHierarchy(test: DiscoveredTest, parent: vscode.TestItemCollection, uri: vscode.Uri): void {
        // If this is a namespace root (has period-separated ID), parse it to create folder structure
        if (test.id.includes('.') && test.children && test.children.length > 0) {
            // Process children and create folder hierarchy for each
            for (const child of test.children) {
                this.createFolderHierarchyAndAddFeature(child, parent, uri);
            }
        } else {
            // This is a leaf node (scenario, scenario outline row) - add directly
            this.addTestItem(test, parent);
        }
    }

    /**
     * Creates folder hierarchy based on feature ID and adds the feature at the appropriate level.
     * Example: For "Example.MsTest.Features.SubFolder.DeepFolder.ClassName"
     * Creates: Example.MsTest → Features → SubFolder → DeepFolder → [Feature]
     */
    private createFolderHierarchyAndAddFeature(feature: DiscoveredTest, parent: vscode.TestItemCollection, uri: vscode.Uri): void {
        const parts = feature.id.split('.');
        
        // Find where the folder structure starts (look for common folder root names)
        // Common patterns: Features, Tests, Specs, etc.
        const folderRootNames = ['Features', 'Tests', 'Specs', 'Test', 'Feature', 'Specifications'];
        let folderStartIndex = -1;
        
        for (let i = 0; i < parts.length - 1; i++) {
            if (folderRootNames.some(name => parts[i].toLowerCase() === name.toLowerCase())) {
                folderStartIndex = i;
                break;
            }
        }
        
        // If we didn't find a folder root, assume the project is the first part only
        if (folderStartIndex === -1) {
            folderStartIndex = 1; // Skip first part (project name)
        }
        
        let currentCollection = parent;
        
        // First, create or find the project node (everything before folder root)
        if (folderStartIndex > 0) {
            const projectId = parts.slice(0, folderStartIndex).join('.');
            const projectLabel = parts.slice(0, folderStartIndex).join('.');
            
            let projectItem = currentCollection.get(projectId);
            if (!projectItem) {
                projectItem = this.controller.createTestItem(projectId, projectLabel, uri);
                projectItem.range = new vscode.Range(0, 0, 0, 0);
                currentCollection.add(projectItem);
            }
            currentCollection = projectItem.children;
        }
        
        // Create folder nodes from folder root to the parent of the class
        for (let i = folderStartIndex; i < parts.length - 1; i++) {
            const segmentId = parts.slice(0, i + 1).join('.');
            const segmentLabel = parts[i];
            
            let folderItem = currentCollection.get(segmentId);
            if (!folderItem) {
                folderItem = this.controller.createTestItem(segmentId, segmentLabel, uri);
                folderItem.range = new vscode.Range(0, 0, 0, 0);
                currentCollection.add(folderItem);
            }
            
            currentCollection = folderItem.children;
        }
        
        // Now add the actual feature (and its children) at the appropriate level
        this.addTestItem(feature, currentCollection);
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

        // store manually set tags in the test item
        test.tags.forEach(tag => {
            tags.push(new vscode.TestTag(tag));
        });

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