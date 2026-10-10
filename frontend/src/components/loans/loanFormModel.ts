import { z } from 'zod'
import type { Card } from '@/api/cards'
import type { Loan, LoanRequest, PaymentMethod } from '@/api/loans'
import { paymentMethods } from '@/api/loans'
import { addMonths, isYearMonth } from '@/lib/dates'
import { centsToEuroInput, parseEuroInput } from '@/lib/money'
import {
  noShare,
  shareFormSchema,
  shareFormValuesOf,
  toShareRequest,
} from '@/components/sharedCosts/shareModel'
import { loanTexts } from './texts'

const t = loanTexts
const maxPaymentCount = 1200

/** A whole number from 1 to 1200 typed in a field, or null. */
export function parseCount(text: string): number | null {
  const trimmed = text.trim()
  if (!/^\d+$/.test(trimmed)) return null
  const count = Number(trimmed)
  return count >= 1 && count <= maxPaymentCount ? count : null
}

/** The end month of `count` payments from `startMonth` on (A11). */
export function endMonthOf(startMonth: string, count: number): string {
  return addMonths(startMonth, count - 1)
}

/** The number of payments from `startMonth` to `endMonth`, both included (A10); null when the end is earlier. */
export function paymentCountOf(startMonth: string, endMonth: string): number | null {
  const months =
    (Number(endMonth.slice(0, 4)) - Number(startMonth.slice(0, 4))) * 12 +
    Number(endMonth.slice(5, 7)) -
    Number(startMonth.slice(5, 7))
  return months >= 0 ? months + 1 : null
}

export const loanFormSchema = z
  .object({
    name: z.string().trim().min(1, t.errors.nameRequired).max(50, t.errors.nameTooLong),
    categoryId: z.string().min(1, t.errors.categoryRequired),
    subcategoryId: z.string().min(1, t.errors.subcategoryRequired),
    lenderId: z.string().min(1, t.errors.lenderRequired),
    paymentMethod: z.enum(paymentMethods as [PaymentMethod, ...PaymentMethod[]]),
    accountId: z.string(),
    startMonth: z.string(),
    paymentDay: z.string(),
    monthlyPayment: z.string(),
    paymentCount: z.string(),
    endMonth: z.string(),
    /** Which of the two the user typed last: it wins when both are filled in. */
    lastEdited: z.enum(['count', 'end']),
    residualValue: z.string(),
    allowsResidualValue: z.boolean(),
    description: z.string().max(200, t.errors.descriptionTooLong),
    share: shareFormSchema,
    /** Whether expected payments with a share set by hand keep it when the loan's share changes (shared bills FR-3). */
    keepChangedPaymentShares: z.boolean(),
  })
  .superRefine((values, ctx) => {
    const issue = (path: string, message: string) =>
      ctx.addIssue({ code: 'custom', path: [path], message })

    if (values.paymentMethod === 'StandingOrder' && values.accountId === '') {
      issue('accountId', t.errors.accountRequired)
    }
    if (!isYearMonth(values.startMonth)) issue('startMonth', t.errors.monthRequired)
    const day = Number(values.paymentDay)
    if (!/^\d+$/.test(values.paymentDay.trim()) || day < 1 || day > 31) {
      issue('paymentDay', t.errors.paymentDayInvalid)
    }
    const monthly = parseEuroInput(values.monthlyPayment)
    if (monthly === null || monthly <= 0) issue('monthlyPayment', t.errors.amountInvalid)

    if (values.lastEdited === 'count') {
      if (values.paymentCount.trim() === '') issue('paymentCount', t.errors.countOrEndRequired)
      else if (parseCount(values.paymentCount) === null)
        issue('paymentCount', t.errors.countInvalid)
    } else if (!isYearMonth(values.endMonth)) {
      issue('endMonth', t.errors.countOrEndRequired)
    } else if (isYearMonth(values.startMonth) && values.endMonth < values.startMonth) {
      issue('endMonth', t.errors.endBeforeStart)
    }

    if (values.allowsResidualValue && values.residualValue.trim() !== '') {
      const residual = parseEuroInput(values.residualValue)
      if (residual === null || residual <= 0) issue('residualValue', t.errors.amountInvalid)
    }
  })

export type LoanFormValues = z.infer<typeof loanFormSchema>

/** API field names that differ from form field names. */
export const apiFieldToFormField: Record<string, keyof LoanFormValues> = {
  monthlyPaymentCents: 'monthlyPayment',
  residualValueCents: 'residualValue',
  'share.participantId': 'share',
  'share.shares': 'share',
  'share.participantShares': 'share',
  'share.contributionAccountId': 'share',
}

/** Accounts a standing order can charge: primary and not blocked, expired, archived or not yet valid (FR-3). */
export function standingOrderAccounts(cards: Card[]): Card[] {
  return cards.filter((c) => c.isPrimary && ['Active', 'ExpiringSoon'].includes(c.status))
}

/** Accounts a payment can be paid from: not blocked, expired or archived (FR-5). */
export function payingAccounts(cards: Card[]): Card[] {
  return cards.filter((c) => !['Blocked', 'Expired', 'Archived'].includes(c.status))
}

/** The account to prefill: only when exactly one account can be chosen (FR-3, FR-5). */
export function onlyPrimaryAccountId(cards: Card[]): string {
  const primary = payingAccounts(cards).filter((c) => c.isPrimary)
  return primary.length === 1 ? primary[0]!.id : ''
}

export function emptyLoanFormValues(accountId: string, startMonth: string): LoanFormValues {
  return {
    name: '',
    categoryId: '',
    subcategoryId: '',
    lenderId: '',
    paymentMethod: 'StandingOrder',
    accountId,
    startMonth,
    paymentDay: '',
    monthlyPayment: '',
    paymentCount: '',
    endMonth: '',
    lastEdited: 'count',
    residualValue: '',
    allowsResidualValue: false,
    description: '',
    share: noShare,
    keepChangedPaymentShares: true,
  }
}

export function loanFormValuesOf(loan: Loan, allowsResidualValue: boolean): LoanFormValues {
  return {
    name: loan.name,
    categoryId: loan.category.id,
    subcategoryId: loan.subcategory.id,
    lenderId: loan.lender.id,
    paymentMethod: loan.paymentMethod,
    accountId: loan.account?.id ?? '',
    startMonth: loan.startMonth,
    paymentDay: String(loan.paymentDay),
    monthlyPayment: centsToEuroInput(loan.monthlyPaymentCents),
    paymentCount: String(loan.paymentCount),
    endMonth: loan.endMonth,
    lastEdited: 'count',
    residualValue:
      loan.residualValueCents === null ? '' : centsToEuroInput(loan.residualValueCents),
    // A loan that already has a residual value keeps it, even when its category no longer allows one.
    allowsResidualValue: allowsResidualValue || loan.residualValueCents !== null,
    description: loan.description ?? '',
    share: shareFormValuesOf(loan.share),
    keepChangedPaymentShares: true,
  }
}

export function toLoanRequest(values: LoanFormValues): LoanRequest {
  const byCount = values.lastEdited === 'count'
  const standingOrder = values.paymentMethod === 'StandingOrder'
  return {
    name: values.name.trim(),
    categoryId: values.categoryId,
    subcategoryId: values.subcategoryId,
    lenderId: values.lenderId,
    paymentMethod: values.paymentMethod,
    accountId: standingOrder && values.accountId !== '' ? values.accountId : null,
    startMonth: values.startMonth,
    paymentDay: Number(values.paymentDay),
    monthlyPaymentCents: parseEuroInput(values.monthlyPayment) ?? 0,
    paymentCount: byCount ? parseCount(values.paymentCount) : null,
    endMonth: byCount ? null : values.endMonth,
    residualValueCents:
      values.allowsResidualValue && values.residualValue.trim() !== ''
        ? (parseEuroInput(values.residualValue) ?? 0)
        : null,
    description: values.description.trim() === '' ? null : values.description.trim(),
    share: toShareRequest(values.share),
    keepChangedPaymentShares: values.keepChangedPaymentShares,
  }
}
