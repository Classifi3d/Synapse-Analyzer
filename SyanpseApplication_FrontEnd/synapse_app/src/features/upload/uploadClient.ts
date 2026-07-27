import { axiosClient } from '@/config/axiosClient'
import type {
  CompleteUploadRequest,
  CompletedPart,
  InitiateUploadRequest,
  InitiateUploadResponse,
  Analysis,
  PresignedUploadPart,
} from '@/types/analysis'

/**
 * Multipart upload straight from the browser to MinIO.
 *
 * The capture never passes through the API: it hands out one presigned PUT url per
 * part, the browser uploads the chunks, and the API only assembles them afterwards
 * from the ETags. That is what makes multi-gigabyte captures viable.
 */

/** Enough to saturate a link without opening a socket per part on a 10 GB file. */
const MAX_CONCURRENT_PARTS = 4
const MAX_ATTEMPTS_PER_PART = 3

export class UploadError extends Error {
  readonly detail: string | undefined

  constructor(message: string, detail?: string) {
    super(message)
    this.name = 'UploadError'
    this.detail = detail
  }
}

export interface UploadProgress {
  /** 0-100 across the whole file, not the current part. */
  percent: number
  bytesUploaded: number
  totalBytes: number
  partsCompleted: number
  totalParts: number
}

export function initiateUpload(
  request: InitiateUploadRequest,
  signal?: AbortSignal,
): Promise<InitiateUploadResponse> {
  return axiosClient
    .post<InitiateUploadResponse>('/analysis/upload/initiate', request, { signal })
    .then((response) => response.data)
}

export function completeUpload(
  request: CompleteUploadRequest,
  signal?: AbortSignal,
): Promise<Analysis> {
  return axiosClient
    .post<Analysis>('/analysis/upload/complete', request, { signal })
    .then((response) => response.data)
}

/**
 * Uploads one part and returns its ETag.
 *
 * XHR rather than fetch for two reasons: fetch exposes no upload progress, and XHR
 * lets us send a body without any `Content-Type`. The url was signed without one,
 * so adding a header here would invalidate the signature.
 */
function putPart(
  url: string,
  body: Blob,
  onProgress: (loaded: number) => void,
  signal: AbortSignal,
): Promise<string> {
  return new Promise<string>((resolve, reject) => {
    const xhr = new XMLHttpRequest()

    const abort = () => xhr.abort()
    signal.addEventListener('abort', abort, { once: true })

    const settle = () => signal.removeEventListener('abort', abort)

    xhr.upload.addEventListener('progress', (event) => {
      if (event.lengthComputable) onProgress(event.loaded)
    })

    xhr.addEventListener('load', () => {
      settle()

      if (xhr.status < 200 || xhr.status >= 300) {
        reject(
          new UploadError(
            `Storage rejected a chunk (HTTP ${xhr.status}).`,
            xhr.responseText?.slice(0, 500) || undefined,
          ),
        )
        return
      }

      const eTag = xhr.getResponseHeader('ETag')

      if (!eTag) {
        // The PUT succeeded but the browser will not let us read the header, which
        // means the object storage did not expose it cross-origin. Without ETags the
        // parts cannot be assembled, so this is fatal and worth explaining precisely.
        reject(
          new UploadError(
            'The storage service did not return a readable ETag.',
            'The chunk uploaded, but its ETag header is not exposed to the browser, so the parts cannot be assembled. Start MinIO with MINIO_API_CORS_ALLOW_ORIGIN set to this app\'s origin.',
          ),
        )
        return
      }

      resolve(eTag)
    })

    xhr.addEventListener('error', () => {
      settle()
      reject(
        new UploadError(
          'The connection to the storage service failed.',
          'The browser blocked or lost the request. This is usually MinIO refusing the origin: set MINIO_API_CORS_ALLOW_ORIGIN on the MinIO container.',
        ),
      )
    })

    xhr.addEventListener('abort', () => {
      settle()
      reject(new DOMException('Upload cancelled.', 'AbortError'))
    })

    xhr.open('PUT', url, true)
    // Deliberately no setRequestHeader: see the note above.
    xhr.send(body)
  })
}

/**
 * Slices the file and uploads every part, reporting aggregate progress.
 *
 * Parts run a few at a time; a part that fails for a transient reason is retried
 * from its own slice, which is cheap because the slice is still local.
 */
export async function uploadParts(
  file: File,
  session: InitiateUploadResponse,
  onProgress: (progress: UploadProgress) => void,
  signal: AbortSignal,
): Promise<CompletedPart[]> {
  const parts = [...session.parts].sort((a, b) => a.partNumber - b.partNumber)
  const eTags = new Array<string | undefined>(parts.length)
  const loadedByPart = new Array<number>(parts.length).fill(0)

  let partsCompleted = 0

  const report = () => {
    const bytesUploaded = loadedByPart.reduce((sum, n) => sum + n, 0)

    onProgress({
      // Capped: the last progress event of a part can momentarily exceed its slice.
      percent: Math.min(100, Math.round((bytesUploaded / file.size) * 100)),
      bytesUploaded: Math.min(bytesUploaded, file.size),
      totalBytes: file.size,
      partsCompleted,
      totalParts: parts.length,
    })
  }

  const sliceFor = (part: PresignedUploadPart): Blob => {
    const start = (part.partNumber - 1) * session.partSizeBytes
    const end = Math.min(start + session.partSizeBytes, file.size)
    return file.slice(start, end)
  }

  const uploadOne = async (index: number): Promise<void> => {
    const part = parts[index]!
    const blob = sliceFor(part)

    for (let attempt = 1; attempt <= MAX_ATTEMPTS_PER_PART; attempt++) {
      try {
        eTags[index] = await putPart(
          part.uploadUrl,
          blob,
          (loaded) => {
            loadedByPart[index] = loaded
            report()
          },
          signal,
        )

        loadedByPart[index] = blob.size
        partsCompleted++
        report()
        return
      } catch (error) {
        // A cancellation and an unreadable ETag are both final; retrying either just
        // repeats the same outcome more slowly.
        if (error instanceof DOMException && error.name === 'AbortError') throw error
        if (attempt === MAX_ATTEMPTS_PER_PART) throw error
        if (error instanceof UploadError && error.message.includes('ETag')) throw error

        loadedByPart[index] = 0
        report()

        await new Promise((resolve) => setTimeout(resolve, 500 * attempt))
      }
    }
  }

  report()

  // A shared cursor gives every worker the next outstanding part, so one slow chunk
  // does not idle the others the way a fixed partition would.
  let cursor = 0

  const worker = async (): Promise<void> => {
    while (cursor < parts.length) {
      if (signal.aborted) throw new DOMException('Upload cancelled.', 'AbortError')
      await uploadOne(cursor++)
    }
  }

  await Promise.all(
    Array.from({ length: Math.min(MAX_CONCURRENT_PARTS, parts.length) }, worker),
  )

  return parts.map((part, index) => {
    const eTag = eTags[index]

    if (!eTag) {
      throw new UploadError(`Chunk ${part.partNumber} did not report an ETag.`)
    }

    return { partNumber: part.partNumber, eTag }
  })
}
