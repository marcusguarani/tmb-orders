import { lazy, Suspense } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router'
import { Toaster } from 'sonner'
import { Layout } from '@/components/Layout'
import { Skeleton } from '@/components/ui/Skeleton'
import { useTheme } from '@/hooks/useTheme'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { OrdersPage } from '@/pages/OrdersPage'

// Code splitting: páginas secundárias só são baixadas quando acessadas.
const OrderDetailsPage = lazy(() => import('@/pages/OrderDetailsPage').then((m) => ({ default: m.OrderDetailsPage })))
const AssistantPage = lazy(() => import('@/pages/AssistantPage').then((m) => ({ default: m.AssistantPage })))

function PageFallback() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-8 w-64" />
      <Skeleton className="h-64 w-full" />
    </div>
  )
}

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<OrdersPage />} />
          <Route
            path="pedidos/:id"
            element={<Suspense fallback={<PageFallback />}><OrderDetailsPage /></Suspense>}
          />
          <Route
            path="assistente"
            element={<Suspense fallback={<PageFallback />}><AssistantPage /></Suspense>}
          />
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
