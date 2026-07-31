namespace PewPew.Domain.Skills;

/// <summary>
/// Lifecycle states for an automation <see cref="SkillPackage"/>.
/// </summary>
public enum SkillPackageStatus
{
    /// <summary>
    /// Skill package manifest has been submitted but hash verification is pending.
    /// </summary>
    ManifestSubmitted = 1,

    /// <summary>
    /// Hash verification in progress.
    /// </summary>
    VerifyingHash = 2,

    /// <summary>
    /// Skill package hash is verified, capabilities are checked, and skill is enabled for execution.
    /// </summary>
    Enabled = 3,

    /// <summary>
    /// Skill package is manually or policy disabled.
    /// </summary>
    Disabled = 4,

    /// <summary>
    /// Skill package is quarantined due to hash mismatch, capability violation, or security incident.
    /// Execution is permanently blocked until explicitly remediated.
    /// </summary>
    Quarantined = 5
}
