using Monthwise.Domain.Cards;

namespace Monthwise.Domain.Tests.Cards;

public class CardListEntryTests
{
    [Fact]
    public void New_entries_are_trimmed_and_not_predefined()
    {
        var bank = Bank.Create("  Addiko  ").Value;
        var issuer = CardIssuer.Create("UnionPay").Value;

        Assert.Equal("Addiko", bank.Name);
        Assert.False(bank.IsPredefined);
        Assert.Equal("UnionPay", issuer.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("This name is far too long for a bank list entry, really")]
    public void Name_is_required_and_at_most_50_characters(string? name)
    {
        Assert.Equal("name", Assert.Single(Bank.Create(name).Errors).Field);
        Assert.Equal("name", Assert.Single(CardIssuer.Create(name).Errors).Field);
    }
}
