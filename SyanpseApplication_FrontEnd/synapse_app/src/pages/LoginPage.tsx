import { useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Alert, Button, Card, Spinner } from 'react-bootstrap'
import { If, Then } from 'react-if'
import { LogIn, ShieldHalf } from 'lucide-react'
import { useAuth } from '@/features/auth/useAuth'

interface LocationState {
  from?: string
  reason?: string
}

export function LoginPage() {
  const { isAuthenticated, login } = useAuth()
  const location = useLocation()
  const [isRedirecting, setIsRedirecting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const state = (location.state ?? {}) as LocationState

  if (isAuthenticated) {
    return <Navigate to={state.from ?? '/'} replace />
  }

  const handleSignIn = async () => {
    setError(null)
    setIsRedirecting(true)

    try {
      await login()
    } catch (cause) {
      setIsRedirecting(false)
      setError(
        cause instanceof Error
          ? cause.message
          : 'The sign-in could not be started.',
      )
    }
  }

  return (
    <div className="centered-page">
      <Card className="surface-card auth-card">
        <Card.Body className="p-4 p-md-5 text-center">
          <ShieldHalf size={44} className="text-primary mb-3" aria-hidden />

          <h1 className="h4 fw-semibold mb-2">Synapse Analyzer</h1>
          <p className="text-body-secondary small mb-4">
            Packet capture threat analysis. Sign in with Cerberus SSO to continue.
          </p>

          <If condition={Boolean(state.reason)}>
            <Then>
              <Alert variant="warning" className="text-start small py-2">
                {state.reason}
              </Alert>
            </Then>
          </If>

          <If condition={error !== null}>
            <Then>
              <Alert variant="danger" className="text-start small py-2">
                {error}
              </Alert>
            </Then>
          </If>

          <Button
            variant="primary"
            className="w-100 d-flex align-items-center justify-content-center gap-2"
            onClick={() => void handleSignIn()}
            disabled={isRedirecting}
          >
            <If condition={isRedirecting}>
              <Then>
                <Spinner animation="border" size="sm" aria-hidden />
              </Then>
            </If>
            <If condition={!isRedirecting}>
              <Then>
                <LogIn size={17} aria-hidden />
              </Then>
            </If>
            {isRedirecting ? 'Redirecting…' : 'Sign in with Cerberus'}
          </Button>
        </Card.Body>
      </Card>
    </div>
  )
}
