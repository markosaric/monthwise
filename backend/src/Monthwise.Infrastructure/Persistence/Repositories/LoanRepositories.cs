using Microsoft.EntityFrameworkCore;
using Monthwise.Application.Loans;
using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Infrastructure.Persistence.Repositories;

internal sealed class LoanRepository(MonthwiseDbContext db) : ILoanRepository
{
    public async Task<IReadOnlyList<Loan>> ListAsync(CancellationToken cancellationToken) =>
        await db.Loans.ToListAsync(cancellationToken);

    public Task<Loan?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(string name, Guid? exceptLoanId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return db.Loans.AnyAsync(
            l => l.ClosedOn == null && l.Name == name && l.Id != exceptLoanId, cancellationToken);
    }

    public void Add(Loan loan) => db.Loans.Add(loan);

    public void Remove(Loan loan) => db.Loans.Remove(loan);
}

internal sealed class LoanPaymentRepository(MonthwiseDbContext db) : ILoanPaymentRepository
{
    public async Task<IReadOnlyList<LoanPayment>> ListAsync(Guid? loanId, CancellationToken cancellationToken) =>
        await db.LoanPayments.Where(p => loanId == null || p.LoanId == loanId).ToListAsync(cancellationToken);

    public async Task<LoanPayment?> GetAsync(Guid loanId, YearMonth month, LoanPaymentKind kind, CancellationToken cancellationToken) =>
        // A payment stored in this request is not in the database yet.
        db.LoanPayments.Local.FirstOrDefault(p => p.LoanId == loanId && p.Month == month && p.Kind == kind)
        ?? await db.LoanPayments.FirstOrDefaultAsync(p => p.LoanId == loanId && p.Month == month && p.Kind == kind, cancellationToken);

    public void Add(LoanPayment payment) => db.LoanPayments.Add(payment);

    public void Remove(LoanPayment payment) => db.LoanPayments.Remove(payment);
}

internal sealed class LoanCategoryRepository(MonthwiseDbContext db) : ILoanCategoryRepository
{
    public async Task<IReadOnlyList<LoanCategory>> ListAsync(CancellationToken cancellationToken) =>
        await db.LoanCategories.ToListAsync(cancellationToken);

    public Task<LoanCategory?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.LoanCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return db.LoanCategories.AnyAsync(c => c.Name == name && c.Id != exceptId, cancellationToken);
    }

    public Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken) =>
        db.Loans.AnyAsync(l => l.CategoryId == id, cancellationToken);

    public void Add(LoanCategory category) => db.LoanCategories.Add(category);

    public void Remove(LoanCategory category) => db.LoanCategories.Remove(category);
}

internal sealed class LoanSubcategoryRepository(MonthwiseDbContext db) : ILoanSubcategoryRepository
{
    public async Task<IReadOnlyList<LoanSubcategory>> ListAsync(CancellationToken cancellationToken) =>
        await db.LoanSubcategories.ToListAsync(cancellationToken);

    public Task<LoanSubcategory?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.LoanSubcategories.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(Guid categoryId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return db.LoanSubcategories.AnyAsync(
            s => s.CategoryId == categoryId && s.Name == name && s.Id != exceptId, cancellationToken);
    }

    public Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken) =>
        db.Loans.AnyAsync(l => l.SubcategoryId == id, cancellationToken);

    public void Add(LoanSubcategory subcategory) => db.LoanSubcategories.Add(subcategory);

    public void Remove(LoanSubcategory subcategory) => db.LoanSubcategories.Remove(subcategory);
}
