import { useCallback, useEffect, useRef, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { describeApiError } from '@/config/axiosClient'
import type { Analysis } from '@/types/analysis'
import {
  completeUpload,
  initiateUpload,
  uploadParts,
  UploadError,
  type UploadProgress,
} from './uploadClient'

export type UploadStage = 'idle' | 'initiating' | 'uploading' | 'finalizing'

interface UploadVariables {
  file: File
  prompt?: string
}

const IDLE_PROGRESS: UploadProgress = {
  percent: 0,
  bytesUploaded: 0,
  totalBytes: 0,
  partsCompleted: 0,
  totalParts: 0,
}

/**
 * Drives the three-step upload as one operation: open the session, push the parts
 * to storage, then have the API assemble them.
 */
export function useCaptureUpload() {
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
    onSettled: () => {
      abortRef.current = null
      setStage('idle')
    },
  })

  const cancel = useCallback(() => {
    abortRef.current?.abort()
  }, [])

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
    error: mutation.error ? toUploadMessage(mutation.error) : null,
  }
}

interface UploadMessage {
  message: string
  detail?: string
}

function toUploadMessage(error: Error): UploadMessage {
  if (error instanceof DOMException && error.name === 'AbortError') {
    return { message: 'Upload cancelled.' }
  }

  if (error instanceof UploadError) {
    return error.detail
      ? { message: error.message, detail: error.detail }
      : { message: error.message }
  }

  return { message: describeApiError(error, 'The upload failed.') }
}
