using System.Globalization;
using System.Text;

namespace QuanAnBackend.Services;

public static class WebReviewTextNormalizer
{
    public static string NormalizeForMatching(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = true;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                var lower = char.ToLowerInvariant(character);
                builder.Append(lower == 'đ' ? 'd' : lower);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    public static string Compact(string normalizedText) =>
        string.Concat(normalizedText.Where(char.IsLetterOrDigit));

    public static string CollapseRepeatedLetters(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var builder = new StringBuilder(value.Length);
        char previous = '\0';
        foreach (var character in value)
        {
            if (character == previous) continue;
            previous = character;
            builder.Append(character);
        }
        return builder.ToString();
    }
}
