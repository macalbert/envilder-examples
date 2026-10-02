import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    globalSetup: './aspire.setup.ts',
    hookTimeout: 300_000,
  },
});
