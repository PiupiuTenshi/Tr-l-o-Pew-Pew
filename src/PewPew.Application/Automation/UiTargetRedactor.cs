using System.Text.RegularExpressions;

namespace PewPew.Application.Automation;

/// <summary>
/// Security redactor service that inspects UI input fields and element payloads,
/// redacting sensitive secrets (passwords, API keys, JWT tokens, credit card CVVs)
/// with <c>"[REDACTED_SECRET]"</c>.
/// </summary>
public sealed class UiTargetRedactor
{
    public const string RedactedReplacement = "[REDACTED_SECRET]";

    private static readonly HashSet<string> SensitiveInputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "secret", "cvv", "creditcard", "cardnumber", "pin"
    };

    private static readonly HashSet<string> SensitiveKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pass", "pwd", "secret", "token", "apikey", "api_key", "bearer", "auth_token", "cvv", "card"
    };

    private static readonly Regex JwtTokenRegex = new(
        @"eyJ[A-Za-z0-9-_=]+\.[A-Za-z0-9-_=]+\.?[A-Za-z0-9-_.+/=]*",
        RegexOptions.Compiled);

    private static readonly Regex ApiKeyRegex = new(
        @"(?:sk|pk|api|key|mock_key)(?:_[a-z0-9]+)*_[A-Za-z0-9]{16,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);



    /// <summary>
    /// Checks whether an input field is sensitive based on input type or field identifier.
    /// </summary>
    public static bool IsSensitiveField(string? inputType, string? fieldName)
    {
        if (!string.IsNullOrWhiteSpace(inputType) && SensitiveInputTypes.Contains(inputType.Trim()))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            var normalizedName = fieldName.Trim().ToLowerInvariant();
            if (SensitiveKeywords.Any(k => normalizedName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Redacts secret data from an input text string or sensitive field.
    /// Returns <c>"[REDACTED_SECRET]"</c> if the field is sensitive or contains secret patterns.
    /// </summary>
    public static string RedactText(string? input, string? inputType = null, string? fieldName = null)

    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        if (IsSensitiveField(inputType, fieldName))
        {
            return RedactedReplacement;
        }

        var result = JwtTokenRegex.Replace(input, RedactedReplacement);
        result = ApiKeyRegex.Replace(result, RedactedReplacement);

        return result;
    }
}
