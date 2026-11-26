import { useMutation, useQuery } from '@tanstack/react-query'
import { Bot, ChevronDown, Database, Loader2, SendHorizontal, Sparkles, TriangleAlert, User } from 'lucide-react'
import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { SimpleMarkdown } from '@/components/SimpleMarkdown'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { analyticsApi } from '@/lib/api'
import { cn } from '@/lib/utils'
import type { AssistantAnswer, ToolCallInfo } from '@/types/analytics'

const MAX_LENGTH = 500

const SUGGESTIONS = [
  'Quantos pedidos temos hoje?',
  'Qual o tempo médio para aprovar os pedidos?',
  'Quantos pedidos estão pendentes?',
  'Qual o valor total de pedidos finalizados este mês?',
  'Quais clientes mais compraram?',
  'Quais foram os 5 maiores pedidos?',
]

type Message =
  | { id: number; role: 'user'; text: string }
  | { id: number; role: 'assistant'; answer: AssistantAnswer }
  | { id: number; role: 'error'; text: string }

const TOOL_LABELS: Record<string, string> = {
  contar_pedidos: 'Contar pedidos',
  resumo_por_status: 'Resumo por status',
  valor_dos_pedidos: 'Valores dos pedidos',
  tempo_medio_processamento: 'Tempo de processamento',
  listar_pedidos: 'Listar pedidos',
  ranking_clientes: 'Ranking de clientes',
  pedidos_por_dia: 'Pedidos por dia',
}

function ToolCalls({ calls }: { calls: ToolCallInfo[] }) {
  const [open, setOpen] = useState(false)
  if (calls.length === 0) return null

  return (
    <div className="mt-3 border-t border-zinc-200 pt-2 dark:border-zinc-700">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="flex items-center gap-1.5 text-xs font-medium text-zinc-500 hover:text-zinc-800 dark:text-zinc-400 dark:hover:text-zinc-200"
      >
        <Database className="h-3.5 w-3.5" />
        {calls.length === 1 ? '1 consulta ao banco' : `${calls.length} consultas ao banco`}
        <ChevronDown className={cn('h-3.5 w-3.5 transition-transform', open && 'rotate-180')} />
      </button>

      {open && (
        <ol className="mt-2 space-y-2">
          {calls.map((call, i) => (
            <li key={i} className="rounded-lg bg-zinc-50 p-2.5 text-xs dark:bg-zinc-950/60">
              <p className="font-medium text-zinc-700 dark:text-zinc-200">
                {i + 1}. {TOOL_LABELS[call.ferramenta] ?? call.ferramenta}
                <code className="ml-1.5 font-mono text-[11px] text-zinc-400">{call.ferramenta}</code>
              </p>
              <p className="mt-1.5 text-zinc-500">Parâmetros</p>
              <pre className="mt-0.5 overflow-x-auto font-mono text-[11px] text-zinc-600 dark:text-zinc-300">
                {JSON.stringify(call.argumentos, null, 2)}
              </pre>
              <p className="mt-1.5 text-zinc-500">Resultado</p>
              <pre className="mt-0.5 max-h-48 overflow-auto font-mono text-[11px] text-zinc-600 dark:text-zinc-300">
                {JSON.stringify(call.resultado, null, 2)}
              </pre>
            </li>
          ))}
        </ol>
      )}
    </div>
  )
}

function Avatar({ role }: { role: 'user' | 'assistant' | 'error' }) {
  return (
    <span
      className={cn(
        'flex h-8 w-8 shrink-0 items-center justify-center rounded-full',
        role === 'user' && 'bg-zinc-200 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-200',
        role === 'assistant' && 'bg-indigo-600 text-white',
        role === 'error' && 'bg-red-100 text-red-600 dark:bg-red-500/15 dark:text-red-400',
      )}
    >
      {role === 'user' ? <User className="h-4 w-4" /> : role === 'assistant' ? <Bot className="h-4 w-4" /> : <TriangleAlert className="h-4 w-4" />}
    </span>
  )
}

export function AssistantPage() {
  const status = useQuery({ queryKey: ['analytics', 'status'], queryFn: analyticsApi.status, staleTime: 60_000 })
  const [messages, setMessages] = useState<Message[]>([])
  const [question, setQuestion] = useState('')
  const nextId = useRef(1)
  const endRef = useRef<HTMLDivElement>(null)

  const ask = useMutation({
    mutationFn: (pergunta: string) => analyticsApi.ask(pergunta),
    onSuccess: (answer) => setMessages((m) => [...m, { id: nextId.current++, role: 'assistant', answer }]),
    onError: (error) =>
      setMessages((m) => [
        ...m,
        { id: nextId.current++, role: 'error', text: error instanceof Error ? error.message : 'Erro inesperado.' },
      ]),
  })

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [messages, ask.isPending])

  const send = (text: string) => {
    const pergunta = text.trim()
    if (!pergunta || ask.isPending) return
    setMessages((m) => [...m, { id: nextId.current++, role: 'user', text: pergunta }])
    setQuestion('')
    ask.mutate(pergunta)
  }

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    send(question)
  }

  // Enter envia; Shift+Enter quebra a linha.
  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      send(question)
    }
  }

  const configured = status.data?.configurado ?? false

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold tracking-tight">
            <Sparkles className="h-6 w-6 text-indigo-500" />
            Pergunte sobre os pedidos
          </h1>
          <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
            A IA interpreta sua pergunta, consulta o banco com segurança e responde com dados reais.
          </p>
        </div>
        {configured && (
          <span
            title={`Provedor: ${status.data?.provedor ?? ''}`}
            className="flex items-center gap-1.5 self-start whitespace-nowrap rounded-full border border-zinc-200 px-2.5 py-1 text-xs text-zinc-500 sm:self-auto dark:border-zinc-800 dark:text-zinc-400"
          >
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
            {status.data?.modelo}
          </span>
        )}
      </div>

      {status.isSuccess && !configured && (
        <Card className="border-amber-200 bg-amber-50 p-5 dark:border-amber-500/30 dark:bg-amber-500/10">
          <p className="flex items-center gap-2 font-medium text-amber-900 dark:text-amber-200">
            <TriangleAlert className="h-4 w-4" /> Módulo de IA não configurado
          </p>
          <p className="mt-2 text-sm text-amber-800 dark:text-amber-200/80">
            Defina <code className="font-mono">AI_BASE_URL</code>, <code className="font-mono">AI_API_KEY</code> e{' '}
            <code className="font-mono">AI_MODEL</code> no arquivo <code className="font-mono">.env</code> e reinicie a API
            com <code className="font-mono">docker compose up -d api</code>. Há exemplos para OpenAI, Gemini, Groq e Ollama
            no <code className="font-mono">.env.example</code>.
          </p>
        </Card>
      )}

      <Card className="flex min-h-[28rem] flex-col">
        <div className="flex-1 space-y-5 p-4 sm:p-6" aria-live="polite">
          {messages.length === 0 && (
            <div className="py-6 text-center">
              <span className="inline-flex rounded-full bg-indigo-50 p-3 text-indigo-600 dark:bg-indigo-500/10 dark:text-indigo-400">
                <Bot className="h-6 w-6" />
              </span>
              <p className="mt-3 font-medium">Como posso ajudar?</p>
              <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">Experimente uma das perguntas abaixo.</p>
              <div className="mt-5 flex flex-wrap justify-center gap-2">
                {SUGGESTIONS.map((s) => (
                  <button
                    key={s}
                    type="button"
                    disabled={!configured || ask.isPending}
                    onClick={() => send(s)}
                    className="rounded-full border border-zinc-200 bg-white px-3 py-1.5 text-sm text-zinc-700 transition-colors hover:border-indigo-300 hover:bg-indigo-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-200 dark:hover:border-indigo-500/50 dark:hover:bg-indigo-500/10"
                  >
                    {s}
                  </button>
                ))}
              </div>
            </div>
          )}

          {messages.map((message) => (
            <div key={message.id} className={cn('flex gap-3', message.role === 'user' && 'flex-row-reverse')}>
              <Avatar role={message.role} />
              <div
                className={cn(
                  'max-w-[85%] rounded-2xl px-4 py-2.5 text-sm',
                  message.role === 'user' && 'bg-indigo-600 text-white',
                  message.role === 'assistant' && 'bg-zinc-100 text-zinc-800 dark:bg-zinc-800/70 dark:text-zinc-100',
                  message.role === 'error' && 'bg-red-50 text-red-700 dark:bg-red-500/10 dark:text-red-300',
                )}
              >
                {message.role === 'assistant' ? (
                  <>
                    <SimpleMarkdown text={message.answer.resposta} />
                    <ToolCalls calls={message.answer.consultas} />
                    <p className="mt-2 text-[11px] text-zinc-400">
                      {(message.answer.duracao_ms / 1000).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} s ·{' '}
                      {message.answer.modelo}
                    </p>
                  </>
                ) : (
                  <p className="whitespace-pre-wrap">{message.text}</p>
                )}
              </div>
            </div>
          ))}

          {ask.isPending && (
            <div className="flex gap-3">
              <Avatar role="assistant" />
              <div className="flex items-center gap-2 rounded-2xl bg-zinc-100 px-4 py-2.5 text-sm text-zinc-500 dark:bg-zinc-800/70 dark:text-zinc-400">
                <Loader2 className="h-4 w-4 animate-spin" /> Consultando os pedidos...
              </div>
            </div>
          )}
          <div ref={endRef} />
        </div>

        <form onSubmit={handleSubmit} className="border-t border-zinc-200 p-3 dark:border-zinc-800">
          <div className="flex items-end gap-2">
            <label htmlFor="pergunta" className="sr-only">Sua pergunta</label>
            <textarea
              id="pergunta"
              rows={1}
              maxLength={MAX_LENGTH}
              value={question}
              disabled={!configured}
              onChange={(e) => setQuestion(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder={configured ? 'Pergunte algo sobre os pedidos...' : 'Configure o módulo de IA para usar o assistente'}
              className="max-h-40 min-h-10 flex-1 resize-none rounded-lg border-0 bg-zinc-50 px-3 py-2.5 text-sm ring-1 ring-inset ring-zinc-200 placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-zinc-950 dark:ring-zinc-800"
            />
            <Button type="submit" size="icon" className="h-10 w-10" disabled={!configured || ask.isPending || !question.trim()} aria-label="Enviar pergunta">
              <SendHorizontal className="h-4 w-4" />
            </Button>
          </div>
          <p className="mt-1.5 px-1 text-[11px] text-zinc-400">
            Enter envia · Shift+Enter quebra a linha · {question.length}/{MAX_LENGTH}
          </p>
        </form>
      </Card>
    </div>
  )
}
