import {
  ArrowDownLeft,
  ArrowUpRight,
  CircleAlert,
  CircleCheck,
  Clock,
  CreditCard,
  Equal,
  Landmark,
  Repeat,
  Users,
  type LucideIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { useInstallmentCards } from '@/api/cardPurchases'
import { useDashboard, type Dashboard, type DashboardItem } from '@/api/dashboard'
import { formatDate } from '@/components/cards/format'
import { ExportReminder } from '@/components/dataTransfer/ExportReminder'
import { dashboardTexts } from '@/components/dashboard/texts'
import { addActions } from '@/components/design/actions'
import { ChangePill } from '@/components/design/ChangePill'
import { meaningColors, sectionColors } from '@/components/design/colors'
import { IconTile } from '@/components/design/IconTile'
import { PairedBarChart } from '@/components/design/PairedBarChart'
import { percentChange } from '@/components/design/percent'
import { QuickActionButton, QuickActionRow } from '@/components/design/QuickActionButton'
import { RingChart } from '@/components/design/RingChart'
import { StatCard, StatGrid } from '@/components/design/StatCard'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatYearMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { cn } from '@/lib/utils'

const t = dashboardTexts

/**
 * The Dashboard: how the current month is going (design refresh FR-9 to FR-11, FR-14). It shows no
 * account balances; those are on the Cards screen.
 */
export function DashboardPage() {
  const { data, isPending, isError } = useDashboard()

  return (
    <div className="flex max-w-6xl flex-col gap-4">
      <header className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-2xl font-bold tracking-tight">
          {data ? formatYearMonth(data.month) : 'Dashboard'}
        </h1>
        <span className="text-sm text-muted-foreground">{t.subtitle}</span>
      </header>
      <QuickActions month={data?.month} />
      <ExportReminder />
      {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
      {isError && <p className="text-sm text-destructive">{t.loadError}</p>}
      {data && <DashboardContent data={data} />}
    </div>
  )
}

/** Icon-only add buttons with a delayed label (FR-14). */
function QuickActions({ month }: { month?: string }) {
  const { data: cards } = useInstallmentCards()
  const canAddPurchase = cards?.some((c) => c.canBeUsed && !c.needsBillingDetails) ?? false
  const m = month ?? ''
  const back = 'returnTo=/'
  const actions: { key: keyof typeof addActions; label: string; to: string }[] = [
    { key: 'income', label: t.quickActions.addIncome, to: `/month/${m}/inputs/new?${back}` },
    { key: 'bill', label: t.quickActions.addBill, to: `/month/${m}/bills/new?${back}` },
    { key: 'expense', label: t.quickActions.addExpense, to: `/month/${m}/expenses/new?${back}` },
    {
      key: 'cash',
      label: t.quickActions.cashWithdrawal,
      to: `/month/${m}/expenses/new?kind=cash&${back}`,
    },
    ...(canAddPurchase
      ? [
          {
            key: 'purchase' as const,
            label: t.quickActions.addPurchase,
            to: `/cards/purchases/new?${back}`,
          },
        ]
      : []),
  ]
  if (!month) return null
  return (
    <QuickActionRow>
      {actions.map((a) => (
        <QuickActionButton
          key={a.key}
          to={a.to}
          label={a.label}
          icon={addActions[a.key].icon}
          color={addActions[a.key].color}
        />
      ))}
    </QuickActionRow>
  )
}

function DashboardContent({ data }: { data: Dashboard }) {
  const { inputs, outputs, stillToPay } = data
  return (
    <>
      <StatGrid label={t.keyFigures}>
        <StatCard
          icon={ArrowDownLeft}
          color={meaningColors.moneyIn}
          label={t.inputs}
          detail={t.received(formatCents(inputs.doneCents))}
          value={formatCents(inputs.totalCents)}
          pill={
            <ChangePill
              current={inputs.totalCents}
              previous={inputs.previousMonthTotalCents}
              higherIsBetter
            />
          }
          to="/month"
        />
        <StatCard
          icon={ArrowUpRight}
          color={meaningColors.moneyOut}
          label={t.outputs}
          detail={t.paid(formatCents(outputs.doneCents))}
          value={formatCents(outputs.totalCents)}
          pill={
            <ChangePill
              current={outputs.totalCents}
              previous={outputs.previousMonthTotalCents}
              higherIsBetter={false}
            />
          }
          to="/month"
        />
        <StatCard
          icon={Equal}
          color={meaningColors.highlight}
          label={t.monthResult}
          value={formatCents(data.monthResultCents)}
          problem={data.monthResultCents < 0}
          pill={
            <ChangePill
              current={data.monthResultCents}
              previous={data.previousMonthResultCents}
              higherIsBetter
            />
          }
          to={`/cash-flow/${data.month}`}
        />
        <StatCard
          icon={stillToPay.overdueCount > 0 ? CircleAlert : Clock}
          color={stillToPay.overdueCount > 0 ? meaningColors.problem : meaningColors.expense}
          label={t.stillToPay}
          detail={
            stillToPay.overdueCount > 0
              ? t.overdueItems(stillToPay.overdueCount)
              : t.items(stillToPay.count)
          }
          value={formatCents(stillToPay.totalCents)}
          problem={stillToPay.overdueCount > 0}
          to="/month"
        />
      </StatGrid>

      <div className="grid gap-4 lg:grid-cols-2">
        <HighlightCard data={data} />
        <ComingUp items={data.comingUp} />
      </div>

      <Overview data={data} />

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base font-semibold">{t.outputsByKind.title}</CardTitle>
          </CardHeader>
          <CardContent>
            <RingChart
              label={t.outputsByKind.title}
              slices={[
                {
                  label: t.outputsByKind.bills,
                  cents: data.outputsByKind.billsCents,
                  color: meaningColors.moneyOut,
                },
                {
                  label: t.outputsByKind.expenses,
                  cents: data.outputsByKind.expensesCents,
                  color: meaningColors.expense,
                },
                {
                  label: t.outputsByKind.loanPayments,
                  cents: data.outputsByKind.loanPaymentsCents,
                  color: sectionColors.loans,
                },
                {
                  label: t.outputsByKind.cardPayments,
                  cents: data.outputsByKind.cardPaymentsCents,
                  color: sectionColors.cards,
                },
              ]}
            />
          </CardContent>
        </Card>
        <SmallCards data={data} />
      </div>
    </>
  )
}

/** One short message about the month: overdue, else the next due payment, else all paid (FR-10). */
function HighlightCard({ data }: { data: Dashboard }) {
  const { stillToPay, nextDue } = data
  const empty = data.inputs.totalCents === 0 && data.outputs.totalCents === 0
  let tone: 'highlight' | 'problem' = 'highlight'
  let icon: LucideIcon = CircleCheck
  let title: string
  let hint: string
  if (empty) {
    title = t.highlight.empty
    hint = t.highlight.emptyHint
  } else if (stillToPay.overdueCount > 0) {
    tone = 'problem'
    icon = CircleAlert
    title = t.highlight.overdue(stillToPay.overdueCount, formatCents(stillToPay.overdueCents))
    hint = t.highlight.overdueHint
  } else if (nextDue) {
    icon = Clock
    title = t.highlight.next(
      nextDue.name,
      formatCents(nextDue.amountCents),
      formatDate(nextDue.date),
    )
    hint = t.highlight.more(Math.max(0, stillToPay.count - 1))
  } else {
    title = t.highlight.allPaid(formatYearMonth(data.month, 'long').split(' ')[0]!)
    hint = t.highlight.allPaidHint
  }
  const Icon = icon
  return (
    <Link
      to="/month"
      role="status"
      className={cn(
        'flex items-center gap-4 rounded-xl p-5 text-white shadow-[0_2px_8px_rgb(20_24_40/0.06)] focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
        tone === 'problem' ? 'bg-problem' : 'bg-highlight',
      )}
    >
      <span className="grid size-12 shrink-0 place-items-center rounded-xl bg-white/20" aria-hidden>
        <Icon className="size-6" />
      </span>
      <span className="flex flex-col gap-1">
        <span className="font-heading text-xl leading-snug font-bold">{title}</span>
        <span className="text-sm text-white/90">{hint}</span>
      </span>
    </Link>
  )
}

const kindColors: Record<DashboardItem['kind'], string> = {
  Bill: meaningColors.moneyOut,
  LoanPayment: sectionColors.loans,
  CardPayment: sectionColors.cards,
  Input: meaningColors.moneyIn,
}

function ComingUp({ items }: { items: DashboardItem[] }) {
  return (
    <Card>
      <CardHeader className="flex items-center justify-between">
        <CardTitle className="text-base font-semibold">{t.comingUp}</CardTitle>
        <Link
          to="/month"
          className="text-sm text-muted-foreground underline-offset-4 hover:underline"
        >
          {t.openMonth}
        </Link>
      </CardHeader>
      <CardContent>
        {items.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t.nothingComingUp}</p>
        ) : (
          <ul className="flex flex-col divide-y">
            {items.map((item) => {
              const input = item.kind === 'Input'
              return (
                <li
                  key={`${item.kind}-${item.name}-${item.date}`}
                  className="flex items-center gap-3 py-2 text-sm"
                >
                  <span
                    className="size-2 shrink-0 rounded-full"
                    style={{ background: kindColors[item.kind] }}
                    aria-hidden
                  />
                  <span className="min-w-0 truncate">{item.name}</span>
                  <span className="text-xs text-muted-foreground">{formatDate(item.date)}</span>
                  <span
                    className="ml-auto font-semibold tabular-nums"
                    style={{
                      color: input ? meaningColors.moneyInText : meaningColors.moneyOutText,
                    }}
                  >
                    {input ? '+' : '−'}
                    {formatCents(item.amountCents)}
                  </span>
                </li>
              )
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

/** The month result, the comparison with last month and the 6-month chart (FR-10). */
function Overview({ data }: { data: Dashboard }) {
  const inputsChange = percentChange(data.inputs.totalCents, data.inputs.previousMonthTotalCents)
  const outputsChange = percentChange(data.outputs.totalCents, data.outputs.previousMonthTotalCents)
  return (
    <Card>
      <CardContent className="grid items-end gap-6 md:grid-cols-[minmax(12rem,1fr)_2fr]">
        <div>
          <h2 className="text-base font-semibold">{t.overview.title}</h2>
          <p
            className={cn(
              'mt-1 font-heading text-4xl font-bold tabular-nums md:text-5xl',
              data.monthResultCents < 0 && 'text-problem',
            )}
          >
            {formatCents(data.monthResultCents)}
          </p>
          <p className="text-sm text-muted-foreground">{t.overview.caption}</p>
          <ul className="mt-5 flex flex-col gap-2 text-sm">
            {inputsChange !== null && (
              <Comparison good={inputsChange >= 0}>
                {inputsChange >= 0
                  ? t.overview.moreInputs(inputsChange)
                  : t.overview.lessInputs(-inputsChange)}
              </Comparison>
            )}
            {outputsChange !== null && (
              <Comparison good={outputsChange <= 0}>
                {outputsChange >= 0
                  ? t.overview.moreOutputs(outputsChange)
                  : t.overview.lessOutputs(-outputsChange)}
              </Comparison>
            )}
          </ul>
          <Link
            to="/cash-flow"
            className="mt-4 inline-block text-sm text-muted-foreground underline-offset-4 hover:underline"
          >
            Cash flow
          </Link>
        </div>
        <PairedBarChart
          label={t.overview.chart}
          rows={data.lastSixMonths.map((m) => ({
            label: formatYearMonth(m.month, 'short').split(' ')[0]!,
            inputs: m.inputsCents,
            outputs: m.outputsCents,
          }))}
          series={[
            { key: 'inputs', label: t.inputs, color: meaningColors.moneyIn },
            { key: 'outputs', label: t.outputs, color: meaningColors.moneyOut },
          ]}
        />
      </CardContent>
    </Card>
  )
}

function Comparison({ good, children }: { good: boolean; children: ReactNode }) {
  return (
    <li className="flex items-center gap-2">
      <span
        aria-hidden
        className={cn(
          'grid size-6 place-items-center rounded-md text-xs',
          good ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700',
        )}
      >
        {good ? '▲' : '▼'}
      </span>
      <span>
        <b className={cn('font-semibold', good ? 'text-green-700' : 'text-red-700')}>{children}</b>
        {t.overview.thanLastMonth}
      </span>
    </li>
  )
}

/** Small cards for Loans, Card payments, Shared costs and Recurring bills; only those with data. */
function SmallCards({ data }: { data: Dashboard }) {
  const cards: {
    key: string
    icon: LucideIcon
    color: string
    title: string
    detail: string
    to: string
    problem?: boolean
  }[] = []
  if (data.loans) {
    cards.push({
      key: 'loans',
      icon: Landmark,
      color: data.loans.hasOverdue ? meaningColors.problem : sectionColors.loans,
      title: t.small.loans(formatCents(data.loans.monthlyPaymentCents)),
      detail: t.small.loansDetail(formatCents(data.loans.remainingCents)),
      to: '/loans',
      problem: data.loans.hasOverdue,
    })
  }
  if (data.cardPayments) {
    cards.push({
      key: 'cards',
      icon: CreditCard,
      color: sectionColors.cards,
      title: formatCents(data.cardPayments.monthTotalCents),
      detail: `${t.small.cardPayments} · ${t.small.cardsWithInstallments(data.cardPayments.cardsWithInstallments)}`,
      to: '/cards',
    })
  }
  if (data.sharedCosts) {
    cards.push({
      key: 'shared',
      icon: Users,
      color: data.sharedCosts.hasOverdue ? meaningColors.problem : sectionColors.shared,
      title: formatCents(data.sharedCosts.outstandingCents),
      detail: `${t.small.sharedCosts} · ${t.small.owing(data.sharedCosts.participantsOwing)}`,
      to: '/shared-costs',
      problem: data.sharedCosts.hasOverdue,
    })
  }
  if (data.recurringBills) {
    cards.push({
      key: 'recurring',
      icon: Repeat,
      color: sectionColors.month,
      title: t.small.recurring(data.recurringBills.activeCount),
      detail: t.small.notIssued(data.recurringBills.notIssuedThisMonth),
      to: '/recurring-bills',
    })
  }
  if (cards.length === 0) return null
  return (
    <ul className="grid content-start gap-3 sm:grid-cols-2">
      {cards.map((c) => (
        <li key={c.key}>
          <Link
            to={c.to}
            className={cn(
              'flex h-full items-center gap-3 rounded-xl bg-card p-4 shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8 hover:ring-foreground/20 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
              c.problem && 'ring-red-200',
            )}
          >
            <IconTile icon={c.icon} color={c.color} solid />
            <span className="flex min-w-0 flex-col">
              <span className={cn('font-semibold tabular-nums', c.problem && 'text-problem')}>
                {c.title}
              </span>
              <span className="text-sm text-muted-foreground">{c.detail}</span>
            </span>
          </Link>
        </li>
      ))}
    </ul>
  )
}
