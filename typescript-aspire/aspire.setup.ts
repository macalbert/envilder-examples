import { spawn, spawnSync } from 'node:child_process';

export const LOCALSTACK_URL = 'http://localhost:4566';
export const AZURE_PORT = 4577;
export const LOCALSTACK_AZURE_URL = `http://localhost:${AZURE_PORT}`;

export default async function setup(): Promise<() => void> {
  // `npm test` exports its config as npm_config_* env vars; the nested
  // `npm install` that `aspire run` performs would treat them as CLI flags.
  const env = Object.fromEntries(
    Object.entries(process.env).filter(
      ([key]) => !key.toLowerCase().startsWith('npm_config_'),
    ),
  );

  // On Windows `aspire` can be a .cmd shim (dotnet tool), which only cmd.exe runs.
  const [command, args] =
    process.platform === 'win32'
      ? ['cmd.exe', ['/d', '/c', 'aspire', 'run']]
      : ['aspire', ['run']];

  const aspire = spawn(command, args, {
    cwd: import.meta.dirname,
    env,
    stdio: 'ignore',
  });

  await waitUntilOk(`${LOCALSTACK_URL}/_localstack/health`);
  await waitUntilOk(`${LOCALSTACK_AZURE_URL}/_localstack/health`);

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
