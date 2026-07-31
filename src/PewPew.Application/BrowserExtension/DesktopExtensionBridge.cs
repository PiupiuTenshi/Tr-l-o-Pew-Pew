using System.Security.Cryptography;

namespace PewPew.Application.BrowserExtension;

/// <summary>
/// Authenticated Desktop–Extension Bridge service providing session token issuance,
/// caller authentication, anti-replay nonce validation, origin policy verification,
/// and disconnect cleanup.
/// </summary>
public sealed class DesktopExtensionBridge
{
    private static readonly TimeSpan DefaultTokenTtl = TimeSpan.FromMinutes(15);

    private readonly ExtensionOriginPolicyValidator _originValidator;
    private readonly AntiReplayNonceGuard _nonceGuard;
    private readonly object _lock = new();

    private string? _activeSessionToken;
    private DateTimeOffset _tokenExpiresAtUtc = DateTimeOffset.MinValue;
    private bool _isConnected;

    public bool IsConnected
    {
        get
        {
            lock (_lock)
            {
                return _isConnected;
            }
        }
    }


    public DesktopExtensionBridge(
        ExtensionOriginPolicyValidator? originValidator = null,
        AntiReplayNonceGuard? nonceGuard = null)
    {
        _originValidator = originValidator ?? new ExtensionOriginPolicyValidator();
        _nonceGuard = nonceGuard ?? new AntiReplayNonceGuard();
    }

    /// <summary>
    /// Generates and activates a new cryptographically secure session token for the extension caller.
    /// Invalidates any previous token and clears tracked nonces.
    /// </summary>
    public string IssueSessionToken(DateTimeOffset nowUtc, TimeSpan? ttl = null)
    {
        lock (_lock)
        {
            var activeTtl = ttl ?? DefaultTokenTtl;
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = "ext_token_" + Convert.ToHexStringLower(tokenBytes);

            _activeSessionToken = token;
            _tokenExpiresAtUtc = nowUtc.Add(activeTtl);
            _isConnected = true;
            _nonceGuard.Clear();

            return token;
        }
    }

    /// <summary>
    /// Checks whether the given session token is active, unexpired, and matches the connected extension session.
    /// </summary>
    public bool IsTokenValid(string? token, DateTimeOffset nowUtc)
    {
        lock (_lock)
        {
            if (!_isConnected || string.IsNullOrWhiteSpace(_activeSessionToken) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (nowUtc >= _tokenExpiresAtUtc)
            {
                return false;
            }

            return string.Equals(_activeSessionToken, token.Trim(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Processes an incoming <see cref="ExtensionBridgeRequest"/> after validating session token,
    /// anti-replay nonce, timestamp drift, and target origin policy.
    /// </summary>
    public ExtensionBridgeResponse ProcessRequest(ExtensionBridgeRequest request, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_lock)
        {
            // 1. Authenticate caller token
            if (!IsTokenValid(request.SessionToken, nowUtc))
            {
                return ExtensionBridgeResponse.Failed("unauthorized_caller_token");
            }

            // 2. Anti-replay nonce & timestamp drift check
            if (!_nonceGuard.TryValidateAndRegisterNonce(request.Nonce, request.TimestampUtc, nowUtc, out var nonceReason))
            {
                return ExtensionBridgeResponse.Failed(nonceReason ?? "replay_attack_detected");
            }

            // 3. Origin policy check
            if (!_originValidator.IsOriginAllowed(request.TargetOrigin))
            {
                return ExtensionBridgeResponse.Failed("origin_policy_denied");
            }

            // 4. Request validated
            return ExtensionBridgeResponse.Success($"bridge_verified: Command = '{request.Command}', Origin = '{request.TargetOrigin}'");
        }
    }

    /// <summary>
    /// Instantly disconnects the extension bridge, invalidates the active session token,
    /// and purges tracked nonces.
    /// </summary>
    public void Disconnect()
    {
        lock (_lock)
        {
            _isConnected = false;
            _activeSessionToken = null;
            _tokenExpiresAtUtc = DateTimeOffset.MinValue;
            _nonceGuard.Clear();
        }
    }
}
