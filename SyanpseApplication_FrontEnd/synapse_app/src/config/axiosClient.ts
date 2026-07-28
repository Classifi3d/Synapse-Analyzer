import axios, { AxiosError, AxiosHeaders } from 'axios'
import { env } from './env'
import { emitSessionExpired } from '@/features/auth/sessionEvents'
import { tokenStorage } from '@/features/auth/tokenStorage'

/**
 * The app's HTTP layer.
 *
 * Three instances rather than one, because they talk to three different parties and
 * must not share behaviour:
 *
 *   apiClient     - the Synapse API. Carries the bearer token and reacts to 401.
 *   ssoClient     - Cerberus. Form-encoded, and deliberately *without* the bearer
 *                   interceptor: during the code exchange there is no token yet.
 *   storageClient - presigned object-storage urls. No interceptors at all.
 *
 * The split matters most for storageClient. A presigned url points at a third-party
 * host, and any interceptor that attaches `Authorization` would send the user's access
 * token there. It also must not add headers of its own - see putToPresignedUrl.
 */

// ---- Synapse API ----------------------------------------------------------

export const apiClient = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 60_000,
  headers: { Accept: 'application/json' },
})

apiClient.interceptors.request.use((config) => {
  const token = tokenStorage.getAccessToken()

  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }

  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    // 401 means the token was rejected. 403 does not - the caller is authenticated but
    // not permitted - so it must not tear down the session.
    if (error.response?.status === 401) {
      tokenStorage.clear()
      emitSessionExpired()
    }

    return Promise.reject(error)
  },
)

// ---- Cerberus SSO ---------------------------------------------------------

/**
 * The token endpoint is form-bound (`[FromForm]`), so requests are
 * `application/x-www-form-urlencoded` rather than JSON. Passing a `URLSearchParams`
 * body makes axios set that content type itself.
 */
export const ssoClient = axios.create({
  baseURL: env.sso.baseUrl,
  timeout: 20_000,
})

// ---- Object storage -------------------------------------------------------

/** Bare instance: absolute presigned urls, no auth, no shared defaults. */
export const storageClient = axios.create({
  // Long enough for a single chunk on a slow link; the whole upload is many of these.
  timeout: 0,
})

export interface PresignedPutResult {
  /** The ETag exactly as storage returned it, quotes included. */
  eTag: string | null
  status: number
}

/**
 * Uploads one chunk to a presigned url.
 *
 * The url is signed without a `Content-Type`, so sending one changes the signed header
 * set and storage rejects the request. Axios would otherwise derive the header from the
 * body, hence the explicit null - `null` removes a header rather than sending "null".
 */
export async function putToPresignedUrl(
  url: string,
  body: Blob,
  options: {
    signal?: AbortSignal
    onProgress?: (loadedBytes: number) => void
  } = {},
): Promise<PresignedPutResult> {
  const response = await storageClient.put(url, body, {
    headers: { 'Content-Type': null },
    signal: options.signal,
    onUploadProgress: (event) => options.onProgress?.(event.loaded),
  })

  const headers = response.headers

  // Cross-origin responses only expose headers the server allow-lists, so this can be
  // null even on success. Callers must treat that as a failure, not an empty string.
  const eTag =
    headers instanceof AxiosHeaders
      ? (headers.get('etag') as string | null)
      : ((headers as Record<string, string>)['etag'] ?? null)

  return { eTag: eTag ?? null, status: response.status }
}

// ---- Errors ---------------------------------------------------------------

/** True when a request was aborted deliberately rather than failing. */
export function isCancellation(error: unknown): boolean {
  return (
    axios.isCancel(error) ||
    (error instanceof DOMException && error.name === 'AbortError')
  )
}

/**
 * Turns a request failure into something worth showing a user.
 *
 * The API returns RFC 7807 problem details, where the useful text is `detail` with
 * `title` as the fallback. Cerberus returns `{error, error_description}` per RFC 6749.
 */
export function describeApiError(error: unknown, fallback: string): string {
  if (isCancellation(error)) return 'The request was cancelled.'

  if (!axios.isAxiosError(error)) {
    return error instanceof Error ? error.message : fallback
  }

  if (error.code === 'ECONNABORTED') return 'The request timed out.'

  if (!error.response) {
    const target = error.config?.baseURL ?? error.config?.url ?? 'the server'
    return `Could not reach ${target}. Check that it is running and allows this origin.`
  }

  const data = error.response.data as
    | {
        detail?: string
        title?: string
        error?: string
        error_description?: string
      }
    | string
    | undefined

  if (typeof data === 'string' && data.trim()) return data.trim()

  if (data && typeof data === 'object') {
    const message =
      data.detail || data.error_description || data.title || data.error
    if (message) return message
  }

  return `${fallback} (HTTP ${error.response.status})`
}
