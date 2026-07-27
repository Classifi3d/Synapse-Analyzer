/**
 * PKCE (RFC 7636) helpers.
 *
 * The verifier is drawn from `crypto.getRandomValues`. `Math.random` is not a
 * cryptographic source, and a guessable verifier defeats the point of PKCE.
 */

const VERIFIER_ALPHABET =
  'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~'

function randomString(length: number): string {
  const bytes = new Uint8Array(length)
  crypto.getRandomValues(bytes)

  // Modulo bias is negligible here: 256 % 66 skews the last 58 symbols by well
  // under a percent, against a 128-character secret.
  let out = ''
  for (const byte of bytes) {
    out += VERIFIER_ALPHABET[byte % VERIFIER_ALPHABET.length]
  }

  return out
}

function base64UrlEncode(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer)

  // Chunked so a large buffer cannot blow the argument limit of String.fromCharCode.
  let binary = ''
  for (let i = 0; i < bytes.length; i += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000))
  }

  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

/** RFC 7636 allows 43-128 characters; we use the maximum. */
export function createCodeVerifier(): string {
  return randomString(128)
}

/** The S256 challenge: BASE64URL(SHA256(verifier)). */
export async function createCodeChallenge(verifier: string): Promise<string> {
  if (!crypto.subtle) {
    throw new Error(
      'Web Crypto is unavailable. The app must be served over https or from localhost.',
    )
  }

  const digest = await crypto.subtle.digest(
    'SHA-256',
    new TextEncoder().encode(verifier),
  )

  return base64UrlEncode(digest)
}

/** Opaque value echoed back by the provider, used to detect CSRF on the callback. */
export function createState(): string {
  return randomString(32)
}
