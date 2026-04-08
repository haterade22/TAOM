using System;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;

namespace TAOM.Features.NamedCompanions;

public class NamedCompanionService : INamedCompanionService
{
    private readonly INamedCompanionConfigProvider _configProvider;
    private readonly INamedCompanionAdapter _companionAdapter;
    private readonly IHeroRosterAdapter _heroRosterAdapter;
    private readonly IRaceManager _raceManager;
    private readonly IModLogger _logger;
    private bool _spawned;

    public NamedCompanionService(
        INamedCompanionConfigProvider configProvider,
        INamedCompanionAdapter companionAdapter,
        IHeroRosterAdapter heroRosterAdapter,
        IRaceManager raceManager,
        IModLogger logger)
    {
        _configProvider = configProvider;
        _companionAdapter = companionAdapter;
        _heroRosterAdapter = heroRosterAdapter;
        _raceManager = raceManager;
        _logger = logger;
    }

    public void SpawnCompanions()
    {
        if (_spawned) return;
        _spawned = true;

        var companions = _configProvider.GetCompanions();
        var placed = 0;

        foreach (var companion in companions)
        {
            if (!companion.Enabled) continue;

            if (!_companionAdapter.HeroExists(companion.CharacterId))
            {
                _logger.LogWarning(
                    $"[NamedCompanions] Hero '{companion.CharacterId}' not found — check named_companions.xml");
                continue;
            }

            try
            {
                _companionAdapter.PlaceInSettlement(companion.CharacterId, companion.SpawnSettlement);
                _companionAdapter.MarkAsMet(companion.CharacterId);

                var raceId = _raceManager.GetRaceIdFromName(companion.Race);
                _heroRosterAdapter.SetHeroRace(companion.CharacterId, raceId);

                placed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"[NamedCompanions] Failed to spawn '{companion.CharacterId}': {ex.Message}");
            }
        }

        _logger.LogInfo($"[NamedCompanions] Placed {placed} named companions");
    }

    public void EnsureCompanionsPlaced()
    {
        var companions = _configProvider.GetCompanions();

        foreach (var companion in companions)
        {
            if (!companion.Enabled) continue;
            if (!_companionAdapter.HeroExists(companion.CharacterId)) continue;
            if (!_companionAdapter.IsHeroAlive(companion.CharacterId)) continue;
            if (_companionAdapter.IsRecruitedOrInParty(companion.CharacterId)) continue;
            if (_companionAdapter.IsPlacedInSettlement(companion.CharacterId)) continue;

            try
            {
                _companionAdapter.PlaceInSettlement(companion.CharacterId, companion.SpawnSettlement);
                _companionAdapter.MarkAsMet(companion.CharacterId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"[NamedCompanions] Failed to re-place '{companion.CharacterId}': {ex.Message}");
            }
        }
    }
}
