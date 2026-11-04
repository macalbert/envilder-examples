namespace Envilder.Examples.Tests;

using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Testcontainers.LocalStack;

public sealed class LocalStackFixture : IAsyncLifetime
{
	private LocalStackContainer _container = null!;

	public HttpClient Http { get; private set; } = null!;

	public IAmazonSimpleSystemsManagement Ssm { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		var secrets = await Env.ResolveFileAsync("envilder.json");

		_container = new LocalStackBuilder("localstack/localstack:stable")
			.WithEnvironment(secrets)
			.Build();

		await _container.StartAsync();

		var url = _container.GetConnectionString();

		Http = new HttpClient { BaseAddress = new Uri(url) };

		Ssm = new AmazonSimpleSystemsManagementClient(
			new BasicAWSCredentials("test", "test"),
			new AmazonSimpleSystemsManagementConfig { ServiceURL = url });
	}

	public async ValueTask DisposeAsync()
	{
		Http?.Dispose();
		Ssm?.Dispose();

		if (_container is not null)
		{
			await _container.DisposeAsync();
		}
	}
}
