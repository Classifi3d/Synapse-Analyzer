namespace Application.Options;

public class AnalysisOptions
{
    public const string SectionName = "Analysis";

    /// <summary>Rejects oversized captures before any storage session is created. Default 10 GiB.</summary>
    public long MaxFileSizeBytes { get; set; } = 10L * 1024 * 1024 * 1024;

    /// <summary>Lifetime of the presigned PUT urls handed to the browser.</summary>
    public TimeSpan UploadUrlLifetime { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Lifetime of the presigned GET url handed to the Zeek service.</summary>
    public TimeSpan DownloadUrlLifetime { get; set; } = TimeSpan.FromHours(2);

    /// <summary>Maximum rows per protocol log embedded in the prompt, to bound the context size.</summary>
    public int PromptRowsPerLog { get; set; } = 40;

    public string[] AllowedExtensions { get; set; } = [".pcap", ".pcapng", ".cap"];
}
