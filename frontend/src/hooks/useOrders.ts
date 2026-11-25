import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useRealtimeStatus, type RealtimeStatus } from '@/hooks/useRealtime'
import { ApiError, ordersApi } from '@/lib/api'
import { orderKeys } from '@/lib/queryKeys'
import { isInProgress } from '@/lib/status'
import type { CreateOrderInput, Order, OrderStatus } from '@/types/order'

export { orderKeys }

/**
 * Estratégia de atualização:
 *  - SignalR conectado → os eventos chegam por push; polling só de segurança (30 s).
 *  - SignalR fora (fallback) → polling rápido (1,5 s) enquanto houver pedido em andamento,
 *    lento (10 s) quando tudo estiver finalizado.
 */
const FAST_POLLING_MS = 1500
const SLOW_POLLING_MS = 10_000
const SAFETY_POLLING_MS = 30_000

function pollingInterval(realtime: RealtimeStatus, status: OrderStatus | undefined, anyInProgress: boolean) {
  if (realtime === 'connected') return SAFETY_POLLING_MS
  if (status !== undefined) return isInProgress(status) ? FAST_POLLING_MS : false
  return anyInProgress ? FAST_POLLING_MS : SLOW_POLLING_MS
}

export function useOrders() {
  const realtime = useRealtimeStatus()

  return useQuery({
    queryKey: orderKeys.list(),
    queryFn: () => ordersApi.list(),
    refetchInterval: (query) =>
      pollingInterval(realtime, undefined, query.state.data?.some((o) => isInProgress(o.status)) ?? false),
  })
}

export function useOrder(id: string) {
  const realtime = useRealtimeStatus()

  return useQuery({
    queryKey: orderKeys.detail(id),
    queryFn: () => ordersApi.get(id),
    refetchInterval: (query) => {
      const status = query.state.data?.status
      if (!status) return false
      return pollingInterval(realtime, status, false)
    },
    retry: (count, error) => !(error instanceof ApiError && error.status === 404) && count < 2,
  })
}

export function useCreateOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: CreateOrderInput) => ordersApi.create(input),
    onSuccess: (created) => {
      // Mostra o pedido na hora, sem esperar o próximo evento/polling.
      queryClient.setQueryData<Order[]>(orderKeys.list(), (current) =>
        current ? [created, ...current.filter((o) => o.id !== created.id)] : [created],
      )
    },
  })
}
