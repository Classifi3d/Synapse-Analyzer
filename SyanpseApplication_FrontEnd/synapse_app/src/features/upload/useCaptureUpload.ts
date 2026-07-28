import { useCallback, useEffect, useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { describeApiError, isCancellation } from '@/config/axiosClient'
import { queryKeys } from '@/config/queryKeys'
import type { Analysis } from '@/types/analysis'
import { completeUpload, initiateUpload } from './uploadApi'
import { uploadParts, UploadError, type UploadProgress } from './uploadClient'

export type UploadStage = 'idle' | 'initiating' | 'uploading' | 'finalizing'

interface UploadVariables {
  file: File
  prompt?: string
}

export interface UploadFailure {
  message: string
  detail?: string
}

const IDLE_PROGRESS: UploadProgress = {
  percent: 0,
  bytesUploaded: 0,
  totalBytes: 0,
  partsCompleted: 0,
  totalParts: 0,
}

/**
 * Drives the whole upload as one operation: open the session, push the chunks to
 * storage, then have the API assemble them.
 *
 * The three steps are a single mutation rather than three chained ones because they
 * are not independently retryable - a session, its presigned urls and its ETags only
 * mean anything together.
 */
export function useCaptureUpload() {
  const queryClient = useQueryClient()

  const [stage, setStage] = useState<UploadStage>('idle')
  const [progress, setProgress] = useState<UploadProgress>(IDLE_PROGRESS)
  const abortRef = useRef<AbortController | null>(null)

  // A component unmounting mid-upload should not leave requests in flight.
  useEffect(() => () => abortRef.current?.abort(), [])

  const mutation = useMutation<Analysis, Error, UploadVariables>({
    mutationFn: async ({ file, prompt }) => {
      const controller = new AbortController()
      abortRef.current = controller

      setStage('initiating')
      setProgress({ ...IDLE_PROGRESS, totalBytes: file.size })

      const session = await initiateUpload(
        {
          fileName: file.name,
          contentType: file.type || 'application/vnd.tcpdump.pcap',
          fileSize: file.size,
        },
        controller.signal,
      )

      setStage('uploading')

      const parts = await uploadParts(
        file,
        session,
        setProgress,
        controller.signal,
      )

      setStage('finalizing')

      return completeUpload(
        {
          analysisId: session.analysisId,
          parts,
          ...(prompt?.trim() ? { prompt: prompt.trim() } : {}),
        },
        controller.signal,
      )
    },
    onSuccess: (analysis) => {
      queryClient.setQueryData(
        queryKeys.analyses.detail(analysis.analysisId),
        analysis,
      )

      void queryClient.invalidateQueries({ queryKey: queryKeys.analyses.lists() })
    },
    onSettled: () => {
      abortRef.current = null
      setStage('idle')
    },
  })

  const cancel = useCallback(() => abortRef.current?.abort(), [])

  const reset = useCallback(() => {
    mutation.reset()
    setProgress(IDLE_PROGRESS)
    setStage('idle')
  }, [mutation])

  return {
    upload: mutation.mutateAsync,
    cancel,
    reset,
    stage,
    progress,
    isUploading: mutation.isPending,
    error: mutation.error ? toUploadFailure(mutation.error) : null,
  }
}

function toUploadFailure(error: Error): UploadFailure {
  if (isCancellation(error)) return { message: 'Upload cancelled.' }

  if (error instanceof UploadError) {
    return error.detail
      ? { message: error.message, detail: error.detail }
      : { message: error.message }
  }

  return { message: describeApiError(error, 'The upload failed.') }
}
