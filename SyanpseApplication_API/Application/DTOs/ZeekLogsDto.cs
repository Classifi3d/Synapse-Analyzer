using System.Text.Json;

namespace Application.DTOs;

public class ZeekLogsDto
{
    public List<JsonElement> Conn { get; set; } = [];
    public List<JsonElement> Dns { get; set; } = [];
    public List<JsonElement> Http { get; set; } = [];
    public List<JsonElement> Ssl { get; set; } = [];
    public List<JsonElement> Files { get; set; } = [];
    public List<JsonElement> Notice { get; set; } = [];
}