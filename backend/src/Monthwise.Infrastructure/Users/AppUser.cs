using Microsoft.AspNetCore.Identity;
using Monthwise.Application.Users;
using Monthwise.Domain.Users;

namespace Monthwise.Infrastructure.Users;

/// <summary>A user of the household as ASP.NET Core Identity stores it (login U1 to U5, ADR 0008).</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>The password is a temporary one set by a household admin (FR-7).</summary>
    public bool MustChangePassword { get; set; }

    internal UserAccount ToAccount() => new(Id, DisplayName, UserName ?? string.Empty, Role, IsActive, MustChangePassword);
}

/// <summary>A setup code that was used, by its keyed hash (FR-1, FR-9). The code itself is never stored.</summary>
internal sealed class SetupCodeUse
{
    public Guid Id { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset UsedAt { get; set; }
}
