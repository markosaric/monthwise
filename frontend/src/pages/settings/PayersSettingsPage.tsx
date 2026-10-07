import { useState } from 'react'
import { toast } from 'sonner'
import { ApiError } from '@/api/cards'
import { usePayers, useRemovePayer, useRenamePayer, type Payer } from '@/api/cashFlow'
import { CardActionDialog } from '@/components/cards/CardActionDialog'
import { cashFlowTexts } from '@/components/cashflow/texts'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { PageHeader } from '@/pages/PageHeader'

const t = cashFlowTexts.payers

/** Settings → Payers: rename and remove saved payers (FR-5). */
export function PayersSettingsPage() {
  const { data: payers, isPending, isError } = usePayers()
  const [removing, setRemoving] = useState<Payer | null>(null)
  const remove = useRemovePayer()

  return (
    <>
      <PageHeader title={t.section} description={t.description} />
      <div className="flex max-w-2xl flex-col gap-2">
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && <p className="text-sm text-destructive">Payers could not be loaded.</p>}
        {payers?.length === 0 && <p className="text-sm text-muted-foreground">{t.empty}</p>}
        {payers?.map((payer) => (
          <PayerRow key={payer.id} payer={payer} onRemove={() => setRemoving(payer)} />
        ))}
      </div>
      <CardActionDialog
        open={removing !== null}
        onOpenChange={(open) => !open && setRemoving(null)}
        title={`${t.removeTitle.replace('this payer', removing?.name ?? 'this payer')}`}
        description={t.removeDescription}
        confirmLabel={t.remove}
        destructive
        onConfirm={async () => {
          await remove.mutateAsync(removing!.id)
          toast.success(t.removed)
        }}
      />
    </>
  )
}

function PayerRow({ payer, onRemove }: { payer: Payer; onRemove: () => void }) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(payer.name)
  const [error, setError] = useState<string | null>(null)
  const rename = useRenamePayer()

  async function save() {
    setError(null)
    try {
      await rename.mutateAsync({ id: payer.id, name })
      setEditing(false)
      toast.success(t.renamed)
    } catch (e) {
      setError(
        e instanceof ApiError
          ? (Object.values(e.fieldErrors)[0]?.[0] ?? e.message)
          : 'Saving failed.',
      )
    }
  }

  return (
    <div className="flex flex-col gap-1 rounded-lg border p-3">
      {editing ? (
        <div className="flex flex-wrap items-center gap-2">
          <Input
            aria-label={`${t.rename} ${payer.name}`}
            className="min-w-48 flex-1"
            value={name}
            maxLength={100}
            onChange={(e) => setName(e.target.value)}
          />
          <Button size="sm" onClick={save} disabled={rename.isPending}>
            {cashFlowTexts.actions.save}
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
            {cashFlowTexts.actions.cancel}
          </Button>
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{payer.name}</span>
          <span className="ml-auto flex gap-1">
            <Button size="sm" variant="ghost" onClick={() => setEditing(true)}>
              {t.rename}
            </Button>
            <Button size="sm" variant="ghost" onClick={onRemove}>
              {t.remove}
            </Button>
          </span>
        </div>
      )}
      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  )
}
