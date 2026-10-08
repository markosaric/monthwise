using Microsoft.EntityFrameworkCore;
using Monthwise.Application.Common;
using Monthwise.Domain.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;
using Monthwise.Infrastructure.Persistence.Configurations;
using Monthwise.Infrastructure.Persistence.Converters;
using Monthwise.Infrastructure.Security;
using Npgsql;

namespace Monthwise.Infrastructure.Persistence;

public sealed class MonthwiseDbContext(DbContextOptions<MonthwiseDbContext> options, ISensitiveDataCipher cipher)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardIssuer> CardIssuers => Set<CardIssuer>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<CashFlowInput> CashFlowInputs => Set<CashFlowInput>();
    public DbSet<RecurringInput> RecurringInputs => Set<RecurringInput>();
    public DbSet<SkippedOccurrence> SkippedOccurrences => Set<SkippedOccurrence>();
    public DbSet<Payer> Payers => Set<Payer>();
    public DbSet<InputCategory> InputCategories => Set<InputCategory>();
    public DbSet<BalanceEntry> BalanceEntries => Set<BalanceEntry>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<PaymentChange> PaymentChanges => Set<PaymentChange>();
    public DbSet<LoanPayment> LoanPayments => Set<LoanPayment>();
    public DbSet<LoanCategory> LoanCategories => Set<LoanCategory>();
    public DbSet<LoanSubcategory> LoanSubcategories => Set<LoanSubcategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The model is built once per process and cached, together with the encrypting converters
        // and the cipher they hold. The API has one cipher (a singleton); tests use fixed test keys.
        modelBuilder.ApplyConfiguration(new CardConfiguration());
        modelBuilder.ApplyConfiguration(new CardSensitiveDataConfiguration(cipher));
        modelBuilder.ApplyConfiguration(new CardIssuerConfiguration());
        modelBuilder.ApplyConfiguration(new BankConfiguration());
        modelBuilder.ApplyConfiguration(new CashFlowInputConfiguration());
        modelBuilder.ApplyConfiguration(new RecurringInputConfiguration());
        modelBuilder.ApplyConfiguration(new SkippedOccurrenceConfiguration());
        modelBuilder.ApplyConfiguration(new PayerConfiguration());
        modelBuilder.ApplyConfiguration(new InputCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new BalanceEntryConfiguration());
        modelBuilder.ApplyConfiguration(new LoanConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentChangeConfiguration());
        modelBuilder.ApplyConfiguration(new LoanPaymentConfiguration());
        modelBuilder.ApplyConfiguration(new LoanCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new LoanSubcategoryConfiguration());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Money>().HaveConversion<MoneyConverter>();
        configurationBuilder.Properties<YearMonth>().HaveConversion<YearMonthConverter>();
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateSensitiveDataHashes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The constraint name is safe to report; the row values are not.
            throw new UniqueConstraintException("A value that must be unique is already in use.", ex);
        }
    }

    /// <summary>Hashes of the normalized numbers back the "same number only once" unique indexes.</summary>
    private void UpdateSensitiveDataHashes()
    {
        foreach (var entry in ChangeTracker.Entries<CardSensitiveData>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }
            entry.Property(CardSensitiveDataConfiguration.CardNumberHash).CurrentValue =
                entry.Entity.CardNumber is { } number ? HashCardNumber(cipher, number) : null;
            entry.Property(CardSensitiveDataConfiguration.IbanHash).CurrentValue =
                entry.Entity.Iban is { } iban ? HashIban(cipher, iban) : null;
        }
    }

    internal static string HashCardNumber(ISensitiveDataCipher cipher, CardNumber number) => cipher.Hash($"card:{number.Digits}");
    internal static string HashIban(ISensitiveDataCipher cipher, Iban iban) => cipher.Hash($"iban:{iban.Value}");
}
