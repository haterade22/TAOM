using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <inheritdoc />
public sealed class ServiceDiplomacyAdapter : IServiceDiplomacyAdapter
{
    private static readonly IReadOnlyList<string> None = new List<string>();

    private readonly IModLogger _logger;

    public ServiceDiplomacyAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> GetEnemiesOf(string heroId)
    {
        try
        {
            var faction = FindHero(heroId)?.MapFaction;
            return faction == null ? None : EnemiesOf(faction);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetEnemiesOf('{heroId}') failed: {ex.Message}");
            return None;
        }
    }

    public IReadOnlyList<string> GetPlayerEnemies()
    {
        try
        {
            var faction = Hero.MainHero?.MapFaction;
            return faction == null ? None : EnemiesOf(faction);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetPlayerEnemies failed: {ex.Message}");
            return None;
        }
    }

    public string GetPlayerFactionId()
    {
        try
        {
            // Hero.MapFaction is Clan.Kingdom ?? Clan (verified 1.4.8), so this is the id of
            // whichever faction DeclareWarOn/MakePeaceWith would actually act on behalf of right
            // now — a kingdom while the player's clan is a vassal, the clan itself when it is not.
            return Hero.MainHero?.MapFaction?.StringId;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetPlayerFactionId failed: {ex.Message}");
            return null;
        }
    }

    public bool DeclareWarOn(string factionId)
    {
        try
        {
            var us = Hero.MainHero?.MapFaction;
            var them = FindFaction(factionId);
            if (us == null || them == null || us == them || us.IsAtWarWith(them))
                return false;

            DeclareWarAction.ApplyByPlayerHostility(us, them);
            _logger?.LogInfo($"[Enlistment] mirrored the commander's war with '{factionId}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] DeclareWarOn('{factionId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool MakePeaceWith(string factionId)
    {
        try
        {
            var us = Hero.MainHero?.MapFaction;
            var them = FindFaction(factionId);
            if (us == null || them == null || us == them || !us.IsAtWarWith(them))
                return false;

            MakePeaceAction.Apply(us, them);
            _logger?.LogInfo($"[Enlistment] service war with '{factionId}' ended on discharge");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] MakePeaceWith('{factionId}') failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Every faction at war with this one — kingdoms AND minor factions, which is the half
    /// ServeAsSoldier drops. <c>Campaign.Current.Factions</c> is the union of both, so enumerating
    /// it rather than <c>Kingdom.All</c> is what makes mercenary and bandit-clan wars visible to the
    /// mirror, and therefore reversible on discharge.
    /// </summary>
    private static IReadOnlyList<string> EnemiesOf(IFaction faction)
    {
        var result = new List<string>();
        foreach (var other in Campaign.Current.Factions)
        {
            if (other == null || other == faction || other.IsEliminated)
                continue;
            if (faction.IsAtWarWith(other))
                result.Add(other.StringId);
        }
        return result;
    }

    private static IFaction FindFaction(string factionId)
    {
        if (string.IsNullOrEmpty(factionId))
            return null;

        foreach (var faction in Campaign.Current.Factions)
        {
            if (faction?.StringId == factionId)
                return faction;
        }
        return null;
    }

    private static Hero FindHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;

        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero?.StringId == heroId)
                return hero;
        }
        return null;
    }
}
