using Envilder;
using Envilder.Domain.Ports;

// The whole custom provider. Envilder calls these methods and nothing else:
// it never knows the secrets come from an encrypted file.
public sealed class LocalVaultProvider(string vaultPath, byte[] key) : ISecretProvider, ISecretExistenceChecker
{
    // ISecretProvider: return the value, or null when the secret is missing.
    public string? GetSecret(string name)
        => LocalVault.Read(vaultPath).TryGetValue(name, out var sealedValue)
            ? LocalVault.Open(key, sealedValue)
            : null;

    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(GetSecret(name));

    // ISecretExistenceChecker: answer "does it exist?" without decrypting.
    // This is what lets WithContractValidation() check the map file at startup.
    public Task<IReadOnlyDictionary<string, SecretExistence>> CheckSecretsExistAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken)
    {
        var vault = LocalVault.Read(vaultPath);
        IReadOnlyDictionary<string, SecretExistence> existence = names.ToDictionary(
            name => name,
            name => new SecretExistence(
                vault.ContainsKey(name) ? SecretExistenceStatus.Exists : SecretExistenceStatus.Missing));
        return Task.FromResult(existence);
    }
}
