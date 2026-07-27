import axios, { AxiosError } from 'axios'
import { env } from './env'
import { emitSessionExpired } from '@/features/auth/sessionEvents'
import { tokenStorage } from '@/features/auth/tokenStorage'

export const axiosClient = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 60_000,
  headers: { Accept: 'application/json' },
})

axiosClient.interceptors.request.use((config) => {
  const token = tokenStorage.getAccessToken()

  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

axiosClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    // 401 means the token was rejected. 403 does not - the caller is authenticated
    // but not allowed - so it must not clear the session.
    if (error.response?.status === 401) {
      tokenStorage.clear()
      emitSessionExpired()
    }

    return Promise.reject(error)
  },
)

/**
 * Turns an axios failure into something worth showing a user.
 *
 * The API returns RFC 7807 problem details, where the useful text is in `detail`
 * with `title` as the fallback.
 */
export function describeApiError(error: unknown, fallback: string): string {
  if (!axios.isAxiosError(error)) {
    return error instanceof Error ? error.message : fallback
  }

  if (error.code === 'ECONNABORTED') {
    return 'The request timed out.'
  }

  if (!error.response) {
    return `Could not reach the API at ${env.apiBaseUrl}. Check that it is running.`
  }

  const data = error.response.data as
    | { detail?: string; title?: string; error?: string }
    | string
    | undefined

  if (typeof data === 'string' && data.trim()) return data

  if (data && typeof data === 'object') {
    const message = data.detail || data.title || data.error
    if (message) return message
  }

  return `${fallback} (HTTP ${error.response.status})`
}
