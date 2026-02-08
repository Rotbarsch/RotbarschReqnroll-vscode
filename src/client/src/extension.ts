import * as vscode from 'vscode';
import * as path from 'path';
import * as os from 'os';
import * as fs from 'fs';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient;

function getServerCommand(context: vscode.ExtensionContext): { command: string; args: string[] } {
  const exeName =
    os.platform() === 'win32'
      ? 'Reqnroll.LanguageServer.exe'
      : 'Reqnroll.LanguageServer';

  const buildConfig =
    context.extensionMode === vscode.ExtensionMode.Development
      ? 'debug'
      : 'release';

  const serverPath = context.asAbsolutePath(
    path.join('..','client', 'artifacts', 'lsp', buildConfig, exeName)
  );

  if (!fs.existsSync(serverPath)) {
    throw new Error(`Reqnroll LSP not found at ${serverPath}`);
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

  // Register command handlers for running tests
  context.subscriptions.push(
    vscode.commands.registerCommand('reqnroll.runFeatureTests', async (testClassName: string) => {
      await client.sendRequest('workspace/executeCommand', {
        command: 'reqnroll.runFeatureTests',
        arguments: [testClassName]
      });
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand('reqnroll.runScenarioTest', async (testClassName: string, testMethodName: string) => {
      await client.sendRequest('workspace/executeCommand', {
        command: 'reqnroll.runScenarioTest',
        arguments: [testClassName, testMethodName]
      });
    })
  );

  context.subscriptions.push(client);
  client.start();
}

export function deactivate(): Thenable<void> | undefined {
  return client ? client.stop() : undefined;
}