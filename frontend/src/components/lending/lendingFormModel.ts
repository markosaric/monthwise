import { z } from 'zod'
import type { Lending, LendingRequest, Repayment, RepaymentRequest } from '@/api/lending'
import { requiredDate } from '@/lib/dateSchema'
import { centsToEuroInput, parseEuroInput } from '@/lib/money'
import { lendingTexts } from './texts'

const t = lendingTexts

export const borrowerMaxLength = 100
export const descriptionMaxLength = 200

/** The form values of a lending (L1 to L6). */
export const lendingFormSchema = z
  .object({
    borrowerName: z
      .string()
      .trim()
      .min(1, t.errors.borrowerRequired)
      .max(borrowerMaxLength, t.errors.borrowerTooLong),
    amount: z.string(),
    lendDate: requiredDate(t.errors.dateRequired),
    fromAccountId: z.string().min(1, t.errors.accountRequired),
    expectedReturnDate: requiredDate(t.errors.dateRequired),
    description: z.string().max(descriptionMaxLength),
  })
  .superRefine((values, ctx) => {
    const cents = parseEuroInput(values.amount)
    if (cents === null || cents <= 0) {
      ctx.addIssue({ code: 'custom', path: ['amount'], message: t.errors.amountInvalid })
    }
    if (values.expectedReturnDate !== '' && values.expectedReturnDate < values.lendDate) {
      ctx.addIssue({
        code: 'custom',
        path: ['expectedReturnDate'],
        message: t.errors.returnBeforeLend,
      })
    }
  })

export type LendingFormValues = z.infer<typeof lendingFormSchema>

/** A new lending dated today, from the only primary account if there is exactly one (L3, L4). */
export function emptyLendingFormValues(today: string, fromAccountId: string): LendingFormValues {
  return {
    borrowerName: '',
    amount: '',
    lendDate: today,
    fromAccountId,
    expectedReturnDate: '',
    description: '',
  }
}

export function lendingFormValuesOf(lending: Lending): LendingFormValues {
  return {
    borrowerName: lending.borrower.name,
    amount: centsToEuroInput(lending.amountCents),
    lendDate: lending.lendDate,
    fromAccountId: lending.fromAccount.id,
    expectedReturnDate: lending.expectedReturnDate,
    description: lending.description ?? '',
  }
}

export function toLendingRequest(values: LendingFormValues): LendingRequest {
  return {
    borrowerName: values.borrowerName.trim(),
    amountCents: parseEuroInput(values.amount) ?? 0,
    lendDate: values.lendDate,
    fromAccountId: values.fromAccountId,
    expectedReturnDate: values.expectedReturnDate,
    description: values.description.trim() === '' ? null : values.description.trim(),
  }
}

/** The form values of a repayment (R1 to R4). */
export interface RepaymentFormValues {
  amount: string
  toAccountId: string
  receivedDate: string
  note: string
}

/**
 * A new repayment of everything outstanding, to the lending's account, received today (FR-4, R2,
 * R3), or the values of a saved repayment.
 */
export function repaymentFormValuesOf(
  lending: Lending,
  today: string,
  repayment?: Repayment,
): RepaymentFormValues {
  if (repayment) {
    return {
      amount: centsToEuroInput(repayment.amountCents),
      toAccountId: repayment.toAccount.id,
      receivedDate: repayment.receivedDate,
      note: repayment.note ?? '',
    }
  }
  return {
    amount: centsToEuroInput(lending.outstandingCents),
    toAccountId: lending.fromAccount.id,
    receivedDate: today < lending.lendDate ? lending.lendDate : today,
    note: '',
  }
}

/**
 * The field errors of a repayment, checked before saving (R1, R3): the amount may be at most what is
 * outstanding, counting the edited repayment's own amount as outstanding again.
 */
export function repaymentErrors(
  values: RepaymentFormValues,
  lending: Lending,
  repayment?: Repayment,
): Partial<Record<keyof RepaymentFormValues, string>> {
  const errors: Partial<Record<keyof RepaymentFormValues, string>> = {}
  const cents = parseEuroInput(values.amount)
  const available = lending.outstandingCents + (repayment?.amountCents ?? 0)
  if (cents === null || cents <= 0) errors.amount = t.errors.amountInvalid
  else if (cents > available) errors.amount = t.errors.aboveOutstanding
  if (values.toAccountId === '') errors.toAccountId = t.errors.accountRequired
  if (values.receivedDate === '') errors.receivedDate = t.errors.dateRequired
  else if (values.receivedDate < lending.lendDate) errors.receivedDate = t.errors.receivedBeforeLend
  return errors
}

export function toRepaymentRequest(values: RepaymentFormValues): RepaymentRequest {
  return {
    amountCents: parseEuroInput(values.amount) ?? 0,
    toAccountId: values.toAccountId,
    receivedDate: values.receivedDate,
    note: values.note.trim() === '' ? null : values.note.trim(),
  }
}
