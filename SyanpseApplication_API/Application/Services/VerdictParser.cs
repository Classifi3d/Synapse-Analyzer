using System.Text.RegularExpressions;

namespace Application.Services;

/// <summary>
/// Extracts the machine-readable verdict line the prompt asks the model to emit. Falls back to
/// a keyword scan when the model ignores the format, so a usable verdict is always stored.
/// </summary>
public static partial class VerdictParser
{
    public static (bool IsThreatDetected, string Verdict) Parse(string report)
    {
        if (string.IsNullOrWhiteSpace(report))
            return (false, "No report was generated.");

        var match = VerdictLine().Match(report);

        if (match.Success)
        {
            var level = match.Groups["level"].Value.ToUpperInvariant();
            var justification = match.Groups["reason"].Value.Trim();

            var verdict = string.IsNullOrEmpty(justification)
                ? level
                : $"{level} - {justification}";

            return (level is not "NONE", verdict);
        }

        var isThreat = ThreatKeywords().IsMatch(report);

        return (isThreat, isThreat
            ? "Potential threat indicators reported; see the full report."
            : "No explicit threat verdict was reported.");
    }

    [GeneratedRegex(
        @"VERDICT:\s*(?<level>NONE|LOW|MEDIUM|HIGH|CRITICAL)\s*(?:[-–:]\s*(?<reason>[^\r\n]*))?",
        RegexOptions.IgnoreCase | RegexOptions.RightToLeft)]
    private static partial Regex VerdictLine();

    [GeneratedRegex(
        @"\b(malicious|malware|command\s*(&|and)\s*control|c2\b|beacon\w*|exfiltrat\w+|compromis\w+|ransomware|tunnell?ing)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ThreatKeywords();
}
