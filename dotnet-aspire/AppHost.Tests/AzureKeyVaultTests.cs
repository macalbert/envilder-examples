namespace AppHost.Tests;

using AwesomeAssertions;
using Envilder;
using Envilder.Infrastructure.Azure;

public sealed class AzureKeyVaultTests(AspireAppFixture app) : IAsyncLifetime
{
	private readonly List<string> _storedNames = [];

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		foreach (var name in _storedNames)
		{
			await app.Secrets.StartDeleteSecretAsync(name);
		}
	}

	[Fact]
	public async Task Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt()
	{
		// Arrange
		var mapFile = TestMapFiles.WithUniqueSecrets(
			"envilder.test.azure.json", name => $"{name}-{Guid.NewGuid():N}");
		var expected = Guid.NewGuid().ToString();

		await StoreAsync(mapFile.Mappings["DEMO_SECRET"], expected);

		var sut = new EnvilderClient(new AzureKeyVaultSecretProvider(app.Secrets));

		// Act
		var actual = await sut.ResolveSecretsAsync(mapFile, TestContext.Current.CancellationToken);

		// Assert
		actual["DEMO_SECRET"].Should().Be(expected);
	}

	private async Task StoreAsync(string name, string value)
	{
		_storedNames.Add(name);

		await app.Secrets.SetSecretAsync(name, value, TestContext.Current.CancellationToken);
	}
}
