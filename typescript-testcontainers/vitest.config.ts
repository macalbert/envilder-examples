import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    globalSetup: './emulators.setup.ts',
    hookTimeout: 300_000,
  },
});
