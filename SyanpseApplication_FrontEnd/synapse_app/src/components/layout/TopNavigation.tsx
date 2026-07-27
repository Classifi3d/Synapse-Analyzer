import { Button, Container, Navbar } from 'react-bootstrap'
import { If, Then, Else } from 'react-if'
import { LogOut, ShieldHalf } from 'lucide-react'
import { useAuth } from '@/features/auth/useAuth'
import { initialsOf } from '@/utils/format'

export function TopNavigation() {
  const { user, isAuthenticated, login, logout } = useAuth()

  return (
    <Navbar as="header" className="app-navbar border-bottom py-2 flex-shrink-0">
      <Container fluid className="px-3 px-lg-4">
        <Navbar.Brand className="d-flex align-items-center gap-2 fw-semibold mb-0">
          <ShieldHalf size={22} className="text-primary" aria-hidden />
          <span>Synapse Analyzer</span>
        </Navbar.Brand>

        <div className="ms-auto d-flex align-items-center gap-3">
          <If condition={isAuthenticated}>
            <Then>
              <span className="text-body-secondary small d-none d-sm-inline">
                {user?.displayName}
              </span>

              <span
                className="avatar-circle"
                title={user?.email || user?.displayName}
                aria-hidden
              >
                {initialsOf(user?.displayName ?? '')}
              </span>

              <Button
                variant="outline-secondary"
                size="sm"
                onClick={logout}
                className="d-flex align-items-center gap-2"
              >
                <LogOut size={15} aria-hidden />
                <span className="d-none d-md-inline">Sign out</span>
              </Button>
            </Then>
            <Else>
              <Button variant="primary" size="sm" onClick={() => void login()}>
                Sign in
              </Button>
            </Else>
          </If>
        </div>
      </Container>
    </Navbar>
  )
}
