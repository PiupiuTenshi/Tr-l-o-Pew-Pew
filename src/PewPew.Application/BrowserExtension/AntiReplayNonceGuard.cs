namespace PewPew.Application.BrowserExtension;

/// <summary>
/// Anti-replay guard that tracks message nonces and enforces maximum timestamp drift tolerance
/// to prevent replay attacks over the Desktop–Extension bridge.
/// </summary>
public sealed class AntiReplayNonceGuard
{
    private static readonly TimeSpan DefaultMaxAllowedDrift = TimeSpan.FromSeconds(60);

    private readonly HashSet<string> _seenNonces = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public TimeSpan MaxAllowedDrift { get; }

    public AntiReplayNonceGuard(TimeSpan? maxAllowedDrift = null)
    {
        MaxAllowedDrift = maxAllowedDrift ?? DefaultMaxAllowedDrift;
    }

    /// <summary>
    /// Validates a request nonce and timestamp against replay attacks and clock drift.
    /// Returns true if valid and registers the nonce; returns false if replayed or drifted.
    /// </summary>
    public bool TryValidateAndRegisterNonce(
        string nonce,
        DateTimeOffset timestampUtc,
        DateTimeOffset nowUtc,
        out string? failureReason)
    {
        if (string.IsNullOrWhiteSpace(nonce))
        {
            failureReason = "invalid_nonce: Nonce cannot be empty";
            return false;
        }

        var drift = (nowUtc - timestampUtc).Duration();
        if (drift > MaxAllowedDrift)
        {
            failureReason = $"timestamp_drift_exceeded: Message drift of {drift.TotalSeconds:F1}s exceeds max allowed {MaxAllowedDrift.TotalSeconds:F1}s";
            return false;
        }

        lock (_lock)
        {
            var normalized = nonce.Trim();
            if (_seenNonces.Contains(normalized))
            {
                failureReason = "replay_attack_detected: Replayed nonce rejected by security policy";
                return false;
            }

            _seenNonces.Add(normalized);
        }

        failureReason = null;
        return true;
    }

    /// <summary>
    /// Clears all tracked nonces upon extension disconnect or session reset.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _seenNonces.Clear();
        }
    }
}
