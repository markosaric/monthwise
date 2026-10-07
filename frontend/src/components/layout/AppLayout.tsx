import { NavLink, Outlet } from 'react-router'
import { Toaster } from '@/components/ui/sonner'
import { cn } from '@/lib/utils'
import { navItems } from './navigation'

/** Sidebar on desktop, bottom navigation on phones. */
export function AppLayout() {
  return (
    <div className="min-h-dvh bg-background text-foreground md:flex">
      <aside className="hidden w-56 shrink-0 border-r md:block">
        <div className="px-5 py-6 text-lg font-semibold tracking-tight">Monthwise</div>
        <nav aria-label="Main" className="flex flex-col gap-1 px-3">
          {navItems.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/'}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground hover:bg-muted hover:text-foreground',
                  isActive && 'bg-muted font-medium text-foreground',
                )
              }
            >
              <Icon className="size-4" aria-hidden />
              {label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <main className="flex-1 px-4 pt-6 pb-24 md:px-8 md:pb-8">
        <Outlet />
      </main>

      <nav
        aria-label="Main mobile"
        className="fixed inset-x-0 bottom-0 grid grid-cols-7 border-t bg-background pb-[env(safe-area-inset-bottom)] md:hidden"
      >
        {navItems.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            className={({ isActive }) =>
              cn(
                'flex flex-col items-center gap-1 py-2 text-[11px] text-muted-foreground',
                isActive && 'font-medium text-foreground',
              )
            }
          >
            <Icon className="size-5" aria-hidden />
            {label}
          </NavLink>
        ))}
      </nav>
      <Toaster position="top-center" />
    </div>
  )
}
