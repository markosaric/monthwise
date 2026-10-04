import { useState, type ReactNode } from 'react'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { cardTexts } from './texts'

/**
 * Confirmation for block, unblock, expire and archive. Names the card in the title, warns when the
 * primary mark goes away, and optionally asks for a reason.
 */
export function CardActionDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel,
  withReason = false,
  removesPrimary = false,
  destructive = false,
  children,
  onConfirm,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: string
  confirmLabel: string
  withReason?: boolean
  removesPrimary?: boolean
  destructive?: boolean
  children?: ReactNode
  onConfirm: (reason: string | null) => Promise<void>
}) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function confirm() {
    setBusy(true)
    setError(null)
    try {
      await onConfirm(reason.trim() === '' ? null : reason.trim())
      setReason('')
      onOpenChange(false)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Something went wrong.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        {removesPrimary && (
          <p className="text-sm font-medium">{cardTexts.dialogs.primaryWarning}</p>
        )}
        {withReason && (
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="action-reason">{cardTexts.dialogs.reason}</Label>
            <Textarea
              id="action-reason"
              maxLength={200}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </div>
        )}
        {children}
        {error && (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        )}
        <AlertDialogFooter>
          <AlertDialogCancel disabled={busy}>{cardTexts.actions.cancel}</AlertDialogCancel>
          <Button
            variant={destructive ? 'destructive' : 'default'}
            onClick={confirm}
            disabled={busy}
          >
            {confirmLabel}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
