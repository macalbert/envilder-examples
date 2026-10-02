using Envilder;

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration["LocalStack:UseLocalStack"] = "true";

var localstack = builder.AddLocalStack("localstack", configureContainer: container => container.ContainerImageTag = "stable")
	?? throw new InvalidOperationException(
		"LocalStack is disabled (LocalStack:UseLocalStack).");

foreach (var (key, value) in await Env.ResolveFileAsync("envilder.json"))
{
	localstack.WithEnvironment(key, value);
}

builder.AddContainer("keyvault", "nagyesta/lowkey-vault", "7.1.61")
	.WithEnvironment("LOWKEY_ARGS", "--server.port=8443 --LOWKEY_VAULT_RELAXED_PORTS=true")
	.WithHttpsEndpoint(targetPort: 8443, name: "vault")
	.WithHttpEndpoint(targetPort: 8080, name: "token")
	.WithHttpHealthCheck("/ping", endpointName: "token");

builder.Build().Run();
