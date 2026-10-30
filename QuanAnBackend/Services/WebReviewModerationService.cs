using System.Text.RegularExpressions;

namespace QuanAnBackend.Services;

public sealed class WebReviewModerationService : IWebReviewContentClassifier
{
    // Terms are compared after Vietnamese diacritic and punctuation normalization.
    // Legitimate negative feedback such as "dở", "chậm", "đắt" and "phục vụ tệ"
    // is intentionally not part of these lists.
    private static readonly string[] SevereTerms =
    [
        "dit", "dm", "dkm", "cho mat", "giet may", "tao giet",
        "fuck", "fucking", "shit", "bitch", "bastard", "kill you"
    ];

    private static readonly string[] ObfuscationSensitiveTerms =
    [
        "dit", "dm", "dkm", "cho mat", "giet may", "fuck", "shit", "bitch"
    ];

    public WebReviewClassification Classify(string? comment)
    {
        var normalized = WebReviewTextNormalizer.NormalizeForMatching(comment);
        if (normalized.Length == 0) return new(WebReviewModerationDecision.Public);

        foreach (var term in SevereTerms)
        {
            if (ContainsPhrase(normalized, term))
                return new(WebReviewModerationDecision.Rejected, "Explicit severe abusive or threatening language.");
        }

        foreach (var term in ObfuscationSensitiveTerms)
        {
            if (ContainsObfuscatedForm(normalized, term))
                return new(WebReviewModerationDecision.Pending, "Possible obfuscated abusive or threatening language.");
        }

        return new(WebReviewModerationDecision.Public);
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(phrase)}(?![\p{{L}}\p{{N}}])";
        return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant);
    }

    private static bool ContainsObfuscatedForm(string text, string term)
    {
        var tokenPattern = $@"(?<![\p{{L}}\p{{N}}])([\p{{L}}\p{{N}}]+)(?![\p{{L}}\p{{N}}])";
        var collapsedTokens = new List<string>();
        foreach (Match match in Regex.Matches(text, tokenPattern, RegexOptions.CultureInvariant))
        {
            var token = WebReviewTextNormalizer.CollapseRepeatedLetters(match.Groups[1].Value);
            collapsedTokens.Add(token);
        }
        var collapsedText = string.Join(" ", collapsedTokens);
        if (collapsedText != text && ContainsPhrase(collapsedText, term)) return true;

        // Detect deliberate spaces between letters (e.g. d i t), or between
        // words in a threat phrase, without joining unrelated characters.
        var separatedPattern = string.Join(@"\s*", term.Where(char.IsLetterOrDigit)
            .Select(character => Regex.Escape(character.ToString())));
        return Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){separatedPattern}(?![\p{{L}}\p{{N}}])", RegexOptions.CultureInvariant);
    }
}
