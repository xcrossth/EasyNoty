using System.Text.Json;
using EasyNoty.Core;

namespace EasyNoty.Tests;

public sealed class ProtocolContractTests
{
    [Fact]
    public void All_protocol_valid_fixtures_deserialize_with_strict_wire_models_and_verify_hmac()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "ProtocolFixtures", "valid");
        var secret = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        var files = Directory.GetFiles(directory, "*.json");
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (!document.RootElement.TryGetProperty("message_type", out _)) continue; // QR is not an envelope.
            var envelope = document.RootElement.Deserialize<ProtocolEnvelope>(ProtocolV1.Json)!;
            AssertPayload(envelope);
            if (envelope.Authentication is not null) Assert.True(HmacAuthentication.Verify(envelope, secret), $"{Path.GetFileName(file)} expected {envelope.Authentication.PayloadSha256}, actual {HmacAuthentication.PayloadHash(envelope.Payload)} canonical {JcsCanonicalizer.CanonicalizeToString(envelope.Payload)}");
        }
    }

    private static void AssertPayload(ProtocolEnvelope envelope)
    {
        object? payload = envelope.MessageType switch
        {
            "hello" => envelope.Payload.Deserialize<HelloPayload>(ProtocolV1.Json),
            "pairing_request" => envelope.Payload.Deserialize<PairingRequest>(ProtocolV1.Json),
            "pairing_accept" => envelope.Payload.Deserialize<PairingAccept>(ProtocolV1.Json),
            "pairing_reject" => envelope.Payload.Deserialize<PairingReject>(ProtocolV1.Json),
            "auth_challenge" => envelope.Payload.Deserialize<AuthChallenge>(ProtocolV1.Json),
            "auth_response" => envelope.Payload.Deserialize<AuthResponse>(ProtocolV1.Json),
            "auth_success" => envelope.Payload.Deserialize<AuthSuccess>(ProtocolV1.Json),
            "notification_event" => envelope.Payload.Deserialize<NotificationEventPayload>(ProtocolV1.Json),
            "event_transfer_start" => envelope.Payload.Deserialize<EventTransferStart>(ProtocolV1.Json),
            "event_transfer_chunk" => envelope.Payload.Deserialize<EventTransferChunk>(ProtocolV1.Json),
            "event_transfer_complete" => envelope.Payload.Deserialize<EventTransferComplete>(ProtocolV1.Json),
            "event_ack" => envelope.Payload.Deserialize<EventAck>(ProtocolV1.Json),
            "asset_missing_request" => envelope.Payload.Deserialize<AssetMissingRequest>(ProtocolV1.Json),
            "asset_transfer_start" => envelope.Payload.Deserialize<AssetTransferStart>(ProtocolV1.Json),
            "asset_transfer_chunk" => envelope.Payload.Deserialize<AssetTransferChunk>(ProtocolV1.Json),
            "asset_transfer_complete" => envelope.Payload.Deserialize<AssetTransferComplete>(ProtocolV1.Json),
            "asset_ack" => envelope.Payload.Deserialize<AssetAck>(ProtocolV1.Json),
            "ping" or "pong" => envelope.Payload.Deserialize<KeepalivePayload>(ProtocolV1.Json),
            "error" => envelope.Payload.Deserialize<ProtocolError>(ProtocolV1.Json),
            _ => null
        };
        Assert.NotNull(payload);
    }
}
