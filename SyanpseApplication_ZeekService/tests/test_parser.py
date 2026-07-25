import json
from pathlib import Path

from app.config import settings
from app.parser import parse_logs


def write_log(workspace: Path, name: str, rows: list[dict]) -> None:
    """Writes newline-delimited JSON, which is what Zeek emits with LogAscii::use_json=T."""

    path = workspace / f"{name}.log"
    path.write_text("\n".join(json.dumps(row) for row in rows) + "\n", encoding="utf-8")


def test_parses_counts_and_aggregates(tmp_path: Path) -> None:
    write_log(
        tmp_path,
        "conn",
        [
            {
                "ts": 1700000000.0,
                "id.orig_h": "10.0.0.5",
                "id.resp_h": "93.184.216.34",
                "id.resp_p": 443,
                "proto": "tcp",
                "service": "ssl",
                "orig_bytes": 100,
                "resp_bytes": 900,
            },
            {
                "ts": 1700000060.0,
                "id.orig_h": "10.0.0.5",
                "id.resp_h": "93.184.216.34",
                "id.resp_p": 443,
                "proto": "tcp",
                "service": "ssl",
                "orig_bytes": 50,
                "resp_bytes": 150,
            },
            {
                "ts": 1700000120.0,
                "id.orig_h": "10.0.0.9",
                "id.resp_h": "8.8.8.8",
                "id.resp_p": 53,
                "proto": "udp",
                "service": "dns",
                "orig_bytes": 40,
                "resp_bytes": 60,
            },
        ],
    )
    write_log(
        tmp_path,
        "dns",
        [
            {"ts": 1700000120.0, "query": "example.com"},
            {"ts": 1700000121.0, "query": "example.com"},
            {"ts": 1700000122.0, "query": "evil.test"},
        ],
    )
    write_log(tmp_path, "notice", [{"note": "Scan::Port_Scan"}])

    summary, logs, truncated = parse_logs(tmp_path)

    assert summary.connections == 3
    assert summary.dns_queries == 3
    assert summary.notices == 1
    assert summary.http_requests == 0

    assert summary.unique_source_ips == 2
    assert summary.unique_destination_ips == 2
    assert summary.total_bytes == 1300

    # Ordered by connection count, so the repeated pair comes first.
    assert summary.top_talkers[0].source == "10.0.0.5"
    assert summary.top_talkers[0].connections == 2
    assert summary.top_talkers[0].bytes == 1200

    assert summary.top_dns_queries[0].value == "example.com"
    assert summary.top_dns_queries[0].count == 2

    assert summary.top_ports[0].port == 443
    assert summary.top_ports[0].service == "ssl"

    assert summary.notice_types == ["Scan::Port_Scan"]

    assert summary.capture_start is not None
    assert summary.capture_end is not None
    assert summary.capture_start < summary.capture_end

    assert len(logs.conn) == 3
    assert truncated == []


def test_samples_rows_beyond_the_cap(tmp_path: Path) -> None:
    rows = [
        {
            "ts": 1700000000.0 + index,
            "id.orig_h": "10.0.0.1",
            "id.resp_h": f"10.0.1.{index % 250}",
            "id.resp_p": 80,
            "proto": "tcp",
            "orig_bytes": 1,
            "resp_bytes": 1,
        }
        for index in range(settings.max_rows_per_log + 25)
    ]
    write_log(tmp_path, "conn", rows)

    summary, logs, truncated = parse_logs(tmp_path)

    # Counters cover every row; only the returned sample is capped.
    assert summary.connections == len(rows)
    assert len(logs.conn) == settings.max_rows_per_log
    assert truncated == ["conn.log"]


def test_missing_logs_are_zeroed(tmp_path: Path) -> None:
    summary, logs, truncated = parse_logs(tmp_path)

    assert summary.connections == 0
    assert summary.capture_start is None
    assert logs.conn == []
    assert truncated == []


def test_malformed_lines_are_skipped(tmp_path: Path) -> None:
    path = tmp_path / "dns.log"
    path.write_text(
        '{"query": "good.test"}\nnot json at all\n{"query": "also-good.test"}\n',
        encoding="utf-8",
    )

    summary, logs, _ = parse_logs(tmp_path)

    assert summary.dns_queries == 2
    assert len(logs.dns) == 2


def test_handles_missing_byte_counts(tmp_path: Path) -> None:
    # Zeek writes null for byte counts on connections it never saw complete.
    write_log(
        tmp_path,
        "conn",
        [
            {
                "ts": 1700000000.0,
                "id.orig_h": "10.0.0.1",
                "id.resp_h": "10.0.0.2",
                "id.resp_p": 22,
                "proto": "tcp",
                "orig_bytes": None,
                "resp_bytes": None,
            }
        ],
    )

    summary, _, _ = parse_logs(tmp_path)

    assert summary.connections == 1
    assert summary.total_bytes == 0
