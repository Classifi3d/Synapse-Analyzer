import type { AccessTokenClaims, AuthUser, TokenResponse } from '@/types/auth'

/**
 * Session persistence.
 *
 * Tokens live in `localStorage` so a reload keeps the session. That is readable by
 * any script on the origin, which is the accepted trade-off for a token-based SPA;
 * the mitigation that matters is keeping the access token short-lived (Cerberus
 * issues 15-minute tokens) rather than trying to hide it from same-origin script.
 *
 * The PKCE verifier and the state value go in `sessionStorage` instead: they are
 * single-use, scoped to one login attempt, and must not outlive the tab.
 */

const ACCESS_TOKEN_KEY = 'synapse.access_token'
const REFRESH_TOKEN_KEY = 'synapse.refresh_token'
const PKCE_VERIFIER_KEY = 'synapse.pkce_verifier'
const STATE_KEY = 'synapse.oauth_state'
const RETURN_TO_KEY = 'synapse.return_to'

export const tokenStorage = {
  getAccessToken: () => localStorage.getItem(ACCESS_TOKEN_KEY),
  getRefreshToken: () => localStorage.getItem(REFRESH_TOKEN_KEY),

  save(tokens: TokenResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, tokens.access_token)

    if (tokens.refresh_token) {
      localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refresh_token)
    }
  },

  clear(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
  },
}

export const loginAttemptStorage = {
  getVerifier: () => sessionStorage.getItem(PKCE_VERIFIER_KEY),
  getState: () => sessionStorage.getItem(STATE_KEY),
  getReturnTo: () => sessionStorage.getItem(RETURN_TO_KEY),

  begin(verifier: string, state: string, returnTo: string): void {
    sessionStorage.setItem(PKCE_VERIFIER_KEY, verifier)
    sessionStorage.setItem(STATE_KEY, state)
    sessionStorage.setItem(RETURN_TO_KEY, returnTo)
  },

  clear(): void {
    sessionStorage.removeItem(PKCE_VERIFIER_KEY)
    sessionStorage.removeItem(STATE_KEY)
    sessionStorage.removeItem(RETURN_TO_KEY)
  },
}

/**
 * Reads the claims out of a JWT without verifying the signature.
 *
 * Verification is the API's job; the UI only needs a display name and the expiry,
 * and it cannot verify an RS256 signature without the provider's public key anyway.
 * Nothing security-relevant is decided from this.
 */
export function decodeAccessToken(token: string): AccessTokenClaims | null {
  const payload = token.split('.')[1]

  if (!payload) return null

  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')

    // Round-trips through percent-encoding so non-ASCII claims survive intact.
    const json = decodeURIComponent(
      Array.from(atob(padded), (char) =>
        `%${char.charCodeAt(0).toString(16).padStart(2, '0')}`,
      ).join(''),
    )

    return JSON.parse(json) as AccessTokenClaims
  } catch {
    return null
  }
}

/** True when the token is absent, unreadable, or past its expiry. */
export function isTokenExpired(token: string | null, skewSeconds = 30): boolean {
  if (!token) return true

  const claims = decodeAccessToken(token)

  // No exp means we cannot reason about it; let the API be the judge.
  if (!claims?.exp) return false

  return claims.exp * 1000 <= Date.now() + skewSeconds * 1000
}

export function toAuthUser(claims: AccessTokenClaims): AuthUser {
  const email = claims.email ?? ''

  const displayName =
    claims.name ??
    claims.preferred_username ??
    (email ? email.split('@')[0]! : `Analyst ${claims.sub.slice(0, 8)}`)

  return { id: claims.sub, displayName, email }
}
