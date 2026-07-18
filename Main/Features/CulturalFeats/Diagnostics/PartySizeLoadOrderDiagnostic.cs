using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Diagnostics;

/// <summary>
/// TEMPORARY (2026-07-17) — strip once the load-order question is answered.
///
/// Player report: "when you load saved game max troop seems always go over" (party showing 114/111).
/// Hypothesis under test: on the load path, something reads <c>PartyBase.PartySizeLimit</c> BEFORE
/// <c>CareerCampaignBehavior.OnSessionLaunched</c> has run <c>CareerPassiveService.RefreshCache</c>.
/// <c>ApplyFlat</c> silently no-ops on a cache miss, so the limit would be computed WITHOUT the career
/// +2..+6 PartySize passive, then cached by the engine against the current <c>MemberRoster.VersionNo</c>
/// (<c>PartyBase.cs:343-355</c>) and stay wrong until the roster next changes.
///
/// This logs the FIRST main-party limit computation of the process. Read the result against the
/// surrounding "CareerSystem: Passive cache complete" line in the same log:
///   - diag line BEFORE  "cache complete"  => hypothesis CONFIRMED (limit computed passive-blind).
///   - diag line AFTER   "cache complete"  => hypothesis KILLED; the 114/111 is the intended
///                                            over-cap-by-upgrade mechanic, not a load bug.
/// One-shot + static: the limit getter is hot, and only the first computation after load is evidence.
/// </summary>
public static class PartySizeLoadOrderDiagnostic
{
    private static bool _logged;

    // `_logged` short-circuits first: the limit getter is hot, so every call after the first costs a
    // static bool read. The logger is INJECTED (threaded from TaomPartySizeModel's ctor) rather than
    // resolved here — a static helper is not a DI boundary, and a one-shot guard doesn't make
    // IoC.Resolve legal in one (deep-review 2026-07-17, Agent 1 HIGH).
    public static void RecordFirstMainPartyLimit(
        PartyBase? party, float resultLimit, ICareerPassiveService? careerPassives, IModLogger? logger)
    {
        if (_logged || logger == null || party == null || party != PartyBase.MainParty)
            return;

        _logged = true;

        var heroId = CareerPassiveHero.ResolveId(party);
        var careerPartySize = careerPassives?.GetPassiveMagnitude(heroId, PassiveEffectType.PartySize) ?? 0f;
        int raw = party.MemberRoster?.TotalManCount ?? -1;

        logger.LogInfo(
            $"[PartySizeDiag] FIRST main-party limit computation: result={resultLimit:0.##} raw={raw} " +
            $"hero='{heroId ?? "<null>"}' careerPartySizePassive={careerPartySize:0.##}. " +
            "If this line precedes 'CareerSystem: Passive cache complete', the limit was computed " +
            "passive-blind and is cached until the roster changes.");
    }
}
