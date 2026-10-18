import { Link } from 'react-router'
import type { DashboardCards } from '@/api/cards'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { CardSummary } from './CardSummary'
import { cardTexts } from './texts'

/**
 * The Accounts part of the Cards screen: all valid cards and accounts with their current and
 * expected end-of-month balances, or a message to set cards up (card settings FR-9, FR-10 and
 * Appendix 3; input cash flow FR-11 and Appendix 2). It was the Dashboard Cards widget before the
 * design refresh (FR-12) and keeps its rules.
 */
export function AccountsSection({
  data,
  isPending,
  isError,
}: {
  data: DashboardCards | undefined
  isPending: boolean
  isError: boolean
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base font-semibold">{cardTexts.dashboardTitle}</CardTitle>
        {data?.hasAnyCards && (
          <CardAction>
            <Button asChild variant="ghost" size="sm">
              <Link to="/settings/cards">{cardTexts.manageCards}</Link>
            </Button>
          </CardAction>
        )}
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        {isPending && <CardDescription>Loading…</CardDescription>}
        {isError && <CardDescription>Cards could not be loaded.</CardDescription>}
        {data && data.cards.length === 0 && (
          <div className="flex flex-col items-start gap-3">
            <CardDescription>
              {data.hasAnyCards ? cardTexts.dashboardNoValidCards : cardTexts.dashboardNoCards}
            </CardDescription>
            <Button asChild size="sm">
              <Link to="/settings/cards">
                {data.hasAnyCards ? cardTexts.manageCards : cardTexts.setUpCards}
              </Link>
            </Button>
          </div>
        )}
        {data?.cards.map((card) => (
          <CardSummary
            key={card.id}
            card={card}
            compact
            balance={(data.balances ?? []).find((b) => b.accountId === card.id)}
          />
        ))}
      </CardContent>
    </Card>
  )
}
