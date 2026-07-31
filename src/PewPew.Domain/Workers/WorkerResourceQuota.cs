namespace PewPew.Domain.Workers;

/// <summary>
/// Value object defining resource quotas and sandbox permissions for a <see cref="WorkerProcess"/>.
/// </summary>
public sealed record WorkerResourceQuota
{
    public const int DefaultMaxRamMb = 256;
    public const int DefaultMaxCpuPercent = 80;
    public static readonly TimeSpan DefaultMaxExecutionDuration = TimeSpan.FromSeconds(30);
    public const long DefaultMaxOutputSizeBytes = 1_048_576; // 1 MiB

    public int MaxRamMb { get; }
    public int MaxCpuPercent { get; }
    public TimeSpan MaxExecutionDuration { get; }
    public long MaxOutputSizeBytes { get; }
    public bool AllowNetworkAccess { get; }
    public bool AllowFileSystemWrite { get; }

    public static WorkerResourceQuota Default { get; } = new();

    public WorkerResourceQuota(
        int maxRamMb = DefaultMaxRamMb,
        int maxCpuPercent = DefaultMaxCpuPercent,
        TimeSpan? maxExecutionDuration = null,
        long maxOutputSizeBytes = DefaultMaxOutputSizeBytes,
        bool allowNetworkAccess = false,
        bool allowFileSystemWrite = false)
    {
        if (maxRamMb <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRamMb), "Max RAM quota must be greater than zero.");
        }

        if (maxCpuPercent is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCpuPercent), "Max CPU quota must be between 1 and 100 percent.");
        }

        var duration = maxExecutionDuration ?? DefaultMaxExecutionDuration;
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExecutionDuration), "Max execution duration must be positive.");
        }

        if (maxOutputSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOutputSizeBytes), "Max output size quota must be greater than zero.");
        }

        MaxRamMb = maxRamMb;
        MaxCpuPercent = maxCpuPercent;
        MaxExecutionDuration = duration;
        MaxOutputSizeBytes = maxOutputSizeBytes;
        AllowNetworkAccess = allowNetworkAccess;
        AllowFileSystemWrite = allowFileSystemWrite;
    }
}
