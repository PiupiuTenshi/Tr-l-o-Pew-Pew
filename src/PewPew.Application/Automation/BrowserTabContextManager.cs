using PewPew.Domain.Automation;

namespace PewPew.Application.Automation;

/// <summary>
/// Application service managing browser tab contexts and the lifecycle of <see cref="UiTargetSnapshot"/> items.
/// Applies automatic secret redaction, enforces TTL expiration, and handles snapshot invalidation
/// when browser tabs are closed, navigated, or disconnected.
/// </summary>
public sealed class BrowserTabContextManager
{
    private readonly UiTargetRedactor _redactor;
    private readonly Dictionary<Guid, UiTargetSnapshot> _snapshots = new();
    private readonly object _lock = new();

    public BrowserTabContextManager(UiTargetRedactor? redactor = null)
    {
        _redactor = redactor ?? new UiTargetRedactor();
    }

    /// <summary>
    /// Captures, redacts, and registers a new short-lived <see cref="UiTargetSnapshot"/> for a browser tab.
    /// </summary>
    public UiTargetSnapshot CaptureSnapshot(
        string tabId,
        string origin,
        string title,
        string targetSelector,
        string elementText,
        string inputType,
        DateTimeOffset capturedAtUtc,
        TimeSpan? ttl = null)
    {
        var redactedText = UiTargetRedactor.RedactText(elementText, inputType, targetSelector);

        var isRedacted = redactedText != elementText;

        var snapshot = UiTargetSnapshot.Create(
            tabId,
            origin,
            title,
            targetSelector,
            redactedText,
            inputType,
            capturedAtUtc,
            ttl,
            isRedacted);

        lock (_lock)
        {
            _snapshots[snapshot.SnapshotId] = snapshot;
        }

        return snapshot;
    }

    /// <summary>
    /// Gets a target snapshot by ID if active and unexpired.
    /// Returns null if the snapshot is missing, stale, expired, or invalidated.
    /// </summary>
    public UiTargetSnapshot? GetActiveSnapshot(Guid snapshotId, DateTimeOffset nowUtc)
    {
        lock (_lock)
        {
            if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
            {
                return null;
            }

            snapshot.CheckAndApplyExpiry(nowUtc);
            if (snapshot.IsStale(nowUtc))
            {
                return null; // Fail closed on stale/expired target
            }

            return snapshot;
        }
    }

    /// <summary>
    /// Invalidates all active snapshots belonging to a specified tab ID
    /// (e.g. upon tab close, refresh, or URL navigation).
    /// </summary>
    public int InvalidateTabSnapshots(string tabId, string reason)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            return 0;
        }

        var normalizedTabId = tabId.Trim();
        var invalidatedCount = 0;

        lock (_lock)
        {
            foreach (var snapshot in _snapshots.Values)
            {
                if (string.Equals(snapshot.TabId, normalizedTabId, StringComparison.OrdinalIgnoreCase) &&
                    snapshot.Status == UiTargetSnapshotStatus.Active)
                {
                    snapshot.Invalidate(reason);
                    invalidatedCount++;
                }
            }
        }

        return invalidatedCount;
    }

    /// <summary>
    /// Purges all expired or invalidated snapshots from internal memory tracking.
    /// </summary>
    public int CleanupStaleSnapshots(DateTimeOffset nowUtc)
    {
        lock (_lock)
        {
            var staleKeys = new List<Guid>();
            foreach (var (id, snapshot) in _snapshots)
            {
                snapshot.CheckAndApplyExpiry(nowUtc);
                if (snapshot.IsStale(nowUtc))
                {
                    staleKeys.Add(id);
                }
            }

            foreach (var id in staleKeys)
            {
                _snapshots.Remove(id);
            }

            return staleKeys.Count;
        }
    }
}
