import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind
} from 'vscode-languageclient/node';

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

export function activate(context: vscode.ExtensionContext) {
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
  client.start();
}

export function deactivate(): Thenable<void> | undefined {
  return client ? client.stop() : undefined;
}