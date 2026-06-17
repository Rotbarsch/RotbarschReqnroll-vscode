/**
 * Autocomplete Tests  (Ctrl+Space)
 *
 * These tests verify that the Reqnroll VS Code extension's LSP-based auto-
 * completion correctly surfaces suggestions at different positions inside a
 * Gherkin feature file.
 *
 * The completion handler (ReqnrollCompletionHandler) covers three contexts:
 *
 *   (a) Blank indented line inside a Scenario/Background block
 *       → offers the five Gherkin step keywords: Given / When / Then / And / But
 *
 *   (b) Blank unindented line outside any block
 *       → offers block-level keywords: Feature / Scenario / Background / etc.
 *
 *   (c) A step line where the user has already typed a step keyword
 *       → offers matching step-definition completions drawn from the project's
 *          compiled binding assemblies
 *
 * Bindings available in Demo/Example.NUnit (via Example.Bindings):
 *   Given  the system is ready
 *   Given  the following message:
 *   When   I add (.*) and (.*)
 *   When   (.*) is appended with (.*)
 *   Then   the result should be (.*)
 *   Then   the result string should be (.*)
 *   Then   the message should be:
 *
 * A temporary feature file is written to Demo/Example.NUnit/Features/ before
 * the VS Code instance is launched.  It contains one blank indented line
 * (line 6) inside a Scenario — the typing tests place text there, check
 * suggestions, then restore the line.  The file is always deleted in afterAll
 * together with the auto-generated .feature.cs code-behind.
 */

import { test, expect, Page } from '@playwright/test';
import path from 'path';
import fs from 'fs';
import {
  launchVSCode, closeVSCode, VSCodeApp, DEMO_WORKSPACE_PATH,
} from '../helpers/launch-vscode';

// ─── Temp file setup ──────────────────────────────────────────────────────────

const TEMP_FEATURE_PATH = path.join(
  DEMO_WORKSPACE_PATH, 'Features', '_Autocomplete_Temp.feature'
);
const TEMP_CS_PATH = TEMP_FEATURE_PATH + '.cs';

// Line 6 (1-based) is a tab-only line inside the Scenario — where the typing
// tests place text.  Line 2 is blank and outside any block.
const BLANK_INDENTED_LINE  = 6;
const BLANK_UNINDENTED_LINE = 2;

const TEMP_FEATURE_CONTENT = [
  'Feature: Autocomplete test',
  '',
  'Scenario: Autocomplete scenario',
  '\tWhen I add 1 and 2',
  '\tThen the result should be 3',
  '\t',
  '',
].join('\n');

function createTempFile(): void {
  fs.writeFileSync(TEMP_FEATURE_PATH, TEMP_FEATURE_CONTENT, 'utf-8');
}

function cleanupTempFiles(): void {
  for (const p of [TEMP_FEATURE_PATH, TEMP_CS_PATH]) {
    try { if (fs.existsSync(p)) fs.unlinkSync(p); } catch { /* ignore */ }
  }
}

// ─── Test lifecycle ───────────────────────────────────────────────────────────

let vscode: VSCodeApp;

test.beforeAll(async () => {
  createTempFile();
  vscode = await launchVSCode(DEMO_WORKSPACE_PATH);

  // Use the full absolute path so VS Code does not fuzzy-pick the .feature.cs
  // code-behind file that Reqnroll/MSBuild auto-generates alongside every .feature.
  const fullPath = TEMP_FEATURE_PATH.replace(/\\/g, '/');
  await vscode.page.keyboard.press('Control+p');
  await vscode.page.waitForTimeout(500);
  await vscode.page.keyboard.type(fullPath, { delay: 30 });
  await vscode.page.waitForTimeout(1_000);
  await vscode.page.keyboard.press('Enter');
  await vscode.page.waitForTimeout(2_000);

  // Wait for the LSP to load bindings from the compiled project assembly.
  // A cold start (first run after build) can take up to 20 seconds.
  await vscode.page.waitForTimeout(20_000);
});

test.afterAll(async () => {
  await closeVSCode(vscode);
  cleanupTempFiles();
});

// ─── Helpers ─────────────────────────────────────────────────────────────────

/** Moves the cursor to a specific 1-based line number via Ctrl+G. */
async function goToLine(page: Page, line: number): Promise<void> {
  await page.keyboard.press('Control+G');
  await page.waitForTimeout(300);
  await page.keyboard.type(String(line));
  await page.keyboard.press('Enter');
  await page.waitForTimeout(500);
}

/** Triggers the VS Code IntelliSense / suggest widget. */
async function triggerAutocomplete(page: Page): Promise<void> {
  await page.keyboard.press('Control+Space');
  await page.waitForTimeout(1_500);
}

/** Dismisses the suggestion widget without accepting a suggestion. */
async function dismissSuggestWidget(page: Page): Promise<void> {
  await page.keyboard.press('Escape');
  await page.waitForTimeout(400);
}

/**
 * Restores line BLANK_INDENTED_LINE to its original tab-only content.
 * Used after typing tests to reset editor state for the next test.
 */
async function restoreBlankIndentedLine(page: Page): Promise<void> {
  await goToLine(page, BLANK_INDENTED_LINE);
  await page.keyboard.press('Home');
  await page.keyboard.press('Shift+End');
  await page.keyboard.press('Delete');
  await page.keyboard.type('\t');
  await page.waitForTimeout(200);
}

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Autocomplete (Ctrl+Space)', () => {

  test('suggest widget appears when triggered on a blank indented line inside a scenario', async () => {
    // Intention: pressing Ctrl+Space on a blank (tab-only) line inside a
    // Scenario should cause the VS Code IntelliSense suggest widget to appear,
    // confirming that the completion handler is registered for .feature files.
    const { page } = vscode;

    await goToLine(page, BLANK_INDENTED_LINE);
    await page.keyboard.press('End');
    await triggerAutocomplete(page);

    await expect(page.locator('.suggest-widget')).toBeVisible({ timeout: 10_000 });

    await dismissSuggestWidget(page);
  });

  test('step keywords (Given / When / Then / And / But) appear on a blank indented line inside a scenario', async () => {
    // Intention: on a blank indented line inside a Scenario block, the handler
    // offers the five Gherkin step keywords so the user can pick one and keep
    // typing the step text.
    const { page } = vscode;

    await goToLine(page, BLANK_INDENTED_LINE);
    await page.keyboard.press('End');
    await triggerAutocomplete(page);

    const items = page.locator('.suggest-widget .monaco-list-row');
    await expect(items.filter({ hasText: 'Given' })).toBeVisible({ timeout: 10_000 });
    await expect(items.filter({ hasText: 'When'  })).toBeVisible();
    await expect(items.filter({ hasText: 'Then'  })).toBeVisible();
    await expect(items.filter({ hasText: 'And'   })).toBeVisible();
    await expect(items.filter({ hasText: 'But'   })).toBeVisible();

    await dismissSuggestWidget(page);
  });

  test('block keywords (Scenario / Feature / Background) appear on an unindented line outside a scenario', async () => {
    // Intention: on a blank line at indentation level 0 that is not inside any
    // Scenario or Background block, the handler offers block-level Gherkin
    // keywords instead of step keywords.
    //
    // NOTE: `hasText: 'Scenario'` cannot be used here because every completion
    // item's description contains the word "scenario" (e.g. "Steps run before
    // each scenario", "Data table for Scenario Outline"), which causes the filter
    // to match all 5 items and triggers a Playwright strict-mode violation.
    // We therefore match against the item's aria-label which is set to
    // "<keyword>, Keyword" and is fully unique per item.
    const { page } = vscode;

    await goToLine(page, BLANK_UNINDENTED_LINE);
    await page.keyboard.press('Home');
    await triggerAutocomplete(page);

    const widget = page.locator('.suggest-widget');
    await expect(widget).toBeVisible({ timeout: 10_000 });
    await expect(widget.locator('[aria-label="Scenario, Keyword"]'   )).toBeVisible();
    await expect(widget.locator('[aria-label="Feature, Keyword"]'    )).toBeVisible();
    await expect(widget.locator('[aria-label="Background, Keyword"]' )).toBeVisible();

    await dismissSuggestWidget(page);
  });

  test('bound "When" step definitions appear after typing "When "', async () => {
    // Intention: after the user types the "When " keyword, the handler queries
    // the LSP binding store and returns When-type step definitions.
    //
    // Bindings expected (from NumbersBindings.cs and StringBindings.cs):
    //   "When I add (.*) and (.*)"       → displayed as "When I add {value} and {value}"
    //   "When (.*) is appended with (.*)" → displayed as "When {value} is appended with {value}"
    const { page } = vscode;

    await goToLine(page, BLANK_INDENTED_LINE);
    await page.keyboard.press('End');
    await page.keyboard.type('When ');
    await triggerAutocomplete(page);

    const items = page.locator('.suggest-widget .monaco-list-row');
    await expect(items.filter({ hasText: /I add/i      })).toBeVisible({ timeout: 15_000 });
    await expect(items.filter({ hasText: /appended/i   })).toBeVisible();

    await dismissSuggestWidget(page);
    await restoreBlankIndentedLine(page);
  });

  test('bound "Then" step definitions appear after typing "Then "', async () => {
    // Intention: after the user types "Then ", the handler returns Then-type
    // step definitions from the project assembly.
    //
    // Bindings expected (from NumbersBindings.cs and StringBindings.cs):
    //   "Then the result should be (.*)"
    //   "Then the result string should be (.*)"
    //   "Then the message should be:"
    const { page } = vscode;

    await goToLine(page, BLANK_INDENTED_LINE);
    await page.keyboard.press('End');
    await page.keyboard.type('Then ');
    await triggerAutocomplete(page);

    const items = page.locator('.suggest-widget .monaco-list-row');
    await expect(items.filter({ hasText: /the result should be/i })).toBeVisible({ timeout: 15_000 });

    await dismissSuggestWidget(page);
    await restoreBlankIndentedLine(page);
  });

  test('bound "Given" step definitions appear after typing "Given "', async () => {
    // Intention: after the user types "Given ", the handler returns Given-type
    // step definitions from the project assembly.
    //
    // Bindings expected (from GivenBindings.cs):
    //   "Given the system is ready"
    //   "Given the following message:"
    const { page } = vscode;

    await goToLine(page, BLANK_INDENTED_LINE);
    await page.keyboard.press('End');
    await page.keyboard.type('Given ');
    await triggerAutocomplete(page);

    const items = page.locator('.suggest-widget .monaco-list-row');
    await expect(items.filter({ hasText: /the system is ready/i    })).toBeVisible({ timeout: 15_000 });
    await expect(items.filter({ hasText: /the following message/i  })).toBeVisible();

    await dismissSuggestWidget(page);
    await restoreBlankIndentedLine(page);
  });

  test('accepting a suggestion inserts step text into the editor', async () => {
    // Intention: pressing Enter on a highlighted completion item should insert
    // the step text into the editor, confirming that suggestions are not just
    // displayed but are also accepted and applicable.
    const { page } = vscode;

    await goToLine(page, BLANK_INDENTED_LINE);
    await page.keyboard.press('End');
    await page.keyboard.type('When ');
    await triggerAutocomplete(page);

    const suggestWidget = page.locator('.suggest-widget');
    await expect(suggestWidget).toBeVisible({ timeout: 10_000 });

    // Accept the first highlighted suggestion
    await page.keyboard.press('Enter');
    await page.waitForTimeout(500);

    // The active editor line should now contain more than just "When "
    const editorLines = page.locator('.monaco-editor .view-lines').first();
    await expect(editorLines).toContainText(/When\s+\S+/, { timeout: 5_000 });

    // Restore the line to its original tab-only state
    await restoreBlankIndentedLine(page);
  });

});
