using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Devices;

public enum DeviceTrustStatus { Unregistered, PairingPending, Trusted, Suspended, RekeyRequired, Revoked, Removed }

public sealed class DeviceNode
{
    public DeviceNode(EntityId id) => Id = id;
    public EntityId Id { get; }
    public DeviceTrustStatus Status { get; private set; } = DeviceTrustStatus.Unregistered;
    public long PairingGeneration { get; private set; }
    public void RequestPairing() { Require(DeviceTrustStatus.Unregistered); Status = DeviceTrustStatus.PairingPending; PairingGeneration++; }
    public void CompletePairing(long generation) { Require(DeviceTrustStatus.PairingPending); if (generation != PairingGeneration) { throw new InvalidOperationException("Pairing replay rejected."); } Status = DeviceTrustStatus.Trusted; }
    public void ExpirePairing() { Require(DeviceTrustStatus.PairingPending); Status = DeviceTrustStatus.Unregistered; }
    public void Suspend() { Require(DeviceTrustStatus.Trusted); Status = DeviceTrustStatus.Suspended; }
    public void Restore() { Require(DeviceTrustStatus.Suspended); Status = DeviceTrustStatus.Trusted; }
    public void RequireRekey() { Require(DeviceTrustStatus.Trusted, DeviceTrustStatus.Suspended); Status = DeviceTrustStatus.RekeyRequired; }
    public void CompleteRekey() { Require(DeviceTrustStatus.RekeyRequired); Status = DeviceTrustStatus.Trusted; }
    public void Revoke() { Require(DeviceTrustStatus.Trusted, DeviceTrustStatus.Suspended, DeviceTrustStatus.RekeyRequired); Status = DeviceTrustStatus.Revoked; }
    public void Purge() { Require(DeviceTrustStatus.Revoked); Status = DeviceTrustStatus.Removed; }
    private void Require(params DeviceTrustStatus[] allowed) { if (!allowed.Contains(Status)) { throw new InvalidOperationException($"Device transition denied from {Status}."); } }
}
