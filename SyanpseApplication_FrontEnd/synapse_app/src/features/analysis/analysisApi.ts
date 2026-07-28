import { apiClient } from '@/config/axiosClient'
import type { Analysis } from '@/types/analysis'

/**
 * Endpoint functions for `/api/analysis`.
 *
 * These stay free of React so they can be called from a loader, a test, or a
 * prefetch as easily as from a hook. Each accepts the `AbortSignal` react-query
 * supplies, so a query that unmounts also cancels its request.
 */

export async function listAnalyses(signal?: AbortSignal): Promise<Analysis[]> {
  const { data } = await apiClient.get<Analysis[]>('/analysis', { signal })
  return data
}

export async function getAnalysis(
  analysisId: string,
  signal?: AbortSignal,
): Promise<Analysis> {
  const { data } = await apiClient.get<Analysis>(`/analysis/${analysisId}`, {
    signal,
  })

  return data
}
