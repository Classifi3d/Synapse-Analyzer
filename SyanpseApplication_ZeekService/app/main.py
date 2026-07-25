import asyncio
import logging
import shutil
import time
from pathlib import Path

from fastapi import FastAPI

from .config import settings
from .models import AnalyzeRequest, AnalyzeResponse
from .parser import parse_logs
from .storage import CaptureDownloadError, CaptureTooLargeError, download_capture
from .zeek_runner import ZeekExecutionError, run_zeek

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)-8s %(name)s: %(message)s",
)

logger = logging.getLogger(__name__)

app = FastAPI(
    title="Synapse Zeek Analysis Service",
    description=(
        "Runs Zeek against packet captures and returns structured results. This service "
        "performs no authentication - it is reachable only from the orchestration API."
    ),
    version="1.0.0",
)


@app.get("/health")
async def health() -> dict[str, str]:
    zeek_present = Path(settings.zeek_binary).exists()

    return {
        "status": "healthy" if zeek_present else "degraded",
        "zeek": "available" if zeek_present else f"missing at {settings.zeek_binary}",
    }


@app.post("/analyze", response_model=AnalyzeResponse)
async def analyze(request: AnalyzeRequest) -> AnalyzeResponse:
    """
    Downloads the capture, runs Zeek over it in an isolated workspace, and returns parsed
    results. The workspace is always removed, including on failure.

    Failures are returned as ``success: false`` with HTTP 200 rather than an error status, so
    the orchestrating API can surface the reason to the user directly.
    """

    started = time.monotonic()
    workspace = Path(settings.workspace_root) / str(request.analysis_id)

    try:
        workspace.mkdir(parents=True, exist_ok=True)
        capture_path = workspace / "capture.pcap"

        logger.info(
            "Analysis %s starting for %s/%s",
            request.analysis_id,
            request.bucket_name,
            request.object_key,
        )

        await download_capture(request.download_url, capture_path)

        await run_zeek(capture_path, workspace)

        # Parsing is CPU-bound and synchronous; keep it off the event loop so concurrent
        # requests are not blocked.
        summary, logs, truncated = await asyncio.to_thread(parse_logs, workspace)

        duration = time.monotonic() - started

        logger.info(
            "Analysis %s finished in %.1fs (%d connections)",
            request.analysis_id,
            duration,
            summary.connections,
        )

        return AnalyzeResponse(
            success=True,
            duration_seconds=round(duration, 2),
            summary=summary,
            logs=logs,
            truncated_logs=truncated,
        )

    except (CaptureDownloadError, CaptureTooLargeError, ZeekExecutionError) as exc:
        logger.error("Analysis %s failed: %s", request.analysis_id, exc)

        return AnalyzeResponse(
            success=False,
            error=str(exc),
            duration_seconds=round(time.monotonic() - started, 2),
        )

    except Exception as exc:  # noqa: BLE001 - the API needs a reason, not a stack trace
        logger.exception("Analysis %s failed unexpectedly", request.analysis_id)

        return AnalyzeResponse(
            success=False,
            error=f"Unexpected failure: {exc}",
            duration_seconds=round(time.monotonic() - started, 2),
        )

    finally:
        # Captures are large and must not accumulate on disk between requests.
        shutil.rmtree(workspace, ignore_errors=True)
