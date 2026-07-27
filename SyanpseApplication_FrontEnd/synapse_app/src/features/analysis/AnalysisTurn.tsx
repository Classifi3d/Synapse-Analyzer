import { Alert, Card } from 'react-bootstrap'
import { If, Then } from 'react-if'
import { FileArchive } from 'lucide-react'
import type { ZeekSummary } from '@/types/analysis'
import { formatBytes, formatTime } from '@/utils/format'
import { ReportView } from './ReportView'
import { StageIndicator } from './StageIndicator'
import { ThreatBadge } from './ThreatBadge'
import { ZeekSummaryPanel } from './ZeekSummaryPanel'

export interface TurnData {
  id: string
  fileName: string
  fileSizeBytes: number
  prompt: string
  createdAt: string
  summary: ZeekSummary | null
  report: string
  verdict: string | null
  isThreatDetected: boolean | null
  error: string | null
  /** Remediation hint shown under the error, when there is one worth giving. */
  errorDetail?: string | null
  /** Live progress text while the pipeline runs; null once it has settled. */
  stage: string | null
  stageMessage: string | null
  isStreaming: boolean
}

export function AnalysisTurn({ turn }: { turn: TurnData }) {
  const hasReport = turn.report.trim().length > 0

  return (
    <section className="analysis-turn">
      {/* ---- what the analyst asked ---- */}
      <div className="d-flex justify-content-end mb-3">
        <Card className="request-card">
          <Card.Body className="p-3">
            <div className="d-flex align-items-center gap-2 mb-2 file-chip">
              <FileArchive size={15} className="text-primary" aria-hidden />
              <span className="fw-medium text-truncate">{turn.fileName}</span>
              <span className="text-body-secondary x-small flex-shrink-0">
                {formatBytes(turn.fileSizeBytes)}
              </span>
            </div>

            <If condition={turn.prompt.trim().length > 0}>
              <Then>
                <p className="mb-0 request-prompt">{turn.prompt}</p>
              </Then>
            </If>
          </Card.Body>
        </Card>
      </div>

      {/* ---- what came back ---- */}
      <div className="response-block">
        <div className="text-body-secondary x-small mb-2">
          Synapse · {formatTime(turn.createdAt)}
        </div>

        <If condition={turn.summary !== null}>
          <Then>
            <ZeekSummaryPanel summary={turn.summary!} />
          </Then>
        </If>

        <StageIndicator stage={turn.stage} message={turn.stageMessage} />

        <If condition={hasReport}>
          <Then>
            <Card className="surface-card">
              <Card.Body className="p-3 p-md-4">
                <ReportView markdown={turn.report} />

                {/* The caret marks live generation; a settled report should not blink. */}
                <If condition={turn.isStreaming}>
                  <Then>
                    <span className="streaming-caret" aria-hidden />
                  </Then>
                </If>

                <If condition={!turn.isStreaming && turn.isThreatDetected !== null}>
                  <Then>
                    <div className="mt-3 pt-3 border-top">
                      <ThreatBadge
                        isThreatDetected={turn.isThreatDetected}
                        verdict={turn.verdict}
                      />
                    </div>
                  </Then>
                </If>
              </Card.Body>
            </Card>
          </Then>
        </If>

        <If condition={turn.error !== null}>
          <Then>
            <Alert variant="danger" className="mb-0 mt-3">
              <Alert.Heading as="h6" className="mb-1">
                Analysis failed
              </Alert.Heading>
              <p className="mb-0 small">{turn.error}</p>

              <If condition={Boolean(turn.errorDetail)}>
                <Then>
                  <hr className="my-2" />
                  <p className="mb-0 x-small">{turn.errorDetail}</p>
                </Then>
              </If>
            </Alert>
          </Then>
        </If>
      </div>
    </section>
  )
}
