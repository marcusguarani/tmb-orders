import {
  HttpTransportType,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { orderKeys } from '@/lib/queryKeys'
import type { Order, OrderDetails, OrderStatus } from '@/types/order'

/**
 * Estado da conexão em tempo real:
 *  - connected:    recebendo eventos via SignalR (WebSocket)
 *  - connecting:   primeira conexão em andamento
 *  - reconnecting: conexão caiu, tentando voltar (polling ativo enquanto isso)
 *  - offline:      sem SignalR; o frontend funciona só com polling (fallback)
 */
export type RealtimeStatus = 'connecting' | 'connected' | 'reconnecting' | 'offline'

interface StatusChangedPayload {
  id: string
  previous_status: OrderStatus
  status: OrderStatus
  changed_at: string
}

const HUB_URL = `${(import.meta.env.VITE_API_URL as string | undefined) ?? '/api'}/hubs/orders`
const RECONNECT_DELAYS_MS = [0, 2000, 5000, 10_000, 15_000, 30_000]
const INITIAL_RETRY_MS = 10_000

const RealtimeContext = createContext<RealtimeStatus>('connecting')

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<RealtimeStatus>('connecting')

  useEffect(() => {
    let disposed = false
    let retryTimer: number | undefined

    const connection: HubConnection = new HubConnectionBuilder()
      .withUrl(HUB_URL, {
        // WebSocket primeiro; se a rede bloquear, o SignalR cai para SSE / long polling.
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect(RECONNECT_DELAYS_MS)
      .configureLogging(LogLevel.Warning)
      .build()

    // Pedido criado (inclusive em outra aba/usuário): entra no topo da lista.
    connection.on('OrderCreated', (order: Order) => {
      queryClient.setQueryData<Order[]>(orderKeys.list(), (current) =>
        current ? [order, ...current.filter((o) => o.id !== order.id)] : current,
      )
    })

    // Status mudou: atualiza a lista na hora e recarrega os detalhes (que têm o histórico).
    connection.on('OrderStatusChanged', (event: StatusChangedPayload) => {
      let known = false
      queryClient.setQueryData<Order[]>(orderKeys.list(), (current) =>
        current?.map((o) => {
          if (o.id !== event.id) return o
          known = true
          return { ...o, status: event.status, data_atualizacao: event.changed_at }
        }),
      )
      queryClient.setQueryData<OrderDetails>(orderKeys.detail(event.id), (current) =>
        current ? { ...current, status: event.status, data_atualizacao: event.changed_at } : current,
      )
      void queryClient.invalidateQueries({ queryKey: orderKeys.detail(event.id) })
      if (!known) void queryClient.invalidateQueries({ queryKey: orderKeys.list() })
    })

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(() => {
      setStatus('connected')
      // Eventos podem ter sido perdidos enquanto estava desconectado: sincroniza tudo.
      void queryClient.invalidateQueries({ queryKey: orderKeys.all })
    })
    connection.onclose(() => {
      if (disposed) return
      setStatus('offline')
      scheduleStart()
    })

    const start = async () => {
      if (disposed || connection.state !== HubConnectionState.Disconnected) return
      try {
        await connection.start()
        if (disposed) return
        setStatus('connected')
        void queryClient.invalidateQueries({ queryKey: orderKeys.all })
      } catch {
        if (disposed) return
        setStatus('offline')
        scheduleStart()
      }
    }

    // withAutomaticReconnect só atua depois da primeira conexão bem-sucedida;
    // falhas na conexão inicial (ou após esgotar as tentativas) são tratadas aqui.
    function scheduleStart() {
      window.clearTimeout(retryTimer)
      retryTimer = window.setTimeout(() => void start(), INITIAL_RETRY_MS)
    }

    // Inicia no próximo "tick": no modo dev, o StrictMode do React monta/desmonta o componente
    // duas vezes; assim a primeira montagem é cancelada antes de abrir uma conexão à toa.
    retryTimer = window.setTimeout(() => void start(), 0)

    return () => {
      disposed = true
      window.clearTimeout(retryTimer)
      void connection.stop()
    }
  }, [queryClient])

  return <RealtimeContext.Provider value={status}>{children}</RealtimeContext.Provider>
}

export const useRealtimeStatus = () => useContext(RealtimeContext)
