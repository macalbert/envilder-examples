using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// A demo-only "secret store": a JSON file with AES-256-GCM encrypted values.
// Each value is base64(iv | ciphertext | auth tag). The same format is used by
// the TypeScript and Python examples, so a vault sealed by one opens in the
// others. Do NOT use this as a real secret store.
public static class LocalVault
{
    private const int IvLength = 12;
    private const int TagLength = 16;

    public static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(32);

    public static string Seal(byte[] key, string plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(IvLength);
        var input = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[input.Length];
        var tag = new byte[TagLength];
        using var aes = new AesGcm(key, TagLength);
        aes.Encrypt(iv, input, ciphertext, tag);
        return Convert.ToBase64String([.. iv, .. ciphertext, .. tag]);
    }

    public static string Open(byte[] key, string sealedValue)
    {
        var bytes = Convert.FromBase64String(sealedValue);
        var iv = bytes.AsSpan(0, IvLength);
        var tag = bytes.AsSpan(bytes.Length - TagLength);
        var ciphertext = bytes.AsSpan(IvLength, bytes.Length - IvLength - TagLength);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, TagLength);
        aes.Decrypt(iv, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    public static void Write(string path, Dictionary<string, string> vault)
        => File.WriteAllText(path, JsonSerializer.Serialize(vault));

    public static Dictionary<string, string> Read(string path)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
}
