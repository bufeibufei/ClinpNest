using System.Text.RegularExpressions;

namespace ClipNest.Services;

public sealed partial class SensitiveContentService
{
    public bool ShouldSkip(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > 20_000)
        {
            return true;
        }

        return OtpRegex().IsMatch(trimmed)
            || SecretKeywordRegex().IsMatch(trimmed)
            || ChineseIdRegex().IsMatch(trimmed)
            || LooksLikeHighEntropySecret(trimmed);
    }

    private static bool LooksLikeHighEntropySecret(string text)
    {
        if (text.Length < 32 || text.Length > 256 || text.Contains(' '))
        {
            return false;
        }

        var letters = text.Count(char.IsLetter);
        var digits = text.Count(char.IsDigit);
        var symbols = text.Count(c => !char.IsLetterOrDigit(c));
        return letters >= 12 && digits >= 6 && symbols >= 1;
    }

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex OtpRegex();

    [GeneratedRegex(@"(?i)(password|passwd|pwd|secret|token|api[_-]?key|access[_-]?key|private[_-]?key)")]
    private static partial Regex SecretKeywordRegex();

    [GeneratedRegex(@"^\d{17}[\dXx]$")]
    private static partial Regex ChineseIdRegex();
}
