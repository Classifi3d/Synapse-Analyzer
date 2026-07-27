import { env } from '@/config/env'
import { tokenStorage } from '@/features/auth/tokenStorage'
import type {
  AnalysisStreamHandlers,
  DoneEventData,
  ErrorEventData,
  StatusEventData,
  TokenEventData,
  ZeekSummary,
} from '@/types/analysis'

/**
 * Subscribes to `GET /api/analysis/{id}/stream`.
 *
 * Two behaviours of `EventSource` shape this code:
 *
 * 1. It cannot set request headers, so the bearer token goes in the query string.
 *    The API opts into that explicitly (`OnMessageReceived` in
 *    AuthenticationExtensions.cs) for this endpoint only.
 *
 * 2. It reconnects automatically whenever the connection closes. The server closes
 *    the stream when the report is finished, so failing to close it here would
 *    silently re-run the entire analysis, over and over.
 */

export interface AnalysisStreamSubscription {
  close: () => void
}

function parse<T>(raw: string): T | null {
  try {
    return JSON.parse(raw) as T
  } catch {
    return null
  }
}

export function openAnalysisStream(
  analysisId: string,
  prompt: string | undefined,
  handlers: AnalysisStreamHandlers,
): AnalysisStreamSubscription {
  const params = new URLSearchParams()

  if (prompt?.trim()) params.set('prompt', prompt.trim())

  const token = tokenStorage.getAccessToken()
  if (token) params.set('access_token', token)

  const source = new EventSource(
    `${env.apiBaseUrl}/analysis/${analysisId}/stream?${params.toString()}`,
  )

  let finished = false

  const close = () => {
    finished = true
    source.close()
  }

  const on = <T>(name: string, handle: (data: T) => void) => {
    source.addEventListener(name, (event) => {
      const data = parse<T>((event as MessageEvent<string>).data)
      if (data) handle(data)
    })
  }

  on<StatusEventData>('status', (data) => handlers.onStatus?.(data))
  on<ZeekSummary>('summary', (data) => handlers.onSummary?.(data))
  on<TokenEventData>('token', (data) => handlers.onToken?.(data))

  source.addEventListener('done', (event) => {
    const data = parse<DoneEventData>((event as MessageEvent<string>).data)

    // Close before handing control back: the server ends the response here, and an
    // open EventSource would immediately reconnect and start a second run.
    close()

    if (data) handlers.onDone?.(data)
  })

  // Both the server's `event: error` frames and the browser's own transport errors
  // arrive on this listener. Only the former carries a payload.
  source.addEventListener('error', (event) => {
    const raw = (event as MessageEvent<string>).data

    if (typeof raw === 'string') {
      const data = parse<ErrorEventData>(raw)
      close()
      handlers.onError?.(data ?? { message: 'The analysis failed.' })
      return
    }

    // A transport error after completion is just the connection closing normally.
    if (finished) return

    close()

    handlers.onError?.({
      message:
        source.readyState === EventSource.CONNECTING
          ? 'The connection to the analysis stream was lost.'
          : 'The analysis stream could not be opened. Check that the API is running and that your session is still valid.',
    })
  })

  return { close }
}
