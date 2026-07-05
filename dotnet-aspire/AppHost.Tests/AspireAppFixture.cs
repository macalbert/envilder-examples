namespace AppHost.Tests;

using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

public sealed class AspireAppFixture : IAsyncLifetime
{
	private DistributedApplication _app = null!;

	public IAmazonSimpleSystemsManagement Ssm { get; private set; } = null!;

	public HttpClient Http { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		var builder =
			await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();

		_app = await builder.BuildAsync();
		await _app.StartAsync();

		await _app.ResourceNotifications.WaitForResourceHealthyAsync("localstack");

		var endpoint = _app.GetEndpoint("localstack");

		Ssm = new AmazonSimpleSystemsManagementClient(
			new BasicAWSCredentials("test", "test"),
			new AmazonSimpleSystemsManagementConfig
			{
				ServiceURL = endpoint.ToString(),
			});

		Http = new HttpClient { BaseAddress = endpoint };
	}

	public async ValueTask DisposeAsync()
	{
		Http?.Dispose();
		Ssm?.Dispose();

		if (_app is not null)
		{
			await _app.DisposeAsync();
		}
	}
}
