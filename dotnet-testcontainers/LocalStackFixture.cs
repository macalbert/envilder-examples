namespace Envilder.Examples.Tests;

using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Testcontainers.LocalStack;

public sealed class LocalStackFixture : IAsyncLifetime
{
	private LocalStackContainer _container = null!;

	public HttpClient Http { get; private set; } = null!;

	public IAmazonSimpleSystemsManagement Ssm { get; private set; } = null!;

	/// <summary>
	/// Aims the AWS client that <c>Env.FromMapFile</c> builds from the default
	/// credential chain at this container, until the scope is disposed.
	/// </summary>
	public IDisposable UseAsDefaultAwsEndpoint() => new EnvironmentScope(new Dictionary<string, string?>
	{
		["AWS_ENDPOINT_URL_SSM"] = _container.GetConnectionString(),
		["AWS_ACCESS_KEY_ID"] = "test",
		["AWS_SECRET_ACCESS_KEY"] = "test",
		["AWS_SESSION_TOKEN"] = null,
		["AWS_PROFILE"] = null,
		["AWS_REGION"] = "us-east-1",
	});

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
