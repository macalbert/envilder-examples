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

		var sut = new EnvilderClient(new AzureKeyVaultSecretProvider(keyVault.Secrets));

		// Act
		var actual = await sut.ResolveSecretsAsync(mapFile, TestContext.Current.CancellationToken);

		// Assert
		actual["DEMO_SECRET"].Should().Be(expected);
	}
}
