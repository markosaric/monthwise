using Monthwise.Domain.Cards;

namespace Monthwise.Domain.Tests.Cards;

public class IbanTests
{
    [Theory]
    [InlineData("HR1210010051863000160")]
    [InlineData("hr12 1001 0051 8630 0016 0")]
    [InlineData("DE89 3704 0044 0532 0130 00")]
    [InlineData("GB82WEST12345698765432")]
    public void Accepts_valid_ibans_in_any_case_and_spacing(string input)
    {
        Assert.True(Iban.TryParse(input, out var iban));
        Assert.Equal(iban!.Value, iban.Value.ToUpperInvariant());
        Assert.DoesNotContain(' ', iban.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("HR1310010051863000160")] // wrong check digits
    [InlineData("HR121001005186300016")] // wrong length for Croatia
    [InlineData("1210010051863000160HR")]
    [InlineData("HR12-1001-0051-8630-0016-0")]
    public void Rejects_invalid_ibans(string? input)
    {
        Assert.False(Iban.TryParse(input, out var iban));
        Assert.Null(iban);
    }

    [Fact]
    public void Masks_all_but_country_and_last_four()
    {
        var iban = Iban.Parse("HR1210010051863000160");

        Assert.Equal("HR", iban.CountryCode);
        Assert.Equal("0160", iban.Last4);
        Assert.Equal("HR•• … 0160", iban.ToString());
    }

    [Fact]
    public void Formats_in_groups_of_four()
    {
        Assert.Equal("HR12 1001 0051 8630 0016 0", Iban.Parse("HR1210010051863000160").Formatted());
    }
}
