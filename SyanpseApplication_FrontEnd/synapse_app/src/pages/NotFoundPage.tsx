import { Link } from 'react-router-dom'
import { Card } from 'react-bootstrap'

export function NotFoundPage() {
  return (
    <div className="centered-page">
      <Card className="surface-card auth-card">
        <Card.Body className="p-4 text-center">
          <h1 className="h5 fw-semibold mb-2">Page not found</h1>
          <p className="text-body-secondary small mb-3">
            That address does not match anything in Synapse Analyzer.
          </p>
          <Link to="/" className="btn btn-primary w-100">
            Back to the workspace
          </Link>
        </Card.Body>
      </Card>
    </div>
  )
}
