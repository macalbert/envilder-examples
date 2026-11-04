namespace Envilder.Examples.Tests;

using AwesomeAssertions;
using Envilder.Infrastructure.Azure;

public sealed class AzureKeyVaultTests(LocalStackAzureFixture keyVault) : IAsyncLifetime
{
	private readonly List<string> _storedNames = [];

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		foreach (var name in _storedNames)
		{
			await keyVault.Secrets.StartDeleteSecretAsync(name);
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

		var sut = new EnvilderClient(new AzureKeyVaultSecretProvider(keyVault.Secrets));

		// Act
		var actual = await sut.ResolveSecretsAsync(mapFile, TestContext.Current.CancellationToken);

		// Assert
		actual["DEMO_SECRET"].Should().Be(expected);
	}

	private async Task StoreAsync(string name, string value)
	{
		_storedNames.Add(name);

		await keyVault.Secrets.SetSecretAsync(name, value, TestContext.Current.CancellationToken);
	}
}
