import { ArrowLeft, Check, Copy } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { StatusBadge } from '@/components/StatusBadge'
import { StatusTimeline } from '@/components/StatusTimeline'
import { EmptyState, ErrorState } from '@/components/StateMessages'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { Skeleton } from '@/components/ui/Skeleton'
import { useOrder } from '@/hooks/useOrders'
import { ApiError } from '@/lib/api'
import { formatCurrency, formatDateTime, formatDuration, shortId } from '@/lib/format'
import { STATUS_STYLES } from '@/lib/status'

function InfoRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1 py-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
      <dt className="text-sm text-zinc-500 dark:text-zinc-400">{label}</dt>
      <dd className="min-w-0 text-sm font-medium sm:text-right">{children}</dd>
    </div>
  )
}

function CopyId({ id }: { id: string }) {
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(id)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1500)
    } catch {
      // Clipboard indisponível (ex.: página sem HTTPS fora do localhost).
    }
  }

  return (
    <button
      type="button"
      onClick={copy}
      title="Copiar id completo"
      className="inline-flex max-w-full items-center gap-1.5 rounded font-mono text-xs text-zinc-600 hover:text-zinc-900 dark:text-zinc-300 dark:hover:text-white"
    >
      <span className="truncate">{id}</span>
      {copied ? <Check className="h-3.5 w-3.5 shrink-0 text-emerald-500" /> : <Copy className="h-3.5 w-3.5 shrink-0" />}
    </button>
  )
}

const BackLink = () => (
  <Link
    to="/"
    className="inline-flex items-center gap-1.5 rounded text-sm font-medium text-zinc-500 hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-white"
  >
    <ArrowLeft className="h-4 w-4" />
    Voltar para pedidos
  </Link>
)

export function OrderDetailsPage() {
  const { id = '' } = useParams()
  const { data: order, isLoading, isError, error, refetch } = useOrder(id)

  if (isLoading) {
    return (
      <div className="space-y-6">
        <BackLink />
        <Skeleton className="h-8 w-64" />
        <div className="grid gap-6 lg:grid-cols-5">
          <Card className="p-6 lg:col-span-3"><Skeleton className="h-48 w-full" /></Card>
          <Card className="p-6 lg:col-span-2"><Skeleton className="h-48 w-full" /></Card>
        </div>
      </div>
    )
  }

  if (isError || !order) {
    const notFound = error instanceof ApiError && error.status === 404
    return (
      <div className="space-y-6">
        <BackLink />
        <Card>
          {notFound ? (
            <EmptyState
              title="Pedido não encontrado"
              description="Verifique se o endereço está correto ou volte para a lista de pedidos."
              action={<Link to="/"><Button variant="secondary">Ver pedidos</Button></Link>}
            />
          ) : (
            <ErrorState message={error?.message ?? 'Erro ao carregar o pedido.'} onRetry={() => void refetch()} />
          )}
        </Card>
      </div>
    )
  }

  const finishedAt = order.historico.find((h) => h.status === 'Finalizado')?.data

  return (
    <div className="space-y-6">
      <BackLink />

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <p className="font-mono text-xs text-zinc-500 dark:text-zinc-400">Pedido #{shortId(order.id)}</p>
          <h1 className="mt-1 truncate text-2xl font-semibold tracking-tight">{order.produto}</h1>
        </div>
        <StatusBadge status={order.status} className="self-start px-3 py-1 text-sm sm:self-auto" />
      </div>

      <div className="grid gap-6 lg:grid-cols-5">
        <Card className="p-6 lg:col-span-3">
          <h2 className="font-semibold">Informações</h2>
          <dl className="mt-2 divide-y divide-zinc-100 dark:divide-zinc-800">
            <InfoRow label="Cliente">{order.cliente}</InfoRow>
            <InfoRow label="Produto">{order.produto}</InfoRow>
            <InfoRow label="Valor"><span className="text-base tabular-nums">{formatCurrency(order.valor)}</span></InfoRow>
            <InfoRow label="Status"><StatusBadge status={order.status} /></InfoRow>
            <InfoRow label="Criado em">{formatDateTime(order.data_criacao)}</InfoRow>
            <InfoRow label="Última atualização">
              {order.data_atualizacao ? formatDateTime(order.data_atualizacao) : '—'}
            </InfoRow>
            {finishedAt && (
              <InfoRow label="Tempo total de processamento">
                <span className="text-emerald-600 dark:text-emerald-400">{formatDuration(order.data_criacao, finishedAt)}</span>
              </InfoRow>
            )}
            <InfoRow label="Id (CorrelationId)"><CopyId id={order.id} /></InfoRow>
          </dl>
        </Card>

        <Card className="p-6 lg:col-span-2">
          <div className="flex items-center justify-between">
            <h2 className="font-semibold">Histórico de status</h2>
            <span className="text-xs text-zinc-500 dark:text-zinc-400">{STATUS_STYLES[order.status].description}</span>
          </div>
          <div className="mt-6">
            <StatusTimeline order={order} />
          </div>
        </Card>
      </div>
    </div>
  )
}
