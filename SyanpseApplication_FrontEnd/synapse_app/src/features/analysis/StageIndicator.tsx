import { Spinner } from 'react-bootstrap'
import { Activity, Brain } from 'lucide-react'

/** The `stage` values emitted by `AnalysisStreamEvent.Status`. */
const STAGE_ICON: Record<string, typeof Activity> = {
  zeek: Activity,
  reporting: Brain,
}

interface StageIndicatorProps {
  stage: string | null
  message: string | null
}

export function StageIndicator({ stage, message }: StageIndicatorProps) {
  if (!message) return null

  const Icon = (stage && STAGE_ICON[stage]) || Activity

  return (
    <div className="stage-indicator" role="status" aria-live="polite">
      <Spinner animation="border" size="sm" variant="primary" />
      <Icon size={15} aria-hidden />
      <span>{message}</span>
    </div>
  )
}
