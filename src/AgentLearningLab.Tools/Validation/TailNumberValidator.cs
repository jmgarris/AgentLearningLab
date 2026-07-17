using System.Text.RegularExpressions;

namespace AgentLearningLab.Tools.Validation;

public static partial class TailNumberValidator
{
    private static readonly Regex TailNumberRegex = CreateTailNumberRegex();

    public static bool IsValid(string? tailNumber)
    {
        return !string.IsNullOrWhiteSpace(tailNumber) && TailNumberRegex.IsMatch(tailNumber.Trim());
    }

    [GeneratedRegex("^N[0-9]{1,5}[A-Z]{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CreateTailNumberRegex();
}
