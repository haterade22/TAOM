using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// LOTRAOM parity source: WarOfTheRingData.EndWarIfConditionsMet. BOTH victories are
/// gated on the player requirement (LOTRAOM gates the Evil branch too — the plan's
/// "Evil ungated" note was wrong against source). LOTRAOM's SetKingdomLinksToNeutral
/// (same-side stance reset) is dropped: momentum never set alliance stances on TAOM,
/// so there is nothing to undo; cross-side pairs get MakePeace instead.
/// </summary>
public class MomentumVictoryService : IMomentumVictoryService
{
    private readonly IMomentumSettingsProvider _settings;
    private readonly IPlayerMomentumService _playerService;
    private readonly IWarOfTheRingService _wotrService;
    private readonly IAllianceAdapter _allianceAdapter;
    private readonly IModLogger _logger;

    public MomentumVictoryService(
        IMomentumSettingsProvider settings,
        IPlayerMomentumService playerService,
        IWarOfTheRingService wotrService,
        IAllianceAdapter allianceAdapter,
        IModLogger logger)
    {
        _settings = settings;
        _playerService = playerService;
        _wotrService = wotrService;
        _allianceAdapter = allianceAdapter;
        _logger = logger;
    }

    public WarOutcome CheckAndApplyVictory(MomentumWarState state)
    {
        // Endless-war mode (default): the war is tracked but never resolves via momentum.
        if (!_settings.VictoryEnabled)
            return WarOutcome.None;

        if (state == null || state.HasWarEnded || !state.HasWarStarted)
            return WarOutcome.None;

        bool playerRequirementMet = !_settings.RequireParticipationForVictory
                                    || _playerService.HasPlayerMetVictoryRequirement();
        if (!playerRequirementMet)
            return WarOutcome.None;

        float internalMomentum = state.InternalMomentum;
        int threshold = _settings.VictoryThreshold;

        var outcome = WarOutcome.None;
        if (internalMomentum >= threshold || state.Evil.KingdomIds.Count == 0)
            outcome = WarOutcome.FreeVictory;
        else if (internalMomentum <= -threshold || state.Free.KingdomIds.Count == 0)
            outcome = WarOutcome.EvilVictory;

        if (outcome == WarOutcome.None)
            return WarOutcome.None;

        _logger.LogInfo($"[Momentum] War of the Ring decided: {outcome} (momentum {internalMomentum:F0}, threshold {threshold})");

        // Ordering is load-bearing:
        // 1. Freeze the meter — all event processing early-returns on HasWarEnded.
        state.MarkWarEnded(outcome);
        // 2. Phase → WarEnded lifts the three peace-block layers.
        _wotrService.EndWar(outcome);
        // 3. Only now can MakePeaceAction succeed.
        PeaceOutCrossSidePairs(state);

        return outcome;
    }

    private void PeaceOutCrossSidePairs(MomentumWarState state)
    {
        foreach (var freeId in state.Free.KingdomIds)
        {
            foreach (var evilId in state.Evil.KingdomIds)
            {
                if (_allianceAdapter.AreAtWar(freeId, evilId))
                {
                    _logger.LogInfo($"[Momentum] Peace: {freeId} ↔ {evilId}");
                    _allianceAdapter.MakePeace(freeId, evilId);
                }
            }
        }
    }
}
