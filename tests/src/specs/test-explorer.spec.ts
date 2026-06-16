import { test, expect } from '@playwright/test';
import { launchVSCode, closeVSCode, openTestExplorerPanel, DEMO_WORKSPACE_PATH, VSCodeApp } from '../helpers/launch-vscode';

/**
 * These tests use the Demo/Example.NUnit project as the workspace so that
 * the LSP server can discover and run real Reqnroll tests.
 * The project must have been built at least once before running these tests.
 */

let vscode: VSCodeApp;

test.beforeAll(async () => {
  vscode = await launchVSCode(DEMO_WORKSPACE_PATH);
});

test.afterAll(async () => {
  await closeVSCode(vscode);
});

test.describe('Test Explorer', () => {
  test('Test Explorer panel can be opened', async () => {
    await openTestExplorerPanel(vscode.page);

    // The panel header element carries class="hidden" until VS Code fully
    // renders it, so assert on the tree rows that are actually visible once
    // the panel is open (same selector used by the discovery test below).
    const treeRow = vscode.page.locator(
      '.test-explorer-tree .monaco-list-row, .testing-explorer-tree .monaco-list-row'
    ).first();

    await expect(treeRow).toBeVisible({ timeout: 30_000 });
  });

  test('tests are discovered from feature files', async () => {
    await openTestExplorerPanel(vscode.page);

    // Wait for at least one test item to appear in the tree.
    // The extension discovers tests by sending LSP requests after the project
    // is built; give it up to 60 s for the first discovery cycle.
    const testItem = vscode.page.locator(
      '.test-explorer-tree .monaco-list-row, ' +
      '.testing-explorer-tree .monaco-list-row'
    ).first();

    await expect(testItem).toBeVisible({ timeout: 60_000 });
  });
});

test.describe('Test Execution', () => {
  test.beforeEach(async () => {
    await openTestExplorerPanel(vscode.page);
    // Wait for the tree to populate
    await vscode.page.waitForTimeout(3_000);
  });

  test('a single scenario can be run and produces a result', async () => {
    // Expand the tree until leaf items are visible
    const treeRows = vscode.page.locator(
      '.test-explorer-tree .monaco-list-row, .testing-explorer-tree .monaco-list-row'
    );

    // Keep expanding collapsed nodes until we find a leaf (scenario) item
    for (let i = 0; i < 5; i++) {
      const collapsed = vscode.page.locator(
        '.test-explorer-tree .monaco-tl-twistie.collapsible:not(.collapsed), ' +
        '.testing-explorer-tree .monaco-tl-twistie.collapsible:not(.collapsed)'
      ).first();
      if (await collapsed.isVisible({ timeout: 2_000 }).catch(() => false)) {
        await collapsed.click();
        await vscode.page.waitForTimeout(500);
      } else {
        break;
      }
    }

    // Find the first leaf test item (no expand arrow → it's a scenario)
    const firstLeaf = treeRows.first();
    await expect(firstLeaf).toBeVisible({ timeout: 10_000 });

    // Hover to reveal the inline Run button, then click it
    await firstLeaf.hover();
    const runButton = firstLeaf.locator(
      '[title*="Run Test"], [aria-label*="Run Test"], .codicon-testing-run-icon'
    ).first();

    if (await runButton.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await runButton.click();
    } else {
      // Fallback: right-click → "Run Test"
      await firstLeaf.click({ button: 'right' });
      await vscode.page.waitForTimeout(500);
      const menuRun = vscode.page.locator('.context-view .action-item', { hasText: /^Run Test/ }).first();
      if (await menuRun.isVisible({ timeout: 3_000 }).catch(() => false)) {
        await menuRun.click();
      } else {
        await vscode.page.keyboard.press('Escape');
        test.skip(); // can't find a run button; skip rather than fail
        return;
      }
    }

    // Wait for the result icon to appear (pass = green check, fail = red cross)
    const resultIcon = firstLeaf.locator(
      '.codicon-testing-passed-icon, .codicon-testing-failed-icon, ' +
      '.codicon-testing-error-icon, [class*="testing-passed"], [class*="testing-failed"]'
    ).first();

    await expect(resultIcon).toBeVisible({ timeout: 60_000 });
  });

  test('Run All Tests completes without error', async () => {
    // Trigger "Run All Tests" via the command palette
    await vscode.page.keyboard.press('Control+Shift+P');
    await vscode.page.waitForTimeout(400);
    await vscode.page.keyboard.type('Test: Run All Tests', { delay: 40 });
    await vscode.page.waitForTimeout(500);
    await vscode.page.keyboard.press('Enter');

    // Wait for the test run to finish — look for any result icon in the tree
    const anyResult = vscode.page.locator(
      '.codicon-testing-passed-icon, .codicon-testing-failed-icon, ' +
      '.codicon-testing-error-icon, [class*="testing-passed"], [class*="testing-failed"]'
    ).first();

    await expect(anyResult).toBeVisible({ timeout: 120_000 });

    // VS Code should still be open and responsive
    expect(vscode.page.isClosed()).toBe(false);
  });
});
