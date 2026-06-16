import { test, expect } from '@playwright/test';
import { launchVSCode, closeVSCode, VSCodeApp, DEMO_WORKSPACE_PATH } from '../helpers/launch-vscode';

let vscode: VSCodeApp;

test.beforeAll(async () => {
  vscode = await launchVSCode(DEMO_WORKSPACE_PATH);
});

test.afterAll(async () => {
  await closeVSCode(vscode);
});

async function openCommandPalette(page: typeof vscode.page): Promise<void> {
  await page.keyboard.press('Control+Shift+P');
  await page.waitForTimeout(600);
}

async function typeInPalette(page: typeof vscode.page, text: string): Promise<void> {
  await page.keyboard.type(text, { delay: 40 });
  await page.waitForTimeout(800);
}

async function dismissPalette(page: typeof vscode.page): Promise<void> {
  await page.keyboard.press('Escape');
  await page.waitForTimeout(400);
}

async function commandExistsInPalette(page: typeof vscode.page, searchText: string): Promise<boolean> {
  await openCommandPalette(page);
  await typeInPalette(page, searchText);

  // Look for the command in the quick-pick list
  const listItem = page.locator('.quick-input-list .monaco-list-row', { hasText: searchText }).first();
  const found = await listItem.isVisible({ timeout: 5_000 }).catch(() => false);
  await dismissPalette(page);
  return found;
}

test.describe('Command Palette – Reqnroll commands', () => {
  test('Reqnroll: Rebuild project command exists', async () => {
    expect(await commandExistsInPalette(vscode.page, 'Reqnroll: Rebuild project')).toBe(true);
  });

  test('Reqnroll: Rebuild project (full) command exists', async () => {
    expect(await commandExistsInPalette(vscode.page, 'Reqnroll: Rebuild project (full)')).toBe(true);
  });

  test('Reqnroll: Re-run test discovery command exists', async () => {
    expect(await commandExistsInPalette(vscode.page, 'Reqnroll: Re-run test discovery')).toBe(true);
  });

  test('Reqnroll: Refresh bindings command exists', async () => {
    expect(await commandExistsInPalette(vscode.page, 'Reqnroll: Refresh bindings')).toBe(true);
  });
});
