/**
 * Lets the axios interceptor tell the React tree that the session died, without
 * importing React state into a module that loads before the tree exists.
 */

type Listener = () => void

const listeners = new Set<Listener>()

export function onSessionExpired(listener: Listener): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function emitSessionExpired(): void {
  for (const listener of listeners) listener()
}
