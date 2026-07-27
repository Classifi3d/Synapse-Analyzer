# Synapse Zeek Service

A FastAPI wrapper around Zeek. The Synapse API hands it the coordinates of a capture;
it fetches the capture from object storage, runs Zeek over it, and returns structured
logs plus aggregate counters.

The capture never passes through the .NET API — that is the whole point of the
presigned-url handoff, and it is what makes multi-gigabyte captures workable.

## Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/health` | `healthy`, or `degraded` when the Zeek binary is missing |
| `POST` | `/analyze` | Run one capture through Zeek |

### `POST /analyze`

```json
{
  "analysis_id": "11111111-2222-3333-4444-555555555555",
  "bucket_name": "synapse-pcap-uploads",
  "object_key": "captures/<user>/<analysis>/capture.pcap",
  "download_url": "http://minio:9000/...&X-Amz-Signature=..."
}
```

Responds `200` even on failure, with `success: false` and `error` set. That is
deliberate: the API surfaces `error` to the analyst verbatim, and a non-2xx status would
replace it with a generic pipeline message.

Field names are the snake_case forms produced by `JsonNamingPolicy.SnakeCaseLower` on
the C# DTOs in `Application/DTOs`. **Renaming any field here silently breaks the
pipeline** — the value simply arrives as its type default rather than raising an error.
`x509` in particular is the snake_case of the C# `X509` property.

## Configuration

Environment variables, all prefixed `ZEEK_` (see `app/config.py`):

| Variable | Default | Notes |
| --- | --- | --- |
| `ZEEK_BINARY` | `/usr/local/zeek/bin/zeek` | Where the official `zeek/zeek` image puts it. Falls back to `zeek` on `PATH`. |
| `ZEEK_WORKSPACE_ROOT` | `/tmp/synapse` | One subdirectory per analysis, deleted afterwards |
| `ZEEK_MAX_ROWS_PER_LOG` | `200` | Caps rows returned per log; counters still reflect the whole capture |
| `ZEEK_ZEEK_TIMEOUT_SECONDS` | `1800` | Ceiling on a single Zeek run |
| `ZEEK_MAX_CAPTURE_BYTES` | 10 GiB | Refuses larger captures |
| `ZEEK_TOP_N` | `10` | Entries per "top N" list |

Zeek is invoked as `zeek -C -r <capture> local LogAscii::use_json=T`. `local` loads the
policy scripts that produce `notice.log`; without it only the base protocol logs appear.
`-C` ignores bad checksums, which are routine on hosts doing TCP offload.

## Tests

The local macOS system Python is 3.9 and cannot parse the `str | None` syntax these
modules use, so run them in the image:

```bash
docker build -t synapse-zeek ./SyanpseApplication_ZeekService
docker run --rm -v "$PWD/SyanpseApplication_ZeekService/tests:/srv/tests:ro" synapse-zeek \
  sh -c "pip install -q pytest==8.3.4 && cd /srv && python -m pytest tests/ -q"
```

## Security

`/analyze` fetches whatever url it is given and has no authentication. Keep it on the
internal network — publishing it hands anyone a server-side request forgery primitive.
The compose file exposes port 8000 only so the API can run from an IDE on the host.

## Note on disk

`docker-compose.yml` mounts `/tmp/synapse` as a **tmpfs sized 16g**, which is RAM inside
the Docker VM. A capture near the 10 GiB ceiling will therefore try to hold itself in
memory, and Docker Desktop's VM is usually allocated far less than that. For large
captures, swap the `tmpfs:` block for a named volume:

```yaml
    volumes:
      - zeek-work:/tmp/synapse
```
