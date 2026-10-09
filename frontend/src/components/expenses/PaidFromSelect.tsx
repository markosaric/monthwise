import type { Card } from '@/api/cards'
import type { PaidFrom } from '@/api/expenses'
import { NativeSelect } from '@/components/ui/native-select'
import { payableAccounts, payableCreditCards } from './paidFrom'
import { expenseTexts } from './texts'

const t = expenseTexts

/**
 * What a bill or expense is paid with, grouped into accounts and credit cards (FR-5). `current` keeps
 * an account or card that can no longer be chosen on an entry that already has it.
 */
export function PaidFromSelect({
  id,
  cards,
  value,
  current,
  primaryOnly = false,
  invalid,
  onChange,
}: {
  id: string
  cards: Card[]
  value: string
  current?: PaidFrom | null
  /** Only primary accounts (cash withdrawals, FR-8). */
  primaryOnly?: boolean
  invalid?: boolean
  onChange: (value: string) => void
}) {
  const accounts = payableAccounts(cards, primaryOnly)
  const creditCards = primaryOnly ? [] : payableCreditCards(cards)
  const listed = [...accounts, ...creditCards].some((c) => c.id === current?.id)
  return (
    <NativeSelect
      id={id}
      value={value}
      aria-invalid={invalid}
      onChange={(e) => onChange(e.target.value)}
    >
      <option value="">{t.fields.choosePaidFrom}</option>
      {current && !listed && <option value={current.id}>{current.name}</option>}
      <optgroup label={t.fields.accounts}>
        {accounts.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </optgroup>
      {creditCards.length > 0 && (
        <optgroup label={t.fields.creditCards}>
          {creditCards.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </optgroup>
      )}
    </NativeSelect>
  )
}
