import { useState } from 'react'
import { Link, Navigate, useParams } from 'react-router'
import { toast } from 'sonner'
import {
  contributionRefOf,
  useChangeContribution,
  useChangeParticipant,
  useParticipants,
  useSharedCosts,
  type Contribution,
  type Participant,
  type ParticipantSharedCosts,
} from '@/api/sharedCosts'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { formatDate } from '@/components/cards/format'
import { messageOf } from '@/components/loans/messageOf'
import { sharedCostTexts } from '@/components/sharedCosts/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { currentMonth, formatYearMonth, isYearMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'
import { MonthNavigation } from '@/pages/MonthPage'
import { PageHeader } from '@/pages/PageHeader'

const t = sharedCostTexts.screen

/**
 * The Shared costs screen (shared bills FR-8): per participant, the recurring bills and loans they share,
 * their parts and contributions of the month, and what is outstanding, with Received check boxes; and
 * the participants themselves (FR-1).
 */
export function SharedCostsPage() {
  const { yearMonth } = useParams()
  if (yearMonth !== undefined && !isYearMonth(yearMonth))
    return <Navigate to="/shared-costs" replace />
  const month = yearMonth ?? currentMonth()

  return (
    <>
      <PageHeader title={t.title} description={t.description} />
      <MonthNavigation month={month} basePath="/shared-costs" />
      <div className="flex flex-col gap-6">
        <Overview month={month} />
        <ParticipantsCard />
      </div>
    </>
  )
}

function Overview({ month }: { month: string }) {
  const { data, isPending, isError } = useSharedCosts(month)
  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">Shared costs could not be loaded.</p>
  if (data.participants.length === 0)
    return <p className="text-sm text-muted-foreground">{t.noParticipants}</p>
  return (
    <>
      {data.participants.map((p) => (
        <ParticipantCard key={p.participant.id} month={month} data={p} />
      ))}
    </>
  )
}

function ParticipantCard({ month, data }: { month: string; data: ParticipantSharedCosts }) {
  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center gap-2">
          {data.participant.name}
          {data.participant.isArchived && <Badge variant="outline">{t.archived}</Badge>}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <section className="flex flex-col gap-1">
          <h3 className="text-sm font-medium">{t.costs}</h3>
          {data.costs.length === 0 && <p className="text-sm text-muted-foreground">{t.noCosts}</p>}
          <ul className="flex flex-col gap-1 text-sm">
            {data.costs.map((cost) => (
              <li key={cost.id} className="flex flex-wrap items-center gap-2">
                <Link
                  to={
                    cost.kind === 'Bill' ? `/recurring-bills/${cost.id}/edit` : `/loans/${cost.id}`
                  }
                  className="hover:underline"
                >
                  {cost.name}
                </Link>
                <Badge variant="secondary">{cost.kind === 'Bill' ? t.recurringBill : t.loan}</Badge>
                <span className="text-muted-foreground">
                  {sharedCostTexts.share.participantSharesOption(
                    cost.share.participantShares,
                    cost.share.shares,
                  )}{' '}
                  ({cost.share.percentage} %)
                </span>
              </li>
            ))}
          </ul>
        </section>
        <dl className="grid grid-cols-3 gap-2 border-t pt-3 text-sm">
          <Figure label={t.monthParts} cents={data.monthPartsCents} />
          <Figure label={t.monthReceived} cents={data.monthReceivedCents} />
          <Figure label={t.outstanding} cents={data.outstandingCents} />
        </dl>
        <ContributionList
          title={`${t.outstandingList} · ${formatYearMonth(month)}`}
          empty={t.nothingOutstanding}
          contributions={data.outstanding}
        />
        <ContributionList
          title={t.monthContributions}
          empty={t.noMonthContributions}
          contributions={data.monthContributions}
        />
      </CardContent>
    </Card>
  )
}

function Figure({ label, cents }: { label: string; cents: number }) {
  return (
    <div className="flex flex-col">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="font-medium tabular-nums">{formatCents(cents)}</dd>
    </div>
  )
}

function ContributionList({
  title,
  empty,
  contributions,
}: {
  title: string
  empty: string
  contributions: Contribution[]
}) {
  return (
    <section className="flex flex-col gap-1">
      <h3 className="text-sm font-medium">{title}</h3>
      {contributions.length === 0 && <p className="text-sm text-muted-foreground">{empty}</p>}
      <ul className="flex flex-col divide-y">
        {contributions.map((c) => (
          <ContributionLine
            key={`${c.billId ?? `${c.loanId}-${c.month}-${c.paymentKind}`}`}
            contribution={c}
          />
        ))}
      </ul>
    </section>
  )
}

function ContributionLine({ contribution }: { contribution: Contribution }) {
  const change = useChangeContribution()

  async function toggle(received: boolean) {
    try {
      await change.mutateAsync({
        action: received ? 'received' : 'expected',
        ref: contributionRefOf(contribution),
      })
    } catch (error) {
      toast.error(messageOf(error))
    }
  }

  return (
    <li
      className={cn(
        'flex items-center gap-3 py-2 text-sm',
        !contribution.isReceived && 'text-muted-foreground',
      )}
    >
      <input
        type="checkbox"
        className="size-4 accent-primary"
        checked={contribution.isReceived}
        disabled={change.isPending || contribution.needsAccount}
        onChange={(e) => toggle(e.target.checked)}
        aria-label={`${t.tickReceived}: ${contribution.costName}`}
      />
      <span className="flex min-w-0 flex-1 flex-wrap items-center gap-2">
        <span className={cn(contribution.isReceived && 'text-foreground')}>
          {contribution.costName}
        </span>
        <span className="text-xs">
          {contribution.receivedDate
            ? sharedCostTexts.contribution.receivedOn(formatDate(contribution.receivedDate))
            : sharedCostTexts.contribution.expectedOn(formatDate(contribution.expectedDate))}
        </span>
        {contribution.isOverdue && <Badge variant="destructive">{t.overdue}</Badge>}
        {contribution.needsAccount && (
          <Badge variant="destructive">{sharedCostTexts.contribution.needsAccount}</Badge>
        )}
      </span>
      <span className={cn('tabular-nums', contribution.isReceived && 'text-foreground')}>
        {formatCents(contribution.amountCents)}
      </span>
    </li>
  )
}

/** The participants (FR-1): add, edit, archive, restore and delete. */
function ParticipantsCard() {
  const [showArchived, setShowArchived] = useState(false)
  const { data = [], isPending } = useParticipants(showArchived)
  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle>{t.participants}</CardTitle>
        <CardAction>
          <Button asChild size="sm">
            <Link to="/shared-costs/participants/new">{t.addParticipant}</Link>
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <div className="flex items-center gap-3">
          <Switch id="show-archived" checked={showArchived} onCheckedChange={setShowArchived} />
          <Label htmlFor="show-archived">{t.showArchived}</Label>
        </div>
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {!isPending && data.length === 0 && (
          <p className="text-sm text-muted-foreground">{t.noParticipants}</p>
        )}
        <ul className="flex flex-col divide-y">
          {data.map((p) => (
            <ParticipantRow key={p.id} participant={p} />
          ))}
        </ul>
      </CardContent>
    </Card>
  )
}

function ParticipantRow({ participant }: { participant: Participant }) {
  const change = useChangeParticipant()
  const [deleting, setDeleting] = useState(false)

  async function run(action: 'archive' | 'restore') {
    try {
      await change.mutateAsync({ action, id: participant.id })
      toast.success(t.saved)
    } catch (error) {
      toast.error(messageOf(error))
    }
  }

  return (
    <li className="flex flex-wrap items-center gap-2 py-2">
      <Link
        to={`/shared-costs/participants/${participant.id}/edit`}
        className={cn(
          'font-medium hover:underline',
          participant.isArchived && 'text-muted-foreground',
        )}
      >
        {participant.name}
      </Link>
      {participant.isArchived && <Badge variant="outline">{t.archived}</Badge>}
      {participant.description && (
        <span className="text-sm text-muted-foreground">{participant.description}</span>
      )}
      <span className="ml-auto flex gap-1">
        <Button
          size="sm"
          variant="ghost"
          disabled={change.isPending}
          onClick={() => run(participant.isArchived ? 'restore' : 'archive')}
        >
          {participant.isArchived ? t.restore : t.archive}
        </Button>
        {participant.canBeDeleted && (
          <Button size="sm" variant="ghost" onClick={() => setDeleting(true)}>
            {t.delete}
          </Button>
        )}
      </span>
      <CardActionDialog
        open={deleting}
        onOpenChange={setDeleting}
        title={t.deleteConfirm(participant.name)}
        description={t.deleteDescription}
        confirmLabel={t.delete}
        destructive
        onConfirm={async () => {
          try {
            await change.mutateAsync({ action: 'delete', id: participant.id })
          } catch (e) {
            throw new Error(messageOf(e))
          }
          toast.success(t.saved)
        }}
      />
    </li>
  )
}
