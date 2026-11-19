import './zodConfig'
import { QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { createQueryClient } from './api/queryClient'
import { currentLanguage } from './i18n/language'
import { routes } from './routes'
import './index.css'

// Screen readers pronounce the page in the chosen language (croatian-language.md FR-3).
document.documentElement.lang = currentLanguage()

const queryClient = createQueryClient()
const router = createBrowserRouter(routes)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
