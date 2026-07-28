import { apiClient } from '@/config/axiosClient'
import type {
  Analysis,
  CompleteUploadRequest,
  InitiateUploadRequest,
  InitiateUploadResponse,
} from '@/types/analysis'

/**
 * Endpoint functions for the two-step upload session.
 *
 * The chunks themselves do not go through here - they are PUT straight to object
 * storage from `uploadClient`. These two calls only open and close the session.
 */

export async function initiateUpload(
  request: InitiateUploadRequest,
  signal?: AbortSignal,
): Promise<InitiateUploadResponse> {
  const { data } = await apiClient.post<InitiateUploadResponse>(
    '/analysis/upload/initiate',
    request,
    { signal },
  )

  return data
}

export async function completeUpload(
  request: CompleteUploadRequest,
  signal?: AbortSignal,
): Promise<Analysis> {
  const { data } = await apiClient.post<Analysis>(
    '/analysis/upload/complete',
    request,
    { signal },
  )

  return data
}
