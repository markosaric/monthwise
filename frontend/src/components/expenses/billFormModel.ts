import { z } from 'zod'
import type { Bill, BillRequest, RecurringBill, RecurringBillRequest } from '@/api/expenses'
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

/**
 * The form values of a bill (FR-4). A bill from a recurring bill may stay Not issued (no amount and no
 * dates); once one of them is entered, or for a one-off bill (`requireIssued`), all three are needed.
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
    })
    .superRefine((values, ctx) => {
      const issued =
        requireIssued ||
        values.paid ||
        values.amount.trim() !== '' ||
        values.issueDate !== '' ||
        values.dueDate !== ''
      if (issued) {
        const cents = parseEuroInput(values.amount)
        if (cents === null || cents <= 0) {
          ctx.addIssue({ code: 'custom', path: ['amount'], message: t.errors.amountInvalid })
        }
        if (!isoDate.test(values.issueDate)) {
          ctx.addIssue({ code: 'custom', path: ['issueDate'], message: t.errors.issueDateRequired })
        }
        if (!isoDate.test(values.dueDate)) {
          ctx.addIssue({ code: 'custom', path: ['dueDate'], message: t.errors.dueDateRequired })
        } else if (values.dueDate < values.issueDate) {
          ctx.addIssue({ code: 'custom', path: ['dueDate'], message: t.errors.dueBeforeIssue })
        }
      }
      if (values.paid) {
        if (!isoDate.test(values.paidDate)) {
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
  }
}

/** A bill to edit; a Not issued bill gets its due date hint (FR-2) and today as issue date. */
export function billFormValuesOf(bill: Bill, today: string): BillFormValues {
  const notIssued = bill.state === 'NotIssued'
  return {
    name: bill.name,
    categoryId: bill.category.id,
    payee: bill.payee ?? '',
    amount: notIssued ? '' : centsToEuroInput(bill.amountCents),
    issueDate: bill.issueDate ?? (notIssued ? today : ''),
    dueDate: bill.dueDate ?? bill.dueDateHint ?? '',
    paidFromId: bill.paidFrom?.id ?? '',
    description: bill.description ?? '',
    paid: bill.state === 'Paid',
    paidDate: bill.paidDate ?? today,
    share: shareFormValuesOf(bill.share),
  }
}

export function toBillRequest(values: BillFormValues): BillRequest {
  const issued = values.amount.trim() !== '' || values.issueDate !== '' || values.dueDate !== ''
  return {
    name: values.name.trim(),
    categoryId: values.categoryId,
    payee: values.payee.trim() === '' ? null : values.payee.trim(),
    amountCents: issued ? (parseEuroInput(values.amount) ?? 0) : 0,
    issueDate: issued && values.issueDate !== '' ? values.issueDate : null,
    dueDate: issued && values.dueDate !== '' ? values.dueDate : null,
    paidFromId: values.paidFromId === '' ? null : values.paidFromId,
    description: values.description.trim() === '' ? null : values.description.trim(),
    paidDate: values.paid ? values.paidDate : null,
    share: toShareRequest(values.share),
  }
}

export const recurringBillFormSchema = z.object({
  name: z.string().trim().min(1, t.errors.nameRequired).max(50, t.errors.nameTooLong),
  categoryId: z.string().min(1, t.errors.categoryRequired),
  payee: z.string().max(100),
  paidFromId: z.string(),
  description: z.string().max(200),
  share: shareFormSchema,
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
  }
}
