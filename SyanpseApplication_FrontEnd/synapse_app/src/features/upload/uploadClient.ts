import { isCancellation, putToPresignedUrl } from '@/config/axiosClient'
import type {
  InitiateUploadResponse,
  PresignedUploadPart,
  CompletedPart,
} from '@/types/analysis'

/**
 * Multipart upload straight from the browser to object storage.
 *
 * The capture never passes through the API: it hands out one presigned PUT url per
 * part, the browser uploads the chunks, and the API only assembles them afterwards
 * from the returned ETags. That is what makes multi-gigabyte captures viable.
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

const MISSING_ETAG_DETAIL =
  "The chunk uploaded, but its ETag header is not exposed to the browser, so the parts cannot be assembled. Start MinIO with MINIO_API_CORS_ALLOW_ORIGIN set to this app's origin."

export interface UploadProgress {
  /** 0-100 across the whole file, not the current part. */
  percent: number
  bytesUploaded: number
  totalBytes: number
  partsCompleted: number
  totalParts: number
}

/** Uploads one part and returns its ETag. */
async function uploadPart(
  part: PresignedUploadPart,
  body: Blob,
  onProgress: (loadedBytes: number) => void,
  signal: AbortSignal,
): Promise<string> {
  const { eTag } = await putToPresignedUrl(part.uploadUrl, body, {
    signal,
    onProgress,
  })

  if (!eTag) {
    // The PUT succeeded but the ETag is unreadable, which means storage did not expose
    // it cross-origin. Without ETags the parts cannot be assembled, so this is fatal
    // and worth explaining precisely rather than failing as a generic upload error.
    throw new UploadError(
      'The storage service did not return a readable ETag.',
      MISSING_ETAG_DETAIL,
    )
  }

  return eTag
}

/**
 * Slices the file and uploads every part, reporting aggregate progress.
 *
 * Parts run a few at a time; a part that fails for a transient reason is retried from
 * its own slice, which is cheap because the slice is still local.
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
      // Capped: the final progress event of a part can momentarily overshoot its slice.
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

  const runPart = async (index: number): Promise<void> => {
    const part = parts[index]!
    const blob = sliceFor(part)

    for (let attempt = 1; attempt <= MAX_ATTEMPTS_PER_PART; attempt++) {
      try {
        eTags[index] = await uploadPart(
          part,
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
        // Cancellation and an unreadable ETag are both final; retrying either just
        // repeats the same outcome more slowly.
        if (isCancellation(error)) throw error
        if (error instanceof UploadError) throw error
        if (attempt === MAX_ATTEMPTS_PER_PART) throw error

        loadedByPart[index] = 0
        report()

        await new Promise((resolve) => setTimeout(resolve, 500 * attempt))
      }
    }
  }

  report()

  // A shared cursor hands every worker the next outstanding part, so one slow chunk
  // does not idle the others the way a fixed partition would.
  let cursor = 0

  const worker = async (): Promise<void> => {
    while (cursor < parts.length) {
      if (signal.aborted) throw new DOMException('Upload cancelled.', 'AbortError')
      await runPart(cursor++)
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
