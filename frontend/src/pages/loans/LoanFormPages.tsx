import { useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import {
  useCreateLoan,
  useLoan,
  useLoanCategories,
  useUpdateLoan,
  type LoanDetails,
} from '@/api/loans'
import { LoanForm } from '@/components/loans/LoanForm'
import {
  emptyLoanFormValues,
  loanFormValuesOf,
  standingOrderAccounts,
  toLoanRequest,
} from '@/components/loans/loanFormModel'
import { loanTexts } from '@/components/loans/texts'
import { currentMonth } from '@/lib/dates'
import { PageHeader } from '@/pages/PageHeader'

const t = loanTexts

/** Add a loan (FR-2). The standing order account is prefilled only with exactly one primary account (FR-3). */
export function LoanNewPage() {
  const navigate = useNavigate()
  const { data: cards, isPending } = useCards('Default')
  const createLoan = useCreateLoan()

  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  const accounts = standingOrderAccounts(cards ?? [])

  return (
    <>
      <PageHeader title={t.addLoan} />
      <LoanForm
        defaultValues={emptyLoanFormValues(
          accounts.length === 1 ? accounts[0]!.id : '',
          currentMonth(),
        )}
        submitLabel={t.addLoan}
        onCancel={() => navigate(-1)}
        onSubmit={async (values) => {
          const details = await createLoan.mutateAsync(toLoanRequest(values))
          toast.success(t.saved)
          navigate(`/loans/${details.loan.id}`, { replace: true })
        }}
      />
    </>
  )
}

/** Edit a loan (FR-10). */
export function LoanEditPage() {
  const { id } = useParams()
  const { data: details, isPending, isError } = useLoan(id)
  const { data: categories, isPending: categoriesPending } = useLoanCategories(true)

  if (isPending || categoriesPending)
    return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError) return <p className="text-sm text-destructive">This loan could not be loaded.</p>
  const allows =
    categories?.find((c) => c.id === details.loan.category.id)?.allowsResidualValue ?? false
  return <LoanEditor details={details} allowsResidualValue={allows} />
}

function LoanEditor({
  details,
  allowsResidualValue,
}: {
  details: LoanDetails
  allowsResidualValue: boolean
}) {
  const navigate = useNavigate()
  const { loan } = details
  const updateLoan = useUpdateLoan(loan.id)

  return (
    <>
      <PageHeader title={`${t.editLoan}: ${loan.name}`} />
      <LoanForm
        editing
        defaultValues={loanFormValuesOf(loan, allowsResidualValue)}
        current={{ category: loan.category, subcategory: loan.subcategory, account: loan.account }}
        submitLabel={t.actions.save}
        onCancel={() => navigate(-1)}
        onSubmit={async (values) => {
          await updateLoan.mutateAsync(toLoanRequest(values))
          toast.success(t.saved)
          navigate(`/loans/${loan.id}`, { replace: true })
        }}
      />
    </>
  )
}
