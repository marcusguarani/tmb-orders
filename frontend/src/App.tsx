import { BrowserRouter, Route, Routes } from 'react-router'
import { Toaster } from 'sonner'
import { Layout } from '@/components/Layout'
import { useTheme } from '@/hooks/useTheme'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { OrderDetailsPage } from '@/pages/OrderDetailsPage'
import { OrdersPage } from '@/pages/OrdersPage'

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<OrdersPage />} />
          <Route path="pedidos/:id" element={<OrderDetailsPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
      <ThemedToaster />
    </BrowserRouter>
  )
}

function ThemedToaster() {
  const { theme } = useTheme()
  return <Toaster theme={theme} position="top-right" richColors closeButton duration={4000} />
}
