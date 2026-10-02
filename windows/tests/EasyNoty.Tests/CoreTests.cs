using System.Text;
using System.Text.Json;
using EasyNoty.Core;

namespace EasyNoty.Tests;

public sealed class CoreTests
{
    [Fact]
    public void Hmac_signature_detects_payload_tampering()
    {
        var secret = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        var payload = JsonSerializer.SerializeToElement(new { value = "original" }, ProtocolV1.Json);
        var envelope = new ProtocolEnvelope(1, "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "notification_event", "22222222-2222-4222-8222-222222222222", "2026-07-05T06:20:00Z", "55555555-5555-4555-8555-555555555555", payload, null);
        var signed = envelope with { Authentication = HmacAuthentication.Sign(envelope, "44444444-4444-4444-8444-444444444444", secret) };
        Assert.True(HmacAuthentication.Verify(signed, secret));
        var changed = signed with { Payload = JsonSerializer.SerializeToElement(new { value = "changed" }, ProtocolV1.Json) };
        Assert.False(HmacAuthentication.Verify(changed, secret));
    }

    [Fact]
    public void Hmac_matches_normative_protocol_vector()
    {
        var secret = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        var envelope = new ProtocolEnvelope(1, "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "ping", "22222222-2222-4222-8222-222222222222", "2026-07-05T06:20:00Z", "55555555-5555-4555-8555-555555555555", JsonSerializer.SerializeToElement(new { nonce = "abcdefgh" }), null);
        var authentication = HmacAuthentication.Sign(envelope, "44444444-4444-4444-8444-444444444444", secret);
        Assert.Equal("sha256:88ed2b4a4d4114c24d6ea3f34f1392fe8ec293f2e2f5b18ddd25358b9f82669f", authentication.PayloadSha256);
        Assert.Equal("647cbc92c620876484e1d34a599213aedc0ed9974e74a24154119900fd319c6a", authentication.Signature);
    }

    [Fact]
    public void Parser_prefers_messaging_then_gmail_then_generic()
    {
        var engine = new NotificationParserEngine([new GenericAndroidParser(), new GmailFallbackParser(), new MessagingStyleParser()]);
        var messaging = TestEvent("com.chat", "{\"conversation_title\":\"Team\",\"messages\":[{\"text\":\"Hello\"}]}", style: "messaging");
        Assert.Equal("messaging_style", engine.Parse(messaging).ParserName);
        Assert.Equal("Hello", engine.Parse(messaging).Body);
        Assert.Equal("gmail_fallback", engine.Parse(TestEvent("com.google.android.gm", "{\"title\":\"Subject\",\"big_text\":\"Mail\"}")).ParserName);
        Assert.Equal("generic_android", engine.Parse(TestEvent("app.test", "{\"title\":\"Title\",\"text\":\"Body\"}")).ParserName);
    }

    [Fact]
    public void Pairing_verification_code_is_stable_and_six_digits()
    {
        var secret = new byte[32];
        var first = PairingCode.CreateVerificationCode(secret, "phone", "pc");
        Assert.Equal(first, PairingCode.CreateVerificationCode(secret, "phone", "pc"));
        Assert.Matches("^[0-9]{6}$", first);
    }

    internal static NotificationEvent TestEvent(string package, string raw, bool ongoing = false, int revision = 0, string? instance = null, string? style = null, Guid? eventId = null, DateTimeOffset? posted = null, bool summary = false)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        return new(eventId ?? Guid.NewGuid(), instance ?? Guid.NewGuid().ToString("N"), revision, "key", "phone-1", package, "Test App", posted ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, raw, bytes.Length, Hashing.Sha256Hex(bytes), ongoing, summary, style, null, null, []);
    }
}
