import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './useAuth'

/**
 * Gate for the authenticated part of the app.
 *
 * This is a usability guard, not a security boundary: the API validates the token on
 * every request and is the only thing standing between a caller and the data.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
    return (
      <Navigate
        to="/login"
        replace
        state={{
          from: location.pathname + location.search,
          reason: 'Your session has ended. Sign in again to continue.',
        }}
      />
    )
  }

  return <>{children}</>
}
