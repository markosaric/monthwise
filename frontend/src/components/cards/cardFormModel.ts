import { z } from 'zod'
import { cardTypes, type CardRequest, type CardType } from '@/api/cards'
import {
  isValidCardNumber,
  isValidIban,
  normalizeCardNumber,
  normalizeIban,
} from '@/lib/cardNumbers'
import { parseEuroInput } from '@/lib/money'
import { cardTexts } from './texts'

const t = cardTexts

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
  })

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
  }
}
