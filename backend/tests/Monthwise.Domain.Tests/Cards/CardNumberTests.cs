using Monthwise.Domain.Cards;

namespace Monthwise.Domain.Tests.Cards;

public class CardNumberTests
{
    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("5555-5555-5555-4444")]
    [InlineData("378282246310005")]
    public void Accepts_valid_numbers_with_spaces_or_dashes(string input)
    {
        Assert.True(CardNumber.TryParse(input, out var number));
        Assert.DoesNotContain(' ', number!.Digits);
        Assert.DoesNotContain('-', number.Digits);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4111111111111112")] // Luhn check fails
    [InlineData("411111111111")] // too short
    [InlineData("41111111111111111111")] // too long
    [InlineData("4111a11111111111")]
    public void Rejects_invalid_numbers(string? input)
    {
        Assert.False(CardNumber.TryParse(input, out var number));
        Assert.Null(number);
    }

    [Fact]
    public void ToString_is_masked_so_logs_never_show_the_full_number()
    {
        var number = CardNumber.Parse("4111 1111 1111 1111");

        Assert.Equal("1111", number.Last4);
        Assert.Equal("•••• 1111", number.ToString());
        Assert.DoesNotContain("4111", $"{number}", StringComparison.Ordinal);
    }

    [Fact]
    public void Numbers_with_the_same_digits_are_equal()
    {
        Assert.Equal(CardNumber.Parse("4111111111111111"), CardNumber.Parse("4111 1111 1111 1111"));
    }
}
