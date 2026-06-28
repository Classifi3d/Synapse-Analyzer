import { useState, useCallback } from "react";
import { fetchLlmStream } from "../services/api.analysis";

export const useLlmStream = () => {
  const [isStreaming, setIsStreaming] = useState(false);

  const streamAnalysis = useCallback(async (prompt, fileId, onChunk) => {
    setIsStreaming(true);

    try {
      // Call our service to get the raw stream
      const stream = await fetchLlmStream(prompt, fileId);

      const reader = stream.getReader();
      const decoder = new TextDecoder("utf-8");
      let isThreatDetected = undefined;

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;

        if (value) {
          const chunk = decoder.decode(value, { stream: true });

          // Basic SSE parsing: splitting by double newlines
          const lines = chunk.split("\n");
          for (const line of lines) {
            if (line.startsWith("data: ")) {
              const dataStr = line.replace("data: ", "").trim();

              if (dataStr === "[DONE]") break;

              try {
                const parsed = JSON.parse(dataStr);
                // Call the callback provided by the component to update the UI
                if (parsed.text) {
                  onChunk(parsed.text);
                }
                // If the stream sends a final threat determination flag
                if (parsed.isThreat !== undefined) {
                  isThreatDetected = parsed.isThreat;
                }
              } catch {
                // Ignore parse errors on incomplete chunk boundaries
              }
            }
          }
        }
      }

      return isThreatDetected;
    } catch (error) {
      console.error("Streaming error:", error);
      onChunk("\n\n[Error: Connection to local LLM lost]");
      return undefined;
    } finally {
      setIsStreaming(false);
    }
  }, []);

  return { streamAnalysis, isStreaming };
};
