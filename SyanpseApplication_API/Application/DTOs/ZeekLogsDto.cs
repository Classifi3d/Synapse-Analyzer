using System.Text.Json;

namespace Application.DTOs;

/// <summary>
/// Sampled rows from each protocol log. The Zeek service caps how many rows it returns, so
/// these lists stay small enough to fit in a prompt.
/// </summary>
public class ZeekLogsDto
{
    public List<JsonElement> Conn { get; set; } = [];
    public List<JsonElement> Dns { get; set; } = [];
    public List<JsonElement> Http { get; set; } = [];
    public List<JsonElement> Ssl { get; set; } = [];
    public List<JsonElement> Files { get; set; } = [];
    public List<JsonElement> Notice { get; set; } = [];
    public List<JsonElement> Weird { get; set; } = [];
    public List<JsonElement> X509 { get; set; } = [];
}
