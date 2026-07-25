namespace Application.DTOs;

public class ZeekSummaryDto
{
    public int Connections { get; set; }
    public int DnsQueries { get; set; }
    public int HttpRequests { get; set; }
    public int TlsSessions { get; set; }
    public int Notices { get; set; }
}