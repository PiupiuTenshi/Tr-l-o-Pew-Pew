using PewPew.Application.Actions;
using PewPew.Application.Automation;
using PewPew.Application.BrowserExtension;
using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class BrowserActionControlPathTests
{
    private const string Origin = "https://trusted.example";

    [Fact]
    public async Task DeniesBeforeChannelWhenPermissionIsNotActive()
    {
        var scenario = CreateScenario(permissionActive: false);
        var channel = new FakeChannel("observed_paused");

        var result = await BrowserActionControlPath.ExecuteAsync(scenario.Request, channel, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
        Assert.Equal("permission_denied", result.ReasonCode);
        Assert.Equal(0, channel.CallCount);
        Assert.Equal(ActionTaskStatus.Queued, scenario.Task.Status);
    }

    [Fact]
    public async Task ConsumesBoundConfirmationAndCompletesOnlyOnExpectedReadback()
    {
        var scenario = CreateScenario(permissionActive: true);
        var channel = new FakeChannel("observed_paused");

        var result = await BrowserActionControlPath.ExecuteAsync(scenario.Request, channel, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Verified, result.Outcome);
        Assert.Equal(1, channel.CallCount);
        Assert.Equal(ConfirmationStatus.Consumed, scenario.Request.Authorization.Confirmation!.Status);
        Assert.Equal(ActionTaskStatus.Completed, scenario.Task.Status);
    }

    [Fact]
    public async Task MarksUnknownWhenReadbackIsLostAfterAuthorizedDispatch()
    {
        var scenario = CreateScenario(permissionActive: true);
        var channel = new FakeChannel(null);

        var result = await BrowserActionControlPath.ExecuteAsync(scenario.Request, channel, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Unknown, result.Outcome);
        Assert.Equal(ActionTaskStatus.Unknown, scenario.Task.Status);
    }

    [Fact]
    public async Task ExpiredSingleUsePromptDeniesBeforeChannelDispatch()
    {
        var now = DateTimeOffset.UtcNow;
        var contexts = new BrowserActiveTabContextStore();
        contexts.RecordAuthenticatedPoll(new NativeMessagingTransportRequest(
            1, "command_poll", "poll-1", "token", "nonce-1", now, Origin, "tab-1"), now);
        var coordinator = new BrowserMediaActionCoordinator(contexts);
        var prompt = coordinator.RequestSingleUseAction("pause", now);
        Assert.NotNull(prompt);
        var channel = new FakeChannel("observed_paused");

        var result = await coordinator.ConfirmAndExecuteAsync(prompt!.RequestId, channel, now.AddMinutes(2), CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
        Assert.Equal("browser_confirmation_expired", result.ReasonCode);
        Assert.Equal(0, channel.CallCount);
    }

    private static Scenario CreateScenario(bool permissionActive)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = EntityId.New();
        var sessionId = EntityId.New();
        var deviceId = EntityId.New();
        var scope = PermissionScope.Create(userId, deviceId, "browser.media", Origin, "pause", false);
        var grant = new PermissionGrant(EntityId.New(), scope, now.AddMinutes(1));
        grant.Submit();
        if (permissionActive)
        {
            grant.Approve();
        }

        var profile = new AssistantProfile(EntityId.New(), userId);
        profile.CompleteProvisioning();
        var definition = StructuredActionPlan.Create(EntityId.New(), 1, "browser.media", Origin, "pause");
        var plan = new ActionPlan(definition, now.AddMinutes(1));
        plan.SubmitForPolicyReview();
        plan.RequireConfirmation();
        var confirmation = new ConfirmationRequest(EntityId.New(), userId, sessionId, deviceId, definition.Hash, now.AddMinutes(1));
        var task = new ActionTask(EntityId.New(), definition.Id, userId, now.AddMinutes(1), true);
        const string correlationId = "browser-control-correlation";
        var authorization = new ActionDispatchRequest(profile, plan, grant, scope, confirmation, task, userId, sessionId, deviceId, now, correlationId);
        var command = new NativeMessagingBrowserCommand("browser-command", "token", Origin, "tab-1", "snapshot-1", 1, 0, "pause", definition.Hash, correlationId);
        return new(new BrowserActionControlRequest(authorization, command), task);
    }

    private sealed record Scenario(BrowserActionControlRequest Request, ActionTask Task);

    private sealed class FakeChannel(string? status) : IVerifiedBrowserActionChannel
    {
        public int CallCount { get; private set; }
        public Task<string?> DispatchAndReadbackAsync(NativeMessagingBrowserCommand command, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(status);
        }
    }
}
