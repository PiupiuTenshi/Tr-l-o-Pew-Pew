using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Assistant;

public enum AssistantProfileStatus { Provisioning, Active, Paused, SafeMode, Decommissioning, Decommissioned }

public sealed class AssistantProfile
{
    public AssistantProfile(EntityId id, EntityId ownerId) { Id = id; OwnerId = ownerId; }
    public EntityId Id { get; }
    public EntityId OwnerId { get; }
    public AssistantProfileStatus Status { get; private set; } = AssistantProfileStatus.Provisioning;
    public void CompleteProvisioning() => Transition(AssistantProfileStatus.Provisioning, AssistantProfileStatus.Active);
    public void ProvisioningSecurityFailure() => Transition(AssistantProfileStatus.Provisioning, AssistantProfileStatus.SafeMode);
    public void Pause() => Transition(AssistantProfileStatus.Active, AssistantProfileStatus.Paused);
    public void Resume() => Transition(AssistantProfileStatus.Paused, AssistantProfileStatus.Active);
    public void EnterSafeMode() => Transition([AssistantProfileStatus.Active, AssistantProfileStatus.Paused], AssistantProfileStatus.SafeMode);
    public void RecoverToPaused() => Transition(AssistantProfileStatus.SafeMode, AssistantProfileStatus.Paused);
    public void RecoverAndResume() => Transition(AssistantProfileStatus.SafeMode, AssistantProfileStatus.Active);
    public void BeginDecommission() => Transition([AssistantProfileStatus.Active, AssistantProfileStatus.Paused, AssistantProfileStatus.SafeMode], AssistantProfileStatus.Decommissioning);
    public void CompleteDecommission() => Transition(AssistantProfileStatus.Decommissioning, AssistantProfileStatus.Decommissioned);
    private void Transition(AssistantProfileStatus expected, AssistantProfileStatus next)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Cannot transition profile from {Status} to {next}.");
        }

        Status = next;
    }

    private void Transition(AssistantProfileStatus[] expected, AssistantProfileStatus next)
    {
        if (!expected.Contains(Status))
        {
            throw new InvalidOperationException($"Cannot transition profile from {Status} to {next}.");
        }

        Status = next;
    }
}
