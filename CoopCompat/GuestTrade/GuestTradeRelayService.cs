using System;
using System.Linq;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Transport;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CoopInterop.GuestTrade;

namespace TAOM.CoopCompat.GuestTrade;

/// <summary>
/// Carries guest trade (#1329) over Coop: a guest's request to the host, and the host's reply back to that guest.
/// TAOM's <see cref="GuestTradeHost"/> decides and acts; TAOM's <see cref="GuestTradeClient"/> waits and shows. A
/// message from another protocol version is dropped. Design credit: minimalcash63 (TAOM.CoopEmissaryArmory) and
/// Sternab (TAOM-Coop).
/// </summary>
public sealed class GuestTradeRelayService : IGuestTradeLink
{
    private readonly ICoopTransport _transport;
    private readonly GuestTradeClient _client;
    private readonly GuestTradeHost _host;
    private readonly IModLogger _logger;
    private bool _protocolMismatchLogged;

    public GuestTradeRelayService(ICoopTransport transport, GuestTradeClient client, GuestTradeHost host, IModLogger logger)
    {
        _transport = transport;
        _client = client;
        _host = host;
        _logger = logger;
        _transport.TradeRequestReceived += OnRequest;
        _transport.TradeReplyReceived += OnReply;
        _client.Attach(this);
    }

    public bool SendToHost(GuestTradeRequest request) => _transport.SendToServer(new CoopCompatTradeRequest
    {
        ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
        RequestId = request.RequestId,
        Action = (int)request.Action,
        SettlementId = request.SettlementId ?? "",
        Subject = request.Subject ?? "",
        Modifier = request.Modifier ?? "",
        Target = request.Target ?? "",
        Quantity = request.Quantity,
    });

    private void OnRequest(CoopPeer peer, string heroId, CoopCompatTradeRequest message)
    {
        if (!SameProtocol(message.ProtocolVersion)) return;

        var request = new GuestTradeRequest((GuestTradeAction)message.Action, message.SettlementId ?? "", OrNull(message.Subject),
            OrNull(message.Modifier), OrNull(message.Target), message.Quantity, message.RequestId ?? "");
        var reply = _host.Handle(heroId, request);
        if (!_transport.SendToPeer(peer, ToWire(reply)))
            _logger.LogWarning($"[GuestTrade] could not send the reply to {request.Action} for {heroId} (peer {peer.Label}).");
    }

    private void OnReply(CoopCompatTradeReply message)
    {
        if (!SameProtocol(message.ProtocolVersion)) return;

        _client.Receive(new GuestTradeReply(message.RequestId ?? "", (GuestTradeAction)message.Action, (GuestTradeRefusal)message.Refusal,
            message.Status, message.Quantity, message.TotalCost, OrNull(message.ResourceName), OrNull(message.ResourceId),
            message.HasBalance ? message.Balance : null, message.HasArmouryLevel ? message.ArmouryLevel : null, LadderOf(message)));
    }

    /// <summary>The same caps the server applies; anything it would never send gives no view.</summary>
    private static GuestLadderView? LadderOf(CoopCompatTradeReply message)
    {
        if (!message.HasLadder || !Enum.IsDefined(typeof(LadderSlot), message.LadderSlot))
            return null;
        var material = Fits(message.LadderMaterialId) ? message.LadderMaterialId : null;
        var choices = (message.LadderChoices ?? Array.Empty<string>()).Where(Fits).Take(GuestLadderView.MaxChoices).ToList();
        return new GuestLadderView((LadderSlot)message.LadderSlot, message.LadderReady, message.LadderCanClaim, message.LadderLordLevel,
            material, material == null ? 0 : message.LadderNeeded, material == null ? 0 : message.LadderCarried, choices);
    }

    private static bool Fits(string? id) => !string.IsNullOrEmpty(id) && id!.Length <= GuestLadderView.MaxIdLength;

    private static CoopCompatTradeReply ToWire(GuestTradeReply reply) => new()
    {
        ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
        RequestId = reply.RequestId,
        Action = (int)reply.Action,
        Refusal = (int)reply.Refusal,
        Status = reply.Status,
        Quantity = reply.Quantity,
        TotalCost = reply.TotalCost,
        ResourceName = reply.ResourceName ?? "",
        ResourceId = reply.ResourceId ?? "",
        HasBalance = reply.Balance.HasValue,
        Balance = reply.Balance ?? 0f,
        HasArmouryLevel = reply.ArmouryLevel.HasValue,
        ArmouryLevel = reply.ArmouryLevel ?? 0,
        HasLadder = reply.Ladder != null,
        LadderSlot = (int)(reply.Ladder?.Slot ?? default),
        LadderReady = reply.Ladder?.Ready ?? false,
        LadderCanClaim = reply.Ladder?.CanClaim ?? false,
        LadderLordLevel = reply.Ladder?.LordLevel ?? 0,
        LadderMaterialId = reply.Ladder?.MaterialId ?? "",
        LadderNeeded = reply.Ladder?.Needed ?? 0,
        LadderCarried = reply.Ladder?.Carried ?? 0,
        LadderChoices = reply.Ladder?.Choices.ToArray() ?? Array.Empty<string>(),
    };

    private bool SameProtocol(int version)
    {
        if (version == CoopCompatProtocol.ProtocolVersion) return true;

        if (!_protocolMismatchLogged)
        {
            _protocolMismatchLogged = true;
            _logger.LogWarning($"[GuestTrade] dropped a trade message of protocol {version}; this build speaks {CoopCompatProtocol.ProtocolVersion}.");
        }
        return false;
    }

    private static string? OrNull(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
