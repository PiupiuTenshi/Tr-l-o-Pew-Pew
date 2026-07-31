using PewPew.Application.Automation;
using PewPew.Domain.Automation;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Unit and security tests for <see cref="UiTargetSnapshot"/> domain lifecycle,
/// <see cref="UiTargetRedactor"/> secret sanitization, and <see cref="BrowserTabContextManager"/>
/// TTL expiration & tab invalidation.
/// </summary>
public sealed class UiTargetSnapshotLifecycleTests
{
    private static readonly string SampleTabId = "tab_12345";
    private static readonly string SampleOrigin = "https://example.com";
    private static readonly string SampleSelector = "#submit-btn";

    [Fact]
    public void CreateSnapshotSetsActiveStatusAndTTLExpiration()
    {
        var now = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromSeconds(30);

        var snapshot = UiTargetSnapshot.Create(
            SampleTabId,
            SampleOrigin,
            "Example Page",
            SampleSelector,
            "Submit",
            "button",
            now,
            ttl);

        Assert.Equal(UiTargetSnapshotStatus.Active, snapshot.Status);
        Assert.Equal(now.Add(ttl), snapshot.ExpiresAtUtc);
        Assert.False(snapshot.IsStale(now));
    }

    [Fact]
    public void IsStaleReturnsTrueWhenExpiredOrInvalidated()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = UiTargetSnapshot.Create(
            SampleTabId,
            SampleOrigin,
            "Page",
            SampleSelector,
            "Click Me",
            "button",
            now,
            TimeSpan.FromSeconds(10));

        // 1. Fresh
        Assert.False(snapshot.IsStale(now));

        // 2. Expired by time
        Assert.True(snapshot.IsStale(now.AddSeconds(11)));

        // 3. Explicitly invalidated
        snapshot.Invalidate("tab_closed");
        Assert.Equal(UiTargetSnapshotStatus.Invalidated, snapshot.Status);
        Assert.Equal("tab_closed", snapshot.InvalidationReason);
        Assert.True(snapshot.IsStale(now));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("cvv")]
    [InlineData("creditcard")]
    [InlineData("pin")]
    public void UiTargetRedactorRedactsPasswordInputTypes(string sensitiveType)
    {
        var redacted = UiTargetRedactor.RedactText("MySecret123!", sensitiveType, "input_field");

        Assert.Equal("[REDACTED_SECRET]", redacted);
    }

    [Theory]
    [InlineData("user_password")]
    [InlineData("auth_token")]
    [InlineData("api_key")]
    [InlineData("pwd_reset")]
    public void UiTargetRedactorRedactsSensitiveKeywords(string sensitiveFieldName)
    {
        var redacted = UiTargetRedactor.RedactText("SuperSecretValue", "text", sensitiveFieldName);

        Assert.Equal("[REDACTED_SECRET]", redacted);
    }

    [Fact]
    public void UiTargetRedactorRedactsJwtAndApiKeyStrings()
    {
        var jwtText = "User bearer token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c in header";
        var redactedJwt = UiTargetRedactor.RedactText(jwtText, "text", "description");

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIs", redactedJwt);
        Assert.Contains("[REDACTED_SECRET]", redactedJwt);

        var apiKeyText = "Key mock_key_1234567890abcdef1234567890abcdef is private";
        var redactedApiKey = UiTargetRedactor.RedactText(apiKeyText, "text", "description");

        Assert.DoesNotContain("mock_key_1234567890abcdef", redactedApiKey);

        Assert.Contains("[REDACTED_SECRET]", redactedApiKey);
    }

    [Fact]
    public void CaptureSnapshotAutomaticallyRedactsSecretPayload()
    {
        var manager = new BrowserTabContextManager();
        var now = DateTimeOffset.UtcNow;

        var snapshot = manager.CaptureSnapshot(
            SampleTabId,
            SampleOrigin,
            "Login Page",
            "#password-input",
            "P@ssw0rd12345",
            "password",
            now);

        Assert.True(snapshot.IsRedacted);
        Assert.Equal("[REDACTED_SECRET]", snapshot.ElementText);
    }

    [Fact]
    public void GetActiveSnapshotReturnsNullWhenStaleOrExpired()
    {
        var manager = new BrowserTabContextManager();
        var now = DateTimeOffset.UtcNow;

        var snapshot = manager.CaptureSnapshot(
            SampleTabId,
            SampleOrigin,
            "Home Page",
            SampleSelector,
            "Submit",
            "button",
            now,
            TimeSpan.FromSeconds(5));

        // Active immediately
        var activeSnapshot = manager.GetActiveSnapshot(snapshot.SnapshotId, now);
        Assert.NotNull(activeSnapshot);

        // Null after TTL expiration
        var expiredSnapshot = manager.GetActiveSnapshot(snapshot.SnapshotId, now.AddSeconds(6));
        Assert.Null(expiredSnapshot);
    }

    [Fact]
    public void InvalidateTabSnapshotsInvalidatesMatchingTab()
    {
        var manager = new BrowserTabContextManager();
        var now = DateTimeOffset.UtcNow;

        var snapshot1 = manager.CaptureSnapshot(SampleTabId, SampleOrigin, "Page 1", SampleSelector, "Text", "text", now);
        var snapshot2 = manager.CaptureSnapshot("tab_99999", SampleOrigin, "Page 2", SampleSelector, "Text", "text", now);

        // Invalidate tab_12345
        var count = manager.InvalidateTabSnapshots(SampleTabId, "tab_closed");

        Assert.Equal(1, count);
        Assert.Null(manager.GetActiveSnapshot(snapshot1.SnapshotId, now)); // Tab 1 invalidated
        Assert.NotNull(manager.GetActiveSnapshot(snapshot2.SnapshotId, now)); // Tab 2 remains active
    }

    [Fact]
    public void CleanupStaleSnapshotsPurgesStaleItems()
    {
        var manager = new BrowserTabContextManager();
        var now = DateTimeOffset.UtcNow;

        var snapshot1 = manager.CaptureSnapshot(SampleTabId, SampleOrigin, "Page 1", SampleSelector, "Text", "text", now, TimeSpan.FromSeconds(2));
        var snapshot2 = manager.CaptureSnapshot(SampleTabId, SampleOrigin, "Page 2", SampleSelector, "Text", "text", now, TimeSpan.FromSeconds(60));

        Assert.NotNull(manager.GetActiveSnapshot(snapshot1.SnapshotId, now));

        // Fast forward 5 seconds
        var purgedCount = manager.CleanupStaleSnapshots(now.AddSeconds(5));

        Assert.Equal(1, purgedCount);
        Assert.Null(manager.GetActiveSnapshot(snapshot1.SnapshotId, now.AddSeconds(5)));
        Assert.NotNull(manager.GetActiveSnapshot(snapshot2.SnapshotId, now.AddSeconds(5)));
    }
}
