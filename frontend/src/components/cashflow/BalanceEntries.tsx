import { useState } from 'react'
import { toast } from 'sonner'
import {
  useAccountBalance,
  useChangeBalanceEntry,
  type BalanceEntry,
  type BalanceEntryKind,
} from '@/api/cashFlow'
import { ApiError } from '@/api/cards'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { formatDate } from '@/components/cards/format'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { todayIso } from '@/lib/dates'
import { centsToEuroInput, formatCents, parseSignedEuroInput } from '@/lib/money'
import { cn } from '@/lib/utils'
import { Field } from './InputForm'
import { cashFlowTexts } from './texts'

const t = cashFlowTexts.balance

/** Balance of one account with its starting balance and corrections (FR-9, FR-10). */
export function BalanceEntries({ accountId, canAdd }: { accountId: string; canAdd: boolean }) {
  const { data, isPending, isError } = useAccountBalance(accountId)
  const [editor, setEditor] = useState<{ kind: BalanceEntryKind; entry?: BalanceEntry } | null>(
    null,
  )
  const [deleting, setDeleting] = useState<BalanceEntry | null>(null)
  const change = useChangeBalanceEntry(accountId)

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">The balance could not be loaded.</p>

  const { balance, entries } = data
  return (
    <section aria-labelledby="balance-title" className="flex flex-col gap-3">
      <h2 id="balance-title" className="text-lg font-semibold">
        {t.title}
      </h2>
      <dl className="grid grid-cols-2 gap-2 text-sm">
        <div>
          <dt className="text-muted-foreground">{t.current}</dt>
          <dd
            className={cn(
              'font-medium tabular-nums',
              balance.currentBalanceCents < 0 && 'text-destructive',
            )}
          >
            {formatCents(balance.currentBalanceCents)}
          </dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t.expected}</dt>
          <dd className="font-medium text-muted-foreground tabular-nums">
            {formatCents(balance.expectedBalanceCents)}
          </dd>
        </div>
      </dl>
      {!balance.hasStartingBalance && (
        <p className="text-sm text-muted-foreground">{t.noStarting}</p>
      )}
      {canAdd && editor === null && (
        <div className="flex flex-wrap gap-2">
          {!entries.some((e) => e.kind === 'Starting') && (
            <Button size="sm" onClick={() => setEditor({ kind: 'Starting' })}>
              {t.addStarting}
            </Button>
          )}
          <Button size="sm" variant="outline" onClick={() => setEditor({ kind: 'Correction' })}>
            {t.addCorrection}
          </Button>
        </div>
      )}
      {editor && (
        <BalanceEntryForm
          kind={editor.kind}
          entry={editor.entry}
          onCancel={() => setEditor(null)}
          onSave={async (request) => {
            await change.mutateAsync(
              editor.entry
                ? { action: 'update', id: editor.entry.id, request }
                : { action: 'create', request },
            )
            toast.success(t.saved)
            setEditor(null)
          }}
        />
      )}
      {entries.length > 0 && (
        <div className="flex flex-col gap-2">
          <h3 className="text-sm font-medium">{t.entries}</h3>
          <ul className="flex flex-col divide-y rounded-lg border">
            {entries.map((entry) => (
              <li
                key={entry.id}
                className="flex flex-wrap items-center gap-x-3 gap-y-1 p-3 text-sm"
              >
                <span className="font-medium">{t.kinds[entry.kind ?? 'Correction']}</span>
                <span className="text-muted-foreground">{formatDate(entry.date)}</span>
                <span className="tabular-nums">{formatCents(entry.balanceCents)}</span>
                {entry.kind === 'Correction' && (
                  <span className="text-muted-foreground tabular-nums">
                    {t.difference} {entry.differenceCents > 0 ? '+' : ''}
                    {formatCents(entry.differenceCents)}
                  </span>
                )}
                {entry.note && <span className="text-muted-foreground">· {entry.note}</span>}
                <span className="ml-auto flex gap-1">
                  <Button
                    size="xs"
                    variant="ghost"
                    onClick={() => setEditor({ kind: entry.kind ?? 'Correction', entry })}
                  >
                    {cashFlowTexts.actions.edit}
                  </Button>
                  <Button size="xs" variant="ghost" onClick={() => setDeleting(entry)}>
                    {cashFlowTexts.actions.delete}
                  </Button>
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
      <CardActionDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={t.deleteTitle}
        description={
          deleting
            ? `${t.kinds[deleting.kind ?? 'Correction']}, ${formatDate(deleting.date)}, ${formatCents(deleting.balanceCents)}.`
            : ''
        }
        confirmLabel={cashFlowTexts.actions.delete}
        destructive
        onConfirm={async () => {
          await change.mutateAsync({ action: 'delete', id: deleting!.id })
          toast.success(t.deleted)
        }}
      />
    </section>
  )
}

function BalanceEntryForm({
  kind,
  entry,
  onSave,
  onCancel,
}: {
  kind: BalanceEntryKind
  entry?: BalanceEntry
  onSave: (request: {
    kind: BalanceEntryKind
    date: string
    balanceCents: number
    note: string | null
  }) => Promise<void>
  onCancel: () => void
}) {
  const [date, setDate] = useState(entry?.date ?? todayIso())
  const [amount, setAmount] = useState(entry ? centsToEuroInput(entry.balanceCents) : '')
  const [note, setNote] = useState(entry?.note ?? '')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    const cents = parseSignedEuroInput(amount)
    if (cents === null || date === '') {
      setError(t.amountInvalid)
      return
    }
    setBusy(true)
    setError(null)
    try {
      await onSave({
        kind,
        date,
        balanceCents: cents,
        note: note.trim() === '' ? null : note.trim(),
      })
    } catch (e) {
      setError(
        e instanceof ApiError
          ? (Object.values(e.fieldErrors)[0]?.[0] ?? e.message)
          : 'Saving failed.',
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3 rounded-lg border p-3" noValidate>
      <p className="text-sm font-medium">{t.kinds[kind]}</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field id="balance-date" label={t.date}>
          <Input
            id="balance-date"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
          />
        </Field>
        <Field id="balance-amount" label={t.amount} hint={t.amountHint}>
          <Input
            id="balance-amount"
            inputMode="decimal"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
          />
        </Field>
      </div>
      <Field id="balance-note" label={t.note}>
        <Input
          id="balance-note"
          maxLength={200}
          value={note}
          onChange={(e) => setNote(e.target.value)}
        />
      </Field>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={busy}>
          {cashFlowTexts.actions.save}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          {cashFlowTexts.actions.cancel}
        </Button>
      </div>
    </form>
  )
}
