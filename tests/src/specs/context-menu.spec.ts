/**
 * Context Menu Tests
 *
 * These tests verify that the Reqnroll extension contributes commands to VS
 * Code's context menus as declared in the "menus" section of package.json.
 *
 * Two menus are tested:
 *
 *   1. Editor context menu ("editor/context")
 *      Triggered by right-clicking inside the Monaco editor while a .feature
 *      file is active.  All four Reqnroll commands should appear.
 *      The "when" clause in package.json is:
 *        "when": "resourceExtname == .feature"
 *
 *   2. Explorer context menu ("explorer/context")
 *      Triggered by right-clicking a .feature file entry in the Explorer
 *      sidebar.  The file must be visible in the tree for this to work, so
 *      the test gracefully skips when it cannot find the tree entry.
 */

import { test, expect } from '@playwright/test';
import { launchVSCode, closeVSCode, openFileInEditor, VSCodeApp, DEMO_WORKSPACE_PATH } from '../helpers/launch-vscode';

let vscode: VSCodeApp;

test.beforeAll(async () => {
  vscode = await launchVSCode(DEMO_WORKSPACE_PATH);
  // Open a .feature file so the editor "when" clause (resourceExtname == .feature)
  // evaluates to true and the Reqnroll commands appear in the editor context menu.
  await openFileInEditor(vscode.page, 'FirstFeature.feature');
  await vscode.page.waitForTimeout(2_000);
});

test.afterAll(async () => {
  await closeVSCode(vscode);
});

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Context Menu – Editor', () => {
  test('at least one Reqnroll command appears in editor context menu', async () => {
    // Intention: right-clicking inside the Monaco editor while a .feature file
    // is open should show a context menu that contains at least one item whose
    // label starts with "Reqnroll".  This confirms that the "editor/context"
    // contribution from package.json is active.
    const editorContent = vscode.page.locator('.monaco-editor .view-lines').first();
    await editorContent.click({ button: 'right' });
    await vscode.page.waitForTimeout(1_000);

    const menuItem = vscode.page
      .locator('.context-view .action-item', { hasText: /Reqnroll/ })
      .first();

    const isVisible = await menuItem.isVisible({ timeout: 5_000 }).catch(() => false);
    await vscode.page.keyboard.press('Escape');

    expect(isVisible).toBe(true);
  });

  // Check every individual Reqnroll command by its exact label.
  // The labels match the "title" field in package.json "contributes.commands".
  for (const command of [
    'Reqnroll: Rebuild project',
    'Reqnroll: Rebuild project (full)',
    'Reqnroll: Re-run test discovery',
    'Reqnroll: Refresh bindings',
  ]) {
    test(`"${command}" appears individually in editor context menu`, async () => {
      // Intention: each command must appear by its full name so users can
      // identify and click the correct action without ambiguity.
      const editorContent = vscode.page.locator('.monaco-editor .view-lines').first();
      await editorContent.click({ button: 'right' });
      await vscode.page.waitForTimeout(1_000);

      const item = vscode.page
        .locator('.context-view .action-item', { hasText: command })
        .first();

      const isVisible = await item.isVisible({ timeout: 5_000 }).catch(() => false);
      await vscode.page.keyboard.press('Escape');

      expect(isVisible).toBe(true);
    });
  }
});

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Context Menu – Explorer', () => {
  test('Reqnroll commands appear on .feature file in Explorer', async () => {
    // Intention: right-clicking a .feature file in the Explorer sidebar should
    // show Reqnroll commands (contributed via "explorer/context" in package.json
    // with "when": "resourceExtname == .feature").
    //
    // Note: VS Code may not auto-expand the folder tree, so the specific file
    // entry might not be visible.  The test is skipped gracefully in that case.
    await vscode.page.keyboard.press('Control+Shift+E'); // open Explorer sidebar
    await vscode.page.waitForTimeout(1_500);

    // Look for the FirstFeature.feature entry in the file tree
    const featureFile = vscode.page
      .locator('.explorer-item, .monaco-list-row')
      .filter({ hasText: 'FirstFeature.feature' })
      .first();

    const fileVisible = await featureFile.isVisible({ timeout: 8_000 }).catch(() => false);
    if (!fileVisible) {
      // The Explorer may not expand to show the file; skip rather than fail.
      test.skip();
      return;
    }

    await featureFile.click({ button: 'right' });
    await vscode.page.waitForTimeout(1_000);

    const menuItem = vscode.page
      .locator('.context-view .action-item', { hasText: /Reqnroll/ })
      .first();

    const isVisible = await menuItem.isVisible({ timeout: 5_000 }).catch(() => false);
    await vscode.page.keyboard.press('Escape');

    expect(isVisible).toBe(true);
  });
});
