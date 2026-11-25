import { useIsFetching } from '@tanstack/react-query'
import { Moon, Sun } from 'lucide-react'
import { Link, Outlet } from 'react-router'
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

export function Layout() {
  const { theme, toggle } = useTheme()
  const fetching = useIsFetching() > 0

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-30 border-b border-zinc-200 bg-white/80 backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/80">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-4 px-4 sm:px-6">
          <Link to="/" className="flex items-center gap-2.5 rounded-lg">
            <Logo />
            <div className="leading-tight">
              <p className="font-semibold tracking-tight">TMB Pedidos</p>
              <p className="hidden text-xs text-zinc-500 sm:block dark:text-zinc-400">Gestão de pedidos</p>
            </div>
          </Link>

          <div className="flex items-center gap-2">
            <span
              className="flex items-center gap-2 rounded-full px-2.5 py-1 text-xs text-zinc-500 dark:text-zinc-400"
              title="Os dados são atualizados automaticamente"
            >
              <span className="relative flex h-2 w-2">
                <span className={cn('absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75', fetching && 'animate-ping')} />
                <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-500" />
              </span>
              <span className="hidden sm:inline">Atualização automática</span>
            </span>

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
