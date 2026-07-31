using System.Text.Json;
using PewPew.Application.BrowserExtension;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Unit & security tests for Chromium Extension Manifest V3 baseline and origin policy validation.
/// Verifies Manifest V3 compliance, minimal permissions, wildcard permission rejection,
/// prohibited scheme blocklisting (<c>file://</c>, <c>chrome://</c>), and disk manifest integrity.
/// </summary>
public sealed class ChromiumExtensionBaselineTests
{
    [Fact]
    public void ValidateManifestAcceptsValidManifestV3()
    {
        var manifest = new ChromiumExtensionManifest(
            ManifestVersion: 3,
            Name: "Pew Pew Assistant Bridge",
            Version: "1.0.0",
            Description: "Security-bound bridge",
            Permissions: ["activeTab", "scripting", "storage"],
            HostPermissions: ["https://*/*"]);

        var valid = ExtensionOriginPolicyValidator.ValidateManifest(manifest, out var failureReason);

        Assert.True(valid);
        Assert.Null(failureReason);
        Assert.True(manifest.IsManifestV3);
    }

    [Fact]
    public void ValidateManifestRejectsManifestV2()
    {
        var manifest = new ChromiumExtensionManifest(
            ManifestVersion: 2,
            Name: "Legacy Extension",
            Version: "1.0.0",
            Description: "Legacy V2",
            Permissions: ["activeTab"],
            HostPermissions: []);

        var valid = ExtensionOriginPolicyValidator.ValidateManifest(manifest, out var failureReason);

        Assert.False(valid);
        Assert.Contains("manifest_version_invalid", failureReason);
    }

    [Fact]
    public void ValidateManifestRejectsWildcardPermission()
    {
        var manifestWithWildcardPermission = new ChromiumExtensionManifest(
            ManifestVersion: 3,
            Name: "Dangerous Extension",
            Version: "1.0.0",
            Description: "Wildcard permission",
            Permissions: ["activeTab", "<all_urls>"],
            HostPermissions: []);

        var valid = ExtensionOriginPolicyValidator.ValidateManifest(manifestWithWildcardPermission, out var failureReason);

        Assert.False(valid);
        Assert.Contains("manifest_wildcard_permission_prohibited", failureReason);
    }

    [Fact]
    public void ValidateManifestRejectsWildcardHostPermission()
    {
        var manifestWithWildcardHost = new ChromiumExtensionManifest(
            ManifestVersion: 3,
            Name: "Dangerous Extension",
            Version: "1.0.0",
            Description: "Wildcard host permission",
            Permissions: ["activeTab"],
            HostPermissions: ["<all_urls>"]);

        var valid = ExtensionOriginPolicyValidator.ValidateManifest(manifestWithWildcardHost, out var failureReason);

        Assert.False(valid);
        Assert.Contains("manifest_wildcard_host_permission_prohibited", failureReason);
    }

    [Fact]
    public void IsOriginAllowedAllowsHttps()
    {
        var validator = new ExtensionOriginPolicyValidator();

        Assert.True(validator.IsOriginAllowed("https://example.com/page"));
        Assert.True(validator.IsOriginAllowed("https://github.com"));
        Assert.False(validator.IsOriginAllowed("http://example.com")); // Plain HTTP denied by default
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("chrome://settings")]
    [InlineData("chrome-extension://abcdefghijklmnopqrstuvwxyz/popup.html")]
    [InlineData("about:blank")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<h1>Hack</h1>")]
    public void IsOriginAllowedRejectsProhibitedSchemes(string prohibitedUrl)
    {
        var validator = new ExtensionOriginPolicyValidator();

        var allowed = validator.IsOriginAllowed(prohibitedUrl);

        Assert.False(allowed);
    }

    [Fact]
    public void IsOriginAllowedEnforcesScopedOriginAllowlist()
    {
        var validator = new ExtensionOriginPolicyValidator(allowedOrigins: ["https://trusted.com"]);

        Assert.True(validator.IsOriginAllowed("https://trusted.com/api/data"));
        Assert.False(validator.IsOriginAllowed("https://untrusted.com"));

        // Adding explicit origin
        var added = validator.AddAllowedOrigin("https://app.dev");
        Assert.True(added);
        Assert.True(validator.IsOriginAllowed("https://app.dev/dashboard"));

        // Attempting to add prohibited scheme 'file://' must return false
        var addedFileScheme = validator.AddAllowedOrigin("file:///secret.txt");
        Assert.False(addedFileScheme);
    }

    [Fact]
    public void ExtensionManifestFileOnDiskIsValid()
    {
        // Locate src/extension/manifest.json relative to solution directory
        var solutionDir = FindSolutionDirectory();
        var manifestPath = Path.Combine(solutionDir, "src", "extension", "manifest.json");

        Assert.True(File.Exists(manifestPath), $"manifest.json missing at: {manifestPath}");

        var json = File.ReadAllText(manifestPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var manifestVersion = root.GetProperty("manifest_version").GetInt32();
        var name = root.GetProperty("name").GetString()!;
        var version = root.GetProperty("version").GetString()!;
        var description = root.GetProperty("description").GetString()!;

        var permissions = root.GetProperty("permissions").EnumerateArray().Select(e => e.GetString()!).ToList();
        var hostPermissions = root.GetProperty("host_permissions").EnumerateArray().Select(e => e.GetString()!).ToList();

        var manifest = new ChromiumExtensionManifest(manifestVersion, name, version, description, permissions, hostPermissions);

        var valid = ExtensionOriginPolicyValidator.ValidateManifest(manifest, out var failureReason);

        Assert.True(valid, $"Disk manifest failed validation: {failureReason}");
        Assert.Equal(3, manifestVersion);
        Assert.DoesNotContain("<all_urls>", permissions);
        Assert.DoesNotContain("<all_urls>", hostPermissions);
    }

    private static string FindSolutionDirectory()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "PewPew.sln")))
        {
            currentDir = currentDir.Parent;
        }

        return currentDir?.FullName ?? throw new InvalidOperationException("Could not locate solution directory containing PewPew.sln");
    }
}
