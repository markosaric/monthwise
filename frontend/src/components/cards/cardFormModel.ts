import { z } from 'zod'
import { cardTypes, type CardRequest, type CardType } from '@/api/cards'
import {
  isValidCardNumber,
  isValidIban,
  normalizeCardNumber,
  normalizeIban,
} from '@/lib/cardNumbers'
import { parseEuroInput } from '@/lib/money'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { cardTexts } from './texts'

const t = cardTexts
const billing = cardPurchaseTexts.billing

/** keep: leave the stored number as is (edit only); remove: delete it (edit only); enter: type a number. */
export type NumberMode = 'keep' | 'remove' | 'enter'

const yearMonth = /^\d{4}-(0[1-9]|1[0-2])$/

export const cardFormSchema = z
  .object({
    name: z.string().trim().min(1, t.errors.nameRequired).max(50, t.errors.nameTooLong),
    issuerId: z.string().min(1, t.errors.issuerRequired),
    bankId: z.string().min(1, t.errors.bankRequired),
    cardNumberMode: z.enum(['keep', 'remove', 'enter']),
    cardNumber: z.string(),
    ibanMode: z.enum(['keep', 'remove', 'enter']),
    iban: z.string(),
    validFrom: z.union([z.literal(''), z.string().regex(yearMonth, t.errors.monthRequired)]),
    validThrough: z.string().regex(yearMonth, t.errors.monthRequired),
    type: z.enum(cardTypes as [CardType, ...CardType[]]),
    isPrimary: z.boolean(),
    allowsInstallments: z.boolean(),
    debtLimit: z.string(),
    statementDay: z.string(),
    paymentDay: z.string(),
    payingAccountId: z.string(),
    paysFromAccountDirectly: z.boolean(),
    maxInstallments: z.enum(['12', '24', '36']),
  })
  .superRefine((values, ctx) => {
    if (
      values.cardNumberMode === 'enter' &&
      values.cardNumber.trim() !== '' &&
      !isValidCardNumber(values.cardNumber)
    ) {
      ctx.addIssue({ code: 'custom', path: ['cardNumber'], message: t.errors.cardNumberInvalid })
    }
    if (values.ibanMode === 'enter' && values.iban.trim() !== '' && !isValidIban(values.iban)) {
      ctx.addIssue({ code: 'custom', path: ['iban'], message: t.errors.ibanInvalid })
    }
    if (
      yearMonth.test(values.validFrom) &&
      yearMonth.test(values.validThrough) &&
      values.validThrough < values.validFrom
    ) {
      ctx.addIssue({ code: 'custom', path: ['validThrough'], message: t.errors.validityOrder })
    }
    if (values.debtLimit.trim() !== '' && parseEuroInput(values.debtLimit) === null) {
      ctx.addIssue({ code: 'custom', path: ['debtLimit'], message: t.errors.debtLimitInvalid })
    }
    if (allowsInstallments(values.type, values.allowsInstallments)) {
      const statement = dayOf(values.statementDay, 1, 30)
      const payment = dayOf(values.paymentDay, 2, 31)
      if (statement === null) {
        ctx.addIssue({
          code: 'custom',
          path: ['statementDay'],
          message: billing.errors.statementDayInvalid,
        })
      }
      if (payment === null) {
        ctx.addIssue({
          code: 'custom',
          path: ['paymentDay'],
          message: billing.errors.paymentDayInvalid,
        })
      } else if (statement !== null && payment <= statement) {
        ctx.addIssue({
          code: 'custom',
          path: ['paymentDay'],
          message: billing.errors.paymentNotAfterStatement,
        })
      }
      if (values.payingAccountId === '') {
        ctx.addIssue({
          code: 'custom',
          path: ['payingAccountId'],
          message: billing.errors.payingAccountRequired,
        })
      }
    }
  })

/** Credit and Charge cards always allow installments, Prepaid never, Debit by the check box (A11). */
export function allowsInstallments(type: CardType, debitAllows: boolean): boolean {
  return type === 'Credit' || type === 'Charge' || (type === 'Debit' && debitAllows)
}

/** A whole day number within the bounds, or null. */
function dayOf(text: string, min: number, max: number): number | null {
  if (!/^\d{1,2}$/.test(text.trim())) return null
  const day = Number(text.trim())
  return day >= min && day <= max ? day : null
}

export type CardFormValues = z.infer<typeof cardFormSchema>

/** Form values for a new card, optionally prefilled (e.g. from the card it replaces). */
export function emptyCardFormValues(prefill: Partial<CardFormValues> = {}): CardFormValues {
  return {
    name: '',
    issuerId: '',
    bankId: '',
    cardNumberMode: 'enter',
    cardNumber: '',
    ibanMode: 'enter',
    iban: '',
    validFrom: '',
    validThrough: '',
    type: 'Credit',
    isPrimary: false,
    allowsInstallments: false,
    debtLimit: '',
    statementDay: '',
    paymentDay: '',
    payingAccountId: '',
    paysFromAccountDirectly: false,
    maxInstallments: '36',
    ...prefill,
  }
}

function numberForRequest(
  mode: NumberMode,
  value: string,
  normalize: (v: string) => string,
  editing: boolean,
) {
  if (mode === 'keep') return null
  if (mode === 'remove') return ''
  const trimmed = value.trim()
  if (trimmed === '') return editing ? '' : null
  return normalize(trimmed)
}

export function toCardRequest(values: CardFormValues, editing: boolean): CardRequest {
  const withBilling = allowsInstallments(values.type, values.allowsInstallments)
  return {
    name: values.name.trim(),
    issuerId: values.issuerId,
    bankId: values.bankId,
    cardNumber: numberForRequest(
      values.cardNumberMode,
      values.cardNumber,
      normalizeCardNumber,
      editing,
    ),
    iban: numberForRequest(values.ibanMode, values.iban, normalizeIban, editing),
    validFrom: values.validFrom === '' ? null : values.validFrom,
    validThrough: values.validThrough,
    type: values.type,
    isPrimary: values.type === 'Debit' && values.isPrimary,
    allowsInstallments: values.type === 'Debit' && values.allowsInstallments,
    debtLimitCents: values.debtLimit.trim() === '' ? null : parseEuroInput(values.debtLimit),
    statementDay: withBilling ? Number(values.statementDay) : null,
    paymentDay: withBilling ? Number(values.paymentDay) : null,
    payingAccountId: withBilling ? values.payingAccountId : null,
    paysFromAccountDirectly: withBilling && values.paysFromAccountDirectly,
    maxInstallments: withBilling ? Number(values.maxInstallments) : null,
  }
}
