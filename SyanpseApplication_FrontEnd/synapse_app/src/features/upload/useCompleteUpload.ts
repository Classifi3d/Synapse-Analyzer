import { useMutation, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/config/queryKeys'
import type { Analysis, CompleteUploadRequest } from '@/types/analysis'
import { completeUpload } from './uploadApi'

/**
 * Assembles the uploaded parts: `POST /api/analysis/upload/complete`.
 *
 * On success the new analysis is seeded into the detail cache and the list is
 * invalidated, so any view showing either is correct without an extra round trip.
 */
export function useCompleteUpload() {
  const queryClient = useQueryClient()

  return useMutation<Analysis, Error, CompleteUploadRequest>({
    mutationFn: (request) => completeUpload(request),
    onSuccess: (analysis) => {
      queryClient.setQueryData(
        queryKeys.analyses.detail(analysis.analysisId),
        analysis,
      )

      void queryClient.invalidateQueries({ queryKey: queryKeys.analyses.lists() })
    },
  })
}
