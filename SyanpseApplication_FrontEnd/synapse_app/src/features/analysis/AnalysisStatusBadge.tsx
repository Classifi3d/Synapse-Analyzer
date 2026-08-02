import { Badge, Spinner } from 'react-bootstrap'
import type { AnalysisStatus } from '@/types/analysis'

/**
 * Mirrors `Domain.Entities.AnalysisStatus`. Every value is listed explicitly so a new
 * status added on the server fails the type check here rather than rendering blank.
 */
const STATUS: Record<AnalysisStatus, { label: string; variant: string }> = {
  AwaitingUpload: { label: 'Awaiting upload', variant: 'secondary' },
  Uploaded: { label: 'Queued', variant: 'secondary' },
  Analyzing: { label: 'Running Zeek', variant: 'info' },
  Reporting: { label: 'Writing report', variant: 'info' },
  Completed: { label: 'Completed', variant: 'success' },
  Failed: { label: 'Failed', variant: 'danger' },
}

/** Statuses where the pipeline is still working, so the badge shows motion. */
const BUSY: readonly AnalysisStatus[] = ['Analyzing', 'Reporting']

export function AnalysisStatusBadge({ status }: { status: AnalysisStatus }) {
  const { label, variant } = STATUS[status] ?? {
    label: status,
    variant: 'secondary',
  }

  return (
    <Badge bg={variant} className="status-badge">
      {BUSY.includes(status) && (
        <Spinner animation="border" size="sm" className="status-spinner" aria-hidden />
      )}
      {label}
    </Badge>
  )
}
