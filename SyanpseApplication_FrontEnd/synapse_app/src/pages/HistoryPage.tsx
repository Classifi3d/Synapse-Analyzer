import { Link } from 'react-router-dom'
import { Alert, Button, Card, Spinner, Table } from 'react-bootstrap'
import { FileArchive, Inbox, RefreshCw } from 'lucide-react'
import { describeApiError } from '@/config/axiosClient'
import { AnalysisStatusBadge } from '@/features/analysis/AnalysisStatusBadge'
import { useAnalyses } from '@/features/analysis/useAnalyses'
import type { Analysis } from '@/types/analysis'
import { formatBytes, formatDateTime } from '@/utils/format'

/**
 * Every analysis the signed-in user has run.
 *
 * The workspace transcript only lives in memory, so without this page a reload loses
 * sight of work the server has kept. Verdict text is deliberately not shown here -
 * list responses omit the report body, and a verdict without its reasoning invites
 * being read as a conclusion.
 *
 * The states are early returns rather than `<If>` branches: `react-if` builds the JSX
 * of every branch before testing the condition, so a branch that dereferences the
 * loaded data would throw while it is still undefined.
 */
export function HistoryPage() {
  const { data, isPending, isError, error, refetch, isFetching } = useAnalyses()

  return (
    <div className="page-scroll">
      <div className="page-column">
        <header className="d-flex align-items-center justify-content-between mb-4 gap-3">
          <div>
            <h1 className="h4 mb-1">Analyses</h1>
            <p className="text-body-secondary small mb-0">
              Every capture you have submitted, newest first.
            </p>
          </div>

          <Button
            variant="outline-secondary"
            size="sm"
            onClick={() => void refetch()}
            disabled={isFetching}
            className="d-flex align-items-center gap-2 flex-shrink-0"
          >
            <RefreshCw size={15} aria-hidden />
            <span className="d-none d-sm-inline">Refresh</span>
          </Button>
        </header>

        <HistoryBody
          analyses={data}
          isPending={isPending}
          isError={isError}
          error={error}
        />
      </div>
    </div>
  )
}

interface HistoryBodyProps {
  analyses: Analysis[] | undefined
  isPending: boolean
  isError: boolean
  error: unknown
}

function HistoryBody({ analyses, isPending, isError, error }: HistoryBodyProps) {
  if (isPending) {
    return (
      <div className="text-center py-5">
        <Spinner animation="border" variant="primary" />
      </div>
    )
  }

  if (isError) {
    return (
      <Alert variant="danger">
        <Alert.Heading as="h6">Could not load your analyses</Alert.Heading>
        <p className="mb-0 small">{describeApiError(error, 'The request failed.')}</p>
      </Alert>
    )
  }

  if (!analyses || analyses.length === 0) {
    return (
      <Card className="surface-card">
        <Card.Body className="text-center py-5">
          <Inbox size={36} className="text-body-secondary mb-3" aria-hidden />
          <p className="mb-3">You have not analyzed a capture yet.</p>
          <Link to="/" className="btn btn-primary btn-sm">
            Analyze a capture
          </Link>
        </Card.Body>
      </Card>
    )
  }

  return (
    <Card className="surface-card">
      {/* Scrolls inside the card so a long file name cannot widen the page. */}
      <div className="table-responsive">
        <Table hover className="history-table align-middle mb-0">
          <thead>
            <tr>
              <th>Capture</th>
              <th>Status</th>
              <th className="d-none d-md-table-cell">Size</th>
              <th className="d-none d-sm-table-cell">Submitted</th>
            </tr>
          </thead>
          <tbody>
            {analyses.map((analysis) => (
              <tr key={analysis.analysisId}>
                <td>
                  <Link
                    to={`/analyses/${analysis.analysisId}`}
                    className="d-flex align-items-center gap-2 history-link"
                  >
                    <FileArchive
                      size={15}
                      className="text-primary flex-shrink-0"
                      aria-hidden
                    />
                    <span className="text-truncate">{analysis.fileName}</span>
                  </Link>
                </td>
                <td>
                  <AnalysisStatusBadge status={analysis.status} />
                </td>
                <td className="d-none d-md-table-cell text-body-secondary">
                  {formatBytes(analysis.fileSizeBytes)}
                </td>
                <td className="d-none d-sm-table-cell text-body-secondary">
                  {formatDateTime(analysis.createdAt)}
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      </div>
    </Card>
  )
}
