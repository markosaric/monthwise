import { useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import type { InputEntry } from '@/api/cashFlow'
import { contributionCostPath, contributionRefOf, useChangeContribution } from '@/api/sharedCosts'
import { formatDate } from '@/components/cards/format'
import { payableAccounts } from '@/components/expenses/paidFrom'
import { messageOf } from '@/components/loans/messageOf'
import { PaymentDialog } from '@/components/loans/PaymentDialogs'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { centsToEuroInput, formatCents, parseEuroInput } from '@/lib/money'
import { todayIso } from '@/lib/dates'
import { cn } from '@/lib/utils'
import { sharedCostTexts } from './texts'

const t = sharedCostTexts.contribution

/**
 * A contribution in the Income part of the Month screen (shared bills FR-5 to FR-7): its participant,
 * the bill or loan payment it is for, its Received check box, and Edit and Reset to part. It cannot be
 * deleted: it goes away when the cost is no longer shared.
 */
export function ContributionRow({
  entry,
  nested = false,
}: {
  entry: InputEntry
  /** Shown inside its contributor's group: indented, without repeating the contributor. */
  nested?: boolean
}) {
  const contribution = entry.contribution!
  const change = useChangeContribution()
  const [editing, setEditing] = useState<'edit' | 'receive' | null>(null)
  const ref = contributionRefOf(contribution)
  const differs = entry.isReceived && entry.amountCents !== contribution.partCents

  async function run(action: 'received' | 'expected' | 'reset', message?: string) {
    try {
      await change.mutateAsync({ action, ref })
      if (message) toast.success(message)
    } catch (error) {
      toast.error(messageOf(error))
    }
  }

  return (
    <li
      className={cn(
        'flex items-start gap-3 py-3',
        nested && 'pl-7',
        !entry.isReceived && 'text-muted-foreground',
      )}
      data-expected={!entry.isReceived || undefined}
    >
      <input
        type="checkbox"
        className="mt-1 size-4 accent-primary"
        checked={entry.isReceived}
        disabled={change.isPending}
        onChange={(event) =>
          event.target.checked
            ? contribution.needsAccount
              ? setEditing('receive')
              : run('received')
            : run('expected')
        }
        aria-label={`${t.received}: ${entry.payer.name}, ${contribution.costName}`}
      />
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="flex flex-wrap items-center gap-2">
          {!nested && (
            <>
              <span className={cn('font-medium', entry.isReceived && 'text-foreground')}>
                {entry.payer.name}
              </span>
              <Badge variant="secondary">{t.title}</Badge>
            </>
          )}
          {contribution.isEdited && <Badge variant="outline">{t.edited}</Badge>}
          {contribution.needsAccount && <Badge variant="destructive">{t.needsAccount}</Badge>}
        </span>
        <span className="text-sm">
          <Link to={contributionCostPath(contribution)} className="hover:underline">
            {t.of(contribution.costName)}
          </Link>
          {!contribution.needsAccount && ` · ${entry.account.name}`}
        </span>
        <span className="text-xs">
          {entry.receivedDate
            ? t.receivedOn(formatDate(entry.receivedDate))
            : t.expectedOn(formatDate(entry.expectedDate))}
          {differs && ` · ${t.part(contribution.partCents)}`}
        </span>
        <span className="flex flex-wrap gap-1 pt-1">
          <Button size="xs" variant="ghost" onClick={() => setEditing('edit')}>
            {sharedCostTexts.screen.edit}
          </Button>
          {entry.id !== null && (contribution.isEdited || differs) && (
            <Button
              size="xs"
              variant="ghost"
              disabled={change.isPending}
              onClick={() => run('reset', t.reset)}
            >
              {t.resetToPart}
            </Button>
          )}
        </span>
      </div>
      <span className={cn('font-medium tabular-nums', entry.isReceived && 'text-foreground')}>
        {formatCents(entry.amountCents)}
      </span>
      {editing && (
        <ContributionDialog
          entry={entry}
          receive={editing === 'receive'}
          onOpenChange={(open) => !open && setEditing(null)}
        />
      )}
    </li>
  )
}

/** Edits a contribution: the account it is paid to, the amount and the dates (FR-6). */
function ContributionDialog({
  entry,
  receive,
  onOpenChange,
}: {
  entry: InputEntry
  /** Opened by ticking Received on a contribution without an account. */
  receive: boolean
  onOpenChange: (open: boolean) => void
}) {
  const contribution = entry.contribution!
  const { data: cards = [] } = useCards('Default')
  const change = useChangeContribution()
  const accounts = payableAccounts(cards)
  const [accountId, setAccountId] = useState(contribution.needsAccount ? '' : entry.account.id)
  const [amount, setAmount] = useState(centsToEuroInput(entry.amountCents))
  const [expectedDate, setExpectedDate] = useState(entry.expectedDate)
  const [receivedDate, setReceivedDate] = useState(
    entry.receivedDate ?? (receive ? todayIso() : ''),
  )
  const [error, setError] = useState<string | null>(null)

  async function save() {
    const amountCents = parseEuroInput(amount)
    if (accountId === '') {
      setError(t.accountRequired)
      return false
    }
    if (amountCents === null || amountCents <= 0) {
      setError(t.amountInvalid)
      return false
    }
    setError(null)
    try {
      const ref = contributionRefOf(contribution)
      if (receive && amountCents === entry.amountCents && expectedDate === entry.expectedDate) {
        await change.mutateAsync({ action: 'received', ref, receivedDate, accountId })
      } else {
        await change.mutateAsync({
          action: 'edit',
          ref,
          request: {
            accountId,
            amountCents,
            expectedDate,
            receivedDate: receivedDate === '' ? null : receivedDate,
            description: entry.description,
          },
        })
      }
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
      title={`${t.title}: ${entry.payer.name}`}
      description={t.of(contribution.costName)}
      onSave={save}
    >
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="contribution-account">{t.account}</Label>
        <NativeSelect
          id="contribution-account"
          value={accountId}
          onChange={(e) => setAccountId(e.target.value)}
        >
          <option value="" />
          {!contribution.needsAccount && !accounts.some((a) => a.id === entry.account.id) && (
            <option value={entry.account.id}>{entry.account.name}</option>
          )}
          {accounts.map((a) => (
            <option key={a.id} value={a.id}>
              {a.name}
            </option>
          ))}
        </NativeSelect>
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="contribution-amount">{t.amount}</Label>
          <Input
            id="contribution-amount"
            inputMode="decimal"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="contribution-expected">{t.expectedDate}</Label>
          <Input
            id="contribution-expected"
            type="date"
            value={expectedDate}
            onChange={(e) => setExpectedDate(e.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="contribution-received">{t.receivedDate}</Label>
          <Input
            id="contribution-received"
            type="date"
            value={receivedDate}
            onChange={(e) => setReceivedDate(e.target.value)}
          />
        </div>
      </div>
      <p className="text-xs text-muted-foreground">{t.part(contribution.partCents)}</p>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </PaymentDialog>
  )
}
