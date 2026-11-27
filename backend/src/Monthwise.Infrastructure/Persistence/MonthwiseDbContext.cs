using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Monthwise.Application.Common;
using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;
using Monthwise.Domain.DataTransfer;
using Monthwise.Domain.Expenses;
using Monthwise.Domain.Lendings;
using Monthwise.Domain.Loans;
using Monthwise.Domain.Revolving;
using Monthwise.Domain.SharedCosts;
using Monthwise.Infrastructure.Persistence.Configurations;
using Monthwise.Infrastructure.Persistence.Converters;
using Monthwise.Infrastructure.Security;
using Monthwise.Infrastructure.Users;
using Npgsql;

namespace Monthwise.Infrastructure.Persistence;

public sealed class MonthwiseDbContext(DbContextOptions<MonthwiseDbContext> options, ISensitiveDataCipher cipher)
    : IdentityUserContext<AppUser, Guid>(options), IUnitOfWork, IDataProtectionKeyContext
{
    private LoadedRows? _loaded;

    /// <summary>The whole tables this request has read (repeated loading FR-2).</summary>
    internal LoadedRows Loaded => _loaded ??= new LoadedRows(this);

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
    public DbSet<CardBillingTerms> BillingTerms => Set<CardBillingTerms>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<CardPurchase> CardPurchases => Set<CardPurchase>();
    public DbSet<InstallmentReversal> InstallmentReversals => Set<InstallmentReversal>();
    public DbSet<CardPayment> CardPayments => Set<CardPayment>();
    public DbSet<RecurringBill> RecurringBills => Set<RecurringBill>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Lending> Lendings => Set<Lending>();
    public DbSet<LendingRepayment> LendingRepayments => Set<LendingRepayment>();
    public DbSet<RevolvingTerms> RevolvingTerms => Set<RevolvingTerms>();
    public DbSet<DebtEntry> DebtEntries => Set<DebtEntry>();
    public DbSet<ExtraPayment> ExtraPayments => Set<ExtraPayment>();
    public DbSet<RevolvingBill> RevolvingBills => Set<RevolvingBill>();
    public DbSet<DataTransferRecord> DataTransferLog => Set<DataTransferRecord>();
    public DbSet<BackupReminder> BackupReminders => Set<BackupReminder>();
    public DbSet<SafetyCopy> SafetyCopies => Set<SafetyCopy>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    internal DbSet<SetupCodeUse> SetupCodeUses => Set<SetupCodeUse>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // The Identity user tables first, then renamed with the rest of the login tables (ADR 0008).
        base.OnModelCreating(builder);
        builder.ApplyLoginConfiguration();

        // The model is built once per process and cached, together with the encrypting converters
        // and the cipher they hold. The API has one cipher (a singleton); tests use fixed test keys.
        builder.HasNameCollation();
        builder.ApplyConfiguration(new CardConfiguration());
        builder.ApplyConfiguration(new CardSensitiveDataConfiguration(cipher));
        builder.ApplyConfiguration(new CardIssuerConfiguration());
        builder.ApplyConfiguration(new BankConfiguration());
        builder.ApplyConfiguration(new CashFlowInputConfiguration());
        builder.ApplyConfiguration(new RecurringInputConfiguration());
        builder.ApplyConfiguration(new SkippedOccurrenceConfiguration());
        builder.ApplyConfiguration(new PayerConfiguration());
        builder.ApplyConfiguration(new InputCategoryConfiguration());
        builder.ApplyConfiguration(new BalanceEntryConfiguration());
        builder.ApplyConfiguration(new LoanConfiguration());
        builder.ApplyConfiguration(new PaymentChangeConfiguration());
        builder.ApplyConfiguration(new LoanPaymentConfiguration());
        builder.ApplyConfiguration(new LoanCategoryConfiguration());
        builder.ApplyConfiguration(new LoanSubcategoryConfiguration());
        builder.ApplyConfiguration(new CardBillingTermsConfiguration());
        builder.ApplyConfiguration(new ExpenseCategoryConfiguration());
        builder.ApplyConfiguration(new CardPurchaseConfiguration());
        builder.ApplyConfiguration(new InstallmentReversalConfiguration());
        builder.ApplyConfiguration(new CardPaymentConfiguration());
        builder.ApplyConfiguration(new RecurringBillConfiguration());
        builder.ApplyConfiguration(new BillConfiguration());
        builder.ApplyConfiguration(new ExpenseConfiguration());
        builder.ApplyConfiguration(new ParticipantConfiguration());
        builder.ApplyConfiguration(new LendingConfiguration());
        builder.ApplyConfiguration(new LendingRepaymentConfiguration());
        builder.ApplyConfiguration(new RevolvingTermsConfiguration());
        builder.ApplyConfiguration(new DebtEntryConfiguration());
        builder.ApplyConfiguration(new ExtraPaymentConfiguration());
        builder.ApplyConfiguration(new RevolvingBillConfiguration());
        builder.ApplyConfiguration(new DataTransferRecordConfiguration());
        builder.ApplyConfiguration(new BackupReminderConfiguration());
        builder.ApplyConfiguration(new SafetyCopyConfiguration());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Money>().HaveConversion<MoneyConverter>();
        configurationBuilder.Properties<YearMonth>().HaveConversion<YearMonthConverter>();
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateSensitiveDataHashes();
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        finally
        {
            // Everything calculated after a save reads what was saved (repeated loading FR-3).
            _loaded?.Clear();
        }
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException error
            && DatabaseRuleTranslator.Translate(error, WritesTo) is { } rule)
        {
            // Only the kind of rule is reported; the row values and the server's text are not.
            throw new DatabaseRuleException(rule, ex);
        }
    }

    /// <summary>Whether this unit of work adds or modifies rows of the given table.</summary>
    private bool WritesTo(string table) =>
        ChangeTracker.Entries().Any(e =>
            e.State is EntityState.Added or EntityState.Modified
            && string.Equals(e.Metadata.GetTableName(), table, StringComparison.Ordinal));

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
