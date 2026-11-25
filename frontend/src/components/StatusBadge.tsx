import { STATUS_STYLES } from '@/lib/status'
import { cn } from '@/lib/utils'
import type { OrderStatus } from '@/types/order'

export function StatusBadge({ status, className }: { status: OrderStatus; className?: string }) {
  const style = STATUS_STYLES[status]
  const animated = status === 'Processando'

  return (
    <span
      title={style.description}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ring-inset transition-colors duration-500',
        style.badge,
        className,
      )}
    >
      <span className="relative flex h-2 w-2">
        {animated && (
          <span className={cn('absolute inline-flex h-full w-full animate-ping rounded-full opacity-75', style.dot)} />
        )}
        <span className={cn('relative inline-flex h-2 w-2 rounded-full', style.dot)} />
      </span>
      {style.label}
    </span>
  )
}
