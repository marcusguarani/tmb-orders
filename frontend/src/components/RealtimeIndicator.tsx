import { useIsFetching } from '@tanstack/react-query'
import { useRealtimeStatus, type RealtimeStatus } from '@/hooks/useRealtime'
import { cn } from '@/lib/utils'

const LABELS: Record<RealtimeStatus, { text: string; title: string; dot: string; ping: string }> = {
  connected: {
    text: 'Tempo real',
    title: 'Conectado via SignalR: as mudanças de status chegam instantaneamente.',
    dot: 'bg-emerald-500',
    ping: 'bg-emerald-400',
  },
  connecting: {
    text: 'Conectando...',
    title: 'Estabelecendo a conexão em tempo real.',
    dot: 'bg-zinc-400',
    ping: 'bg-zinc-300',
  },
  reconnecting: {
    text: 'Reconectando...',
    title: 'Conexão em tempo real perdida. Tentando reconectar; enquanto isso, os dados são atualizados por polling.',
    dot: 'bg-amber-500',
    ping: 'bg-amber-400',
  },
  offline: {
    text: 'Modo polling',
    title: 'Tempo real indisponível. Os dados continuam sendo atualizados automaticamente por polling (fallback).',
    dot: 'bg-amber-500',
    ping: 'bg-amber-400',
  },
}

/** Mostra como a tela está sendo atualizada: tempo real (SignalR) ou fallback por polling. */
export function RealtimeIndicator() {
  const status = useRealtimeStatus()
  const fetching = useIsFetching() > 0
  const label = LABELS[status]
  const pulsing = status === 'connecting' || status === 'reconnecting' || (status === 'offline' && fetching)

  return (
    <span
      role="status"
      title={label.title}
      className="flex items-center gap-2 rounded-full border border-zinc-200 px-2.5 py-1 text-xs font-medium text-zinc-600 dark:border-zinc-800 dark:text-zinc-300"
    >
      <span className="relative flex h-2 w-2">
        <span className={cn('absolute inline-flex h-full w-full rounded-full opacity-75', label.ping, (pulsing || status === 'connected') && 'animate-ping')} />
        <span className={cn('relative inline-flex h-2 w-2 rounded-full', label.dot)} />
      </span>
      <span className="hidden sm:inline">{label.text}</span>
    </span>
  )
}
