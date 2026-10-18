import { CircleAlert, CircleCheck, HandCoins, Users } from 'lucide-react'
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
import { meaningColors, sectionColors } from '@/components/design/colors'
import { CheckButton } from '@/components/design/ListRow'
import { MonthHeader } from '@/components/design/MonthHeader'
import { ProgressBar } from '@/components/design/ProgressBar'
import { StatCard, StatGrid } from '@/components/design/StatCard'
import { StatusChip } from '@/components/design/StatusChip'
import { sharedCostTexts } from '@/components/sharedCosts/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { currentMonth, formatYearMonth, isYearMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'

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
    <div className="flex max-w-6xl flex-col gap-4">
      <MonthHeader
        month={month}
        basePath="/shared-costs"
        title={t.title}
        aside={formatYearMonth(month)}
      />
      <Overview month={month} />
      <ParticipantsCard />
    </div>
  )
}

/**
 * Key figures, a card per participant, the outstanding contributions with one-tap Received, and
 * received versus outstanding per participant (design refresh FR-19).
 */
function Overview({ month }: { month: string }) {
  const { data, isPending, isError } = useSharedCosts(month)
  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">Shared costs could not be loaded.</p>
  if (data.participants.length === 0)
    return <p className="text-sm text-muted-foreground">{t.noParticipants}</p>

  const participants = data.participants
  const outstanding = participants.flatMap((p) => p.outstanding)
  const sum = (values: number[]) => values.reduce((total, v) => total + v, 0)
  const overdue = sum(outstanding.filter((c) => c.isOverdue).map((c) => c.amountCents))

  return (
    <>
      <StatGrid label="Key figures">
        <StatCard
          icon={HandCoins}
          color={sectionColors.shared}
          label={t.outstanding}
          value={formatCents(sum(participants.map((p) => p.outstandingCents)))}
        />
        <StatCard
          icon={CircleAlert}
          color={overdue > 0 ? meaningColors.problem : sectionColors.shared}
          label={t.overdue}
          value={formatCents(overdue)}
          problem={overdue > 0}
        />
        <StatCard
          icon={CircleCheck}
          color={meaningColors.moneyIn}
          label={t.monthReceived}
          value={formatCents(sum(participants.map((p) => p.monthReceivedCents)))}
        />
        <StatCard
          icon={Users}
          color={sectionColors.shared}
          label={t.participants}
          value={participants.length}
        />
      </StatGrid>
      <div className="grid items-start gap-4 lg:grid-cols-2">
        <div className="flex flex-col gap-4">
          {participants.map((p) => (
            <ParticipantCard key={p.participant.id} data={p} />
          ))}
        </div>
        <Card>
          <CardContent className="flex flex-col gap-5">
            <ContributionList
              title={`${t.outstandingList} · ${formatYearMonth(month)}`}
              empty={t.nothingOutstanding}
              contributions={outstanding}
              showParticipant={participants.length > 1}
            />
            <ReceivedAndOutstanding participants={participants} />
          </CardContent>
        </Card>
      </div>
    </>
  )
}

/** One participant: outstanding amount, this month's progress and what they share (FR-19). */
function ParticipantCard({ data }: { data: ParticipantSharedCosts }) {
  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <div className="flex items-center gap-3">
          <span
            aria-hidden
            className="grid size-10 shrink-0 place-items-center rounded-full font-heading text-lg font-bold text-white"
            style={{ background: sectionColors.shared }}
          >
            {data.participant.name.slice(0, 1).toUpperCase()}
          </span>
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="flex flex-wrap items-center gap-2">
              <h2 className="text-base font-semibold">{data.participant.name}</h2>
              {data.participant.isArchived && <Badge variant="outline">{t.archived}</Badge>}
            </span>
            <span className="text-sm text-muted-foreground">
              {t.sharedCount(data.costs.length)}
            </span>
          </span>
          <span className="text-right">
            <span
              className={cn(
                'block font-heading text-xl font-bold tabular-nums',
                data.outstandingCents > 0 && 'text-problem',
              )}
            >
              {formatCents(data.outstandingCents)}
            </span>
            <span className="text-xs text-muted-foreground">{t.outstanding.toLowerCase()}</span>
          </span>
        </div>
        <ProgressBar
          label={`${data.participant.name}: ${t.monthReceived}`}
          value={data.monthReceivedCents}
          max={data.monthPartsCents}
          color={sectionColors.shared}
        />
        <span className="text-xs text-muted-foreground">
          {t.receivedOf(formatCents(data.monthReceivedCents), formatCents(data.monthPartsCents))}
        </span>
        <section className="flex flex-col gap-1">
          <h3 className="sr-only">{t.costs}</h3>
          {data.costs.length === 0 && <p className="text-sm text-muted-foreground">{t.noCosts}</p>}
          <ul className="flex flex-col divide-y text-sm">
            {data.costs.map((cost) => (
              <li key={cost.id} className="flex flex-wrap items-center gap-2 py-1.5">
                <Link
                  to={
                    cost.kind === 'Bill' ? `/recurring-bills/${cost.id}/edit` : `/loans/${cost.id}`
                  }
                  className="hover:underline"
                >
                  {cost.name}
                </Link>
                <Badge variant="secondary">{cost.kind === 'Bill' ? t.recurringBill : t.loan}</Badge>
                <span className="ml-auto text-xs text-muted-foreground">
                  {sharedCostTexts.share.participantSharesOption(
                    cost.share.participantShares,
                    cost.share.shares,
                  )}{' '}
                  · {cost.share.percentage} %
                </span>
              </li>
            ))}
          </ul>
        </section>
        <details className="text-sm">
          <summary className="cursor-pointer text-muted-foreground">
            {t.monthContributions} ({data.monthContributions.length})
          </summary>
          <div className="pt-2">
            <ContributionList
              title={t.monthContributions}
              empty={t.noMonthContributions}
              contributions={data.monthContributions}
              hideTitle
            />
          </div>
        </details>
      </CardContent>
    </Card>
  )
}

/** Received and outstanding per participant as bars, with the exact amounts (FR-3). */
function ReceivedAndOutstanding({ participants }: { participants: ParticipantSharedCosts[] }) {
  return (
    <section className="flex flex-col gap-2">
      <h3 className="text-sm font-medium">{t.receivedAndOutstanding}</h3>
      <ul className="flex flex-col gap-2">
        {participants.map((p) => {
          const total = p.monthReceivedCents + p.outstandingCents
          const received = total === 0 ? 0 : (p.monthReceivedCents / total) * 100
          return (
            <li
              key={p.participant.id}
              className="grid items-center gap-x-3 gap-y-1 text-sm sm:grid-cols-[6rem_1fr_auto]"
            >
              <span className="truncate">{p.participant.name}</span>
              <span className="flex h-3 overflow-hidden rounded-md bg-muted" aria-hidden>
                <span style={{ width: `${received}%`, background: meaningColors.moneyIn }} />
                {total > 0 && (
                  <span className="flex-1" style={{ background: sectionColors.shared }} />
                )}
              </span>
              <span className="text-xs whitespace-nowrap text-muted-foreground tabular-nums">
                {formatCents(p.monthReceivedCents)} · {formatCents(p.outstandingCents)}
              </span>
            </li>
          )
        })}
      </ul>
      <ul className="flex gap-4 text-xs text-muted-foreground" aria-label="Legend">
        <li className="flex items-center gap-2">
          <span
            className="size-3 rounded-sm"
            style={{ background: meaningColors.moneyIn }}
            aria-hidden
          />
          {t.monthReceived}
        </li>
        <li className="flex items-center gap-2">
          <span
            className="size-3 rounded-sm"
            style={{ background: sectionColors.shared }}
            aria-hidden
          />
          {t.outstanding}
        </li>
      </ul>
    </section>
  )
}

function ContributionList({
  title,
  empty,
  contributions,
  showParticipant = false,
  hideTitle = false,
}: {
  title: string
  empty: string
  contributions: Contribution[]
  showParticipant?: boolean
  hideTitle?: boolean
}) {
  return (
    <section className="flex flex-col gap-1">
      <h3 className={cn('text-sm font-medium', hideTitle && 'sr-only')}>{title}</h3>
      {contributions.length === 0 && <p className="text-sm text-muted-foreground">{empty}</p>}
      <ul className="flex flex-col divide-y">
        {contributions.map((c) => (
          <ContributionLine
            key={`${c.billId ?? `${c.loanId}-${c.month}-${c.paymentKind}`}`}
            contribution={c}
            showParticipant={showParticipant}
          />
        ))}
      </ul>
    </section>
  )
}

function ContributionLine({
  contribution,
  showParticipant,
}: {
  contribution: Contribution
  showParticipant: boolean
}) {
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
    <li className="flex items-center gap-3 py-2 text-sm">
      <CheckButton
        checked={contribution.isReceived}
        disabled={change.isPending || contribution.needsAccount}
        onChange={(e) => toggle(e.target.checked)}
        label={`${t.tickReceived}: ${contribution.costName}`}
      />
      <span className="flex min-w-0 flex-1 flex-wrap items-center gap-x-2 gap-y-1">
        {showParticipant && (
          <span className="text-muted-foreground">{contribution.participant.name} ·</span>
        )}
        <span className={cn('font-medium', !contribution.isReceived && 'text-muted-foreground')}>
          {contribution.costName}
        </span>
        <span className="text-xs text-muted-foreground">
          {contribution.receivedDate
            ? sharedCostTexts.contribution.receivedOn(formatDate(contribution.receivedDate))
            : sharedCostTexts.contribution.expectedOn(formatDate(contribution.expectedDate))}
        </span>
        {contribution.isOverdue ? (
          <StatusChip status="overdue" label={t.overdue} />
        ) : (
          <StatusChip
            status={contribution.isReceived ? 'done' : 'expected'}
            label={contribution.isReceived ? t.tickReceived : t.expected}
          />
        )}
        {contribution.needsAccount && (
          <Badge variant="destructive">{sharedCostTexts.contribution.needsAccount}</Badge>
        )}
      </span>
      <span className="font-semibold tabular-nums">{formatCents(contribution.amountCents)}</span>
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
