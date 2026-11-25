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
}

export interface CreateOrderInput {
  cliente: string
  produto: string
  valor: number
}
