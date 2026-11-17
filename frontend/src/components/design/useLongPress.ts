import { useRef } from 'react'
import type { MouseEvent, PointerEvent } from 'react'

/** A press this long on a touch screen shows a label instead of following the link. */
export const LONG_PRESS_MS = 500

/**
 * Touch long press for icons whose label is a tooltip (design refresh FR-14, key figure tooltips
 * FR-2): holding a finger for `LONG_PRESS_MS` calls `onLongPress`, and the click that follows is
 * swallowed so a link under the finger is not followed. A short tap behaves as usual.
 */
export function useLongPress(onLongPress: () => void) {
  const timer = useRef<number | undefined>(undefined)
  const longPressed = useRef(false)
  const endPress = () => window.clearTimeout(timer.current)

  return {
    onPointerDown: (e: PointerEvent) => {
      if (e.pointerType !== 'touch') return
      longPressed.current = false
      timer.current = window.setTimeout(() => {
        longPressed.current = true
        onLongPress()
      }, LONG_PRESS_MS)
    },
    onPointerUp: endPress,
    onPointerCancel: endPress,
    onClick: (e: MouseEvent) => {
      if (!longPressed.current) return
      e.preventDefault()
      longPressed.current = false
    },
    onContextMenu: (e: MouseEvent) => e.preventDefault(),
  }
}
