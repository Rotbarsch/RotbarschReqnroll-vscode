import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient/node';
import { StartBuildParams, BuildResult } from './DotnetWatch.Models';


export class DotnetBuildController {
    public constructor(
        private readonly client: LanguageClient
    ) {
    }

    public setupBuildTriggers(): vscode.FileSystemWatcher {
        // Watch for .feature file changes to trigger builds
        const watcher = vscode.workspace.createFileSystemWatcher('**/*.feature');

        watcher.onDidCreate(async (uri) => {
            await this.triggerBuild(uri);
        });

        watcher.onDidChange(async (uri) => {
            await this.triggerBuild(uri);
        });

        // Trigger builds for all existing feature files
        vscode.workspace.findFiles('**/*.feature').then(async (uris) => {
            for (const uri of uris) {
                await this.triggerBuild(uri);
            }
        });

        return watcher;
    }

    private async triggerBuild(uri: vscode.Uri): Promise<void> {
        try {
            const result = await this.sendBuildRequest(uri.toString());
            if (!result.success) {
                console.warn(`Build failed: ${result.message}`);
            }
        } catch (error) {
            console.error(`Build failed for ${uri.toString()}: ${this.formatRequestError(error)}`);
        }
    }

    private async sendBuildRequest(featureFileUri: string): Promise<BuildResult> {
        return await this.client.sendRequest(
            'rotbarsch.reqnroll/startBuild',
            { featureFileUri } as StartBuildParams
        ) as BuildResult;
    }

    private formatRequestError(error: unknown): string {
        if (error && typeof error === 'object') {
            const jsonRpcError = error as { code?: number; message?: string; data?: unknown };
            const codePart = typeof jsonRpcError.code === 'number' ? `code=${jsonRpcError.code}; ` : '';
            const messagePart = jsonRpcError.message ?? (error as Error).message ?? 'Unknown LSP request error';
            const dataPart = jsonRpcError.data !== undefined ? `; data=${JSON.stringify(jsonRpcError.data)}` : '';
            return `${codePart}${messagePart}${dataPart}`;
        }

        return String(error);
    }
}
