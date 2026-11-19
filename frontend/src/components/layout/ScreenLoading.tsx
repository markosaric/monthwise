import { Loader2Icon } from 'lucide-react'
import { useEffect, useState } from 'react'
import { appTexts } from '@/components/common/texts'

/** Delay before the loading sign shows, so a fast download does not flash (FR-2). */
export const LOADING_SIGN_DELAY_MS = 300

/** Shown in the screen area while a screen's code downloads. */
export function ScreenLoading() {
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    const timer = window.setTimeout(() => setVisible(true), LOADING_SIGN_DELAY_MS)
    return () => window.clearTimeout(timer)
  }, [])

  if (!visible) return null
  return (
    <div role="status" className="flex items-center gap-2 py-10 text-sm text-muted-foreground">
      <Loader2Icon className="size-4 animate-spin" aria-hidden />
      {appTexts.common.loading}
    </div>
  )
}
