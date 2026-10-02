namespace Envilder.Examples.Tests;

using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

public sealed class LowkeyVaultFixture : IAsyncLifetime
{
	private const int VaultPort = 8443;
	private const int TokenPort = 8080;

	private IContainer _container = null!;

	public SecretClient Secrets { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		_container = new ContainerBuilder("nagyesta/lowkey-vault:7.1.61")
			.WithPortBinding(VaultPort, true)
			.WithPortBinding(TokenPort, true)
			.WithEnvironment("LOWKEY_ARGS", "--server.port=8443 --LOWKEY_VAULT_RELAXED_PORTS=true")
			.WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Started LowkeyVaultApp"))
			.Build();

		await _container.StartAsync();

		var host = _container.Hostname;

		Environment.SetEnvironmentVariable(
			"IDENTITY_ENDPOINT",
			$"http://{host}:{_container.GetMappedPublicPort(TokenPort)}/metadata/identity/oauth2/token");
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
			new Uri($"https://{host}:{_container.GetMappedPublicPort(VaultPort)}"),
			new DefaultAzureCredential(),
			options);
	}

	public async ValueTask DisposeAsync()
	{
		Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", null);
		Environment.SetEnvironmentVariable("IDENTITY_HEADER", null);

		if (_container is not null)
		{
			await _container.DisposeAsync();
		}
	}
}
