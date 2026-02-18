import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient/node';
import { StartBuildParams, BuildResult } from './Models/DotnetBuild.Models';


export class DotnetBuildController {
    private discoveryController?: any;
    private isBuilding = false;

    public constructor(
        private readonly client: LanguageClient
    ) { }

    public setupBuildTriggers(): void {
        vscode.workspace.onDidSaveTextDocument(async (document) => {
            const fileName = document.fileName;
            if ((fileName.endsWith('.feature') || fileName.endsWith('.feature.cs')) && !document.isDirty) {
                // Wait 1 second before sending build request
                await new Promise(resolve => setTimeout(resolve, 1000));
                try {
                    await this.sendBuildRequest(document.uri.toString());
                    // Run test discovery after build completes
                    if (this.discoveryController && typeof this.discoveryController.discoverTestsForFile === 'function') {
                        await this.discoveryController.discoverTestsForFile(document.uri);
                    }
                } catch (error) {
                    vscode.window.showErrorMessage(this.formatRequestError(error));
                }
            }
        });
    }

    /**
     * Optionally set the discovery controller to enable test discovery after build.
     */
    public setDiscoveryController(controller: any) {
        this.discoveryController = controller;
    }

    private async sendBuildRequest(featureFileUri: string): Promise<BuildResult> {
        // Ensure only one build request is active at a time
        if (this.isBuilding) {
            return Promise.reject('Build request already in progress.');
        }
        this.isBuilding = true;
        try {
            const result = await this.client.sendRequest(
                'rotbarsch.reqnroll/startBuild',
                { featureFileUri } as StartBuildParams
            ) as BuildResult;
            return result;
        } finally {
            this.isBuilding = false;
        }
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
