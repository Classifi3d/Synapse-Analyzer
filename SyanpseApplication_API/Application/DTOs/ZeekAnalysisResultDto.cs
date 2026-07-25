namespace Application.DTOs;

public class ZeekAnalysisResultDto
{
    public bool Success { get; set; }
    public ZeekSummaryDto Summary { get; set; } = new();
    public ZeekLogsDto Logs { get; set; } = new();
}