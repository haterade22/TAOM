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

        bool changed = false;
        foreach (var kingdomId in _allianceAdapter.GetAllKingdomIds())
        {
            if (state.DoesKingdomTakePart(kingdomId))
                continue;

            switch (_alignmentService.GetKingdomSide(kingdomId))
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
                // Neutral (Umbar/Khand etc.) never enrolls.
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
}
