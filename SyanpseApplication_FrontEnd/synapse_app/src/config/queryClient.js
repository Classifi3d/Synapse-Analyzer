// src/config/queryClient.js
import { QueryClient } from "@tanstack/react-query";

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false, // Prevent interrupting active LLM streams
      retry: 1,
      staleTime: 5 * 60 * 1000, // 5 minutes
    },
  },
});
