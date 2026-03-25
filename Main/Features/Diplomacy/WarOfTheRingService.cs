using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features;

namespace TAOM.Features.Diplomacy;

public class WarOfTheRingService : IWarOfTheRingService
{
    private readonly IDiplomacyService _diplomacyService;
    private readonly IAllianceAdapter _allianceAdapter;
    private readonly IModLogger _logger;
    private readonly WarOfTheRingConfig _config;

    public WarPhase CurrentPhase { get; private set; } = WarPhase.Peace;
    public bool IsWarOfTheRingActive => CurrentPhase == WarPhase.FullWar;

    public WarOfTheRingService(
        IWarOfTheRingConfigProvider configProvider,
        IDiplomacyService diplomacyService,
        IAllianceAdapter allianceAdapter,
        IModLogger logger)
    {
        _diplomacyService = diplomacyService;
        _allianceAdapter = allianceAdapter;
        _logger = logger;

        _config = configProvider.LoadConfig();
    }

    public bool ShouldBlockPeace(string kingdomAId, string kingdomBId)
    {
        if (CurrentPhase != WarPhase.FullWar) return false;
        if (!_config.Phase2.BlockPeaceBetweenHostileTiers) return false;

        return _diplomacyService.GetRelationshipTier(kingdomAId, kingdomBId) == AllianceTier.Hostile;
    }

    public void CheckPhaseTransition(float elapsedDays)
    {
        if (!GetEffectiveEnabled()) return;

        var (phase1Day, phase2Day) = GetEffectivePhaseDays();

        if (CurrentPhase == WarPhase.Peace && elapsedDays >= phase1Day)
        {
            TransitionToPhase(WarPhase.IsengardWar);
        }

        if (CurrentPhase == WarPhase.IsengardWar && elapsedDays >= phase2Day)
        {
            TransitionToPhase(WarPhase.FullWar);
        }
    }

    private bool GetEffectiveEnabled()
    {
        var mcm = TaomSettings.Instance;
        if (mcm != null) return mcm.WarOfTheRingEnabled;
        return _config.Enabled;
    }

    private (int phase1Day, int phase2Day) GetEffectivePhaseDays()
    {
        var mcm = TaomSettings.Instance;

        if (mcm != null && mcm.TestMode)
            return (_config.TestMode.Phase1Day, _config.TestMode.Phase2Day);

        if (_config.TestMode.Enabled)
            return (_config.TestMode.Phase1Day, _config.TestMode.Phase2Day);

        if (mcm != null)
            return (mcm.Phase1TriggerDay, mcm.Phase2TriggerDay);

        return (_config.Phase1.TriggerDay, _config.Phase2.TriggerDay);
    }

    private void TransitionToPhase(WarPhase newPhase)
    {
        _logger.LogInfo($"War of the Ring: Transitioning to {newPhase}");
        CurrentPhase = newPhase;

        switch (newPhase)
        {
            case WarPhase.IsengardWar:
                DeclareConfiguredWars(_config.Phase1.Wars);
                break;
            case WarPhase.FullWar:
                DeclareConfiguredWars(_config.Phase2.Wars);
                if (_config.Phase2.AutoWarBetweenHostileTiers)
                {
                    DeclareHostileTierWars();
                }
                break;
        }
    }

    private void DeclareConfiguredWars(List<WarDeclaration> wars)
    {
        foreach (var war in wars)
        {
            if (!_allianceAdapter.AreAtWar(war.Attacker, war.Defender))
            {
                _logger.LogInfo($"War of the Ring: {war.Attacker} declares war on {war.Defender}");
                _allianceAdapter.DeclareWar(war.Attacker, war.Defender);
            }
        }
    }

    private void DeclareHostileTierWars()
    {
        var kingdoms = _allianceAdapter.GetAllKingdomIds();
        for (int i = 0; i < kingdoms.Count; i++)
        {
            for (int j = i + 1; j < kingdoms.Count; j++)
            {
                var a = kingdoms[i];
                var b = kingdoms[j];

                if (_diplomacyService.GetRelationshipTier(a, b) == AllianceTier.Hostile
                    && !_allianceAdapter.AreAtWar(a, b))
                {
                    _logger.LogInfo($"War of the Ring: {a} declares war on {b} (hostile tier)");
                    _allianceAdapter.DeclareWar(a, b);
                }
            }
        }
    }
}
