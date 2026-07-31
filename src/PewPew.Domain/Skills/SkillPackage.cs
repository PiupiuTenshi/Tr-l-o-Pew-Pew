using System.Text.RegularExpressions;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Skills;

/// <summary>
/// Aggregate root representing an installed or proposed automation skill package.
/// Enforces manifest validation, SHA-256 hash pinning, explicit capability declarations,
/// and security quarantine boundaries.
/// </summary>
public sealed class SkillPackage
{
    private static readonly Regex Sha256HexRegex = new("^[a-fA-F0-9]{64}$", RegexOptions.Compiled);

    private readonly List<string> _declaredCapabilities;

    public EntityId Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string ExpectedSha256Hash { get; }
    public SkillPackageStatus Status { get; private set; }
    public string? QuarantineReason { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public IReadOnlyList<string> DeclaredCapabilities => _declaredCapabilities.AsReadOnly();

    public SkillPackage(
        EntityId id,
        string name,
        string version,
        string expectedSha256Hash,
        IEnumerable<string> declaredCapabilities,
        DateTimeOffset? submittedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Skill package name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Skill package version cannot be empty.", nameof(version));
        }

        if (string.IsNullOrWhiteSpace(expectedSha256Hash) || !Sha256HexRegex.IsMatch(expectedSha256Hash))
        {
            throw new ArgumentException("Expected hash must be a valid 64-character hex SHA-256 string.", nameof(expectedSha256Hash));
        }

        ArgumentNullException.ThrowIfNull(declaredCapabilities);

        var capList = declaredCapabilities.Select(c => c?.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
        if (capList.Count == 0)
        {
            throw new ArgumentException("Skill package must declare at least one capability.", nameof(declaredCapabilities));
        }

        if (capList.Any(c => c == "*"))
        {
            throw new ArgumentException("Wildcard capability '*' is strictly prohibited.", nameof(declaredCapabilities));
        }

        Id = id;
        Name = name.Trim();
        Version = version.Trim();
        ExpectedSha256Hash = expectedSha256Hash.ToLowerInvariant();
        _declaredCapabilities = capList.Select(c => c!.ToLowerInvariant()).Distinct().ToList();
        Status = SkillPackageStatus.ManifestSubmitted;
        SubmittedAtUtc = submittedAtUtc ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Verifies the skill package contents against its declared SHA-256 hash.
    /// If the hash matches, transitions to <see cref="SkillPackageStatus.VerifyingHash"/> and enables the skill.
    /// If the hash mismatches, automatically quarantines the skill package.
    /// </summary>
    public bool VerifyHash(string computedSha256Hash, DateTimeOffset now)
    {
        RequireNotQuarantined();

        if (string.IsNullOrWhiteSpace(computedSha256Hash))
        {
            Quarantine($"Hash verification failed: computed hash was empty.", now);
            return false;
        }

        var normalizedComputed = computedSha256Hash.Trim().ToLowerInvariant();
        if (normalizedComputed != ExpectedSha256Hash)
        {
            Quarantine($"Hash mismatch: expected '{ExpectedSha256Hash}', computed '{normalizedComputed}'.", now);
            return false;
        }

        Status = SkillPackageStatus.VerifyingHash;
        VerifiedAtUtc = now;
        Enable(now);
        return true;
    }

    /// <summary>
    /// Enables the skill package for execution. Requires valid hash verification.
    /// </summary>
    public void Enable(DateTimeOffset now)
    {
        RequireNotQuarantined();

        if (Status != SkillPackageStatus.VerifyingHash && Status != SkillPackageStatus.Disabled)
        {
            throw new InvalidOperationException($"Cannot enable skill package from status '{Status}'. Must be verified or disabled.");
        }

        Status = SkillPackageStatus.Enabled;
        QuarantineReason = null;
    }

    /// <summary>
    /// Manually or policy disables the skill package.
    /// </summary>
    public void Disable(string reason)
    {
        RequireNotQuarantined();

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Disable reason cannot be empty.", nameof(reason));
        }

        Status = SkillPackageStatus.Disabled;
    }

    /// <summary>
    /// Places the skill package in immediate quarantine due to a security violation, hash mismatch,
    /// or capability breach. Once quarantined, execution is permanently blocked.
    /// </summary>
    public void Quarantine(string reason, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Quarantine reason cannot be empty.", nameof(reason));
        }

        Status = SkillPackageStatus.Quarantined;
        QuarantineReason = reason.Trim();
    }

    /// <summary>
    /// Checks whether the skill package is enabled and authorized to execute the specified capability.
    /// Returns false if quarantined, disabled, or if the capability is not in the declared capabilities list.
    /// </summary>
    public bool CanExecuteCapability(string capability)
    {
        if (Status != SkillPackageStatus.Enabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(capability))
        {
            return false;
        }

        var normalizedCap = capability.Trim().ToLowerInvariant();
        return _declaredCapabilities.Contains(normalizedCap);
    }

    private void RequireNotQuarantined()
    {
        if (Status == SkillPackageStatus.Quarantined)
        {
            throw new InvalidOperationException($"Skill package '{Name}' ({Id}) is quarantined: {QuarantineReason}");
        }
    }
}
