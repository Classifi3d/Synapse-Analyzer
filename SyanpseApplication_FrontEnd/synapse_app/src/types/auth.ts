/** Token payload returned by Cerberus `POST /OAuth/token`. */
export interface TokenResponse {
  access_token: string
  refresh_token: string
  token_type: string
  expires_in: number
}

/** The subset of JWT claims the UI reads. */
export interface AccessTokenClaims {
  sub: string
  client_id?: string
  exp?: number
  iss?: string
  aud?: string | string[]
  preferred_username?: string
  name?: string
  email?: string
}

export interface AuthUser {
  id: string
  /** Falls back to the email local part, then to a truncated subject id. */
  displayName: string
  email: string
}

export interface AuthContextValue {
  user: AuthUser | null
  accessToken: string | null
  isAuthenticated: boolean
  /** Redirects to Cerberus to begin the authorization code flow. */
  login: () => Promise<void>
  logout: () => void
  /** Called by the callback route once the code has been exchanged. */
  completeLogin: (tokens: TokenResponse) => void
}
