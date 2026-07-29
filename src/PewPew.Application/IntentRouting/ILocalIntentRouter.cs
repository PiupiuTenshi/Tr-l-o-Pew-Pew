using System.Text.Json;

namespace PewPew.Application.IntentRouting;

/// <summary>
/// Validates an untrusted local-model proposal. This boundary never grants
/// permission, creates an ActionPlan, or dispatches a tool.
/// </summary>
public interface ILocalIntentRouter
{
    LocalIntentRoutingResult Route(string? structuredCandidate);
}

public enum LocalIntentRoutingStatus
{
    Proposed,
    ClarificationRequired,
    Rejected
}

public enum LocalIntentName
{
    OpenApplication,
    MediaControl,
    AdjustVolume,
    FindFile
}

public sealed record LocalIntentProposal(
    int SchemaVersion,
    LocalIntentName Intent,
    IReadOnlyDictionary<string, string> Arguments);

public sealed record LocalIntentRoutingResult(
    LocalIntentRoutingStatus Status,
    string ReasonCode,
    LocalIntentProposal? Proposal = null);

public sealed class StrictLocalIntentRouter : ILocalIntentRouter
{
    private const int SchemaVersion = 1;
    private const int MaximumCandidateLength = 4_096;
    private static readonly Dictionary<string, LocalIntentName> IntentNames =
        new Dictionary<string, LocalIntentName>(StringComparer.Ordinal)
        {
            ["open_application"] = LocalIntentName.OpenApplication,
            ["media_control"] = LocalIntentName.MediaControl,
            ["adjust_volume"] = LocalIntentName.AdjustVolume,
            ["find_file"] = LocalIntentName.FindFile
        };

    public LocalIntentRoutingResult Route(string? structuredCandidate)
    {
        if (string.IsNullOrWhiteSpace(structuredCandidate))
        {
            return new(LocalIntentRoutingStatus.ClarificationRequired, "proposal_missing");
        }

        if (structuredCandidate.Length > MaximumCandidateLength)
        {
            return new(LocalIntentRoutingStatus.Rejected, "proposal_too_large");
        }

        try
        {
            using var document = JsonDocument.Parse(structuredCandidate);
            return Validate(document.RootElement);
        }
        catch (JsonException)
        {
            return new(LocalIntentRoutingStatus.Rejected, "proposal_invalid_json");
        }
    }

    private static LocalIntentRoutingResult Validate(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !HasExactProperties(root, "schemaVersion", "intent", "arguments"))
        {
            return new(LocalIntentRoutingStatus.Rejected, "proposal_schema_invalid");
        }

        if (!root.TryGetProperty("schemaVersion", out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var schemaVersion)
            || schemaVersion != SchemaVersion
            || !root.TryGetProperty("intent", out var intentElement)
            || intentElement.ValueKind != JsonValueKind.String
            || !IntentNames.TryGetValue(intentElement.GetString() ?? string.Empty, out var intent)
            || !root.TryGetProperty("arguments", out var argumentsElement)
            || argumentsElement.ValueKind != JsonValueKind.Object)
        {
            return new(LocalIntentRoutingStatus.Rejected, "proposal_schema_invalid");
        }

        var arguments = ReadArguments(argumentsElement);
        if (arguments is null)
        {
            return new(LocalIntentRoutingStatus.Rejected, "proposal_arguments_invalid");
        }

        if (arguments.Values.Any(IsPromptInjectionLike))
        {
            return new(LocalIntentRoutingStatus.Rejected, "proposal_untrusted_content");
        }

        if (!AreArgumentsValid(intent, arguments))
        {
            return new(LocalIntentRoutingStatus.Rejected, "proposal_arguments_invalid");
        }

        return new(
            LocalIntentRoutingStatus.Proposed,
            "proposal_validated",
            new LocalIntentProposal(schemaVersion, intent, arguments));
    }

    private static Dictionary<string, string>? ReadArguments(JsonElement arguments)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in arguments.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String || !result.TryAdd(property.Name, property.Value.GetString() ?? string.Empty))
            {
                return null;
            }
        }

        return result;
    }

    private static bool AreArgumentsValid(LocalIntentName intent, Dictionary<string, string> arguments) => intent switch
    {
        LocalIntentName.OpenApplication => HasExactArguments(arguments, "applicationId") && IsIdentifier(arguments["applicationId"]),
        LocalIntentName.MediaControl => HasExactArguments(arguments, "command") && arguments["command"] is "play" or "pause" or "stop" or "next" or "previous",
        LocalIntentName.AdjustVolume => HasExactArguments(arguments, "direction") && arguments["direction"] is "up" or "down",
        LocalIntentName.FindFile => HasExactArguments(arguments, "query") && IsSearchQuery(arguments["query"]),
        _ => false
    };

    private static bool HasExactProperties(JsonElement objectElement, params string[] expected) =>
        objectElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static bool HasExactArguments(Dictionary<string, string> arguments, string expected) =>
        arguments.Count == 1 && arguments.ContainsKey(expected);

    private static bool IsIdentifier(string value) =>
        value.Length is > 0 and <= 64
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static bool IsSearchQuery(string value) =>
        value.Length is > 0 and <= 120 && value.All(character => !char.IsControl(character));

    private static bool IsPromptInjectionLike(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Contains("ignore previous", StringComparison.Ordinal)
            || normalized.Contains("ignore all", StringComparison.Ordinal)
            || normalized.Contains("system prompt", StringComparison.Ordinal)
            || normalized.Contains("developer message", StringComparison.Ordinal)
            || normalized.Contains("execute shell", StringComparison.Ordinal)
            || normalized.Contains("powershell", StringComparison.Ordinal)
            || normalized.Contains("cmd.exe", StringComparison.Ordinal)
            || normalized.Contains("<|", StringComparison.Ordinal)
            || normalized.Contains("<!--", StringComparison.Ordinal);
    }
}
