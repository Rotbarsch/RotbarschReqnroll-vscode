import { test, expect } from '@playwright/test';
import { launchVSCode, closeVSCode, openFileInEditor, VSCodeApp, DEMO_WORKSPACE_PATH } from '../helpers/launch-vscode';

let vscode: VSCodeApp;

test.beforeAll(async () => {
  vscode = await launchVSCode(DEMO_WORKSPACE_PATH);
});

test.afterAll(async () => {
  await closeVSCode(vscode);
});

// Helper: the first Monaco editor's line container.
// Using .first() avoids strict-mode violations when VS Code renders multiple
// editor instances (e.g. minimap, peek view, diff editor).
const editorLines = (page: typeof vscode.page) =>
  page.locator('.monaco-editor .view-lines').first();

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Syntax Highlighting – Language Detection', () => {
  test.beforeEach(async () => {
    await openFileInEditor(vscode.page, 'FirstFeature.feature');
  });

  test('feature file is detected as Reqnroll Feature language', async () => {
    // Status-bar language selector (bottom-right corner)
    for (const selector of [
      '[id="status.editor.mode"]',
      '.statusbar-item[id*="status.editor.mode"]',
      'a[aria-label*="Select Language Mode"]',
    ]) {
      const el = vscode.page.locator(selector).first();
      if (await el.isVisible({ timeout: 4_000 }).catch(() => false)) {
        await expect(el).toContainText(/Reqnroll Feature|feature/i);
        return;
      }
    }
    // Fallback: any status-bar item showing "Feature"
    const fallback = vscode.page.locator('.statusbar-item').filter({ hasText: /Feature/i }).first();
    await expect(fallback).toBeVisible({ timeout: 5_000 });
  });

  test('special-chars feature file (apostrophes) opens without crash', async () => {
    await openFileInEditor(vscode.page, 'SpecialChars.feature');

    const editor = vscode.page.locator('.monaco-editor').first();
    await expect(editor).toBeVisible({ timeout: 10_000 });
    await expect(editorLines(vscode.page)).toContainText("d'artagnan", { timeout: 5_000 });
  });
});

test.describe('Syntax Highlighting – Keywords visible in editor', () => {
  // FirstFeature.feature contains: Feature, Scenario, When, Then, @firstTest
  test.describe('FirstFeature.feature', () => {
    test.beforeEach(async () => {
      await openFileInEditor(vscode.page, 'FirstFeature.feature');
      await vscode.page.waitForTimeout(1_500);
    });

    test('Feature: keyword is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('Feature:', { timeout: 10_000 });
    });

    test('Scenario: keyword is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('Scenario:', { timeout: 5_000 });
    });

    test('When step keyword is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('When', { timeout: 5_000 });
    });

    test('Then step keyword is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('Then', { timeout: 5_000 });
    });

    test('tag (@firstTest) is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('@firstTest', { timeout: 5_000 });
    });
  });

  // NumbersOutline.feature contains: Scenario Outline, Examples, parameters <summand1> etc.
  test.describe('NumbersOutline.feature', () => {
    test.beforeEach(async () => {
      await openFileInEditor(vscode.page, 'NumbersOutline.feature');
      await vscode.page.waitForTimeout(1_500);
    });

    test('Scenario Outline: keyword is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('Scenario Outline:', { timeout: 5_000 });
    });

    test('Examples: keyword is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('Examples:', { timeout: 5_000 });
    });

    test('outline parameters (<summand1>) are visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('<summand1>', { timeout: 5_000 });
    });
  });

  // SyntaxShowcase.feature contains: Background, comments, docstrings, quoted strings, tags
  test.describe('SyntaxShowcase.feature', () => {
    test.beforeEach(async () => {
      await openFileInEditor(vscode.page, 'SyntaxShowcase.feature');
      await vscode.page.waitForTimeout(1_500);
    });

    test('Background: keyword is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('Background:', { timeout: 5_000 });
    });

    test('Given step keyword is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('Given', { timeout: 5_000 });
    });

    test('comment lines (# …) are visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('# This feature file demonstrates', { timeout: 5_000 });
    });

    test('triple-quoted docstring markers are visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('"""', { timeout: 5_000 });
    });

    test('double-quoted strings in scenario name are visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('"hello"', { timeout: 5_000 });
    });

    test('tags (@syntax @showcase) are visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('@syntax', { timeout: 5_000 });
      await expect(editorLines(vscode.page)).toContainText('@showcase', { timeout: 5_000 });
    });
  });
});

test.describe('Syntax Highlighting – Grammar is active', () => {
  test.beforeEach(async () => {
    await openFileInEditor(vscode.page, 'FirstFeature.feature');
    await vscode.page.waitForTimeout(1_500);
  });

  test('editor applies coloured syntax tokens', async () => {
    // Monaco assigns classes like mtk2..mtkN to coloured tokens.
    // Any value > 0 confirms the TextMate grammar is wired up correctly.
    const coloredTokens = await vscode.page.evaluate(() => {
      const spans = Array.from(document.querySelectorAll('.monaco-editor .view-lines span'));
      return spans.filter(s => /\bmtk[2-9]\b|\bmtk[1-9]\d+\b/.test(s.className)).length;
    });
    expect(coloredTokens).toBeGreaterThan(0);
  });
});
