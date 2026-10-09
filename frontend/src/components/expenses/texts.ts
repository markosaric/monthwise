import type { BillState } from '@/api/expenses'

/**
 * Bill, expense and expense category labels and messages in one place, so they can be translated to
 * Croatian later (bills and expenses NFR-5).
 */
export const expenseTexts = {
  bills: {
    title: 'Bills',
    empty: 'No bills this month yet.',
    addBill: 'Add bill',
    addRecurring: 'Add recurring bills',
    addRecurringTo: (month: string) => `Add recurring bills to ${month}`,
    added: (count: number, month: string) =>
      count === 0
        ? `All recurring bills are already in ${month}.`
        : `${count} ${count === 1 ? 'bill' : 'bills'} added to ${month}.`,
    recurring: 'Recurring bills',
    editBill: 'Edit bill',
    issueBill: 'Enter bill',
    notIssuedCount: (count: number) => `${count} not issued yet`,
    withCard: 'Paid with credit cards (in their card payments)',
  },
  states: {
    NotIssued: 'Not issued',
    Expected: 'Expected',
    Paid: 'Paid',
  } satisfies Record<BillState, string>,
  recurring: {
    title: 'Recurring bills',
    description:
      'Bills that arrive every month. Add them to a month with “Add recurring bills” on the Month screen.',
    add: 'Add recurring bill',
    edit: 'Edit recurring bill',
    empty: 'No recurring bills yet.',
    showStopped: 'Show stopped',
    stopped: 'Stopped',
    stop: 'Stop',
    restart: 'Restart',
    saved: 'Recurring bill saved.',
    deleted: 'Recurring bill deleted.',
    noDates:
      'Recurring bills have no dates or amount: you enter them on each bill when it arrives.',
    deleteTitle: (name: string) => `Delete ${name}?`,
    deleteDescription: 'The recurring bill is removed. It was never added to a month.',
  },
  expenses: {
    title: 'Expenses',
    empty: 'No expenses this month yet.',
    add: 'Add expense',
    edit: 'Edit expense',
    cashWithdrawal: 'Cash withdrawal',
    addCashWithdrawal: 'Cash withdrawal',
    editCashWithdrawal: 'Edit cash withdrawal',
    totals: {
      total: 'Total',
      fromAccounts: 'From accounts',
      withCard: 'With credit cards',
      cash: 'Cash withdrawals',
    },
  },
  fields: {
    name: 'Name',
    category: 'Category',
    payee: 'Payee (optional)',
    amount: 'Amount (€)',
    issueDate: 'Issue date',
    dueDate: 'Due date',
    paid: 'Paid',
    paidDate: 'Paid on',
    paidFrom: 'Paid from',
    usualPaidFrom: 'Usually paid from (optional)',
    account: 'Primary account',
    date: 'Date',
    month: 'Month',
    description: 'Description (optional)',
    chooseCategory: 'Choose…',
    choosePaidFrom: 'Choose…',
    accounts: 'Accounts',
    creditCards: 'Credit cards',
  },
  hints: {
    dueDateHint: 'Prefilled with the due day of last month’s bill.',
    notIssued:
      'Amount, issue date and due date go together. Clear all three to keep the bill not issued.',
    creditCard: 'Paid with a credit card, it goes into that card’s card payment.',
    cashAccount: 'Cash can only be withdrawn from a primary account.',
  },
  labels: {
    overdue: 'Overdue',
    withCard: 'In card payment',
    hint: 'hint',
    due: 'due',
    issued: 'issued',
    paidOn: 'paid',
  },
  errors: {
    nameRequired: 'Name is required.',
    nameTooLong: 'Name can be at most 50 characters.',
    categoryRequired: 'Choose a category.',
    amountInvalid: 'Enter an amount in euros greater than zero, e.g. 42,50.',
    dateRequired: 'Enter the date.',
    issueDateRequired: 'Enter the issue date.',
    dueDateRequired: 'Enter the due date.',
    dueBeforeIssue: 'Due date must be the same as or later than the issue date.',
    paidBeforeIssue: 'Paid date must be the same as or later than the issue date.',
    paidFromRequired: 'Choose what it was paid with.',
    accountRequired: 'Choose the primary account.',
  },
  paidDialog: {
    title: (name: string) => `${name} paid`,
    description: (amount: string, due: string) => `Bill of ${amount}, due ${due}.`,
  },
  dialogs: {
    deleteBillTitle: (name: string) => `Delete ${name}?`,
    deleteBillDescription:
      'The bill is removed from its month. A recurring bill it was added from stays.',
    deleteExpenseTitle: (name: string) => `Delete ${name}?`,
    deleteExpenseDescription: 'The expense is removed.',
    chargedWarning:
      'This is in a card payment whose payment date has passed; that card payment will change.',
  },
  actions: {
    save: 'Save',
    cancel: 'Cancel',
    edit: 'Edit',
    delete: 'Delete',
  },
  saved: 'Saved.',
  deleted: 'Deleted.',
  categories: {
    section: 'Expense categories',
    description: 'One list for bills, expenses and card purchases, e.g. Electricity or Groceries.',
    newName: 'New category',
    add: 'Add',
    rename: 'Rename',
    hide: 'Hide',
    show: 'Show',
    delete: 'Delete',
    hidden: 'Hidden',
    fixed: 'Fixed',
    predefined: 'Starting category',
    usedHint:
      'A category used by bills, expenses or purchases cannot be deleted; hide it instead. Cash withdrawal always stays.',
    saved: 'Category saved.',
  },
} as const
