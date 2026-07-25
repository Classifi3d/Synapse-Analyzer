namespace Infrastructure.Configuration;

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseAddress { get; set; } = "http://localhost:11434";

    public string Model { get; set; } = "llama3.1";

    /// <summary>Context window passed to Ollama. Zeek logs make for a long prompt.</summary>
    public int ContextLength { get; set; } = 8192;

    public double Temperature { get; set; } = 0.2;

    /// <summary>Applies to the whole generation, which streams for as long as the model runs.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);
}
