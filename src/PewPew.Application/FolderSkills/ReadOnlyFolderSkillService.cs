using PewPew.Application.Actions;
using PewPew.Domain.Actions;

namespace PewPew.Application.FolderSkills;

public sealed class ReadOnlyFolderSkillService(IReadOnlyFolderSkillAdapter adapter)
{
    private readonly IReadOnlyFolderSkillAdapter _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));

    public async Task<ReadOnlyFolderSkillExecutionResult> ExecuteAsync(
        ActionDispatchRequest dispatchRequest,
        ReadOnlyFolderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(command);

        if (!MatchesAuthorizedCommand(dispatchRequest, command))
        {
            return ReadOnlyFolderSkillExecutionResult.InvalidCommand(dispatchRequest.Task);
        }

        var dispatch = ActionDispatchService.Dispatch(dispatchRequest);
        if (!dispatch.IsAllowed)
        {
            return ReadOnlyFolderSkillExecutionResult.Denied(dispatchRequest.Task, dispatch);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _adapter.ExecuteAsync(new AuthorizedReadOnlyFolderCommand(command), cancellationToken).ConfigureAwait(false);
            if (result.IsVerified)
            {
                dispatchRequest.Task.Complete(result.Evidence);
                return ReadOnlyFolderSkillExecutionResult.Completed(dispatchRequest.Task, dispatch, result.Entries, result.Evidence);
            }

            var failure = result.FailureReason ?? "folder_verification_failed";
            dispatchRequest.Task.Fail(failure);
            return ReadOnlyFolderSkillExecutionResult.Failed(dispatchRequest.Task, dispatch, failure);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            dispatchRequest.Task.Cancel();
            return ReadOnlyFolderSkillExecutionResult.Cancelled(dispatchRequest.Task, dispatch);
        }
        catch (Exception)
        {
            dispatchRequest.Task.Fail("folder_adapter_failed");
            return ReadOnlyFolderSkillExecutionResult.Failed(dispatchRequest.Task, dispatch, "folder_adapter_failed");
        }
    }

    private static bool MatchesAuthorizedCommand(ActionDispatchRequest request, ReadOnlyFolderCommand command) =>
        request.Plan.Definition.Intent == ReadOnlyFolderCommand.SkillName
        && request.Plan.Definition.Target == command.Root.Id
        && request.RequestedScope.Skill == ReadOnlyFolderCommand.SkillName
        && request.RequestedScope.Resource == command.Root.Id
        && request.RequestedScope.Action == "read";
}

public sealed record ReadOnlyFolderSkillExecutionResult(
    ActionTaskStatus TaskStatus,
    string ReasonCode,
    IReadOnlyList<ReadOnlyFolderEntry> Entries,
    string? VerificationEvidence,
    ActionDispatchResult? Dispatch)
{
    internal static ReadOnlyFolderSkillExecutionResult InvalidCommand(ActionTask task) =>
        new(task.Status, "invalid_folder_command", Array.Empty<ReadOnlyFolderEntry>(), null, null);

    internal static ReadOnlyFolderSkillExecutionResult Denied(ActionTask task, ActionDispatchResult dispatch) =>
        new(task.Status, dispatch.ReasonCode, Array.Empty<ReadOnlyFolderEntry>(), null, dispatch);

    internal static ReadOnlyFolderSkillExecutionResult Completed(ActionTask task, ActionDispatchResult dispatch, IReadOnlyList<ReadOnlyFolderEntry> entries, string evidence) =>
        new(task.Status, "completed", entries, evidence, dispatch);

    internal static ReadOnlyFolderSkillExecutionResult Failed(ActionTask task, ActionDispatchResult dispatch, string reason) =>
        new(task.Status, reason, Array.Empty<ReadOnlyFolderEntry>(), null, dispatch);

    internal static ReadOnlyFolderSkillExecutionResult Cancelled(ActionTask task, ActionDispatchResult dispatch) =>
        new(task.Status, "cancelled", Array.Empty<ReadOnlyFolderEntry>(), null, dispatch);
}
