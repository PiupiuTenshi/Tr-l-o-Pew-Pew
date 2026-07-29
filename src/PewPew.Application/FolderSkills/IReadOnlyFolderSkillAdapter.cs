using PewPew.Application.IntentRouting;

namespace PewPew.Application.FolderSkills;

public sealed record AllowedFolderRoot(string Id, string FullPath)
{
    public static AllowedFolderRoot Create(string id, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(fullPath) || !Path.IsPathFullyQualified(fullPath))
        {
            throw new ArgumentException("An absolute, named allowed folder root is required.");
        }

        return new(id, Path.TrimEndingDirectorySeparator(Path.GetFullPath(fullPath)));
    }
}

public enum ReadOnlyFolderOperation { Find, Open }

/// <summary>
/// Structured local folder command. The root is selected by trusted local
/// configuration; model output provides only a bounded file-name query.
/// </summary>
public sealed record ReadOnlyFolderCommand(
    ReadOnlyFolderOperation Operation,
    AllowedFolderRoot Root,
    string RelativePathOrQuery)
{
    public const string SkillName = "find_file";

    public static bool TryCreateFind(
        LocalIntentProposal proposal,
        AllowedFolderRoot root,
        out ReadOnlyFolderCommand? command)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(root);

        command = proposal.Intent == LocalIntentName.FindFile
            && proposal.Arguments.TryGetValue("query", out var query)
            && IsSafeQuery(query)
                ? new(ReadOnlyFolderOperation.Find, root, query)
                : null;
        return command is not null;
    }

    public static ReadOnlyFolderCommand CreateOpen(AllowedFolderRoot root, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!IsSafeRelativePath(relativePath))
        {
            throw new ArgumentException("Only a non-traversing relative path is allowed.", nameof(relativePath));
        }

        return new(ReadOnlyFolderOperation.Open, root, relativePath);
    }

    public static bool IsSafeRelativePath(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 260
        && !Path.IsPathFullyQualified(value)
        && !value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "." or ".." or "");

    private static bool IsSafeQuery(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 120
        && value.All(character => char.IsLetterOrDigit(character) || character is ' ' or '.' or '-' or '_');
}

public sealed record ReadOnlyFolderEntry(string RelativePath);

public static class ReadOnlyFolderPathGuards
{
    public static bool IsReparsePoint(FileAttributes attributes) =>
        (attributes & FileAttributes.ReparsePoint) != 0;
}

public sealed record ReadOnlyFolderAdapterResult(
    bool IsVerified,
    IReadOnlyList<ReadOnlyFolderEntry> Entries,
    string Evidence,
    string? FailureReason = null)
{
    public static ReadOnlyFolderAdapterResult Verified(IReadOnlyList<ReadOnlyFolderEntry> entries, string evidence) =>
        new(true, entries ?? throw new ArgumentNullException(nameof(entries)), RequireText(evidence, nameof(evidence)));

    public static ReadOnlyFolderAdapterResult Failed(string reason) =>
        new(false, Array.Empty<ReadOnlyFolderEntry>(), "folder_adapter_failure", RequireText(reason, nameof(reason)));

    private static string RequireText(string value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("A non-empty result value is required.", parameterName);
}

public interface IReadOnlyFolderSkillAdapter
{
    Task<ReadOnlyFolderAdapterResult> ExecuteAsync(
        AuthorizedReadOnlyFolderCommand command,
        CancellationToken cancellationToken);
}

/// <summary>
/// Application-only capability token. Presentation and model code cannot
/// construct a folder adapter request directly.
/// </summary>
public sealed class AuthorizedReadOnlyFolderCommand
{
    internal AuthorizedReadOnlyFolderCommand(ReadOnlyFolderCommand command) =>
        Command = command ?? throw new ArgumentNullException(nameof(command));

    public ReadOnlyFolderCommand Command { get; }
}
