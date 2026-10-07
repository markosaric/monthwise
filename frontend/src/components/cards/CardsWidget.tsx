import { Link } from 'react-router'
import { useDashboardCards } from '@/api/cards'
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
 * Dashboard widget: all valid cards with their balances, or a message to set cards up (FR-9, FR-10;
 * balances: input cash flow FR-11).
 */
export function CardsWidget() {
  const { data, isPending, isError } = useDashboardCards()

  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle>{cardTexts.dashboardTitle}</CardTitle>
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
