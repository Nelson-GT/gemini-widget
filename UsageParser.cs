using System.Globalization;
using System.Text.RegularExpressions;

namespace GeminiWidget;

/// <summary>
/// Extrae los porcentajes de uso y las horas de restablecimiento del texto plano
/// (<c>innerText</c>) de la página de límites de Gemini.
/// El scraping es heurístico: Gemini no expone un contrato estable, así que si
/// Google cambia el diseño hay que ajustar estas expresiones.
/// </summary>
public static class UsageParser
{
    private static readonly Regex PercentRegex =
        new(@"(\d{1,3}(?:[.,]\d{1,2})?)\s*%", RegexOptions.Compiled);

    private static readonly Regex ResetPhraseRegex =
        new(@"(?:se\s+restablece|se\s+renueva|resets?|renews?)\b[^\n\r]*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] CurrentKeywords =
        { "current", "uso actual", "actual", "5-hour", "5 hour", "5h", "hora", "hour" };

    private static readonly string[] WeeklyKeywords =
        { "weekly", "semanal", "7-day", "7 day", "7d", "semana", "week" };

    public static UsageParseResult Parse(string? rawText)
    {
        var result = new UsageParseResult();
        if (string.IsNullOrWhiteSpace(rawText))
            return result;

        var lines = rawText.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        var entries = new List<UsageEntry>();
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (Match match in PercentRegex.Matches(lines[i]))
            {
                if (!double.TryParse(match.Groups[1].Value.Replace(',', '.'),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
                    continue;

                if (percentage < 0 || percentage > 100)
                    continue;

                entries.Add(new UsageEntry(
                    percentage,
                    BuildContext(lines, i).ToLowerInvariant(),
                    ExtractReset(lines, i)));
            }
        }

        if (entries.Count == 0)
            return result;

        var current = entries.FirstOrDefault(e =>
            ContainsAny(e.Context, CurrentKeywords) && !ContainsAny(e.Context, WeeklyKeywords));
        var weekly = entries.FirstOrDefault(e => ContainsAny(e.Context, WeeklyKeywords));

        if (current is null && weekly is null)
        {
            current = entries[0];
            weekly = entries.Count > 1 ? entries[1] : null;
        }
        else if (current is null)
        {
            current = entries.FirstOrDefault(e => !ReferenceEquals(e, weekly)) ?? weekly;
        }
        else if (weekly is null)
        {
            weekly = entries.FirstOrDefault(e => !ReferenceEquals(e, current));
        }

        result.Success = true;

        if (current is not null)
        {
            result.CurrentPercentage = current.Percentage;
            result.CurrentReset = current.Reset;
        }

        if (weekly is not null)
        {
            result.WeeklyPercentage = weekly.Percentage;
            result.WeeklyReset = weekly.Reset;
        }

        return result;
    }

    private static string BuildContext(string[] lines, int index)
    {
        var start = Math.Max(0, index - 1);
        var end = Math.Min(lines.Length - 1, index + 1);
        return string.Join(" ", lines.Skip(start).Take(end - start + 1));
    }

    private static string? ExtractReset(string[] lines, int index)
    {
        for (var i = index; i <= Math.Min(index + 2, lines.Length - 1); i++)
        {
            var match = ResetPhraseRegex.Match(lines[i]);
            if (!match.Success)
                continue;

            var phrase = match.Value.Trim();
            if (phrase.Length > 0)
                return phrase;
        }

        return null;
    }

    private static bool ContainsAny(string text, string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.Ordinal));

    private sealed record UsageEntry(double Percentage, string Context, string? Reset);
}

public sealed class UsageParseResult
{
    public bool Success { get; set; }
    public double CurrentPercentage { get; set; }
    public string? CurrentReset { get; set; }
    public double WeeklyPercentage { get; set; }
    public string? WeeklyReset { get; set; }
}
