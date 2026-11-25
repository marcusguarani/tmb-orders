import { useEffect, useState } from 'react'

/** Retorna o horário atual, atualizado periodicamente (para textos como "há 2 minutos"). */
export function useNow(intervalMs = 15_000) {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), intervalMs)
    return () => window.clearInterval(id)
  }, [intervalMs])

  return now
}
