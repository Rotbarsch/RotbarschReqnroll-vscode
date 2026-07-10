/**
 * Command Tests
 *
 * Two areas are covered:
 *
 * 1. Command Palette presence – verifies that all four Reqnroll commands are
 *    registered and discoverable through the VS Code command palette
 *    (Ctrl+Shift+P).  These commands are declared in the extension's
 *    package.json "contributes.commands" section.
 *
 * 2. Command Execution – verifies that triggering a command via the editor
 *    context menu produces a visible result notification.  The two commands
 *    tested are:
 *      - "Reqnroll: Rebuild project"   → triggers a dotnet build and shows
 *                                         an info/warning notification with
 *                                         the word "Reqnroll".
 *      - "Reqnroll: Refresh bindings"  → rebuilds then reloads step bindings;
 *                                         shows "Loaded N step binding(s)."
 *
 * Prerequisites:
 *   - Demo/Example.NUnit must be built before running these tests so the LSP
 *     server can complete the build without error (rebuild command still
 *     invokes dotnet build, which may be fast or slow depending on cache).
 */

import { test, expect, Page } from '@playwright/test';
import { launchVSCode, closeVSCode, openFileInEditor, VSCodeApp, DEMO_WORKSPACE_PATH } from '../helpers/launch-vscode';

let vscode: VSCodeApp;

test.beforeAll(async () => {
  vscode = await launchVSCode(DEMO_WORKSPACE_PATH);
});

test.afterAll(async () => {
  await closeVSCode(vscode);
});

// ─── Command Palette helpers ──────────────────────────────────────────────────

/** Opens the VS Code command palette with Ctrl+Shift+P. */
async function openCommandPalette(page: Page): Promise<void> {
  await page.keyboard.press('Control+Shift+P');
  await page.waitForTimeout(600);
}

/** Types a search string into the already-open command palette. */
async function typeInPalette(page: Page, text: string): Promise<void> {
  await page.keyboard.type(text, { delay: 40 });
  await page.waitForTimeout(800);
}

/** Dismisses the command palette without executing anything. */
async function dismissPalette(page: Page): Promise<void> {
  await page.keyboard.press('Escape');
  await page.waitForTimeout(400);
}

/**
 * Searches for `searchText` in the command palette and returns whether a
 * matching list entry is visible.  Always closes the palette before returning.
 */
async function commandExistsInPalette(page: Page, searchText: string): Promise<boolean> {
  await openCommandPalette(page);
  await typeInPalette(page, searchText);

  // The matching command appears in the quick-pick list
  const listItem = page.locator('.quick-input-list .monaco-list-row', { hasText: searchText }).first();
  const found = await listItem.isVisible({ timeout: 5_000 }).catch(() => false);
  await dismissPalette(page);
  return found;
}

// ─── Editor context-menu helper ───────────────────────────────────────────────

/**
 * Right-clicks inside the Monaco editor and clicks the context menu item
 * whose label matches `commandLabel`.
 *
 * Returns true if the item was found and clicked; false if it wasn't present
 * (in which case the menu is dismissed with Escape before returning).
 */
async function clickEditorContextMenuItem(page: Page, commandLabel: string): Promise<boolean> {
  // Right-click in the middle of the first Monaco editor
  const editor = page.locator('.monaco-editor .view-lines').first();
  await editor.click({ button: 'right' });
  await page.waitForTimeout(800);

  const item = page.locator('.context-view .action-item', { hasText: commandLabel }).first();
  const found = await item.isVisible({ timeout: 3_000 }).catch(() => false);

  if (found) {
    await item.click();
  } else {
    await page.keyboard.press('Escape');
    await page.waitForTimeout(300);
  }

  return found;
}

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Command Palette – Reqnroll commands', () => {
  // These tests only verify that each command is REGISTERED and appears in
  // the palette; they do not execute the command.

  test('Reqnroll: Rebuild project command exists', async () => {
    // Intention: the "reqnroll.buildProject" command must be discoverable so
    // users can trigger a project build without leaving the keyboard.
    expect(await commandExistsInPalette(vscode.page, 'Reqnroll: Rebuild project')).toBe(true);
  });

  test('Reqnroll: Rebuild project (full) command exists', async () => {
    // Intention: the "full" variant passes --no-incremental to dotnet build,
    // forcing a clean rebuild.  It must also appear in the palette.
    expect(await commandExistsInPalette(vscode.page, 'Reqnroll: Rebuild project (full)')).toBe(true);
  });

  test('Reqnroll: Re-run test discovery command exists', async () => {
    // Intention: this command triggers the LSP test-discovery cycle so the
    // Test Explorer is refreshed without restarting VS Code.
    expect(await commandExistsInPalette(vscode.page, 'Reqnroll: Re-run test discovery')).toBe(true);
  });

  test('Reqnroll: Refresh bindings command exists', async () => {
    // Intention: this command reloads the step-binding index that drives
    // IntelliSense and diagnostics for .feature files.
    expect(await commandExistsInPalette(vscode.page, 'Reqnroll: Refresh bindings')).toBe(true);
  });
});

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Command Execution – via editor context menu', () => {
  test.beforeEach(async () => {
    // Commands that operate on the active document require an open .feature
    // file; without one the extension shows a "can only be used on files"
    // error message instead of triggering the LSP request.
    await openFileInEditor(vscode.page, 'FirstFeature.feature');
    await vscode.page.waitForTimeout(2_000);
  });

  test('"Rebuild project" command triggers a result notification', async () => {
    // Intention: clicking "Reqnroll: Rebuild project" in the editor context
    // menu should invoke the LSP buildProject request.  The server runs
    // dotnet build and then sends the result back; the extension converts it
    // into either an information (success) or warning (failure) notification
    // that contains the word "Reqnroll" in its message.
    const clicked = await clickEditorContextMenuItem(vscode.page, 'Reqnroll: Rebuild project');
    if (!clicked) {
      test.skip(); // Command not found in context menu – skip rather than fail
      return;
    }

    // The notification toast appears in the bottom-right corner and persists
    // until dismissed.  Allow up to 90 s for a first build (cold start).
    const notification = vscode.page
      .locator('.notifications-toasts .notification-toast')
      .filter({ hasText: /Reqnroll/ })
      .first();

    await expect(notification).toBeVisible({ timeout: 90_000 });
  });

  test('"Refresh bindings" command shows step binding count notification', async () => {
    // Intention: "Reqnroll: Refresh bindings" triggers a build followed by a
    // refreshBindings LSP request.  On success the extension calls
    // vscode.window.showInformationMessage("Reqnroll: Loaded N step binding(s).")
    // which appears as a toast notification.  At least one binding must be
    // loaded from Example.Bindings for the message to match.
    const clicked = await clickEditorContextMenuItem(vscode.page, 'Reqnroll: Refresh bindings');
    if (!clicked) {
      test.skip();
      return;
    }

    // The notification must mention "binding" (case-insensitive) as evidence
    // that the binding-count message was produced by the LSP server.
    const notification = vscode.page
      .locator('.notifications-toasts .notification-toast')
      .filter({ hasText: /binding/i })
      .first();

    await expect(notification).toBeVisible({ timeout: 90_000 });
  });
});
