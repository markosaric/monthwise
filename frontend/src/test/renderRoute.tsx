import { QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { createQueryClient } from '@/api/queryClient'
import { preloadScreens, routes } from '@/routes'

// Screens load on demand in the app; tests download them all first so they render at once.
await preloadScreens()

/** Renders the app at the given path with a fresh query cache. */
export function renderRoute(path = '/') {
  const queryClient = createQueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}
