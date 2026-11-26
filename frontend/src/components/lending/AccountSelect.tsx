import type { Card } from '@/api/cards'
import { payableAccounts } from '@/components/expenses/paidFrom'
import { NativeSelect } from '@/components/ui/native-select'
import { lendingTexts } from './texts'

/**
 * The accounts money can be lent from or returned to: usable accounts, never credit cards (L4, R2).
 * `current` keeps the saved account on an entry whose account can no longer be chosen.
 */
export function AccountSelect({
  id,
  cards,
  value,
  current,
  invalid,
  onChange,
}: {
  id: string
  cards: Card[]
  value: string
  current?: { id: string; name: string } | null
  invalid?: boolean
  onChange: (value: string) => void
}) {
  const accounts = payableAccounts(cards)
  const listed = accounts.some((c) => c.id === current?.id)
  return (
    <NativeSelect
      id={id}
      value={value}
      aria-invalid={invalid}
      onChange={(e) => onChange(e.target.value)}
    >
      <option value="">{lendingTexts.fields.chooseAccount}</option>
      {current && !listed && <option value={current.id}>{current.name}</option>}
      {accounts.map((c) => (
        <option key={c.id} value={c.id}>
          {c.name}
        </option>
      ))}
    </NativeSelect>
  )
}
