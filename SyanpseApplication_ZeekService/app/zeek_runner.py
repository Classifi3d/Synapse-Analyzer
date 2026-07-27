import asyncio
import logging
import os
import shutil
from pathlib import Path

from .config import settings

logger = logging.getLogger(__name__)


class ZeekExecutionError(Exception):
    """Raised when Zeek exits non-zero or does not finish in time."""


def resolve_zeek_binary() -> str | None:
    """
    Locate the Zeek binary, or return None if it is unusable.

    The configured path wins, but different images install Zeek in different places -
    the official zeek/zeek image uses /usr/local/zeek while zeek.org's .deb packages use
    /opt/zeek - so PATH is consulted as a fallback rather than reporting the service
    broken over a layout difference.
    """

    candidate = Path(settings.zeek_binary)

    if candidate.is_file() and os.access(candidate, os.X_OK):
        return str(candidate)

    found = shutil.which("zeek")

    if found:
        logger.warning(
            "Zeek was not at the configured path %s; using %s from PATH instead.",
            settings.zeek_binary,
            found,
        )

    return found


async def run_zeek(capture_path: Path, workspace: Path) -> None:
    """
    Runs Zeek against the capture, writing logs into ``workspace``.

    ``local`` loads Zeek's policy scripts, which is what produces notice.log and the
    detection signals the report depends on. Without it only the base protocol logs appear.
    ``-C`` ignores bad checksums, which are common in captures taken on hosts doing offload.
    """

    binary = resolve_zeek_binary()

    if binary is None:
        raise ZeekExecutionError(
            f"The Zeek binary was not found at '{settings.zeek_binary}' or on PATH."
        )

    command = [
        binary,
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
