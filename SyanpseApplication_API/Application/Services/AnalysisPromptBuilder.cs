using System.Text;
using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Application.Options;
using Microsoft.Extensions.Options;

namespace Application.Services;

/// <summary>
/// Builds the LLM prompt from structured Zeek output. Prompt construction lives entirely in
/// the application layer - the Zeek service is unaware that an LLM exists, and the Ollama
/// client is unaware that Zeek exists.
/// </summary>
public class AnalysisPromptBuilder(IOptions<AnalysisOptions> options) : IAnalysisPromptBuilder
{
    private readonly AnalysisOptions _options = options.Value;

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        WriteIndented = false
    };

    public const string VerdictMarker = "VERDICT:";

    public string Build(string fileName, string? userPrompt, ZeekAnalysisResultDto zeek)
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            """
            You are a senior network security analyst reviewing Zeek telemetry from a packet capture.

            Assess the traffic for:
            - Malware and command & control channels
            - Beaconing and periodic callbacks
            - DNS tunnelling and suspicious resolution patterns
            - Data exfiltration
            - Lateral movement
            - Port and service scanning
            - Malicious or anomalous HTTP activity
            - Suspicious TLS behaviour and certificate anomalies

            Rules:
            - Base every statement only on the Zeek data supplied below. Do not invent hosts,
              domains, or events that do not appear in it.
            - The logs may be a sample of a larger capture. Say so when a conclusion depends on
              rows you cannot see.
            - If the traffic looks benign, say so plainly rather than manufacturing findings.

            Write the report in markdown with exactly these sections:

            # Executive Summary
            # Findings
            # Indicators of Compromise
            # Risk Assessment
            # Recommendations
            """);

        builder.AppendLine();
        builder.AppendLine(
            $"""
            Finish your response with a single final line in exactly this format:
            {VerdictMarker} <NONE|LOW|MEDIUM|HIGH|CRITICAL> - <one sentence justification>
            """);

        builder.AppendLine();
        builder.AppendLine("## Capture");
        builder.AppendLine($"File name: {fileName}");
        builder.AppendLine($"Zeek processing time: {zeek.DurationSeconds:F1}s");

        if (zeek.TruncatedLogs.Count > 0)
        {
            builder.AppendLine(
                $"Sampled logs (only the first {_options.PromptRowsPerLog} rows shown): " +
                string.Join(", ", zeek.TruncatedLogs));
        }

        builder.AppendLine();
        builder.AppendLine("## Zeek Summary");
        builder.AppendLine(JsonSerializer.Serialize(zeek.Summary, CompactJson));

        builder.AppendLine();
        builder.AppendLine("## Zeek Logs");
        AppendLog(builder, "conn.log", zeek.Logs.Conn);
        AppendLog(builder, "dns.log", zeek.Logs.Dns);
        AppendLog(builder, "http.log", zeek.Logs.Http);
        AppendLog(builder, "ssl.log", zeek.Logs.Ssl);
        AppendLog(builder, "files.log", zeek.Logs.Files);
        AppendLog(builder, "notice.log", zeek.Logs.Notice);
        AppendLog(builder, "weird.log", zeek.Logs.Weird);
        AppendLog(builder, "x509.log", zeek.Logs.X509);

        builder.AppendLine();
        builder.AppendLine("## Analyst Request");
        builder.AppendLine(string.IsNullOrWhiteSpace(userPrompt)
            ? "Produce a general threat assessment of this capture."
            : userPrompt.Trim());

        return builder.ToString();
    }

    private void AppendLog(StringBuilder builder, string name, List<JsonElement> rows)
    {
        if (rows.Count == 0)
            return;

        builder.AppendLine();
        builder.AppendLine($"### {name} ({rows.Count} rows)");

        foreach (var row in rows.Take(_options.PromptRowsPerLog))
        {
            builder.AppendLine(row.GetRawText());
        }
    }
}
