import { RotateCwIcon } from 'lucide-react'
import { useEffect } from 'react'
import { Link, useRouteError } from 'react-router'
import { Button } from '@/components/ui/button'
import { pageReload } from '@/lib/lazyScreen'
import { PageHeader } from '@/pages/PageHeader'
import { appTexts } from '@/components/common/texts'

/**
 * Shown instead of a screen that failed, below the app shell (page loading and errors FR-3), or
 * on the whole page when the app shell itself failed (`fullPage`). The error is written to the
 * console only, never shown.
 */
export function ScreenError({ fullPage = false }: { fullPage?: boolean }) {
  const error = useRouteError()

  useEffect(() => {
    console.error(error)
  }, [error])

  const message = (
    <section aria-labelledby="screen-error-title">
      <div id="screen-error-title">
        <PageHeader
          title={appTexts.screenError.title}
          description={appTexts.screenError.description}
        />
      </div>
      <div className="flex flex-wrap gap-2">
        <Button type="button" onClick={() => pageReload.reload()}>
          <RotateCwIcon aria-hidden />
          {appTexts.screenError.reload}
        </Button>
        <Button asChild variant="outline">
          <Link to="/">{appTexts.common.backToDashboard}</Link>
        </Button>
      </div>
    </section>
  )

  if (!fullPage) return message
  return (
    <div className="grid min-h-dvh place-items-center bg-page px-4 text-foreground">{message}</div>
  )
}
