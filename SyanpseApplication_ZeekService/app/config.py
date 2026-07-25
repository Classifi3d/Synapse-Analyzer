from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Service configuration, overridable through environment variables."""

    model_config = SettingsConfigDict(env_prefix="ZEEK_", env_file=".env")

    # Path to the Zeek binary inside the container image.
    zeek_binary: str = "/opt/zeek/bin/zeek"

    # Parent directory for per-request workspaces. Each request gets its own subdirectory,
    # which is deleted once the response has been built.
    workspace_root: str = "/tmp/synapse"

    # Zeek is CPU-bound and runs for as long as the capture is large; this caps a single run.
    zeek_timeout_seconds: int = 1800

    # Ceiling on how long the capture download may take.
    download_timeout_seconds: int = 900

    # Refuse captures larger than this to protect the container's disk. Default 10 GiB.
    max_capture_bytes: int = 10 * 1024 * 1024 * 1024

    # Rows returned per protocol log. The API embeds these in an LLM prompt, so the cap keeps
    # the response small regardless of capture size.
    max_rows_per_log: int = 200

    # How many entries the top-talkers / top-queries aggregations return.
    top_n: int = 10


settings = Settings()
