import type { InputEntry, MonthInputs } from '@/api/cashFlow'

export const salary = { id: 'cat-salary', name: 'Salary', isPredefined: true, isHidden: false }
export const otherIncome = {
  id: 'cat-other',
  name: 'Other income',
  isPredefined: true,
  isHidden: false,
}

export function anEntry(overrides: Partial<InputEntry> = {}): InputEntry {
  return {
    id: 'input-1',
    recurringInputId: null,
    occurrenceDate: null,
    payer: { id: 'payer-1', name: 'Example Employer' },
    category: { id: salary.id, name: salary.name },
    account: { id: 'card-1', name: 'Tekući' },
    amountCents: 185000,
    issueDate: '2026-10-01',
    expectedDate: '2026-10-10',
    receivedDate: null,
    description: null,
    isReceived: false,
    isRecurring: false,
    frequency: null,
    ...overrides,
  }
}

export function aMonth(inputs: InputEntry[], month = '2026-10'): MonthInputs {
  const expected = inputs.filter((i) => !i.isReceived).reduce((sum, i) => sum + i.amountCents, 0)
  const received = inputs.filter((i) => i.isReceived).reduce((sum, i) => sum + i.amountCents, 0)
  return {
    month,
    inputs,
    totals: { expectedCents: expected, receivedCents: received, totalCents: expected + received },
  }
}
