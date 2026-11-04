import { z } from 'zod'
import { dateEntryMessage, isInvalidDateEntry } from './dates'

const isoDate = /^\d{4}-\d{2}-\d{2}$/

/**
 * A required date field: a filled-in text that is not a complete, existing date gets the
 * DD.MM.YYYY message (design refresh Appendix 2), an empty field gets `requiredMessage`.
 */
export function requiredDate(requiredMessage: string) {
  return z.string().superRefine((value, ctx) => {
    if (isInvalidDateEntry(value)) ctx.addIssue({ code: 'custom', message: dateEntryMessage })
    else if (!isoDate.test(value)) ctx.addIssue({ code: 'custom', message: requiredMessage })
  })
}

/** An optional date field: empty or a complete, existing date. */
export function optionalDate() {
  return z.string().refine((value) => !isInvalidDateEntry(value), dateEntryMessage)
}
