import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Alert, Button, Card } from 'react-bootstrap'
import { ShieldAlert } from 'lucide-react'
import { FullPageSpinner } from '@/components/FullPageSpinner'
import { exchangeCodeForTokens, OAuthError } from '@/features/auth/oauth'
import { loginAttemptStorage } from '@/features/auth/tokenStorage'
import { useAuth } from '@/features/auth/useAuth'

interface CallbackFailure {
  message: string
  detail?: string
}

/**
 * Lands the OAuth redirect: swaps the authorization code for tokens, then sends the
 * user on to wherever they were headed.
 */
export function CallbackPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const { completeLogin } = useAuth()

  const [failure, setFailure] = useState<CallbackFailure | null>(null)

  // The code is single-use. StrictMode double-invokes effects in development, and a
  // second exchange would fail against a code the provider has already consumed.
  const hasStarted = useRef(false)

  useEffect(() => {
    if (hasStarted.current) return
    hasStarted.current = true

    const run = async () => {
      // The provider reports its own failures as query parameters, not as an
      // error status on a response we ever see.
      const providerError = params.get('error')

      if (providerError) {
        setFailure({
          message: 'The SSO service refused the sign-in.',
          detail: params.get('error_description') ?? providerError,
        })
        return
      }

      const code = params.get('code')

      if (!code) {
        setFailure({
          message: 'The sign-in response was incomplete.',
          detail:
            'No authorization code was present in the callback url. Start the sign-in again.',
        })
        return
      }

      try {
        const tokens = await exchangeCodeForTokens(code, params.get('state'))
        const returnTo = loginAttemptStorage.getReturnTo()

        completeLogin(tokens)

        // replace: the callback url holds a spent code and should not be revisitable
        // through the back button.
        navigate(returnTo && returnTo !== '/auth/callback' ? returnTo : '/', {
          replace: true,
        })
      } catch (error) {
        setFailure(
          error instanceof OAuthError
            ? { message: error.message, detail: error.detail }
            : {
                message: 'The sign-in could not be completed.',
                detail: error instanceof Error ? error.message : undefined,
              },
        )
      }
    }

    void run()
  }, [params, navigate, completeLogin])

  if (!failure) {
    return <FullPageSpinner label="Completing sign-in…" />
  }

  return (
    <div className="centered-page">
      <Card className="surface-card auth-card">
        <Card.Body className="p-4">
          <div className="d-flex align-items-center gap-2 mb-3">
            <ShieldAlert size={20} className="text-danger" aria-hidden />
            <h1 className="h6 fw-semibold mb-0">Sign-in failed</h1>
          </div>

          <Alert variant="danger" className="small mb-3">
            <div className="fw-medium">{failure.message}</div>
            {failure.detail && (
              <div className="x-small mt-2">{failure.detail}</div>
            )}
          </Alert>

          <Button
            variant="primary"
            className="w-100"
            onClick={() => navigate('/login', { replace: true })}
          >
            Back to sign-in
          </Button>
        </Card.Body>
      </Card>
    </div>
  )
}
