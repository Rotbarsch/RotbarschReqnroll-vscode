import { defineConfig } from '@playwright/test';
import path from 'path';

export default defineConfig({
  testDir: './src/specs',
  timeout: 120_000,
  retries: 1,
  workers: 1, // VS Code electron tests must run serially
  use: {
    actionTimeout: 30_000,
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'vscode-electron',
      use: {
        // Extension development path for the client extension
        // Playwright electron configuration is handled inside tests via helpers
      },
    },
  ],
  outputDir: './test-results',
  reporter: [['list'], ['html', { outputFolder: './playwright-report', open: 'never' }]],
});
