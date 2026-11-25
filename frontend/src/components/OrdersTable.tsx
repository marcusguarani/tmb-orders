import { ChevronRight } from 'lucide-react'
import { useNavigate } from 'react-router'
import { StatusBadge } from '@/components/StatusBadge'
import { Skeleton } from '@/components/ui/Skeleton'
import { useNow } from '@/hooks/useNow'
import { formatCurrency, formatDateTime, formatRelative, shortId } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Order } from '@/types/order'

interface OrdersTableProps {
  orders: Order[]
  highlighted: ReadonlySet<string>
  isLoading: boolean
}

/**
 * Tabela responsiva:
 *  - telas médias/grandes: tabela tradicional;
 *  - celular: lista de cartões (tabelas largas não cabem bem em telas pequenas).
 */
export function OrdersTable({ orders, highlighted, isLoading }: OrdersTableProps) {
  const navigate = useNavigate()
  const now = useNow()
  const open = (id: string) => navigate(`/pedidos/${id}`)

  if (isLoading) {
    return (
      <div className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {Array.from({ length: 6 }, (_, i) => (
          <div key={i} className="flex items-center gap-4 px-4 py-4">
            <Skeleton className="h-4 w-20" />
            <Skeleton className="h-4 flex-1" />
            <Skeleton className="h-4 w-24" />
            <Skeleton className="h-5 w-24 rounded-full" />
          </div>
        ))}
      </div>
    )
  }

  return (
    <>
      {/* Desktop */}
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-zinc-200 bg-zinc-50/70 text-xs font-medium uppercase tracking-wide text-zinc-500 dark:border-zinc-800 dark:bg-zinc-900/60 dark:text-zinc-400">
            <tr>
              <th scope="col" className="px-4 py-3">Pedido</th>
              <th scope="col" className="px-4 py-3">Cliente</th>
              <th scope="col" className="px-4 py-3">Produto</th>
              <th scope="col" className="px-4 py-3 text-right">Valor</th>
              <th scope="col" className="px-4 py-3">Status</th>
              <th scope="col" className="px-4 py-3">Criado</th>
              <th scope="col" className="w-10 px-4 py-3"><span className="sr-only">Abrir</span></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {orders.map((order) => (
              <tr
                key={order.id}
                tabIndex={0}
                onClick={() => open(order.id)}
                onKeyDown={(e) => e.key === 'Enter' && open(order.id)}
                className={cn(
                  'group cursor-pointer transition-colors hover:bg-zinc-50 focus-visible:bg-zinc-50 dark:hover:bg-zinc-800/50 dark:focus-visible:bg-zinc-800/50',
                  highlighted.has(order.id) && 'animate-flash',
                )}
              >
                <td className="px-4 py-3 font-mono text-xs text-zinc-500 dark:text-zinc-400">#{shortId(order.id)}</td>
                <td className="max-w-48 truncate px-4 py-3 font-medium">{order.cliente}</td>
                <td className="max-w-56 truncate px-4 py-3 text-zinc-600 dark:text-zinc-300">{order.produto}</td>
                <td className="px-4 py-3 text-right font-medium tabular-nums">{formatCurrency(order.valor)}</td>
                <td className="px-4 py-3"><StatusBadge status={order.status} /></td>
                <td className="px-4 py-3 text-zinc-500 dark:text-zinc-400" title={formatDateTime(order.data_criacao)}>
                  {formatRelative(order.data_criacao, now)}
                </td>
                <td className="px-4 py-3 text-zinc-400">
                  <ChevronRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Mobile */}
      <ul className="divide-y divide-zinc-100 md:hidden dark:divide-zinc-800">
        {orders.map((order) => (
          <li key={order.id}>
            <button
              type="button"
              onClick={() => open(order.id)}
              className={cn(
                'flex w-full items-center gap-3 px-4 py-3.5 text-left transition-colors hover:bg-zinc-50 dark:hover:bg-zinc-800/50',
                highlighted.has(order.id) && 'animate-flash',
              )}
            >
              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-2">
                  <p className="truncate font-medium">{order.cliente}</p>
                  <p className="shrink-0 text-sm font-medium tabular-nums">{formatCurrency(order.valor)}</p>
                </div>
                <p className="truncate text-sm text-zinc-500 dark:text-zinc-400">{order.produto}</p>
                <div className="mt-2 flex items-center justify-between gap-2">
                  <StatusBadge status={order.status} />
                  <span className="text-xs text-zinc-500 dark:text-zinc-400">
                    #{shortId(order.id)} · {formatRelative(order.data_criacao, now)}
                  </span>
                </div>
              </div>
              <ChevronRight className="h-4 w-4 shrink-0 text-zinc-400" />
            </button>
          </li>
        ))}
      </ul>
    </>
  )
}
