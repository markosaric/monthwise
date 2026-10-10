import { useState } from 'react'
import { toast } from 'sonner'
import { useCards } from '@/api/cards'
import type { LoanPayment } from '@/api/loans'
import { useChangeLoanPaymentShare } from '@/api/sharedCosts'
import { messageOf } from '@/components/loans/messageOf'
import { PaymentDialog } from '@/components/loans/PaymentDialogs'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { ContributionWarning } from './ContributionWarning'
import { ShareFields } from './ShareFields'
import { shareFormSchema, shareFormValuesOf, toShareRequest } from './shareModel'
import { sharedCostTexts } from './texts'

const t = sharedCostTexts.payment

/**
 * Shares one loan payment, changes its share or stops sharing it, whatever the loan's share is; or lets
 * it follow the loan's share again (shared bills FR-3).
 */
export function PaymentShareDialog({
  payment,
  onOpenChange,
}: {
  payment: LoanPayment
  onOpenChange: (open: boolean) => void
}) {
  const { data: cards = [] } = useCards('Default')
  const change = useChangeLoanPaymentShare()
  const [share, setShare] = useState(shareFormValuesOf(payment.share))
  const [followLoan, setFollowLoan] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function save() {
    const parsed = shareFormSchema.safeParse(share)
    if (!followLoan && !parsed.success) {
      setError(parsed.error.issues[0]?.message ?? null)
      return false
    }
    setError(null)
    try {
      await change.mutateAsync({
        loanId: payment.loanId,
        month: payment.month,
        kind: payment.kind,
        share: followLoan ? 'reset' : toShareRequest(share),
      })
      toast.success(t.saved)
      return true
    } catch (e) {
      setError(messageOf(e))
      return false
    }
  }

  return (
    <PaymentDialog
      open
      onOpenChange={onOpenChange}
      title={`${t.title}: ${payment.loanName}`}
      description={t.description}
      onSave={save}
    >
      {payment.isShareChanged && (
        <div className="flex items-center gap-3">
          <Switch id="payment-follow-loan" checked={followLoan} onCheckedChange={setFollowLoan} />
          <Label htmlFor="payment-follow-loan">{t.followLoan}</Label>
        </div>
      )}
      {!followLoan && (
        <ShareFields
          id="payment-share"
          value={share}
          onChange={setShare}
          amountCents={payment.amountCents}
          cards={cards}
          current={payment.share}
        />
      )}
      {!followLoan && payment.contribution && (
        <ContributionWarning
          contributions={[payment.contribution]}
          amountCents={payment.amountCents}
          share={share}
        />
      )}
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </PaymentDialog>
  )
}
