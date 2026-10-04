using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Monthwise.Infrastructure.Security;

/// <summary>Keys for card numbers and IBANs. Kept in user secrets or environment variables, never in appsettings.</summary>
public sealed class SensitiveDataOptions
{
    public const string SectionName = "SensitiveData";

    /// <summary>Id of the key used for new values; older keys stay in <see cref="EncryptionKeys"/> to read old values.</summary>
    public string? ActiveKeyId { get; set; }

    /// <summary>Key id → base64 of a 256-bit key.</summary>
    public Dictionary<string, string> EncryptionKeys { get; init; } = [];

    /// <summary>Base64 of a 256-bit key for the uniqueness hashes. Changing it requires recomputing all hashes.</summary>
    public string? HashKey { get; set; }

    public static SensitiveDataOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        var options = new SensitiveDataOptions { ActiveKeyId = section["ActiveKeyId"], HashKey = section["HashKey"] };
        foreach (var key in section.GetSection("EncryptionKeys").GetChildren())
        {
            options.EncryptionKeys[key.Key] = key.Value ?? string.Empty;
        }
        return options;
    }
}

/// <summary>Encrypts sensitive values at rest and hashes them for uniqueness checks.</summary>
public interface ISensitiveDataCipher
{
    string Encrypt(string plaintext);
    string Decrypt(string stored);

    /// <summary>Keyed hash (HMAC-SHA256): equal values give equal hashes, without revealing the value.</summary>
    string Hash(string value);
}

/// <summary>
/// AES-256-GCM with a random nonce per value. Stored form: <c>v1:&lt;keyId&gt;:&lt;base64(nonce|ciphertext|tag)&gt;</c>,
/// so keys can be rotated without a schema change. See docs/decisions/0005-encrypted-sensitive-data.md.
/// </summary>
internal sealed class SensitiveDataCipher : ISensitiveDataCipher
{
    private const string Version = "v1";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly string _activeKeyId;
    private readonly Dictionary<string, byte[]> _keys;
    private readonly byte[] _hashKey;

    public SensitiveDataCipher(SensitiveDataOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        const string help = "See README.md, step 5 'Create the encryption keys for card numbers'.";

        _keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (id, base64) in value.EncryptionKeys)
        {
            if (id.Length == 0 || !id.All(char.IsAsciiLetterOrDigit))
            {
                throw new InvalidOperationException($"SensitiveData key id '{id}' must contain only letters and digits. {help}");
            }
            _keys[id] = DecodeKey(base64, $"SensitiveData:EncryptionKeys:{id}", help);
        }

        if (string.IsNullOrEmpty(value.ActiveKeyId) || !_keys.ContainsKey(value.ActiveKeyId))
        {
            throw new InvalidOperationException(
                $"SensitiveData:ActiveKeyId is missing or has no matching SensitiveData:EncryptionKeys entry. {help}");
        }
        _activeKeyId = value.ActiveKeyId;
        _hashKey = DecodeKey(value.HashKey, "SensitiveData:HashKey", help);
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var payload = new byte[NonceSize + plain.Length + TagSize];
        var nonce = payload.AsSpan(0, NonceSize);
        var cipher = payload.AsSpan(NonceSize, plain.Length);
        var tag = payload.AsSpan(NonceSize + plain.Length, TagSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_keys[_activeKeyId], TagSize);
        aes.Encrypt(nonce, plain, cipher, tag, AssociatedData(_activeKeyId));
        return $"{Version}:{_activeKeyId}:{Convert.ToBase64String(payload)}";
    }

    public string Decrypt(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var parts = stored.Split(':');
        if (parts.Length != 3 || parts[0] != Version)
        {
            throw new CryptographicException("Stored value has an unknown format.");
        }
        if (!_keys.TryGetValue(parts[1], out var key))
        {
            throw new CryptographicException($"No encryption key with id '{parts[1]}' is configured.");
        }

        var payload = Convert.FromBase64String(parts[2]);
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Stored value is too short.");
        }
        var cipherLength = payload.Length - NonceSize - TagSize;
        var plain = new byte[cipherLength];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            payload.AsSpan(0, NonceSize),
            payload.AsSpan(NonceSize, cipherLength),
            payload.AsSpan(NonceSize + cipherLength, TagSize),
            plain,
            AssociatedData(parts[1]));
        return Encoding.UTF8.GetString(plain);
    }

    public string Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexStringLower(HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes(value)));
    }

    private static byte[] AssociatedData(string keyId) => Encoding.UTF8.GetBytes($"monthwise:{Version}:{keyId}");

    private static byte[] DecodeKey(string? base64, string name, string help)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64 ?? string.Empty);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{name} is not valid base64. {help}");
        }
        if (key.Length != KeySize)
        {
            throw new InvalidOperationException($"{name} must be a 256-bit (32-byte) key. {help}");
        }
        return key;
    }
}
