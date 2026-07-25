import json
import logging
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterator

from .config import settings
from .models import Logs, PortUsage, Summary, Talker, ValueCount

logger = logging.getLogger(__name__)

# Logs returned to the API. Zeek emits many more; these are the ones the report relies on.
LOG_FILES = ["conn", "dns", "http", "ssl", "files", "notice", "weird", "x509"]


def _iter_rows(path: Path) -> Iterator[dict[str, Any]]:
    """
    Yields one dict per line. With LogAscii::use_json=T each line is a standalone JSON
    object, so the file can be streamed instead of loaded whole - captures can produce
    conn.log files of many gigabytes.
    """

    with path.open("r", encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()

            if not line:
                continue

            try:
                yield json.loads(line)
            except json.JSONDecodeError:
                logger.warning("Skipping malformed line in %s", path.name)


def _to_datetime(value: Any) -> datetime | None:
    """Zeek writes timestamps as epoch seconds when emitting JSON."""

    if value is None:
        return None

    try:
        return datetime.fromtimestamp(float(value), tz=timezone.utc)
    except (TypeError, ValueError, OSError):
        return None


def parse_logs(workspace: Path) -> tuple[Summary, Logs, list[str]]:
    """
    Reads every log Zeek produced and returns aggregate counters, a capped sample of rows,
    and the names of logs that were sampled rather than returned whole.
    """

    summary = Summary()
    logs = Logs()
    truncated: list[str] = []

    counts: dict[str, int] = {}
    sources: set[str] = set()
    destinations: set[str] = set()
    talkers: Counter[tuple[str, str]] = Counter()
    talker_bytes: Counter[tuple[str, str]] = Counter()
    ports: Counter[tuple[int, str, str]] = Counter()
    dns_queries: Counter[str] = Counter()
    notice_types: Counter[str] = Counter()

    first_ts: float | None = None
    last_ts: float | None = None
    total_bytes = 0

    for name in LOG_FILES:
        path = workspace / f"{name}.log"

        if not path.exists():
            counts[name] = 0
            continue

        rows = 0
        sample: list[dict[str, Any]] = []

        for row in _iter_rows(path):
            rows += 1

            if len(sample) < settings.max_rows_per_log:
                sample.append(row)

            if name == "conn":
                source = str(row.get("id.orig_h", ""))
                destination = str(row.get("id.resp_h", ""))

                if source:
                    sources.add(source)
                if destination:
                    destinations.add(destination)

                if source and destination:
                    pair = (source, destination)
                    talkers[pair] += 1

                    payload = int(row.get("orig_bytes") or 0) + int(row.get("resp_bytes") or 0)
                    talker_bytes[pair] += payload
                    total_bytes += payload

                port = row.get("id.resp_p")
                if isinstance(port, int):
                    ports[(port, str(row.get("proto", "")), str(row.get("service") or ""))] += 1

                ts = row.get("ts")
                if isinstance(ts, (int, float)):
                    first_ts = ts if first_ts is None else min(first_ts, ts)
                    last_ts = ts if last_ts is None else max(last_ts, ts)

            elif name == "dns":
                query = row.get("query")
                if query:
                    dns_queries[str(query)] += 1

            elif name == "notice":
                note = row.get("note")
                if note:
                    notice_types[str(note)] += 1

        counts[name] = rows
        setattr(logs, name, sample)

        if rows > len(sample):
            truncated.append(f"{name}.log")

    summary.connections = counts.get("conn", 0)
    summary.dns_queries = counts.get("dns", 0)
    summary.http_requests = counts.get("http", 0)
    summary.tls_sessions = counts.get("ssl", 0)
    summary.transferred_files = counts.get("files", 0)
    summary.notices = counts.get("notice", 0)
    summary.weird = counts.get("weird", 0)
    summary.certificates = counts.get("x509", 0)

    summary.capture_start = _to_datetime(first_ts)
    summary.capture_end = _to_datetime(last_ts)

    summary.unique_source_ips = len(sources)
    summary.unique_destination_ips = len(destinations)
    summary.total_bytes = total_bytes

    summary.top_talkers = [
        Talker(
            source=source,
            destination=destination,
            connections=count,
            bytes=talker_bytes[(source, destination)],
        )
        for (source, destination), count in talkers.most_common(settings.top_n)
    ]

    summary.top_dns_queries = [
        ValueCount(value=query, count=count)
        for query, count in dns_queries.most_common(settings.top_n)
    ]

    summary.top_ports = [
        PortUsage(
            port=port,
            protocol=proto,
            service=service or None,
            connections=count,
        )
        for (port, proto, service), count in ports.most_common(settings.top_n)
    ]

    summary.notice_types = [note for note, _ in notice_types.most_common(settings.top_n)]

    return summary, logs, truncated
