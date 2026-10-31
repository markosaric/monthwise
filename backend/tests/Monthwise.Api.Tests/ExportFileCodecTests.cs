using System.Text.Json.Nodes;
using Monthwise.Application.DataTransfer;
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

    private static ExportFileCodec Codec() => new(new SensitiveDataCipher(new SensitiveDataOptions
    {
        ActiveKeyId = "test",
        EncryptionKeys = { ["test"] = MonthwiseApiFactory.TestEncryptionKey },
        HashKey = MonthwiseApiFactory.TestHashKey,
    }))
    { Iterations = 1_000 };

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
        Assert.Equal(Data.Migration, codec.Unprotect(content).Migration);
    }
}
