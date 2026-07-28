namespace Infrastructure.Configuration;

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseAddress { get; set; } = "http://localhost:11434";

    public string Model { get; set; } = "llama3.1";

    /// <summary>
    /// Context window passed to Ollama. Zeek logs make for a long prompt: a capture with
    /// ~1,100 connections already builds a ~9k token prompt at the default
    /// Analysis:PromptRowsPerLog of 40.
    ///
    /// This must stay comfortably above that. Ollama silently truncates a prompt that
    /// exceeds num_ctx, and it drops the *start* - which is where the system
    /// instructions and the "end with VERDICT:" rule live. The symptom is not an error
    /// but a shapeless report with no verdict line, and VerdictParser then falling back
    /// to a keyword guess.
    ///
    /// Raising it costs memory, so lower Analysis:PromptRowsPerLog instead if the model
    /// has to fit in a smaller footprint.
    /// </summary>
    public int ContextLength { get; set; } = 32768;

    public double Temperature { get; set; } = 0.2;

    /// <summary>Applies to the whole generation, which streams for as long as the model runs.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);
}
