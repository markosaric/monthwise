import { DatabaseBackup } from 'lucide-react'
import { Link } from 'react-router'
import { useDataOverview, useDismissReminder } from '@/api/dataTransfer'
import { sectionColors } from '@/components/design/colors'
import { IconTile } from '@/components/design/IconTile'
import { Button } from '@/components/ui/button'
import { formatDay } from './format'
import { dataTexts } from './texts'

const t = dataTexts.reminder

/** The Dashboard reminder to export the data, while it is due (backup and restore FR-10). */
export function ExportReminder() {
  const { data } = useDataOverview()
  const dismiss = useDismissReminder()
  if (!data?.reminder.isDue) return null

  return (
    <section
      aria-label={t.dashboardTitle}
      className="flex flex-wrap items-center gap-3 rounded-xl border-l-4 bg-card p-4 shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8"
      style={{ borderLeftColor: sectionColors.savings }}
    >
      <IconTile icon={DatabaseBackup} color={sectionColors.savings} />
      <div className="flex min-w-0 flex-1 flex-col">
        <span className="font-semibold">{t.dashboardTitle}</span>
        <span className="text-sm text-muted-foreground">
          {t.dashboard(data.lastExport ? formatDay(data.lastExport.occurredAt) : null)}
        </span>
      </div>
      <span className="flex gap-2">
        <Button asChild size="sm">
          <Link to="/settings/data">{t.open}</Link>
        </Button>
        <Button
          size="sm"
          variant="ghost"
          disabled={dismiss.isPending}
          onClick={() => dismiss.mutate()}
        >
          {t.dismiss}
        </Button>
      </span>
    </section>
  )
}
