import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Button, ProgressBar, Spinner } from 'react-bootstrap'
import { If, Then, Else } from 'react-if'
import { FileArchive, Send, ShieldHalf, X } from 'lucide-react'
import { PcapDropzone } from '@/features/upload/PcapDropzone'
import { useCaptureUpload } from '@/features/upload/useCaptureUpload'
import { formatBytes } from '@/utils/format'
import { AnalysisTurn, type TurnData } from './AnalysisTurn'
import { useAnalysisStream } from './useAnalysisStream'

interface PendingTurn {
  id: string
  fileName: string
  fileSizeBytes: number
  prompt: string
  createdAt: string
}

export function AnalysisWorkspace() {
  const [file, setFile] = useState<File | null>(null)
  const [prompt, setPrompt] = useState('')

  // Settled turns are snapshots; the newest one renders from live stream state.
  const [history, setHistory] = useState<TurnData[]>([])
  const [active, setActive] = useState<PendingTurn | null>(null)

  const upload = useCaptureUpload()
  const stream = useAnalysisStream()

  const scrollRef = useRef<HTMLDivElement>(null)

  const {
    summary,
    report,
    verdict,
    isThreatDetected,
    error: streamError,
    stage,
    stageMessage,
    isStreaming,
  } = stream

  const uploadFailure = upload.error

  // Memoized so `submit`, which closes over it, is not rebuilt on every render.
  const activeTurn = useMemo<TurnData | null>(
    () =>
      active && {
        ...active,
        summary,
        report,
        verdict,
        isThreatDetected,
        // An upload that never produced an analysis id fails before the stream
        // exists, so its message has to surface on the turn too.
        error: streamError ?? uploadFailure?.message ?? null,
        errorDetail: streamError ? null : (uploadFailure?.detail ?? null),
        stage,
        stageMessage,
        isStreaming,
      },
    [
      active,
      summary,
      report,
      verdict,
      isThreatDetected,
      streamError,
      uploadFailure,
      stage,
      stageMessage,
      isStreaming,
    ],
  )

  const isBusy = upload.isUploading || isStreaming

  // Follow the output as it grows, the way a terminal does.
  useEffect(() => {
    const container = scrollRef.current
    if (!container) return

    container.scrollTop = container.scrollHeight
  }, [report, stageMessage, history.length, active])

  const submit = useCallback(async () => {
    if (!file || isBusy) return

    // Retire the previous turn before the stream state is reset underneath it.
    if (activeTurn) {
      setHistory((previous) => [
        ...previous,
        { ...activeTurn, isStreaming: false, stage: null, stageMessage: null },
      ])
    }

    setActive({
      id: crypto.randomUUID(),
      fileName: file.name,
      fileSizeBytes: file.size,
      prompt,
      createdAt: new Date().toISOString(),
    })

    stream.reset()
    upload.reset()

    const submittedFile = file
    const submittedPrompt = prompt

    setFile(null)
    setPrompt('')

    try {
      const analysis = await upload.upload({
        file: submittedFile,
        prompt: submittedPrompt,
      })

      stream.start(analysis.analysisId, submittedPrompt)
    } catch {
      // useCaptureUpload has already converted this into a readable message, which
      // the active turn renders. Catching here only prevents an unhandled rejection.
    }
  }, [file, prompt, isBusy, activeTurn, upload, stream])

  const handleSubmit = useCallback(
    (event: FormEvent) => {
      event.preventDefault()
      void submit()
    },
    [submit],
  )

  const handleCancel = useCallback(() => {
    if (upload.isUploading) upload.cancel()
    if (isStreaming) stream.stop()
  }, [upload, stream, isStreaming])

  const isEmpty = history.length === 0 && activeTurn === null

  return (
    <div className="d-flex flex-column flex-grow-1 overflow-hidden min-h-0">
      <div ref={scrollRef} className="transcript-scroll flex-grow-1 overflow-auto">
        <div className="transcript-column">
          <If condition={isEmpty}>
            <Then>
              <EmptyState />
            </Then>
            <Else>
              <>
                {history.map((turn) => (
                  <AnalysisTurn key={turn.id} turn={turn} />
                ))}

                {activeTurn && <AnalysisTurn turn={activeTurn} />}
              </>
            </Else>
          </If>
        </div>
      </div>

      <div className="composer-bar flex-shrink-0">
        <form className="transcript-column" onSubmit={handleSubmit}>
          <If condition={upload.isUploading}>
            <Then>
              <UploadProgressBar
                stage={upload.stage}
                percent={upload.progress.percent}
                bytesUploaded={upload.progress.bytesUploaded}
                totalBytes={upload.progress.totalBytes}
                partsCompleted={upload.progress.partsCompleted}
                totalParts={upload.progress.totalParts}
              />
            </Then>
            <Else>
              <If condition={file !== null}>
                <Then>
                  <div className="attached-file">
                    <FileArchive size={16} className="text-primary" aria-hidden />
                    <span className="fw-medium text-truncate">{file?.name}</span>
                    <span className="text-body-secondary x-small flex-shrink-0">
                      {formatBytes(file?.size ?? 0)}
                    </span>
                    <Button
                      variant="link"
                      size="sm"
                      className="ms-auto p-0 text-body-secondary"
                      onClick={() => setFile(null)}
                      aria-label="Remove attached capture"
                    >
                      <X size={16} aria-hidden />
                    </Button>
                  </div>
                </Then>
                <Else>
                  <PcapDropzone onFileAccepted={setFile} disabled={isBusy} />
                </Else>
              </If>
            </Else>
          </If>

          <div className="composer-input mt-2">
            <textarea
              rows={2}
              className="form-control"
              placeholder="What should the analysis focus on? Leave empty for a general assessment."
              value={prompt}
              onChange={(event) => setPrompt(event.target.value)}
              disabled={isBusy}
              onKeyDown={(event) => {
                // Enter sends, Shift+Enter adds a line - the convention for this
                // shape of input.
                if (event.key === 'Enter' && !event.shiftKey) {
                  event.preventDefault()
                  void submit()
                }
              }}
            />

            <If condition={isBusy}>
              <Then>
                <Button
                  variant="outline-secondary"
                  className="composer-action"
                  onClick={handleCancel}
                  type="button"
                >
                  <Spinner animation="border" size="sm" aria-hidden />
                  <span className="d-none d-md-inline">Cancel</span>
                </Button>
              </Then>
              <Else>
                <Button
                  variant="primary"
                  type="submit"
                  className="composer-action"
                  disabled={file === null}
                  title={
                    file === null
                      ? 'Attach a capture to run an analysis'
                      : undefined
                  }
                >
                  <Send size={16} aria-hidden />
                  <span className="d-none d-md-inline">Analyze</span>
                </Button>
              </Else>
            </If>
          </div>

          <p className="composer-hint">
            The capture uploads straight to object storage; only its findings reach
            the model.
          </p>
        </form>
      </div>
    </div>
  )
}

function EmptyState() {
  return (
    <div className="empty-state">
      <ShieldHalf size={44} className="text-primary mb-3" aria-hidden />
      <h1 className="h3 fw-semibold mb-2">Network threat intelligence</h1>
      <p className="text-body-secondary mb-0">
        Attach a packet capture and Synapse will run it through Zeek, then have a
        local model write up what it found.
      </p>
    </div>
  )
}

interface UploadProgressBarProps {
  stage: string
  percent: number
  bytesUploaded: number
  totalBytes: number
  partsCompleted: number
  totalParts: number
}

function UploadProgressBar({
  stage,
  percent,
  bytesUploaded,
  totalBytes,
  partsCompleted,
  totalParts,
}: UploadProgressBarProps) {
  const label =
    stage === 'initiating'
      ? 'Opening upload session…'
      : stage === 'finalizing'
        ? 'Assembling parts…'
        : `Uploading ${formatBytes(bytesUploaded)} of ${formatBytes(totalBytes)}`

  // The first and last steps have no byte-level progress to report, so a
  // determinate bar would sit frozen instead of showing activity.
  const indeterminate = stage !== 'uploading'

  return (
    <div className="upload-progress">
      <div className="d-flex justify-content-between align-items-center mb-1">
        <span className="small">{label}</span>

        <If condition={!indeterminate && totalParts > 1}>
          <Then>
            <span className="x-small text-body-secondary">
              part {Math.min(partsCompleted + 1, totalParts)} of {totalParts}
            </span>
          </Then>
        </If>
      </div>

      <ProgressBar
        now={indeterminate ? 100 : percent}
        striped
        animated
        variant={indeterminate ? 'secondary' : 'primary'}
        label={indeterminate ? undefined : `${percent}%`}
        style={{ height: '0.65rem' }}
      />
    </div>
  )
}
