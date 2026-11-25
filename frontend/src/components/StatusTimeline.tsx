import { Check } from 'lucide-react'
import { formatDateTime, formatDuration } from '@/lib/format'
import { STATUS_STYLES } from '@/lib/status'
import { cn } from '@/lib/utils'
import { ORDER_STATUSES, type OrderDetails } from '@/types/order'

/**
 * Linha do tempo do pedido: mostra as 3 etapas, quando cada uma aconteceu
 * e quanto tempo levou desde a etapa anterior.
 */
export function StatusTimeline({ order }: { order: OrderDetails }) {
  const reachedAt = new Map(order.historico.map((h) => [h.status, h.data]))
  const currentIndex = ORDER_STATUSES.indexOf(order.status)

  return (
    <ol className="relative">
      {ORDER_STATUSES.map((status, index) => {
        const date = reachedAt.get(status)
        const done = index <= currentIndex
        const isCurrent = index === currentIndex && status !== 'Finalizado'
        const previousDate = index > 0 ? reachedAt.get(ORDER_STATUSES[index - 1]) : undefined
        const isLast = index === ORDER_STATUSES.length - 1
        const style = STATUS_STYLES[status]

        return (
          <li key={status} className="relative flex gap-4 pb-8 last:pb-0">
            {!isLast && (
              <span
                aria-hidden
                className={cn(
                  'absolute left-4 top-9 -ml-px h-[calc(100%-2.25rem)] w-0.5 transition-colors duration-700',
                  index < currentIndex ? 'bg-emerald-500' : 'bg-zinc-200 dark:bg-zinc-800',
                )}
              />
            )}

            <span
              className={cn(
                'relative z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-full ring-4 ring-white transition-colors duration-500 dark:ring-zinc-900',
                done && !isCurrent && 'bg-emerald-500 text-white',
                isCurrent && 'bg-sky-500 text-white',
                !done && 'bg-zinc-100 text-zinc-400 dark:bg-zinc-800',
              )}
            >
              {isCurrent && <span className="absolute inset-0 animate-ping rounded-full bg-sky-400 opacity-40" />}
              {done && !isCurrent ? (
                <Check className="h-4 w-4" />
              ) : (
                <span className="text-xs font-semibold">{index + 1}</span>
              )}
            </span>

            <div className="min-w-0 pt-1">
              <p className={cn('font-medium', !done && 'text-zinc-400 dark:text-zinc-500')}>{style.label}</p>
              <p className="text-sm text-zinc-500 dark:text-zinc-400">
                {date ? formatDateTime(date) : isCurrent ? style.description : 'Aguardando etapa anterior'}
              </p>
              {date && previousDate && (
                <p className={cn('mt-0.5 text-xs font-medium', style.accent)}>
                  +{formatDuration(previousDate, date)} após {STATUS_STYLES[ORDER_STATUSES[index - 1]].label.toLowerCase()}
                </p>
              )}
            </div>
          </li>
        )
      })}
    </ol>
  )
}
