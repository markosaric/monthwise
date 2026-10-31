import { PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { Tooltip } from 'radix-ui'
import type { ReactElement } from 'react'
import { NavLink, Outlet } from 'react-router'
import { useReloadAfterImport } from '@/api/dataTransfer'
import { tint } from '@/components/design/colors'
import { Toaster } from '@/components/ui/sonner'
import { cn } from '@/lib/utils'
import { useMenuCollapsed } from './menuState'
import { navItems } from './navigation'
import { SystemStatusAlert } from './SystemStatusAlert'

/**
 * Collapsible left menu with coloured icons on desktop and tablet, bottom navigation on phones
 * (design refresh FR-6 to FR-8). System status appears only when something is wrong.
 */
export function AppLayout() {
  const [collapsed, toggle] = useMenuCollapsed()
  useReloadAfterImport()

  return (
    <Tooltip.Provider delayDuration={300}>
      <div className="min-h-dvh bg-page text-foreground md:flex">
        <aside
          className={cn(
            'sticky top-0 hidden h-dvh shrink-0 flex-col border-r bg-card transition-[width] duration-200 md:flex',
            collapsed ? 'w-[72px]' : 'w-60',
          )}
        >
          <div
            className={cn('flex items-center gap-2.5 py-4', collapsed ? 'flex-col px-0' : 'px-4')}
          >
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
            {!collapsed && (
              <span className="font-heading text-lg font-bold tracking-tight">Monthwise</span>
            )}
            <MenuTooltip label={collapsed ? 'Expand menu' : undefined}>
              <button
                type="button"
                onClick={toggle}
                aria-expanded={!collapsed}
                aria-controls="main-menu"
                aria-label={collapsed ? 'Expand menu' : 'Collapse menu'}
                className={cn(
                  'grid size-8 shrink-0 place-items-center rounded-md border text-muted-foreground hover:bg-muted hover:text-foreground',
                  !collapsed && 'ml-auto',
                )}
              >
                {collapsed ? (
                  <PanelLeftOpen className="size-4" aria-hidden />
                ) : (
                  <PanelLeftClose className="size-4" aria-hidden />
                )}
              </button>
            </MenuTooltip>
          </div>

          <nav id="main-menu" aria-label="Main" className="flex flex-col gap-0.5 px-2.5">
            {navItems.map(({ to, label, icon: Icon, color }) => (
              <MenuTooltip key={to} label={collapsed ? label : undefined}>
                <NavLink
                  to={to}
                  end={to === '/'}
                  aria-label={collapsed ? label : undefined}
                  className={({ isActive }) =>
                    cn(
                      'relative flex items-center gap-3 rounded-lg px-2 py-1.5 text-sm text-muted-foreground hover:bg-muted hover:text-foreground',
                      collapsed && 'justify-center',
                      isActive && 'font-semibold text-foreground',
                    )
                  }
                  style={({ isActive }) => (isActive ? { background: tint(color, 10) } : undefined)}
                >
                  {({ isActive }) => (
                    <>
                      {isActive && (
                        <span
                          aria-hidden
                          className="absolute top-2 bottom-2 -left-2.5 w-1 rounded-r"
                          style={{ background: color }}
                        />
                      )}
                      <span
                        aria-hidden
                        className="grid size-8 shrink-0 place-items-center rounded-lg"
                        style={
                          isActive
                            ? { background: color, color: 'white' }
                            : { background: tint(color, 14), color }
                        }
                      >
                        <Icon className="size-4" />
                      </span>
                      {!collapsed && label}
                    </>
                  )}
                </NavLink>
              </MenuTooltip>
            ))}
          </nav>

          <div className="mt-auto pt-4">
            <SystemStatusAlert variant="menu" collapsed={collapsed} />
          </div>
        </aside>

        <main className="min-w-0 flex-1 px-4 pt-6 pb-28 md:px-8 md:pb-8">
          <Outlet />
        </main>

        <div className="fixed inset-x-0 bottom-0 z-40 md:hidden">
          <SystemStatusAlert variant="bar" />
          <nav
            aria-label="Main mobile"
            className="grid grid-cols-8 border-t bg-card pb-[env(safe-area-inset-bottom)]"
          >
            {navItems.map(({ to, label, shortLabel, icon: Icon, color }) => (
              <NavLink
                key={to}
                to={to}
                end={to === '/'}
                className={({ isActive }) =>
                  cn(
                    'flex flex-col items-center gap-1 py-2 text-[11px] text-muted-foreground',
                    isActive && 'font-semibold text-foreground',
                  )
                }
              >
                <Icon className="size-5" style={{ color }} aria-hidden />
                {shortLabel ?? label}
              </NavLink>
            ))}
          </nav>
        </div>
        <Toaster position="top-center" />
      </div>
    </Tooltip.Provider>
  )
}

/** A label to the right of a menu item, shown only when the menu is collapsed. */
function MenuTooltip({ label, children }: { label?: string; children: ReactElement }) {
  if (!label) return children
  return (
    <Tooltip.Root>
      <Tooltip.Trigger asChild>{children}</Tooltip.Trigger>
      <Tooltip.Portal>
        <Tooltip.Content
          side="right"
          sideOffset={8}
          className="z-50 rounded-md bg-foreground px-2 py-1 text-xs font-medium text-background"
        >
          {label}
        </Tooltip.Content>
      </Tooltip.Portal>
    </Tooltip.Root>
  )
}
