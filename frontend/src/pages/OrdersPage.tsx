import { Search, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { NewOrderDialog } from '@/components/NewOrderDialog'
import { OrdersTable } from '@/components/OrdersTable'
import { Pagination } from '@/components/Pagination'
import { StatsCards } from '@/components/StatsCards'
import { EmptyState, ErrorState } from '@/components/StateMessages'
import { Card } from '@/components/ui/Card'
import { useOrders } from '@/hooks/useOrders'
import { useStatusChangeNotifier } from '@/hooks/useStatusChangeNotifier'
import { STATUS_STYLES } from '@/lib/status'
import { cn } from '@/lib/utils'
import { ORDER_STATUSES, type OrderStatus } from '@/types/order'

type Filter = OrderStatus | 'Todos'

const PAGE_SIZE = 10

export function OrdersPage() {
  const { data: orders, isLoading, isError, error, refetch } = useOrders()
  const highlighted = useStatusChangeNotifier(orders)
  const [filter, setFilter] = useState<Filter>('Todos')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const counts = useMemo(() => {
    const result: Record<Filter, number> = { Todos: orders?.length ?? 0, Pendente: 0, Processando: 0, Finalizado: 0 }
    orders?.forEach((o) => result[o.status]++)
    return result
  }, [orders])

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase()
    return (orders ?? []).filter(
      (o) =>
        (filter === 'Todos' || o.status === filter) &&
        (!term ||
          o.cliente.toLowerCase().includes(term) ||
          o.produto.toLowerCase().includes(term) ||
          o.id.toLowerCase().includes(term)),
    )
  }, [orders, filter, search])

  const hasOrders = (orders?.length ?? 0) > 0
  const totalPages = Math.max(1, Math.ceil(visible.length / PAGE_SIZE))

  // Ao mudar filtro ou busca, volta para a primeira página.
  useEffect(() => setPage(1), [filter, search])

  // Se a lista diminuir (ex.: filtro ativo e pedido mudou de status), não deixa a página "sobrar".
  useEffect(() => {
    if (page > totalPages) setPage(totalPages)
  }, [page, totalPages])

  const pageItems = useMemo(() => visible.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE), [visible, page])

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Pedidos</h1>
          <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
            Crie pedidos e acompanhe o processamento assíncrono em tempo real.
          </p>
        </div>
        <NewOrderDialog />
      </div>

      <StatsCards orders={orders} isLoading={isLoading} />

      <Card className="overflow-hidden">
        <div className="flex flex-col gap-3 border-b border-zinc-200 p-3 lg:flex-row lg:items-center lg:justify-between dark:border-zinc-800">
          <div role="tablist" aria-label="Filtrar por status" className="flex gap-1 overflow-x-auto">
            {(['Todos', ...ORDER_STATUSES] as Filter[]).map((option) => {
              const active = filter === option
              return (
                <button
                  key={option}
                  role="tab"
                  type="button"
                  aria-selected={active}
                  onClick={() => setFilter(option)}
                  className={cn(
                    'flex shrink-0 items-center gap-2 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
                    active
                      ? 'bg-zinc-900 text-white dark:bg-white dark:text-zinc-900'
                      : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-400 dark:hover:bg-zinc-800',
                  )}
                >
                  {option !== 'Todos' && <span className={cn('h-1.5 w-1.5 rounded-full', STATUS_STYLES[option].dot)} />}
                  {option}
                  <span
                    className={cn(
                      'rounded-full px-1.5 text-xs tabular-nums',
                      active ? 'bg-white/20 dark:bg-zinc-900/10' : 'bg-zinc-100 dark:bg-zinc-800',
                    )}
                  >
                    {counts[option]}
                  </span>
                </button>
              )
            })}
          </div>

          <div className="relative lg:w-72">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-zinc-400" />
            <input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Buscar cliente, produto ou nº"
              aria-label="Buscar pedidos"
              className="h-9 w-full rounded-lg border-0 bg-zinc-50 pl-9 pr-8 text-sm ring-1 ring-inset ring-zinc-200 placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 dark:bg-zinc-950 dark:ring-zinc-800 [&::-webkit-search-cancel-button]:hidden"
            />
            {search && (
              <button
                type="button"
                onClick={() => setSearch('')}
                aria-label="Limpar busca"
                className="absolute right-2 top-1/2 -translate-y-1/2 rounded p-0.5 text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-200"
              >
                <X className="h-4 w-4" />
              </button>
            )}
          </div>
        </div>

        {isError ? (
          <ErrorState message={error.message} onRetry={() => void refetch()} />
        ) : !isLoading && !hasOrders ? (
          <EmptyState
            title="Nenhum pedido ainda"
            description="Crie o primeiro pedido e acompanhe ele passar por Pendente → Processando → Finalizado."
            action={<NewOrderDialog />}
          />
        ) : !isLoading && visible.length === 0 ? (
          <EmptyState title="Nenhum pedido encontrado" description="Ajuste o filtro de status ou o termo de busca." />
        ) : (
          <OrdersTable orders={pageItems} highlighted={highlighted} isLoading={isLoading} />
        )}

        {!isLoading && !isError && visible.length > 0 && (
          <Pagination page={page} pageSize={PAGE_SIZE} total={visible.length} onPageChange={setPage} />
        )}
      </Card>
    </div>
  )
}
