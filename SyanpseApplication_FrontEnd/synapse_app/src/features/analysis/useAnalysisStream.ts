import { useCallback, useEffect, useRef, useState } from 'react'
import type { ZeekSummary } from '@/types/analysis'
import {
  openAnalysisStream,
  type AnalysisStreamSubscription,
} from './analysisStream'

export interface AnalysisStreamState {
  isStreaming: boolean
  /** `zeek` while the capture is processed, `reporting` while the model writes. */
  stage: string | null
  stageMessage: string | null
  summary: ZeekSummary | null
  /** The report so far, as markdown. */
  report: string
  verdict: string | null
  isThreatDetected: boolean | null
  error: string | null
}

const INITIAL_STATE: AnalysisStreamState = {
  isStreaming: false,
  stage: null,
  stageMessage: null,
  summary: null,
  report: '',
  verdict: null,
  isThreatDetected: null,
  error: null,
}

/**
 * Consumes one analysis stream at a time.
 *
 * Tokens arrive far faster than the screen refreshes, so they are buffered and
 * flushed once per animation frame. Setting state per token would re-render the
 * whole transcript hundreds of times a second for no visible benefit.
 */
export function useAnalysisStream() {
  const [state, setState] = useState<AnalysisStreamState>(INITIAL_STATE)

  const subscriptionRef = useRef<AnalysisStreamSubscription | null>(null)
  const bufferRef = useRef('')
  const frameRef = useRef<number | null>(null)

  const flush = useCallback(() => {
    frameRef.current = null

    if (!bufferRef.current) return

    const pending = bufferRef.current
    bufferRef.current = ''

    setState((previous) => ({ ...previous, report: previous.report + pending }))
  }, [])

  const scheduleFlush = useCallback(() => {
    if (frameRef.current !== null) return
    frameRef.current = requestAnimationFrame(flush)
  }, [flush])

  const teardown = useCallback(() => {
    subscriptionRef.current?.close()
    subscriptionRef.current = null

    if (frameRef.current !== null) {
      cancelAnimationFrame(frameRef.current)
      frameRef.current = null
    }
  }, [])

  useEffect(() => teardown, [teardown])

  const stop = useCallback(() => {
    teardown()
    flush()
    setState((previous) => ({ ...previous, isStreaming: false }))
  }, [teardown, flush])

  const start = useCallback(
    (analysisId: string, prompt?: string) => {
      // Starting a second stream while one is open would interleave two reports.
      teardown()

      bufferRef.current = ''
      setState({ ...INITIAL_STATE, isStreaming: true })

      subscriptionRef.current = openAnalysisStream(analysisId, prompt, {
        onStatus: ({ stage, message }) =>
          setState((previous) => ({
            ...previous,
            stage,
            stageMessage: message,
          })),

        onSummary: (summary) =>
          setState((previous) => ({ ...previous, summary })),

        onToken: ({ text }) => {
          bufferRef.current += text
          scheduleFlush()
        },

        onDone: ({ isThreatDetected, verdict }) => {
          flush()
          setState((previous) => ({
            ...previous,
            isStreaming: false,
            stage: null,
            stageMessage: null,
            isThreatDetected,
            verdict,
          }))
        },

        onError: ({ message }) => {
          flush()
          setState((previous) => ({
            ...previous,
            isStreaming: false,
            stage: null,
            stageMessage: null,
            error: message,
          }))
        },
      })
    },
    [teardown, flush, scheduleFlush],
  )

  const reset = useCallback(() => {
    teardown()
    bufferRef.current = ''
    setState(INITIAL_STATE)
  }, [teardown])

  return { ...state, start, stop, reset }
}
