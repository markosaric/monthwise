import { z } from 'zod'
import type { Share, ShareRequest } from '@/api/sharedCosts'
import { sharedCostTexts } from './texts'

/** Shares a cost can be split into (shared bills FR-2). */
export const minShares = 2
export const maxShares = 4

/** The share fields of a bill, recurring bill, loan or loan payment form. */
export const shareFormSchema = z
  .object({
    enabled: z.boolean(),
    participantId: z.string(),
    shares: z.number().int().min(minShares).max(maxShares),
    participantShares: z.number().int().min(1),
    contributionAccountId: z.string(),
  })
  .superRefine((share, ctx) => {
    if (!share.enabled) return
    if (share.participantId === '') {
      ctx.addIssue({
        code: 'custom',
        path: ['participantId'],
        message: sharedCostTexts.share.participantRequired,
      })
    }
    if (share.participantShares >= share.shares) {
      ctx.addIssue({
        code: 'custom',
        path: ['participantShares'],
        message: sharedCostTexts.errors.participantSharesInvalid,
      })
    }
  })

export type ShareFormValues = z.infer<typeof shareFormSchema>

export const noShare: ShareFormValues = {
  enabled: false,
  participantId: '',
  shares: 2,
  participantShares: 1,
  contributionAccountId: '',
}

export function shareFormValuesOf(share: Share | null | undefined): ShareFormValues {
  return share
    ? {
        enabled: true,
        participantId: share.participant.id,
        shares: share.shares,
        participantShares: share.participantShares,
        contributionAccountId: share.contributionAccount?.id ?? '',
      }
    : noShare
}

export function toShareRequest(values: ShareFormValues): ShareRequest | null {
  return values.enabled
    ? {
        participantId: values.participantId,
        shares: values.shares,
        participantShares: values.participantShares,
        contributionAccountId:
          values.contributionAccountId === '' ? null : values.contributionAccountId,
      }
    : null
}

/**
 * The participant's part in cents (FR-4): amount × participant shares ÷ shares, rounded to the nearest
 * cent with half a cent rounded up, in integers only, exactly as the back-end does.
 */
export function partOf(amountCents: number, shares: number, participantShares: number): number {
  return Math.floor((2 * amountCents * participantShares + shares) / (2 * shares))
}

/** What the household pays itself (FR-4). */
export function ownPartOf(amountCents: number, shares: number, participantShares: number): number {
  return amountCents - partOf(amountCents, shares, participantShares)
}

/** The participant's shares as a whole percentage (FR-2). */
export function percentageOf(shares: number, participantShares: number): number {
  return Math.round((100 * participantShares) / shares)
}

/** API field errors of the share (`share.participantId`, …) shown on the form's share field. */
export const shareErrorFields = {
  'share.participantId': 'share',
  'share.shares': 'share',
  'share.participantShares': 'share',
  'share.contributionAccountId': 'share',
} as const
