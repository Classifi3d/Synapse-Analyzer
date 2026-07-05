using Domain.Interfaces;
using System.Diagnostics;

namespace Infrastructure.ExternalServices
{
    public class ZeekProcessor : IZeekProcessor
    {
        public async Task<string> ProcessPcapAsync(string localPcapFilePath, string outputDirectory)
        {

            var processInfo = new ProcessStartInfo
            {
                FileName = "zeek",
                Arguments = $"-r \"{localPcapFilePath}\"",
                WorkingDirectory = outputDirectory, // Zeek drops logs in the current working directory
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };

            process.Start();

            // Asynchronously wait for Zeek to finish reading the PCAP
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Zeek processing failed with code {process.ExitCode}: {error}");
            }

            // The output directory now contains conn.log, dns.log, http.log, etc.
            return outputDirectory;
        }
    }
}