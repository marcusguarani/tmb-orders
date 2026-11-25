import { Link } from 'react-router'
import { EmptyState } from '@/components/StateMessages'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'

export function NotFoundPage() {
  return (
    <Card>
      <EmptyState
        title="Página não encontrada"
        description="O endereço acessado não existe."
        action={<Link to="/"><Button variant="secondary">Ir para pedidos</Button></Link>}
      />
    </Card>
  )
}
