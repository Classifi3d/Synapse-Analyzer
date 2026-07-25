from datetime import datetime
from typing import Any
from uuid import UUID

from pydantic import BaseModel, Field


class AnalyzeRequest(BaseModel):
    """
    Coordinates of a capture to analyze. The capture itself is never posted to this service -
    it is fetched from object storage using the presigned url.
    """

    analysis_id: UUID
    bucket_name: str
    object_key: str
    download_url: str


class Talker(BaseModel):
    source: str = ""
    destination: str = ""
    connections: int = 0
    bytes: int = 0


class ValueCount(BaseModel):
    value: str = ""
    count: int = 0


class PortUsage(BaseModel):
    port: int = 0
    protocol: str = ""
    service: str | None = None
    connections: int = 0


class Summary(BaseModel):
    connections: int = 0
    dns_queries: int = 0
    http_requests: int = 0
    tls_sessions: int = 0
    transferred_files: int = 0
    notices: int = 0
    weird: int = 0
    certificates: int = 0

    capture_start: datetime | None = None
    capture_end: datetime | None = None

    unique_source_ips: int = 0
    unique_destination_ips: int = 0
    total_bytes: int = 0

    top_talkers: list[Talker] = Field(default_factory=list)
    top_dns_queries: list[ValueCount] = Field(default_factory=list)
    top_ports: list[PortUsage] = Field(default_factory=list)
    notice_types: list[str] = Field(default_factory=list)


class Logs(BaseModel):
    conn: list[dict[str, Any]] = Field(default_factory=list)
    dns: list[dict[str, Any]] = Field(default_factory=list)
    http: list[dict[str, Any]] = Field(default_factory=list)
    ssl: list[dict[str, Any]] = Field(default_factory=list)
    files: list[dict[str, Any]] = Field(default_factory=list)
    notice: list[dict[str, Any]] = Field(default_factory=list)
    weird: list[dict[str, Any]] = Field(default_factory=list)
    x509: list[dict[str, Any]] = Field(default_factory=list)


class AnalyzeResponse(BaseModel):
    success: bool
    error: str | None = None
    duration_seconds: float = 0.0
    summary: Summary = Field(default_factory=Summary)
    logs: Logs = Field(default_factory=Logs)
    truncated_logs: list[str] = Field(default_factory=list)
