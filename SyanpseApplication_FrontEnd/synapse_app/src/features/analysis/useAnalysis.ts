import { useQuery } from '@tanstack/react-query'
import { queryKeys } from '@/config/queryKeys'
import type { Analysis, AnalysisStatus } from '@/types/analysis'
import { getAnalysis } from './analysisApi'

/** Statuses from which an analysis can still change on its own. */
const IN_FLIGHT: readonly AnalysisStatus[] = ['Uploaded', 'Analyzing', 'Reporting']

interface UseAnalysisOptions {
  /**
   * Poll while the pipeline is still working. Off by default because the stream
   * endpoint already pushes live updates; this is for views that are not streaming.
   */
  pollWhileRunning?: boolean
}

/**
 * A single analysis, including the full report once it has been generated.
 *
 * Disabled when no id is supplied, so it is safe to call before one is known.
 */
export function useAnalysis(
  analysisId: string | undefined,
  options: UseAnalysisOptions = {},
) {
  return useQuery<Analysis>({
    queryKey: queryKeys.analyses.detail(analysisId ?? ''),
    queryFn: ({ signal }) => getAnalysis(analysisId!, signal),
    enabled: Boolean(analysisId),
    refetchInterval: (query) => {
      if (!options.pollWhileRunning) return false

      const status = query.state.data?.status

      // Stop polling the moment it reaches a terminal state, rather than leaving a
      // timer running against a result that can no longer change.
      return status && IN_FLIGHT.includes(status) ? 3_000 : false
    },
  })
}
