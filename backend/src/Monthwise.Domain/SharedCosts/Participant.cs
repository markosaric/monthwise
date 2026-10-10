using Monthwise.Domain.Common;

namespace Monthwise.Domain.SharedCosts;

/// <summary>What the user enters for a participant (FR-1, P1 and P2).</summary>
public sealed record ParticipantDetails(string? Name, string? Description);

/// <summary>
/// A person outside the household who shares some bills and loans (FR-1). Their contributions are
/// inputs with the participant's <see cref="PayerId"/> as payer. A participant that shares a cost is
/// archived instead of deleted.
/// </summary>
public sealed class Participant
{
    public const int NameMaxLength = 50;
    public const int DescriptionMaxLength = 200;

    private Participant()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }

    /// <summary>The payer of the participant's contributions; renamed with the participant.</summary>
    public Guid PayerId { get; private set; }

    public string? Description { get; private set; }
    public DateOnly? ArchivedOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsArchived => ArchivedOn is not null;

    /// <summary>Adds a participant. Unique names and the payer are handled by the caller.</summary>
    public static Result<Participant> Create(ParticipantDetails details, Guid payerId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details);
        if (errors.Count > 0)
        {
            return Result.Failure<Participant>(errors);
        }
        var participant = new Participant { Id = Guid.CreateVersion7(now), PayerId = payerId, CreatedAt = now };
        participant.Apply(details, now);
        return Result.Success(participant);
    }

    public Result Update(ParticipantDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Apply(details, now);
        return Result.Success();
    }

    public void Archive(DateOnly today, DateTimeOffset now)
    {
        ArchivedOn ??= today;
        UpdatedAt = now;
    }

    public void Restore(DateTimeOffset now)
    {
        ArchivedOn = null;
        UpdatedAt = now;
    }

    /// <summary>The trimmed name, or the name errors.</summary>
    public static Result<string> ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<string>(SharedCostErrors.ParticipantNameRequired);
        }
        return trimmed.Length > NameMaxLength
            ? Result.Failure<string>(SharedCostErrors.ParticipantNameTooLong)
            : Result.Success(trimmed);
    }

    private static List<DomainError> Validate(ParticipantDetails details)
    {
        var errors = new List<DomainError>(ValidateName(details.Name).Errors);
        if (Normalize(details.Description) is { Length: > DescriptionMaxLength })
        {
            errors.Add(SharedCostErrors.ParticipantDescriptionTooLong);
        }
        return errors;
    }

    private void Apply(ParticipantDetails details, DateTimeOffset now)
    {
        Name = details.Name!.Trim();
        Description = Normalize(details.Description);
        UpdatedAt = now;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
