using System.Text.RegularExpressions;
using PewPew.Domain.Actions;
using PewPew.Domain.Audit;
using PewPew.Domain.Permissions;
using PewPew.Domain.Skills;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Application.EmergencyStop;

/// <summary>
/// Cascades a security revocation or quarantine to workers that were bound to
/// the exact grant or package at registration. This service never authorizes,
/// retries, resumes, or targets an unbound worker.
/// </summary>
public sealed class SecurityWorkerCascadeService(LocalWorkerOwnershipRegistry ownership)
{
    private static readonly Regex ReasonCodePattern = new("^[a-z0-9_-]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly LocalWorkerOwnershipRegistry _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));

    public async Task<WorkerCascadeExecutionResult> RevokePermissionAsync(
        PermissionGrant permissionGrant,
        IEnumerable<ActionTask> tasks,
        WorkerCascadeRequest request)
    {
        ArgumentNullException.ThrowIfNull(permissionGrant);
        EnsureRequest(request);

        if (permissionGrant.Status != PermissionGrantStatus.Revoked)
        {
            permissionGrant.Revoke();
        }

        var stops = await _ownership.StopByPermissionAsync(permissionGrant.Id).ConfigureAwait(false);
        return Reconcile(tasks, stops, request, "permission_revoke_cascade", "permission_revoked");
    }

    public async Task<WorkerCascadeExecutionResult> QuarantineSkillAsync(
        SkillPackage skillPackage,
        string reasonCode,
        IEnumerable<ActionTask> tasks,
        WorkerCascadeRequest request)
    {
        ArgumentNullException.ThrowIfNull(skillPackage);
        EnsureReasonCode(reasonCode);
        EnsureRequest(request);

        if (skillPackage.Status != SkillPackageStatus.Quarantined)
        {
            skillPackage.Quarantine(reasonCode);
        }

        var stops = await _ownership.StopBySkillAsync(skillPackage.Id).ConfigureAwait(false);
        return Reconcile(tasks, stops, request, "skill_quarantine_cascade", "skill_quarantined");
    }

    private static WorkerCascadeExecutionResult Reconcile(
        IEnumerable<ActionTask> tasks,
        IReadOnlyList<LocalWorkerStopResult> stops,
        WorkerCascadeRequest request,
        string auditAction,
        string policyResult)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        var tasksById = tasks.ToDictionary(task => task.Id);
        var cancelledTaskIds = new List<EntityId>();
        var unknownTaskIds = new List<EntityId>();

        foreach (var stop in stops)
        {
            if (!tasksById.TryGetValue(stop.TaskId, out var task) || task.Status != ActionTaskStatus.Running)
            {
                continue;
            }

            if (stop.IsStopped && task.SupportsCancellation)
            {
                task.Cancel();
                cancelledTaskIds.Add(task.Id);
                continue;
            }

            task.MarkOutcomeUnknown();
            unknownTaskIds.Add(task.Id);
        }

        var audit = new AuditRecord(
            EntityId.New(),
            request.CorrelationId,
            request.ActorId,
            request.SourceDeviceId,
            request.TargetDeviceId,
            auditAction,
            policyResult);
        audit.Seal();

        return new WorkerCascadeExecutionResult(cancelledTaskIds, unknownTaskIds, stops, audit);
    }

    private static void EnsureRequest(WorkerCascadeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CorrelationId);
    }

    private static void EnsureReasonCode(string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        if (!ReasonCodePattern.IsMatch(reasonCode))
        {
            throw new ArgumentException("Quarantine reason must be a canonical metadata-only reason code.", nameof(reasonCode));
        }
    }
}

public sealed record WorkerCascadeRequest(
    string CorrelationId,
    EntityId ActorId,
    EntityId SourceDeviceId,
    EntityId TargetDeviceId);

public sealed record WorkerCascadeExecutionResult(
    IReadOnlyList<EntityId> CancelledTaskIds,
    IReadOnlyList<EntityId> UnknownTaskIds,
    IReadOnlyList<LocalWorkerStopResult> WorkerStops,
    AuditRecord AuditRecord);
