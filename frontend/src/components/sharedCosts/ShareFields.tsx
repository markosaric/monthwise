import { useState } from 'react'
import type { Card } from '@/api/cards'
import { type Share, useChangeParticipant, useParticipants } from '@/api/sharedCosts'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { Switch } from '@/components/ui/switch'
import { payableAccounts } from '@/components/expenses/paidFrom'
import {
  maxShares,
  minShares,
  ownPartOf,
  partOf,
  percentageOf,
  type ShareFormValues,
} from './shareModel'
import { sharedCostTexts } from './texts'

const t = sharedCostTexts.share
const addNewValue = '__new__'

/**
 * Shares a bill, recurring bill, loan or loan payment with one participant (shared bills FR-2): a
 * Shared switch, the participant (with Add new…), the shares (2 to 4) and how many the participant
 * pays, with the percentage and, for a known amount, both parts.
 */
export function ShareFields({
  id,
  value,
  onChange,
  amountCents,
  cards,
  current,
  error,
}: {
  id: string
  value: ShareFormValues
  onChange: (value: ShareFormValues) => void
  /** The amount being shared, when known, to show the parts. */
  amountCents?: number | null
  cards: Card[]
  /** The share the cost already has, so an archived participant or account stays listed. */
  current?: Share | null
  error?: string
}) {
  const { data: participants = [] } = useParticipants()
  const changeParticipant = useChangeParticipant()
  const [adding, setAdding] = useState(false)
  const [newName, setNewName] = useState('')
  const [addError, setAddError] = useState<string | null>(null)
  const accounts = payableAccounts(cards)
  const listed = participants.some((p) => p.id === current?.participant.id)
  const accountListed = accounts.some((a) => a.id === current?.contributionAccount?.id)
  const participantName =
    participants.find((p) => p.id === value.participantId)?.name ??
    (current?.participant.id === value.participantId ? current.participant.name : '')
  const set = (change: Partial<ShareFormValues>) => onChange({ ...value, ...change })

  async function addParticipant() {
    setAddError(null)
    try {
      const created = await changeParticipant.mutateAsync({
        action: 'create',
        request: { name: newName.trim(), description: null },
      })
      if (created) set({ participantId: created.id })
      setAdding(false)
      setNewName('')
    } catch (e) {
      setAddError(e instanceof Error ? e.message : String(e))
    }
  }

  return (
    <fieldset className="flex flex-col gap-3 rounded-lg border p-3">
      <div className="flex items-center gap-3">
        <Switch
          id={`${id}-enabled`}
          checked={value.enabled}
          onCheckedChange={(enabled) => set({ enabled })}
        />
        <Label htmlFor={`${id}-enabled`}>{t.shared}</Label>
      </div>
      {!value.enabled && <p className="text-xs text-muted-foreground">{t.sharedHint}</p>}
      {value.enabled && (
        <>
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor={`${id}-participant`}>{t.participant}</Label>
              <NativeSelect
                id={`${id}-participant`}
                value={adding ? addNewValue : value.participantId}
                aria-invalid={!!error && value.participantId === ''}
                onChange={(e) => {
                  if (e.target.value === addNewValue) {
                    setAdding(true)
                  } else {
                    setAdding(false)
                    set({ participantId: e.target.value })
                  }
                }}
              >
                <option value="">{t.chooseParticipant}</option>
                {current && !listed && (
                  <option value={current.participant.id}>{current.participant.name}</option>
                )}
                {participants.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
                <option value={addNewValue}>{t.addNew}</option>
              </NativeSelect>
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor={`${id}-shares`}>{t.shares}</Label>
              <NativeSelect
                id={`${id}-shares`}
                value={value.shares}
                onChange={(e) => {
                  const shares = Number(e.target.value)
                  set({ shares, participantShares: Math.min(value.participantShares, shares - 1) })
                }}
              >
                {range(minShares, maxShares).map((count) => (
                  <option key={count} value={count}>
                    {t.sharesOption(count)}
                  </option>
                ))}
              </NativeSelect>
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor={`${id}-participantShares`}>{t.participantShares}</Label>
              <NativeSelect
                id={`${id}-participantShares`}
                value={value.participantShares}
                onChange={(e) => set({ participantShares: Number(e.target.value) })}
              >
                {range(1, value.shares - 1).map((count) => (
                  <option key={count} value={count}>
                    {t.participantSharesOption(count, value.shares)}
                  </option>
                ))}
              </NativeSelect>
            </div>
          </div>
          {adding && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor={`${id}-newParticipant`}>{t.newName}</Label>
              <div className="flex gap-2">
                <Input
                  id={`${id}-newParticipant`}
                  maxLength={50}
                  value={newName}
                  onChange={(e) => setNewName(e.target.value)}
                />
                <Button
                  type="button"
                  variant="outline"
                  disabled={newName.trim() === '' || changeParticipant.isPending}
                  onClick={addParticipant}
                >
                  {t.add}
                </Button>
              </div>
              {addError && <p className="text-sm text-destructive">{addError}</p>}
            </div>
          )}
          <p className="text-sm text-muted-foreground">
            {participantName &&
              t.summary(participantName, percentageOf(value.shares, value.participantShares))}
            {participantName && amountCents ? '. ' : ''}
            {amountCents
              ? t.parts(
                  partOf(amountCents, value.shares, value.participantShares),
                  ownPartOf(amountCents, value.shares, value.participantShares),
                )
              : ''}
          </p>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor={`${id}-account`}>{t.contributionAccount}</Label>
            <NativeSelect
              id={`${id}-account`}
              value={value.contributionAccountId}
              onChange={(e) => set({ contributionAccountId: e.target.value })}
            >
              <option value="">{t.contributionAccountDefault}</option>
              {current?.contributionAccount && !accountListed && (
                <option value={current.contributionAccount.id}>
                  {current.contributionAccount.name}
                </option>
              )}
              {accounts.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
                </option>
              ))}
            </NativeSelect>
          </div>
        </>
      )}
      {error && <p className="text-sm text-destructive">{error}</p>}
    </fieldset>
  )
}

function range(from: number, to: number): number[] {
  return Array.from({ length: Math.max(0, to - from + 1) }, (_, i) => from + i)
}
