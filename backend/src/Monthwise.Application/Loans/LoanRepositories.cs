using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Application.Loans;

public interface ILoanRepository
{
    /// <summary>All loans, closed ones included, each with its payment changes.</summary>
    Task<IReadOnlyList<Loan>> ListAsync(CancellationToken cancellationToken);

    /// <summary>One loan with its payment changes.</summary>
    Task<Loan?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Case-insensitive; only loans that are not closed count (A1).</summary>
    Task<bool> NameInUseAsync(string name, Guid? exceptLoanId, CancellationToken cancellationToken);

    void Add(Loan loan);
    void Remove(Loan loan);
}

public interface ILoanPaymentRepository
{
    /// <summary>Stored payments of one loan, or of all loans when <paramref name="loanId"/> is null.</summary>
    Task<IReadOnlyList<LoanPayment>> ListAsync(Guid? loanId, CancellationToken cancellationToken);

    Task<LoanPayment?> GetAsync(Guid loanId, YearMonth month, LoanPaymentKind kind, CancellationToken cancellationToken);

    void Add(LoanPayment payment);
    void Remove(LoanPayment payment);
}

public interface ILoanCategoryRepository
{
    Task<IReadOnlyList<LoanCategory>> ListAsync(CancellationToken cancellationToken);
    Task<LoanCategory?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Whether any loan uses the category.</summary>
    Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken);
    void Add(LoanCategory category);
    void Remove(LoanCategory category);
}

public interface ILoanSubcategoryRepository
{
    Task<IReadOnlyList<LoanSubcategory>> ListAsync(CancellationToken cancellationToken);
    Task<LoanSubcategory?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Case-insensitive, within one category.</summary>
    Task<bool> NameInUseAsync(Guid categoryId, string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Whether any loan uses the subcategory.</summary>
    Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken);
    void Add(LoanSubcategory subcategory);
    void Remove(LoanSubcategory subcategory);
}
