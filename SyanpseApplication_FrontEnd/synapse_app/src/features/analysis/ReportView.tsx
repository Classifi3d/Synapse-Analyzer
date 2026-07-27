import Markdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

/**
 * Renders the model's markdown report.
 *
 * `react-markdown` builds a React tree rather than injecting HTML, so untrusted
 * model output cannot introduce script or markup. GFM is enabled because the prompt
 * asks for tables.
 */
export function ReportView({ markdown }: { markdown: string }) {
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
        {markdown}
      </Markdown>
    </div>
  )
}
