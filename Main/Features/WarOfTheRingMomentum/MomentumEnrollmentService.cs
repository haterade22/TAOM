using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

public class MomentumEnrollmentService : IMomentumEnrollmentService
{
    private readonly IWarOfTheRingService _wotrService;
    private readonly IAllianceAdapter _allianceAdapter;
    private readonly IAlignmentService _alignmentService;
    private readonly IModLogger _logger;

    public MomentumEnrollmentService(
        IWarOfTheRingService wotrService,
        IAllianceAdapter allianceAdapter,
        IAlignmentService alignmentService,
        IModLogger logger)
    {
        _wotrService = wotrService;
        _allianceAdapter = allianceAdapter;
        _alignmentService = alignmentService;
        _logger = logger;
    }

    public bool SweepEnrollment(MomentumWarState state)
    {
        if (state.HasWarEnded)
            return false;
        if (_wotrService.CurrentPhase != WarPhase.FullWar)
            return false;

        var liveKingdomIds = _allianceAdapter.GetAllKingdomIds();
        bool changed = PruneStaleKingdoms(state, liveKingdomIds);

        foreach (var kingdomId in liveKingdomIds)
        {
            if (state.DoesKingdomTakePart(kingdomId))
                continue;

            switch (ResolveSide(kingdomId))
            {
                case FactionSide.Free:
                    if (state.Free.AddKingdom(kingdomId))
                    {
                        _logger.LogInfo($"[Momentum] {kingdomId} enrolled on the Free side");
                        changed = true;
                    }
                    break;
                case FactionSide.Evil:
                    if (state.Evil.AddKingdom(kingdomId))
                    {
                        _logger.LogInfo($"[Momentum] {kingdomId} enrolled on the Evil side");
                        changed = true;
                    }
                    break;
                // Neutral (Umbar/Shaghana/Abanissa/Khand) never enrolls.
            }
        }

        if (!state.HasWarStarted && (state.Free.KingdomIds.Count > 0 || state.Evil.KingdomIds.Count > 0))
        {
            state.MarkWarStarted();
            _logger.LogInfo("[Momentum] The War of the Ring momentum tracking has begun");
            changed = true;
        }

        return changed;
    }

    public bool RemoveKingdom(MomentumWarState state, string kingdomId)
    {
        bool removed = state.Free.RemoveKingdom(kingdomId) || state.Evil.RemoveKingdom(kingdomId);
        if (removed)
            _logger.LogInfo($"[Momentum] {kingdomId} removed from the war (kingdom destroyed)");
        return removed;
    }

    // Side = kingdom-id alignment, falling back to the kingdom's CULTURE when the
    // kingdom id isn't in alignment.json. This catches player-founded / dynamically
    // created kingdoms (id like "new_kingdom") whose culture IS classified — without
    // it, the player's own kingdom would resolve Neutral and never enroll, hiding the
    // whole feature from a player who founds a kingdom (Codex #327 HIGH).
    private FactionSide ResolveSide(string kingdomId)
    {
        var side = _alignmentService.GetKingdomSide(kingdomId);
        if (side != FactionSide.Neutral)
            return side;

        var cultureId = _allianceAdapter.GetKingdomCultureId(kingdomId);
        return string.IsNullOrEmpty(cultureId) ? FactionSide.Neutral : _alignmentService.GetCultureSide(cultureId);
    }

    // Reconcile the enrolled sets against the CURRENT world: drop any enrolled kingdom that
    // is (a) no longer live — a KingdomDestroyed missed because the feature was toggled OFF
    // (without this a wiped side keeps a stale id and its count never reaches 0, blocking the
    // elimination-victory check, Codex #327 MED); or (b) still live but whose side no longer
    // matches where it's enrolled — an alignment.json edit (e.g. Khand → Neutral) or a
    // culture/kingdom change. The enroll loop then re-adds it to the correct side if it moved
    // Free↔Evil. Without (b), a kingdom already enrolled before its alignment changed would be
    // stuck on the old side on an existing save.
    private bool PruneStaleKingdoms(MomentumWarState state, IReadOnlyList<string> liveKingdomIds)
    {
        var live = new HashSet<string>(liveKingdomIds);
        bool changed = false;

        changed |= PruneSide(state, state.Free, live, FactionSide.Free);
        changed |= PruneSide(state, state.Evil, live, FactionSide.Evil);
        return changed;
    }

    private bool PruneSide(MomentumWarState state, MomentumSideData side, HashSet<string> live, FactionSide enrolledSide)
    {
        bool changed = false;
        foreach (var id in side.KingdomIds.Where(id => !live.Contains(id) || ResolveSide(id) != enrolledSide).ToList())
        {
            if (RemoveKingdom(state, id))
                changed = true;
        }
        return changed;
    }
}
