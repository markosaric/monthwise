import { plural, type Translation } from '@/i18n/texts'
import type { LendingTextsEn } from './texts'

/** Croatian lending labels and messages (money lending NFR-5). */
export const lendingTextsHr: Translation<LendingTextsEn> = {
  title: 'Posudbe',
  add: 'Dodaj posudbu',
  edit: 'Uredi posudbu',
  empty: 'Trenutno nikome niste posudili novac.',
  emptyReturned: 'Još nema vraćenih posudbi.',
  filters: {
    Open: 'Otvorene',
    Returned: 'Vraćene',
  },
  states: {
    Open: 'Otvorena',
    Overdue: 'Kasni',
    Returned: 'Vraćeno',
  },
  figures: {
    outstanding: 'Još duguju',
    overdue: 'Kasni',
    open: 'Otvorene posudbe',
  },
  labels: {
    lent: 'Posuđeno',
    returned: 'Vraćeno',
    outstanding: 'Nepodmireno',
    lentOn: (date: string) => `Posuđeno ${date}`,
    dueBack: (date: string) => `Očekuje se ${date}`,
    returnedOf: (returned: string, amount: string) => `${returned} od ${amount} vraćeno`,
    repayments: 'Povrati',
    noRepayments: 'Još ništa nije vraćeno.',
  },
  borrowers: {
    title: 'Po dužniku',
    hint: 'Dužnici su s popisa uplatitelja; preimenujte ih ili uklonite u Postavkama, Uplatitelji.',
  },
  fields: {
    borrower: 'Dužnik',
    amount: 'Iznos (€)',
    lendDate: 'Datum posudbe',
    fromAccount: 'S računa',
    expectedReturnDate: 'Očekivani povrat',
    description: 'Opis (neobavezno)',
    toAccount: 'Na račun',
    receivedDate: 'Primljeno',
    note: 'Bilješka (neobavezno)',
    chooseAccount: 'Odaberite račun…',
  },
  hints: {
    borrower:
      'Odaberite spremljenog uplatitelja ili upišite novo ime; dodaje se na popis uplatitelja.',
    fromAccount: 'Novac odlazi s ovog računa na datum posudbe.',
    expectedReturnDate: 'Promijenite ga kada dužnik zatraži više vremena.',
    noAccount: 'Najprije postavite račun, u Postavkama, Kartice i računi.',
  },
  errors: {
    borrowerRequired: 'Upišite dužnika.',
    borrowerTooLong: 'Dužnik može imati najviše 100 znakova.',
    amountInvalid: 'Unesite iznos u eurima veći od nule, npr. 42,50.',
    dateRequired: 'Unesite datum.',
    accountRequired: 'Odaberite račun.',
    returnBeforeLend: 'Očekivani datum povrata mora biti isti ili kasniji od datuma posudbe.',
    receivedBeforeLend: 'Datum primitka mora biti isti ili kasniji od datuma posudbe.',
    aboveOutstanding: 'Povrat ne može biti veći od nepodmirenog iznosa.',
  },
  actions: {
    edit: 'Uredi',
    save: 'Spremi',
    cancel: 'Odustani',
    delete: 'Obriši',
    recordRepayment: 'Zabilježi povrat',
    editRepayment: 'Uredi povrat',
    deleteRepayment: 'Obriši povrat',
    setUpAccounts: 'Postavi račune',
  },
  dialogs: {
    deleteTitle: (borrower: string) => `Obrisati posudbu dužniku ${borrower}?`,
    deleteDescription: (amount: string, date: string, repayments: number) =>
      repayments === 0
        ? `${amount} posuđeno ${date}. Ovo se ne može poništiti.`
        : `${amount} posuđeno ${date}. Brišu se i ${repayments} ${plural(repayments, { one: 'povrat', few: 'povrata', other: 'povrata' })}. Ovo se ne može poništiti.`,
    repaymentTitle: (borrower: string) => `Povrat od dužnika ${borrower}`,
    repaymentDescription: (outstanding: string) => `Nepodmireno: ${outstanding}.`,
    deleteRepaymentTitle: 'Obrisati ovaj povrat?',
    deleteRepaymentDescription: (amount: string, date: string) =>
      `${amount} primljeno ${date}. Iznos ponovno postaje nepodmiren.`,
  },
  month: {
    title: 'Posudbe',
    lent: 'Posuđeno',
    returned: 'Vraćeno',
    expectedBack: 'Očekuje se povrat',
    empty: 'Ovaj mjesec nema posudbi.',
    totals: {
      lent: 'Posuđeno',
      returned: 'Vraćeno',
      expectedBack: 'Očekuje se',
    },
  },
  report: {
    total: 'Ukupno',
    title: 'Posudbe',
    borrower: 'Dužnik',
  },
  dashboard: {
    title: 'Posudbe',
    owing: (count: number) =>
      `${count} ${plural(count, { one: 'otvorena posudba', few: 'otvorene posudbe', other: 'otvorenih posudbi' })}`,
    overdue: (amount: string) => `${amount} kasni`,
  },
  chart: {
    borrower: 'Dužnik',
    allBorrowers: 'Svi dužnici',
  },
  saved: 'Spremljeno.',
  deleted: 'Obrisano.',
  repaymentSaved: 'Povrat je spremljen.',
}
