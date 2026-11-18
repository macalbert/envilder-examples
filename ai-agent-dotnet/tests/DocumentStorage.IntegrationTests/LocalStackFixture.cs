namespace DocumentStorage.IntegrationTests;

using Amazon.Runtime;
using Amazon.S3;
using Testcontainers.LocalStack;

/// <summary>
/// One LocalStack container for the whole run, used as an empty, stateless S3 host.
/// Each test creates and deletes its own bucket.
/// </summary>
public sealed class LocalStackFixture : IAsyncLifetime
{
	/// <summary>
	/// Supplied by the process environment, for example by
	/// <c>envilder exec --map=envilder.json -- dotnet test ...</c>. The tests never read it from a file.
	/// </summary>
	public const string AuthTokenVariable = "LOCALSTACK_AUTH_TOKEN";

	private LocalStackContainer? _container;

	public IAmazonS3 S3 { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		var token = Environment.GetEnvironmentVariable(AuthTokenVariable);

		if (string.IsNullOrWhiteSpace(token))
		{
			throw new InvalidOperationException(
				$"{AuthTokenVariable} is not set. Run the integration tests through Envilder, which resolves it from AWS SSM: " +
				"npx --no envilder exec --map=envilder.json -- dotnet test --project tests/DocumentStorage.IntegrationTests");
		}

		_container = new LocalStackBuilder("localstack/localstack:stable")
			.WithEnvironment(AuthTokenVariable, token)
			.Build();

		await _container.StartAsync();

		var endpoint = new Uri(_container.GetConnectionString());

		// The S3 client below must never reach a real AWS endpoint, whatever the environment holds.
		if (!endpoint.IsLoopback)
		{
			throw new InvalidOperationException($"Refusing to run S3 tests against a non-local endpoint: {endpoint.Host}");
		}

		// LocalStack accepts any credentials. Explicit ones keep the SDK from falling back to
		// AWS_PROFILE, ~/.aws or other real credentials present in the environment.
		S3 = new AmazonS3Client(
			new BasicAWSCredentials("test", "test"),
			new AmazonS3Config
			{
				ServiceURL = endpoint.ToString(),
				ForcePathStyle = true,
				AuthenticationRegion = "us-east-1",
			});
	}

	public async ValueTask DisposeAsync()
	{
		S3?.Dispose();

		if (_container is not null)
		{
			await _container.DisposeAsync();
		}
	}
}
