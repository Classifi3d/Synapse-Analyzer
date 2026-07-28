import { useQuery } from '@tanstack/react-query'
import { queryKeys } from '@/config/queryKeys'
import type { Analysis } from '@/types/analysis'
import { listAnalyses } from './analysisApi'

/**
 * Every analysis belonging to the signed-in user, newest first.
 *
 * The API omits the report body from list responses, so `report` is null here even
 * for completed analyses - use {@link useAnalysis} when the text is needed.
 */
export function useAnalyses(options: { enabled?: boolean } = {}) {
  return useQuery<Analysis[]>({
    queryKey: queryKeys.analyses.lists(),
    // The signal comes from react-query and is forwarded to axios, so navigating away
    // mid-flight cancels the request rather than leaving it to resolve into nothing.
    queryFn: ({ signal }) => listAnalyses(signal),
    enabled: options.enabled ?? true,
  })
}
