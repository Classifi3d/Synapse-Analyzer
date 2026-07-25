import logging
from pathlib import Path

import httpx

from .config import settings

logger = logging.getLogger(__name__)


class CaptureTooLargeError(Exception):
    """Raised when a capture exceeds the configured size ceiling."""


class CaptureDownloadError(Exception):
    """Raised when the capture cannot be retrieved from object storage."""


async def download_capture(download_url: str, destination: Path) -> int:
    """
    Streams the capture to disk in chunks so a multi-gigabyte file never has to fit in
    memory. Returns the number of bytes written.
    """

    timeout = httpx.Timeout(settings.download_timeout_seconds, connect=30.0)
    written = 0

    try:
        async with httpx.AsyncClient(timeout=timeout, follow_redirects=True) as client:
            async with client.stream("GET", download_url) as response:
                if response.status_code != 200:
                    body = (await response.aread())[:500].decode(errors="replace")
                    raise CaptureDownloadError(
                        f"Object storage returned {response.status_code}: {body}"
                    )

                declared = response.headers.get("content-length")
                if declared and int(declared) > settings.max_capture_bytes:
                    raise CaptureTooLargeError(
                        f"Capture is {int(declared)} bytes, over the "
                        f"{settings.max_capture_bytes} byte limit."
                    )

                with destination.open("wb") as handle:
                    async for chunk in response.aiter_bytes(chunk_size=1024 * 1024):
                        written += len(chunk)

                        # Guards against a missing or dishonest content-length header.
                        if written > settings.max_capture_bytes:
                            raise CaptureTooLargeError(
                                f"Capture exceeds the {settings.max_capture_bytes} byte limit."
                            )

                        handle.write(chunk)

    except httpx.HTTPError as exc:
        raise CaptureDownloadError(f"Failed to download the capture: {exc}") from exc

    if written == 0:
        raise CaptureDownloadError("The downloaded capture was empty.")

    logger.info("Downloaded %s bytes to %s", written, destination)

    return written
