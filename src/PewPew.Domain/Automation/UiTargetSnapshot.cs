namespace PewPew.Domain.Automation;

/// <summary>
/// Aggregate root representing a short-lived, security-redacted UI target snapshot
/// captured from a browser tab or desktop window.
/// Enforces TTL expiration, invalidation on tab close/navigation, and secret redaction.
/// </summary>
public sealed class UiTargetSnapshot
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);

    public Guid SnapshotId { get; }
    public string TabId { get; }
    public string Origin { get; }
    public string Title { get; }
    public string TargetSelector { get; }
    public string ElementText { get; private set; }
    public string InputType { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public UiTargetSnapshotStatus Status { get; private set; }
    public bool IsRedacted { get; private set; }
    public string? InvalidationReason { get; private set; }

    /// <summary>
    /// Monotonically increasing version counter. Incremented on each mutation
    /// (e.g. redaction update). Used for optimistic concurrency binding.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Browser navigation generation counter at capture time. A navigation
    /// (e.g. page reload, URL change) increments this value in the extension,
    /// invalidating snapshots captured at a previous generation.
    /// </summary>
    public long NavigationGeneration { get; }

    private UiTargetSnapshot(
        Guid snapshotId,
        string tabId,
        string origin,
        string title,
        string targetSelector,
        string elementText,
        string inputType,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset expiresAtUtc,
        bool isRedacted,
        long navigationGeneration)
    {
        SnapshotId = snapshotId;
        TabId = tabId;
        Origin = origin;
        Title = title;
        TargetSelector = targetSelector;
        ElementText = elementText;
        InputType = inputType;
        CapturedAtUtc = capturedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = UiTargetSnapshotStatus.Active;
        IsRedacted = isRedacted;
        Version = 1;
        NavigationGeneration = navigationGeneration;
    }

    public static UiTargetSnapshot Create(
        string tabId,
        string origin,
        string title,
        string targetSelector,
        string elementText,
        string inputType,
        DateTimeOffset capturedAtUtc,
        TimeSpan? ttl = null,
        bool isRedacted = false,
        long navigationGeneration = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSelector);

        var validTtl = ttl ?? DefaultTtl;
        if (validTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive");
        }

        var expiresAtUtc = capturedAtUtc.Add(validTtl);

        return new UiTargetSnapshot(
            Guid.NewGuid(),
            tabId.Trim(),
            origin.Trim(),
            title ?? string.Empty,
            targetSelector.Trim(),
            elementText ?? string.Empty,
            inputType ?? string.Empty,
            capturedAtUtc,
            expiresAtUtc,
            isRedacted,
            navigationGeneration);
    }

    public bool IsStale(DateTimeOffset nowUtc)
    {
        return Status != UiTargetSnapshotStatus.Active || nowUtc >= ExpiresAtUtc;
    }

    public void UpdateRedactedText(string redactedText)
    {
        ElementText = redactedText ?? string.Empty;
        IsRedacted = true;
        Version++;
    }

    public void Invalidate(string reason)
    {
        if (Status == UiTargetSnapshotStatus.Invalidated)
        {
            return;
        }

        Status = UiTargetSnapshotStatus.Invalidated;
        InvalidationReason = string.IsNullOrWhiteSpace(reason) ? "invalidated" : reason.Trim();
    }

    public void CheckAndApplyExpiry(DateTimeOffset nowUtc)
    {
        if (Status == UiTargetSnapshotStatus.Active && nowUtc >= ExpiresAtUtc)
        {
            Status = UiTargetSnapshotStatus.Expired;
            InvalidationReason = "ttl_expired";
        }
    }
}
