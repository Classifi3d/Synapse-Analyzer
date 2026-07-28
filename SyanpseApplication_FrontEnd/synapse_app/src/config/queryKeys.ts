/**
 * Central query key factory.
 *
 * Keys are built hierarchically so a broad key invalidates everything beneath it:
 * invalidating `analyses.all` also clears every list and detail entry, without any
 * caller needing to know which keys exist.
 */
export const queryKeys = {
  analyses: {
    all: ['analyses'] as const,
    lists: () => [...queryKeys.analyses.all, 'list'] as const,
    details: () => [...queryKeys.analyses.all, 'detail'] as const,
    detail: (analysisId: string) =>
      [...queryKeys.analyses.details(), analysisId] as const,
  },
} as const
