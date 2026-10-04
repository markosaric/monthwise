import { useState } from 'react'
import { Link } from 'react-router'
import { useCards, type CardListFilter } from '@/api/cards'
import { CardSummary } from '@/components/cards/CardSummary'
import { cardTexts } from '@/components/cards/texts'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/pages/PageHeader'

const filters: CardListFilter[] = ['Default', 'Expired', 'Archived']

/** Settings → Cards and accounts: the list with its filters (FR-1). */
export function CardsSettingsPage() {
  const [filter, setFilter] = useState<CardListFilter>('Default')
  const { data: cards, isPending, isError } = useCards(filter)

  return (
    <>
      <PageHeader title={cardTexts.section} description={cardTexts.sectionDescription} />
      <div className="flex max-w-2xl flex-col gap-4">
        <div className="flex flex-wrap items-center gap-2">
          <div role="group" aria-label="Show" className="flex gap-1">
            {filters.map((value) => (
              <Button
                key={value}
                size="sm"
                variant={filter === value ? 'secondary' : 'ghost'}
                aria-pressed={filter === value}
                onClick={() => setFilter(value)}
              >
                {cardTexts.filters[value]}
              </Button>
            ))}
          </div>
          <Button asChild size="sm" className="ml-auto">
            <Link to="/settings/cards/new">{cardTexts.addCard}</Link>
          </Button>
        </div>

        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && <p className="text-sm text-destructive">Cards could not be loaded.</p>}
        {cards?.length === 0 && (
          <div className="flex flex-col items-start gap-3 rounded-lg border border-dashed p-4">
            <p className="text-sm text-muted-foreground">
              {filter === 'Default' ? cardTexts.emptyList : cardTexts.emptyFilter}
            </p>
            {filter === 'Default' && (
              <Button asChild size="sm">
                <Link to="/settings/cards/new">{cardTexts.addCard}</Link>
              </Button>
            )}
          </div>
        )}
        {cards?.map((card) => (
          <CardSummary key={card.id} card={card} />
        ))}
      </div>
    </>
  )
}
