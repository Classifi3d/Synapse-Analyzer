import { Card, Col, Row, Table } from 'react-bootstrap'
import { If, Then } from 'react-if'
import type { ZeekSummary } from '@/types/analysis'
import { formatBytes, formatDateTime, formatNumber } from '@/utils/format'

/** Counters worth showing at a glance, in the order an analyst tends to read them. */
function headlineStats(summary: ZeekSummary) {
  return [
    { label: 'Connections', value: summary.connections },
    { label: 'DNS queries', value: summary.dnsQueries },
    { label: 'HTTP requests', value: summary.httpRequests },
    { label: 'TLS sessions', value: summary.tlsSessions },
    { label: 'Files', value: summary.transferredFiles },
    { label: 'Notices', value: summary.notices },
  ]
}

export function ZeekSummaryPanel({ summary }: { summary: ZeekSummary }) {
  const stats = headlineStats(summary)
  const hasCaptureWindow = Boolean(summary.captureStart || summary.captureEnd)

  return (
    <Card className="surface-card mb-3">
      <Card.Header className="d-flex align-items-center justify-content-between flex-wrap gap-2">
        <span className="fw-semibold small text-uppercase tracking-wide">
          Capture summary
        </span>

        <span className="text-body-secondary x-small">
          {formatNumber(summary.uniqueSourceIps)} sources ·{' '}
          {formatNumber(summary.uniqueDestinationIps)} destinations ·{' '}
          {formatBytes(summary.totalBytes)}
        </span>
      </Card.Header>

      <Card.Body className="pb-2">
        <Row className="g-2 mb-3">
          {stats.map((stat) => (
            <Col xs={6} md={4} lg={2} key={stat.label}>
              <div className="stat-tile">
                <div className="stat-value">{formatNumber(stat.value)}</div>
                <div className="stat-label">{stat.label}</div>
              </div>
            </Col>
          ))}
        </Row>

        <If condition={hasCaptureWindow}>
          <Then>
            <p className="text-body-secondary x-small mb-3">
              Capture window: {formatDateTime(summary.captureStart)} →{' '}
              {formatDateTime(summary.captureEnd)}
            </p>
          </Then>
        </If>

        <Row className="g-3">
          <If condition={summary.topTalkers.length > 0}>
            <Then>
              <Col lg={6}>
                <SubTable
                  title="Top talkers"
                  head={['Source', 'Destination', 'Conns', 'Bytes']}
                  rows={summary.topTalkers.map((talker) => [
                    talker.source,
                    talker.destination,
                    formatNumber(talker.connections),
                    formatBytes(talker.bytes),
                  ])}
                />
              </Col>
            </Then>
          </If>

          <If condition={summary.topPorts.length > 0}>
            <Then>
              <Col lg={6}>
                <SubTable
                  title="Top ports"
                  head={['Port', 'Proto', 'Service', 'Conns']}
                  rows={summary.topPorts.map((port) => [
                    String(port.port),
                    port.protocol,
                    port.service ?? '—',
                    formatNumber(port.connections),
                  ])}
                />
              </Col>
            </Then>
          </If>

          <If condition={summary.topDnsQueries.length > 0}>
            <Then>
              <Col lg={6}>
                <SubTable
                  title="Top DNS queries"
                  head={['Query', 'Count']}
                  rows={summary.topDnsQueries.map((query) => [
                    query.value,
                    formatNumber(query.count),
                  ])}
                />
              </Col>
            </Then>
          </If>

          <If condition={summary.noticeTypes.length > 0}>
            <Then>
              <Col lg={6}>
                <div className="sub-table-title">Notice types</div>
                <div className="d-flex flex-wrap gap-1">
                  {summary.noticeTypes.map((notice) => (
                    <span key={notice} className="notice-chip">
                      {notice}
                    </span>
                  ))}
                </div>
              </Col>
            </Then>
          </If>
        </Row>
      </Card.Body>
    </Card>
  )
}

interface SubTableProps {
  title: string
  head: string[]
  rows: string[][]
}

function SubTable({ title, head, rows }: SubTableProps) {
  return (
    <>
      <div className="sub-table-title">{title}</div>

      {/* Wrapped so long IP pairs scroll inside the card instead of widening it. */}
      <div className="table-responsive">
        <Table size="sm" borderless className="sub-table mb-0">
          <thead>
            <tr>
              {head.map((cell) => (
                <th key={cell}>{cell}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={index}>
                {row.map((cell, cellIndex) => (
                  <td key={cellIndex} title={cell}>
                    {cell}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </Table>
      </div>
    </>
  )
}
