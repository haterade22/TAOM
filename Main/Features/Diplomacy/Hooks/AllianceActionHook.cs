using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy.Hooks;

public class AllianceActionHook : IOnAllianceAction
{
    private readonly IDiplomacyService _diplomacyService;
    private readonly IWarOfTheRingService _wotrService;
    private readonly IModLogger _logger;
    private readonly ICoopSessionProvider _coop;

    public AllianceActionHook(
        IDiplomacyService diplomacyService, IWarOfTheRingService wotrService, IModLogger logger,
        ICoopSessionProvider coop)
    {
        _diplomacyService = diplomacyService;
        _wotrService = wotrService;
        _logger = logger;
        _coop = coop;
    }

    public bool ShouldPreventAllianceEnd(string kingdomAId, string kingdomBId)
    {
        if (DeferToHost("alliance end", kingdomAId, kingdomBId)) return false;

        var isPermanent = _diplomacyService.GetRelationshipTier(kingdomAId, kingdomBId) == AllianceTier.Permanent;
        if (isPermanent)
            _logger.LogInfo($"[Diplomacy] Alliance end blocked: {kingdomAId} <-> {kingdomBId} (Permanent)");
        return isPermanent;
    }

    public bool ShouldPreventWarDeclaration(string factionAId, string factionBId, bool causedByPlayer = false)
    {
        if (DeferToHost("war declaration", factionAId, factionBId)) return false;

        var veto = _wotrService.GetWarVeto(factionAId, factionBId, causedByPlayer);
        if (veto != WarVeto.None)
            _logger.LogInfo($"[Diplomacy] War declaration blocked: {factionAId} <-> {factionBId} ({veto})");
        return veto != WarVeto.None;
    }

    public bool ShouldPreventCallToWar(string calledKingdomId, string targetKingdomId)
    {
        if (ShouldPreventWarDeclaration(calledKingdomId, targetKingdomId)) return true;
        // ShouldPreventWarDeclaration already returned false under co-op; the host's rule governs the refusal too.
        if (_coop.ShouldDeferToHost || !_diplomacyService.RefusesCallToWar(calledKingdomId, targetKingdomId))
            return false;

        _logger.LogInfo($"[Diplomacy] Call to war refused: {calledKingdomId} will not join a war against {targetKingdomId}");
        return true;
    }

    public string GetWarRefusalText(string kingdomAId, string kingdomBId)
    {
        if (_coop.ShouldDeferToHost) return null;

        var veto = _wotrService.GetWarVeto(kingdomAId, kingdomBId, causedByPlayer: false);
        return veto == WarVeto.None ? null : WarVetoReasons.Text(veto);
    }

    public string GetCallToWarRefusalText(string calledKingdomId, string targetKingdomId)
    {
        var warRefusal = GetWarRefusalText(calledKingdomId, targetKingdomId);
        if (warRefusal != null || _coop.ShouldDeferToHost) return warRefusal;

        return _diplomacyService.RefusesCallToWar(calledKingdomId, targetKingdomId) ? WarVetoReasons.CallToWarRefused : null;
    }

    public float GetDeclareWarScorePenalty(
        string factionAId, bool factionAIsKingdom, string factionBId, bool factionBIsKingdom)
    {
        if (_coop.ShouldDeferToHost || !factionAIsKingdom || !factionBIsKingdom) return 0f;

        return _wotrService.GetDeclareWarScorePenalty(factionAId, factionBId);
    }

    /// <summary>
    /// #370 — under a co-op session the host's diplomacy is authoritative, so TAOM's veto is off.
    ///
    /// TAOM's diplomacy prefixes are <c>Priority.High</c> (600) and therefore run BEFORE
    /// BannerlordTogether's own suppression prefix (default <c>Normal</c>, 400) on the same
    /// method. When a client applies a war/peace the host already committed, BT lets it through
    /// (its <c>IsApplyingSync</c> guard) — but TAOM's WotR/culture rule would still be free to
    /// return true and skip the vanilla body. The result is not a crash: it is one peer at war and
    /// the other at peace, with no log and two saves that disagree. Deferring is the only correct
    /// answer, because TAOM cannot know which peer's ruleset the session agreed on.
    ///
    /// Deliberately gated on TAOM's own <c>CoopPresence</c> rather than on BT's
    /// <c>KingdomSyncBehavior.IsApplyingSync</c>: reflecting into a private field of another mod
    /// couples us to their internals and breaks silently on their next build.
    /// </summary>
    private bool DeferToHost(string action, string aId, string bId)
    {
        if (!_coop.ShouldDeferToHost) return false;
        _logger.LogDebug(
            $"[Diplomacy][coop] TAOM {action} veto skipped for {aId} <-> {bId} — host is authoritative");
        return true;
    }
}
