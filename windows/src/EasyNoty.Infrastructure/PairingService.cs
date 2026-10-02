using System.Security.Cryptography;
using System.Text;
using EasyNoty.Core;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace EasyNoty.Infrastructure;

public sealed record DeviceCredential(string DeviceId, string KeyId, byte[] Secret);
public enum ReplayDisposition { New, Identical, Conflict }
public sealed record ReplayResult(ReplayDisposition Disposition, string? ResponseJson);

public sealed class PairingService(IDbContextFactory<EasyNotyDbContext> dbFactory, ISecretProtector protector)
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending = new(StringComparer.Ordinal);
    public IReadOnlyCollection<string> PendingDeviceIds => _pending.Keys.ToArray();

    public async Task<PairingAccept> RequestApprovalAsync(PairingRequest request, CancellationToken ct = default)
    {
        await ValidateAvailableAsync(request.PairingToken, ct);
        var approval = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(request.AndroidDeviceId, approval)) throw new InvalidOperationException("A pairing request for this device is already pending.");
        try
        {
            using var registration = ct.Register(() => approval.TrySetCanceled(ct));
            if (!await approval.Task) throw new UnauthorizedAccessException("Pairing was rejected by the user.");
            return await AcceptAsync(request, ct);
        }
        finally { _pending.TryRemove(request.AndroidDeviceId, out _); }
    }

    public bool Approve(string deviceId) => _pending.TryGetValue(deviceId, out var pending) && pending.TrySetResult(true);
    public bool Reject(string deviceId) => _pending.TryGetValue(deviceId, out var pending) && pending.TrySetResult(false);

    private async Task ValidateAvailableAsync(string token, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var hash = TokenHash(token);
        var session = await db.PairingSessions.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct) ?? throw new UnauthorizedAccessException("Pairing token is invalid.");
        if (session.ConsumedAt is not null) throw new UnauthorizedAccessException("Pairing token was already used.");
        if (session.ExpiresAt < DateTimeOffset.UtcNow) throw new UnauthorizedAccessException("Pairing token expired.");
    }
    public async Task<PairingQr> CreateAsync(string pcDeviceId, string pcName, string certificateFingerprint, IReadOnlyList<PairingEndpoint> endpoints, CancellationToken ct = default)
    {
        var token = WireValues.Base64Url(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(2);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.PairingSessions.Add(new PairingSessionEntity { TokenHash = TokenHash(token), PcDeviceId = pcDeviceId, PcName = pcName, CreatedAt = now, ExpiresAt = expires });
        await db.SaveChangesAsync(ct);
        return new(ProtocolV1.Version, pcDeviceId, pcName, endpoints, certificateFingerprint, token, WireValues.Utc(expires));
    }

    public async Task<PairingAccept> AcceptAsync(PairingRequest request, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var tokenHash = TokenHash(request.PairingToken);
        var session = await db.PairingSessions.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, ct) ?? throw new UnauthorizedAccessException("Pairing token is invalid.");
        if (session.ConsumedAt is not null) throw new UnauthorizedAccessException("Pairing token was already used.");
        if (session.ExpiresAt < now) throw new UnauthorizedAccessException("Pairing token expired.");
        var secret = RandomNumberGenerator.GetBytes(32);
        var protectedSecret = protector.Protect(secret);
        var keyId = WireValues.Uuid();
        var serverNonce = WireValues.Base64Url(RandomNumberGenerator.GetBytes(16));
        var device = await db.Devices.SingleOrDefaultAsync(x => x.DeviceId == request.AndroidDeviceId, ct);
        if (device is null)
        {
            device = new DeviceEntity { DeviceId = request.AndroidDeviceId, DisplayName = request.AndroidName, PairedAt = now, CreatedAt = now, UpdatedAt = now };
            db.Devices.Add(device);
        }
        device.DisplayName = request.AndroidName;
        device.SharedSecretRef = protectedSecret;
        device.KeyId = keyId;
        device.IsRevoked = false;
        device.UpdatedAt = now;
        session.ConsumedAt = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(session.PcDeviceId, session.PcName, keyId, WireValues.Base64Url(secret), PairingCode.CreateVerificationCode(secret, request.AndroidDeviceId, session.PcDeviceId), serverNonce, WireValues.Utc(now));
    }

    public async Task<DeviceCredential> GetCredentialAsync(string deviceId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(x => x.DeviceId == deviceId && !x.IsRevoked, ct) ?? throw new UnauthorizedAccessException("Unknown or revoked device.");
        if (device.IsPaused) throw new UnauthorizedAccessException("Device is paused.");
        return new(device.DeviceId, device.KeyId ?? throw new UnauthorizedAccessException("Device has no key id."), protector.Unprotect(device.SharedSecretRef ?? throw new UnauthorizedAccessException("Device has no secret.")));
    }

    public async Task<ReplayResult> CheckReplayAsync(string messageId, string deviceId, string sessionId, string signedDigest, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var id = Guid.Parse(messageId);
        var existing = await db.ProcessedMessages.AsNoTracking().SingleOrDefaultAsync(x => x.MessageId == id && x.DeviceId == deviceId && x.SessionId == sessionId, ct);
        return existing is null ? new(ReplayDisposition.New, null) : existing.SignedDigest == signedDigest ? new(ReplayDisposition.Identical, existing.ResponseJson) : new(ReplayDisposition.Conflict, null);
    }

    public async Task CacheResponseAsync(string messageId, string deviceId, string sessionId, string signedDigest, string responseJson, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.ProcessedMessages.Add(new ProcessedMessageEntity { MessageId = Guid.Parse(messageId), DeviceId = deviceId, SessionId = sessionId, SignedDigest = signedDigest, ResponseJson = responseJson, ProcessedAt = DateTimeOffset.UtcNow });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { }
    }

    private static string TokenHash(string token) => Hashing.Sha256Hex(Encoding.UTF8.GetBytes(token));
}
