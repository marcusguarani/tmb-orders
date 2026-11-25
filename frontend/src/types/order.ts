// Espelha o contrato JSON da API (.NET), que usa snake_case.

export const ORDER_STATUSES = ['Pendente', 'Processando', 'Finalizado'] as const

export type OrderStatus = (typeof ORDER_STATUSES)[number]

export interface Order {
  id: string
  cliente: string
  produto: string
  valor: number
  status: OrderStatus
  data_criacao: string
  data_atualizacao: string | null
}

export interface OrderStatusHistoryEntry {
  status: OrderStatus
  data: string
}

export interface OrderDetails extends Order {
  historico: OrderStatusHistoryEntry[]
  /** Id do trace (OpenTelemetry) da criação do pedido, para abrir no Jaeger. */
  trace_id: string | null
}

export interface CreateOrderInput {
  cliente: string
  produto: string
  valor: number
}
