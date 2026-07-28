using PewPew.Domain.Devices;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class DeviceLifecycleTests
{
    [Fact]
    public void PairingRejectsReplayAndRevokedDeviceCannotPairAgain()
    {
        var device = new DeviceNode(EntityId.New()); device.RequestPairing();
        Assert.Throws<InvalidOperationException>(() => device.CompletePairing(0));
        device.CompletePairing(1); device.Revoke();
        Assert.Throws<InvalidOperationException>(device.RequestPairing);
    }
    [Fact]
    public void SessionExpiresAndTerminalSessionRejectsReuse()
    {
        var now = DateTimeOffset.UtcNow; var session = new DeviceSession(EntityId.New(), EntityId.New(), now);
        session.CompleteHandshake(); session.Expire(now);
        Assert.Equal(DeviceSessionStatus.Expired, session.Status);
        Assert.Throws<InvalidOperationException>(session.Reauthenticate);
    }
}
