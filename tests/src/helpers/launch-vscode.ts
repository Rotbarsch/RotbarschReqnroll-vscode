import path from 'path';
import fs from 'fs';
import os from 'os';
import { ElectronApplication, Page, _electron as electron } from '@playwright/test';

const EXTENSION_PATH = path.resolve(__dirname, '../../../src/client');
const DEFAULT_WORKSPACE_PATH = path.resolve(__dirname, '../fixtures/workspace');

// Isolated VS Code profile so tests don't pollute the developer's real profile.
// Persisted between runs to avoid slow first-launch setup every time.
const USER_DATA_DIR = path.resolve(__dirname, '../../.vscode-test-profile');

export const DEMO_WORKSPACE_PATH = path.resolve(__dirname, '../../../Demo/Example.NUnit');

export interface VSCodeApp {
  app: ElectronApplication;
  page: Page;
}

/**
 * Returns the path to the locally-installed VS Code executable.
 * Checks the most common installation locations on Windows.
 */
export function getVSCodeExecutablePath(): string {
  const candidates = [
    path.join(os.homedir(), 'AppData', 'Local', 'Programs', 'Microsoft VS Code', 'Code.exe'),
    'C:\\Program Files\\Microsoft VS Code\\Code.exe',
    path.join(process.env['ProgramFiles'] ?? 'C:\\Program Files', 'Microsoft VS Code', 'Code.exe'),
  ];

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      return candidate;
    }
  }
  throw new Error(
    'Could not find a VS Code installation. Tried:\n' + candidates.join('\n')
  );
}

/**
 * Launches a fresh VS Code instance with the local extension build loaded.
 *
 * Using --extensionDevelopmentPath loads the extension from the local source
 * tree (not the published marketplace version).  The extension runs in
 * Development mode, but --wait-for-debugger is now opt-in via the
 * REQNROLL_LSP_DEBUG env var, so the LSP server starts immediately.
 *
 * @param workspacePath  Folder VS Code should open as the workspace.
 *                       Defaults to the Playwright fixture workspace.
 */
export async function launchVSCode(workspacePath: string = DEFAULT_WORKSPACE_PATH): Promise<VSCodeApp> {
  const vscodeExecutablePath = getVSCodeExecutablePath();

  // Ensure the isolated profile directory exists so VS Code doesn't complain
  fs.mkdirSync(USER_DATA_DIR, { recursive: true });

  const app = await electron.launch({
    executablePath: vscodeExecutablePath,
    args: [
      // Use our isolated profile (avoids polluting the developer's real profile)
      `--user-data-dir=${USER_DATA_DIR}`,
      // --extensionDevelopmentPath loads the local build of the extension.
      // --disable-extensions suppresses all other installed extensions so
      // only our extension under test is active.
      '--disable-extensions',
      `--extensionDevelopmentPath=${EXTENSION_PATH}`,
      '--new-window',
      '--no-sandbox',
      '--skip-release-notes',
      '--skip-welcome',
      workspacePath,
    ],
    timeout: 60_000,
  });

  // Wait for the first window to be ready
  const page = await app.firstWindow();
  await page.waitForLoadState('domcontentloaded');

  // Give VS Code a moment to finish startup and activate the extension
  await page.waitForTimeout(5_000);

  return { app, page };
}

/** Closes the VS Code Electron app. */
export async function closeVSCode(vscode: VSCodeApp): Promise<void> {
  await vscode.app.close();
}

/**
 * Opens a file inside the VS Code editor by executing the "Open File" quick-
 * open command via keyboard shortcut (Ctrl+P).
 */
export async function openFileInEditor(page: Page, relativePath: string): Promise<void> {
  await page.keyboard.press('Control+p');
  await page.waitForTimeout(500);
  await page.keyboard.type(relativePath, { delay: 50 });
  await page.waitForTimeout(800);
  await page.keyboard.press('Enter');
  await page.waitForTimeout(2_000);
}

/**
 * Returns the text of the VS Code status-bar language selector.
 * Tries several selectors used across VS Code versions.
 */
export async function getStatusBarLanguage(page: Page): Promise<string> {
  const selectors = [
    '[id="status.editor.mode"]',
    '.statusbar-item[id*="status.editor.mode"]',
    'a[aria-label*="Select Language Mode"]',
  ];

  for (const sel of selectors) {
    const el = page.locator(sel).first();
    if (await el.isVisible({ timeout: 3_000 }).catch(() => false)) {
      return (await el.textContent())?.trim() ?? '';
    }
  }

  // Fallback: any status-bar item that contains "Feature"
  const fallback = page.locator('.statusbar-item').filter({ hasText: 'Feature' }).first();
  if (await fallback.isVisible({ timeout: 3_000 }).catch(() => false)) {
    return (await fallback.textContent())?.trim() ?? '';
  }
  return '';
}

/**
 * Opens the Testing side-bar panel via the command palette.
 */
export async function openTestExplorerPanel(page: Page): Promise<void> {
  await page.keyboard.press('Control+Shift+P');
  await page.waitForTimeout(400);
  await page.keyboard.type('Testing: Focus on Test Explorer View', { delay: 40 });
  await page.waitForTimeout(500);
  await page.keyboard.press('Enter');
  await page.waitForTimeout(2_000);
}

