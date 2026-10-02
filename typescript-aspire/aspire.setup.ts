import { spawn, spawnSync } from 'node:child_process';

export const LOCALSTACK_URL = 'http://localhost:4566';
export const KEYVAULT_URL = 'https://localhost:8443';
export const KEYVAULT_TOKEN_URL = 'http://localhost:8080';

export default async function setup(): Promise<() => void> {
  // `npm test` exports its config as npm_config_* env vars; the nested
  // `npm install` that `aspire run` performs would treat them as CLI flags.
  const env = Object.fromEntries(
    Object.entries(process.env).filter(
      ([key]) => !key.toLowerCase().startsWith('npm_config_'),
    ),
  );

  const aspire = spawn('aspire', ['run'], {
    cwd: import.meta.dirname,
    env,
    stdio: 'ignore',
    shell: process.platform === 'win32',
  });

  await waitUntilOk(`${LOCALSTACK_URL}/_localstack/health`);
  await waitUntilOk(`${KEYVAULT_TOKEN_URL}/ping`);

  return () => {
    if (process.platform === 'win32') {
      spawnSync('taskkill', ['/pid', String(aspire.pid), '/t', '/f']);
    } else {
      aspire.kill('SIGINT');
    }
  };
}

async function waitUntilOk(url: string, timeoutMs = 240_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const response = await fetch(url).catch(() => null);
    if (response?.ok) {
      return;
    }
    await new Promise((resolve) => setTimeout(resolve, 2_000));
  }

  throw new Error(`${url} did not answer within ${timeoutMs} ms`);
}
