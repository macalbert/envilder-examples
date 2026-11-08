using AwesomeAssertions;
using Envilder;

public sealed class LocalVaultProviderTests : IDisposable
{
    private const string MapFile = "envilder.vault.json";
    private static readonly byte[] Key = LocalVault.GenerateKey();
    private readonly string _vaultPath = Path.Combine(Path.GetTempPath(), $"envilder-vault-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        File.Delete(_vaultPath);
        Environment.SetEnvironmentVariable("DB_PASSWORD", null);
        Environment.SetEnvironmentVariable("API_KEY", null);
    }

    [Fact]
    public async Task Should_InjectDecryptedSecrets_When_VaultHoldsEveryMappedSecret()
    {
        // Arrange
        LocalVault.Write(_vaultPath, new()
        {
            ["/envilder-examples/db-password"] = LocalVault.Seal(Key, "correct-horse"),
            ["/envilder-examples/api-key"] = LocalVault.Seal(Key, "battery-staple"),
        });

        // Act
        await Env.FromMapFile(MapFile)
            .WithSecretProvider(new LocalVaultProvider(_vaultPath, Key))
            .WithContractValidation()
            .InjectAsync(TestContext.Current.CancellationToken);

        // Assert
        Environment.GetEnvironmentVariable("DB_PASSWORD").Should().Be("correct-horse");
        Environment.GetEnvironmentVariable("API_KEY").Should().Be("battery-staple");
    }

    [Fact]
    public async Task Should_FailAtStartupWithoutInjecting_When_MappedSecretIsMissing()
    {
        // Arrange
        LocalVault.Write(_vaultPath, new()
        {
            ["/envilder-examples/db-password"] = LocalVault.Seal(Key, "correct-horse"),
        });
        var sut = Env.FromMapFile(MapFile)
            .WithSecretProvider(new LocalVaultProvider(_vaultPath, Key))
            .WithContractValidation();

        // Act
        var act = () => sut.InjectAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ContractValidationException>()
            .WithMessage("*API_KEY (/envilder-examples/api-key): missing*");
        Environment.GetEnvironmentVariable("DB_PASSWORD").Should().BeNull();
    }

    [Fact]
    public async Task Should_ReportEachKeyWithoutDecrypting_When_ContractIsValidated()
    {
        // Arrange
        LocalVault.Write(_vaultPath, new()
        {
            ["/envilder-examples/db-password"] = LocalVault.Seal(Key, "correct-horse"),
        });
        var wrongKey = LocalVault.GenerateKey();

        // Act
        var actual = await Env.FromMapFile(MapFile)
            .WithSecretProvider(new LocalVaultProvider(_vaultPath, wrongKey))
            .ValidateContractAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.Keys.Select(k => (k.Variable, k.Status)).Should().Equal(
            ("DB_PASSWORD", SecretExistenceStatus.Exists),
            ("API_KEY", SecretExistenceStatus.Missing));
    }
}
