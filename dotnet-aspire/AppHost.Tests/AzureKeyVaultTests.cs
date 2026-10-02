namespace AppHost.Tests;

using AwesomeAssertions;
using Envilder;
using Envilder.Infrastructure.Azure;

public sealed class AzureKeyVaultTests(AspireAppFixture app)
{
	[Fact]
	public async Task Should_ResolveSecretFromKeyVault_When_MapFilePointsToIt()
	{
		// Arrange
		var mapFile = new MapFileParser().Parse(File.ReadAllText("envilder.test.azure.json"));
		var expected = Guid.NewGuid().ToString();

		await app.Secrets.SetSecretAsync(
			mapFile.Mappings["DEMO_SECRET"], expected, TestContext.Current.CancellationToken);

		// Act
		var envilder = new EnvilderClient(new AzureKeyVaultSecretProvider(app.Secrets));
		var actual = await envilder.ResolveSecretsAsync(mapFile, TestContext.Current.CancellationToken);

		// Assert
		actual["DEMO_SECRET"].Should().Be(expected);
	}
}
