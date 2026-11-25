import { useEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import { formatCurrency } from '@/lib/format'
import type { Order, OrderStatus } from '@/types/order'

const HIGHLIGHT_MS = 2400

/**
 * Compara a lista atual com a anterior e, para cada pedido que mudou de status:
 *  - mostra uma notificação (toast);
 *  - devolve o id em `highlighted` por alguns segundos (a linha "pisca" na tabela).
 */
export function useStatusChangeNotifier(orders: Order[] | undefined) {
  const previous = useRef<Map<string, OrderStatus> | null>(null)
  const timers = useRef(new Set<number>())
  const [highlighted, setHighlighted] = useState<ReadonlySet<string>>(new Set())

  // Limpa os timers pendentes só quando o componente sai da tela.
  useEffect(() => {
    const pending = timers.current
    return () => pending.forEach((id) => window.clearTimeout(id))
  }, [])

  useEffect(() => {
    if (!orders) return

    const current = new Map(orders.map((o) => [o.id, o.status]))

    // Primeira carga: só memoriza o estado, sem notificar.
    if (previous.current === null) {
      previous.current = current
      return
    }

    const before = previous.current
    previous.current = current

    const changed = orders.filter((o) => {
      const old = before.get(o.id)
      return old !== undefined && old !== o.status
    })

    if (changed.length === 0) return

    for (const order of changed) {
      const description = `${order.cliente} · ${order.produto}`
      if (order.status === 'Finalizado') {
        toast.success('Pedido finalizado', { description: `${description} · ${formatCurrency(order.valor)}` })
      } else if (order.status === 'Processando') {
        toast.info('Pedido em processamento', { description })
      }
    }

    const ids = changed.map((o) => o.id)
    setHighlighted((prev) => new Set([...prev, ...ids]))

    const timer = window.setTimeout(() => {
      timers.current.delete(timer)
      setHighlighted((prev) => {
        const next = new Set(prev)
        ids.forEach((id) => next.delete(id))
        return next
      })
    }, HIGHLIGHT_MS)
    timers.current.add(timer)
  }, [orders])

  return highlighted
}
