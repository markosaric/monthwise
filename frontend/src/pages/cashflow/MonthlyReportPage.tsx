import { ArrowDownLeft, ArrowUpRight, Equal } from 'lucide-react'
import { Link, Navigate, useParams } from 'react-router'
import { useMonthlyReport, type InputTotals, type MonthlyReport } from '@/api/cashFlow'
import type { LoanPaymentTotals } from '@/api/loans'
import type { ReportLending } from '@/api/lending'
import type { ReportSharedCosts } from '@/api/sharedCosts'
import { formatDate } from '@/components/cards/format'
import { cashFlowTexts } from '@/components/cashflow/texts'
import { ChangePill } from '@/components/design/ChangePill'
import { CollapsibleCard } from '@/components/design/CollapsibleCard'
import { categoryColor, meaningColors, sectionColors } from '@/components/design/colors'
import { MonthHeader } from '@/components/design/MonthHeader'
import { RingChart } from '@/components/design/RingChart'
import { StatCard, StatGrid } from '@/components/design/StatCard'
import { lendingTexts } from '@/components/lending/texts'
import { sharedCostTexts } from '@/components/sharedCosts/texts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatYearMonth, isYearMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { appTexts } from '@/components/common/texts'
import { dashboardTexts } from '@/components/dashboard/texts'

const t = cashFlowTexts.report

/** The monthly cash flow report with outputs and the month result (FR-13, Appendix 1). */
export function MonthlyReportPage() {
  const { yearMonth } = useParams()
  const { data: report, isPending, isError } = useMonthlyReport(yearMonth ?? '')
  if (!isYearMonth(yearMonth)) return <Navigate to="/cash-flow" replace />

  return (
    <div className="flex max-w-5xl flex-col gap-4">
      <MonthHeader
        month={yearMonth}
        basePath="/cash-flow"
        title={`${t.title}: ${formatYearMonth(yearMonth)}`}
      />
      {isPending && <p className="text-sm text-muted-foreground">{appTexts.common.loading}</p>}
      {isError && <p className="text-sm text-destructive">{appTexts.loadErrors.report}</p>}
      {report && <Report report={report} />}
    </div>
  )
}

function Report({ report }: { report: MonthlyReport }) {
  const empty = report.totals.totalCents === 0
  const outputs = report.outputs
  return (
    <div className="flex max-w-5xl flex-col gap-4">
      <StatGrid label={appTexts.common.keyFigures}>
        <StatCard
          icon={ArrowDownLeft}
          color={meaningColors.moneyIn}
          label={t.inputs}
          value={formatCents(report.totals.totalCents)}
          pill={
            <ChangePill
              current={report.totals.totalCents}
              previous={report.previousMonthTotalCents}
              higherIsBetter
            />
          }
        />
        <StatCard
          icon={ArrowUpRight}
          color={meaningColors.moneyOut}
          label={cashFlowTexts.cashFlow.outputs}
          value={formatCents(outputs.totals.totalCents)}
          pill={
            <ChangePill
              current={outputs.totals.totalCents}
              previous={report.previousMonthOutputsCents}
              higherIsBetter={false}
            />
          }
        />
        <StatCard
          icon={Equal}
          color={sectionColors.cashflow}
          label={dashboardTexts.monthResult}
          value={formatCents(report.monthResultCents)}
          problem={report.monthResultCents < 0}
          pill={
            <ChangePill
              current={report.monthResultCents}
              previous={report.previousMonthResultCents}
              higherIsBetter
            />
          }
        />
      </StatGrid>
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base font-semibold">
              {appTexts.report.inputsByCategory}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <RingChart
              label={appTexts.report.inputsByCategory}
              slices={report.byCategory.map((g, i) => ({
                label: g.name,
                cents: g.totals.totalCents,
                color: categoryColor(i),
              }))}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-base font-semibold">{t.outputsByCategory}</CardTitle>
          </CardHeader>
          <CardContent>
            <RingChart
              label={t.outputsByCategory}
              slices={outputs.byCategory.map((g, i) => ({
                label: g.name,
                cents: g.totals.totalCents,
                color: categoryColor(i),
              }))}
            />
          </CardContent>
        </Card>
      </div>
      <Card>
        <CardContent>
          <dl className="grid grid-cols-2 gap-2 text-sm">
            <div>
              <dt className="text-muted-foreground">{t.monthResult}</dt>
              <dd className="text-lg font-semibold tabular-nums">
                {formatCents(report.monthResultCents)}
              </dd>
            </div>
            <div>
              <dt className="text-muted-foreground">{t.previousMonthResult}</dt>
              <dd className="tabular-nums">{formatCents(report.previousMonthResultCents)}</dd>
            </div>
          </dl>
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>{t.inputs}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <TotalsRow totals={report.totals} />
          <dl className="grid grid-cols-2 gap-2 border-t pt-3 text-sm">
            <div>
              <dt className="text-muted-foreground">{t.previousMonth}</dt>
              <dd className="tabular-nums">{formatCents(report.previousMonthTotalCents)}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">{t.difference}</dt>
              <dd className="font-medium tabular-nums">
                {report.differenceCents > 0 ? '+' : ''}
                {formatCents(report.differenceCents)}
              </dd>
            </div>
          </dl>
        </CardContent>
      </Card>
      {empty ? (
        <p className="text-sm text-muted-foreground">{t.empty}</p>
      ) : (
        <>
          <Groups title={t.byCategory} groups={report.byCategory} defaultOpen />
          <Groups title={t.byAccount} groups={report.byAccount} />
          <Groups title={t.byPayer} groups={report.byPayer} />
        </>
      )}
      <Card>
        <CardHeader>
          <CardTitle>{t.outputs}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <OutputTotalsRow totals={outputs.totals} />
          <dl className="grid grid-cols-2 gap-2 border-t pt-3 text-sm">
            <div>
              <dt className="text-muted-foreground">{t.previousMonthOutputs}</dt>
              <dd className="tabular-nums">{formatCents(report.previousMonthOutputsCents)}</dd>
            </div>
          </dl>
        </CardContent>
      </Card>
      {outputs.byCategory.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t.noOutputs}</p>
      ) : (
        <>
          <OutputGroups title={t.outputsByCategory} groups={outputs.byCategory} defaultOpen />
          <OutputGroups title={t.outputsByAccount} groups={outputs.byAccount} />
          {outputs.byLoan.length > 0 && (
            <OutputGroups title={t.outputsByLoan} groups={outputs.byLoan} />
          )}
          {outputs.byCard.length > 0 && (
            <OutputGroups
              title={t.outputsByCard}
              groups={outputs.byCard}
              linkTo={(cardId) => `/cards/${cardId}/payments/${report.month}`}
            />
          )}
        </>
      )}
      {report.sharedCosts && report.sharedCosts.totalCents > 0 && (
        <SharedCostsCard shared={report.sharedCosts} />
      )}
      {report.lending && <LendingReportCard lending={report.lending} />}

      <CollapsibleCard title={t.balanceEntries}>
        <div className="flex flex-col gap-2 text-sm">
          <p className="text-xs text-muted-foreground">{t.note}</p>
          {report.balanceEntries.length === 0 ? (
            <p className="text-muted-foreground">{t.noBalanceEntries}</p>
          ) : (
            <ul className="flex flex-col divide-y">
              {report.balanceEntries.map((entry) => (
                <li
                  key={`${entry.account.id}-${entry.date}-${entry.balanceCents}`}
                  className="flex flex-wrap gap-x-3 py-2"
                >
                  <span className="font-medium">{entry.account.name}</span>
                  <span>{cashFlowTexts.balance.kinds[entry.kind ?? 'Correction']}</span>
                  <span className="text-muted-foreground">{formatDate(entry.date)}</span>
                  <span className="ml-auto tabular-nums">{formatCents(entry.balanceCents)}</span>
                  {entry.kind === 'Correction' && (
                    <span className="text-muted-foreground tabular-nums">
                      ({entry.differenceCents > 0 ? '+' : ''}
                      {formatCents(entry.differenceCents)})
                    </span>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
      </CollapsibleCard>
    </div>
  )
}

function TotalsRow({ totals }: { totals: InputTotals }) {
  const labels = cashFlowTexts.totals
  return (
    <dl className="grid grid-cols-3 gap-2 text-sm">
      <div>
        <dt className="text-muted-foreground">{labels.expected}</dt>
        <dd className="text-muted-foreground tabular-nums">{formatCents(totals.expectedCents)}</dd>
      </div>
      <div>
        <dt className="text-muted-foreground">{labels.received}</dt>
        <dd className="tabular-nums">{formatCents(totals.receivedCents)}</dd>
      </div>
      <div>
        <dt className="text-muted-foreground">{labels.total}</dt>
        <dd className="text-lg font-semibold tabular-nums">{formatCents(totals.totalCents)}</dd>
      </div>
    </dl>
  )
}

function Groups({
  title,
  groups,
  defaultOpen = false,
}: {
  title: string
  groups: MonthlyReport['byCategory']
  defaultOpen?: boolean
}) {
  const labels = cashFlowTexts.totals
  return (
    <CollapsibleCard title={title} defaultOpen={defaultOpen}>
      <ul className="flex flex-col divide-y text-sm">
        {groups.map((group) => (
          <li key={group.id} className="flex flex-col gap-0.5 py-2">
            <div className="flex items-baseline gap-3">
              <span className="min-w-0 flex-1 truncate">{group.name}</span>
              <span className="font-medium tabular-nums">
                {formatCents(group.totals.totalCents)}
              </span>
            </div>
            <div className="flex flex-wrap gap-x-4 text-xs text-muted-foreground">
              <span>
                {labels.received}{' '}
                <span className="tabular-nums">{formatCents(group.totals.receivedCents)}</span>
              </span>
              <span>
                {labels.expected}{' '}
                <span className="tabular-nums">{formatCents(group.totals.expectedCents)}</span>
              </span>
            </div>
          </li>
        ))}
      </ul>
    </CollapsibleCard>
  )
}

function OutputTotalsRow({ totals }: { totals: LoanPaymentTotals }) {
  const labels = cashFlowTexts.totals
  return (
    <dl className="grid grid-cols-3 gap-2 text-sm">
      <div>
        <dt className="text-muted-foreground">{labels.expected}</dt>
        <dd className="text-muted-foreground tabular-nums">{formatCents(totals.expectedCents)}</dd>
      </div>
      <div>
        <dt className="text-muted-foreground">{t.paid}</dt>
        <dd className="tabular-nums">{formatCents(totals.paidCents)}</dd>
      </div>
      <div>
        <dt className="text-muted-foreground">{t.outputs}</dt>
        <dd className="text-lg font-semibold tabular-nums">{formatCents(totals.totalCents)}</dd>
      </div>
    </dl>
  )
}

function OutputGroups({
  title,
  groups,
  linkTo,
  defaultOpen = false,
}: {
  title: string
  groups: MonthlyReport['outputs']['byCategory']
  /** Where a group's name links to, e.g. the drill-down of a card payment (installments FR-8). */
  linkTo?: (id: string) => string
  defaultOpen?: boolean
}) {
  return (
    <CollapsibleCard title={title} defaultOpen={defaultOpen}>
      <ul className="flex flex-col divide-y text-sm">
        {groups.map((group) => (
          <li key={group.id ?? 'none'} className="flex flex-col gap-0.5 py-2">
            <div className="flex items-baseline gap-3">
              {linkTo && group.id ? (
                <Link className="min-w-0 flex-1 truncate hover:underline" to={linkTo(group.id)}>
                  {group.name}
                </Link>
              ) : (
                <span className="min-w-0 flex-1 truncate">{group.name}</span>
              )}
              <span className="font-medium tabular-nums">
                {formatCents(group.totals.totalCents)}
              </span>
            </div>
            <div className="flex flex-wrap gap-x-4 text-xs text-muted-foreground">
              <span>
                {t.paid} <span className="tabular-nums">{formatCents(group.totals.paidCents)}</span>
              </span>
              <span>
                {cashFlowTexts.totals.expected}{' '}
                <span className="tabular-nums">{formatCents(group.totals.expectedCents)}</span>
              </span>
            </div>
          </li>
        ))}
      </ul>
    </CollapsibleCard>
  )
}

/** The month's shared bills and loan payments, the participants' parts, the own part and contributions (shared bills FR-9). */
function SharedCostsCard({ shared }: { shared: ReportSharedCosts }) {
  const s = sharedCostTexts.report
  const rows: [string, number][] = [
    [s.total, shared.totalCents],
    [s.participantsPart, shared.participantsPartCents],
    [s.ownPart, shared.ownPartCents],
    [s.received, shared.contributionsReceivedCents],
    [s.outstanding, shared.contributionsOutstandingCents],
  ]
  return (
    <Card>
      <CardHeader>
        <CardTitle>{s.title}</CardTitle>
      </CardHeader>
      <CardContent>
        <dl className="grid gap-2 text-sm sm:grid-cols-2">
          {rows.map(([label, cents]) => (
            <div key={label} className="flex justify-between gap-4">
              <dt className="text-muted-foreground">{label}</dt>
              <dd className="font-medium tabular-nums">{formatCents(cents)}</dd>
            </div>
          ))}
        </dl>
      </CardContent>
    </Card>
  )
}

/** The month's lending: lent, returned and expected back, in total and per borrower (money lending FR-10). */
function LendingReportCard({ lending }: { lending: ReportLending }) {
  const l = lendingTexts
  return (
    <Card>
      <CardHeader>
        <CardTitle>{l.report.title}</CardTitle>
      </CardHeader>
      <CardContent className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="text-left text-muted-foreground">
            <tr>
              <th className="py-1 pr-3 font-medium">{l.report.borrower}</th>
              <th className="py-1 pr-3 text-right font-medium">{l.month.totals.lent}</th>
              <th className="py-1 pr-3 text-right font-medium">{l.month.totals.returned}</th>
              <th className="py-1 text-right font-medium">{l.month.totals.expectedBack}</th>
            </tr>
          </thead>
          <tbody>
            {lending.byBorrower.map((b) => (
              <tr key={b.payerId} className="border-t">
                <td className="py-1.5 pr-3">{b.name}</td>
                <td className="py-1.5 pr-3 text-right tabular-nums">{formatCents(b.lentCents)}</td>
                <td className="py-1.5 pr-3 text-right tabular-nums">
                  {formatCents(b.returnedCents)}
                </td>
                <td className="py-1.5 text-right tabular-nums">
                  {formatCents(b.expectedBackCents)}
                </td>
              </tr>
            ))}
            <tr className="border-t font-medium">
              <td className="py-1.5 pr-3">{l.report.total}</td>
              <td className="py-1.5 pr-3 text-right tabular-nums">
                {formatCents(lending.lentCents)}
              </td>
              <td className="py-1.5 pr-3 text-right tabular-nums">
                {formatCents(lending.returnedCents)}
              </td>
              <td className="py-1.5 text-right tabular-nums">
                {formatCents(lending.expectedBackCents)}
              </td>
            </tr>
          </tbody>
        </table>
      </CardContent>
    </Card>
  )
}
