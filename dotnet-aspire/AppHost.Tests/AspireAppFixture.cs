[assembly: AssemblyFixture(typeof(AppHost.Tests.AspireAppFixture))]

namespace AppHost.Tests;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

public sealed class AspireAppFixture : IAsyncLifetime
{
	// The emulator accepts any service principal in its single all-zero tenant and subscription
	private const string TenantId = "00000000-0000-0000-0000-000000000000";
	private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
	private const string ClientId = "any-app";
	private const string ClientSecret = "dummy";
	private const string ResourceGroup = "envilder-examples";
	private const string Location = "westeurope";

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
		await _app.ResourceNotifications.WaitForResourceHealthyAsync("localstack-azure");

		var localStackUrl = _app.GetEndpoint("localstack");

		LocalStackHttp = new HttpClient { BaseAddress = localStackUrl };

		Ssm = new AmazonSimpleSystemsManagementClient(
			new BasicAWSCredentials("test", "test"),
			new AmazonSimpleSystemsManagementConfig { ServiceURL = localStackUrl.ToString() });

		var port = _app.GetEndpoint("localstack-azure", "gateway").Port;

		// *.localhost.localstack.cloud resolves to 127.0.0.1 and has a valid TLS certificate
		var authorityHost = new Uri($"https://azure.localhost.localstack.cloud:{port}");
		var credential = new ClientSecretCredential(
			TenantId,
			ClientId,
			ClientSecret,
			new ClientSecretCredentialOptions
			{
				AuthorityHost = authorityHost,
				DisableInstanceDiscovery = true,
			});

		var vaultUrl = await CreateVaultAsync(authorityHost, credential);

		Secrets = new SecretClient(new Uri(vaultUrl), credential);
	}

	public async ValueTask DisposeAsync()
	{
		LocalStackHttp?.Dispose();
		Ssm?.Dispose();

		if (_app is not null)
		{
			await _app.DisposeAsync();
		}
	}

	private static async Task<string> CreateVaultAsync(Uri managementUrl, TokenCredential credential)
	{
		var token = await credential.GetTokenAsync(
			new TokenRequestContext(["https://management.azure.com/.default"]),
			CancellationToken.None);

		using var http = new HttpClient { BaseAddress = managementUrl };
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

		var groupPath = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroup}";

		await ArmPutAsync(http, $"{groupPath}?api-version=2021-04-01", new { location = Location });

		var vaultName = $"kv-{Guid.NewGuid():N}"[..11];
		var vault = await ArmPutAsync(
			http,
			$"{groupPath}/providers/Microsoft.KeyVault/vaults/{vaultName}?api-version=2023-07-01",
			new
			{
				location = Location,
				properties = new
				{
					tenantId = TenantId,
					sku = new { family = "A", name = "standard" },
					accessPolicies = Array.Empty<object>(),
				},
			});

		var vaultUri = vault["properties"]?["vaultUri"]?.GetValue<string>()
			?? throw new InvalidOperationException("ARM response did not include properties.vaultUri.");

		return vaultUri.TrimEnd('/');
	}

	private static async Task<JsonNode> ArmPutAsync(HttpClient http, string path, object body)
	{
		using var response = await http.PutAsJsonAsync(path, body);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(
				$"ARM request failed ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
		}

		return await response.Content.ReadFromJsonAsync<JsonNode>()
			?? throw new InvalidOperationException("ARM response body was empty.");
	}
}
