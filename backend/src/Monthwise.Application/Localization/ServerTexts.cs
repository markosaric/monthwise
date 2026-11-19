using System.Text.RegularExpressions;

namespace Monthwise.Application.Localization;

/// <summary>
/// Every message and name the API makes up for the user, in Croatian (croatian-language.md FR-3).
/// The English text written in the code stays the source and the key; <see cref="Translate"/>
/// returns the Croatian text when the request is in Croatian, and the English text otherwise or
/// when no translation exists. Texts with values (lengths, days, a release) are matched by
/// <see cref="Patterns"/>, so the values written in the code are never repeated here.
/// </summary>
public static partial class ServerTexts
{
    /// <summary>The name shown for an item that no longer exists.</summary>
    public static string Unknown => Translate("Unknown");

    /// <summary>The text in the language of the current request.</summary>
    public static string Translate(string english)
    {
        ArgumentNullException.ThrowIfNull(english);
        return AppLanguage.IsCroatian ? ToCroatian(english) ?? english : english;
    }

    /// <summary>The Croatian text, or null when there is none.</summary>
    public static string? ToCroatian(string english)
    {
        ArgumentNullException.ThrowIfNull(english);
        if (Croatian.TryGetValue(english, out var croatian))
        {
            return croatian;
        }
        foreach (var (pattern, replace) in Patterns)
        {
            var match = pattern.Match(english);
            if (match.Success)
            {
                return replace(match);
            }
        }
        return null;
    }

    /// <summary>How the import names each name list in a message (backup and restore, name comparison FR-7).</summary>
    private static readonly Dictionary<string, string> ListNames = new(StringComparer.Ordinal)
    {
        ["cards"] = "kartice",
        ["card issuers"] = "izdavatelje kartica",
        ["banks"] = "banke",
        ["payers"] = "uplatitelje",
        ["input categories"] = "kategorije uplata",
        ["loan categories"] = "kategorije kredita",
        ["loan subcategories"] = "potkategorije kredita",
        ["loans"] = "kredite",
        ["expense categories"] = "kategorije troškova",
        ["recurring bills"] = "ponavljajuće račune",
        ["participants"] = "sudionike",
    };

    private static readonly Dictionary<string, string> Fields = new(StringComparer.Ordinal)
    {
        ["Name"] = "Naziv",
        ["Description"] = "Opis",
        ["Note"] = "Bilješka",
        ["Payer"] = "Uplatitelj",
        ["Payee"] = "Primatelj",
        ["Reason"] = "Razlog",
    };

    private static string Day(Match m, int group) => m.Groups[group].Value;

    private static readonly (Regex Pattern, Func<Match, string> Replace)[] Patterns =
    [
        (TooLong(), m => $"{Fields.GetValueOrDefault(m.Groups[1].Value, m.Groups[1].Value)} može imati najviše {m.Groups[2].Value} znakova."),
        (BillDay(), m => $"Dan računa mora biti dan između {Day(m, 1)} i {Day(m, 2)}."),
        (DueDay(), m => $"Dan dospijeća mora biti dan između {Day(m, 1)} i {Day(m, 2)}."),
        (PaymentDate(), m => $"Datum dospijeća mora biti dan između {Day(m, 1)} i {Day(m, 2)}."),
        (StatementDate(), m => $"Datum obračuna mora biti dan između {Day(m, 1)} i {Day(m, 2)}."),
        (PaymentDay(), m => $"Dan plaćanja mora biti između {Day(m, 1)} i {Day(m, 2)}."),
        (PeriodTooLong(), m => $"Odaberite razdoblje od najviše {m.Groups[1].Value} godina."),
        (PaymentCountTooLarge(), m => $"Broj rata može biti najviše {m.Groups[1].Value}."),
        (TooFewInstallments(), m => $"Kupnja na rate ima najmanje {m.Groups[1].Value} rate."),
        (SharesBetween(), m => $"Broj udjela mora biti između {Day(m, 1)} i {Day(m, 2)}."),
        (PasswordTooShort(), m => $"Lozinka mora imati najmanje {m.Groups[1].Value} znakova."),
        (FileTooLarge(), m => $"Ova je datoteka prevelika da bi bila datoteka izvoza Monthwisea (više od {m.Groups[1].Value} MB)."),
        (ConfirmationWord(), m => $"Upišite {m.Groups[1].Value} za potvrdu da se svi podaci zamjenjuju."),
        (MonthRange(), m => $"Unesite mjesec od {m.Groups[1].Value} do {m.Groups[2].Value}."),
        (DateRange(), m => $"Unesite datum od {m.Groups[1].Value} do {m.Groups[2].Value}."),
        (SameNames(), m =>
            $"Datoteka ima {ListNames.GetValueOrDefault(m.Groups[1].Value, m.Groups[1].Value)} s nazivima „{m.Groups[2].Value}” i „{m.Groups[3].Value}”, "
            + "koji se razlikuju samo po velikim i malim slovima. Monthwise treba različite nazive na popisu. Nijedan podatak nije promijenjen."),
        (NewerRelease(), m =>
            $"Datoteku je izradio Monthwise {m.Groups[1].Value}, koji je noviji od ovoga. Najprije ažurirajte Monthwise."),
        (ResidualValue(), m => $"{m.Groups[1].Value}, ostatak vrijednosti"),
    ];

    [GeneratedRegex(@"^(Name|Description|Note|Payer|Payee|Reason) can be at most (\d+) characters\.$")]
    private static partial Regex TooLong();

    [GeneratedRegex(@"^Bill day must be a day between (\d+) and (\d+)\.$")]
    private static partial Regex BillDay();

    [GeneratedRegex(@"^Due day must be a day between (\d+) and (\d+)\.$")]
    private static partial Regex DueDay();

    [GeneratedRegex(@"^Payment date must be a day between (\d+) and (\d+)\.$")]
    private static partial Regex PaymentDate();

    [GeneratedRegex(@"^Statement date must be a day between (\d+) and (\d+)\.$")]
    private static partial Regex StatementDate();

    [GeneratedRegex(@"^Payment day must be between (\d+) and (\d+)\.$")]
    private static partial Regex PaymentDay();

    [GeneratedRegex(@"^Choose a period of at most (\d+) years\.$")]
    private static partial Regex PeriodTooLong();

    [GeneratedRegex(@"^Number of payments can be at most (\d+)\.$")]
    private static partial Regex PaymentCountTooLarge();

    [GeneratedRegex(@"^A purchase in installments has at least (\d+) installments\.$")]
    private static partial Regex TooFewInstallments();

    [GeneratedRegex(@"^Shares must be between (\d+) and (\d+)\.$")]
    private static partial Regex SharesBetween();

    [GeneratedRegex(@"^The password must be at least (\d+) characters long\.$")]
    private static partial Regex PasswordTooShort();

    [GeneratedRegex(@"^This file is too large to be a Monthwise export file \(more than (\d+) MB\)\.$")]
    private static partial Regex FileTooLarge();

    [GeneratedRegex(@"^Type (\S+) to confirm that all data is replaced\.$")]
    private static partial Regex ConfirmationWord();

    [GeneratedRegex(@"^Enter a month from (\d{4}-\d{2}) to (\d{4}-\d{2})\.$")]
    private static partial Regex MonthRange();

    [GeneratedRegex(@"^Enter a date from (\d{4}-\d{2}-\d{2}) to (\d{4}-\d{2}-\d{2})\.$")]
    private static partial Regex DateRange();

    [GeneratedRegex("^The file has (.+) named \"(.*)\" and \"(.*)\", which differ only in upper/lower case\\. Monthwise needs names in a list to be different\\. No data was changed\\.$", RegexOptions.Singleline)]
    private static partial Regex SameNames();

    [GeneratedRegex(@"^The file was made by Monthwise (.+), which is newer than this one\. Update Monthwise first\.$")]
    private static partial Regex NewerRelease();

    [GeneratedRegex(@"^(.+), residual value$", RegexOptions.Singleline)]
    private static partial Regex ResidualValue();

    /// <summary>English text → Croatian text, for texts without values.</summary>
    private static readonly Dictionary<string, string> Croatian = new(StringComparer.Ordinal)
    {
        // Names the API makes up for groups and payments.
        ["No account yet"] = "Još nema računa",
        ["Card payments"] = "Plaćanja kartica",
        ["Unknown"] = "Nepoznato",
        ["An import is running. Try again in a moment."] = "Uvoz je u tijeku. Pokušajte ponovno za trenutak.",
        ["The database could not be brought up to date. See the log for details."] = "Bazu podataka nije bilo moguće ažurirati. Pojedinosti su u zapisniku.",

        // Database rules (error mapping FR-1 to FR-4) and request shapes (month range).
        ["A value that must be unique is already in use."] = "Vrijednost koja mora biti jedinstvena već se koristi.",
        ["A record this change refers to no longer exists. Reload and try again."] =
            "Zapis na koji se ova promjena odnosi više ne postoji. Ponovno učitajte i pokušajte ponovno.",
        ["This record is still used by other data and cannot be deleted."] =
            "Ovaj zapis još koriste drugi podaci i ne može se obrisati.",
        ["Some of the values are not allowed. Check the entries and try again."] =
            "Neke vrijednosti nisu dopuštene. Provjerite unose i pokušajte ponovno.",
        ["Enter the month as yyyy-MM."] = "Unesite mjesec kao gggg-MM.",
        ["Enter the month and year as printed on the card."] = "Unesite mjesec i godinu kako su otisnuti na kartici.",

        // Business rules (DomainError messages).
        ["A bill has no account; it is chosen when a payment is paid."] = "Račun nema račun plaćanja; odabire se kada se rata plati.",
        ["A card cannot replace itself."] = "Kartica ne može zamijeniti samu sebe.",
        ["A card that allows monthly installments needs its statement date and payment date."] =
            "Kartica koja omogućuje mjesečne rate treba datum obračuna i datum dospijeća.",
        ["A contribution cannot be deleted; stop sharing the bill or loan payment instead."] =
            "Doprinos se ne može obrisati; umjesto toga prestanite dijeliti račun ili ratu kredita.",
        ["A debt transfer to or from a replacement card cannot be changed."] =
            "Prijenos duga na zamjensku karticu ili s nje ne može se mijenjati.",
        ["A monthly bill cannot be paid before its bill date."] = "Mjesečni račun ne može se platiti prije datuma računa.",
        ["A payment this change applies to is already paid, so it cannot be changed or deleted."] =
            "Rata na koju se ova promjena odnosi već je plaćena, pa se promjena ne može mijenjati ni obrisati.",
        ["A revolving card can only be replaced by a revolving card."] = "Revolving karticu može zamijeniti samo revolving kartica.",
        ["A revolving card does not allow installments: the purchase is paid in full and becomes debt on the card."] =
            "Revolving kartica ne omogućuje rate: kupnja se plaća u cijelosti i postaje dug na kartici.",
        ["A revolving card needs its max debt limit."] = "Revolving kartica treba najveći limit duga.",
        ["A revolving card needs its minimum payment, interest rate, bill day and due day."] =
            "Revolving kartica treba minimalnu uplatu, kamatnu stopu, dan računa i dan dospijeća.",
        ["A standing order needs a primary account."] = "Trajni nalog treba tekući račun.",
        ["Amount cannot be more than the debt on that day."] = "Iznos ne može biti veći od duga na taj dan.",
        ["Amount must be greater than zero, or empty."] = "Iznos mora biti veći od nule ili prazan.",
        ["Amount must be greater than zero."] = "Iznos mora biti veći od nule.",
        ["Amount must be more than zero."] = "Iznos mora biti veći od nule.",
        ["An extra payment is always recorded as paid: its date cannot be in the future."] =
            "Dodatna uplata uvijek se bilježi kao plaćena: njezin datum ne može biti u budućnosti.",
        ["An import is already running."] = "Uvoz je već u tijeku.",
        ["Another card already has this name."] = "Druga kartica već ima ovaj naziv.",
        ["Another category already has this name."] = "Druga kategorija već ima ovaj naziv.",
        ["Another loan already has this name."] = "Drugi kredit već ima ovaj naziv.",
        ["Another participant already has this name."] = "Drugi sudionik već ima ovo ime.",
        ["Another payer already has this name."] = "Drugi uplatitelj već ima ovaj naziv.",
        ["Another payment change already starts in this month."] = "Druga promjena rate već počinje u ovom mjesecu.",
        ["Another recurring bill already has this name."] = "Drugi ponavljajući račun već ima ovaj naziv.",
        ["Balance entry not found."] = "Stavka salda nije pronađena.",
        ["Bill not found."] = "Račun nije pronađen.",
        ["Bills were already added from this recurring bill. Stop it instead."] =
            "Iz ovog ponavljajućeg računa već su dodani računi. Umjesto toga ga zaustavite.",
        ["Card not found."] = "Kartica nije pronađena.",
        ["Card number is not valid."] = "Broj kartice nije ispravan.",
        ["Cash can only be withdrawn from a primary account."] = "Gotovina se može podići samo s tekućeg računa.",
        ["Cash withdrawal cannot be renamed, hidden or deleted."] = "Podizanje gotovine ne može se preimenovati, sakriti ni obrisati.",
        ["Category not found."] = "Kategorija nije pronađena.",
        ["Choose 7, 14, 30, 60 or 90 days."] = "Odaberite 7, 14, 30, 60 ili 90 dana.",
        ["Choose a bank."] = "Odaberite banku.",
        ["Choose a card."] = "Odaberite karticu.",
        ["Choose a category."] = "Odaberite kategoriju.",
        ["Choose a lender."] = "Odaberite vjerovnika.",
        ["Choose a primary account that is not blocked, expired or archived."] =
            "Odaberite tekući račun koji nije blokiran, istekao ni arhiviran.",
        ["Choose a subcategory of the chosen category."] = "Odaberite potkategoriju odabrane kategorije.",
        ["Choose a subcategory."] = "Odaberite potkategoriju.",
        ["Choose an account that is not blocked, expired or archived."] = "Odaberite račun koji nije blokiran, istekao ni arhiviran.",
        ["Choose an account."] = "Odaberite račun.",
        ["Choose an export file."] = "Odaberite datoteku izvoza.",
        ["Choose an issuer."] = "Odaberite izdavatelja.",
        ["Choose how often it repeats."] = "Odaberite koliko se često ponavlja.",
        ["Choose standing order or bill."] = "Odaberite trajni nalog ili račun.",
        ["Choose starting balance or correction."] = "Odaberite početni saldo ili ispravak.",
        ["Choose the account it was paid from."] = "Odaberite račun s kojeg je plaćeno.",
        ["Choose the account the contribution is paid to."] = "Odaberite račun na koji se uplaćuje doprinos.",
        ["Choose the account the monthly bills are paid from."] = "Odaberite račun s kojeg se plaćaju mjesečni računi.",
        ["Choose the month the change applies from."] = "Odaberite mjesec od kojeg promjena vrijedi.",
        ["Choose the primary account card payments are paid from."] = "Odaberite tekući račun s kojeg se plaćaju kartice.",
        ["Choose what it was paid with."] = "Odaberite čime je plaćeno.",
        ["Choose who shares the cost."] = "Odaberite tko dijeli trošak.",
        ["Debt cannot be negative."] = "Dug ne može biti negativan.",
        ["Debt entry not found."] = "Stavka duga nije pronađena.",
        ["Description is required."] = "Opis je obavezan.",
        ["Due date is required."] = "Datum dospijeća je obavezan.",
        ["Due date must be in the payment's month."] = "Datum dospijeća mora biti u mjesecu rate.",
        ["Due date must be the same as or later than the issue date."] = "Datum dospijeća mora biti isti ili kasniji od datuma računa.",
        ["End date must be the same as or later than the first expected date."] =
            "Datum završetka mora biti isti ili kasniji od prvog datuma naplate.",
        ["End month does not match the number of payments."] = "Završni mjesec ne odgovara broju rata.",
        ["End month is not valid."] = "Završni mjesec nije ispravan.",
        ["End month must be the same as or later than the start month."] = "Završni mjesec mora biti isti ili kasniji od početnog mjeseca.",
        ["Enter a day from 1 to 31, or leave it empty."] = "Unesite dan od 1 do 31 ili ostavite prazno.",
        ["Enter a new monthly payment, a new payment day, or both."] = "Unesite novu mjesečnu ratu, novi dan plaćanja ili oboje.",
        ["Enter the amount, issue date and due date before marking the bill as paid."] =
            "Unesite iznos, datum računa i datum dospijeća prije nego što račun označite kao plaćen.",
        ["Enter the number of installments."] = "Unesite broj rata.",
        ["Enter the number of payments or the end month."] = "Unesite broj rata ili završni mjesec.",
        ["Enter the password of the file."] = "Unesite lozinku datoteke.",
        ["Expected date must be the same as or later than the issue date."] = "Datum naplate mora biti isti ili kasniji od datuma računa.",
        ["Expense not found."] = "Trošak nije pronađen.",
        ["Extra payment not found."] = "Dodatna uplata nije pronađena.",
        ["IBAN is not valid."] = "IBAN nije ispravan.",
        ["Input not found."] = "Uplata nije pronađena.",
        ["Interest rate cannot be negative."] = "Kamatna stopa ne može biti negativna.",
        ["Issue date is required."] = "Datum računa je obavezan.",
        ["Loan not found."] = "Kredit nije pronađen.",
        ["Max debt limit cannot be negative."] = "Najveći limit duga ne može biti negativan.",
        ["Max installments must be 12, 24 or 36."] = "Najveći broj rata mora biti 12, 24 ili 36.",
        ["Minimum payment % must be more than 0 and at most 100."] = "Minimalna uplata u % mora biti veća od 0 i najviše 100.",
        ["Minimum payment amount cannot be negative."] = "Iznos minimalne uplate ne može biti negativan.",
        ["Minimum payment and interest cannot be negative."] = "Minimalna uplata i kamata ne mogu biti negativne.",
        ["Minimum payment interest rate must be between 0 and the minimum payment %."] =
            "Minimalna kamatna stopa za plaćanje mora biti između 0 i postotka minimalne uplate.",
        ["Monthly payment must be greater than zero."] = "Mjesečna rata mora biti veća od nule.",
        ["Name is required."] = "Naziv je obavezan.",
        ["Number of payments must be a whole number of 1 or more."] = "Broj rata mora biti cijeli broj 1 ili veći.",
        ["Only debit cards can be a primary account."] = "Samo debitne kartice mogu biti tekući račun.",
        ["Only debit cards have a monthly installments option."] = "Samo debitne kartice imaju mogućnost mjesečnih rata.",
        ["Paid date must be the same as or later than the bill date."] = "Datum plaćanja mora biti isti ili kasniji od datuma računa.",
        ["Paid date must be the same as or later than the issue date."] = "Datum plaćanja mora biti isti ili kasniji od datuma računa.",
        ["Paid date must be the same as or later than the statement date."] = "Datum plaćanja mora biti isti ili kasniji od datuma obračuna.",
        ["Participant not found."] = "Sudionik nije pronađen.",
        ["Payer is required."] = "Uplatitelj je obavezan.",
        ["Payer not found."] = "Uplatitelj nije pronađen.",
        ["Payment change not found."] = "Promjena rate nije pronađena.",
        ["Payment date must be later than the statement date."] = "Datum dospijeća mora biti kasniji od datuma obračuna.",
        ["Purchase not found."] = "Kupnja nije pronađena.",
        ["Received date must be the same as or later than the issue date."] = "Datum primitka mora biti isti ili kasniji od datuma računa.",
        ["Record a cash withdrawal with Cash withdrawal instead."] = "Podizanje gotovine zabilježite pomoću Podizanja gotovine.",
        ["Recurring bill not found."] = "Ponavljajući račun nije pronađen.",
        ["Recurring input not found."] = "Ponavljajuća uplata nije pronađena.",
        ["Residual value must be greater than zero."] = "Ostatak vrijednosti mora biti veći od nule.",
        ["Set the card's statement date, payment date and paying account first."] =
            "Najprije postavite datum obračuna, datum dospijeća i račun plaćanja kartice.",
        ["Start month cannot be after a paid payment."] = "Početni mjesec ne može biti nakon plaćene rate.",
        ["Start month is not valid."] = "Početni mjesec nije ispravan.",
        ["Subcategory not found."] = "Potkategorija nije pronađena.",
        ["The bill has no contribution until it is issued."] = "Račun nema doprinos dok nije izdan.",
        ["The change must start after the month of the latest paid payment."] = "Promjena mora početi nakon mjeseca zadnje plaćene rate.",
        ["The change must start within the loan's payments."] = "Promjena mora početi unutar rata kredita.",
        ["The chosen category does not allow a residual value."] = "Odabrana kategorija ne omogućuje ostatak vrijednosti.",
        ["The contribution follows its bill or loan payment already."] = "Doprinos već prati svoj račun ili ratu kredita.",
        ["The date is before the card's valid from."] = "Datum je prije početka valjanosti kartice.",
        ["The end of the period is before its start."] = "Kraj razdoblja je prije njegova početka.",
        ["The export failed. See the log for details."] = "Izvoz nije uspio. Pojedinosti su u zapisniku.",
        ["The file is incomplete: it does not hold all the data Monthwise expects."] =
            "Datoteka je nepotpuna: ne sadrži sve podatke koje Monthwise očekuje.",
        ["The file was not made by a release of Monthwise this one knows."] =
            "Datoteku nije izradila verzija Monthwisea koju ova verzija poznaje.",
        ["The import failed and no data was changed. See the log for details."] =
            "Uvoz nije uspio i nijedan podatak nije promijenjen. Pojedinosti su u zapisniku.",
        ["The loan cannot end before a paid payment."] = "Kredit ne može završiti prije plaćene rate.",
        ["The participant pays at least one share and fewer than all of them."] = "Sudionik plaća barem jedan udio i manje od svih.",
        ["The password is wrong or the file is damaged."] = "Lozinka je pogrešna ili je datoteka oštećena.",
        ["The purchase date is before the card's valid from."] = "Datum kupnje je prije početka valjanosti kartice.",
        ["The replacement card does not exist."] = "Zamjenska kartica ne postoji.",
        ["The replacement card is archived, blocked or expired."] = "Zamjenska kartica je arhivirana, blokirana ili istekla.",
        ["The residual value is already paid and cannot be changed."] = "Ostatak vrijednosti već je plaćen i ne može se mijenjati.",
        ["There is no monthly bill in this month."] = "U ovom mjesecu nema mjesečnog računa.",
        ["There is no safety copy."] = "Nema sigurnosne kopije.",
        ["This IBAN is already registered."] = "Ovaj je IBAN već upisan.",
        ["This account already has a starting balance. Add a correction instead."] =
            "Ovaj račun već ima početni saldo. Umjesto toga dodajte ispravak.",
        ["This account does not exist."] = "Ovaj račun ne postoji.",
        ["This account is blocked, expired or archived and cannot receive new inputs."] =
            "Ovaj je račun blokiran, istekao ili arhiviran i ne može primati nove uplate.",
        ["This account is blocked, expired or archived."] = "Ovaj je račun blokiran, istekao ili arhiviran.",
        ["This account or card does not exist."] = "Ovaj račun ili kartica ne postoji.",
        ["This account or card is blocked, expired, archived or not valid yet."] =
            "Ovaj račun ili kartica je blokiran, istekao, arhiviran ili još nije valjan.",
        ["This bank does not exist."] = "Ova banka ne postoji.",
        ["This bill already belongs to a recurring bill."] = "Ovaj račun već pripada ponavljajućem računu.",
        ["This bill or loan payment is not shared."] = "Ovaj račun ili rata kredita ne dijeli se.",
        ["This card allows fewer installments."] = "Ova kartica omogućuje manje rata.",
        ["This card does not allow monthly installments."] = "Ova kartica ne omogućuje mjesečne rate.",
        ["This card does not exist."] = "Ova kartica ne postoji.",
        ["This card has a revolving debt, charges or payments, so its type cannot change to or from Revolving."] =
            "Ova kartica ima revolving dug, terećenja ili uplate, pa joj se vrsta ne može promijeniti u revolving ni iz njega.",
        ["This card has purchases, so it must keep allowing monthly installments."] =
            "Ova kartica ima kupnje, pa mora i dalje omogućavati mjesečne rate.",
        ["This card is already archived."] = "Ova je kartica već arhivirana.",
        ["This card is already blocked."] = "Ova je kartica već blokirana.",
        ["This card is already marked as expired."] = "Ova je kartica već označena kao istekla.",
        ["This card is blocked, expired or archived and cannot be changed."] =
            "Ova je kartica blokirana, istekla ili arhivirana i ne može se mijenjati.",
        ["This card is blocked, expired, archived or not valid yet."] = "Ova je kartica blokirana, istekla, arhivirana ili još nije valjana.",
        ["This card is not blocked."] = "Ova kartica nije blokirana.",
        ["This card number is already registered."] = "Ovaj je broj kartice već upisan.",
        ["This card payment does not exist."] = "Ovo plaćanje kartice ne postoji.",
        ["This category already has a subcategory with this name."] = "Ova kategorija već ima potkategoriju s ovim nazivom.",
        ["This category does not exist."] = "Ova kategorija ne postoji.",
        ["This category is hidden."] = "Ova je kategorija skrivena.",
        ["This category is used and cannot be deleted. Hide it instead."] =
            "Ova se kategorija koristi i ne može se obrisati. Umjesto toga je sakrijte.",
        ["This category is used by inputs and cannot be deleted. Hide it instead."] =
            "Ovu kategoriju koriste uplate i ne može se obrisati. Umjesto toga je sakrijte.",
        ["This category is used by loans and cannot be deleted. Hide it instead."] =
            "Ovu kategoriju koriste krediti i ne može se obrisati. Umjesto toga je sakrijte.",
        ["This file is too large to be a Monthwise export file."] = "Ova je datoteka prevelika da bi bila datoteka izvoza Monthwisea.",
        ["This installment does not exist."] = "Ova rata ne postoji.",
        ["This installment is already reversed."] = "Ova je rata već stornirana.",
        ["This is not a Monthwise export file."] = "Ovo nije datoteka izvoza Monthwisea.",
        ["This issuer does not exist."] = "Ovaj izdavatelj ne postoji.",
        ["This lender does not exist."] = "Ovaj vjerovnik ne postoji.",
        ["This loan has paid payments and cannot be deleted. Close it instead."] =
            "Ovaj kredit ima plaćene rate i ne može se obrisati. Umjesto toga ga zatvorite.",
        ["This loan is closed and cannot be changed."] = "Ovaj je kredit zatvoren i ne može se mijenjati.",
        ["This monthly bill is not made yet: it is only an estimate."] = "Ovaj mjesečni račun još nije izdan: samo je procjena.",
        ["This name is already in the list."] = "Ovaj je naziv već na popisu.",
        ["This occurrence does not exist."] = "Ovo ponavljanje ne postoji.",
        ["This participant does not exist."] = "Ovaj sudionik ne postoji.",
        ["This participant is archived."] = "Ovaj je sudionik arhiviran.",
        ["This participant shares costs; archive it instead."] = "Ovaj sudionik dijeli troškove; umjesto toga ga arhivirajte.",
        ["This payment does not exist."] = "Ova uplata ne postoji.",
        ["This payment is already paid. Untick it first."] = "Ova je rata već plaćena. Najprije poništite oznaku.",
        ["This purchase is reversed and cannot be changed."] = "Ova je kupnja stornirana i ne može se mijenjati.",
        ["This recurring input has already ended."] = "Ova je ponavljajuća uplata već završila.",
        ["This revolving card does not exist."] = "Ova revolving kartica ne postoji.",
        ["This revolving card is not active."] = "Ova revolving kartica nije aktivna.",
        ["This revolving card still has debt. Pay the whole debt, or mark it as expired with a replacement card, before archiving it."] =
            "Ova revolving kartica još ima dug. Prije arhiviranja platite cijeli dug ili je označite kao isteklu sa zamjenskom karticom.",
        ["This subcategory does not exist."] = "Ova potkategorija ne postoji.",
        ["This subcategory is hidden."] = "Ova je potkategorija skrivena.",
        ["This subcategory is used by loans and cannot be deleted. Hide it instead."] =
            "Ovu potkategoriju koriste krediti i ne može se obrisati. Umjesto toga je sakrijte.",
        ["Valid through must be the same as or later than valid from."] =
            "Datum „vrijedi do” mora biti isti ili kasniji od datuma „vrijedi od”.",
        ["When the schedule changes, the first expected date must be after the last received occurrence."] =
            "Kada se raspored mijenja, prvi datum naplate mora biti nakon posljednjeg primljenog ponavljanja.",
    };
}
