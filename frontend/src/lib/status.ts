import type { OrderStatus } from '@/types/order'

interface StatusStyle {
  label: string
  description: string
  badge: string
  dot: string
  accent: string
}

export const STATUS_STYLES: Record<OrderStatus, StatusStyle> = {
  Pendente: {
    label: 'Pendente',
    description: 'Aguardando processamento',
    badge: 'bg-amber-50 text-amber-800 ring-amber-600/20 dark:bg-amber-400/10 dark:text-amber-300 dark:ring-amber-400/25',
    dot: 'bg-amber-500',
    accent: 'text-amber-600 dark:text-amber-400',
  },
  Processando: {
    label: 'Processando',
    description: 'Worker processando o pedido',
    badge: 'bg-sky-50 text-sky-800 ring-sky-600/20 dark:bg-sky-400/10 dark:text-sky-300 dark:ring-sky-400/25',
    dot: 'bg-sky-500',
    accent: 'text-sky-600 dark:text-sky-400',
  },
  Finalizado: {
    label: 'Finalizado',
    description: 'Processamento concluído',
    badge: 'bg-emerald-50 text-emerald-800 ring-emerald-600/20 dark:bg-emerald-400/10 dark:text-emerald-300 dark:ring-emerald-400/25',
    dot: 'bg-emerald-500',
    accent: 'text-emerald-600 dark:text-emerald-400',
  },
}

export const isInProgress = (status: OrderStatus) => status !== 'Finalizado'
