using PewPew.Domain.Ai;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class LocalModelRuntimeTests
{
    [Fact]
    public void RuntimeLazyLoadsUsesAndUnloadsAfterIdleTtl()
    {
        var now = DateTimeOffset.UtcNow;
        var runtime = NewRuntime();

        runtime.BeginLoad();
        runtime.MarkReady(now);
        runtime.BeginInference();
        runtime.CompleteInference(now.AddSeconds(1));

        Assert.False(runtime.BeginUnloadIfIdle(now.AddSeconds(30), TimeSpan.FromMinutes(1), memoryPressure: false));
        Assert.True(runtime.BeginUnloadIfIdle(now.AddMinutes(2), TimeSpan.FromMinutes(1), memoryPressure: false));
        runtime.MarkUnloaded();

        Assert.Equal(LocalModelRuntimeStatus.Unloaded, runtime.Status);
        Assert.Null(runtime.LastUsedAtUtc);
    }

    [Fact]
    public void MemoryPressureUnloadsReadyRuntimeBeforeIdleTtl()
    {
        var runtime = NewRuntime();
        runtime.BeginLoad();
        runtime.MarkReady(DateTimeOffset.UtcNow);

        Assert.True(runtime.BeginUnloadIfIdle(DateTimeOffset.UtcNow, TimeSpan.FromHours(1), memoryPressure: true));
        Assert.Equal(LocalModelRuntimeStatus.Unloading, runtime.Status);
    }

    [Fact]
    public void BusyRuntimeCannotBeUnloadedOrReceiveInvalidTransition()
    {
        var runtime = NewRuntime();
        runtime.BeginLoad();
        runtime.MarkReady(DateTimeOffset.UtcNow);
        runtime.BeginInference();

        Assert.False(runtime.BeginUnloadIfIdle(DateTimeOffset.UtcNow.AddDays(1), TimeSpan.FromMinutes(1), memoryPressure: true));
        Assert.Throws<InvalidOperationException>(runtime.BeginLoad);
    }

    [Fact]
    public void FailedRuntimeMustBeCleanedUpBeforeItCanUnload()
    {
        var runtime = NewRuntime();
        runtime.BeginLoad();
        runtime.Fail();

        runtime.BeginFailedCleanup();
        runtime.MarkUnloaded();

        Assert.Equal(LocalModelRuntimeStatus.Unloaded, runtime.Status);
    }

    private static LocalModelRuntime NewRuntime() => new(EntityId.New(), "whisper-small", 512 * 1024 * 1024);
}
