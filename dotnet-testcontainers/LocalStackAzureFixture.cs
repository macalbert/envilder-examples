namespace Envilder.Examples.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

public sealed class LocalStackAzureFixture : IAsyncLifetime
{
	// The emulator accepts any service principal in its all-zero tenant and subscription
	private const string ZeroGuid = "00000000-0000-0000-0000-000000000000";
	private const string ResourceGroup = "envilder-examples";
	private const string Location = "westeurope";

	private IContainer _container = null!;

	public SecretClient Secrets { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		var secrets = await Env.ResolveFileAsync("envilder.json");

		// Vault URLs embed the gateway port, so host and container ports must match
		var port = FreePort();

		_container = new ContainerBuilder("localstack/localstack-azure:latest")
			.WithName($"localstack-azure-{Guid.NewGuid():N}")
			.WithPortBinding(port, port)
			.WithEnvironment(secrets)
			.WithEnvironment("GATEWAY_LISTEN", $"0.0.0.0:{port}")
			.WithEnvironment("LOCALSTACK_HOST", $"localhost.localstack.cloud:{port}")
			.WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(@"Ready\."))
			.Build();

		await _container.StartAsync();

		var authority = new Uri($"https://azure.localhost.localstack.cloud:{port}");
		var credential = new ClientSecretCredential(
			ZeroGuid,
			"any-app",
			"dummy",
			new ClientSecretCredentialOptions { AuthorityHost = authority, DisableInstanceDiscovery = true });

		var vaultUri = await CreateVaultAsync(authority, credential);

		Secrets = new SecretClient(vaultUri, credential);
	}

	public async ValueTask DisposeAsync()
	{
		if (_container is not null)
		{
			await _container.DisposeAsync();
		}
	}

	private static async Task<Uri> CreateVaultAsync(Uri authority, TokenCredential credential)
	{
		var token = await credential.GetTokenAsync(
			new TokenRequestContext(["https://management.azure.com/.default"]),
			CancellationToken.None);

		using var http = new HttpClient { BaseAddress = authority };
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

		var group = $"/subscriptions/{ZeroGuid}/resourceGroups/{ResourceGroup}";

		await PutAsync(http, $"{group}?api-version=2021-04-01", new { location = Location });

		var vaultName = $"kv-{Guid.NewGuid():N}"[..11];

		var vault = await PutAsync(
			http,
			$"{group}/providers/Microsoft.KeyVault/vaults/{vaultName}?api-version=2023-07-01",
			new
			{
				location = Location,
				properties = new
				{
					tenantId = ZeroGuid,
					sku = new { family = "A", name = "standard" },
					accessPolicies = Array.Empty<object>(),
				},
			});

		return new Uri(vault["properties"]!["vaultUri"]!.GetValue<string>().TrimEnd('/'));
	}

	private static async Task<JsonNode> PutAsync(HttpClient http, string path, object body)
	{
		using var response = await http.PutAsJsonAsync(path, body);
		response.EnsureSuccessStatusCode();

		return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
	}

	private static int FreePort()
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
