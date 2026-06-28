export const parseStreamChunk = (chunk, onText, onThreat) => {
  const lines = chunk.split("\n");
  for (const line of lines) {
    if (line.startsWith("data: ")) {
      const dataStr = line.replace("data: ", "").trim();

      if (dataStr === "[DONE]") continue;

      try {
        const parsed = JSON.parse(dataStr);
        if (parsed.text) {
          onText(parsed.text);
        }
        if (parsed.isThreat !== undefined) {
          onThreat(parsed.isThreat);
        }
      } catch {
        // Ignore parse errors on incomplete chunk boundaries
      }
    }
  }
};
