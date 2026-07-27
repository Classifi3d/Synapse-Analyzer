/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base url of the Synapse API, including the /api prefix. */
  readonly VITE_API_BASE_URL: string
  /** Origin of the Cerberus SSO service. No trailing slash, no path. */
  readonly VITE_SSO_BASE_URL: string
  /** OAuth client id registered with Cerberus via POST /OAuth/clients. */
  readonly VITE_SSO_CLIENT_ID: string
  /** Must match the redirect uri registered for the client, exactly. */
  readonly VITE_SSO_REDIRECT_URI: string
  readonly VITE_SSO_SCOPE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
