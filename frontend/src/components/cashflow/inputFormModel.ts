import { z } from 'zod'
import type {
  InputEntry,
  InputRequest,
  RecurrenceFrequency,
  RecurringInput,
  RecurringInputRequest,
} from '@/api/cashFlow'
import { frequencies } from '@/api/cashFlow'
import type { Card } from '@/api/cards'
import { centsToEuroInput, parseEuroInput } from '@/lib/money'
import { cashFlowTexts } from './texts'

const t = cashFlowTexts
const isoDate = /^\d{4}-\d{2}-\d{2}$/

export const inputFormSchema = z
  .object({
    payerName: z.string().trim().min(1, t.errors.payerRequired).max(100, t.errors.payerTooLong),
    accountId: z.string().min(1, t.errors.accountRequired),
    categoryId: z.string().min(1, t.errors.categoryRequired),
    amount: z.string(),
    issueDate: z.string().regex(isoDate, t.errors.dateRequired),
    expectedDate: z.string().regex(isoDate, t.errors.dateRequired),
    received: z.boolean(),
    receivedDate: z.string(),
    description: z.string().max(200, t.errors.descriptionTooLong),
    recurring: z.boolean(),
    frequency: z.enum(frequencies as [RecurrenceFrequency, ...RecurrenceFrequency[]]),
    endDate: z.string(),
  })
  .superRefine((values, ctx) => {
    const cents = parseEuroInput(values.amount)
    if (cents === null || cents <= 0) {
      ctx.addIssue({ code: 'custom', path: ['amount'], message: t.errors.amountInvalid })
    }
    if (
      isoDate.test(values.issueDate) &&
      isoDate.test(values.expectedDate) &&
      values.expectedDate < values.issueDate
    ) {
      ctx.addIssue({
        code: 'custom',
        path: ['expectedDate'],
        message: t.errors.expectedBeforeIssue,
      })
    }
    if (values.received) {
      if (!isoDate.test(values.receivedDate)) {
        ctx.addIssue({ code: 'custom', path: ['receivedDate'], message: t.errors.dateRequired })
      } else if (isoDate.test(values.issueDate) && values.receivedDate < values.issueDate) {
        ctx.addIssue({
          code: 'custom',
          path: ['receivedDate'],
          message: t.errors.receivedBeforeIssue,
        })
      }
    }
    if (values.recurring && values.endDate !== '' && values.endDate < values.expectedDate) {
      ctx.addIssue({ code: 'custom', path: ['endDate'], message: t.errors.endBeforeStart })
    }
  })

export type InputFormValues = z.infer<typeof inputFormSchema>

/** API field names that differ from form field names. */
export const apiFieldToFormField: Record<string, keyof InputFormValues> = {
  amountCents: 'amount',
  receivedDate: 'receivedDate',
}

/** Accounts that can receive new inputs: not blocked, expired or archived (FR-4). */
export function receivingAccounts(cards: Card[]): Card[] {
  return cards.filter((c) => !['Blocked', 'Expired', 'Archived'].includes(c.status))
}

/** The account to prefill: only when exactly one usable account is primary (FR-4). */
export function defaultAccountId(cards: Card[]): string {
  const primary = receivingAccounts(cards).filter((c) => c.isPrimary)
  return primary.length === 1 ? primary[0]!.id : ''
}

/** Values for a new input in the given month: issued on the first, expected on the 10th, or today in the current month. */
export function emptyInputFormValues(
  accountId: string,
  issueDate: string,
  expectedDate: string,
): InputFormValues {
  return {
    payerName: '',
    accountId,
    categoryId: '',
    amount: '',
    issueDate,
    expectedDate,
    received: false,
    receivedDate: '',
    description: '',
    recurring: false,
    frequency: 'Monthly',
    endDate: '',
  }
}

export function inputFormValuesOf(entry: InputEntry): InputFormValues {
  return {
    payerName: entry.payer.name,
    accountId: entry.account.id,
    categoryId: entry.category.id,
    amount: centsToEuroInput(entry.amountCents),
    issueDate: entry.issueDate,
    expectedDate: entry.expectedDate,
    received: entry.isReceived,
    receivedDate: entry.receivedDate ?? '',
    description: entry.description ?? '',
    recurring: false,
    frequency: entry.frequency ?? 'Monthly',
    endDate: '',
  }
}

export function recurringFormValuesOf(recurring: RecurringInput): InputFormValues {
  return {
    payerName: recurring.payer.name,
    accountId: recurring.account.id,
    categoryId: recurring.category.id,
    amount: centsToEuroInput(recurring.amountCents),
    issueDate: recurring.issueDate,
    expectedDate: recurring.expectedDate,
    received: false,
    receivedDate: '',
    description: recurring.description ?? '',
    recurring: true,
    frequency: recurring.frequency ?? 'Monthly',
    endDate: recurring.endDate ?? '',
  }
}

export function toInputRequest(values: InputFormValues, withRecurrence: boolean): InputRequest {
  return {
    payerName: values.payerName.trim(),
    accountId: values.accountId,
    categoryId: values.categoryId,
    amountCents: parseEuroInput(values.amount) ?? 0,
    issueDate: values.issueDate,
    expectedDate: values.expectedDate,
    receivedDate: values.received ? values.receivedDate : null,
    description: values.description.trim() === '' ? null : values.description.trim(),
    recurrence:
      withRecurrence && values.recurring
        ? { frequency: values.frequency, endDate: values.endDate === '' ? null : values.endDate }
        : null,
  }
}

export function toRecurringRequest(values: InputFormValues): RecurringInputRequest {
  const { recurrence, receivedDate, ...rest } = toInputRequest(
    { ...values, received: false },
    false,
  )
  void recurrence
  void receivedDate
  return {
    ...rest,
    frequency: values.frequency,
    endDate: values.endDate === '' ? null : values.endDate,
  }
}
