using System.Diagnostics;
using PewPew.Application.FolderSkills;

namespace PewPew.Desktop;

/// <summary>
/// Windows filesystem boundary for a configured read-only folder root. It does
/// not follow reparse points and never accepts an absolute or traversal path.
/// </summary>
public sealed class WindowsReadOnlyFolderSkillAdapter : IReadOnlyFolderSkillAdapter
{
    private const int MaximumResults = 20;

    public Task<ReadOnlyFolderAdapterResult> ExecuteAsync(
        AuthorizedReadOnlyFolderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var folderCommand = command.Command;
        return folderCommand.Operation switch
        {
            ReadOnlyFolderOperation.Find => Task.FromResult(Find(folderCommand, cancellationToken)),
            ReadOnlyFolderOperation.Open => Task.FromResult(Open(folderCommand, cancellationToken)),
            _ => Task.FromResult(ReadOnlyFolderAdapterResult.Failed("folder_operation_not_allowlisted"))
        };
    }

    private static ReadOnlyFolderAdapterResult Find(ReadOnlyFolderCommand command, CancellationToken cancellationToken)
    {
        if (!TryValidateRoot(command.Root, out var root))
        {
            return ReadOnlyFolderAdapterResult.Failed("folder_root_not_available");
        }

        var results = new List<ReadOnlyFolderEntry>();
        try
        {
            FindRecursive(root, root, command.RelativePathOrQuery, results, cancellationToken);
            return ReadOnlyFolderAdapterResult.Verified(results, "folder_find_verified");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return ReadOnlyFolderAdapterResult.Failed("folder_read_denied");
        }
        catch (IOException)
        {
            return ReadOnlyFolderAdapterResult.Failed("folder_read_failed");
        }
    }

    private static void FindRecursive(
        string root,
        string directory,
        string query,
        List<ReadOnlyFolderEntry> results,
        CancellationToken cancellationToken)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (results.Count >= MaximumResults)
            {
                return;
            }

            var fullPath = Path.GetFullPath(entry);
            if (!IsWithinRoot(root, fullPath) || IsReparsePoint(fullPath))
            {
                continue;
            }

            if (Directory.Exists(fullPath))
            {
                FindRecursive(root, fullPath, query, results, cancellationToken);
                continue;
            }

            if (Path.GetFileName(fullPath).Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new ReadOnlyFolderEntry(Path.GetRelativePath(root, fullPath)));
            }
        }
    }

    private static ReadOnlyFolderAdapterResult Open(ReadOnlyFolderCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryValidateRoot(command.Root, out var root)
            || !ReadOnlyFolderCommand.IsSafeRelativePath(command.RelativePathOrQuery))
        {
            return ReadOnlyFolderAdapterResult.Failed("folder_path_not_allowed");
        }

        var candidate = Path.GetFullPath(Path.Combine(root, command.RelativePathOrQuery));
        if (!IsWithinRoot(root, candidate) || HasReparsePointInPath(root, candidate) || !File.Exists(candidate))
        {
            return ReadOnlyFolderAdapterResult.Failed("folder_path_not_allowed");
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(candidate) { UseShellExecute = true });
            return process is not null && !process.HasExited
                ? ReadOnlyFolderAdapterResult.Verified(Array.Empty<ReadOnlyFolderEntry>(), "folder_open_verified")
                : ReadOnlyFolderAdapterResult.Failed("folder_open_not_verified");
        }
        catch (Exception)
        {
            return ReadOnlyFolderAdapterResult.Failed("folder_open_failed");
        }
    }

    private static bool TryValidateRoot(AllowedFolderRoot configuredRoot, out string root)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot.FullPath));
        return Directory.Exists(root) && !IsReparsePoint(root);
    }

    private static bool IsWithinRoot(string root, string candidate) =>
        candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool HasReparsePointInPath(string root, string candidate)
    {
        var current = root;
        var relative = Path.GetRelativePath(root, candidate);
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (IsReparsePoint(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReparsePoint(string path) =>
        ReadOnlyFolderPathGuards.IsReparsePoint(File.GetAttributes(path));
}
