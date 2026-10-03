using Monthwise.Domain.Common;

namespace Monthwise.Domain.Tests.Common;

public class MoneyTests
{
    [Fact]
    public void Addition_and_subtraction_work_in_whole_cents()
    {
        var a = Money.FromCents(12_345);
        var b = Money.FromCents(655);

        Assert.Equal(Money.FromCents(13_000), a + b);
        Assert.Equal(Money.FromCents(11_690), a - b);
    }

    [Fact]
    public void Negation_and_abs()
    {
        var value = Money.FromCents(250);

        Assert.Equal(Money.FromCents(-250), -value);
        Assert.True((-value).IsNegative);
        Assert.Equal(value, (-value).Abs());
    }

    [Fact]
    public void Multiplication_by_whole_number()
    {
        Assert.Equal(Money.FromCents(3_000), Money.FromCents(1_000) * 3);
        Assert.Equal(Money.FromCents(3_000), 3 * Money.FromCents(1_000));
    }

    [Fact]
    public void Sum_of_empty_sequence_is_zero()
    {
        Assert.Equal(Money.Zero, Money.Sum([]));
    }

    [Fact]
    public void Sum_adds_all_values()
    {
        Assert.Equal(Money.FromCents(60), Money.Sum([Money.FromCents(10), Money.FromCents(20), Money.FromCents(30)]));
    }

    [Fact]
    public void Comparison_operators()
    {
        Assert.True(Money.FromCents(1) > Money.Zero);
        Assert.True(Money.FromCents(-1) < Money.Zero);
        Assert.True(Money.FromCents(5) <= Money.FromCents(5));
    }

    [Fact]
    public void Overflow_throws_instead_of_wrapping()
    {
        Assert.Throws<OverflowException>(() => Money.FromCents(long.MaxValue) + Money.FromCents(1));
    }

    [Fact]
    public void Has_no_floating_point_api()
    {
        var members = typeof(Money).GetMembers()
            .SelectMany(m => m switch
            {
                System.Reflection.MethodInfo method => method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType),
                System.Reflection.PropertyInfo property => [property.PropertyType],
                System.Reflection.ConstructorInfo ctor => ctor.GetParameters().Select(p => p.ParameterType),
                _ => [],
            });

        Assert.DoesNotContain(members, t => t == typeof(double) || t == typeof(float) || t == typeof(decimal));
    }
}
