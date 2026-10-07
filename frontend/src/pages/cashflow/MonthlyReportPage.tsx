import { Navigate, useParams } from 'react-router'
import { useMonthlyReport, type InputTotals, type MonthlyReport } from '@/api/cashFlow'
import { formatDate } from '@/components/cards/format'
import { cashFlowTexts } from '@/components/cashflow/texts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatYearMonth, isYearMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { MonthNavigation } from '@/pages/MonthPage'
import { PageHeader } from '@/pages/PageHeader'

const t = cashFlowTexts.report

/** The monthly cash flow report (FR-13). */
export function MonthlyReportPage() {
  const { yearMonth } = useParams()
  const { data: report, isPending, isError } = useMonthlyReport(yearMonth ?? '')
  if (!isYearMonth(yearMonth)) return <Navigate to="/cash-flow" replace />

  return (
    <>
      <PageHeader title={`${t.title}: ${formatYearMonth(yearMonth)}`} />
      <MonthNavigation month={yearMonth} basePath="/cash-flow" />
      {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
      {isError && <p className="text-sm text-destructive">The report could not be loaded.</p>}
      {report && <Report report={report} />}
    </>
  )
}

function Report({ report }: { report: MonthlyReport }) {
  const empty = report.totals.totalCents === 0
  return (
    <div className="flex max-w-3xl flex-col gap-4">
      <Card>
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
          <Groups title={t.byCategory} groups={report.byCategory} />
          <Groups title={t.byAccount} groups={report.byAccount} />
          <Groups title={t.byPayer} groups={report.byPayer} />
        </>
      )}
      <Card>
        <CardHeader>
          <CardTitle>{t.balanceEntries}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2 text-sm">
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
        </CardContent>
      </Card>
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

function Groups({ title, groups }: { title: string; groups: MonthlyReport['byCategory'] }) {
  const labels = cashFlowTexts.totals
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent>
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
      </CardContent>
    </Card>
  )
}
