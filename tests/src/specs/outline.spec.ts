/**
 * Document Outline Tests
 *
 * These tests verify that the Reqnroll VS Code extension's LSP document-symbol
 * handler (ReqnrollDocumentSymbolHandler) correctly exposes the Gherkin
 * structure of a feature file as navigable symbols.
 *
 * VS Code consumes document symbols in two ways:
 *
 *   (a) The "Outline" panel in the Explorer sidebar shows the full symbol tree
 *       for the currently active editor.  It mirrors the hierarchy produced by
 *       the handler: Feature → Scenario / Background / Scenario Outline →
 *       Examples.
 *
 *   (b) The "Go to Symbol in Editor" quick-pick (Ctrl+Shift+O) lists all
 *       symbols for the current file and lets the user jump to any one of them.
 *
 * Symbol kinds produced by the handler (maps to icons in the Outline panel):
 *   Feature          → SymbolKind.Module
 *   Background       → SymbolKind.Event
 *   Scenario         → SymbolKind.Method
 *   Scenario Outline → SymbolKind.Method
 *   Examples         → SymbolKind.Array
 *
 * Feature files used:
 *   - FirstFeature.feature      — Feature + two Scenarios
 *   - SyntaxShowcase.feature    — Feature + Background + multiple Scenarios
 *   - NumbersOutline.feature    — Feature + Scenario Outline + Examples
 */

import { test, expect, Page } from '@playwright/test';
import {
  launchVSCode, closeVSCode, openFileInEditor, VSCodeApp, DEMO_WORKSPACE_PATH,
} from '../helpers/launch-vscode';

let vscode: VSCodeApp;

test.beforeAll(async () => {
  vscode = await launchVSCode(DEMO_WORKSPACE_PATH);
});

test.afterAll(async () => {
  await closeVSCode(vscode);
});

// ─── Helpers ─────────────────────────────────────────────────────────────────

/**
 * Opens (or focuses) the Outline panel inside the Explorer sidebar.
 *
 * In VS Code the Outline is a collapsible section at the bottom of the
 * Explorer sidebar.  Its header button has `aria-label="Outline Section"`.
 * Clicking it toggles the panel open/closed; we only click if it is closed.
 *
 * After expansion, VS Code requests document symbols from the LSP and renders
 * each symbol as a `role="treeitem"` element accessible by symbol name.
 */
async function openOutlinePanel(page: Page): Promise<void> {
  // Ensure the Explorer sidebar is visible
  await page.keyboard.press('Control+Shift+E');
  await page.waitForTimeout(600);

  // The Outline section header button in the Explorer sidebar.
  // aria-label is the reliable selector (verified via accessibility tree).
  const outlineBtn = page.locator('[aria-label="Outline Section"]');
  await outlineBtn.scrollIntoViewIfNeeded({ timeout: 5_000 }).catch(() => {});

  // Read the current expanded state via aria-expanded.
  // If the section is already open (aria-expanded="true"), clicking would
  // collapse it — so we only click when it is closed.
  const expanded = await outlineBtn
    .getAttribute('aria-expanded', { timeout: 3_000 })
    .catch(() => null);

  if (expanded !== 'true') {
    await outlineBtn.click({ timeout: 3_000 }).catch(() => {});
    await page.waitForTimeout(1_500);
  }
}

/**
 * Opens the "Go to Symbol in Editor" quick-pick (Ctrl+Shift+O) and waits for
 * the quick-pick widget to appear.
 */
async function openGoToSymbol(page: Page): Promise<void> {
  await page.keyboard.press('Control+Shift+O');
  await page.waitForTimeout(1_000);
}

/** Dismisses any open quick-pick or dialog. */
async function dismiss(page: Page): Promise<void> {
  await page.keyboard.press('Escape');
  await page.waitForTimeout(400);
}

// Quick-pick items rendered by Ctrl+Shift+O
const quickPickItem = (page: Page) =>
  page.locator('.quick-input-list .monaco-list-row');

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Document Outline – Outline panel (Explorer sidebar)', () => {

  test.beforeEach(async () => {
    // Start each test with FirstFeature.feature open so the Outline panel
    // reflects a predictable, simple file structure.
    await openFileInEditor(vscode.page, 'FirstFeature.feature');
    await openOutlinePanel(vscode.page);
  });

  test('Feature node appears in the Outline panel for FirstFeature.feature', async () => {
    // Intention: the top-level Feature keyword should appear as the root symbol
    // in the Outline panel, confirming that the document-symbol handler is
    // active and producing output for .feature files.
    //
    // VS Code renders outline symbols as role="treeitem" elements with accessible
    // names in the format "<symbolName> (<symbolKind>)".
    // Feature → SymbolKind.Module → "(module)"
    const { page } = vscode;

    await expect(
      page.getByRole('treeitem', { name: 'FirstFeature (module)' }).first()
    ).toBeVisible({ timeout: 15_000 });
  });

  test('Scenario nodes appear as children of the Feature node', async () => {
    // Intention: each Scenario: block becomes a child symbol of the Feature.
    // FirstFeature.feature has two scenarios: "Addition" and "Another Addition".
    // Scenario → SymbolKind.Method → "(method)"
    const { page } = vscode;

    await expect(
      page.getByRole('treeitem', { name: 'Addition (method)' }).first()
    ).toBeVisible({ timeout: 15_000 });

    await expect(
      page.getByRole('treeitem', { name: 'Another Addition (method)' }).first()
    ).toBeVisible({ timeout: 10_000 });
  });

  test('Background node appears in the Outline panel for SyntaxShowcase.feature', async () => {
    // Intention: a Background: block should appear as a separate symbol under
    // the Feature, allowing navigation directly to the common precondition steps.
    // SyntaxShowcase.feature has "Background: Common setup".
    // Background → SymbolKind.Event → "(event)"
    const { page } = vscode;

    await openFileInEditor(page, 'SyntaxShowcase.feature');
    await page.waitForTimeout(1_500);

    await expect(
      page.getByRole('treeitem', { name: 'Common setup (event)' }).first()
    ).toBeVisible({ timeout: 15_000 });
  });

  test('Scenario Outline node appears in the Outline panel for NumbersOutline.feature', async () => {
    // Intention: a Scenario Outline: block is given SymbolKind.Method (same as
    // a regular Scenario) but with its own name in the outline, letting the user
    // distinguish parameterised scenarios from regular ones.
    // NumbersOutline.feature has "Scenario Outline: Addition with Examples".
    // Scenario Outline → SymbolKind.Method → "(method)"
    const { page } = vscode;

    await openFileInEditor(page, 'NumbersOutline.feature');
    await page.waitForTimeout(1_500);

    await expect(
      page.getByRole('treeitem', { name: 'Addition with Examples (method)' }).first()
    ).toBeVisible({ timeout: 15_000 });
  });

});

test.describe('Document Outline – Go to Symbol in Editor (Ctrl+Shift+O)', () => {

  test.beforeEach(async () => {
    await openFileInEditor(vscode.page, 'FirstFeature.feature');
    await vscode.page.waitForTimeout(1_000);
  });

  test('Ctrl+Shift+O opens the symbol quick-pick for FirstFeature.feature', async () => {
    // Intention: Ctrl+Shift+O should trigger the "Go to Symbol" quick-pick
    // which is populated by the LSP document-symbol response.  The widget must
    // appear within a reasonable time, confirming the extension responds to the
    // textDocument/documentSymbol request.
    const { page } = vscode;

    await openGoToSymbol(page);

    const quickInput = page.locator('.quick-input-widget');
    await expect(quickInput).toBeVisible({ timeout: 10_000 });

    await dismiss(page);
  });

  test('Feature name "FirstFeature" appears in the symbol list', async () => {
    // Intention: the Feature symbol (SymbolKind.Module) should be listed in the
    // quick-pick so the user can jump to the Feature declaration line.
    const { page } = vscode;

    await openGoToSymbol(page);

    await expect(
      quickPickItem(page).filter({ hasText: 'FirstFeature' }).first()
    ).toBeVisible({ timeout: 10_000 });

    await dismiss(page);
  });

  test('Scenario names appear in the symbol list', async () => {
    // Intention: both Scenario symbols (SymbolKind.Method) from FirstFeature
    // should be listed, letting the user navigate directly to any scenario.
    const { page } = vscode;

    await openGoToSymbol(page);

    await expect(
      quickPickItem(page).filter({ hasText: 'Addition' }).first()
    ).toBeVisible({ timeout: 10_000 });

    await expect(
      quickPickItem(page).filter({ hasText: 'Another Addition' }).first()
    ).toBeVisible({ timeout: 10_000 });

    await dismiss(page);
  });

  test('selecting a symbol navigates the editor to the correct line', async () => {
    // Intention: pressing Enter on a highlighted completion item should move
    // the editor cursor to the corresponding line, verifying that the symbol
    // range information in the LSP response is correct.
    //
    // "Another Addition" is the second scenario in FirstFeature.feature.
    // After navigation the active editor should display the text "Another Addition".
    const { page } = vscode;

    await openGoToSymbol(page);
    await page.keyboard.type('Another Addition');
    await page.waitForTimeout(500);
    await page.keyboard.press('Enter');
    await page.waitForTimeout(800);

    // Editor cursor is now on the "Another Addition" scenario line
    const editorLines = page.locator('.monaco-editor .view-lines').first();
    await expect(editorLines).toContainText('Another Addition', { timeout: 5_000 });
  });

  test('Background symbol appears in the symbol list for SyntaxShowcase.feature', async () => {
    // Intention: Background: blocks receive SymbolKind.Event and should be
    // listed in the quick-pick as a navigable symbol named "Common setup"
    // (the text after "Background:" in SyntaxShowcase.feature).
    const { page } = vscode;

    await openFileInEditor(page, 'SyntaxShowcase.feature');
    await page.waitForTimeout(1_000);

    await openGoToSymbol(page);

    await expect(
      quickPickItem(page).filter({ hasText: 'Common setup' }).first()
    ).toBeVisible({ timeout: 10_000 });

    await dismiss(page);
  });

  test('Examples symbol appears in the symbol list for NumbersOutline.feature', async () => {
    // Intention: Examples: tables inside a Scenario Outline receive
    // SymbolKind.Array and should also be listed in the quick-pick, providing
    // direct navigation to the data table.
    const { page } = vscode;

    await openFileInEditor(page, 'NumbersOutline.feature');
    await page.waitForTimeout(1_000);

    await openGoToSymbol(page);

    await expect(
      quickPickItem(page).filter({ hasText: 'Examples' }).first()
    ).toBeVisible({ timeout: 10_000 });

    await dismiss(page);
  });

});
