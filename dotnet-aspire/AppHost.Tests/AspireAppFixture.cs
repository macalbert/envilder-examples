[assembly: AssemblyFixture(typeof(AppHost.Tests.AspireAppFixture))]

namespace AppHost.Tests;

using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

public sealed class AspireAppFixture : IAsyncLifetime
{
	private DistributedApplication _app = null!;

	public HttpClient LocalStackHttp { get; private set; } = null!;

	public IAmazonSimpleSystemsManagement Ssm { get; private set; } = null!;

	public SecretClient Secrets { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		var builder =
			await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();

		_app = await builder.BuildAsync();
		await _app.StartAsync();

		await _app.ResourceNotifications.WaitForResourceHealthyAsync("localstack");
		await _app.ResourceNotifications.WaitForResourceHealthyAsync("keyvault");

		var localStackUrl = _app.GetEndpoint("localstack");

		LocalStackHttp = new HttpClient { BaseAddress = localStackUrl };

		Ssm = new AmazonSimpleSystemsManagementClient(
			new BasicAWSCredentials("test", "test"),
			new AmazonSimpleSystemsManagementConfig { ServiceURL = localStackUrl.ToString() });

		Environment.SetEnvironmentVariable(
			"IDENTITY_ENDPOINT",
			new Uri(_app.GetEndpoint("keyvault", "token"), "/metadata/identity/oauth2/token").ToString());
		Environment.SetEnvironmentVariable("IDENTITY_HEADER", "dummy");

		var options = new SecretClientOptions(SecretClientOptions.ServiceVersion.V7_2)
		{
			Transport = new HttpClientTransport(new HttpClient(new HttpClientHandler
			{
				ServerCertificateCustomValidationCallback =
					HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
			})),
			DisableChallengeResourceVerification = true,
		};

		Secrets = new SecretClient(
			_app.GetEndpoint("keyvault", "vault"),
			new DefaultAzureCredential(),
			options);
	}

	public async ValueTask DisposeAsync()
	{
		Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", null);
		Environment.SetEnvironmentVariable("IDENTITY_HEADER", null);

		LocalStackHttp?.Dispose();
		Ssm?.Dispose();

		if (_app is not null)
		{
			await _app.DisposeAsync();
		}
	}
}
