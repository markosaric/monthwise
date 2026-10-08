using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.Tests.CashFlow;

public class AccountBalanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);

    private static BalanceCheckpoint Checkpoint(int day, long cents, int minutes = 0) =>
        new(Guid.NewGuid(), new DateOnly(2026, 10, day), Money.FromCents(cents), Now.AddMinutes(minutes));

    private static BalanceMovement Received(int day, long cents) => new(new DateOnly(2026, 10, day), Money.FromCents(cents));

    private static BalanceMovement Paid(int day, long cents) => new(new DateOnly(2026, 10, day), Money.FromCents(-cents));

    [Fact]
    public void Without_a_checkpoint_the_balance_is_the_sum_of_received_inputs()
    {
        var balance = new AccountBalance([], [Received(1, 1_000), Received(5, 2_000), Received(20, 4_000)]);

        Assert.False(balance.HasCheckpoint);
        Assert.Equal(Money.FromCents(3_000), balance.BalanceOn(new DateOnly(2026, 10, 6)));
    }

    [Fact]
    public void A_checkpoint_includes_inputs_received_on_its_date()
    {
        var balance = new AccountBalance([Checkpoint(5, -50_000)], [Received(3, 1_000), Received(5, 2_000), Received(6, 4_000)]);

        Assert.Equal(Money.FromCents(-50_000), balance.BalanceOn(new DateOnly(2026, 10, 5)));
        Assert.Equal(Money.FromCents(-46_000), balance.BalanceOn(new DateOnly(2026, 10, 6)));
        Assert.Equal(Money.FromCents(1_000), balance.BalanceOn(new DateOnly(2026, 10, 4)));
    }

    [Fact]
    public void A_correction_makes_the_balance_equal_to_it_and_shows_its_difference()
    {
        var starting = Checkpoint(1, 10_000);
        var correction = Checkpoint(10, 13_500);
        var balance = new AccountBalance([starting, correction], [Received(5, 5_000), Received(12, 1_000)]);

        Assert.Equal(Money.FromCents(13_500), balance.BalanceOn(new DateOnly(2026, 10, 10)));
        Assert.Equal(Money.FromCents(14_500), balance.BalanceOn(new DateOnly(2026, 10, 12)));
        Assert.Equal(Money.FromCents(-1_500), balance.DifferenceOf(correction.Id));
        Assert.Equal(Money.FromCents(10_000), balance.DifferenceOf(starting.Id));
    }

    [Fact]
    public void Checkpoints_on_the_same_date_are_ordered_by_entry()
    {
        var first = Checkpoint(10, 1_000, minutes: 0);
        var second = Checkpoint(10, 3_000, minutes: 5);
        var balance = new AccountBalance([second, first], []);

        Assert.Equal(Money.FromCents(3_000), balance.BalanceOn(new DateOnly(2026, 10, 10)));
        Assert.Equal(Money.FromCents(2_000), balance.DifferenceOf(second.Id));
    }

    [Fact]
    public void Expected_balance_adds_expected_inputs()
    {
        var balance = new AccountBalance([Checkpoint(1, 1_000)], [Received(2, 500)]);

        Assert.Equal(
            Money.FromCents(4_000),
            balance.ExpectedBalance(new DateOnly(2026, 10, 6), [Money.FromCents(2_000), Money.FromCents(500)]));
    }

    [Fact]
    public void Paid_outputs_lower_the_balance_after_the_checkpoint_only()
    {
        var balance = new AccountBalance([Checkpoint(10, 100_000)], [Paid(5, 30_000), Received(12, 5_000), Paid(15, 45_000)]);

        Assert.Equal(Money.FromCents(-30_000), balance.BalanceOn(new DateOnly(2026, 10, 9)));
        Assert.Equal(Money.FromCents(105_000), balance.BalanceOn(new DateOnly(2026, 10, 12)));
        Assert.Equal(Money.FromCents(60_000), balance.BalanceOn(new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void A_correction_after_a_payment_makes_the_balance_equal_to_it()
    {
        var correction = Checkpoint(16, 50_000);
        var balance = new AccountBalance([Checkpoint(1, 100_000), correction], [Paid(15, 45_000)]);

        Assert.Equal(Money.FromCents(50_000), balance.BalanceOn(new DateOnly(2026, 10, 16)));
        Assert.Equal(Money.FromCents(-5_000), balance.DifferenceOf(correction.Id));
    }

    [Fact]
    public void Expected_balance_subtracts_expected_payments()
    {
        var balance = new AccountBalance([Checkpoint(1, 100_000)], []);

        Assert.Equal(
            Money.FromCents(75_000),
            balance.ExpectedBalance(new DateOnly(2026, 10, 6), [Money.FromCents(20_000), Money.FromCents(-45_000)]));
    }
}
