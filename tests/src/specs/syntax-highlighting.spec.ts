/**
 * Syntax Highlighting Tests
 *
 * These tests verify that the Reqnroll VS Code extension correctly applies
 * TextMate grammar-based syntax highlighting to Gherkin feature files.
 *
 * Monaco (the VS Code editor engine) assigns CSS classes "mtk1" through "mtkN"
 * to contiguous colour runs in each line:
 *   - mtk1  = the default foreground colour (no specific grammar scope)
 *   - mtkN (N > 1) = a colour assigned by the active theme for a TextMate scope
 *
 * Tests therefore check that:
 *   (a) keywords, tags, parameters, comments etc. receive a non-default class
 *   (b) different grammatical elements receive DIFFERENT classes from each other
 *
 * Feature files used:
 *   - FirstFeature.feature  (Feature/Scenario/step keywords, tags, @firstTest)
 *   - NumbersOutline.feature (Scenario Outline, Examples, <parameters>)
 *   - SyntaxShowcase.feature (Background, comments, docstrings, quoted strings)
 *   - SpecialChars.feature   (apostrophes – must NOT be highlighted as strings)
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

/**
 * Returns the first Monaco editor's line container.
 * Using .first() avoids strict-mode violations when VS Code renders multiple
 * editor instances (e.g. minimap, peek view, diff editor).
 */
const editorLines = (page: typeof vscode.page) =>
  page.locator('.monaco-editor .view-lines').first();

/**
 * Finds a leaf <span> inside the Monaco editor whose text content overlaps
 * with `searchText` and returns its first "mtk..." CSS class (e.g. "mtk3").
 *
 * Monaco splits each visual line into coloured <span> elements; each span
 * may have exactly one "mtkN" class indicating its colour bucket.
 *
 * IMPORTANT: Monaco replaces all ASCII spaces (U+0020) with non-breaking
 * spaces (U+00A0, &nbsp;) in the rendered DOM to prevent browsers from
 * collapsing whitespace.  Both the line text and each span's text must
 * therefore be normalised (U+00A0 → U+0020) before string comparison.
 *
 * Matching strategy:
 *   - First the line is identified: normalised(line.textContent) contains
 *     normalised(searchText).
 *   - Then within that line we look for a span where EITHER:
 *       (a) normalised(spanText) is a substring of normalised(searchText)
 *           — the span is part of our token (e.g. just the opening quote)
 *     OR
 *       (b) normalised(searchText) is a substring of normalised(spanText)
 *           — the span contains our whole token
 *   This bidirectional check handles grammar boundaries where Monaco may
 *   tokenise begin/end patterns into multiple spans.
 *
 * Returns null if the text is not found or if no mtk class is present.
 */
async function getTokenClass(page: Page, searchText: string): Promise<string | null> {
  return await page.evaluate((text) => {
    // Monaco replaces ASCII spaces with non-breaking spaces in DOM textContent.
    const norm = (s: string) => s.replace(/\u00a0/g, ' ');
    const needle = norm(text);

    const lines = Array.from(
      document.querySelectorAll('.monaco-editor .view-lines .view-line')
    );
    for (const line of lines) {
      const lineText = norm(line.textContent ?? '');
      // Skip lines that don't even contain the text
      if (!lineText.includes(needle)) continue;
      // Walk every child span looking for one that overlaps with searchText
      const spans = Array.from(line.querySelectorAll('span'));
      for (const span of spans) {
        const spanText = norm(span.textContent ?? '');
        if (!spanText || spanText.trim() === '') continue;
        // Accept if spanText ⊆ searchText (span is part of our token)
        // or searchText ⊆ spanText (span contains our whole token)
        if (!needle.includes(spanText) && !spanText.includes(needle)) continue;
        const match = span.className.match(/\bmtk\d+\b/);
        if (match) return match[0];
      }
    }
    return null;
  }, searchText);
}

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Syntax Highlighting – Language Detection', () => {
  test.beforeEach(async () => {
    // Open a feature file so VS Code applies language detection
    await openFileInEditor(vscode.page, 'FirstFeature.feature');
  });

  test('feature file is detected as Reqnroll Feature language', async () => {
    // Intention: the extension registers the "reqnrollfeature" language ID with
    // the .feature file extension.  VS Code should display "Reqnroll Feature"
    // (or similar) in the status-bar language selector (bottom-right corner).
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
    // Intention: the grammar's single-quote pattern uses a negative lookbehind
    // (?<![a-zA-Z]) so that d'artagnan is NOT treated as a string delimiter.
    // This test confirms the file can be opened and its content is displayed.
    await openFileInEditor(vscode.page, 'SpecialChars.feature');

    const editor = vscode.page.locator('.monaco-editor').first();
    await expect(editor).toBeVisible({ timeout: 10_000 });
    await expect(editorLines(vscode.page)).toContainText("d'artagnan", { timeout: 5_000 });
  });
});

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Syntax Highlighting – Keywords visible in editor', () => {
  // FirstFeature.feature contains: Feature, Scenario, When, Then, @firstTest
  test.describe('FirstFeature.feature', () => {
    test.beforeEach(async () => {
      await openFileInEditor(vscode.page, 'FirstFeature.feature');
      await vscode.page.waitForTimeout(1_500);
    });

    test('Feature: keyword is visible', async () => {
      // Intention: confirms the file is opened and the top-level Feature
      // keyword is rendered by the Monaco editor.
      await expect(editorLines(vscode.page)).toContainText('Feature:', { timeout: 10_000 });
    });

    test('Scenario: keyword is visible', async () => {
      // Intention: the Scenario block delimiter must be rendered as text.
      await expect(editorLines(vscode.page)).toContainText('Scenario:', { timeout: 5_000 });
    });

    test('When step keyword is visible', async () => {
      // Intention: "When" is one of the five Gherkin step keywords defined
      // in the grammar's "stepKeywords" pattern.
      await expect(editorLines(vscode.page)).toContainText('When', { timeout: 5_000 });
    });

    test('Then step keyword is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('Then', { timeout: 5_000 });
    });

    test('tag (@firstTest) is visible', async () => {
      // Intention: tags (@ prefixed) are a distinct grammar scope
      // (entity.name.tag.reqnroll) and must appear in the rendered output.
      await expect(editorLines(vscode.page)).toContainText('@firstTest', { timeout: 5_000 });
    });
  });

  // NumbersOutline.feature exercises Scenario Outline with <parameters>
  test.describe('NumbersOutline.feature', () => {
    test.beforeEach(async () => {
      await openFileInEditor(vscode.page, 'NumbersOutline.feature');
      await vscode.page.waitForTimeout(1_500);
    });

    test('Scenario Outline: keyword is visible', async () => {
      // Intention: "Scenario Outline:" is an alternative scenario block
      // delimiter; it must be tokenised by the grammar.
      await expect(editorLines(vscode.page)).toContainText('Scenario Outline:', { timeout: 5_000 });
    });

    test('Examples: keyword is visible', async () => {
      // Intention: the Examples table block is unique to Scenario Outlines.
      await expect(editorLines(vscode.page)).toContainText('Examples:', { timeout: 5_000 });
    });

    test('outline parameters (<summand1>) are visible', async () => {
      // Intention: angle-bracket parameters in outline steps (e.g. <summand1>)
      // are rendered by the grammar as "variable.other.reqnroll".
      await expect(editorLines(vscode.page)).toContainText('<summand1>', { timeout: 5_000 });
    });
  });

  // SyntaxShowcase.feature exercises Background, comments, docstrings,
  // double-quoted strings, single-quoted strings, and tags.
  test.describe('SyntaxShowcase.feature', () => {
    test.beforeEach(async () => {
      await openFileInEditor(vscode.page, 'SyntaxShowcase.feature');
      await vscode.page.waitForTimeout(1_500);
    });

    test('Background: keyword is visible', async () => {
      // Intention: the Background block creates a set of steps that run before
      // every scenario.  It must be recognised by the grammar.
      await expect(editorLines(vscode.page)).toContainText('Background:', { timeout: 5_000 });
    });

    test('Given step keyword is visible', async () => {
      await expect(editorLines(vscode.page)).toContainText('Given', { timeout: 5_000 });
    });

    test('comment lines (# …) are visible', async () => {
      // Intention: lines starting with # are comment lines (comment.line.reqnroll).
      await expect(editorLines(vscode.page)).toContainText('# This feature file exercises', { timeout: 5_000 });
    });

    test('triple-quoted docstring markers (""") are visible', async () => {
      // Intention: triple-quoted docstrings (string.quoted.triple.reqnroll)
      // delimit multi-line string arguments passed to step bindings.
      await expect(editorLines(vscode.page)).toContainText('"""', { timeout: 5_000 });
    });

    test('double-quoted strings in scenario name are visible', async () => {
      // Intention: the grammar highlights "quoted" text within step lines
      // as string.quoted.double.reqnroll.
      await expect(editorLines(vscode.page)).toContainText('"hello"', { timeout: 5_000 });
    });

    test("single-quoted string arguments ('hello') are visible", async () => {
      // Intention: the grammar also highlights 'quoted' step arguments as
      // string.quoted.single.reqnroll using a lookbehind (?<![a-zA-Z])'
      // so apostrophes in words (e.g. d'artagnan) are NOT highlighted.
      //
      // 'hello' is on line 29 of SyntaxShowcase.feature — beyond the default
      // viewport (~27 lines visible).  Monaco only renders lines in the DOM that
      // are currently visible, so we must scroll to line 29 with Ctrl+G first;
      // otherwise toContainText never finds the text and times out.
      await vscode.page.keyboard.press('Control+G');
      await vscode.page.waitForTimeout(300);
      await vscode.page.keyboard.type('29', { delay: 30 });
      await vscode.page.keyboard.press('Enter');
      await vscode.page.waitForTimeout(800);

      await expect(editorLines(vscode.page)).toContainText("'hello'", { timeout: 5_000 });
    });

    test('tags (@syntax @showcase) are visible', async () => {
      // Intention: multiple tags on the same line must all be rendered.
      await expect(editorLines(vscode.page)).toContainText('@syntax', { timeout: 5_000 });
      await expect(editorLines(vscode.page)).toContainText('@showcase', { timeout: 5_000 });
    });
  });
});

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Syntax Highlighting – Grammar is active', () => {
  test.beforeEach(async () => {
    await openFileInEditor(vscode.page, 'FirstFeature.feature');
    await vscode.page.waitForTimeout(1_500);
  });

  test('editor applies coloured syntax tokens', async () => {
    // Intention: if the grammar is NOT active, Monaco uses only mtk1 (the
    // plain foreground colour).  The presence of any mtkN (N > 1) confirms
    // that the TextMate grammar has been loaded and tokenised the file.
    const coloredTokens = await vscode.page.evaluate(() => {
      const spans = Array.from(document.querySelectorAll('.monaco-editor .view-lines span'));
      return spans.filter(s => /\bmtk[2-9]\b|\bmtk[1-9]\d+\b/.test(s.className)).length;
    });
    expect(coloredTokens).toBeGreaterThan(0);
  });
});

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Syntax Highlighting – Token Colours', () => {
  // These tests verify that specific token types receive a non-default colour
  // (i.e. a class other than "mtk1") and that different token types receive
  // different colour classes from each other.

  test.describe('FirstFeature.feature token colours', () => {
    test.beforeEach(async () => {
      await openFileInEditor(vscode.page, 'FirstFeature.feature');
      await vscode.page.waitForTimeout(2_000); // grammar tokenisation can be slow on first open
    });

    test('"Feature:" keyword has a non-default colour', async () => {
      // Intention: keyword.control.reqnroll scope should be mapped to a theme
      // colour that differs from the plain foreground (mtk1).
      const cls = await getTokenClass(vscode.page, 'Feature:');
      expect(cls).not.toBeNull();
      expect(cls).not.toBe('mtk1');
    });

    test('"Scenario:" keyword has a non-default colour', async () => {
      const cls = await getTokenClass(vscode.page, 'Scenario:');
      expect(cls).not.toBeNull();
      expect(cls).not.toBe('mtk1');
    });

    test('"@firstTest" tag has a non-default colour', async () => {
      // Intention: entity.name.tag.reqnroll should be coloured differently
      // from plain text so tags are visually distinct in the editor.
      const cls = await getTokenClass(vscode.page, '@firstTest');
      expect(cls).not.toBeNull();
      expect(cls).not.toBe('mtk1');
    });

    test('"Feature:" and "@firstTest" have different colours', async () => {
      // Intention: keywords and tags are different TextMate scopes and a
      // well-configured theme should assign them distinct colours.
      const keywordClass = await getTokenClass(vscode.page, 'Feature:');
      const tagClass = await getTokenClass(vscode.page, '@firstTest');
      expect(keywordClass).not.toBeNull();
      expect(tagClass).not.toBeNull();
      expect(keywordClass).not.toBe(tagClass);
    });
  });

  test.describe('NumbersOutline.feature token colours', () => {
    test.beforeEach(async () => {
      await openFileInEditor(vscode.page, 'NumbersOutline.feature');
      await vscode.page.waitForTimeout(2_000);
    });

    test('"<summand1>" outline parameter has a non-default colour', async () => {
      // Intention: variable.other.reqnroll scope should be highlighted so
      // parameters stand out from the surrounding step text.
      const cls = await getTokenClass(vscode.page, '<summand1>');
      expect(cls).not.toBeNull();
      expect(cls).not.toBe('mtk1');
    });
  });

  test.describe('SyntaxShowcase.feature token colours', () => {
    test.beforeEach(async () => {
      await openFileInEditor(vscode.page, 'SyntaxShowcase.feature');
      // Scroll to the top so line 1 (Feature:) is always in the viewport.
      // Individual tests that need a lower line navigate with Ctrl+G.
      await vscode.page.keyboard.press('Control+Home');
      await vscode.page.waitForTimeout(2_000);
    });

    test('comment line has a non-default colour', async () => {
      // Intention: comment.line.reqnroll should appear in a muted colour that
      // is different from keyword and step text.
      const cls = await getTokenClass(vscode.page, '# This feature file exercises');
      expect(cls).not.toBeNull();
      expect(cls).not.toBe('mtk1');
    });

    test('triple-quote docstring marker (""") has a non-default colour', async () => {
      // Intention: string.quoted.triple.reqnroll should be visually distinct
      // so docstring boundaries are easy to spot.
      const cls = await getTokenClass(vscode.page, '"""');
      expect(cls).not.toBeNull();
      expect(cls).not.toBe('mtk1');
    });

    test("single-quoted string ('hello') has a non-default colour", async () => {
      // Intention: string.quoted.single.reqnroll should receive a colour so
      // single-quoted step arguments are identifiable at a glance.
      // The grammar prevents apostrophes in words (e.g. d'artagnan) from
      // triggering this scope via a negative lookbehind.
      //
      // 'hello' is on line 29 of SyntaxShowcase.feature.  Navigate there
      // explicitly so Monaco renders the line in the DOM before we inspect it.
      await vscode.page.keyboard.press('Control+G');
      await vscode.page.waitForTimeout(300);
      await vscode.page.keyboard.type('29', { delay: 30 });
      await vscode.page.keyboard.press('Enter');
      await vscode.page.waitForTimeout(800);

      const cls = await getTokenClass(vscode.page, "'hello'");
      expect(cls).not.toBeNull();
      expect(cls).not.toBe('mtk1');
    });

    test('"Feature:" keyword and comment line have different colours', async () => {
      // Intention: keywords and comments are semantically unrelated and a good
      // theme should give them different colours.
      const keywordClass = await getTokenClass(vscode.page, 'Feature:');
      const commentClass = await getTokenClass(vscode.page, '# This feature file exercises');
      expect(keywordClass).not.toBeNull();
      expect(commentClass).not.toBeNull();
      expect(keywordClass).not.toBe(commentClass);
    });

    test('"Feature:" keyword and @syntax tag have different colours', async () => {
      // Intention: keywords (keyword.control.reqnroll) and tags
      // (entity.name.tag.reqnroll) are different TextMate scopes and should
      // receive distinct theme colours.
      const keywordClass = await getTokenClass(vscode.page, 'Feature:');
      const tagClass = await getTokenClass(vscode.page, '@syntax');
      expect(keywordClass).not.toBeNull();
      expect(tagClass).not.toBeNull();
      expect(keywordClass).not.toBe(tagClass);
    });
  });
});
