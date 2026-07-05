import {
  type ChildProcess,
  spawn,
  spawnSync,
} from 'node:child_process';
import {
  GetParameterCommand,
  PutParameterCommand,
  SSMClient,
} from '@aws-sdk/client-ssm';
import { afterAll, beforeAll, expect, it } from 'vitest';

const LOCALSTACK_URL = 'http://localhost:4566';

let aspire: ChildProcess;
let ssm: SSMClient;

beforeAll(async () => {
  aspire = spawn('aspire', ['run'], {
    cwd: import.meta.dirname,
    stdio: 'ignore',
    shell: process.platform === 'win32',
  });

  await waitForLocalStack(`${LOCALSTACK_URL}/_localstack/health`, 180_000);

  ssm = new SSMClient({
    endpoint: LOCALSTACK_URL,
    region: 'us-east-1',
    credentials: { accessKeyId: 'test', secretAccessKey: 'test' },
  });
}, 240_000);

afterAll(() => {
  if (aspire?.pid === undefined) {
    return;
  }

  if (process.platform === 'win32') {
    spawnSync('taskkill', ['/pid', String(aspire.pid), '/t', '/f']);
  } else {
    aspire.kill('SIGINT');
  }
});

it('Should_ReportSsmService_When_AspireStartsLocalStack', async () => {
  // Act
  const response = await fetch(`${LOCALSTACK_URL}/_localstack/health`);
  const health = await response.json();

  // Assert
  expect(['available', 'running']).toContain(health.services.ssm);
});

it('Should_RoundTripSecureString_When_AspireInjectsResolvedToken', async () => {
  // Arrange
  await ssm.send(
    new PutParameterCommand({
      Name: '/demo/secret',
      Value: 'hunter2',
      Type: 'SecureString',
      Overwrite: true,
    }),
  );

  // Act
  const actual = await ssm.send(
    new GetParameterCommand({ Name: '/demo/secret', WithDecryption: true }),
  );

  // Assert
  expect(actual.Parameter?.Value).toBe('hunter2');
});

async function waitForLocalStack(
  url: string,
  timeoutMs: number,
): Promise<void> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const response = await fetch(url).catch(() => null);
    if (response?.ok) {
      return;
    }
    await new Promise((resolve) => setTimeout(resolve, 2_000));
  }

  throw new Error(`LocalStack did not become ready within ${timeoutMs} ms`);
}
