# Synapse Analyzer — PCAP Pipeline

How a capture travels from the browser to a generated report, and the exact contract each
step expects.

## Components

| Component | Responsibility | Default address |
|---|---|---|
| React frontend | UI, auth, direct-to-MinIO upload, renders the stream | `http://localhost:5173` |
| ASP.NET Core API | Orchestration, authz, metadata, prompt construction | `http://localhost:8080` |
| PostgreSQL | Analysis metadata only | `localhost:5432` |
| MinIO | Capture storage, presigned multipart uploads | `localhost:9000` |
| FastAPI Zeek service | Zeek execution and log parsing | `localhost:8000` |
| Ollama | Report generation | `localhost:11434` |

The API never receives, downloads, or stores capture bytes. It moves coordinates and URLs.

## Upload

```
Browser                          API                       MinIO
   |  POST /upload/initiate       |                          |
   |----------------------------->|  CreateMultipartUpload   |
   |                              |------------------------->|
   |                              |  presign PUT per part    |
   |  { uploadId, parts[], ... }  |                          |
   |<-----------------------------|                          |
   |                                                         |
   |  PUT part 1..N  (direct, parallel)                      |
   |-------------------------------------------------------->|
   |  <- ETag per part                                       |
   |                                                         |
   |  POST /upload/complete       |                          |
   |  { analysisId, parts[] }     |  CompleteMultipartUpload |
   |----------------------------->|------------------------->|
```

### `POST /api/analysis/upload/initiate`

```jsonc
// request
{ "fileName": "capture.pcapng", "contentType": "application/vnd.tcpdump.pcap", "fileSize": 5368709120 }

// response
{
  "analysisId": "…",
  "uploadId": "…",
  "bucketName": "synapse-pcap-uploads",
  "objectKey": "captures/<userId>/<analysisId>/capture.pcapng",
  "partSizeBytes": 67108864,
  "expiresAtUtc": "…",
  "parts": [ { "partNumber": 1, "uploadUrl": "http://localhost:9000/…" } ]
}
```

### Uploading the parts

Two rules the client must follow, both signature-related:

1. **Slice at exactly `partSizeBytes`.** Part *n* is `file.slice((n-1) * partSizeBytes, n * partSizeBytes)`.
   Every part except the last must be that size — S3 rejects the assembly otherwise.
2. **Do not set a `Content-Type` header on the part PUT.** The urls are signed without one;
   adding it invalidates the signature.

```js
const res = await fetch(part.uploadUrl, { method: "PUT", body: chunk });
const eTag = res.headers.get("ETag");   // required by the complete step
```

If `res.headers.get("ETag")` is `null`, the browser is being blocked by CORS — see
[MinIO CORS](#minio-cors) below.

### `POST /api/analysis/upload/complete`

```jsonc
{
  "analysisId": "…",
  "parts": [ { "partNumber": 1, "eTag": "\"a1b2…\"" } ],
  "prompt": "Focus on anything talking to external infrastructure after hours."
}
```

Quoted or unquoted ETags are both accepted. The API assembles the object, confirms it is
readable, and moves the analysis to `Uploaded`.

`prompt` is the analyst's request, submitted together with the upload, and is stored on the
analysis. It is optional — the stream endpoint can override it, and a general assessment is
produced when neither is supplied.

### What the model actually receives

The prompt sent to Ollama is assembled by the API from three parts:

1. **System instructions** — the analyst role, the threat categories to look for, the
   "only use the supplied data" constraint, and the required report sections.
2. **Zeek output** — the summary counters plus a capped sample of each protocol log.
3. **The analyst request** — the `prompt` above, under an `## Analyst Request` heading.

All three are combined in [`AnalysisPromptBuilder`](../SyanpseApplication_API/Application/Services/AnalysisPromptBuilder.cs).
`OllamaService` receives the finished string, so it has no knowledge of Zeek or captures, and
the Zeek service has no knowledge that an LLM exists.

## Analysis

`GET /api/analysis/{id}/stream?prompt=<optional>&access_token=<jwt>`

Server-sent events, one-way — which is why this is SSE rather than a websocket. Four event
types:

| Event | Payload | Meaning |
|---|---|---|
| `status` | `{ stage, message }` | `zeek` then `reporting` |
| `summary` | Zeek counters | Emitted once, before generation starts |
| `token` | `{ text }` | One fragment of the report |
| `done` | `{ analysisId, isThreatDetected, verdict }` | Final frame, report persisted |
| `error` | `{ message }` | Final frame, analysis marked `Failed` |

```js
const source = new EventSource(
  `${API}/api/analysis/${id}/stream?access_token=${token}&prompt=${encodeURIComponent(prompt)}`
);

source.addEventListener("token", e => append(JSON.parse(e.data).text));
source.addEventListener("done",  e => source.close());
source.addEventListener("error", e => source.close());
```

`EventSource` cannot send an `Authorization` header, so the JWT goes in the query string;
the API accepts it there for `/api/analysis` routes only.

Zeek output is cached on the analysis row, so re-running the stream with a different prompt
regenerates the report without re-processing the capture.

### Status flow

```
AwaitingUpload → Uploaded → Analyzing → Reporting → Completed
                     └──────────┴───────────┴──────→ Failed
```

## Running it

```bash
docker compose up -d
```

Then pull the model once:

```bash
docker compose exec ollama ollama pull llama3.1
```

Run the API from your IDE (it targets `localhost` by default), or add it to the stack with
`docker compose --profile api up -d`.

### MinIO CORS

The browser writes to MinIO directly, so MinIO must allow the frontend origin. MinIO does not
implement the S3 `PutBucketCors` API — the origin comes from an environment variable, already
set in `docker-compose.yml`:

```yaml
MINIO_API_CORS_ALLOW_ORIGIN: "http://localhost:5173,http://localhost:3000"
```

Change it if the frontend runs on a different port, and restart the container. Symptom of a
mismatch: part PUTs fail, or succeed but expose no `ETag` header.

### A token for local testing

Until SSO is wired up, `appsettings.Development.json` validates tokens against a symmetric
key. The `sub` claim must be a GUID — it becomes the user id every analysis is scoped to.

```bash
python - <<'EOF'
import base64, hashlib, hmac, json, time, uuid

KEY = b"development-only-signing-key-change-me-at-least-32-bytes"
b64 = lambda d: base64.urlsafe_b64encode(d).rstrip(b"=")

header = b64(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
payload = b64(json.dumps({
    "sub": str(uuid.uuid4()),
    "email": "analyst@example.com",
    "iss": "synapse",
    "aud": "synapse-api",
    "exp": int(time.time()) + 86400,
}).encode())

signature = b64(hmac.new(KEY, header + b"." + payload, hashlib.sha256).digest())
print((header + b"." + payload + b"." + signature).decode())
EOF
```

Reuse the same token across requests — a new `sub` is a different user, and analyses are not
visible across users.

### Why the AWS S3 client instead of the MinIO SDK

Presigning an individual part requires a url carrying both `uploadId` and `partNumber`. The
MinIO .NET SDK keeps multipart creation internal and only presigns whole objects, so the API
uses `AWSSDK.S3` against MinIO's S3-compatible endpoint. This is a client-library choice only —
MinIO itself is unchanged, and runs from the stock image.

Two S3 clients are registered, because SigV4 binds the signature to the hostname:

- **internal** (`MinIO:Endpoint`) — API-side calls, and the presigned GET the Zeek service uses
- **public** (`MinIO:PublicEndpoint`) — the presigned PUTs the browser calls

On a single host both are `http://localhost:9000`. Under compose they differ
(`http://minio:9000` vs `http://localhost:9000`), which is why rewriting the host after signing
is not an option.

## Configuration

| Key | Purpose |
|---|---|
| `ConnectionStrings:Postgres` | Metadata database |
| `MinIO:Endpoint` / `PublicEndpoint` | Internal and browser-facing storage addresses |
| `MinIO:PartSizeBytes` | Chunk size; minimum 5 MiB, default 64 MiB |
| `Zeek:BaseAddress` / `Timeout` | Zeek service address and per-capture ceiling |
| `Ollama:BaseAddress` / `Model` / `ContextLength` | Generation settings |
| `Analysis:MaxFileSizeBytes` | Rejected before a session is opened |
| `Analysis:PromptRowsPerLog` | Log rows embedded in the prompt |
| `Jwt:Authority` | SSO authority (production) |
| `Jwt:SigningKey` | Symmetric key for local development instead of an authority |

The Zeek service reads `ZEEK_`-prefixed environment variables (`ZEEK_MAX_ROWS_PER_LOG`,
`ZEEK_ZEEK_TIMEOUT_SECONDS`, …).
