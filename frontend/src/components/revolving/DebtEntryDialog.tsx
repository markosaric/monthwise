import { useState } from 'react'
import { toast } from 'sonner'
import { useChangeDebtEntry, useRevolvingDebt, type DebtEntry } from '@/api/revolving'
import { PaymentDialog } from '@/components/loans/PaymentDialogs'
import { messageOf } from '@/components/loans/messageOf'
import { DateInput } from '@/components/DateInput'
import { Input } from '@/components/ui/input'
import { NativeSelect } from '@/components/ui/native-select'
import { dateEntryMessage, isInvalidDateEntry, todayIso } from '@/lib/dates'
import { centsToEuroInput, formatCents, parseEuroInput } from '@/lib/money'
import { FormField } from './ExtraPaymentForm'
import { revolvingTexts } from './texts'

const t = revolvingTexts.entry

/**
 * Records or edits a starting debt or a correction (revolving FR-5): the debt the bank shows on a
 * date. Shows the calculated debt on that date and the difference the entry makes.
 */
export function DebtEntryDialog({
  cardId,
  entry,
  onOpenChange,
}: {
  cardId: string
  /** The entry to edit; a new correction when not given. */
  entry?: DebtEntry
  onOpenChange: (open: boolean) => void
}) {
  const action = useChangeDebtEntry(cardId)
  const [kind, setKind] = useState<'Starting' | 'Correction'>(
    entry?.kind === 'Starting' ? 'Starting' : 'Correction',
  )
  const [date, setDate] = useState(entry?.date ?? todayIso())
  const [debt, setDebt] = useState(entry ? centsToEuroInput(entry.amountCents) : '')
  const [note, setNote] = useState(entry?.note ?? '')
  const [error, setError] = useState<string | null>(null)
  const { data: calculated } = useRevolvingDebt(entry ? undefined : cardId, date)
  const entered = parseEuroInput(debt)

  async function save() {
    if (entered === null) {
      setError(t.errors.debtInvalid)
      return false
    }
    if (isInvalidDateEntry(date)) {
      setError(dateEntryMessage)
      return false
    }
    if (date === '') {
      setError(t.errors.dateRequired)
      return false
    }
    if (note.trim().length > 200) {
      setError(t.errors.noteTooLong)
      return false
    }
    setError(null)
    const request = {
      kind,
      date,
      debtCents: entered,
      note: note.trim() === '' ? null : note.trim(),
    }
    try {
      await action.mutateAsync(
        entry ? { action: 'update', entryId: entry.id, request } : { action: 'create', request },
      )
      toast.success(t.saved)
      return true
    } catch (e) {
      setError(messageOf(e))
      return false
    }
  }

  return (
    <PaymentDialog
      open
      onOpenChange={onOpenChange}
      title={entry ? t.editTitle : t.title}
      description={t.description}
      onSave={save}
    >
      {entry && (
        <FormField id="entry-kind" label={t.kind}>
          <NativeSelect
            id="entry-kind"
            value={kind}
            onChange={(e) => setKind(e.target.value as 'Starting' | 'Correction')}
          >
            <option value="Starting">{revolvingTexts.kinds.Starting}</option>
            <option value="Correction">{revolvingTexts.kinds.Correction}</option>
          </NativeSelect>
        </FormField>
      )}
      <FormField id="entry-date" label={t.date}>
        <DateInput id="entry-date" value={date} onChange={setDate} />
      </FormField>
      <FormField
        id="entry-debt"
        label={t.debt}
        hint={calculated ? t.calculated(formatCents(calculated.debtCents)) : undefined}
      >
        <Input
          id="entry-debt"
          inputMode="decimal"
          value={debt}
          onChange={(e) => setDebt(e.target.value)}
        />
      </FormField>
      {calculated && entered !== null && (
        <p className="text-sm text-muted-foreground">
          {t.difference(formatCents(entered - calculated.debtCents))}
        </p>
      )}
      <FormField id="entry-note" label={t.note}>
        <Input
          id="entry-note"
          maxLength={200}
          value={note}
          onChange={(e) => setNote(e.target.value)}
        />
      </FormField>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </PaymentDialog>
  )
}
