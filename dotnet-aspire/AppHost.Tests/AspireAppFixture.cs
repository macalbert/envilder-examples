[assembly: AssemblyFixture(typeof(AppHost.Tests.AspireAppFixture))]

namespace AppHost.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
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

	// These would make DefaultAzureCredential pick another credential before the managed identity one
	private static readonly string[] CompetingAzureEnvVars =
	[
		"AZURE_TENANT_ID",
		"AZURE_CLIENT_ID",
		"AZURE_CLIENT_SECRET",
		"AZURE_CLIENT_CERTIFICATE_PATH",
		"AZURE_USERNAME",
		"AZURE_PASSWORD",
		"AZURE_FEDERATED_TOKEN_FILE",
		"AZURE_AUTHORITY_HOST",
		"AZURE_TOKEN_CREDENTIALS",
	];

	private static readonly string[] IdentityEnvVars = ["IDENTITY_ENDPOINT", "IDENTITY_HEADER"];

	private DistributedApplication _app = null!;

	private ManagedIdentityEndpointStub? _identityEndpoint;

	private (string Name, string? Value)[] _originalEnvValues = [];

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

		// Lets DefaultAzureCredential reach the emulator through its ManagedIdentityCredential
		_identityEndpoint = new ManagedIdentityEndpointStub(credential, FindFreePort());
		_originalEnvValues =
		[
			.. CompetingAzureEnvVars.Concat(IdentityEnvVars)
				.Select(v => (Name: v, Value: Environment.GetEnvironmentVariable(v))),
		];

		foreach (var name in CompetingAzureEnvVars)
		{
			Environment.SetEnvironmentVariable(name, null);
		}

		Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", _identityEndpoint.Endpoint.AbsoluteUri);
		Environment.SetEnvironmentVariable("IDENTITY_HEADER", _identityEndpoint.IdentityHeader);

		Secrets = new SecretClient(new Uri(vaultUrl), new DefaultAzureCredential());
	}

	public async ValueTask DisposeAsync()
	{
		foreach (var (name, value) in _originalEnvValues)
		{
			Environment.SetEnvironmentVariable(name, value);
		}

		if (_identityEndpoint is not null)
		{
			await _identityEndpoint.DisposeAsync();
		}

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

	private static int FindFreePort()
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
}
