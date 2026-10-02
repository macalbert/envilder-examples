import { Envilder } from '@envilder/sdk';
import { createBuilder } from './.modules/aspire.js';

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

await builder
  .addContainer('keyvault', { image: 'nagyesta/lowkey-vault', tag: '7.1.61' })
  .withEnvironment(
    'LOWKEY_ARGS',
    '--server.port=8443 --LOWKEY_VAULT_RELAXED_PORTS=true',
  )
  .withHttpsEndpoint({ port: 8443, targetPort: 8443, name: 'vault' })
  .withHttpEndpoint({ port: 8080, targetPort: 8080, name: 'token' })
  .withHttpHealthCheck({ path: '/ping', endpointName: 'token' });

await builder.build().run();
