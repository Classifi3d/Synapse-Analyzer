import { Badge } from 'react-bootstrap'
import { ShieldAlert, ShieldCheck, ShieldQuestion } from 'lucide-react'

/**
 * The severity words the prompt asks the model to emit on its `VERDICT:` line.
 * `VerdictParser` on the API stores the verdict as `LEVEL - justification`.
 */
const SEVERITIES = ['NONE', 'LOW', 'MEDIUM', 'HIGH', 'CRITICAL'] as const

type Severity = (typeof SEVERITIES)[number]

const SEVERITY_VARIANT: Record<Severity, string> = {
  NONE: 'success',
  LOW: 'info',
  MEDIUM: 'warning',
  HIGH: 'danger',
  CRITICAL: 'danger',
}

interface ThreatBadgeProps {
  isThreatDetected: boolean | null
  verdict: string | null
}

export function ThreatBadge({ isThreatDetected, verdict }: ThreatBadgeProps) {
  // Null is genuinely different from "safe": the analysis has not reached a verdict.
  // The previous UI rendered these the same way and claimed the capture was clean.
  if (isThreatDetected === null) {
    return (
      <Badge bg="secondary" className="verdict-badge">
        <ShieldQuestion size={15} aria-hidden />
        Awaiting verdict
      </Badge>
    )
  }

  const severity = extractSeverity(verdict)
  const variant = severity
    ? SEVERITY_VARIANT[severity]
    : isThreatDetected
      ? 'danger'
      : 'success'

  const label =
    severity && severity !== 'NONE'
      ? `${titleCase(severity)} severity`
      : isThreatDetected
        ? 'Threat indicators found'
        : 'No threat indicators'

  // A bare `VERDICT: NONE` leaves nothing to say beyond the badge itself.
  const justification = verdict ? stripSeverity(verdict) : ''

  return (
    <div className="d-flex flex-column gap-2">
      <Badge bg={variant} className="verdict-badge">
        {isThreatDetected ? (
          <ShieldAlert size={15} aria-hidden />
        ) : (
          <ShieldCheck size={15} aria-hidden />
        )}
        {label}
      </Badge>

      {justification && (
        <p className="mb-0 small text-body-secondary">{justification}</p>
      )}
    </div>
  )
}

function extractSeverity(verdict: string | null): Severity | null {
  if (!verdict) return null

  const head = verdict.trim().split(/[\s-]/, 1)[0]?.toUpperCase()

  return SEVERITIES.find((level) => level === head) ?? null
}

/** Drops the leading `LEVEL - ` so the badge and the text do not repeat each other. */
function stripSeverity(verdict: string): string {
  return verdict.replace(/^\s*(NONE|LOW|MEDIUM|HIGH|CRITICAL)\s*[-–:]?\s*/i, '').trim()
}

function titleCase(value: string): string {
  return value.charAt(0) + value.slice(1).toLowerCase()
}
