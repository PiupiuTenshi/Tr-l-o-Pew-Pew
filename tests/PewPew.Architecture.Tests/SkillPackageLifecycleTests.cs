using PewPew.Domain.Skills;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Unit tests for the <see cref="SkillPackage"/> aggregate root and lifecycle state machine.
/// Verifies hash pinning, capability checks, wildcard capability rejection,
/// enable/disable operations, and quarantine isolation.
/// </summary>
public sealed class SkillPackageLifecycleTests
{
    private static readonly string ValidHash = new('a', 64); // 64 hex chars
    private static readonly string MismatchedHash = new('b', 64);
    private static readonly string[] DefaultCapabilities = ["browser.read_tab", "terminal.run_build"];

    [Fact]
    public void CreateSkillPackageSuccessfullyWithValidManifest()
    {
        var id = EntityId.New();
        var now = DateTimeOffset.UtcNow;
        var package = new SkillPackage(id, "TestSkill", "1.0.0", ValidHash, DefaultCapabilities, now);

        Assert.Equal(id, package.Id);
        Assert.Equal("TestSkill", package.Name);
        Assert.Equal("1.0.0", package.Version);
        Assert.Equal(ValidHash, package.ExpectedSha256Hash);
        Assert.Equal(SkillPackageStatus.ManifestSubmitted, package.Status);
        Assert.Equal(2, package.DeclaredCapabilities.Count);
        Assert.Null(package.QuarantineReason);
    }

    [Theory]
    [InlineData("", "1.0.0")]
    [InlineData("   ", "1.0.0")]
    [InlineData("TestSkill", "")]
    [InlineData("TestSkill", "  ")]
    public void CreateSkillPackageRejectsEmptyNameOrVersion(string name, string version)
    {
        Assert.Throws<ArgumentException>(
            () => new SkillPackage(EntityId.New(), name, version, ValidHash, DefaultCapabilities));
    }

    [Theory]
    [InlineData("invalid-hash")]
    [InlineData("12345")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")] // non-hex
    public void CreateSkillPackageRejectsInvalidHashFormat(string invalidHash)
    {
        Assert.Throws<ArgumentException>(
            () => new SkillPackage(EntityId.New(), "TestSkill", "1.0.0", invalidHash, DefaultCapabilities));
    }

    [Fact]
    public void CreateSkillPackageRejectsEmptyCapabilitiesList()
    {
        Assert.Throws<ArgumentException>(
            () => new SkillPackage(EntityId.New(), "TestSkill", "1.0.0", ValidHash, []));
    }

    [Fact]
    public void CreateSkillPackageRejectsWildcardCapability()
    {
        string[] capabilitiesWithWildcard = ["browser.read_tab", "*"];

        var ex = Assert.Throws<ArgumentException>(
            () => new SkillPackage(EntityId.New(), "TestSkill", "1.0.0", ValidHash, capabilitiesWithWildcard));

        Assert.Contains("Wildcard", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifyHashMatchingHashEnablesSkillPackage()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;

        var result = package.VerifyHash(ValidHash, now);

        Assert.True(result);
        Assert.Equal(SkillPackageStatus.Enabled, package.Status);
        Assert.Equal(now, package.VerifiedAtUtc);
        Assert.Null(package.QuarantineReason);
    }

    [Fact]
    public void VerifyHashMismatchQuarantinesSkillPackage()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;

        var result = package.VerifyHash(MismatchedHash, now);

        Assert.False(result);
        Assert.Equal(SkillPackageStatus.Quarantined, package.Status);
        Assert.NotNull(package.QuarantineReason);
        Assert.Contains("Hash mismatch", package.QuarantineReason);
    }

    [Fact]
    public void CanExecuteCapabilityReturnsTrueOnlyWhenEnabledAndDeclared()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;
        package.VerifyHash(ValidHash, now); // Moves to Enabled

        Assert.True(package.CanExecuteCapability("browser.read_tab"));
        Assert.True(package.CanExecuteCapability("terminal.run_build"));
        Assert.False(package.CanExecuteCapability("system.admin_access")); // Undeclared
    }

    [Fact]
    public void CanExecuteCapabilityReturnsFalseWhenDisabledOrQuarantined()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;
        package.VerifyHash(ValidHash, now);

        // Disable
        package.Disable("Maintenance");
        Assert.False(package.CanExecuteCapability("browser.read_tab"));

        // Quarantine
        package.Quarantine("Security breach", now);
        Assert.False(package.CanExecuteCapability("browser.read_tab"));
    }

    [Fact]
    public void QuarantineTriggersFromAnyStatusAndBlocksEnable()
    {
        var package = CreatePackage(); // Status: ManifestSubmitted
        var now = DateTimeOffset.UtcNow;

        package.Quarantine("Suspicious behavior", now);

        Assert.Equal(SkillPackageStatus.Quarantined, package.Status);
        Assert.Equal("Suspicious behavior", package.QuarantineReason);

        // Enabling a quarantined package must throw InvalidOperationException
        var ex = Assert.Throws<InvalidOperationException>(() => package.Enable(now));
        Assert.Contains("quarantined", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisableAndReEnableWorkflow()
    {
        var package = CreatePackage();
        var now = DateTimeOffset.UtcNow;
        package.VerifyHash(ValidHash, now); // Status: Enabled

        package.Disable("User request");
        Assert.Equal(SkillPackageStatus.Disabled, package.Status);

        package.Enable(now.AddMinutes(1));
        Assert.Equal(SkillPackageStatus.Enabled, package.Status);
    }

    private static SkillPackage CreatePackage() => new(
        EntityId.New(),
        "TestAutomationSkill",
        "1.0.0",
        ValidHash,
        DefaultCapabilities);
}
