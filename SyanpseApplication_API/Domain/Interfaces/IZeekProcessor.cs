namespace Domain.Interfaces;

public interface IZeekProcessor
{
    /// <summary>
    /// Processes a PCAP file using Zeek and returns the path to the directory containing the generated logs.
    /// </summary>
    Task<string> ProcessPcapAsync(string localPcapFilePath, string outputDirectory);
}