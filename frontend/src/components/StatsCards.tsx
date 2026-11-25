import { CheckCircle2, Clock3, Loader2, Package, Wallet } from 'lucide-react'
import type { ReactNode } from 'react'
import { Card } from '@/components/ui/Card'
import { Skeleton } from '@/components/ui/Skeleton'
import { formatCurrency } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Order } from '@/types/order'

interface Stat {
  label: string
  value: string
  hint: string
  icon: ReactNode
  iconClass: string
}

function buildStats(orders: Order[]): Stat[] {
  const count = (status: Order['status']) => orders.filter((o) => o.status === status).length
  const finished = orders.filter((o) => o.status === 'Finalizado')
  const totalFinished = finished.reduce((sum, o) => sum + o.valor, 0)
  const total = orders.reduce((sum, o) => sum + o.valor, 0)

  return [
    {
      label: 'Total de pedidos',
      value: orders.length.toLocaleString('pt-BR'),
      hint: `${formatCurrency(total)} no total`,
      icon: <Package className="h-5 w-5" />,
      iconClass: 'bg-indigo-50 text-indigo-600 dark:bg-indigo-500/10 dark:text-indigo-400',
    },
    {
      label: 'Pendentes',
      value: count('Pendente').toLocaleString('pt-BR'),
      hint: 'Aguardando o worker',
      icon: <Clock3 className="h-5 w-5" />,
      iconClass: 'bg-amber-50 text-amber-600 dark:bg-amber-500/10 dark:text-amber-400',
    },
    {
      label: 'Processando',
      value: count('Processando').toLocaleString('pt-BR'),
      hint: 'Em processamento agora',
      icon: <Loader2 className={cn('h-5 w-5', count('Processando') > 0 && 'animate-spin')} />,
      iconClass: 'bg-sky-50 text-sky-600 dark:bg-sky-500/10 dark:text-sky-400',
    },
    {
      label: 'Finalizados',
      value: finished.length.toLocaleString('pt-BR'),
      hint: orders.length ? `${Math.round((finished.length / orders.length) * 100)}% dos pedidos` : '—',
      icon: <CheckCircle2 className="h-5 w-5" />,
      iconClass: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-400',
    },
    {
      label: 'Valor finalizado',
      value: formatCurrency(totalFinished),
      hint: 'Soma dos pedidos finalizados',
      icon: <Wallet className="h-5 w-5" />,
      iconClass: 'bg-violet-50 text-violet-600 dark:bg-violet-500/10 dark:text-violet-400',
    },
  ]
}

export function StatsCards({ orders, isLoading }: { orders: Order[] | undefined; isLoading: boolean }) {
  if (isLoading || !orders) {
    return (
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        {Array.from({ length: 5 }, (_, i) => (
          <Card key={i} className={cn('p-4', i === 4 && 'col-span-2 lg:col-span-1')}>
            <Skeleton className="h-4 w-24" />
            <Skeleton className="mt-3 h-7 w-16" />
            <Skeleton className="mt-2 h-3 w-28" />
          </Card>
        ))}
      </div>
    )
  }

  return (
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
      {buildStats(orders).map((stat, i) => (
        <Card key={stat.label} className={cn('p-4', i === 4 && 'col-span-2 lg:col-span-1')}>
          <div className="flex items-start justify-between gap-2">
            <p className="text-sm font-medium text-zinc-500 dark:text-zinc-400">{stat.label}</p>
            <span className={cn('rounded-lg p-1.5', stat.iconClass)}>{stat.icon}</span>
          </div>
          <p className="mt-2 text-2xl font-semibold tracking-tight tabular-nums">{stat.value}</p>
          <p className="mt-1 truncate text-xs text-zinc-500 dark:text-zinc-400">{stat.hint}</p>
        </Card>
      ))}
    </div>
  )
}
