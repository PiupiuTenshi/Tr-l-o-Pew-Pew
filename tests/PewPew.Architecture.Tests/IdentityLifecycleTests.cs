using PewPew.Domain.Assistant;
using PewPew.Domain.Identity;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class IdentityLifecycleTests
{
    [Fact]
    public void AccountSupportsApprovedHappyPathAndDeletedIsTerminal()
    {
        var account = new UserAccount(EntityId.New());
        account.Activate(); account.RequestDeletion(); account.CompleteDeletion();
        Assert.Equal(UserAccountStatus.Deleted, account.Status);
        Assert.Throws<InvalidOperationException>(account.Activate);
    }

    [Fact]
    public void AccountRejectsIllegalTransition()
    {
        Assert.Throws<InvalidOperationException>(() => new UserAccount(EntityId.New()).Unlock());
    }

    [Fact]
    public void SafeModeRequiresExplicitRecoveryAndProfileTerminalIsProtected()
    {
        var profile = new AssistantProfile(EntityId.New(), EntityId.New());
        profile.CompleteProvisioning(); profile.EnterSafeMode();
        Assert.Equal(AssistantProfileStatus.SafeMode, profile.Status);
        Assert.Throws<InvalidOperationException>(profile.Resume);
        profile.RecoverToPaused(); profile.BeginDecommission(); profile.CompleteDecommission();
        Assert.Throws<InvalidOperationException>(profile.RecoverAndResume);
    }
}
