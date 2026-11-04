import type * as React from 'react'
import { Input } from '@/components/ui/input'
import { dateInputToIso, formatDateInput, isoToDateInput } from '@/lib/dates'

/**
 * A day typed as "DD.MM.YYYY", the same in every browser (design refresh Appendix 2). Only digits
 * are kept and the dots are added. The value is "YYYY-MM-DD" once the text is a complete, existing
 * date, '' when empty, and the typed text otherwise (never a valid date, see `isInvalidDateEntry`).
 */
export function DateInput({
  value,
  onChange,
  ...props
}: Omit<React.ComponentProps<'input'>, 'value' | 'onChange' | 'type' | 'min' | 'max'> & {
  value: string
  onChange: (value: string) => void
}) {
  return (
    <Input
      {...props}
      type="text"
      inputMode="numeric"
      autoComplete="off"
      placeholder="DD.MM.YYYY"
      value={isoToDateInput(value)}
      onChange={(event) => {
        const text = formatDateInput(event.target.value)
        onChange(dateInputToIso(text) ?? text)
      }}
    />
  )
}
