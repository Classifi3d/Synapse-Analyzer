namespace Infrastructure.Configuration;

public class ZeekOptions
{
    public const string SectionName = "Zeek";

    public string BaseAddress { get; set; } = "http://localhost:8000";

    /// <summary>
    /// Zeek runs over the whole capture before responding, so this has to tolerate multi-gigabyte
    /// files. Default 30 minutes.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
}
