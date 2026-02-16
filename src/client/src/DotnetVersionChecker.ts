import * as vscode from 'vscode';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

export class DotnetVersionChecker {
  private static readonly REQUIRED_MAJOR_VERSION = 8;
  private static readonly DOWNLOAD_URL = 'https://dotnet.microsoft.com/download';

  public static async checkVersion(): Promise<boolean> {
    try {
      const { stdout } = await execAsync('dotnet --version');
      const version = stdout.trim();
      const majorVersion = parseInt(version.split('.')[0], 10);
      
      if (majorVersion >= this.REQUIRED_MAJOR_VERSION) {
        return true;
      }
      
      this.showVersionTooOldError(version);
      return false;
    } catch (error) {
      this.showDotnetNotFoundError();
      return false;
    }
  }

  private static showVersionTooOldError(currentVersion: string): void {
    vscode.window.showErrorMessage(
      `Reqnroll extension requires .NET ${this.REQUIRED_MAJOR_VERSION} or higher. Found version ${currentVersion}. Please install .NET ${this.REQUIRED_MAJOR_VERSION} SDK or later from ${this.DOWNLOAD_URL}`,
      'Open Download Page'
    ).then(selection => {
      if (selection === 'Open Download Page') {
        vscode.env.openExternal(vscode.Uri.parse(this.DOWNLOAD_URL));
      }
    });
  }

  private static showDotnetNotFoundError(): void {
    vscode.window.showErrorMessage(
      `Reqnroll extension requires .NET ${this.REQUIRED_MAJOR_VERSION} SDK or higher, but dotnet CLI was not found. Please install .NET ${this.REQUIRED_MAJOR_VERSION} SDK from ${this.DOWNLOAD_URL}`,
      'Open Download Page'
    ).then(selection => {
      if (selection === 'Open Download Page') {
        vscode.env.openExternal(vscode.Uri.parse(this.DOWNLOAD_URL));
      }
    });
  }
}
