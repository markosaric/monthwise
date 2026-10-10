using Monthwise.Domain.Common;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Domain.Tests.SharedCosts;

public class CostShareTests
{
    private static readonly Guid Ana = Guid.NewGuid();

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    [InlineData(4, 3)]
    public void Two_to_four_shares_with_the_household_keeping_at_least_one_are_valid(int shares, int participantShares)
    {
        var share = CostShare.Create(Ana, shares, participantShares, null);

        Assert.True(share.IsSuccess);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Fewer_than_two_or_more_than_four_shares_are_refused(int shares)
    {
        var share = CostShare.Create(Ana, shares, 1, null);

        Assert.Equal(SharedCostErrors.SharesInvalid, Assert.Single(share.Errors));
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    public void The_participant_pays_at_least_one_share_and_fewer_than_all(int shares, int participantShares)
    {
        var share = CostShare.Create(Ana, shares, participantShares, null);

        Assert.Equal(SharedCostErrors.ParticipantSharesInvalid, Assert.Single(share.Errors));
    }

    [Fact]
    public void A_share_needs_a_participant()
    {
        var share = CostShare.Create(Guid.Empty, 2, 1, null);

        Assert.Equal(SharedCostErrors.ShareParticipantRequired, Assert.Single(share.Errors));
    }

    [Theory]
    [InlineData(10_001, 2, 1, 5_001)] // half a cent is rounded up
    [InlineData(10_000, 3, 1, 3_333)]
    [InlineData(10_000, 3, 2, 6_667)]
    [InlineData(10_001, 4, 1, 2_500)]
    [InlineData(10_002, 4, 1, 2_501)] // 2500.5 rounded up
    [InlineData(10_001, 4, 3, 7_501)]
    [InlineData(1, 4, 1, 0)]
    public void The_part_is_rounded_to_the_nearest_cent_and_the_own_part_makes_up_the_rest(
        long amountCents, int shares, int participantShares, long partCents)
    {
        var share = new CostShare(Ana, shares, participantShares);
        var amount = Money.FromCents(amountCents);

        Assert.Equal(partCents, share.PartOf(amount).Cents);
        Assert.Equal(amount, share.PartOf(amount) + share.OwnPartOf(amount));
    }

    [Theory]
    [InlineData(2, 1, 50)]
    [InlineData(3, 1, 33)]
    [InlineData(3, 2, 67)]
    [InlineData(4, 3, 75)]
    public void The_percentage_is_rounded_to_whole_percent(int shares, int participantShares, int percentage)
    {
        Assert.Equal(percentage, new CostShare(Ana, shares, participantShares).Percentage);
    }

    [Fact]
    public void An_empty_contribution_account_means_none()
    {
        var share = CostShare.Create(Ana, 2, 1, Guid.Empty).Value;

        Assert.Null(share.ContributionAccountId);
    }
}
