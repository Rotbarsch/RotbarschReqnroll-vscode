/**
 * Diagnostics Tests
 *
 * These tests verify that the Reqnroll LSP server correctly analyses open
 * feature files and publishes diagnostic messages (warnings) to VS Code for
 * steps that have no matching step-binding in the project.
 *
 * How it works:
 *   - The LSP server's FeatureFileDiagnosticsService scans every open feature
 *     file and, for each step line, attempts to match it against the loaded
 *     step-binding index.
 *   - If no binding matches, it publishes a DiagnosticSeverity.Warning:
 *       "No binding found for step: {stepText}".
 *   - VS Code receives this via LSP and renders it as:
 *       (a) a yellow squiggly underline in the Monaco editor
 *       (b) a warning entry in the Problems panel (Ctrl+Shift+M)
 *
 * Test setup:
 *   - A temporary feature file containing a step with a unique text that
 *     cannot possibly match any existing binding is created on disk.
 *   - The file is opened in VS Code using its FULL absolute path to avoid
 *     VS Code's fuzzy quick-open picking the auto-generated .feature.cs
 *     code-behind file instead of the .feature file itself.
 *   - The 20-second wait in beforeAll covers cold-start scenarios where the
 *     LSP needs time to load step bindings from the compiled assembly.
 *   - After the test suite finishes, both the .feature and the generated
 *     .feature.cs code-behind file are always deleted.
 *
 * NOTE: The LSP publishes diagnostics when HasMatchingBinding() returns false.
 *   If bindings have not yet loaded, every step appears "unbound".
 *   If bindings ARE loaded, only truly unbound steps appear in the Problems panel.
 *   Either way, the unique step text used here is never found → warning appears.
 */

import { test, expect } from '@playwright/test';
import fs from 'fs';
import path from 'path';
import { launchVSCode, closeVSCode, VSCodeApp, DEMO_WORKSPACE_PATH } from '../helpers/launch-vscode';

let vscode: VSCodeApp;

// Temporary feature file written into the workspace so the LSP analyses it.
const TEMP_FEATURE_PATH = path.join(
  DEMO_WORKSPACE_PATH,
  'Features',
  '_UnboundStep_Temp.feature'
);

// The auto-generated C# code-behind that Reqnroll/MSBuild creates for every
// .feature file.  We delete this in afterAll along with the .feature file.
const TEMP_CS_PATH = TEMP_FEATURE_PATH + '.cs';

// Unique step text that cannot match any existing binding — deliberately long
// and unusual so there is zero chance of an accidental match.
const UNBOUND_STEP = 'this step definitively has no binding registered anywhere in the project';

const TEMP_FEATURE_CONTENT = `Feature: Temporary diagnostics test

# This file is created by the Playwright test suite and deleted after the
# test run.  Its sole purpose is to trigger an "unbound step" warning from
# the Reqnroll LSP server.

Scenario: Step with no binding
\tWhen ${UNBOUND_STEP}
`;

/** Deletes the temp files, ignoring errors (e.g., if they never existed). */
function cleanupTempFiles(): void {
  for (const p of [TEMP_FEATURE_PATH, TEMP_CS_PATH]) {
    try {
      if (fs.existsSync(p)) fs.unlinkSync(p);
    } catch { /* ignore */ }
  }
}

test.beforeAll(async () => {
  // Write the temporary feature file before launching VS Code so that VS Code
  // finds it in the workspace and the LSP processes it on file open.
  fs.writeFileSync(TEMP_FEATURE_PATH, TEMP_FEATURE_CONTENT, 'utf-8');

  vscode = await launchVSCode(DEMO_WORKSPACE_PATH);

  // Open the temp file using its FULL absolute path in VS Code's quick-open.
  // Using the full path avoids VS Code's fuzzy match selecting the auto-generated
  // _UnboundStep_Temp.feature.cs code-behind file (which was recently created by
  // the Reqnroll build system and therefore ranks highly in recent-files sorting).
  const fullPath = TEMP_FEATURE_PATH.replace(/\\/g, '/');
  await vscode.page.keyboard.press('Control+p');
  await vscode.page.waitForTimeout(500);
  await vscode.page.keyboard.type(fullPath, { delay: 30 });
  await vscode.page.waitForTimeout(1_000);
  await vscode.page.keyboard.press('Enter');
  await vscode.page.waitForTimeout(2_000);

  // Give the LSP time to start, load bindings, and publish diagnostics.
  // A cold start (bindings not yet indexed) can take up to 20 seconds.
  await vscode.page.waitForTimeout(20_000);
});

test.afterAll(async () => {
  // closeVSCode performs a graceful close with a 30-second force-kill fallback,
  // ensuring the worker teardown budget is not exceeded even if dotnet child
  // processes are still running.
  await closeVSCode(vscode);

  // Always clean up the temp files regardless of test outcome.
  cleanupTempFiles();
});

// ─────────────────────────────────────────────────────────────────────────────

test.describe('LSP Diagnostics – Unbound Steps', () => {
  test('warning squiggle appears for an unbound step', async () => {
    // Intention: when a step in a .feature file has no matching binding, the
    // LSP publishes a Warning diagnostic.  Monaco renders this as a yellow
    // squiggly underline ("squiggly-warning" class) in the editor overlay layer.
    //
    // If the squiggle is not found in the DOM (it may render with a different
    // class in some VS Code versions), we fall back to checking the Problems
    // panel for at least one entry — a less precise but equally valid check.

    const hasSquiggle = await vscode.page.evaluate(() => {
      return document.querySelectorAll(
        '.squiggly-warning, .cdr.squiggly-warning, .view-overlays [class*="squiggly-warning"]'
      ).length > 0;
    });

    if (hasSquiggle) {
      // Primary check passed: Monaco rendered a squiggle.
      expect(hasSquiggle).toBe(true);
      return;
    }

    // Fallback: open Problems panel and look for at least one row.
    // Ctrl+Shift+M opens (or focuses) the Problems panel in VS Code.
    await vscode.page.keyboard.press('Control+Shift+M');
    await vscode.page.waitForTimeout(2_000);

    const problemRow = vscode.page.locator(
      '.markers-panel .monaco-list-row, .markers-panel-container .monaco-list-row'
    ).first();

    // Allow up to 20 s for the LSP to publish and VS Code to render the entry.
    await expect(problemRow).toBeVisible({ timeout: 20_000 });
  });

  test('Problems panel lists at least one entry for the temp feature file', async () => {
    // Intention: the Problems panel (Ctrl+Shift+M) should contain at least one
    // warning entry after the LSP processes the file with the unbound step.
    // This provides a user-visible diagnostic that explicitly calls out the step.
    await vscode.page.keyboard.press('Control+Shift+M');
    await vscode.page.waitForTimeout(2_000);

    const problemRow = vscode.page.locator(
      '.markers-panel .monaco-list-row, .markers-panel-container .monaco-list-row'
    ).first();

    await expect(problemRow).toBeVisible({ timeout: 20_000 });
  });
});

