import * as Dialog from '@radix-ui/react-dialog'
import { Loader2, Plus, X } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/Button'
import { Field } from '@/components/ui/Field'
import { useCreateOrder } from '@/hooks/useOrders'
import { ApiError } from '@/lib/api'
import { formatCurrency, parseCurrencyInput } from '@/lib/format'

type Errors = Partial<Record<'cliente' | 'produto' | 'valor', string>>

const MAX_TEXT = 150

function validate(cliente: string, produto: string, valor: number): Errors {
  const errors: Errors = {}
  if (!cliente.trim()) errors.cliente = 'Informe o cliente.'
  else if (cliente.trim().length > MAX_TEXT) errors.cliente = `Máximo de ${MAX_TEXT} caracteres.`
  if (!produto.trim()) errors.produto = 'Informe o produto.'
  else if (produto.trim().length > MAX_TEXT) errors.produto = `Máximo de ${MAX_TEXT} caracteres.`
  if (valor <= 0) errors.valor = 'O valor deve ser maior que zero.'
  return errors
}

export function NewOrderDialog() {
  const [open, setOpen] = useState(false)
  const [cliente, setCliente] = useState('')
  const [produto, setProduto] = useState('')
  const [valor, setValor] = useState(0)
  const [errors, setErrors] = useState<Errors>({})
  const createOrder = useCreateOrder()

  const reset = () => {
    setCliente('')
    setProduto('')
    setValor(0)
    setErrors({})
    createOrder.reset()
  }

  const handleOpenChange = (next: boolean) => {
    setOpen(next)
    if (!next) reset()
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()

    const clientErrors = validate(cliente, produto, valor)
    setErrors(clientErrors)
    if (Object.keys(clientErrors).length > 0) return

    try {
      const created = await createOrder.mutateAsync({ cliente: cliente.trim(), produto: produto.trim(), valor })
      toast.success('Pedido criado', {
        description: `${created.cliente} · ${formatCurrency(created.valor)} — enviado para processamento.`,
      })
      handleOpenChange(false)
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        // Erros de validação vindos da API (400 ProblemDetails) aparecem em cada campo.
        const fromApi: Errors = {}
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          if (field === 'cliente' || field === 'produto' || field === 'valor') fromApi[field] = messages[0]
        }
        setErrors(fromApi)
      } else {
        toast.error('Não foi possível criar o pedido', {
          description: error instanceof Error ? error.message : 'Erro inesperado.',
        })
      }
    }
  }

  return (
    <Dialog.Root open={open} onOpenChange={handleOpenChange}>
      <Dialog.Trigger asChild>
        <Button>
          <Plus className="h-4 w-4" />
          Novo pedido
        </Button>
      </Dialog.Trigger>

      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-40 bg-zinc-950/50 backdrop-blur-[2px] data-[state=open]:animate-fade-in" />
        <Dialog.Content
          className="fixed left-1/2 top-1/2 z-50 w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-2xl border border-zinc-200 bg-white p-6 shadow-xl data-[state=open]:animate-scale-in dark:border-zinc-800 dark:bg-zinc-900"
        >
          <div className="flex items-start justify-between gap-4">
            <div>
              <Dialog.Title className="text-lg font-semibold">Novo pedido</Dialog.Title>
              <Dialog.Description className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
                O pedido é criado como <strong>Pendente</strong> e processado automaticamente pelo worker.
              </Dialog.Description>
            </div>
            <Dialog.Close asChild>
              <Button variant="ghost" size="icon" aria-label="Fechar">
                <X className="h-4 w-4" />
              </Button>
            </Dialog.Close>
          </div>

          <form onSubmit={handleSubmit} noValidate className="mt-6 space-y-4">
            <Field
              label="Cliente"
              placeholder="Ex.: Maria Silva"
              autoComplete="off"
              autoFocus
              maxLength={MAX_TEXT}
              value={cliente}
              onChange={(e) => setCliente(e.target.value)}
              error={errors.cliente}
            />
            <Field
              label="Produto"
              placeholder="Ex.: Notebook Dell Inspiron"
              autoComplete="off"
              maxLength={MAX_TEXT}
              value={produto}
              onChange={(e) => setProduto(e.target.value)}
              error={errors.produto}
            />
            <Field
              label="Valor"
              inputMode="numeric"
              placeholder="R$ 0,00"
              value={valor > 0 ? formatCurrency(valor) : ''}
              onChange={(e) => setValor(parseCurrencyInput(e.target.value))}
              error={errors.valor}
              hint="Digite apenas números — os centavos são preenchidos automaticamente."
              className="tabular-nums"
            />

            <div className="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
              <Dialog.Close asChild>
                <Button variant="secondary">Cancelar</Button>
              </Dialog.Close>
              <Button type="submit" disabled={createOrder.isPending}>
                {createOrder.isPending && <Loader2 className="h-4 w-4 animate-spin" />}
                {createOrder.isPending ? 'Criando...' : 'Criar pedido'}
              </Button>
            </div>
          </form>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
