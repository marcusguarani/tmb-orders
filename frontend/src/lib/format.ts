const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'medium' })
const time = new Intl.DateTimeFormat('pt-BR', { timeStyle: 'medium' })
const relative = new Intl.RelativeTimeFormat('pt-BR', { numeric: 'auto' })

export const formatCurrency = (value: number) => currency.format(value)

export const formatDateTime = (iso: string) => dateTime.format(new Date(iso))

export const formatTime = (iso: string) => time.format(new Date(iso))

export function formatRelative(iso: string, now = Date.now()): string {
  const seconds = Math.round((new Date(iso).getTime() - now) / 1000)
  const abs = Math.abs(seconds)

  if (abs < 10) return 'agora mesmo'
  if (abs < 60) return relative.format(seconds, 'second')
  if (abs < 3600) return relative.format(Math.round(seconds / 60), 'minute')
  if (abs < 86400) return relative.format(Math.round(seconds / 3600), 'hour')
  return relative.format(Math.round(seconds / 86400), 'day')
}

/** Duração entre duas datas, ex.: "1,2 s", "3 min 05 s". */
export function formatDuration(fromIso: string, toIso: string): string {
  const ms = Math.max(0, new Date(toIso).getTime() - new Date(fromIso).getTime())
  if (ms < 60_000) return `${(ms / 1000).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} s`
  const minutes = Math.floor(ms / 60_000)
  const seconds = Math.round((ms % 60_000) / 1000)
  return `${minutes} min ${String(seconds).padStart(2, '0')} s`
}

export const shortId = (id: string) => id.slice(-8).toUpperCase()

/** Converte só os dígitos digitados em reais: "459990" → 4599.90 */
export function parseCurrencyInput(raw: string): number {
  const digits = raw.replace(/\D/g, '').slice(0, 12)
  return digits ? Number(digits) / 100 : 0
}
