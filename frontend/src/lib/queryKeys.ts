/** Chaves de cache do TanStack Query (compartilhadas por hooks e pelo tempo real). */
export const orderKeys = {
  all: ['orders'] as const,
  list: () => [...orderKeys.all, 'list'] as const,
  detail: (id: string) => [...orderKeys.all, 'detail', id] as const,
}
