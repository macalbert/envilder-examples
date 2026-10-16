using System.Net;
using System.Net.Sockets;
using Envilder;

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration["LocalStack:UseLocalStack"] = "true";

var localstack = builder.AddLocalStack("localstack", configureContainer: container => container.ContainerImageTag = "stable")
	?? throw new InvalidOperationException(
		"LocalStack is disabled (LocalStack:UseLocalStack).");

var environment = await Env.ResolveFileAsync("envilder.json");

foreach (var (key, value) in environment)
{
	localstack.WithEnvironment(key, value);
}

// Azure vault URLs embed the gateway port, so the host port must equal the
// port the emulator listens on inside the container.
var azurePort = FindFreePort();
var azurePortText = azurePort.ToString(System.Globalization.CultureInfo.InvariantCulture);

var azure = builder.AddContainer("localstack-azure", "localstack/localstack-azure", "latest")
	.WithEnvironment("GATEWAY_LISTEN", $"0.0.0.0:{azurePortText}")
	.WithEnvironment("LOCALSTACK_HOST", $"localhost.localstack.cloud:{azurePortText}")
	.WithHttpEndpoint(port: azurePort, targetPort: azurePort, name: "gateway", isProxied: false)
	.WithHttpHealthCheck("/_localstack/health", endpointName: "gateway");

foreach (var (key, value) in environment)
{
	azure.WithEnvironment(key, value);
}

builder.Build().Run();

static int FindFreePort()
{
	var listener = new TcpListener(IPAddress.Loopback, 0);
	listener.Start();
	try
	{
		return ((IPEndPoint)listener.LocalEndpoint).Port;
	}
	finally
	{
		listener.Stop();
	}
}
