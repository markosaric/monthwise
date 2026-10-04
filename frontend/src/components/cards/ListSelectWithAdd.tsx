import { useState } from 'react'
import { ApiError, useAddCardListEntry, useCardList, type ListKind } from '@/api/cards'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect } from '@/components/ui/native-select'
import { cardTexts } from './texts'

const addNewValue = '__add__'

/** Issuer or bank picker with an "Add new…" option that saves the new entry to the list (A2, A3). */
export function ListSelectWithAdd({
  id,
  kind,
  value,
  onChange,
  invalid,
}: {
  id: string
  kind: ListKind
  value: string
  onChange: (value: string) => void
  invalid?: boolean
}) {
  const { data: entries = [] } = useCardList(kind)
  const addEntry = useAddCardListEntry(kind)
  const [adding, setAdding] = useState(false)
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function add() {
    setError(null)
    try {
      const entry = await addEntry.mutateAsync(name)
      onChange(entry.id)
      setAdding(false)
      setName('')
    } catch (e) {
      setError(
        e instanceof ApiError ? (e.fieldErrors.name?.[0] ?? e.message) : 'Could not add the entry.',
      )
    }
  }

  if (adding) {
    return (
      <div className="flex flex-col gap-1">
        <div className="flex gap-2">
          <Input
            id={id}
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder={kind === 'banks' ? 'New bank' : 'New issuer'}
            maxLength={50}
            aria-invalid={error !== null}
            autoFocus
          />
          <Button type="button" onClick={add} disabled={name.trim() === '' || addEntry.isPending}>
            Add
          </Button>
          <Button type="button" variant="ghost" onClick={() => setAdding(false)}>
            {cardTexts.actions.cancel}
          </Button>
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
      </div>
    )
  }

  return (
    <NativeSelect
      id={id}
      value={value}
      aria-invalid={invalid}
      onChange={(e) => {
        if (e.target.value === addNewValue) {
          setAdding(true)
        } else {
          onChange(e.target.value)
        }
      }}
    >
      <option value="">Choose…</option>
      {entries.map((entry) => (
        <option key={entry.id} value={entry.id}>
          {entry.name}
        </option>
      ))}
      <option value={addNewValue}>{cardTexts.actions.addNew}</option>
    </NativeSelect>
  )
}
