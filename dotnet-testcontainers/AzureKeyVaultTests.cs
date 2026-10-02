namespace Envilder.Examples.Tests;

using AwesomeAssertions;
using Envilder.Infrastructure.Azure;

public sealed class AzureKeyVaultTests(LowkeyVaultFixture keyVault)
	: IClassFixture<LowkeyVaultFixture>
{
	[Fact]
	public async Task Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt()
	{
		// Arrange
		var mapFile = new MapFileParser().Parse(File.ReadAllText("envilder.test.azure.json"));
		var expected = Guid.NewGuid().ToString();

		await keyVault.Secrets.SetSecretAsync(
			mapFile.Mappings["DEMO_SECRET"], expected, TestContext.Current.CancellationToken);

		// Act
		var envilder = new EnvilderClient(new AzureKeyVaultSecretProvider(keyVault.Secrets));
		var actual = await envilder.ResolveSecretsAsync(mapFile, TestContext.Current.CancellationToken);

		// Assert
		actual["DEMO_SECRET"].Should().Be(expected);
	}
}
