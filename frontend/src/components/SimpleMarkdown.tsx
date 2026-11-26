import { Fragment, type ReactNode } from 'react'

/** Renderiza **negrito** dentro de uma linha. */
function renderInline(text: string): ReactNode[] {
  return text.split(/(\*\*[^*]+\*\*)/g).map((part, i) =>
    part.startsWith('**') && part.endsWith('**') && part.length > 4 ? (
      <strong key={i} className="font-semibold">{part.slice(2, -2)}</strong>
    ) : (
      <Fragment key={i}>{part}</Fragment>
    ),
  )
}

/**
 * Markdown mínimo e seguro para as respostas da IA: parágrafos, listas e **negrito**.
 * Não usa dangerouslySetInnerHTML: tudo vira elementos React (sem risco de XSS).
 */
export function SimpleMarkdown({ text }: { text: string }) {
  const blocks: ReactNode[] = []
  let list: string[] = []

  const flushList = () => {
    if (list.length === 0) return
    blocks.push(
      <ul key={`ul-${blocks.length}`} className="my-2 list-disc space-y-1 pl-5">
        {list.map((item, i) => <li key={i}>{renderInline(item)}</li>)}
      </ul>,
    )
    list = []
  }

  for (const rawLine of text.split('\n')) {
    const line = rawLine.trim()
    const bullet = line.match(/^(?:[-*•]|\d+[.)])\s+(.*)$/)

    if (bullet) {
      list.push(bullet[1])
      continue
    }

    flushList()
    if (line) {
      const heading = line.match(/^#{1,6}\s+(.*)$/)
      blocks.push(
        <p key={`p-${blocks.length}`} className={heading ? 'mt-2 font-semibold' : 'my-1'}>
          {renderInline(heading ? heading[1] : line)}
        </p>,
      )
    }
  }
  flushList()

  return <div className="leading-relaxed">{blocks}</div>
}
