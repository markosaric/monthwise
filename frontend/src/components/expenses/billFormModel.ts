import { z } from 'zod'
import type { Bill, BillRequest, RecurringBill, RecurringBillRequest } from '@/api/expenses'
import { dateEntryMessage, isInvalidDateEntry } from '@/lib/dates'
import { centsToEuroInput, parseEuroInput } from '@/lib/money'
import {
  noShare,
  shareFormSchema,
  shareFormValuesOf,
  toShareRequest,
} from '@/components/sharedCosts/shareModel'
import { expenseTexts } from './texts'

const t = expenseTexts
const isoDate = /^\d{4}-\d{2}-\d{2}$/
const dayOfMonth = /^\d{1,2}$/

/** An optional day of the month (recurring bills R7, R8): empty, or 1 to 31. */
function isDayOrEmpty(value: string): boolean {
  const day = value.trim()
  return day === '' || (dayOfMonth.test(day) && Number(day) >= 1 && Number(day) <= 31)
}

/** An optional amount (recurring bills R6): empty, or greater than zero. */
function isAmountOrEmpty(value: string): boolean {
  if (value.trim() === '') return true
  const cents = parseEuroInput(value)
  return cents !== null && cents > 0
}

function toDay(value: string): number | null {
  return value.trim() === '' ? null : Number(value.trim())
}

function toAmountCents(value: string): number | null {
  return value.trim() === '' ? null : parseEuroInput(value)
}

function dayOf(isoDateValue: string): string {
  return isoDate.test(isoDateValue) ? String(Number(isoDateValue.slice(8, 10))) : ''
}

/**
 * The form values of a bill (FR-4). A one-off bill (`requireIssued`) or a paid bill needs amount, issue
 * date and due date. A bill from a recurring bill may keep any of them empty and stays Not issued
 * until all three are entered (Appendix 2, A2.2). Ticking Recurring on a one-off bill adds the
 * optional amount, issue day and due day of the recurring bill made from it (A2.3).
 */
export function billFormSchema(requireIssued: boolean) {
  return z
    .object({
      name: z.string().trim().min(1, t.errors.nameRequired).max(50, t.errors.nameTooLong),
      categoryId: z.string().min(1, t.errors.categoryRequired),
      payee: z.string().max(100),
      amount: z.string(),
      issueDate: z.string(),
      dueDate: z.string(),
      paidFromId: z.string(),
      description: z.string().max(200),
      paid: z.boolean(),
      paidDate: z.string(),
      share: shareFormSchema,
      recurring: z.boolean(),
      recurringAmount: z.string(),
      recurringIssueDay: z.string(),
      recurringDueDay: z.string(),
    })
    .superRefine((values, ctx) => {
      if (requireIssued || values.paid) {
        const cents = parseEuroInput(values.amount)
        if (cents === null || cents <= 0) {
          ctx.addIssue({ code: 'custom', path: ['amount'], message: t.errors.amountInvalid })
        }
        if (isInvalidDateEntry(values.issueDate)) {
          ctx.addIssue({ code: 'custom', path: ['issueDate'], message: dateEntryMessage })
        } else if (!isoDate.test(values.issueDate)) {
          ctx.addIssue({ code: 'custom', path: ['issueDate'], message: t.errors.issueDateRequired })
        }
        if (isInvalidDateEntry(values.dueDate)) {
          ctx.addIssue({ code: 'custom', path: ['dueDate'], message: dateEntryMessage })
        } else if (!isoDate.test(values.dueDate)) {
          ctx.addIssue({ code: 'custom', path: ['dueDate'], message: t.errors.dueDateRequired })
        } else if (values.dueDate < values.issueDate) {
          ctx.addIssue({ code: 'custom', path: ['dueDate'], message: t.errors.dueBeforeIssue })
        }
      } else {
        if (!isAmountOrEmpty(values.amount)) {
          ctx.addIssue({ code: 'custom', path: ['amount'], message: t.errors.amountInvalid })
        }
        // An empty date is allowed here; a typed one must be a complete, existing date.
        if (values.issueDate !== '' && !isoDate.test(values.issueDate)) {
          ctx.addIssue({ code: 'custom', path: ['issueDate'], message: dateEntryMessage })
        }
        if (values.dueDate !== '' && !isoDate.test(values.dueDate)) {
          ctx.addIssue({ code: 'custom', path: ['dueDate'], message: dateEntryMessage })
        } else if (values.dueDate !== '' && values.dueDate < values.issueDate) {
          ctx.addIssue({ code: 'custom', path: ['dueDate'], message: t.errors.dueBeforeIssue })
        }
      }
      if (values.recurring) {
        if (!isAmountOrEmpty(values.recurringAmount)) {
          ctx.addIssue({
            code: 'custom',
            path: ['recurringAmount'],
            message: t.errors.optionalAmountInvalid,
          })
        }
        if (!isDayOrEmpty(values.recurringIssueDay)) {
          ctx.addIssue({
            code: 'custom',
            path: ['recurringIssueDay'],
            message: t.errors.dayInvalid,
          })
        }
        if (!isDayOrEmpty(values.recurringDueDay)) {
          ctx.addIssue({ code: 'custom', path: ['recurringDueDay'], message: t.errors.dayInvalid })
        }
      }
      if (values.paid) {
        if (isInvalidDateEntry(values.paidDate)) {
          ctx.addIssue({ code: 'custom', path: ['paidDate'], message: dateEntryMessage })
        } else if (!isoDate.test(values.paidDate)) {
          ctx.addIssue({ code: 'custom', path: ['paidDate'], message: t.errors.dateRequired })
        } else if (values.paidDate < values.issueDate) {
          ctx.addIssue({ code: 'custom', path: ['paidDate'], message: t.errors.paidBeforeIssue })
        }
        if (values.paidFromId === '') {
          ctx.addIssue({ code: 'custom', path: ['paidFromId'], message: t.errors.paidFromRequired })
        }
      }
    })
}

export type BillFormValues = z.infer<ReturnType<typeof billFormSchema>>

/** A new one-off bill, issued today. */
export function emptyBillFormValues(paidFromId: string, today: string): BillFormValues {
  return {
    name: '',
    categoryId: '',
    payee: '',
    amount: '',
    issueDate: today,
    dueDate: '',
    paidFromId,
    description: '',
    paid: false,
    paidDate: today,
    share: noShare,
    ...notRecurring,
  }
}

const notRecurring = {
  recurring: false,
  recurringAmount: '',
  recurringIssueDay: '',
  recurringDueDay: '',
}

/**
 * The Recurring bill section when Recurring is ticked (A2.3): prefilled from the bill's amount and the
 * days of its issue and due date.
 */
export function recurringOptionsOf(
  values: Pick<BillFormValues, 'amount' | 'issueDate' | 'dueDate'>,
) {
  return {
    recurringAmount: isAmountOrEmpty(values.amount) ? values.amount.trim() : '',
    recurringIssueDay: dayOf(values.issueDate),
    recurringDueDay: dayOf(values.dueDate),
  }
}

/**
 * A bill to edit, with what it already has (A2.2); a Not issued bill gets its due date hint (FR-2) and
 * today as issue date when it has none.
 */
export function billFormValuesOf(bill: Bill, today: string): BillFormValues {
  const notIssued = bill.state === 'NotIssued'
  return {
    name: bill.name,
    categoryId: bill.category.id,
    payee: bill.payee ?? '',
    amount: bill.amountCents > 0 ? centsToEuroInput(bill.amountCents) : '',
    issueDate: bill.issueDate ?? (notIssued ? today : ''),
    dueDate: bill.dueDate ?? bill.dueDateHint ?? '',
    paidFromId: bill.paidFrom?.id ?? '',
    description: bill.description ?? '',
    paid: bill.state === 'Paid',
    paidDate: bill.paidDate ?? today,
    share: shareFormValuesOf(bill.share),
    // A bill from a recurring bill already has one; the form shows it as ticked (A2.3).
    ...notRecurring,
  }
}

export function toBillRequest(values: BillFormValues): BillRequest {
  return {
    name: values.name.trim(),
    categoryId: values.categoryId,
    payee: values.payee.trim() === '' ? null : values.payee.trim(),
    amountCents: toAmountCents(values.amount) ?? 0,
    issueDate: values.issueDate !== '' ? values.issueDate : null,
    dueDate: values.dueDate !== '' ? values.dueDate : null,
    paidFromId: values.paidFromId === '' ? null : values.paidFromId,
    description: values.description.trim() === '' ? null : values.description.trim(),
    paidDate: values.paid ? values.paidDate : null,
    share: toShareRequest(values.share),
    recurring: values.recurring
      ? {
          amountCents: toAmountCents(values.recurringAmount),
          issueDay: toDay(values.recurringIssueDay),
          dueDay: toDay(values.recurringDueDay),
        }
      : null,
  }
}

/** API fields of the Recurring bill section and their form fields. */
export const recurringErrorFields = {
  'recurring.amountCents': 'recurringAmount',
  'recurring.issueDay': 'recurringIssueDay',
  'recurring.dueDay': 'recurringDueDay',
} as const

/** A recurring bill (FR-2), with its optional amount, issue day and due day (A2.1). */
export const recurringBillFormSchema = z.object({
  name: z.string().trim().min(1, t.errors.nameRequired).max(50, t.errors.nameTooLong),
  categoryId: z.string().min(1, t.errors.categoryRequired),
  payee: z.string().max(100),
  paidFromId: z.string(),
  description: z.string().max(200),
  share: shareFormSchema,
  amount: z.string().refine(isAmountOrEmpty, t.errors.optionalAmountInvalid),
  issueDay: z.string().refine(isDayOrEmpty, t.errors.dayInvalid),
  dueDay: z.string().refine(isDayOrEmpty, t.errors.dayInvalid),
})

export type RecurringBillFormValues = z.infer<typeof recurringBillFormSchema>

export function recurringBillFormValuesOf(bill?: RecurringBill): RecurringBillFormValues {
  return {
    name: bill?.name ?? '',
    categoryId: bill?.category.id ?? '',
    payee: bill?.payee ?? '',
    paidFromId: bill?.paidFrom?.id ?? '',
    description: bill?.description ?? '',
    share: shareFormValuesOf(bill?.share),
    amount: bill?.amountCents ? centsToEuroInput(bill.amountCents) : '',
    issueDay: bill?.issueDay ? String(bill.issueDay) : '',
    dueDay: bill?.dueDay ? String(bill.dueDay) : '',
  }
}

export function toRecurringBillRequest(values: RecurringBillFormValues): RecurringBillRequest {
  return {
    name: values.name.trim(),
    categoryId: values.categoryId,
    payee: values.payee.trim() === '' ? null : values.payee.trim(),
    paidFromId: values.paidFromId === '' ? null : values.paidFromId,
    description: values.description.trim() === '' ? null : values.description.trim(),
    share: toShareRequest(values.share),
    amountCents: toAmountCents(values.amount),
    issueDay: toDay(values.issueDay),
    dueDay: toDay(values.dueDay),
  }
}
