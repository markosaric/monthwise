import { useState } from 'react'
import { toast } from 'sonner'
import {
  useDataHistory,
  useDataOverview,
  useUpdateReminder,
  type BackupReminder,
  type DataTransfer,
} from '@/api/dataTransfer'
import {
  ExportDialog,
  ImportDialog,
  RestoreSafetyCopyDialog,
} from '@/components/dataTransfer/DataDialogs'
import { daysAgo, formatDateTime } from '@/components/dataTransfer/format'
import { dataTexts as t } from '@/components/dataTransfer/texts'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { Switch } from '@/components/ui/switch'
import { cn } from '@/lib/utils'
import { PageHeader } from '@/pages/PageHeader'

const cardClass =
  'flex flex-col gap-3 rounded-xl bg-card p-4 shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8'

/**
 * Settings → Data and backups: export, import, the safety copy, the reminder and the history
 * (docs/feature-requirements/backup-and-restore.md FR-1 to FR-11).
 */
export function DataSettingsPage() {
  const { data, isPending, isError } = useDataOverview()
  const [dialog, setDialog] = useState<'export' | 'import' | 'restore' | null>(null)
  const close = (open: boolean) => !open && setDialog(null)

  return (
    <>
      <PageHeader title={t.section} description={t.description} />
      <div className="flex max-w-3xl flex-col gap-4">
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && (
          <p className="text-sm text-destructive">Data and backups could not be loaded.</p>
        )}
        {data && (
          <>
            <section aria-label={t.section} className={cardClass}>
              <dl className="grid gap-3 sm:grid-cols-2">
                <LastTransfer label={t.lastExport} transfer={data.lastExport} />
                <LastTransfer label={t.lastImport} transfer={data.lastImport} />
              </dl>
              {data.canManage ? (
                <div className="flex flex-wrap gap-2">
                  <Button onClick={() => setDialog('export')}>{t.export.action}</Button>
                  <Button variant="outline" onClick={() => setDialog('import')}>
                    {t.import.action}
                  </Button>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">{t.readOnly}</p>
              )}
              <p className="text-sm text-muted-foreground">{t.keepSafe}</p>
            </section>

            <section aria-label={t.safetyCopy.title} className={cardClass}>
              <h2 className="font-semibold">{t.safetyCopy.title}</h2>
              <div className="flex flex-wrap items-center gap-2">
                <p className="text-sm text-muted-foreground">
                  {data.safetyCopyCreatedAt
                    ? t.safetyCopy.made(formatDateTime(data.safetyCopyCreatedAt))
                    : t.safetyCopy.none}
                </p>
                {data.safetyCopyCreatedAt && data.canManage && (
                  <Button
                    size="sm"
                    variant="outline"
                    className="ml-auto"
                    onClick={() => setDialog('restore')}
                  >
                    {t.safetyCopy.action}
                  </Button>
                )}
              </div>
            </section>

            <ReminderSetting reminder={data.reminder} canManage={data.canManage} />
            <History />
          </>
        )}
      </div>
      <ExportDialog open={dialog === 'export'} onOpenChange={close} />
      <ImportDialog open={dialog === 'import'} onOpenChange={close} />
      <RestoreSafetyCopyDialog open={dialog === 'restore'} onOpenChange={close} />
    </>
  )
}

function LastTransfer({ label, transfer }: { label: string; transfer: DataTransfer | null }) {
  return (
    <div className="flex flex-col">
      <dt className="text-xs font-semibold tracking-wider text-muted-foreground uppercase">
        {label}
      </dt>
      <dd className="font-semibold">
        {transfer ? (
          <>
            {formatDateTime(transfer.occurredAt)}{' '}
            <span className="font-normal text-muted-foreground">
              ({daysAgo(transfer.occurredAt)})
            </span>
          </>
        ) : (
          t.never
        )}
      </dd>
    </div>
  )
}

/** On or off, and the interval (FR-11). Saved as soon as it changes. */
function ReminderSetting({
  reminder,
  canManage,
}: {
  reminder: BackupReminder
  canManage: boolean
}) {
  const update = useUpdateReminder()

  async function save(enabled: boolean, intervalDays: number) {
    try {
      await update.mutateAsync({ enabled, intervalDays })
      toast.success(t.reminder.saved)
    } catch {
      toast.error('Saving failed.')
    }
  }

  return (
    <section aria-label={t.reminder.title} className={cardClass}>
      <div>
        <h2 className="font-semibold">{t.reminder.title}</h2>
        <p className="text-sm text-muted-foreground">{t.reminder.description}</p>
      </div>
      <div className="flex flex-wrap items-center gap-4">
        <div className="flex items-center gap-2">
          <Switch
            id="reminder-enabled"
            checked={reminder.enabled}
            disabled={!canManage || update.isPending}
            onCheckedChange={(enabled) => save(enabled, reminder.intervalDays)}
          />
          <Label htmlFor="reminder-enabled">{t.reminder.enabled}</Label>
        </div>
        <div className="flex items-center gap-2">
          <Label htmlFor="reminder-interval">{t.reminder.interval}</Label>
          <NativeSelect
            id="reminder-interval"
            value={reminder.intervalDays}
            disabled={!canManage || !reminder.enabled || update.isPending}
            onChange={(e) => save(reminder.enabled, Number(e.target.value))}
          >
            {reminder.intervalChoices.map((days) => (
              <option key={days} value={days}>
                {t.reminder.days(days)}
              </option>
            ))}
          </NativeSelect>
        </div>
      </div>
    </section>
  )
}

/** Every export, import and safety copy, newest first (FR-9). */
function History() {
  const [page, setPage] = useState(1)
  const { data } = useDataHistory(page)
  const pages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1

  return (
    <section aria-label={t.history.title} className={cardClass}>
      <h2 className="font-semibold">{t.history.title}</h2>
      {data?.items.length === 0 && (
        <p className="text-sm text-muted-foreground">{t.history.empty}</p>
      )}
      <ul className="flex flex-col divide-y">
        {data?.items.map((item) => (
          <li key={item.id} className="flex flex-col gap-0.5 py-2 text-sm">
            <span className="flex flex-wrap items-center gap-2">
              <span className={cn('font-medium', !item.succeeded && 'text-destructive')}>
                {t.history.kinds[item.kind]}
                {!item.succeeded && ` · ${t.history.failed}`}
              </span>
              <span className="ml-auto text-muted-foreground">
                {formatDateTime(item.occurredAt)}
              </span>
            </span>
            <span className="text-muted-foreground">
              {[
                item.fileName,
                item.fileCreatedAt && `${t.dataFrom} ${formatDateTime(item.fileCreatedAt)}`,
                `Monthwise ${item.release}`,
                `${t.history.by} ${item.userName ?? '—'}`,
              ]
                .filter(Boolean)
                .join(' · ')}
            </span>
            {item.message && <span className="text-destructive">{item.message}</span>}
          </li>
        ))}
      </ul>
      {pages > 1 && (
        <div className="flex gap-2">
          <Button size="sm" variant="ghost" disabled={page === 1} onClick={() => setPage(page - 1)}>
            {t.history.newer}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            disabled={page >= pages}
            onClick={() => setPage(page + 1)}
          >
            {t.history.older}
          </Button>
        </div>
      )}
    </section>
  )
}
