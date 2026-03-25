using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public class WarOfTheRingService : IWarOfTheRingService
{
    private readonly IDiplomacyService _diplomacyService;
    private readonly IAllianceAdapter _allianceAdapter;
    private readonly IModLogger _logger;
    private readonly WarOfTheRingConfig _config;
    private readonly int _phase1Day;
    private readonly int _phase2Day;

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

        if (_config.TestMode.Enabled)
        {
            _phase1Day = _config.TestMode.Phase1Day;
            _phase2Day = _config.TestMode.Phase2Day;
        }
        else
        {
            _phase1Day = _config.Phase1.TriggerDay;
            _phase2Day = _config.Phase2.TriggerDay;
        }
    }

    public bool ShouldBlockPeace(string kingdomAId, string kingdomBId)
    {
        if (CurrentPhase != WarPhase.FullWar) return false;
        if (!_config.Phase2.BlockPeaceBetweenHostileTiers) return false;

        return _diplomacyService.GetRelationshipTier(kingdomAId, kingdomBId) == AllianceTier.Hostile;
    }

    public void CheckPhaseTransition(float elapsedDays)
    {
        if (!_config.Enabled) return;

        if (CurrentPhase == WarPhase.Peace && elapsedDays >= _phase1Day)
        {
            TransitionToPhase(WarPhase.IsengardWar);
        }

        if (CurrentPhase == WarPhase.IsengardWar && elapsedDays >= _phase2Day)
        {
            TransitionToPhase(WarPhase.FullWar);
        }
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
