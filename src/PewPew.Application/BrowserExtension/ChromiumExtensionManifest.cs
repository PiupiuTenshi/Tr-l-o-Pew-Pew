namespace PewPew.Application.BrowserExtension;

/// <summary>
/// C# representation of a parsed Chromium Extension <c>manifest.json</c> (Manifest V3).
/// </summary>
public sealed record ChromiumExtensionManifest(
    int ManifestVersion,
    string Name,
    string Version,
    string Description,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> HostPermissions)
{
    public bool IsManifestV3 => ManifestVersion == 3;
}
