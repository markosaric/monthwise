import { lazy, Suspense, type ComponentType } from 'react'
import { ScreenLoading } from '@/components/layout/ScreenLoading'

/** Set before the one reload after a release, cleared by the next screen that loads (FR-4). */
export const RELOAD_FLAG_KEY = 'monthwise.reloadedForNewRelease'

/** The page reload, in an object so tests can replace it. */
export const pageReload = { reload: () => window.location.reload() }

export type LazyScreen = ComponentType & { preload: () => Promise<ComponentType> }

/**
 * A screen whose code is downloaded the first time it is opened (page loading and errors FR-1).
 * Until then a loading sign is shown below the app shell (FR-2); once loaded, the screen renders
 * at once. If the code cannot be downloaded, the page reloads once to pick up a new release, and
 * after that the error goes to the route's error element (FR-4).
 */
export function lazyScreen(load: () => Promise<ComponentType>): LazyScreen {
  let loaded: ComponentType | undefined
  let pending: Promise<ComponentType> | undefined

  const preload = () => {
    pending ??= load().then(
      (component) => {
        loaded = component
        clearReloadFlag()
        return component
      },
      (error: unknown) => {
        // Try again the next time the screen is opened.
        pending = undefined
        Lazy = createLazy()
        return reloadOnceOrThrow(error)
      },
    )
    return pending
  }

  const createLazy = () => lazy(async () => ({ default: await preload() }))
  let Lazy = createLazy()

  function Screen() {
    if (loaded) {
      const Loaded = loaded
      return <Loaded />
    }
    return (
      <Suspense fallback={<ScreenLoading />}>
        <Lazy />
      </Suspense>
    )
  }

  return Object.assign(Screen, { preload })
}

function reloadOnceOrThrow(error: unknown): Promise<never> {
  try {
    if (window.sessionStorage.getItem(RELOAD_FLAG_KEY) === null) {
      window.sessionStorage.setItem(RELOAD_FLAG_KEY, '1')
      pageReload.reload()
      // Keep the loading sign until the page reloads.
      return new Promise<never>(() => {})
    }
  } catch {
    // No session storage: show the error rather than risk reloading in a loop.
  }
  return Promise.reject(error)
}

function clearReloadFlag() {
  try {
    window.sessionStorage.removeItem(RELOAD_FLAG_KEY)
  } catch {
    // Nothing to clear without session storage.
  }
}
