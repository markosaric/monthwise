using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Monthwise.Application.DataTransfer;
using Monthwise.Domain.Common;
using Monthwise.Domain.DataTransfer;
using Monthwise.Infrastructure.DataTransfer;
using Monthwise.Infrastructure.Security;

namespace Monthwise.Api.Tests;

/// <summary>The password-protected export file (backup and restore FR-3, FR-4).</summary>
public class ExportFileCodecTests
{
    private static readonly HouseholdData Data = new(
        new DateTimeOffset(2026, 10, 31, 8, 15, 0, TimeSpan.Zero),
        "1.2.0+abc",
        "20261031063743_AddDataTransfer",
        new DataSummaryResponse(1, 0, 0, 0, 0, 0),
        new JsonObject { ["card"] = new JsonArray(new JsonObject { ["id"] = "x", ["amount_cents"] = 123_456_789_012L }) });

    private static SensitiveDataCipher Cipher() => new(new SensitiveDataOptions
    {
        ActiveKeyId = "test",
        EncryptionKeys = { ["test"] = MonthwiseApiFactory.TestEncryptionKey },
        HashKey = MonthwiseApiFactory.TestHashKey,
    });

    private static ExportFileCodec Codec() => new(Cipher()) { Iterations = 1_000 };

    [Fact]
    public void A_file_reads_back_with_its_password()
    {
        var codec = Codec();

        var read = codec.Read(codec.Write(Data, "secret1"), "secret1");

        Assert.True(read.IsSuccess);
        Assert.Equal(Data.CreatedAt, read.Value.CreatedAt);
        Assert.Equal(Data.Migration, read.Value.Migration);
        Assert.Equal(Data.Summary, read.Value.Summary);
        Assert.Equal(123_456_789_012L, (long)read.Value.Tables["card"]![0]!["amount_cents"]!);
    }

    [Fact]
    public void A_wrong_password_and_a_changed_byte_give_the_same_answer()
    {
        var codec = Codec();
        var file = codec.Write(Data, "secret1");
        var changed = file.ToArray();
        changed[^1] ^= 1;

        Assert.Equal(DataTransferErrors.WrongPasswordOrDamaged, Assert.Single(codec.Read(file, "secret2").Errors));
        Assert.Equal(DataTransferErrors.WrongPasswordOrDamaged, Assert.Single(codec.Read(changed, "secret1").Errors));
    }

    [Fact]
    public void Other_files_are_not_export_files()
    {
        Assert.Equal(DataTransferErrors.NotAnExportFile, Assert.Single(Codec().Read("hello"u8.ToArray(), "secret1").Errors));
    }

    [Fact]
    public void A_safety_copy_is_protected_with_the_installation_key()
    {
        var codec = Codec();

        var content = codec.Protect(Data);

        Assert.StartsWith("v1:test:", content, StringComparison.Ordinal);
        Assert.Equal(Data.Migration, codec.Unprotect(content).Value.Migration);
    }

    [Theory]
    [InlineData("""{"createdAt":null}""")]
    [InlineData("""{"createdAt":20261031}""")]
    [InlineData("""{"createdAt":"last week"}""")]
    [InlineData("""{"release":12}""")]
    [InlineData("""{"summary":{"cards":"1"}}""")]
    [InlineData("""{"summary":{"cards":1.5}}""")]
    [InlineData("""{"summary":{"cards":-1}}""")]
    [InlineData("""{"summary":[]}""")]
    [InlineData("""{"format":1}""")]
    [InlineData("""{"migration":""}""")]
    public void Content_with_a_wrong_field_is_incomplete(string change)
    {
        var content = ValidContent();
        foreach (var (name, value) in JsonNode.Parse(change)!.AsObject())
        {
            content[name] = value?.DeepClone();
        }

        Assert.Equal(DataTransferErrors.FileIncomplete, Assert.Single(ReadContent(Gzip(content.ToJsonString())).Errors));
    }

    [Theory]
    [InlineData("createdAt")]
    [InlineData("format")]
    [InlineData("migration")]
    [InlineData("summary")]
    [InlineData("tables")]
    public void Content_without_a_required_field_is_incomplete(string name)
    {
        var content = ValidContent();
        content.Remove(name);

        Assert.Equal(DataTransferErrors.FileIncomplete, Assert.Single(ReadContent(Gzip(content.ToJsonString())).Errors));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"monthwise-export\"")]
    [InlineData("{")]
    public void Content_that_is_not_an_export_object_is_incomplete(string json)
    {
        Assert.Equal(DataTransferErrors.FileIncomplete, Assert.Single(ReadContent(Gzip(json)).Errors));
    }

    [Fact]
    public void Content_that_is_not_compressed_is_incomplete()
    {
        Assert.Equal(DataTransferErrors.FileIncomplete, Assert.Single(ReadContent(Encoding.UTF8.GetBytes(ValidContent().ToJsonString())).Errors));
    }

    [Fact]
    public void Release_and_summary_counts_are_optional()
    {
        var content = ValidContent();
        content.Remove("release");
        content["summary"] = new JsonObject();

        var read = ReadContent(Gzip(content.ToJsonString()));

        Assert.True(read.IsSuccess);
        Assert.Equal("unknown", read.Value.Release);
        Assert.Equal(new DataSummaryResponse(0, 0, 0, 0, 0, 0), read.Value.Summary);
    }

    [Fact]
    public void Content_that_unpacks_past_the_limit_is_too_large()
    {
        var plain = Gzip(Padded(ValidContent().ToJsonString(), DataTransferLimits.MaxContentBytes + 1));

        Assert.True(plain.Length < 1024 * 1024, "the packed file is small");
        Assert.Equal(DataTransferErrors.ContentTooLarge, Assert.Single(ReadContent(plain).Errors));
    }

    [Fact]
    public void Content_that_unpacks_to_just_the_limit_is_read()
    {
        var read = ReadContent(Gzip(Padded(ValidContent().ToJsonString(), DataTransferLimits.MaxContentBytes)));

        Assert.True(read.IsSuccess);
    }

    [Fact]
    public void A_safety_copy_that_unpacks_past_the_limit_is_too_large()
    {
        var packed = Gzip(Padded(ValidContent().ToJsonString(), DataTransferLimits.MaxContentBytes + 1));
        var content = Cipher().Encrypt(Convert.ToBase64String(packed));

        Assert.Equal(DataTransferErrors.ContentTooLarge, Assert.Single(Codec().Unprotect(content).Errors));
    }

    /// <summary>The JSON with spaces after it, so it is <paramref name="length"/> bytes long and still valid.</summary>
    private static string Padded(string json, long length) => json + new string(' ', (int)(length - Encoding.UTF8.GetByteCount(json)));

    /// <summary>The content <see cref="ExportFileCodec.Write"/> makes for <see cref="Data"/>, before compression.</summary>
    private static JsonObject ValidContent() => new()
    {
        ["format"] = ExportFileCodec.FileFormat,
        ["formatVersion"] = ExportFileCodec.FormatVersion,
        ["createdAt"] = Data.CreatedAt,
        ["release"] = Data.Release,
        ["migration"] = Data.Migration,
        ["summary"] = new JsonObject { ["cards"] = 1 },
        ["tables"] = Data.Tables.DeepClone(),
    };

    private static Result<HouseholdData> ReadContent(byte[] plain)
    {
        var codec = Codec();
        return codec.Read(codec.WritePayload(plain, "secret1"), "secret1");
    }

    private static byte[] Gzip(string json)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            gzip.Write(Encoding.UTF8.GetBytes(json));
        }
        return output.ToArray();
    }
}
