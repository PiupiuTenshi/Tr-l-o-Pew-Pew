using PewPew.Domain.Voice;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Application.Voice;

/// <summary>
/// Application-owned abstraction for encrypted local sample storage.
/// Contracts carry opaque IDs, not file paths, raw audio, storage
/// implementation details, or encryption key material.
/// Infrastructure adapters implement this with device-scoped encryption.
/// </summary>
public interface IVoiceProfileSampleVault
{
    /// <summary>
    /// Encrypts and stores a sample for the given profile.
    /// Returns the opaque <see cref="ProfileSampleId"/> on success.
    /// The caller must also register metadata with the aggregate.
    /// </summary>
    Task<Result<ProfileSampleId>> StoreEncryptedSampleAsync(
        EntityId profileId,
        ReadOnlyMemory<byte> sampleData,
        SampleEnvironmentLabel label,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a single encrypted sample from the vault.
    /// Idempotent: returns success if the sample does not exist.
    /// </summary>
    Task<Result> DeleteSampleAsync(
        EntityId profileId,
        ProfileSampleId sampleId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Purges all encrypted samples for a profile.
    /// Returns the number of samples deleted.
    /// Idempotent: returns 0 if no samples exist.
    /// </summary>
    Task<Result<int>> PurgeAllSamplesAsync(
        EntityId profileId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Purges only samples whose TTL has expired.
    /// Returns the number of samples deleted.
    /// </summary>
    Task<Result<int>> PurgeExpiredSamplesAsync(
        EntityId profileId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether a sample exists in the vault without
    /// returning any raw data or file path.
    /// </summary>
    Task<bool> HasSampleAsync(
        EntityId profileId,
        ProfileSampleId sampleId,
        CancellationToken cancellationToken);
}
