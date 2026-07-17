using System.Text.RegularExpressions;

namespace AgentLearningLab.Agent;

public sealed partial class TailNumberExtractor : ITailNumberExtractor
{
    [GeneratedRegex(@"\b(N[0-9]{1,5}[A-Z]{0,2})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TailNumberRegex();

    public string? Extract(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var match = TailNumberRegex().Match(text);
        return match.Success
            ? match.Groups[1].Value.ToUpperInvariant()
            : null;
    }
}
