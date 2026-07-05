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
  .addContainer('localstack', 'localstack/localstack', 'stable')
  .withEnvironment('LOCALSTACK_AUTH_TOKEN', token)
  .withHttpEndpoint({ port: 4566, targetPort: 4566 });

await builder.build().run();
