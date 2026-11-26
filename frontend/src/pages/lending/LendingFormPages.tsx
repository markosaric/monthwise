import { useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import { useChangeLending, useLending } from '@/api/lending'
import { appTexts } from '@/components/common/texts'
import { onlyPrimaryAccount } from '@/components/expenses/paidFrom'
import { LendingForm } from '@/components/lending/LendingForm'
import { emptyLendingFormValues, lendingFormValuesOf } from '@/components/lending/lendingFormModel'
import { lendingTexts } from '@/components/lending/texts'
import { todayIso } from '@/lib/dates'
import { PageHeader } from '@/pages/PageHeader'

const t = lendingTexts

/** Add a lending (FR-2): dated today, from the only primary account when there is exactly one (L3, L4). */
export function LendingNewPage() {
  const navigate = useNavigate()
  const { data: cards, isPending } = useCards('Default')
  const change = useChangeLending()
  if (isPending) return <p className="text-sm text-muted-foreground">{appTexts.common.loading}</p>

  return (
    <>
      <PageHeader title={t.add} />
      <LendingForm
        defaultValues={emptyLendingFormValues(todayIso(), onlyPrimaryAccount(cards ?? []))}
        submitLabel={t.actions.save}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          await change.mutateAsync({ action: 'create', request })
          toast.success(t.saved)
          navigate('/lending', { replace: true })
        }}
      />
    </>
  )
}

/** Edit a lending (FR-3), e.g. a later expected return date when the borrower asks (FR-4). */
export function LendingEditPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: lending, isPending, isError } = useLending(id)
  const change = useChangeLending()
  if (isPending) return <p className="text-sm text-muted-foreground">{appTexts.common.loading}</p>
  if (isError) return <p className="text-sm text-destructive">{appTexts.loadErrors.lending}</p>

  return (
    <>
      <PageHeader title={`${t.edit}: ${lending.borrower.name}`} />
      <LendingForm
        defaultValues={lendingFormValuesOf(lending)}
        currentAccount={lending.fromAccount}
        submitLabel={t.actions.save}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          await change.mutateAsync({ action: 'update', id: lending.id, request })
          toast.success(t.saved)
          navigate('/lending', { replace: true })
        }}
      />
    </>
  )
}
