namespace PewPew.Application.BrowserExtension;

/// <summary>
/// Security policy validator for Chromium browser extension manifest & origin permissions.
/// Enforces Manifest V3 compliance, minimal permission principle, wildcard origin rejection,
/// and scheme blocklisting (<c>file://</c>, <c>chrome://</c>, <c>about:</c>).
/// </summary>
public sealed class ExtensionOriginPolicyValidator
{
    private static readonly HashSet<string> ProhibitedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "file", "chrome", "chrome-extension", "about", "edge", "javascript", "data"
    };

    private readonly HashSet<string> _allowedOrigins = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _allowHttpLocalhost;

    public ExtensionOriginPolicyValidator(IEnumerable<string>? allowedOrigins = null, bool allowHttpLocalhost = false)
    {
        _allowHttpLocalhost = allowHttpLocalhost;

        if (allowedOrigins is not null)
        {
            foreach (var origin in allowedOrigins)
            {
                if (!string.IsNullOrWhiteSpace(origin))
                {
                    _allowedOrigins.Add(origin.Trim().ToLowerInvariant());
                }
            }
        }
    }

    /// <summary>
    /// Validates a parsed extension manifest for Manifest V3 compliance and security rules.
    /// Rejects manifests containing wildcard origin grants (<c>&lt;all_urls&gt;</c>) or prohibited permissions.
    /// </summary>
    public static bool ValidateManifest(ChromiumExtensionManifest manifest, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (!manifest.IsManifestV3)
        {
            failureReason = "manifest_version_invalid: Extension must use Manifest V3";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.Name) || string.IsNullOrWhiteSpace(manifest.Version))
        {
            failureReason = "manifest_metadata_invalid: Name and version are required";
            return false;
        }

        // Check for wildcard permissions
        if (manifest.Permissions.Any(p => p == "<all_urls>" || p == "*://*/*"))
        {
            failureReason = "manifest_wildcard_permission_prohibited: Wildcard '<all_urls>' permission is denied by security policy";
            return false;
        }

        if (manifest.HostPermissions.Any(p => p == "<all_urls>" || p == "*://*/*"))
        {
            failureReason = "manifest_wildcard_host_permission_prohibited: Wildcard '<all_urls>' host permission is denied by security policy";
            return false;
        }

        failureReason = null;
        return true;
    }

    /// <summary>
    /// Checks whether a given URL or origin is permitted by security policy.
    /// Rejects prohibited schemes (<c>file://</c>, <c>chrome://</c>, <c>about:</c>) and unapproved origins.
    /// </summary>
    public bool IsOriginAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (ProhibitedSchemes.Contains(scheme))
        {
            return false; // Prohibited system/local file scheme
        }

        if (scheme == "http")
        {
            if (_allowHttpLocalhost && (uri.Host == "localhost" || uri.Host == "127.0.0.1"))
            {
                return true;
            }
            return false; // Plain HTTP non-localhost denied
        }

        if (scheme != "https")
        {
            return false;
        }

        // HTTPS scheme — if allowed origins list is populated, enforce match
        if (_allowedOrigins.Count == 0)
        {
            return true; // Default HTTPS allowlist
        }

        var originString = $"{uri.Scheme}://{uri.Authority}".ToLowerInvariant();
        return _allowedOrigins.Contains(originString) || _allowedOrigins.Contains(uri.Host.ToLowerInvariant());
    }

    /// <summary>
    /// Allows registering an explicit HTTPS origin to the validator allowlist.
    /// Returns false if the origin uses a prohibited scheme.
    /// </summary>
    public bool AddAllowedOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            if (ProhibitedSchemes.Contains(uri.Scheme))
            {
                return false;
            }
            _allowedOrigins.Add($"{uri.Scheme}://{uri.Authority}".ToLowerInvariant());
            return true;
        }

        // Domain name registration
        _allowedOrigins.Add(origin.Trim().ToLowerInvariant());
        return true;
    }
}
