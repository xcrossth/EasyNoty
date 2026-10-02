using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Text.Encodings.Web;

namespace EasyNoty.Core;

public interface INotificationParser
{
    int Priority { get; }
    bool CanParse(NotificationEvent notification);
    ParsedNotification Parse(NotificationEvent notification);
}

public sealed class NotificationParserEngine(IEnumerable<INotificationParser> parsers)
{
    private readonly INotificationParser[] _parsers = parsers.OrderByDescending(x => x.Priority).ToArray();

    public ParsedNotification Parse(NotificationEvent notification) =>
        _parsers.FirstOrDefault(x => x.CanParse(notification))?.Parse(notification)
        ?? notification.AndroidParsedResult
        ?? new ParsedNotification("empty", 1, notification.AppName, null, null, null, null, notification.GroupKey, 0.1);
}

public sealed class MessagingStyleParser : INotificationParser
{
    public int Priority => 300;
    public bool CanParse(NotificationEvent n) =>
        string.Equals(n.NotificationStyle, "messaging", StringComparison.OrdinalIgnoreCase) || n.RawSnapshotJson.Contains("messages", StringComparison.OrdinalIgnoreCase);

    public ParsedNotification Parse(NotificationEvent n)
    {
        using var doc = JsonDocument.Parse(n.RawSnapshotJson);
        var root = doc.RootElement;
        var title = JsonValue(root, "conversation_title") ?? JsonValue(root, "title") ?? n.AndroidParsedResult?.Title;
        var sender = JsonValue(root, "sender_name") ?? JsonValue(root, "sender") ?? n.AndroidParsedResult?.SenderName;
        var body = LastMessage(root) ?? JsonValue(root, "text") ?? n.AndroidParsedResult?.Body;
        return new("messaging_style", 1, title ?? sender, body, body, sender, title, n.GroupKey, 0.9);
    }

    private static string? LastMessage(JsonElement root)
    {
        if (!root.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array) return null;
        var last = messages.EnumerateArray().LastOrDefault();
        return last.ValueKind == JsonValueKind.Object ? JsonValue(last, "text") : null;
    }

    internal static string? JsonValue(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

public sealed class GmailFallbackParser : INotificationParser
{
    public int Priority => 200;
    public bool CanParse(NotificationEvent n) => n.PackageName.Contains("gmail", StringComparison.OrdinalIgnoreCase) || n.PackageName.Equals("com.google.android.gm", StringComparison.OrdinalIgnoreCase);
    public ParsedNotification Parse(NotificationEvent n)
    {
        using var doc = JsonDocument.Parse(n.RawSnapshotJson);
        var title = MessagingStyleParser.JsonValue(doc.RootElement, "title") ?? n.AndroidParsedResult?.Title;
        var body = MessagingStyleParser.JsonValue(doc.RootElement, "big_text") ?? MessagingStyleParser.JsonValue(doc.RootElement, "text") ?? n.AndroidParsedResult?.Body;
        var sender = MessagingStyleParser.JsonValue(doc.RootElement, "sub_text") ?? n.AndroidParsedResult?.SenderName;
        return new("gmail_fallback", 1, title, body, body, sender, null, n.GroupKey, 0.75);
    }
}

public sealed class GenericAndroidParser : INotificationParser
{
    public int Priority => 100;
    public bool CanParse(NotificationEvent notification) => true;
    public ParsedNotification Parse(NotificationEvent n)
    {
        using var doc = JsonDocument.Parse(n.RawSnapshotJson);
        var title = MessagingStyleParser.JsonValue(doc.RootElement, "title") ?? n.AndroidParsedResult?.Title ?? n.AppName;
        var body = MessagingStyleParser.JsonValue(doc.RootElement, "big_text") ?? MessagingStyleParser.JsonValue(doc.RootElement, "text") ?? n.AndroidParsedResult?.Body;
        var sender = MessagingStyleParser.JsonValue(doc.RootElement, "sender") ?? n.AndroidParsedResult?.SenderName;
        return new("generic_android", 1, title, body, body, sender, n.AndroidParsedResult?.ConversationTitle, n.GroupKey, body is null ? 0.4 : 0.7);
    }
}

public enum InboxView { Inbox, Archived, All }

public sealed record InboxQuery(
    InboxView View = InboxView.Inbox,
    IReadOnlyCollection<string>? DeviceIds = null,
    IReadOnlyCollection<string>? PackageNames = null,
    string? Search = null,
    int Skip = 0,
    int Take = 100);

public sealed record InboxItem(
    Guid Id,
    string DeviceId,
    string PackageName,
    string AppName,
    DateTimeOffset PostedAt,
    string? Title,
    string? Preview,
    bool IsSensitive,
    bool IsRevealed,
    bool IsRead,
    bool IsArchived,
    int RevisionIndex,
    bool IsOversized);

public sealed record FilterCount(string Key, long UnreadCount, long TotalCount);

public sealed class SensitiveRevealSession
{
    private readonly ConcurrentDictionary<Guid, byte> _revealed = new();
    public void Reveal(Guid notificationId) => _revealed[notificationId] = 0;
    public bool IsRevealed(Guid notificationId) => _revealed.ContainsKey(notificationId);
    public void Clear() => _revealed.Clear();
    public void ClearExcept(Guid notificationId)
    {
        _revealed.Clear();
        _revealed[notificationId] = 0;
    }
}

public sealed record RetentionPolicy(int? ArchiveAfterDays = 30, int? DeleteAfterDays = null);

public static class Hashing
{
    public static string Sha256Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string NormalizeSha256(string hash) => hash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? hash[7..].ToLowerInvariant() : hash.ToLowerInvariant();
}

public static class HmacAuthentication
{
    public static string PayloadHash(JsonElement payload) => "sha256:" + Hashing.Sha256Hex(JcsCanonicalizer.Canonicalize(payload));

    public static string Canonical(ProtocolEnvelope envelope) => string.Join('\n',
        envelope.ProtocolVersion,
        envelope.MessageId,
        envelope.MessageType,
        envelope.DeviceId,
        envelope.SentAtUtc,
        envelope.SessionId ?? string.Empty,
        PayloadHash(envelope.Payload));

    public static MessageAuthentication Sign(ProtocolEnvelope envelope, string keyId, ReadOnlySpan<byte> secret)
    {
        var bytes = Encoding.UTF8.GetBytes(Canonical(envelope));
        return new("hmac-sha256", keyId, PayloadHash(envelope.Payload), Convert.ToHexStringLower(HMACSHA256.HashData(secret, bytes)));
    }

    public static bool Verify(ProtocolEnvelope envelope, ReadOnlySpan<byte> secret)
    {
        if (envelope.Authentication is not { Algorithm: "hmac-sha256" } authentication) return false;
        try
        {
            var payloadHash = PayloadHash(envelope.Payload);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(payloadHash), Encoding.ASCII.GetBytes(authentication.PayloadSha256))) return false;
            var expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(Canonical(envelope with { Authentication = null })));
            var supplied = Convert.FromHexString(authentication.Signature);
            return CryptographicOperations.FixedTimeEquals(expected, supplied);
        }
        catch (FormatException) { return false; }
    }
}

public static class JcsCanonicalizer
{
    public static byte[] Canonicalize(JsonElement element) => Encoding.UTF8.GetBytes(CanonicalizeToString(element));
    public static string CanonicalizeToString(JsonElement element)
    {
        var output = new StringBuilder();
        Write(element, output);
        return output.ToString();
    }

    private static void Write(JsonElement value, StringBuilder output)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                output.Append('{');
                var first = true;
                foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    if (!first) output.Append(',');
                    first = false;
                    WriteString(property.Name, output);
                    output.Append(':');
                    Write(property.Value, output);
                }
                output.Append('}');
                break;
            case JsonValueKind.Array:
                output.Append('[');
                for (var index = 0; index < value.GetArrayLength(); index++)
                {
                    if (index > 0) output.Append(',');
                    Write(value[index], output);
                }
                output.Append(']');
                break;
            case JsonValueKind.String: WriteString(value.GetString()!, output); break;
            case JsonValueKind.Number: output.Append(CanonicalNumber(value.GetRawText())); break;
            case JsonValueKind.True: output.Append("true"); break;
            case JsonValueKind.False: output.Append("false"); break;
            case JsonValueKind.Null: output.Append("null"); break;
            default: throw new InvalidDataException("Unsupported JSON token in canonical payload.");
        }
    }

    private static void WriteString(string value, StringBuilder output)
    {
        output.Append('"');
        output.Append(JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString());
        output.Append('"');
    }

    private static string CanonicalNumber(string raw)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || double.IsNaN(number) || double.IsInfinity(number)) throw new InvalidDataException("JCS requires a finite IEEE-754 number.");
        if (number == 0) return "0";
        var text = number.ToString("R", CultureInfo.InvariantCulture).Replace('E', 'e');
        var marker = text.IndexOf('e');
        if (marker < 0) return text;
        var mantissa = text[..marker];
        var exponent = text[(marker + 1)..];
        var negative = exponent.StartsWith('-');
        exponent = exponent.TrimStart('+', '-').TrimStart('0');
        if (exponent.Length == 0) exponent = "0";
        return $"{mantissa}e{(negative ? "-" : string.Empty)}{exponent}";
    }
}

public static class WireValues
{
    public static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    public static string Uuid() => Guid.NewGuid().ToString("D").ToLowerInvariant();
    public static string Base64Url(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - base64.Length % 4) % 4);
        return Convert.FromBase64String(base64);
    }
}

public static class SessionAuthentication
{
    public static string Proof(ReadOnlySpan<byte> secret, string challengeId, string serverNonce, string clientNonce, string androidDeviceId)
    {
        var input = Encoding.UTF8.GetBytes($"auth-v1\n{challengeId}\n{serverNonce}\n{clientNonce}\n{androidDeviceId}");
        return Convert.ToHexStringLower(HMACSHA256.HashData(secret, input));
    }
}

public sealed class PairingCode
{
    public static string CreateVerificationCode(ReadOnlySpan<byte> sharedSecret, string deviceId, string pcDeviceId)
    {
        var input = Encoding.UTF8.GetBytes($"{deviceId}\n{pcDeviceId}");
        var hash = HMACSHA256.HashData(sharedSecret, input);
        return (BitConverter.ToUInt32(hash, 0) % 1_000_000).ToString("D6");
    }
}
