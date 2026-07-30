# Running the stack locally

Four moving parts: the API, the web client, the Zeek service, and Cerberus SSO — plus
Postgres, MinIO and Ollama underneath them.

## What runs where

| Component | Where | Why |
| --- | --- | --- |
| Postgres, Redis, MongoDB | **native** | Already installed on the dev machine. `docker compose` also defines Postgres, so starting that service will collide on port 5432. |
| MinIO, Zeek | **docker compose** | `docker compose up -d minio zeek` |
| Ollama | **native** (`ollama serve`) | A container on macOS has no access to Metal, so a containerised model runs on CPU and is dramatically slower. The `ollama` compose service exists for Linux hosts with an NVIDIA GPU and is behind a profile. |
| API, web client | IDE / `npm run dev` | |

## One-time setup

### 1. Make `minio` resolve on the host

This is the step that is easy to miss and produces a confusing failure.

MinIO presigned urls are signed **against a specific hostname** — change the host and
the signature no longer validates. The same url has to work in two places:

- the **API**, running on the host, which signs it and also talks to MinIO directly;
- the **Zeek container**, which fetches the capture using that url.

Inside the Zeek container `localhost` means the container itself, so a url signed for
`localhost:9000` can never work there. Both sides therefore have to agree on the name
`minio`, which is what `MinIO:Endpoint` is set to. Compose already resolves it; the host
needs one line:

```bash
echo "127.0.0.1 minio" | sudo tee -a /etc/hosts
```

Without it the API cannot reach MinIO at all and `/api/diagnostics/health` reports
`minio` as down.

`MinIO:PublicEndpoint` stays `http://localhost:9000`, because the *browser* uploads
chunks directly and reaches MinIO over the published port.

**Without sudo**, use the machine's LAN address instead — the host reaches it directly
and Docker routes to it from inside the container, so both sides resolve the same name
with no hosts entry:

```bash
MinIO__Endpoint=http://$(ipconfig getifaddr en0):9000 dotnet run ...
```

The trade-off is that the address changes when you change network, which is exactly
why the hosts entry is the better default.

### 2. Pull the model

```bash
ollama pull llama3.1
```

Until this is done `/api/diagnostics/health` reports `ollama` as down — it is reachable
but has no model.

### 3. Run Cerberus on http, not https

Cerberus calls `UseHttpsRedirection()`. Started with its **`https` launch profile** it
binds both 7077 and 5211 and then 307-redirects every plain-http request up to
`https://localhost:7077`.

That breaks browser clients in a way the logs do not make obvious: a browser will not
follow a redirect issued in response to a **CORS preflight**, so the cross-origin login
and token calls fail before they are ever sent. The self-signed dev certificate has to
be trusted on top of that.

Run the `http` profile instead — with no https port bound there is nothing to redirect
to, and the whole flow stays on one scheme:

```bash
dotnet run --project Presentation/Presentation.csproj --launch-profile http
```

Everything is then consistent on `http://localhost:5211`:

| Setting | Value |
| --- | --- |
| Cerberus `JWT:Issuer` | `http://localhost:5211` |
| Synapse API `Jwt:Authority` | `http://localhost:5211` (must match the issuer exactly) |
| Synapse client `VITE_SSO_BASE_URL` | `http://localhost:5211` |
| Angular `environment.apiUrl` | `http://localhost:5211` |
| Cerberus `OAuth:LoginUrl` | `http://localhost:4200/login` |

To use https everywhere instead, set all five to `https://localhost:7077` and run
`dotnet dev-certs https --trust` — the browser *and* the API's discovery fetch both need
to trust that certificate.

### 4. Register the SSO client

See `SyanpseApplication_FrontEnd/synapse_app/README.md`. Cerberus needs a **public**
client (no secret, so the flow uses PKCE) whose redirect uri matches the SPA exactly.

## Day to day

```bash
docker compose up -d minio zeek
ollama serve
```

Then run the API from the IDE and `npm run dev` in the web client.

## Checking it works

```bash
curl -s http://localhost:5056/api/diagnostics/health | python3 -m json.tool
```

Every component reports its own status, latency and a specific reason when unhealthy —
that endpoint is the fastest way to find which dependency is missing. It is
development-only and unauthenticated.

## Notes

- MinIO's CORS origin is set in `docker-compose.yml` via `MINIO_API_CORS_ALLOW_ORIGIN`.
  MinIO does not implement the S3 `PutBucketCors` API, so that variable is the only way
  to configure it. Browser uploads fail on their first chunk without it.
- The compose `api` profile (`docker compose --profile api up`) runs everything in
  Docker instead, in which case no hosts entry is needed — `minio` resolves over the
  compose network. It points at the compose Postgres, not the native one.
