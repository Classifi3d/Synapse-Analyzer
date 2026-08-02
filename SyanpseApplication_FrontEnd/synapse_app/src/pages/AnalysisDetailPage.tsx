import { useEffect, useRef } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, Spinner } from 'react-bootstrap'
import { ArrowLeft, FileArchive, Play, RotateCcw } from 'lucide-react'
import { describeApiError } from '@/config/axiosClient'
import { queryKeys } from '@/config/queryKeys'
import { AnalysisStatusBadge } from '@/features/analysis/AnalysisStatusBadge'
import { ReportView } from '@/features/analysis/ReportView'
import { StageIndicator } from '@/features/analysis/StageIndicator'
import { ThreatBadge } from '@/features/analysis/ThreatBadge'
import { ZeekSummaryPanel } from '@/features/analysis/ZeekSummaryPanel'
import { useAnalysis } from '@/features/analysis/useAnalysis'
import { useAnalysisStream } from '@/features/analysis/useAnalysisStream'
import type { Analysis } from '@/types/analysis'
import { formatBytes, formatDateTime } from '@/utils/format'

/**
 * One stored analysis, in full, with the ability to run it.
 *
 * Reads from the database rather than the stream, so this is the analysis after the
 * fact - the same report, on any device and after a reload.
 *
 * It can also start the pipeline. That matters because leaving this page cancels the
 * stream: the server stops work and puts the status back, which would otherwise leave
 * an interrupted analysis with no way forward short of uploading the capture again.
 * Zeek output is cached on the record, so resuming re-runs only the generation.
 */
export function AnalysisDetailPage() {
  const { analysisId } = useParams<{ analysisId: string }>()
  const queryClient = useQueryClient()

  const { data, isPending, isError, error } = useAnalysis(analysisId, {
    pollWhileRunning: true,
  })

  const stream = useAnalysisStream()
  const wasStreaming = useRef(false)

  // When a run finishes, the stored record is now the better source: it has the
  // verdict the server parsed and persisted. Refetch rather than trusting the
  // stream's own final frame.
  useEffect(() => {
    if (wasStreaming.current && !stream.isStreaming && analysisId) {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.analyses.detail(analysisId),
      })
      void queryClient.invalidateQueries({ queryKey: queryKeys.analyses.lists() })
    }

    wasStreaming.current = stream.isStreaming
  }, [stream.isStreaming, analysisId, queryClient])

  return (
    <div className="page-scroll">
      <div className="page-column">
        <Link
          to="/analyses"
          className="d-inline-flex align-items-center gap-2 small mb-3 back-link"
        >
          <ArrowLeft size={15} aria-hidden />
          All analyses
        </Link>

        {isPending ? (
          <div className="text-center py-5">
            <Spinner animation="border" variant="primary" />
          </div>
        ) : isError || !data ? (
          <Alert variant="danger">
            <Alert.Heading as="h6">Could not load this analysis</Alert.Heading>
            <p className="mb-0 small">
              {describeApiError(error, 'The request failed.')}
            </p>
          </Alert>
        ) : (
          <AnalysisDetail
            analysis={data}
            stream={stream}
            onRun={() => stream.start(data.analysisId, data.prompt ?? undefined)}
          />
        )}
      </div>
    </div>
  )
}

interface AnalysisDetailProps {
  analysis: Analysis
  stream: ReturnType<typeof useAnalysisStream>
  onRun: () => void
}

function AnalysisDetail({ analysis, stream, onRun }: AnalysisDetailProps) {
  const storedReport = analysis.report?.trim() ?? ''
  const liveReport = stream.report.trim()

  // While a run is in flight the stream is ahead of the database, so it wins. Once it
  // settles the refetched record takes over again.
  const showLive = stream.isStreaming || (liveReport.length > 0 && !storedReport)
  const report = showLive ? stream.report : analysis.report
  const summary = showLive ? (stream.summary ?? analysis.zeekSummary) : analysis.zeekSummary

  const canRun =
    !stream.isStreaming &&
    analysis.status !== 'AwaitingUpload' &&
    analysis.status !== 'Analyzing' &&
    analysis.status !== 'Reporting'

  const runLabel =
    analysis.status === 'Failed'
      ? 'Try again'
      : storedReport
        ? 'Run again'
        : 'Run analysis'

  return (
    <>
      <header className="mb-4">
        <div className="d-flex align-items-center gap-2 mb-2">
          <FileArchive size={20} className="text-primary flex-shrink-0" aria-hidden />
          <h1 className="h5 mb-0 text-break">{analysis.fileName}</h1>
        </div>

        <div className="d-flex align-items-center flex-wrap gap-3 small text-body-secondary">
          <AnalysisStatusBadge status={analysis.status} />
          <span>{formatBytes(analysis.fileSizeBytes)}</span>
          <span>Submitted {formatDateTime(analysis.createdAt)}</span>
          {analysis.completedAt && (
            <span>Completed {formatDateTime(analysis.completedAt)}</span>
          )}

          {canRun && (
            <Button
              variant={storedReport ? 'outline-secondary' : 'primary'}
              size="sm"
              onClick={onRun}
              className="d-flex align-items-center gap-2 ms-auto"
            >
              {storedReport ? <RotateCcw size={15} aria-hidden /> : <Play size={15} aria-hidden />}
              {runLabel}
            </Button>
          )}
        </div>
      </header>

      {analysis.prompt?.trim() && (
        <Card className="surface-card mb-3">
          <Card.Body className="p-3">
            <div className="section-label mb-1">Analyst request</div>
            <p className="mb-0 request-prompt">{analysis.prompt}</p>
          </Card.Body>
        </Card>
      )}

      {/* The stream's error is about this run; the record's is about the last one. */}
      {(stream.error ?? analysis.errorMessage) && (
        <Alert variant="danger">
          <Alert.Heading as="h6" className="mb-1">
            Analysis failed
          </Alert.Heading>
          <p className="mb-0 small">{stream.error ?? analysis.errorMessage}</p>
        </Alert>
      )}

      {summary && <ZeekSummaryPanel summary={summary} />}

      <StageIndicator stage={stream.stage} message={stream.stageMessage} />

      {report?.trim() ? (
        <Card className="surface-card">
          <Card.Body className="p-3 p-md-4">
            <ReportView markdown={report} />

            {stream.isStreaming && <span className="streaming-caret" aria-hidden />}

            {!stream.isStreaming && analysis.isThreatDetected !== null && (
              <div className="mt-3 pt-3 border-top">
                <ThreatBadge
                  isThreatDetected={analysis.isThreatDetected}
                  verdict={analysis.verdict}
                />
              </div>
            )}
          </Card.Body>
        </Card>
      ) : (
        <NoReport
          isRunning={
            stream.isStreaming ||
            analysis.status === 'Analyzing' ||
            analysis.status === 'Reporting'
          }
          hasError={Boolean(stream.error ?? analysis.errorMessage)}
        />
      )}
    </>
  )
}

function NoReport({
  isRunning,
  hasError,
}: {
  isRunning: boolean
  hasError: boolean
}) {
  if (isRunning) {
    return (
      <Card className="surface-card">
        <Card.Body className="d-flex align-items-center gap-3 py-4">
          <Spinner animation="border" size="sm" variant="primary" />
          <span className="small text-body-secondary">
            This analysis is running. The report will appear here as it is written.
          </span>
        </Card.Body>
      </Card>
    )
  }

  // The error alert above already explains this case.
  if (hasError) return null

  return (
    <Card className="surface-card">
      <Card.Body className="py-4 text-center text-body-secondary small">
        No report has been generated for this capture yet.
      </Card.Body>
    </Card>
  )
}
