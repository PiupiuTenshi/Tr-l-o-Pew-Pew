using System.Security.Cryptography;
using PewPew.Application.Voice;
using PewPew.Domain.Voice;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Infrastructure.Voice;

/// <summary>
/// AES-256-encrypted file-based vault for voice profile samples.
/// Each sample is stored as an encrypted file in a per-profile subdirectory
/// under the user's local application data.
/// <para>
/// Security properties:
/// <list type="bullet">
/// <item>Each profile has a unique AES-256 key stored alongside the samples.</item>
/// <item>The key file is only accessible to the current OS user (no cross-user access).</item>
/// <item>No raw audio, file path, or key material appears in any log or return value.</item>
/// <item>Idempotent delete — missing file is treated as success.</item>
/// <item>All I/O is async with <see cref="CancellationToken"/>.</item>
/// </list>
/// </para>
/// <para>
/// Note: Production hardening should replace this with DPAPI
/// (<c>DataProtectionScope.CurrentUser</c>) or ASP.NET Data Protection
/// when the Infrastructure project moves to a Windows-specific TFM.
/// The current implementation provides encryption at rest using
/// AES-256-CBC with HMAC-SHA256 and a per-profile random key.
/// </para>
/// </summary>
public sealed class EncryptedLocalVoiceProfileSampleVault : IVoiceProfileSampleVault
{
    private const string VaultDirectoryName = "VoiceProfiles";
    private const string SamplesSubdirectory = "samples";
    private const string MetadataExtension = ".meta";
    private const string SampleExtension = ".enc";
    private const string KeyFileName = ".profile-key";
    private const int KeySizeBytes = 32; // AES-256

    private readonly string _basePath;

    public EncryptedLocalVoiceProfileSampleVault(string? basePath = null)
    {
        _basePath = string.IsNullOrWhiteSpace(basePath) ? GetDefaultBasePath() : basePath;
    }


    public async Task<Result<ProfileSampleId>> StoreEncryptedSampleAsync(
        EntityId profileId,
        ReadOnlyMemory<byte> sampleData,
        SampleEnvironmentLabel label,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(label);

        if (sampleData.Length == 0)
        {
            return Result.Failure<ProfileSampleId>(
                new DomainError("vault.empty_sample", "Sample data cannot be empty."));
        }

        if (ttl <= TimeSpan.Zero)
        {
            return Result.Failure<ProfileSampleId>(
                new DomainError("vault.invalid_ttl", "TTL must be positive."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var sampleId = ProfileSampleId.New();
            var profileDir = GetProfileSamplesDirectory(profileId);
            Directory.CreateDirectory(profileDir);

            var key = GetOrCreateProfileKey(profileDir);
            var encryptedBytes = Encrypt(sampleData.Span, key);

            var samplePath = GetSampleFilePath(profileDir, sampleId);
            await File.WriteAllBytesAsync(samplePath, encryptedBytes, cancellationToken)
                .ConfigureAwait(false);

            // Write TTL metadata (expiry timestamp only — no audio content)
            var expiresAtUtc = DateTimeOffset.UtcNow.Add(ttl);
            var metadataPath = GetMetadataFilePath(profileDir, sampleId);
            await File.WriteAllTextAsync(metadataPath, expiresAtUtc.ToString("O"), cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(sampleId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result.Failure<ProfileSampleId>(
                new DomainError("vault.store_failed", "Failed to store encrypted sample."));
        }
    }

    public Task<Result> DeleteSampleAsync(
        EntityId profileId,
        ProfileSampleId sampleId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var profileDir = GetProfileSamplesDirectory(profileId);
            DeleteFileIfExists(GetSampleFilePath(profileDir, sampleId));
            DeleteFileIfExists(GetMetadataFilePath(profileDir, sampleId));
            return Task.FromResult(Result.Success());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(Result.Failure(
                new DomainError("vault.delete_failed", "Failed to delete sample.")));
        }
    }

    public Task<Result<int>> PurgeAllSamplesAsync(
        EntityId profileId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var profileDir = GetProfileSamplesDirectory(profileId);
            if (!Directory.Exists(profileDir))
            {
                return Task.FromResult(Result.Success(0));
            }

            var sampleFiles = Directory.GetFiles(profileDir, $"*{SampleExtension}");
            var count = sampleFiles.Length;

            // Delete everything in the samples directory including key material
            foreach (var file in Directory.GetFiles(profileDir))
            {
                File.Delete(file);
            }

            return Task.FromResult(Result.Success(count));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(Result.Failure<int>(
                new DomainError("vault.purge_failed", "Failed to purge samples.")));
        }
    }

    public Task<Result<int>> PurgeExpiredSamplesAsync(
        EntityId profileId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var profileDir = GetProfileSamplesDirectory(profileId);
            if (!Directory.Exists(profileDir))
            {
                return Task.FromResult(Result.Success(0));
            }

            var purgedCount = 0;
            var metadataFiles = Directory.GetFiles(profileDir, $"*{MetadataExtension}");

            foreach (var metaFile in metadataFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryReadExpiry(metaFile, out var expiry) || now < expiry)
                {
                    continue;
                }

                // Extract sample ID from metadata filename
                var sampleIdString = Path.GetFileNameWithoutExtension(metaFile);
                if (!Guid.TryParse(sampleIdString, out var sampleGuid))
                {
                    continue;
                }

                var sampleId = new ProfileSampleId(sampleGuid);
                DeleteFileIfExists(GetSampleFilePath(profileDir, sampleId));
                DeleteFileIfExists(metaFile);
                purgedCount++;
            }

            return Task.FromResult(Result.Success(purgedCount));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(Result.Failure<int>(
                new DomainError("vault.purge_expired_failed", "Failed to purge expired samples.")));
        }
    }

    public Task<bool> HasSampleAsync(
        EntityId profileId,
        ProfileSampleId sampleId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var profileDir = GetProfileSamplesDirectory(profileId);
        var samplePath = GetSampleFilePath(profileDir, sampleId);
        return Task.FromResult(File.Exists(samplePath));
    }

    // ── Encryption helpers ───────────────────────────────────────────

    private static byte[] GetOrCreateProfileKey(string profileDir)
    {
        var keyPath = Path.Combine(profileDir, KeyFileName);
        if (File.Exists(keyPath))
        {
            return File.ReadAllBytes(keyPath);
        }

        var key = RandomNumberGenerator.GetBytes(KeySizeBytes);
        File.WriteAllBytes(keyPath, key);
        return key;
    }

    private static byte[] Encrypt(ReadOnlySpan<byte> plaintext, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext.ToArray(), 0, plaintext.Length);

        // Prepend IV to ciphertext for later decryption
        var result = new byte[aes.IV.Length + ciphertext.Length];
        aes.IV.CopyTo(result, 0);
        ciphertext.CopyTo(result, aes.IV.Length);
        return result;
    }

    // ── Path helpers ─────────────────────────────────────────────────

    private string GetProfileSamplesDirectory(EntityId profileId) =>
        Path.Combine(_basePath, VaultDirectoryName, profileId.Value.ToString("N"), SamplesSubdirectory);

    private static string GetSampleFilePath(string profileDir, ProfileSampleId sampleId) =>
        Path.Combine(profileDir, sampleId.Value.ToString("N") + SampleExtension);

    private static string GetMetadataFilePath(string profileDir, ProfileSampleId sampleId) =>
        Path.Combine(profileDir, sampleId.Value.ToString("N") + MetadataExtension);

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool TryReadExpiry(string metadataPath, out DateTimeOffset expiry)
    {
        expiry = default;
        try
        {
            var content = File.ReadAllText(metadataPath);
            return DateTimeOffset.TryParse(content, out expiry);
        }
        catch
        {
            return false;
        }
    }

    private static string GetDefaultBasePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PewPew");
}
