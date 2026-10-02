using System.IO.Compression;
using System.Security.Cryptography;
using EasyNoty.Core;
using Microsoft.EntityFrameworkCore;

namespace EasyNoty.Infrastructure;

public sealed record StoragePaths(string Root, string Database, string Assets, string Logs, bool IsPortable, string? FallbackReason);

public static class StoragePathResolver
{
    public static StoragePaths Resolve(string executableDirectory)
    {
        var portable = Path.Combine(executableDirectory, "data");
        try
        {
            Directory.CreateDirectory(portable);
            var probe = Path.Combine(portable, $".write-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return Create(portable, true, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyNoty");
            Directory.CreateDirectory(local);
            return Create(local, false, ex.Message);
        }
    }

    private static StoragePaths Create(string root, bool portable, string? reason)
    {
        var assets = Path.Combine(root, "assets");
        var logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(assets);
        Directory.CreateDirectory(logs);
        return new(root, Path.Combine(root, "app.db"), assets, logs, portable, reason);
    }
}

public interface ISecretProtector
{
    string Protect(ReadOnlySpan<byte> secret);
    byte[] Unprotect(string protectedValue);
}

public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = "EasyNoty-v1-device-secret"u8.ToArray();
    public string Protect(ReadOnlySpan<byte> secret) => Convert.ToBase64String(ProtectedData.Protect(secret.ToArray(), Entropy, DataProtectionScope.CurrentUser));
    public byte[] Unprotect(string protectedValue) => ProtectedData.Unprotect(Convert.FromBase64String(protectedValue), Entropy, DataProtectionScope.CurrentUser);
}

public sealed class TestSecretProtector : ISecretProtector
{
    public string Protect(ReadOnlySpan<byte> secret) => Convert.ToBase64String(secret);
    public byte[] Unprotect(string protectedValue) => Convert.FromBase64String(protectedValue);
}

public sealed record AssetStoreResult(string Hash, long ByteSize, bool Duplicate, DateTimeOffset VerifiedAt);

public sealed class AssetStore(StoragePaths paths, IDbContextFactory<EasyNotyDbContext> dbFactory)
{
    public async Task<bool> ExistsAsync(string hash, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Assets.AnyAsync(x => x.Hash == Hashing.NormalizeSha256(hash), ct);
    }

    public async Task<AssetStoreResult> StoreAsync(string expectedHash, string mime, ReadOnlyMemory<byte> payload, string encoding = "none", CancellationToken ct = default)
    {
        var canonical = Decode(payload, encoding);
        if (canonical.Length > ProtocolV1.MaximumAssetBytes) throw new InvalidDataException("Asset exceeds the 25 MB limit.");
        var normalized = Hashing.NormalizeSha256(expectedHash);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(normalized), SHA256.HashData(canonical))) throw new InvalidDataException("Asset SHA-256 mismatch.");
        var directory = Path.Combine(paths.Assets, normalized[..2]);
        Directory.CreateDirectory(directory);
        var final = Path.Combine(directory, normalized);
        var temp = final + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllBytesAsync(temp, canonical, ct);
        File.Move(temp, final, true);
        var now = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var asset = await db.Assets.FindAsync([normalized], ct);
        var duplicate = asset is not null;
        if (asset is null)
            db.Assets.Add(new AssetEntity { Hash = normalized, Mime = mime, ByteSize = canonical.Length, LocalPath = final, FirstSeenAt = now, LastSeenAt = now, CreatedAt = now });
        else
            asset.LastSeenAt = now;
        await db.SaveChangesAsync(ct);
        return new(normalized, canonical.Length, duplicate, now);
    }

    private static byte[] Decode(ReadOnlyMemory<byte> payload, string encoding)
    {
        if (encoding.Equals("none", StringComparison.OrdinalIgnoreCase)) return payload.ToArray();
        using var input = new MemoryStream(payload.ToArray());
        using Stream decoder = encoding.Equals("zlib", StringComparison.OrdinalIgnoreCase)
            ? new ZLibStream(input, CompressionMode.Decompress)
            : throw new NotSupportedException($"Encoding '{encoding}' is unavailable; sender must retry with zlib or none.");
        using var output = new MemoryStream();
        decoder.CopyTo(output);
        return output.ToArray();
    }
}

public sealed class DatabaseInitializer(IDbContextFactory<EasyNotyDbContext> dbFactory)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", ct);
    }
}
