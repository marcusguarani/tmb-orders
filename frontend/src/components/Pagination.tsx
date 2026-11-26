import { ChevronLeft, ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'

interface PaginationProps {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
}

/** Lista de páginas com reticências: 1 … 4 5 6 … 12 */
function pageItems(page: number, totalPages: number): (number | 'gap')[] {
  if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1)

  const pages = new Set([1, totalPages, page - 1, page, page + 1])
  const sorted = [...pages].filter((p) => p >= 1 && p <= totalPages).sort((a, b) => a - b)

  return sorted.flatMap((p, i) => (i > 0 && p - sorted[i - 1] > 1 ? (['gap', p] as const) : [p]))
}

export function Pagination({ page, pageSize, total, onPageChange }: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize))
  const first = total === 0 ? 0 : (page - 1) * pageSize + 1
  const last = Math.min(page * pageSize, total)

  const buttonClass =
    'inline-flex h-8 min-w-8 items-center justify-center rounded-lg px-2 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-40'

  return (
    <nav
      aria-label="Paginação"
      className="flex flex-col items-center justify-between gap-3 border-t border-zinc-200 px-4 py-3 text-xs text-zinc-500 sm:flex-row dark:border-zinc-800 dark:text-zinc-400"
    >
      <p>
        Exibindo <span className="font-medium text-zinc-700 dark:text-zinc-200">{first}–{last}</span> de{' '}
        <span className="font-medium text-zinc-700 dark:text-zinc-200">{total}</span> pedido(s)
      </p>

      {totalPages > 1 && (
        <div className="flex items-center gap-1">
          <button
            type="button"
            onClick={() => onPageChange(page - 1)}
            disabled={page === 1}
            aria-label="Página anterior"
            className={cn(buttonClass, 'hover:bg-zinc-100 dark:hover:bg-zinc-800')}
          >
            <ChevronLeft className="h-4 w-4" />
          </button>

          {pageItems(page, totalPages).map((item, i) =>
            item === 'gap' ? (
              <span key={`gap-${i}`} className="px-1" aria-hidden>
                …
              </span>
            ) : (
              <button
                key={item}
                type="button"
                onClick={() => onPageChange(item)}
                aria-current={item === page ? 'page' : undefined}
                aria-label={`Página ${item}`}
                className={cn(
                  buttonClass,
                  item === page
                    ? 'bg-zinc-900 text-white dark:bg-white dark:text-zinc-900'
                    : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800',
                )}
              >
                {item}
              </button>
            ),
          )}

          <button
            type="button"
            onClick={() => onPageChange(page + 1)}
            disabled={page === totalPages}
            aria-label="Próxima página"
            className={cn(buttonClass, 'hover:bg-zinc-100 dark:hover:bg-zinc-800')}
          >
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      )}
    </nav>
  )
}
