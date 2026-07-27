const BYTE_UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'] as const

export function formatBytes(bytes: number, decimals = 1): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B'

  const exponent = Math.min(
    Math.floor(Math.log(bytes) / Math.log(1024)),
    BYTE_UNITS.length - 1,
  )

  const value = bytes / Math.pow(1024, exponent)

  // Whole bytes never need a decimal point.
  const fractionDigits = exponent === 0 ? 0 : decimals

  return `${value.toFixed(fractionDigits)} ${BYTE_UNITS[exponent]}`
}

export function formatNumber(value: number): string {
  return new Intl.NumberFormat().format(value)
}

export function formatTime(value: Date | string): string {
  const date = typeof value === 'string' ? new Date(value) : value

  if (Number.isNaN(date.getTime())) return ''

  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

export function formatDateTime(value: string | null): string {
  if (!value) return '—'

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) return '—'

  return date.toLocaleString([], {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
}

/** Initials for the avatar, from a display name or an email local part. */
export function initialsOf(name: string): string {
  const words = name.trim().split(/[\s._-]+/).filter(Boolean)

  if (words.length === 0) return '??'

  const letters =
    words.length === 1
      ? words[0]!.slice(0, 2)
      : `${words[0]![0]}${words[words.length - 1]![0]}`

  return letters.toUpperCase()
}
