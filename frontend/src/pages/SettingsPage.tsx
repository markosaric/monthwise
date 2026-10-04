import { ChevronRight, CreditCard } from 'lucide-react'
import { Link } from 'react-router'
import { cardTexts } from '@/components/cards/texts'
import { PageHeader } from './PageHeader'

const sections = [
  {
    to: '/settings/cards',
    icon: CreditCard,
    title: cardTexts.section,
    description: cardTexts.sectionDescription,
  },
]

export function SettingsPage() {
  return (
    <>
      <PageHeader
        title="Settings"
        description="Categories, recurring items, accounts, cards and shares."
      />
      <nav aria-label="Settings sections" className="flex max-w-2xl flex-col gap-2">
        {sections.map(({ to, icon: Icon, title, description }) => (
          <Link
            key={to}
            to={to}
            className="flex items-center gap-3 rounded-lg border p-3 hover:bg-muted"
          >
            <Icon className="size-5 text-muted-foreground" aria-hidden />
            <span className="flex flex-col">
              <span className="font-medium">{title}</span>
              <span className="text-sm text-muted-foreground">{description}</span>
            </span>
            <ChevronRight className="ml-auto size-4 text-muted-foreground" aria-hidden />
          </Link>
        ))}
      </nav>
    </>
  )
}
