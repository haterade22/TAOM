using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy.Hooks;

public class AllianceActionHook : IOnAllianceAction
{
    private readonly IDiplomacyService _diplomacyService;
    private readonly IModLogger _logger;
    private readonly ICoopSessionProvider _coop;

    public AllianceActionHook(
        IDiplomacyService diplomacyService, IModLogger logger, ICoopSessionProvider coop)
    {
        _diplomacyService = diplomacyService;
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

    public bool ShouldPreventWarDeclaration(string factionAId, string factionBId)
    {
        if (DeferToHost("war declaration", factionAId, factionBId)) return false;

        var blocked = !_diplomacyService.IsWarAllowed(factionAId, factionBId);
        if (blocked)
            _logger.LogInfo($"[Diplomacy] War declaration blocked: {factionAId} <-> {factionBId}");
        return blocked;
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
