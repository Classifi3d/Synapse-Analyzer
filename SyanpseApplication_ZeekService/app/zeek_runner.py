import asyncio
import logging
from pathlib import Path

from .config import settings

logger = logging.getLogger(__name__)


class ZeekExecutionError(Exception):
    """Raised when Zeek exits non-zero or does not finish in time."""


async def run_zeek(capture_path: Path, workspace: Path) -> None:
    """
    Runs Zeek against the capture, writing logs into ``workspace``.

    ``local`` loads Zeek's policy scripts, which is what produces notice.log and the
    detection signals the report depends on. Without it only the base protocol logs appear.
    ``-C`` ignores bad checksums, which are common in captures taken on hosts doing offload.
    """

    command = [
        settings.zeek_binary,
        "-C",
        "-r",
        str(capture_path),
        "local",
        "LogAscii::use_json=T",
    ]

    logger.info("Running: %s", " ".join(command))

    process = await asyncio.create_subprocess_exec(
        *command,
        cwd=str(workspace),
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE,
    )

    try:
        stdout, stderr = await asyncio.wait_for(
            process.communicate(), timeout=settings.zeek_timeout_seconds
        )
    except asyncio.TimeoutError:
        process.kill()
        await process.wait()

        raise ZeekExecutionError(
            f"Zeek did not finish within {settings.zeek_timeout_seconds} seconds."
        ) from None

    stderr_text = stderr.decode(errors="replace").strip()

    if process.returncode != 0:
        raise ZeekExecutionError(
            f"Zeek exited with code {process.returncode}: {stderr_text[:1000]}"
        )

    # Zeek reports non-fatal problems (unknown protocols, truncated packets) on stderr while
    # still producing usable logs, so this is informational rather than a failure.
    if stderr_text:
        logger.warning("Zeek stderr: %s", stderr_text[:1000])

    if stdout:
        logger.debug("Zeek stdout: %s", stdout.decode(errors="replace")[:1000])
