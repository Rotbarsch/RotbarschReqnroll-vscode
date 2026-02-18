import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind
} from 'vscode-languageclient/node';
import { DotnetBuildController } from './DotnetBuildController';
import { ReqnrollTestDiscoveryController } from './Controllers/ReqnrollTestDiscoveryController';
import { ReqnrollTestRunnerController } from './Controllers/ReqnrollTestRunnerController';
import { DotnetVersionChecker } from './DotnetVersionChecker';

let client: LanguageClient;

function getServerCommand(context: vscode.ExtensionContext): { command: string; args: string[] } {
  const exeName = 'Reqnroll.LanguageServer.exe';

  // Try multiple possible locations for the LSP server
  const possiblePaths = [
    // Release build location (when published)
    context.asAbsolutePath(path.join('artifacts', 'lsp', exeName)),
    // Debug build location
    context.asAbsolutePath(path.join('..', 'client', 'artifacts', 'lsp', exeName)),
  ];

  let serverPath: string | undefined;
  for (const possiblePath of possiblePaths) {
    if (fs.existsSync(possiblePath)) {
      serverPath = possiblePath;
      break;
    }
  }

  if (!serverPath) {
    const triedPaths = possiblePaths.join('\n  - ');
    throw new Error(`Reqnroll LSP not found. Tried the following locations:\n  - ${triedPaths}`);
  }

  const args =
    context.extensionMode === vscode.ExtensionMode.Development
      ? ['--wait-for-debugger']
      : [];

  return { command: serverPath, args };
}

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  if (!await DotnetVersionChecker.checkVersion()) {
    return;
  }

  const server = getServerCommand(context);

  const serverOptions: ServerOptions = {
    command: server.command,
    args: server.args,
    transport: TransportKind.stdio
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'reqnroll-feature' }],
    outputChannelName: 'Reqnroll Language Server',
    synchronize: {
      configurationSection: 'reqnroll'
    },
    initializationOptions: {}
  };

  client = new LanguageClient(
    'reqnrollLanguageServer',
    'Reqnroll Language Server',
    serverOptions,
    clientOptions
  );

  context.subscriptions.push(client);
  await client.start();

  // Setup test discovery
  const discoveryController = new ReqnrollTestDiscoveryController(
    client,
    'rotbarsch.reqnrollController',
    'Reqnroll'
  );
  const discoveryWatcher = discoveryController.setupTestDiscovery();

  // Setup test runner
  const discoveryControllerInstance = discoveryController.getController();
  const runnerController = new ReqnrollTestRunnerController(client);
  const runProfile = runnerController.activate(discoveryControllerInstance);

  context.subscriptions.push(discoveryControllerInstance, discoveryWatcher, runProfile);

  // Setup DotnetBuildController file watcher for feature file saves
  const buildController = new DotnetBuildController(client);
  buildController.setDiscoveryController(discoveryController);
  buildController.setupBuildTriggers();

  // Register rebuild command for feature files
  context.subscriptions.push(
    vscode.commands.registerCommand('rotbarsch.reqnroll.forceRebuildProject', async (uri: vscode.Uri) => {
      if (!uri || uri.scheme !== 'file') {
        vscode.window.showErrorMessage('This command can only be used on files.');
        return;
      }
      const filePath = uri.fsPath;
      if (!(filePath.endsWith('.feature') || filePath.endsWith('.feature.cs') || filePath.endsWith('.csproj'))) {
        vscode.window.showErrorMessage('This command can only be used on .feature, .feature.cs, or .csproj files.');
        return;
      }
      try {
        await client.sendRequest('rotbarsch.reqnroll/forceBuild', { featureFileUri: uri.toString() });
        vscode.window.showInformationMessage('Reqnroll: Project rebuild triggered.');
      } catch (err) {
        vscode.window.showErrorMessage('Reqnroll: Failed to trigger project rebuild.');
      }
    })
  );

  // Register full rebuild command for feature files
  context.subscriptions.push(
    vscode.commands.registerCommand('rotbarsch.reqnroll.forceRebuildProjectFull', async (uri: vscode.Uri) => {
      if (!uri || uri.scheme !== 'file') {
        vscode.window.showErrorMessage('This command can only be used on files.');
        return;
      }
      const filePath = uri.fsPath;
      if (!(filePath.endsWith('.feature') || filePath.endsWith('.feature.cs') || filePath.endsWith('.csproj'))) {
        vscode.window.showErrorMessage('This command can only be used on .feature, .feature.cs, or .csproj files.');
        return;
      }
      try {
        await client.sendRequest('rotbarsch.reqnroll/forceBuild', { featureFileUri: uri.toString(), fullRebuild: true });
        vscode.window.showInformationMessage('Reqnroll: Project rebuild triggered.');
      } catch (err) {
        vscode.window.showErrorMessage('Reqnroll: Failed to trigger project rebuild.');
      }
    })
  );

  // Register re-run test discovery command for feature files
  context.subscriptions.push(
    vscode.commands.registerCommand('rotbarsch.reqnroll.rerunTestDiscovery', async (uri: vscode.Uri) => {
      if (!uri || uri.scheme !== 'file') {
        vscode.window.showErrorMessage('This command can only be used on files.');
        return;
      }
      const filePath = uri.fsPath;
      if (!(filePath.endsWith('.feature') || filePath.endsWith('.feature.cs') || filePath.endsWith('.csproj'))) {
        vscode.window.showErrorMessage('This command can only be used on .feature, .feature.cs, or .csproj files.');
        return;
      }
      try {
        if (filePath.endsWith('.csproj')) {
          vscode.window.showInformationMessage('Reqnroll: Test discovery is not applicable for .csproj files.');
          return;
        }
        discoveryController.removeTestItemsForFile(uri);
        await discoveryController.discoverTestsForFile(uri);
        vscode.window.showInformationMessage('Reqnroll: Test discovery re-run for this file.');
      } catch (err) {
        vscode.window.showErrorMessage('Reqnroll: Failed to re-run test discovery.');
      }
    })
  );
}

export function deactivate(): Thenable<void> | undefined {
  return client ? client.stop() : undefined;
}
