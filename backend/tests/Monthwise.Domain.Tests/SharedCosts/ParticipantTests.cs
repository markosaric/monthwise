using Monthwise.Domain.SharedCosts;

namespace Monthwise.Domain.Tests.SharedCosts;

public class ParticipantTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Payer = Guid.NewGuid();

    [Fact]
    public void A_participant_has_a_trimmed_name_and_an_optional_description()
    {
        var participant = Participant.Create(new ParticipantDetails("  Ana  ", " "), Payer, Now).Value;

        Assert.Equal("Ana", participant.Name);
        Assert.Null(participant.Description);
        Assert.Equal(Payer, participant.PayerId);
        Assert.False(participant.IsArchived);
    }

    [Fact]
    public void The_name_is_required_and_at_most_fifty_characters()
    {
        Assert.Equal(
            SharedCostErrors.ParticipantNameRequired,
            Assert.Single(Participant.Create(new ParticipantDetails(" ", null), Payer, Now).Errors));
        Assert.Equal(
            SharedCostErrors.ParticipantNameTooLong,
            Assert.Single(Participant.Create(new ParticipantDetails(new string('a', 51), null), Payer, Now).Errors));
    }

    [Fact]
    public void The_description_is_at_most_two_hundred_characters()
    {
        var result = Participant.Create(new ParticipantDetails("Ana", new string('a', 201)), Payer, Now);

        Assert.Equal(SharedCostErrors.ParticipantDescriptionTooLong, Assert.Single(result.Errors));
    }

    [Fact]
    public void A_participant_can_be_archived_and_restored()
    {
        var participant = Participant.Create(new ParticipantDetails("Ana", null), Payer, Now).Value;

        participant.Archive(new DateOnly(2026, 10, 10), Now);
        Assert.True(participant.IsArchived);

        participant.Restore(Now);
        Assert.False(participant.IsArchived);
    }
}
