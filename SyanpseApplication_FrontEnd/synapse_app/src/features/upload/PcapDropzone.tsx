import { useCallback, useMemo } from 'react'
import { useDropzone, type FileRejection } from 'react-dropzone'
import { If, Then, Else } from 'react-if'
import { FileUp, UploadCloud } from 'lucide-react'
import { formatBytes } from '@/utils/format'

/** Mirrors Analysis:MaxFileSizeBytes and Analysis:AllowedExtensions on the API. */
const MAX_FILE_SIZE = 10 * 1024 * 1024 * 1024
const ALLOWED_EXTENSIONS = ['.pcap', '.pcapng', '.cap']

interface PcapDropzoneProps {
  onFileAccepted: (file: File) => void
  disabled?: boolean
}

export function PcapDropzone({ onFileAccepted, disabled }: PcapDropzoneProps) {
  const onDrop = useCallback(
    (accepted: File[]) => {
      const file = accepted[0]
      if (file) onFileAccepted(file)
    },
    [onFileAccepted],
  )

  const { getRootProps, getInputProps, isDragActive, isDragReject, fileRejections } =
    useDropzone({
      onDrop,
      disabled,
      multiple: false,
      maxSize: MAX_FILE_SIZE,
      accept: {
        'application/vnd.tcpdump.pcap': ['.pcap', '.cap'],
        'application/x-pcapng': ['.pcapng'],
        // Most systems report captures as a generic binary, so without this fallback
        // valid files get rejected by their MIME type alone.
        'application/octet-stream': ALLOWED_EXTENSIONS,
      },
    })

  const rejectionMessage = useMemo(
    () => describeRejection(fileRejections),
    [fileRejections],
  )

  const stateClass = isDragReject
    ? 'is-invalid-drop'
    : isDragActive
      ? 'is-active-drop'
      : ''

  return (
    <div>
      <div
        {...getRootProps({
          className: `dropzone ${stateClass} ${disabled ? 'is-disabled' : ''}`,
        })}
      >
        <input {...getInputProps()} />

        <If condition={isDragActive}>
          <Then>
            <UploadCloud size={26} className="mb-2" aria-hidden />
            <span className="small fw-medium">
              {isDragReject
                ? 'That file type is not supported'
                : 'Drop the capture to attach it'}
            </span>
          </Then>
          <Else>
            <FileUp size={22} className="mb-2 text-body-secondary" aria-hidden />
            <span className="small">
              Drag a capture here, or{' '}
              <span className="dropzone-browse">browse</span>
            </span>
            <span className="text-body-secondary x-small mt-1">
              {ALLOWED_EXTENSIONS.join(', ')} · up to {formatBytes(MAX_FILE_SIZE, 0)}
            </span>
          </Else>
        </If>
      </div>

      <If condition={rejectionMessage !== null}>
        <Then>
          <div className="text-danger small mt-2" role="alert">
            {rejectionMessage}
          </div>
        </Then>
      </If>
    </div>
  )
}

function describeRejection(rejections: readonly FileRejection[]): string | null {
  const rejection = rejections[0]

  if (!rejection) return null

  const code = rejection.errors[0]?.code

  if (code === 'file-too-large') {
    return `${rejection.file.name} is ${formatBytes(rejection.file.size)}, over the ${formatBytes(MAX_FILE_SIZE, 0)} limit.`
  }

  if (code === 'too-many-files') {
    return 'Only one capture can be analyzed at a time.'
  }

  return `${rejection.file.name} is not a supported capture. Use ${ALLOWED_EXTENSIONS.join(', ')}.`
}
