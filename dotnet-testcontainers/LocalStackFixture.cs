namespace Envilder.Examples.Tests;

using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Testcontainers.LocalStack;

public sealed class LocalStackFixture : IAsyncLifetime
{
	private LocalStackContainer _container = null!;

	public IAmazonSimpleSystemsManagement Ssm { get; private set; } = null!;

	public HttpClient Http { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		_container = new LocalStackBuilder("localstack/localstack:stable")
			.WithEnvironment(await Env.ResolveFileAsync("envilder.json"))
			.Build();

		await _container.StartAsync();

		Ssm = new AmazonSimpleSystemsManagementClient(
			new BasicAWSCredentials("test", "test"),
			new AmazonSimpleSystemsManagementConfig
			{
				ServiceURL = _container.GetConnectionString(),
			});

		Http = new HttpClient
		{
			BaseAddress = new Uri(_container.GetConnectionString()),
		};
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
