import { QueryClient } from '@tanstack/react-query'
import { isAxiosError } from 'axios'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Refetching on focus would fire mid-analysis while the user is reading the
      // report in another window.
      refetchOnWindowFocus: false,
      staleTime: 30_000,
      retry: (failureCount, error) => {
        // Retrying an auth or validation failure just delays the error.
        if (isAxiosError(error)) {
          const status = error.response?.status ?? 0
          if (status >= 400 && status < 500) return false
        }

        return failureCount < 2
      },
    },
    mutations: {
      retry: false,
    },
  },
})
