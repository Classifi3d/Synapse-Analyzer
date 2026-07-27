import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import type { AuthContextValue, AuthUser, TokenResponse } from '@/types/auth'
import { AuthContext } from './AuthContext'
import { beginAuthorization, clearSession } from './oauth'
import { onSessionExpired } from './sessionEvents'
import {
  decodeAccessToken,
  isTokenExpired,
  toAuthUser,
  tokenStorage,
} from './tokenStorage'

interface SessionState {
  user: AuthUser | null
  accessToken: string | null
}

const EMPTY_SESSION: SessionState = { user: null, accessToken: null }

/** Rebuilds the session from a stored token, discarding it if it is unusable. */
function restoreSession(): SessionState {
  const token = tokenStorage.getAccessToken()

  if (!token || isTokenExpired(token)) {
    // An expired token is worse than none: every request would 401 and bounce the
    // user mid-navigation instead of at the door.
    if (token) tokenStorage.clear()
    return EMPTY_SESSION
  }

  const claims = decodeAccessToken(token)

  if (!claims?.sub) {
    tokenStorage.clear()
    return EMPTY_SESSION
  }

  return { user: toAuthUser(claims), accessToken: token }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  // Reading storage is synchronous, so the session is known on the very first
  // render. Doing this in an effect instead would render one frame as signed-out
  // and bounce an already-authenticated user to the login screen.
  const [session, setSession] = useState<SessionState>(restoreSession)

  const logout = useCallback(() => {
    clearSession()
    setSession(EMPTY_SESSION)
  }, [])

  // A 401 from the API means the token is gone or no longer accepted; drop the
  // session so the router sends the user back to the login screen.
  useEffect(() => onSessionExpired(logout), [logout])

  // Expire the session in place when the token runs out while the tab is open,
  // rather than waiting for the next request to fail.
  useEffect(() => {
    const claims = session.accessToken
      ? decodeAccessToken(session.accessToken)
      : null

    if (!claims?.exp) return

    // Clamped rather than called inline: a token that expired between the restore
    // and this effect still has to go through the timer, because calling setState
    // synchronously here would cascade an extra render.
    const msRemaining = Math.max(claims.exp * 1000 - Date.now(), 0)

    const timer = window.setTimeout(logout, msRemaining)
    return () => window.clearTimeout(timer)
  }, [session.accessToken, logout])

  const completeLogin = useCallback((tokens: TokenResponse) => {
    tokenStorage.save(tokens)

    const claims = decodeAccessToken(tokens.access_token)

    if (!claims?.sub) {
      // The API derives the user id from `sub`; without it nothing downstream works.
      clearSession()
      throw new Error('The access token contains no subject claim.')
    }

    setSession({ user: toAuthUser(claims), accessToken: tokens.access_token })
  }, [])

  const login = useCallback(async () => {
    await beginAuthorization(window.location.pathname + window.location.search)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session.user,
      accessToken: session.accessToken,
      isAuthenticated: session.user !== null,
      login,
      logout,
      completeLogin,
    }),
    [session, login, logout, completeLogin],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
