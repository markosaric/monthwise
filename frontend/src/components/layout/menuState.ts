import { useCallback, useState } from 'react'

/** Where the left menu remembers whether it is collapsed, on this device only (FR-6). */
export const MENU_STATE_KEY = 'monthwise.menu'

function readCollapsed(): boolean {
  try {
    return window.localStorage.getItem(MENU_STATE_KEY) === 'collapsed'
  } catch {
    return false
  }
}

/** The left menu's state: expanded the first time, then as the user left it. */
export function useMenuCollapsed(): [boolean, () => void] {
  const [collapsed, setCollapsed] = useState(readCollapsed)
  const toggle = useCallback(() => {
    setCollapsed((current) => {
      const next = !current
      try {
        window.localStorage.setItem(MENU_STATE_KEY, next ? 'collapsed' : 'expanded')
      } catch {
        // Storage can be unavailable (private mode); the menu still works for this visit.
      }
      return next
    })
  }, [])
  return [collapsed, toggle]
}
