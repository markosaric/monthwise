import type { LendingFilter, LendingState } from '@/api/lending'
import { localized } from '@/i18n/texts'
import { lendingTextsHr } from './texts.hr'

/** Lending labels and messages, in English and Croatian (money lending NFR-5). */
const en = {
  title: 'Lending',
  add: 'Add lending',
  edit: 'Edit lending',
  empty: 'No money lent to anyone right now.',
  emptyReturned: 'No returned lendings yet.',
  filters: {
    Open: 'Open',
    Returned: 'Returned',
  } satisfies Record<LendingFilter, string>,
  states: {
    Open: 'Open',
    Overdue: 'Overdue',
    Returned: 'Returned',
  } satisfies Record<LendingState, string>,
  figures: {
    outstanding: 'Still owed',
    overdue: 'Overdue',
    open: 'Open lendings',
  },
  labels: {
    lent: 'Lent',
    returned: 'Returned',
    outstanding: 'Outstanding',
    lentOn: (date: string) => `Lent on ${date}`,
    dueBack: (date: string) => `Expected back ${date}`,
    returnedOf: (returned: string, amount: string) => `${returned} of ${amount} returned`,
    repayments: 'Repayments',
    noRepayments: 'Nothing returned yet.',
  },
  borrowers: {
    title: 'By borrower',
    hint: 'Borrowers come from the payers list; rename or remove them in Settings, Payers.',
  },
  fields: {
    borrower: 'Borrower',
    amount: 'Amount (€)',
    lendDate: 'Lend date',
    fromAccount: 'From account',
    expectedReturnDate: 'Expected back on',
    description: 'Description (optional)',
    toAccount: 'To account',
    receivedDate: 'Received on',
    note: 'Note (optional)',
    chooseAccount: 'Choose the account…',
  },
  hints: {
    borrower: 'Pick a saved payer or type a new name; it is added to the payers list.',
    fromAccount: 'The money leaves this account on the lend date.',
    expectedReturnDate: 'Change it when the borrower asks for more time.',
    noAccount: 'Set up an account first, in Settings, Cards and accounts.',
  },
  errors: {
    borrowerRequired: 'Enter the borrower.',
    borrowerTooLong: 'Borrower can be at most 100 characters.',
    amountInvalid: 'Enter an amount in euros greater than zero, e.g. 42,50.',
    dateRequired: 'Enter the date.',
    accountRequired: 'Choose the account.',
    returnBeforeLend: 'Expected return date must be the same as or later than the lend date.',
    receivedBeforeLend: 'Received date must be the same as or later than the lend date.',
    aboveOutstanding: 'The repayment cannot be more than the outstanding amount.',
  },
  actions: {
    edit: 'Edit',
    save: 'Save',
    cancel: 'Cancel',
    delete: 'Delete',
    recordRepayment: 'Record repayment',
    editRepayment: 'Edit repayment',
    deleteRepayment: 'Delete repayment',
    setUpAccounts: 'Set up accounts',
  },
  dialogs: {
    deleteTitle: (borrower: string) => `Delete the lending to ${borrower}?`,
    deleteDescription: (amount: string, date: string, repayments: number) =>
      repayments === 0
        ? `${amount} lent on ${date}. This cannot be undone.`
        : `${amount} lent on ${date}. Its ${repayments === 1 ? 'repayment is' : `${repayments} repayments are`} deleted too. This cannot be undone.`,
    repaymentTitle: (borrower: string) => `Repayment from ${borrower}`,
    repaymentDescription: (outstanding: string) => `Outstanding: ${outstanding}.`,
    deleteRepaymentTitle: 'Delete this repayment?',
    deleteRepaymentDescription: (amount: string, date: string) =>
      `${amount} received on ${date}. The amount becomes outstanding again.`,
  },
  month: {
    title: 'Lending',
    lent: 'Lent',
    returned: 'Returned',
    expectedBack: 'Expected back',
    empty: 'No lending this month.',
    totals: {
      lent: 'Lent',
      returned: 'Returned',
      expectedBack: 'Expected back',
    },
  },
  report: {
    total: 'Total',
    title: 'Lending',
    borrower: 'Borrower',
  },
  dashboard: {
    title: 'Lending',
    owing: (count: number) => `${count} ${count === 1 ? 'lending' : 'lendings'} open`,
    overdue: (amount: string) => `${amount} overdue`,
  },
  chart: {
    borrower: 'Borrower',
    allBorrowers: 'All borrowers',
  },
  saved: 'Saved.',
  deleted: 'Deleted.',
  repaymentSaved: 'Repayment saved.',
} as const

export type LendingTextsEn = typeof en

export const lendingTexts = localized(en, lendingTextsHr)
