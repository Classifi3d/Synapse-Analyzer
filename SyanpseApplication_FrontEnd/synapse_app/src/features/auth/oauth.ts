import { describeApiError, ssoClient } from '@/config/axiosClient'
import { env } from '@/config/env'
import type { TokenResponse } from '@/types/auth'
import { createCodeChallenge, createCodeVerifier, createState } from './pkce'
import { loginAttemptStorage, tokenStorage } from './tokenStorage'

/**
 * Authorization code flow with PKCE against Cerberus SSO.
 *
 * The flow is public-client: there is no client secret, because a secret shipped to
 * a browser is not a secret. Proof of possession comes from the PKCE verifier.
 */

const AUTHORIZE_ENDPOINT = '/OAuth/authorize'
const TOKEN_ENDPOINT = '/OAuth/token'

export class OAuthError extends Error {
  readonly detail: string | undefined

  constructor(message: string, detail?: string) {
    super(message)
    this.name = 'OAuthError'
    this.detail = detail
  }
}

/**
 * Starts the flow by navigating to the provider. Never returns: the browser leaves
 * the page.
 */
export async function beginAuthorization(returnTo: string): Promise<void> {
  const verifier = createCodeVerifier()
  const challenge = await createCodeChallenge(verifier)
  const state = createState()

  loginAttemptStorage.begin(verifier, state, returnTo)

  const params = new URLSearchParams({
    client_id: env.sso.clientId,
    redirect_uri: env.sso.redirectUri,
    response_type: 'code',
    scope: env.sso.scope,
    state,
    code_challenge: challenge,
    code_challenge_method: 'S256',
  })

  window.location.assign(
    `${env.sso.baseUrl}${AUTHORIZE_ENDPOINT}?${params.toString()}`,
  )
}

/**
 * Exchanges the authorization code for tokens.
 *
 * The provider's token endpoint is form-bound, so the body is
 * `application/x-www-form-urlencoded` rather than JSON.
 */
export async function exchangeCodeForTokens(
  code: string,
  returnedState: string | null,
): Promise<TokenResponse> {
  const expectedState = loginAttemptStorage.getState()
  const verifier = loginAttemptStorage.getVerifier()

  // Both checks guard the same thing from different angles: that this callback
  // belongs to a login *this* tab started.
  if (!verifier) {
    throw new OAuthError(
      'No login is in progress.',
      'The PKCE verifier is missing, which happens when the callback is opened directly or in a different tab. Start the sign-in again.',
    )
  }

  if (!expectedState || returnedState !== expectedState) {
    loginAttemptStorage.clear()

    throw new OAuthError(
      'The sign-in response could not be verified.',
      'The state parameter did not match the one sent with the request. The login was abandoned as a precaution.',
    )
  }

  // URLSearchParams rather than a plain object: axios serializes it as
  // application/x-www-form-urlencoded and sets the content type, which is what the
  // form-bound token endpoint expects.
  const body = new URLSearchParams({
    grant_type: 'authorization_code',
    code,
    redirect_uri: env.sso.redirectUri,
    client_id: env.sso.clientId,
    code_verifier: verifier,
  })

  let tokens: Partial<TokenResponse>

  try {
    const response = await ssoClient.post<Partial<TokenResponse>>(
      TOKEN_ENDPOINT,
      body,
    )

    tokens = response.data
  } catch (cause) {
    // describeApiError already pulls `error_description` out of the RFC 6749 error
    // body, which is where Cerberus explains a rejected grant.
    throw new OAuthError(
      'The SSO service rejected the sign-in.',
      describeApiError(cause, 'The token exchange failed.'),
    )
  } finally {
    // The verifier is single-use either way.
    loginAttemptStorage.clear()
  }

  if (!tokens.access_token) {
    throw new OAuthError(
      'The SSO service returned no access token.',
      'The token response was well-formed but contained no access_token.',
    )
  }

  return tokens as TokenResponse
}

/** Drops local session state. Cerberus exposes no end-session endpoint to call. */
export function clearSession(): void {
  tokenStorage.clear()
  loginAttemptStorage.clear()
}
