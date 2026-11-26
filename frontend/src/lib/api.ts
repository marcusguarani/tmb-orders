import type { AnalyticsStatus, AssistantAnswer } from '@/types/analytics'
import type { CreateOrderInput, Order, OrderDetails, OrderStatus } from '@/types/order'

const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'

/** Formato de erro da API (ProblemDetails / RFC 9457). */
interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.detail ?? problem?.title ?? `Erro ${status} ao comunicar com a API.`)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = problem?.errors ?? {}
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${API_BASE}${path}`, {
      ...init,
      headers: { Accept: 'application/json', 'Content-Type': 'application/json', ...init?.headers },
    })
  } catch {
    throw new ApiError(0, { title: 'Não foi possível conectar à API. Verifique se ela está em execução.' })
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null
    throw new ApiError(response.status, problem)
  }

  return (await response.json()) as T
}

export const ordersApi = {
  list: (status?: OrderStatus) =>
    request<Order[]>(status ? `/orders?status=${encodeURIComponent(status)}` : '/orders'),

  get: (id: string) => request<OrderDetails>(`/orders/${encodeURIComponent(id)}`),

  create: (input: CreateOrderInput) =>
    request<Order>('/orders', { method: 'POST', body: JSON.stringify(input) }),
}

export const analyticsApi = {
  status: () => request<AnalyticsStatus>('/analytics/status'),

  ask: (pergunta: string) =>
    request<AssistantAnswer>('/analytics/ask', { method: 'POST', body: JSON.stringify({ pergunta }) }),
}
