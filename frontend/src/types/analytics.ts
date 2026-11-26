export interface AnalyticsStatus {
  configurado: boolean
  modelo: string | null
  provedor: string | null
}

/** Uma consulta (function calling) feita pela IA para responder a pergunta. */
export interface ToolCallInfo {
  ferramenta: string
  argumentos: Record<string, unknown>
  resultado: unknown
}

export interface AssistantAnswer {
  resposta: string
  consultas: ToolCallInfo[]
  modelo: string
  duracao_ms: number
}
