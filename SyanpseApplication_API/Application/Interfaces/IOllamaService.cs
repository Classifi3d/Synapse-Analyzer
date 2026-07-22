namespace Application.Interfaces;

public interface IOllamaService
{
    Task<OllamaAnalysisResult> AnalyzePcapDataAsync(string prompt, string pcapData);
}

public class OllamaAnalysisResult
{
    public bool IsThreat { get; set; }
    public string Details { get; set; } = string.Empty;
}