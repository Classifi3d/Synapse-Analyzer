/**
 * Environment access in one place, validated at module load.
 *
 * A missing variable surfaces here as a clear startup error rather than as an
 * `undefined` spliced into a request url and a confusing 404 much later.
 */

function required(name: keyof ImportMetaEnv): string {
  const value = import.meta.env[name]

  if (typeof value !== 'string' || value.trim() === '') {
    throw new Error(
      `Missing required environment variable ${name}. Copy .env.example to .env and fill it in.`,
    )
  }

  return value.trim()
}

function stripTrailingSlash(value: string): string {
  return value.replace(/\/+$/, '')
}

export const env = {
  apiBaseUrl: stripTrailingSlash(required('VITE_API_BASE_URL')),
  sso: {
    baseUrl: stripTrailingSlash(required('VITE_SSO_BASE_URL')),
    clientId: required('VITE_SSO_CLIENT_ID'),
    redirectUri: required('VITE_SSO_REDIRECT_URI'),
    scope: import.meta.env.VITE_SSO_SCOPE?.trim() || 'openid profile email',
  },
} as const
