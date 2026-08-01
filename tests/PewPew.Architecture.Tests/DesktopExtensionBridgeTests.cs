using PewPew.Application.BrowserExtension;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Unit and security tests for <see cref="DesktopExtensionBridge"/>, <see cref="AntiReplayNonceGuard"/>,
/// caller token authentication, anti-replay protection, timestamp drift enforcement, and disconnect cleanup.
/// </summary>
public sealed class DesktopExtensionBridgeTests
{
    private static readonly string AllowedOrigin = "https://example.com";

    [Fact]
    public void IssueSessionTokenGeneratesActiveUnexpiredToken()
    {
        var bridge = CreateBridge();
        var now = DateTimeOffset.UtcNow;

        var token = bridge.IssueSessionToken(now, TimeSpan.FromMinutes(10));

        Assert.True(bridge.IsConnected);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(bridge.IsTokenValid(token, now));
        Assert.False(bridge.IsTokenValid(token, now.AddMinutes(11))); // Expired
    }

    [Fact]
    public void ProcessRequestWithValidTokenAndNonceSucceeds()
    {
        var bridge = CreateBridge();
        var now = DateTimeOffset.UtcNow;
        var token = bridge.IssueSessionToken(now);

        var request = new ExtensionBridgeRequest(
            SessionToken: token,
            Nonce: Guid.NewGuid().ToString("N"),
            TimestampUtc: now,
            TargetOrigin: AllowedOrigin,
            Command: "read_tab_title");

        var response = bridge.ProcessRequest(request, now);

        Assert.True(response.IsSuccess);
        Assert.Equal("allowed", response.ReasonCode);
        Assert.NotNull(response.DataPayload);
        Assert.Contains("read_tab_title", response.DataPayload);
    }

    [Fact]
    public void ProcessRequestRejectsInvalidOrExpiredToken()
    {
        var bridge = CreateBridge();
        var now = DateTimeOffset.UtcNow;
        var token = bridge.IssueSessionToken(now, TimeSpan.FromMinutes(5));

        // 1. Invalid token
        var invalidTokenRequest = new ExtensionBridgeRequest(
            SessionToken: "invalid_fake_token",
            Nonce: Guid.NewGuid().ToString("N"),
            TimestampUtc: now,
            TargetOrigin: AllowedOrigin,
            Command: "test");

        var response1 = bridge.ProcessRequest(invalidTokenRequest, now);
        Assert.False(response1.IsSuccess);
        Assert.Equal("unauthorized_caller_token", response1.ReasonCode);

        // 2. Expired token
        var validTokenRequest = new ExtensionBridgeRequest(
            SessionToken: token,
            Nonce: Guid.NewGuid().ToString("N"),
            TimestampUtc: now.AddMinutes(6),
            TargetOrigin: AllowedOrigin,
            Command: "test");

        var response2 = bridge.ProcessRequest(validTokenRequest, now.AddMinutes(6));
        Assert.False(response2.IsSuccess);
        Assert.Equal("unauthorized_caller_token", response2.ReasonCode);
    }

    [Fact]
    public void ProcessRequestRejectsReplayedNonce()
    {
        var bridge = CreateBridge();
        var now = DateTimeOffset.UtcNow;
        var token = bridge.IssueSessionToken(now);
        var reusedNonce = "nonce_12345_unique_key";

        var request1 = new ExtensionBridgeRequest(
            SessionToken: token,
            Nonce: reusedNonce,
            TimestampUtc: now,
            TargetOrigin: AllowedOrigin,
            Command: "query");

        var request2 = new ExtensionBridgeRequest(
            SessionToken: token,
            Nonce: reusedNonce, // Replayed nonce!
            TimestampUtc: now.AddSeconds(1),
            TargetOrigin: AllowedOrigin,
            Command: "query");

        // First attempt succeeds
        var response1 = bridge.ProcessRequest(request1, now);
        Assert.True(response1.IsSuccess);

        // Replayed attempt fails with replay_attack_detected
        var response2 = bridge.ProcessRequest(request2, now.AddSeconds(1));
        Assert.False(response2.IsSuccess);
        Assert.Contains("replay_attack_detected", response2.ReasonCode);
    }

    [Fact]
    public void ProcessRequestRejectsExceededTimestampDrift()
    {
        var bridge = CreateBridge();
        var now = DateTimeOffset.UtcNow;
        var token = bridge.IssueSessionToken(now);

        // Message timestamp is 120 seconds in the past (exceeds 60s max allowed drift)
        var staleRequest = new ExtensionBridgeRequest(
            SessionToken: token,
            Nonce: Guid.NewGuid().ToString("N"),
            TimestampUtc: now.AddSeconds(-120),
            TargetOrigin: AllowedOrigin,
            Command: "stale_query");

        var response = bridge.ProcessRequest(staleRequest, now);

        Assert.False(response.IsSuccess);
        Assert.Contains("timestamp_drift_exceeded", response.ReasonCode);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("chrome://settings")]
    [InlineData("about:blank")]
    public void ProcessRequestRejectsProhibitedOrUnapprovedOrigin(string prohibitedOrigin)
    {
        var bridge = CreateBridge();
        var now = DateTimeOffset.UtcNow;
        var token = bridge.IssueSessionToken(now);

        var request = new ExtensionBridgeRequest(
            SessionToken: token,
            Nonce: Guid.NewGuid().ToString("N"),
            TimestampUtc: now,
            TargetOrigin: prohibitedOrigin,
            Command: "read_page");

        var response = bridge.ProcessRequest(request, now);

        Assert.False(response.IsSuccess);
        Assert.Equal("origin_policy_denied", response.ReasonCode);
    }

    [Fact]
    public void DisconnectInvalidatesTokenAndClearsNonces()
    {
        var bridge = CreateBridge();
        var now = DateTimeOffset.UtcNow;
        var token = bridge.IssueSessionToken(now);

        Assert.True(bridge.IsConnected);

        // Disconnect
        bridge.Disconnect();

        Assert.False(bridge.IsConnected);
        Assert.False(bridge.IsTokenValid(token, now));

        var requestAfterDisconnect = new ExtensionBridgeRequest(
            SessionToken: token,
            Nonce: Guid.NewGuid().ToString("N"),
            TimestampUtc: now,
            TargetOrigin: AllowedOrigin,
            Command: "post_disconnect_cmd");

        var response = bridge.ProcessRequest(requestAfterDisconnect, now);
        Assert.False(response.IsSuccess);
        Assert.Equal("unauthorized_caller_token", response.ReasonCode);
    }

    [Fact]
    public void ProcessRequestDeniesOriginWhenNoAllowlistIsConfigured()
    {
        var bridge = new DesktopExtensionBridge();
        var now = DateTimeOffset.UtcNow;
        var token = bridge.IssueSessionToken(now);
        var request = new ExtensionBridgeRequest(token, Guid.NewGuid().ToString("N"), now, AllowedOrigin, "read_tab_title");

        var response = bridge.ProcessRequest(request, now);

        Assert.False(response.IsSuccess);
        Assert.Equal("origin_policy_denied", response.ReasonCode);
    }

    [Fact]
    public void NonceGuardEvictsExpiredNoncesAndBoundsUnexpiredEntries()
    {
        var guard = new AntiReplayNonceGuard(TimeSpan.FromSeconds(1), maximumTrackedNonces: 1);
        var now = DateTimeOffset.UtcNow;

        Assert.True(guard.TryValidateAndRegisterNonce("first", now, now, out _));
        Assert.False(guard.TryValidateAndRegisterNonce("second", now, now, out var capacityFailure));
        Assert.Contains("nonce_capacity_exceeded", capacityFailure);
        Assert.True(guard.TryValidateAndRegisterNonce("second", now.AddSeconds(2), now.AddSeconds(2), out _));
    }

    private static DesktopExtensionBridge CreateBridge() =>
        new(new ExtensionOriginPolicyValidator([AllowedOrigin]));
}
