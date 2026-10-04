import { CardsWidget } from '@/components/cards/CardsWidget'
import { HealthStatus } from '@/components/HealthStatus'
import { PageHeader } from './PageHeader'

export function DashboardPage() {
  return (
    <>
      <PageHeader
        title="Dashboard"
        description="Available budget, income vs. expenses, upcoming due dates and total debt will appear here."
      />
      <div className="flex flex-col gap-6">
        <CardsWidget />
        <HealthStatus />
      </div>
    </>
  )
}
