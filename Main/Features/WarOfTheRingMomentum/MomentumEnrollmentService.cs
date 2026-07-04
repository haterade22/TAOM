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

    // Reconcile the enrolled sets against the live (non-eliminated) kingdom set. Handles
    // a KingdomDestroyed event that was missed because the feature was toggled OFF at the
    // time — without this, a wiped side keeps a stale id and its count never reaches 0,
    // permanently blocking the elimination-victory check (Codex #327 MED).
    private bool PruneStaleKingdoms(MomentumWarState state, IReadOnlyList<string> liveKingdomIds)
    {
        var live = new HashSet<string>(liveKingdomIds);
        bool changed = false;

        foreach (var staleId in state.Free.KingdomIds.Concat(state.Evil.KingdomIds).Where(id => !live.Contains(id)).ToList())
        {
            if (RemoveKingdom(state, staleId))
                changed = true;
        }

        return changed;
    }
}
