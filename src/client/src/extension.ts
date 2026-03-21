import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as fsPromises from 'fs/promises';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind
} from 'vscode-languageclient/node';
import { DotnetBuildController } from './Controllers/DotnetBuildController';
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

async function validateAndGetProjectUri(folderUri: vscode.Uri): Promise<vscode.Uri | undefined> {
  try {
    const entries = await fsPromises.readdir(folderUri.fsPath, { withFileTypes: true });
    
    // Look for .sln, .slnx, or .csproj files directly in the folder (not nested)
    const projectFile = entries.find(entry => 
      entry.isFile() && (
        entry.name.endsWith('.sln') ||
        entry.name.endsWith('.slnx') ||
        entry.name.endsWith('.csproj')
      )
    );
    
    if (projectFile) {
      return vscode.Uri.file(path.join(folderUri.fsPath, projectFile.name));
    }
    
    return undefined;
  } catch (err) {
    return undefined;
  }
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
    'rotbarsch.reqnroll',
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
      const stat = await vscode.workspace.fs.stat(uri);
      const isDirectory = stat.type === vscode.FileType.Directory;
      
      if (isDirectory) {
        const validatedUri = await validateAndGetProjectUri(uri);
        if (!validatedUri) {
          vscode.window.showErrorMessage('This folder must contain a .csproj, .sln, or .slnx file directly (not nested).');
          return;
        }
        uri = validatedUri;
      } else if (!(filePath.endsWith('.feature') || filePath.endsWith('.feature.cs') || filePath.endsWith('.csproj') || filePath.endsWith('.sln') || filePath.endsWith('.slnx'))) {
        vscode.window.showErrorMessage('This command can only be used on .feature, .feature.cs, .csproj, .sln, or .slnx files, or folders containing these files.');
        return;
      }
      try {
        await client.sendRequest('rotbarsch.reqnroll/forceBuild', { referenceFileUri: uri.toString() });
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
      const stat = await vscode.workspace.fs.stat(uri);
      const isDirectory = stat.type === vscode.FileType.Directory;
      
      if (isDirectory) {
        const validatedUri = await validateAndGetProjectUri(uri);
        if (!validatedUri) {
          vscode.window.showErrorMessage('This folder must contain a .csproj, .sln, or .slnx file directly (not nested).');
          return;
        }
        uri = validatedUri;
      } else if (!(filePath.endsWith('.feature') || filePath.endsWith('.feature.cs') || filePath.endsWith('.csproj') || filePath.endsWith('.sln') || filePath.endsWith('.slnx'))) {
        vscode.window.showErrorMessage('This command can only be used on .feature, .feature.cs, .csproj, .sln, or .slnx files, or folders containing these files.');
        return;
      }
      try {
        await client.sendRequest('rotbarsch.reqnroll/forceBuild', { referenceFileUri: uri.toString(), fullRebuild: true });
        vscode.window.showInformationMessage('Reqnroll: Project rebuild triggered.');
      } catch (err) {
        vscode.window.showErrorMessage('Reqnroll: Failed to trigger project rebuild.');
      }
    })
  );

  // Register refresh bindings command for feature files
  context.subscriptions.push(
    vscode.commands.registerCommand('rotbarsch.reqnroll.refreshBindings', async (uri: vscode.Uri) => {
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
        await client.sendRequest('rotbarsch.reqnroll/refreshBindings',{});
        vscode.window.showInformationMessage('Reqnroll: Bindings refreshed.');
      } catch (err) {
        vscode.window.showErrorMessage('Reqnroll: Failed to refresh bindings.');
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
      const stat = await vscode.workspace.fs.stat(uri);
      const isDirectory = stat.type === vscode.FileType.Directory;
      
      if (isDirectory) {
        vscode.window.showInformationMessage('Reqnroll: Test discovery is not applicable for folders.');
        return;
      } else if (!(filePath.endsWith('.feature') || filePath.endsWith('.feature.cs') || filePath.endsWith('.csproj') || filePath.endsWith('.sln') || filePath.endsWith('.slnx'))) {
        vscode.window.showErrorMessage('This command can only be used on .feature, .feature.cs, .csproj, .sln, or .slnx files, or folders containing these files.');
        return;
      }
      try {
        if (filePath.endsWith('.csproj') || filePath.endsWith('.sln') || filePath.endsWith('.slnx')) {
          vscode.window.showInformationMessage('Reqnroll: Test discovery is not applicable for project/solution files.');
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
