using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Terminal;

/// <summary>
/// Aggregate root representing a versioned, hash-pinned, policy-approved structured terminal workflow definition.
/// Enforces structured executable validation, parameter placeholder bounds, SHA-256 integrity verification,
/// and lifecycle state transitions.
/// </summary>
public sealed class TerminalWorkflowDefinition
{
    private static readonly Regex Sha256HexRegex = new("^[a-fA-F0-9]{64}$", RegexOptions.Compiled);

    private static readonly HashSet<string> ForbiddenExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "cmd.exe",
        "powershell", "powershell.exe", "pwsh", "pwsh.exe",
        "bash", "bash.exe", "sh", "sh.exe", "zsh", "zsh.exe", "wsl", "wsl.exe",
        "regedit", "regedit.exe", "reg", "reg.exe",
        "vssadmin", "vssadmin.exe",
        "format", "format.com", "diskpart", "diskpart.exe",
        "bcdedit", "bcdedit.exe", "net", "net.exe", "schtasks", "schtasks.exe"
    };

    private static readonly HashSet<string> AllowedExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet", "dotnet.exe",
        "git", "git.exe",
        "npm", "npm.cmd", "npm.exe", "npx", "npx.cmd", "npx.exe",
        "cargo", "cargo.exe",
        "go", "go.exe",
        "python", "python.exe", "pytest", "pytest.exe"
    };

    private static readonly SearchValues<char> ShellOperators = SearchValues.Create(['|', ';', '&', '$', '`', '>', '<', '\n', '\r']);

    private readonly List<string> _fixedArguments;
    private readonly List<string> _allowedPlaceholders;

    public EntityId Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string ExecutablePath { get; }
    public string WorkingDirectoryRoot { get; }
    public string ExpectedSha256Hash { get; }
    public TerminalWorkflowStatus Status { get; private set; }
    public TerminalWorkflowRiskLevel RiskLevel { get; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<string> FixedArguments => _fixedArguments.AsReadOnly();
    public IReadOnlyList<string> AllowedPlaceholders => _allowedPlaceholders.AsReadOnly();

    public TerminalWorkflowDefinition(
        EntityId id,
        string name,
        string version,
        string executablePath,
        string workingDirectoryRoot,
        IEnumerable<string> fixedArguments,
        IEnumerable<string> allowedPlaceholders,
        TerminalWorkflowRiskLevel riskLevel,
        string expectedSha256Hash,
        DateTimeOffset? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Workflow name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Workflow version cannot be empty.", nameof(version));
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be empty.", nameof(executablePath));
        }

        var normalizedExe = Path.GetFileName(executablePath.Trim());
        if (ForbiddenExecutables.Contains(normalizedExe) || ForbiddenExecutables.Contains(executablePath.Trim()))
        {
            throw new ArgumentException($"Executable '{executablePath}' is a prohibited shell interpreter or unsafe administrative binary.", nameof(executablePath));
        }

        if (!AllowedExecutables.Contains(normalizedExe) && !AllowedExecutables.Contains(executablePath.Trim()))
        {
            throw new ArgumentException($"Executable '{executablePath}' is not in the approved terminal executable allowlist.", nameof(executablePath));
        }

        if (string.IsNullOrWhiteSpace(workingDirectoryRoot))
        {
            throw new ArgumentException("Working directory root cannot be empty.", nameof(workingDirectoryRoot));
        }

        if (riskLevel == TerminalWorkflowRiskLevel.Critical)
        {
            throw new ArgumentException("Terminal workflows with Critical risk rating are strictly prohibited.", nameof(riskLevel));
        }

        if (string.IsNullOrWhiteSpace(expectedSha256Hash) || !Sha256HexRegex.IsMatch(expectedSha256Hash))
        {
            throw new ArgumentException("Expected SHA-256 hash must be a 64-character hex string.", nameof(expectedSha256Hash));
        }

        ArgumentNullException.ThrowIfNull(fixedArguments);
        ArgumentNullException.ThrowIfNull(allowedPlaceholders);

        var argsList = fixedArguments.Select(a => a?.Trim()).Where(a => !string.IsNullOrEmpty(a)).ToList();
        foreach (var arg in argsList)
        {
            if (arg!.AsSpan().IndexOfAny(ShellOperators) >= 0)
            {
                throw new ArgumentException($"Fixed argument '{arg}' contains prohibited shell operator characters.", nameof(fixedArguments));
            }
        }

        var placeholderList = allowedPlaceholders.Select(p => p?.Trim()).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
        foreach (var p in placeholderList)
        {
            if (p!.AsSpan().IndexOfAny(ShellOperators) >= 0)
            {
                throw new ArgumentException($"Placeholder '{p}' contains prohibited shell operator characters.", nameof(allowedPlaceholders));
            }
        }

        Id = id;
        Name = name.Trim();
        Version = version.Trim();
        ExecutablePath = executablePath.Trim();
        WorkingDirectoryRoot = workingDirectoryRoot.Trim();
        _fixedArguments = argsList!;
        _allowedPlaceholders = placeholderList!;
        RiskLevel = riskLevel;
        ExpectedSha256Hash = expectedSha256Hash.ToLowerInvariant();
        Status = TerminalWorkflowStatus.Draft;

        var now = createdAtUtc ?? DateTimeOffset.UtcNow;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Submits the draft workflow definition for policy review and hash verification.
    /// </summary>
    public void Submit(DateTimeOffset nowUtc)
    {
        RequireStatus(TerminalWorkflowStatus.Draft, "submit");

        Status = TerminalWorkflowStatus.Submitted;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Approves the submitted workflow if the provided computed SHA-256 hash matches <see cref="ExpectedSha256Hash"/>.
    /// If hash mismatch occurs, automatically rejects the workflow.
    /// </summary>
    public bool Approve(string computedSha256Hash, DateTimeOffset nowUtc)
    {
        RequireStatus(TerminalWorkflowStatus.Submitted, "approve");

        if (string.IsNullOrWhiteSpace(computedSha256Hash))
        {
            Reject("Hash verification failed: computed hash was empty.", nowUtc);
            return false;
        }

        var normalizedComputed = computedSha256Hash.Trim().ToLowerInvariant();
        if (normalizedComputed != ExpectedSha256Hash)
        {
            Reject($"Hash mismatch: expected '{ExpectedSha256Hash}', computed '{normalizedComputed}'.", nowUtc);
            return false;
        }

        Status = TerminalWorkflowStatus.PolicyApproved;
        RejectionReason = null;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    /// <summary>
    /// Activates an approved workflow for execution by worker runner.
    /// </summary>
    public void Activate(DateTimeOffset nowUtc)
    {
        if (Status != TerminalWorkflowStatus.PolicyApproved && Status != TerminalWorkflowStatus.Active)
        {
            throw new InvalidOperationException($"Cannot activate workflow from status '{Status}'. Must be PolicyApproved.");
        }

        Status = TerminalWorkflowStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Rejects the workflow definition with a reason, blocking execution permanently.
    /// </summary>
    public void Reject(string reason, DateTimeOffset nowUtc)
    {

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Rejection reason cannot be empty.", nameof(reason));
        }

        if (Status == TerminalWorkflowStatus.Archived)
        {
            throw new InvalidOperationException("Cannot reject an archived workflow.");
        }

        Status = TerminalWorkflowStatus.PolicyRejected;
        RejectionReason = reason.Trim();
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Archives the workflow definition, preventing future executions.
    /// </summary>
    public void Archive(DateTimeOffset nowUtc)
    {
        Status = TerminalWorkflowStatus.Archived;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Verifies the provided computed SHA-256 hash.
    /// If mismatch occurs and workflow is active/approved, automatically rejects it.
    /// </summary>
    public bool VerifyHash(string computedSha256Hash, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(computedSha256Hash))
        {
            Reject("SHA-256 hash verification failed: computed hash was empty.", nowUtc);
            return false;
        }

        var normalizedComputed = computedSha256Hash.Trim().ToLowerInvariant();
        if (normalizedComputed != ExpectedSha256Hash)
        {
            Reject($"SHA-256 hash mismatch: expected '{ExpectedSha256Hash}', computed '{normalizedComputed}'.", nowUtc);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Calculates the deterministic SHA-256 hash for a workflow's metadata and structure.
    /// </summary>
    public static string CalculateSha256Hash(
        string executablePath,
        IEnumerable<string> fixedArguments,
        IEnumerable<string> allowedPlaceholders,
        string version)
    {
        var exe = executablePath?.Trim() ?? string.Empty;
        var ver = version?.Trim() ?? string.Empty;
        var args = string.Join("|", (fixedArguments ?? Enumerable.Empty<string>()).Select(a => a?.Trim() ?? string.Empty));
        var placeholders = string.Join("|", (allowedPlaceholders ?? Enumerable.Empty<string>()).Select(p => p?.Trim() ?? string.Empty));

        var payload = $"{exe.ToLowerInvariant()};{ver.ToLowerInvariant()};{args};{placeholders}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(bytes);
    }

    private void RequireStatus(TerminalWorkflowStatus requiredStatus, string operation)
    {
        if (Status != requiredStatus)
        {
            throw new InvalidOperationException($"Cannot {operation} workflow from status '{Status}'. Required status: '{requiredStatus}'.");
        }
    }
}
