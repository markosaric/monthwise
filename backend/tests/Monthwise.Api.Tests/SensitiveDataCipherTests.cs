using System.Security.Cryptography;
using Monthwise.Domain.Cards;
using Monthwise.Infrastructure.Persistence;
using Monthwise.Infrastructure.Security;

namespace Monthwise.Api.Tests;

public class SensitiveDataCipherTests
{
    private static readonly string KeyA = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
    private static readonly string KeyB = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray());
    private static readonly string HashKey = Convert.ToBase64String(Enumerable.Repeat((byte)3, 32).ToArray());

    private static SensitiveDataCipher Cipher(string activeKeyId = "a", Dictionary<string, string>? keys = null) =>
        new(new SensitiveDataOptions
        {
            ActiveKeyId = activeKeyId,
            EncryptionKeys = keys ?? new Dictionary<string, string> { ["a"] = KeyA },
            HashKey = HashKey,
        });

    [Fact]
    public void Encrypts_and_decrypts_without_revealing_the_value()
    {
        var cipher = Cipher();

        var stored = cipher.Encrypt("4111111111111111");

        Assert.StartsWith("v1:a:", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("4111", stored, StringComparison.Ordinal);
        Assert.Equal("4111111111111111", cipher.Decrypt(stored));
    }

    [Fact]
    public void The_same_value_encrypts_differently_every_time()
    {
        var cipher = Cipher();

        Assert.NotEqual(cipher.Encrypt("HR1210010051863000160"), cipher.Encrypt("HR1210010051863000160"));
    }

    [Fact]
    public void Tampered_values_are_rejected()
    {
        var cipher = Cipher();
        var stored = cipher.Encrypt("4111111111111111");
        var payload = Convert.FromBase64String(stored.Split(':')[2]);
        payload[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt($"v1:a:{Convert.ToBase64String(payload)}"));
    }

    [Fact]
    public void Values_encrypted_with_an_older_key_stay_readable_after_rotation()
    {
        var stored = Cipher().Encrypt("4111111111111111");
        var rotated = Cipher("b", new Dictionary<string, string> { ["a"] = KeyA, ["b"] = KeyB });

        Assert.Equal("4111111111111111", rotated.Decrypt(stored));
        Assert.StartsWith("v1:b:", rotated.Encrypt("x"), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_key_ids_are_rejected()
    {
        var stored = Cipher().Encrypt("4111111111111111");
        var other = Cipher("b", new Dictionary<string, string> { ["b"] = KeyB });

        Assert.Throws<CryptographicException>(() => other.Decrypt(stored));
    }

    [Theory]
    [InlineData(null, "a")]
    [InlineData("missing", "a")]
    [InlineData("a", "not base64!")]
    [InlineData("a", "AAAA")] // too short
    public void Missing_or_invalid_keys_fail_at_startup(string? activeKeyId, string key)
    {
        Assert.Throws<InvalidOperationException>(() => Cipher(activeKeyId!, new Dictionary<string, string> { ["a"] = key }));
    }

    [Fact]
    public void Hashes_are_stable_and_ignore_spacing_of_the_input()
    {
        var cipher = Cipher();

        Assert.Equal(
            MonthwiseDbContext.HashCardNumber(cipher, CardNumber.Parse("4111 1111 1111 1111")),
            MonthwiseDbContext.HashCardNumber(cipher, CardNumber.Parse("4111111111111111")));
        Assert.Equal(
            MonthwiseDbContext.HashIban(cipher, Iban.Parse("hr12 1001 0051 8630 0016 0")),
            MonthwiseDbContext.HashIban(cipher, Iban.Parse("HR1210010051863000160")));
        Assert.DoesNotContain("4111", MonthwiseDbContext.HashCardNumber(cipher, CardNumber.Parse("4111111111111111")), StringComparison.Ordinal);
    }
}
