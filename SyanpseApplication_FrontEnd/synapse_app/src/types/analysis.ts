/**
 * Mirrors the API's DTOs. The backend serializes with System.Text.Json web defaults,
 * so every property below is the camelCase form of the C# name.
 *
 * Source of truth: SyanpseApplication_API/Application/DTOs.
 */

/** `Domain.Entities.AnalysisStatus`, serialized by name via `Status.ToString()`. */
export type AnalysisStatus =
  | 'AwaitingUpload'
  | 'Uploaded'
  | 'Analyzing'
  | 'Reporting'
  | 'Completed'
  | 'Failed'

export interface ZeekTalker {
  source: string
  destination: string
  connections: number
  bytes: number
}

export interface ZeekCount {
  value: string
  count: number
}

export interface ZeekPort {
  port: number
  protocol: string
  service: string | null
  connections: number
}

export interface ZeekSummary {
  connections: number
  dnsQueries: number
  httpRequests: number
  tlsSessions: number
  transferredFiles: number
  notices: number
  weird: number
  certificates: number
  captureStart: string | null
  captureEnd: string | null
  uniqueSourceIps: number
  uniqueDestinationIps: number
  totalBytes: number
  topTalkers: ZeekTalker[]
  topDnsQueries: ZeekCount[]
  topPorts: ZeekPort[]
  noticeTypes: string[]
}

export interface Analysis {
  analysisId: string
  fileName: string
  fileSizeBytes: number
  status: AnalysisStatus
  createdAt: string
  uploadedAt: string | null
  completedAt: string | null
  prompt: string | null
  isThreatDetected: boolean | null
  verdict: string | null
  /** Omitted from list responses; only the detail endpoint populates it. */
  report: string | null
  errorMessage: string | null
  zeekSummary: ZeekSummary | null
}

// ---- Upload ---------------------------------------------------------------

export interface InitiateUploadRequest {
  fileName: string
  contentType?: string
  fileSize: number
}

export interface PresignedUploadPart {
  partNumber: number
  uploadUrl: string
}

export interface InitiateUploadResponse {
  analysisId: string
  uploadId: string
  bucketName: string
  objectKey: string
  /** Exact chunk size to slice the file into. The final part may be smaller. */
  partSizeBytes: number
  expiresAtUtc: string
  parts: PresignedUploadPart[]
}

export interface CompletedPart {
  partNumber: number
  /** The ETag exactly as MinIO returned it. The API strips the quotes. */
  eTag: string
}

export interface CompleteUploadRequest {
  analysisId: string
  parts: CompletedPart[]
  prompt?: string
}

// ---- Server-sent events ---------------------------------------------------

/**
 * The stream endpoint emits *named* SSE events, so each is delivered on its own
 * listener rather than through `onmessage`.
 *
 * Source of truth: Application/DTOs/AnalysisStreamEvent.cs.
 */
export interface StatusEventData {
  /** `zeek` while the capture is processed, `reporting` while the model generates. */
  stage: string
  message: string
}

export interface TokenEventData {
  text: string
}

export interface DoneEventData {
  analysisId: string
  isThreatDetected: boolean
  verdict: string | null
}

export interface ErrorEventData {
  message: string
}

export interface AnalysisStreamHandlers {
  onStatus?: (data: StatusEventData) => void
  onSummary?: (data: ZeekSummary) => void
  onToken?: (data: TokenEventData) => void
  onDone?: (data: DoneEventData) => void
  onError?: (data: ErrorEventData) => void
}
