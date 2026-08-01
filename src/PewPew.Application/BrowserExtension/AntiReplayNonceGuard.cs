namespace PewPew.Application.BrowserExtension;

/// <summary>
/// Anti-replay guard that tracks message nonces and enforces maximum timestamp drift tolerance
/// to prevent replay attacks over the Desktop–Extension bridge.
/// </summary>
public sealed class AntiReplayNonceGuard
{
    private static readonly TimeSpan DefaultMaxAllowedDrift = TimeSpan.FromSeconds(60);

    private const int DefaultMaximumTrackedNonces = 4_096;
    private readonly Dictionary<string, DateTimeOffset> _seenNonces = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public TimeSpan MaxAllowedDrift { get; }
    public int MaximumTrackedNonces { get; }

    public AntiReplayNonceGuard(TimeSpan? maxAllowedDrift = null, int maximumTrackedNonces = DefaultMaximumTrackedNonces)
    {
        MaxAllowedDrift = maxAllowedDrift ?? DefaultMaxAllowedDrift;
        MaximumTrackedNonces = maximumTrackedNonces > 0
            ? maximumTrackedNonces
            : throw new ArgumentOutOfRangeException(nameof(maximumTrackedNonces));
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
            foreach (var expiredNonce in _seenNonces.Where(pair => pair.Value <= nowUtc).Select(pair => pair.Key).ToArray())
            {
                _seenNonces.Remove(expiredNonce);
            }

            var normalized = nonce.Trim();
            if (_seenNonces.ContainsKey(normalized))
            {
                failureReason = "replay_attack_detected: Replayed nonce rejected by security policy";
                return false;
            }

            if (_seenNonces.Count >= MaximumTrackedNonces)
            {
                failureReason = "nonce_capacity_exceeded: Too many unexpired bridge requests";
                return false;
            }

            _seenNonces.Add(normalized, nowUtc.Add(MaxAllowedDrift));
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
