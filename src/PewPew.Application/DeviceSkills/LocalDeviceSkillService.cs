using PewPew.Application.Actions;
using PewPew.Domain.Actions;

namespace PewPew.Application.DeviceSkills;

/// <summary>
/// Keeps the sole execution path for local device skills behind the P01
/// dispatch guard. A denied, cancelled, or unverified command never reports a
/// completed task.
/// </summary>
public sealed class LocalDeviceSkillService(ILocalDeviceSkillAdapter adapter)
{
    private readonly ILocalDeviceSkillAdapter _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));

    public async Task<LocalDeviceSkillExecutionResult> ExecuteAsync(
        ActionDispatchRequest dispatchRequest,
        LocalDeviceSkillCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(command);

        if (!MatchesAuthorizedCommand(dispatchRequest, command))
        {
            return LocalDeviceSkillExecutionResult.InvalidCommand(dispatchRequest.Task);
        }

        var dispatch = ActionDispatchService.Dispatch(dispatchRequest);
        if (!dispatch.IsAllowed)
        {
            return LocalDeviceSkillExecutionResult.Denied(dispatchRequest.Task, dispatch);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var authorizedCommand = new AuthorizedLocalDeviceSkillCommand(command);
            var adapterResult = await _adapter.ExecuteAsync(authorizedCommand, cancellationToken).ConfigureAwait(false);
            if (adapterResult.IsVerified)
            {
                dispatchRequest.Task.Complete(adapterResult.Evidence);
                return LocalDeviceSkillExecutionResult.Completed(dispatchRequest.Task, dispatch, adapterResult.Evidence);
            }

            dispatchRequest.Task.Fail(adapterResult.FailureReason ?? "device_skill_verification_failed");
            return LocalDeviceSkillExecutionResult.Failed(dispatchRequest.Task, dispatch, adapterResult.FailureReason ?? "device_skill_verification_failed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            dispatchRequest.Task.Cancel();
            return LocalDeviceSkillExecutionResult.Cancelled(dispatchRequest.Task, dispatch);
        }
        catch (Exception)
        {
            dispatchRequest.Task.Fail("device_skill_adapter_failed");
            return LocalDeviceSkillExecutionResult.Failed(dispatchRequest.Task, dispatch, "device_skill_adapter_failed");
        }
    }

    private static bool MatchesAuthorizedCommand(ActionDispatchRequest request, LocalDeviceSkillCommand command) =>
        request.Plan.Definition.Intent == command.Skill
        && request.Plan.Definition.Target == command.Resource
        && request.RequestedScope.Skill == command.Skill
        && request.RequestedScope.Resource == command.Resource
        && request.RequestedScope.Action == "execute";
}

public sealed record LocalDeviceSkillExecutionResult(
    ActionTaskStatus TaskStatus,
    string ReasonCode,
    string? VerificationEvidence,
    ActionDispatchResult? Dispatch)
{
    internal static LocalDeviceSkillExecutionResult InvalidCommand(ActionTask task) =>
        new(task.Status, "invalid_device_skill_command", null, null);

    internal static LocalDeviceSkillExecutionResult Denied(ActionTask task, ActionDispatchResult dispatch) =>
        new(task.Status, dispatch.ReasonCode, null, dispatch);

    internal static LocalDeviceSkillExecutionResult Completed(ActionTask task, ActionDispatchResult dispatch, string evidence) =>
        new(task.Status, "completed", evidence, dispatch);

    internal static LocalDeviceSkillExecutionResult Failed(ActionTask task, ActionDispatchResult dispatch, string reason) =>
        new(task.Status, reason, null, dispatch);

    internal static LocalDeviceSkillExecutionResult Cancelled(ActionTask task, ActionDispatchResult dispatch) =>
        new(task.Status, "cancelled", null, dispatch);
}
