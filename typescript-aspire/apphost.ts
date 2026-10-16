import { Envilder } from '@envilder/sdk';
import { createBuilder } from './.modules/aspire.js';

const AZURE_PORT = 4577;

const builder = await createBuilder();

const secrets = await Envilder.resolveFile('../envilder.json');
const token = secrets.get('LOCALSTACK_AUTH_TOKEN');
if (!token) {
  throw new Error(
    'LOCALSTACK_AUTH_TOKEN could not be resolved from envilder.json',
  );
}

await builder
  .addContainer('localstack', { image: 'localstack/localstack', tag: 'stable' })
  .withEnvironment('LOCALSTACK_AUTH_TOKEN', token)
  .withHttpEndpoint({ port: 4566, targetPort: 4566 });

// Vault URLs embed the gateway port, so the host port must equal the port the
// emulator listens on inside the container.
await builder
  .addContainer('localstack-azure', {
    image: 'localstack/localstack-azure',
    tag: 'latest',
  })
  .withEnvironment('LOCALSTACK_AUTH_TOKEN', token)
  .withEnvironment('GATEWAY_LISTEN', `0.0.0.0:${AZURE_PORT}`)
  .withEnvironment('LOCALSTACK_HOST', `localhost.localstack.cloud:${AZURE_PORT}`)
  .withHttpEndpoint({ port: AZURE_PORT, targetPort: AZURE_PORT });

await builder.build().run();
