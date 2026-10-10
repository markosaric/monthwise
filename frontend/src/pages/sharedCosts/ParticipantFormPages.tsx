import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { z } from 'zod'
import { useChangeParticipant, useParticipant, type ParticipantRequest } from '@/api/sharedCosts'
import { applyApiErrors } from '@/components/expenses/formErrors'
import { Field } from '@/components/expenses/formParts'
import { sharedCostTexts } from '@/components/sharedCosts/texts'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { PageHeader } from '@/pages/PageHeader'

const t = sharedCostTexts

const participantFormSchema = z.object({
  name: z.string().trim().min(1, t.errors.nameRequired).max(50, t.errors.nameTooLong),
  description: z.string().max(200, t.errors.descriptionTooLong),
})

type ParticipantFormValues = z.infer<typeof participantFormSchema>

/** Add a participant (shared bills FR-1). */
export function ParticipantNewPage() {
  const navigate = useNavigate()
  const change = useChangeParticipant()
  return (
    <>
      <PageHeader title={t.screen.addParticipant} />
      <ParticipantForm
        defaultValues={{ name: '', description: '' }}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          await change.mutateAsync({ action: 'create', request })
          toast.success(t.screen.saved)
          navigate('/shared-costs', { replace: true })
        }}
      />
    </>
  )
}

/** Edit a participant; the payer of their contributions is renamed with them (FR-1). */
export function ParticipantEditPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: participant, isPending, isError } = useParticipant(id)
  const change = useChangeParticipant()
  if (isPending) return <p className="text-sm text-muted-foreground">Loading…</p>
  if (isError)
    return <p className="text-sm text-destructive">This participant could not be loaded.</p>
  return (
    <>
      <PageHeader title={t.screen.editParticipant} description={participant.name} />
      <ParticipantForm
        defaultValues={{ name: participant.name, description: participant.description ?? '' }}
        onCancel={() => navigate(-1)}
        onSubmit={async (request) => {
          await change.mutateAsync({ action: 'update', id: participant.id, request })
          toast.success(t.screen.saved)
          navigate('/shared-costs', { replace: true })
        }}
      />
    </>
  )
}

function ParticipantForm({
  defaultValues,
  onSubmit,
  onCancel,
}: {
  defaultValues: ParticipantFormValues
  onSubmit: (request: ParticipantRequest) => Promise<void>
  onCancel: () => void
}) {
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ParticipantFormValues>({
    resolver: zodResolver(participantFormSchema),
    defaultValues,
  })

  async function submit(values: ParticipantFormValues) {
    setFormError(null)
    try {
      await onSubmit({
        name: values.name.trim(),
        description: values.description.trim() === '' ? null : values.description.trim(),
      })
    } catch (error) {
      setFormError(applyApiErrors(error, defaultValues, setError))
    }
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="flex max-w-xl flex-col gap-4">
      <Field id="name" label={t.screen.name} error={errors.name?.message}>
        <Input id="name" maxLength={50} aria-invalid={!!errors.name} {...register('name')} />
      </Field>
      <Field id="description" label={t.screen.description_} error={errors.description?.message}>
        <Textarea id="description" maxLength={200} rows={2} {...register('description')} />
      </Field>
      {formError && (
        <p role="alert" className="text-sm text-destructive">
          {formError}
        </p>
      )}
      <div className="flex gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {t.screen.save}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          {t.screen.cancel}
        </Button>
      </div>
    </form>
  )
}
