using Monthwise.Domain.Loans;
using static Monthwise.Domain.Tests.Loans.LoanTestData;

namespace Monthwise.Domain.Tests.Loans;

public class LoanCategoryTests
{
    [Fact]
    public void A_category_is_created_renamed_hidden_and_shown()
    {
        var category = LoanCategory.Create("  Student loan ", allowsResidualValue: false, Now).Value;

        Assert.Equal("Student loan", category.Name);
        Assert.True(category.Rename("Study loan").IsSuccess);
        category.Hide(new DateOnly(2026, 10, 20));
        Assert.True(category.IsHidden);
        category.Show();
        Assert.False(category.IsHidden);
        category.SetAllowsResidualValue(true);
        Assert.True(category.AllowsResidualValue);
    }

    [Fact]
    public void Names_are_required_and_at_most_50_characters()
    {
        Assert.Equal(new[] { LoanErrors.CategoryNameRequired }, LoanCategory.Create(" ", false, Now).Errors);
        Assert.Equal(new[] { LoanErrors.CategoryNameTooLong }, LoanCategory.Create(new string('a', 51), false, Now).Errors);
        Assert.Equal(new[] { LoanErrors.SubcategoryNameRequired }, LoanSubcategory.Create(Guid.NewGuid(), null, Now).Errors);
        Assert.Equal(new[] { LoanErrors.SubcategoryNameTooLong }, LoanSubcategory.Create(Guid.NewGuid(), new string('a', 51), Now).Errors);
    }

    [Fact]
    public void A_subcategory_belongs_to_its_category()
    {
        var categoryId = Guid.NewGuid();
        var subcategory = LoanSubcategory.Create(categoryId, "Boat lease", Now).Value;

        Assert.Equal(categoryId, subcategory.CategoryId);
        Assert.False(subcategory.IsPredefined);
    }
}
