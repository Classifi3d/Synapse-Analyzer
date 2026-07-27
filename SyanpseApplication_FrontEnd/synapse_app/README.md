# Synapse Analyzer — web client

React 19 + TypeScript + Vite. Talks to the Synapse Analyzer API and authenticates
against Cerberus SSO using the OAuth 2.0 authorization code flow with PKCE.

## Running

```bash
npm install
cp .env.example .env
npm run dev
```

| Script | Purpose |
| --- | --- |
| `npm run dev` | Dev server on **port 5173** (fixed — the API's CORS policy names this exact origin) |
| `npm run build` | Typecheck, then production build |
| `npm run typecheck` | Types only |
| `npm run lint` | ESLint, type-aware |

## Layout

```
src/
  config/      env parsing, axios instance, react-query client
  types/       mirrors of the API DTOs and the SSE event payloads
  features/
    auth/      PKCE, token storage, AuthProvider, route guard
    upload/    multipart upload direct to object storage
    analysis/  SSE consumption, report rendering, workspace
  pages/       login, OAuth callback, not-found
  components/  layout and shared pieces
```

## How an analysis runs

1. `POST /api/analysis/upload/initiate` returns `partSizeBytes` and one presigned
   `PUT` url per part.
2. The browser slices the file and uploads each part **straight to MinIO**. The
   capture never passes through the API, which is what makes 10 GB captures viable.
3. Each part's `ETag` response header is collected and posted to
   `POST /api/analysis/upload/complete`, which assembles the object.
4. `GET /api/analysis/{id}/stream` is consumed as server-sent events. It emits *named*
   events — `status`, `summary`, `token`, `done`, `error` — so the client subscribes
   per event type rather than parsing `onmessage`.

Two constraints are easy to break by accident:

- **Part uploads must not send a `Content-Type` header.** The presigned urls are signed
  without one, so adding it invalidates the signature. This is why the upload uses
  `XMLHttpRequest` and never calls `setRequestHeader` — it also gives us upload
  progress, which `fetch` cannot provide.
- **The stream must be closed on `done`.** `EventSource` reconnects automatically when
  a connection ends, so leaving it open after the server finishes would silently re-run
  the entire analysis, repeatedly.

## Prerequisites outside this app

### MinIO must allow browser uploads

Parts are uploaded from the browser, so MinIO has to accept this origin **and** expose
the `ETag` header — without a readable ETag the parts cannot be assembled. MinIO does
not implement the S3 `PutBucketCors` API; it reads an environment variable instead:

```bash
MINIO_API_CORS_ALLOW_ORIGIN=http://localhost:5173
```

If this is missing the upload fails on the first chunk, and the UI says so explicitly
rather than failing silently.

### The SSO client must be registered

Cerberus needs a **public** client — registered with no secret, so the flow relies on
PKCE. Its `redirectUri` must equal `VITE_SSO_REDIRECT_URI` byte for byte:

```bash
curl -X POST http://localhost:5211/OAuth/clients \
  -H 'Content-Type: application/json' \
  -d '{"clientId":"synapse-spa","clientSecret":"","redirectUri":"http://localhost:5173/auth/callback","allowedScopes":"openid profile email"}'
```

The API must then trust that issuer, in its `appsettings.json`:

```json
"Jwt": { "Authority": "http://localhost:5211", "Audience": "synapse-spa" }
```

`Audience` is the **client id**, because Cerberus stamps the client id into the token's
`aud` claim.

## Environment

See `.env.example`. Every variable is required except `VITE_SSO_SCOPE`, and a missing
one throws at startup rather than becoming an `undefined` spliced into a request url.
