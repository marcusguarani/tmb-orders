import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ordersApi } from '@/lib/api'
import { isInProgress } from '@/lib/status'
import type { CreateOrderInput, Order } from '@/types/order'

export const orderKeys = {
  all: ['orders'] as const,
  list: () => [...orderKeys.all, 'list'] as const,
  detail: (id: string) => [...orderKeys.all, 'detail', id] as const,
}

/** Polling "inteligente": rápido enquanto houver pedido em andamento, lento quando tudo estiver finalizado. */
const FAST_POLLING_MS = 1500
const SLOW_POLLING_MS = 10_000

export function useOrders() {
  return useQuery({
    queryKey: orderKeys.list(),
    queryFn: () => ordersApi.list(),
    refetchInterval: (query) =>
      query.state.data?.some((o) => isInProgress(o.status)) ? FAST_POLLING_MS : SLOW_POLLING_MS,
  })
}

export function useOrder(id: string) {
  return useQuery({
    queryKey: orderKeys.detail(id),
    queryFn: () => ordersApi.get(id),
    refetchInterval: (query) => {
      const status = query.state.data?.status
      if (!status) return false
      return isInProgress(status) ? FAST_POLLING_MS : false
    },
    retry: (count, error) => !(error instanceof Error && 'status' in error && error.status === 404) && count < 2,
  })
}

export function useCreateOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: CreateOrderInput) => ordersApi.create(input),
    onSuccess: (created) => {
      // Mostra o pedido na hora (otimista), sem esperar o próximo polling.
      queryClient.setQueryData<Order[]>(orderKeys.list(), (current) =>
        current ? [created, ...current.filter((o) => o.id !== created.id)] : [created],
      )
      void queryClient.invalidateQueries({ queryKey: orderKeys.list() })
    },
  })
}
