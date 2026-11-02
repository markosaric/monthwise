import type * as React from 'react'
import { Input } from '@/components/ui/input'
import { formatCardMonthInput } from '@/lib/dates'

/**
 * Month and year as printed on a card ("MM/YY"), the same in every browser (Appendix 4).
 * Only digits are kept and the slash is added after the month.
 */
export function CardMonthInput({
  value,
  onChange,
  ...props
}: Omit<React.ComponentProps<'input'>, 'value' | 'onChange' | 'type'> & {
  value: string
  onChange: (value: string) => void
}) {
  return (
    <Input
      {...props}
      type="text"
      inputMode="numeric"
      autoComplete="off"
      placeholder="MM/YY"
      maxLength={5}
      value={value}
      onChange={(event) => onChange(formatCardMonthInput(event.target.value))}
    />
  )
}
