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
  expect: {
    // Long-running LSP operations (build, refresh bindings) can take up to
    // 90 seconds.  Set the default expect timeout to match so that explicit
    // { timeout: 90_000 } in individual assertions is not silently capped by
    // the actionTimeout (30 s).
    timeout: 90_000,
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
