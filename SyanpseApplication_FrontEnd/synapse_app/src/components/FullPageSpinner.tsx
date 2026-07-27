import { Spinner } from 'react-bootstrap'

export function FullPageSpinner({ label }: { label: string }) {
  return (
    <div className="centered-page">
      <div className="text-center" role="status" aria-live="polite">
        <Spinner animation="border" variant="primary" />
        <p className="text-body-secondary small mt-3 mb-0">{label}</p>
      </div>
    </div>
  )
}
