import { useMutation } from '@tanstack/react-query'
import type {
  InitiateUploadRequest,
  InitiateUploadResponse,
} from '@/types/analysis'
import { initiateUpload } from './uploadApi'

/**
 * Opens an upload session: `POST /api/analysis/upload/initiate`.
 *
 * Returns the chunk size and one presigned url per part. Composed by
 * {@link useCaptureUpload}; exposed on its own so the step can be driven or tested
 * in isolation.
 */
export function useInitiateUpload() {
  return useMutation<InitiateUploadResponse, Error, InitiateUploadRequest>({
    mutationFn: (request) => initiateUpload(request),
  })
}
