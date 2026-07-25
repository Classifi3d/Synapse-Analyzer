namespace Application.DTOs;

/// <summary>Aggregated counters the Zeek service derives from the generated logs.</summary>
public class ZeekSummaryDto
{
    public int Connections { get; set; }
    public int DnsQueries { get; set; }
    public int HttpRequests { get; set; }
    public int TlsSessions { get; set; }
    public int TransferredFiles { get; set; }
    public int Notices { get; set; }
    public int Weird { get; set; }
    public int Certificates { get; set; }

    public DateTime? CaptureStart { get; set; }
    public DateTime? CaptureEnd { get; set; }

    public int UniqueSourceIps { get; set; }
    public int UniqueDestinationIps { get; set; }
    public long TotalBytes { get; set; }

    public List<ZeekTalkerDto> TopTalkers { get; set; } = [];
    public List<ZeekCountDto> TopDnsQueries { get; set; } = [];
    public List<ZeekPortDto> TopPorts { get; set; } = [];
    public List<string> NoticeTypes { get; set; } = [];
}

public class ZeekTalkerDto
{
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public int Connections { get; set; }
    public long Bytes { get; set; }
}

public class ZeekCountDto
{
    public string Value { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ZeekPortDto
{
    public int Port { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string? Service { get; set; }
    public int Connections { get; set; }
}
