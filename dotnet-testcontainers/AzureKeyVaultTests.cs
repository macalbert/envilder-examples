namespace Envilder.Examples.Tests;

using AwesomeAssertions;
using Envilder.Infrastructure.Azure;

public sealed class AzureKeyVaultTests(LocalStackAzureFixture keyVault)
	: IClassFixture<LocalStackAzureFixture>
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

	[Fact]
	public async Task Should_ReportEveryKeyAsExists_When_ContractHoldsInKeyVault()
	{
		// Arrange
		await SetDemoSecretAsync();

		// Act
		var actual = await Env.FromMapFile("envilder.test.azure.json")
			.WithVaultUrl(keyVault.Secrets.VaultUri.AbsoluteUri)
			.ValidateContractAsync(TestContext.Current.CancellationToken);

		// Assert
		actual.IsValid.Should().BeTrue();
		actual.Keys.Should().ContainSingle()
			.Which.Status.Should().Be(SecretExistenceStatus.Exists);
	}

	[Fact]
	public async Task Should_ReportKeyAsMissing_When_SecretIsNotInKeyVault()
	{
		// Arrange
		await SetDemoSecretAsync();

		using var mapFile = TestMapFile.WithExtraKey(
			"envilder.test.azure.json", "NOT_PUSHED_YET", "envilder-examples-not-pushed-yet");

		// Act
		var actual = await Env.FromMapFile(mapFile.Path)
			.WithVaultUrl(keyVault.Secrets.VaultUri.AbsoluteUri)
			.ValidateContractAsync(TestContext.Current.CancellationToken);

		// Assert
		actual.IsValid.Should().BeFalse();
		actual.Keys.Should().ContainSingle(k => k.Variable == "NOT_PUSHED_YET")
			.Which.Status.Should().Be(SecretExistenceStatus.Missing);
		actual.Keys.Should().ContainSingle(k => k.Variable == "DEMO_SECRET")
			.Which.Status.Should().Be(SecretExistenceStatus.Exists);
	}

	[Fact]
	public async Task Should_FailTheLoad_When_ContractValidationIsOnAndSecretIsNotInKeyVault()
	{
		// Arrange
		await SetDemoSecretAsync();

		using var mapFile = TestMapFile.WithExtraKey(
			"envilder.test.azure.json", "NOT_PUSHED_YET", "envilder-examples-not-pushed-yet");

		// Act
		var act = () => Env.FromMapFile(mapFile.Path)
			.WithVaultUrl(keyVault.Secrets.VaultUri.AbsoluteUri)
			.WithContractValidation()
			.ResolveAsync(TestContext.Current.CancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ContractValidationException>())
			.WithMessage("*NOT_PUSHED_YET (envilder-examples-not-pushed-yet): missing*");
	}

	private async Task SetDemoSecretAsync()
	{
		var mapFile = new MapFileParser().Parse(File.ReadAllText("envilder.test.azure.json"));

		await keyVault.Secrets.SetSecretAsync(
			mapFile.Mappings["DEMO_SECRET"], Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);
	}
}
