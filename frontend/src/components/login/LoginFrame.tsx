import type { ReactNode } from 'react'
import { LanguageSetting } from '@/components/common/LanguageSetting'
import { Toaster } from '@/components/ui/sonner'
import { loginTexts } from './texts'

/** The page around sign-in, setup and the first password: the app's name, a card, and the language choice. */
export function LoginFrame({
  title,
  description,
  children,
}: {
  title: string
  description?: string
  children: ReactNode
}) {
  return (
    <div className="flex min-h-dvh flex-col items-center bg-page px-4 py-10 text-foreground">
      <main className="flex w-full max-w-md flex-col gap-6">
        <div className="flex items-center gap-2.5">
          <span
            aria-hidden
            className="grid size-9 shrink-0 place-items-center rounded-lg font-heading text-lg font-bold text-white"
            style={{
              background:
                'linear-gradient(135deg, var(--section-dashboard), var(--section-shared))',
            }}
          >
            M
          </span>
          <span className="font-heading text-lg font-bold tracking-tight">
            {loginTexts.appName}
          </span>
        </div>
        <section
          aria-labelledby="login-title"
          className="flex flex-col gap-4 rounded-xl bg-card p-5 shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8"
        >
          <div className="flex flex-col gap-1">
            <h1 id="login-title" className="font-heading text-2xl font-bold tracking-tight">
              {title}
            </h1>
            {description && <p className="text-sm text-muted-foreground">{description}</p>}
          </div>
          {children}
        </section>
        <LanguageSetting />
      </main>
      <Toaster position="top-center" />
    </div>
  )
}
