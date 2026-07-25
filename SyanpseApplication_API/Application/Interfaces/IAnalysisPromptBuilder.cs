using Application.DTOs;

namespace Application.Interfaces;

public interface IAnalysisPromptBuilder
{
    string Build(string fileName, string? userPrompt, ZeekAnalysisResultDto zeek);
}
