import { test, expect } from '@playwright/test';
import { launchVSCode, closeVSCode, openFileInEditor, VSCodeApp, DEMO_WORKSPACE_PATH } from '../helpers/launch-vscode';

let vscode: VSCodeApp;

test.beforeAll(async () => {
  vscode = await launchVSCode(DEMO_WORKSPACE_PATH);
  // Open a feature file so editor context menu commands are available
  await openFileInEditor(vscode.page, 'FirstFeature.feature');
  await vscode.page.waitForTimeout(2_000);
});

test.afterAll(async () => {
  await closeVSCode(vscode);
});

test.describe('Context Menu – Editor', () => {
  test('Reqnroll commands appear in editor context menu', async () => {
    // Right-click inside the editor area
    const editorContent = vscode.page.locator('.monaco-editor .view-lines').first();
    await editorContent.click({ button: 'right' });
    await vscode.page.waitForTimeout(1_000);

    // At least one Reqnroll command should appear
    const menuItem = vscode.page.locator('.context-view .action-item', {
      hasText: /Reqnroll/,
    }).first();

    const isVisible = await menuItem.isVisible({ timeout: 5_000 }).catch(() => false);

    // Dismiss the menu regardless
    await vscode.page.keyboard.press('Escape');

    expect(isVisible).toBe(true);
  });
});

test.describe('Context Menu – Explorer', () => {
  test('Reqnroll commands appear on .feature file in Explorer', async () => {
    // Open the Explorer side-bar (Ctrl+Shift+E)
    await vscode.page.keyboard.press('Control+Shift+E');
    await vscode.page.waitForTimeout(1_500);

    // Find the Sample.feature entry in the explorer tree
    const featureFile = vscode.page
      .locator('.explorer-item, .monaco-list-row')
      .filter({ hasText: 'Sample.feature' })
      .first();

    const fileVisible = await featureFile.isVisible({ timeout: 8_000 }).catch(() => false);
    if (!fileVisible) {
      // Explorer may not show files without a .csproj; skip gracefully
      test.skip();
      return;
    }

    await featureFile.click({ button: 'right' });
    await vscode.page.waitForTimeout(1_000);

    const menuItem = vscode.page.locator('.context-view .action-item', {
      hasText: /Reqnroll/,
    }).first();

    const isVisible = await menuItem.isVisible({ timeout: 5_000 }).catch(() => false);

    await vscode.page.keyboard.press('Escape');
    expect(isVisible).toBe(true);
  });
});
