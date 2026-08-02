import { useMemo } from 'react'
import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

/**
 * The machine-readable verdict line the prompt asks the model to end with. The API
 * parses it into `isThreatDetected` and `verdict`, which the UI shows as a badge, so
 * leaving it in the prose repeats the same sentence twice under its own heading.
 */
const VERDICT_LINE = /^[ \t]*VERDICT:.*$/gim

/**
 * Renders the model's markdown report.
 *
 * `react-markdown` builds a React tree rather than injecting HTML, so untrusted
 * model output cannot introduce script or markup. GFM is enabled because the prompt
 * asks for tables.
 */
export function ReportView({ markdown }: { markdown: string }) {
  // Stripped at render time rather than on the way into storage: the raw report is
  // the record of what the model actually said, and the verdict line is the evidence
  // that it followed the format.
  const body = useMemo(
    () => markdown.replace(VERDICT_LINE, '').trimEnd(),
    [markdown],
  )

  return (
    <div className="report-body">
      <Markdown
        remarkPlugins={[remarkGfm]}
        components={{
          // Tables need their own scroll container or a wide one pushes the layout out.
          table: ({ children }) => (
            <div className="table-responsive">
              <table className="table table-sm table-bordered">{children}</table>
            </div>
          ),
          a: ({ children, href }) => (
            <a href={href} target="_blank" rel="noopener noreferrer nofollow">
              {children}
            </a>
          ),
        }}
      >
        {body}
      </Markdown>
    </div>
  )
}
