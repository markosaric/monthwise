import { z } from 'zod'
import type { CardPurchaseDetails, CardPurchaseRequest } from '@/api/cardPurchases'
import { centsToEuroInput, parseEuroInput } from '@/lib/money'
import { cardPurchaseTexts } from './texts'

const t = cardPurchaseTexts

/** The form values of a purchase; the installment limit of the chosen card is checked by `purchaseFormSchema`. */
export function purchaseFormSchema(maxInstallments: number) {
  return z
    .object({
      cardId: z.string().min(1, t.errors.cardRequired),
      description: z
        .string()
        .trim()
        .min(1, t.errors.descriptionRequired)
        .max(100, t.errors.descriptionTooLong),
      purchaseDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, t.errors.dateRequired),
      amount: z.string(),
      inInstallments: z.boolean(),
      installmentCount: z.string(),
      postponeFirst: z.boolean(),
      categoryId: z.string(),
    })
    .superRefine((values, ctx) => {
      const cents = parseEuroInput(values.amount)
      if (cents === null || cents <= 0) {
        ctx.addIssue({ code: 'custom', path: ['amount'], message: t.errors.amountInvalid })
      }
      if (values.inInstallments) {
        const count = /^\d+$/.test(values.installmentCount.trim())
          ? Number(values.installmentCount.trim())
          : NaN
        if (!(count >= 2 && count <= maxInstallments)) {
          ctx.addIssue({
            code: 'custom',
            path: ['installmentCount'],
            message: t.errors.installmentCountInvalid(maxInstallments),
          })
        }
      }
    })
}

export type PurchaseFormValues = z.infer<ReturnType<typeof purchaseFormSchema>>

/** A new purchase: not in installments by default (P5). */
export function emptyPurchaseFormValues(cardId: string, today: string): PurchaseFormValues {
  return {
    cardId,
    description: '',
    purchaseDate: today,
    amount: '',
    inInstallments: false,
    installmentCount: '12',
    postponeFirst: false,
    categoryId: '',
  }
}

export function purchaseFormValuesOf(details: CardPurchaseDetails): PurchaseFormValues {
  const { purchase } = details
  return {
    cardId: purchase.card.id,
    description: purchase.description,
    purchaseDate: purchase.purchaseDate,
    amount: centsToEuroInput(purchase.amountCents),
    inInstallments: purchase.inInstallments,
    installmentCount: purchase.inInstallments ? String(purchase.installmentCount) : '12',
    postponeFirst: purchase.postponeFirst,
    categoryId: purchase.category?.id ?? '',
  }
}

export function toPurchaseRequest(values: PurchaseFormValues): CardPurchaseRequest {
  return {
    cardId: values.cardId,
    description: values.description.trim(),
    purchaseDate: values.purchaseDate,
    amountCents: parseEuroInput(values.amount) ?? 0,
    inInstallments: values.inInstallments,
    installmentCount: values.inInstallments ? Number(values.installmentCount) : null,
    postponeFirst: values.inInstallments && values.postponeFirst,
    categoryId: values.categoryId === '' ? null : values.categoryId,
  }
}

/**
 * The installment amounts of a purchase (FR-4): equal parts rounded down to the cent, with the
 * remaining cents on the last one. Shown as a preview; the server calculates the real ones.
 */
export function installmentAmounts(amountCents: number, count: number): number[] {
  if (count < 1 || amountCents <= 0) return []
  const regular = Math.floor(amountCents / count)
  return Array.from({ length: count }, (_, i) =>
    i === count - 1 ? amountCents - regular * (count - 1) : regular,
  )
}
