using Monthwise.Domain.Users;

namespace Monthwise.Domain.Tests.Users;

/// <summary>User attributes U1 to U3 and the household admin rule (login FR-6).</summary>
public class UserRulesTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Ivo = Guid.NewGuid();

    [Theory]
    [InlineData("ana")]
    [InlineData("Ana.Anic")]
    [InlineData("ivo-horvat_2")]
    [InlineData("đuro.žic")]
    [InlineData("  ana  ")]
    public void A_username_of_letters_digits_dots_dashes_and_underscores_is_accepted(string username)
    {
        var (value, errors) = UserRules.ValidateUsername(username);

        Assert.Empty(errors);
        Assert.Equal(username.Trim(), value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("ana anic")]
    [InlineData("ana@home")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    public void Any_other_username_is_refused(string? username)
    {
        var (value, errors) = UserRules.ValidateUsername(username);

        Assert.Null(value);
        Assert.Equal([UserErrors.UsernameInvalid], errors);
    }

    [Fact]
    public void A_display_name_is_required_and_at_most_100_characters()
    {
        Assert.Equal([UserErrors.DisplayNameRequired], UserRules.ValidateDisplayName("  ").Errors);
        Assert.Equal([UserErrors.DisplayNameTooLong], UserRules.ValidateDisplayName(new string('a', 101)).Errors);
        Assert.Equal("Ana", UserRules.ValidateDisplayName(" Ana ").Value);
    }

    [Fact]
    public void A_password_has_12_to_128_characters_of_any_kind()
    {
        Assert.Single(UserRules.ValidatePassword(null, "password"));
        Assert.Single(UserRules.ValidatePassword(new string('a', 11), "password"));
        Assert.Empty(UserRules.ValidatePassword("twelve chars", "password"));
        Assert.Empty(UserRules.ValidatePassword(new string('ž', 128), "password"));
        var tooLong = Assert.Single(UserRules.ValidatePassword(new string('a', 129), "newPassword"));
        Assert.Equal("newPassword", tooLong.Field);
    }

    [Fact]
    public void The_last_active_household_admin_cannot_become_a_member_inactive_or_be_deleted()
    {
        UserSnapshot[] users = [new(Ana, UserRole.HouseholdAdmin, true), new(Ivo, UserRole.Member, true)];

        Assert.True(UserRules.EnsureHouseholdAdminRemains(users, new UserSnapshot(Ana, UserRole.Member, true), Ana).IsFailure);
        Assert.True(UserRules.EnsureHouseholdAdminRemains(users, new UserSnapshot(Ana, UserRole.HouseholdAdmin, false), Ana).IsFailure);
        Assert.True(UserRules.EnsureHouseholdAdminRemains(users, null, Ana).IsFailure);
        Assert.True(UserRules.EnsureHouseholdAdminRemains(users, null, Ivo).IsSuccess);
        Assert.True(UserRules.EnsureHouseholdAdminRemains(users, new UserSnapshot(Ivo, UserRole.HouseholdAdmin, true), Ivo).IsSuccess);
    }

    [Fact]
    public void With_a_second_active_household_admin_the_first_one_may_change()
    {
        UserSnapshot[] users = [new(Ana, UserRole.HouseholdAdmin, true), new(Ivo, UserRole.HouseholdAdmin, true)];

        Assert.True(UserRules.EnsureHouseholdAdminRemains(users, new UserSnapshot(Ana, UserRole.Member, true), Ana).IsSuccess);
        Assert.True(UserRules.EnsureHouseholdAdminRemains(users, null, Ana).IsSuccess);
    }
}
