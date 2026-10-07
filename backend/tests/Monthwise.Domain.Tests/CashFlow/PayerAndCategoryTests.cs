using Monthwise.Domain.CashFlow;

namespace Monthwise.Domain.Tests.CashFlow;

public class PayerAndCategoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 6);

    [Fact]
    public void Payer_names_are_trimmed_and_limited()
    {
        Assert.Equal("Employer d.o.o.", Payer.Create("  Employer d.o.o. ", Now).Value.Name);
        Assert.Contains(CashFlowErrors.PayerRequired, Payer.Create(" ", Now).Errors);
        Assert.Contains(CashFlowErrors.PayerTooLong, Payer.Create(new string('x', Payer.NameMaxLength + 1), Now).Errors);
    }

    [Fact]
    public void A_removed_payer_can_be_restored()
    {
        var payer = Payer.Create("Employer", Now).Value;

        payer.Remove(Today);
        Assert.True(payer.IsRemoved);
        payer.Restore();
        Assert.False(payer.IsRemoved);
    }

    [Fact]
    public void Renaming_a_payer_checks_the_name()
    {
        var payer = Payer.Create("Employer", Now).Value;

        Assert.Contains(CashFlowErrors.PayerNameRequired, payer.Rename("").Errors);
        Assert.True(payer.Rename("New employer").IsSuccess);
        Assert.Equal("New employer", payer.Name);
    }

    [Fact]
    public void Categories_can_be_renamed_hidden_and_shown()
    {
        var category = InputCategory.Create("Bonus", Now).Value;

        Assert.False(category.IsPredefined);
        Assert.True(category.Rename(" Yearly bonus ").IsSuccess);
        Assert.Equal("Yearly bonus", category.Name);
        category.Hide(Today);
        Assert.True(category.IsHidden);
        category.Show();
        Assert.False(category.IsHidden);
        Assert.Contains(CashFlowErrors.CategoryNameTooLong, InputCategory.Create(new string('x', 51), Now).Errors);
    }
}
