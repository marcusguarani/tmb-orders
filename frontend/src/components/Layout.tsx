import { LayoutList, Moon, Sparkles, Sun } from 'lucide-react'
import { Link, NavLink, Outlet } from 'react-router'
import { RealtimeIndicator } from '@/components/RealtimeIndicator'
import { Button } from '@/components/ui/Button'
import { useTheme } from '@/hooks/useTheme'
import { cn } from '@/lib/utils'

function Logo() {
  return (
    <svg viewBox="0 0 32 32" className="h-8 w-8" aria-hidden>
      <rect width="32" height="32" rx="8" className="fill-indigo-600" />
      <path d="M9 11.5 16 8l7 3.5v9L16 24l-7-3.5z" fill="none" stroke="#fff" strokeWidth="2" strokeLinejoin="round" />
      <path d="M9 11.5 16 15l7-3.5M16 15v9" fill="none" stroke="#fff" strokeWidth="2" strokeLinejoin="round" />
    </svg>
  )
}

const navClass = ({ isActive }: { isActive: boolean }) =>
  cn(
    'flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-sm font-medium transition-colors',
    isActive
      ? 'bg-zinc-100 text-zinc-900 dark:bg-zinc-800 dark:text-white'
      : 'text-zinc-500 hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-white',
  )

export function Layout() {
  const { theme, toggle } = useTheme()

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-30 border-b border-zinc-200 bg-white/80 backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/80">
        <div className="mx-auto flex h-16 max-w-7xl items-center gap-3 px-4 sm:gap-6 sm:px-6">
          <Link to="/" className="flex items-center gap-2.5 rounded-lg">
            <Logo />
            <div className="leading-tight">
              <p className="font-semibold tracking-tight">TMB Pedidos</p>
              <p className="hidden text-xs text-zinc-500 sm:block dark:text-zinc-400">Gestão de pedidos</p>
            </div>
          </Link>

          <nav aria-label="Principal" className="flex items-center gap-1">
            <NavLink to="/" end className={navClass}>
              <LayoutList className="h-4 w-4" />
              <span className="hidden sm:inline">Pedidos</span>
            </NavLink>
            <NavLink to="/assistente" className={navClass}>
              <Sparkles className="h-4 w-4" />
              <span className="hidden sm:inline">Assistente IA</span>
            </NavLink>
          </nav>

          <div className="ml-auto flex items-center gap-2">
            <RealtimeIndicator />

            <Button
              variant="ghost"
              size="icon"
              onClick={toggle}
              aria-label={theme === 'dark' ? 'Usar tema claro' : 'Usar tema escuro'}
              title={theme === 'dark' ? 'Tema claro' : 'Tema escuro'}
            >
              {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8">
        <Outlet />
      </main>
    </div>
  )
}
