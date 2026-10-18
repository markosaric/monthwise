/** Texts of the Dashboard (design refresh FR-9 to FR-11, FR-14). */
export const dashboardTexts = {
  subtitle: 'How is this month going?',
  quickActions: {
    addIncome: 'Add income',
    addBill: 'Add bill',
    addExpense: 'Add expense',
    cashWithdrawal: 'Cash withdrawal',
    addPurchase: 'Add purchase',
  },
  keyFigures: 'Key figures',
  inputs: 'Inputs',
  outputs: 'Outputs',
  monthResult: 'Month result',
  stillToPay: 'Still to pay',
  received: (amount: string) => `${amount} received`,
  paid: (amount: string) => `${amount} paid`,
  items: (count: number) => `${count} ${count === 1 ? 'item' : 'items'}`,
  overdueItems: (count: number) => `${count} overdue`,
  highlight: {
    overdue: (count: number, amount: string) =>
      `${count} ${count === 1 ? 'bill or payment is' : 'bills or payments are'} overdue: ${amount}`,
    overdueHint: 'Open the Month screen to pay them.',
    next: (name: string, amount: string, date: string) => `Next: ${name} ${amount} on ${date}`,
    more: (count: number) =>
      count === 0
        ? 'Nothing else is due this month.'
        : `${count} more ${count === 1 ? 'bill or payment' : 'bills and payments'} due this month.`,
    allPaid: (month: string) => `All bills and payments of ${month} are paid`,
    allPaidHint: 'Nothing is left to pay this month.',
    empty: "Add this month's inputs and bills",
    emptyHint: 'Start on the Month screen.',
    openMonth: 'Open Month',
  },
  comingUp: 'Coming up · next 7 days',
  nothingComingUp: 'Nothing due in the next 7 days.',
  openMonth: 'Open Month',
  overview: {
    title: 'Month result',
    caption: 'Inputs minus outputs this month',
    moreInputs: (percent: number) => `${percent}% more inputs`,
    lessInputs: (percent: number) => `${percent}% less inputs`,
    moreOutputs: (percent: number) => `${percent}% more outputs`,
    lessOutputs: (percent: number) => `${percent}% less outputs`,
    thanLastMonth: ' than last month',
    chart: 'Inputs and outputs of the last 6 months',
  },
  outputsByKind: {
    title: "This month's outputs by kind",
    bills: 'Bills',
    expenses: 'Expenses',
    loanPayments: 'Loan payments',
    cardPayments: 'Card payments',
  },
  small: {
    loans: (amount: string) => `${amount} / month`,
    loansDetail: (amount: string) => `Loans · ${amount} to repay`,
    cardPayments: 'Card payments this month',
    cardsWithInstallments: (count: number) =>
      `${count} ${count === 1 ? 'card' : 'cards'} with installments`,
    sharedCosts: 'Shared costs outstanding',
    owing: (count: number) => `${count} ${count === 1 ? 'participant owes' : 'participants owe'}`,
    recurring: (count: number) => `${count} recurring ${count === 1 ? 'bill' : 'bills'}`,
    notIssued: (count: number) => `${count} not issued this month`,
  },
  loadError: 'The Dashboard could not be loaded.',
}
