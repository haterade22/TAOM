using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Features.FactionUI.FactionScreen;

/// <summary>
/// The faction screen's rules on characters and heroes (#704), from Kysaro's <c>FactionCatalog</c> and
/// <c>FactionScreenVM</c>, kept off the engine (<see cref="IFactionRosterAdapter"/> does the lookups):
/// the troop the 3D viewport shows, the living lords the browse lists offer, when the Leader tab is
/// shown, and which faction a click on the large minimap lands on.
/// </summary>
public sealed class FactionRoster
{
    private readonly IFactionRosterAdapter _roster;
    private readonly FactionScreenConfigProvider _config;

    public FactionRoster(IFactionRosterAdapter roster, FactionScreenConfigProvider config)
    {
        _roster = roster;
        _config = config;
    }

    public RosterEntry? Character(string? id) => string.IsNullOrEmpty(id) ? null : _roster.Character(id!);

    /// <summary>A named card's character, or the culture's troop when TAOM has no such character.</summary>
    public RosterEntry? CardCharacter(SpecialCharacter card, string? cultureId) =>
        Character(card.CharacterId) ?? CultureTroop(cultureId);

    /// <summary>The highest-tier foot soldier that does not shoot in the culture's troop trees (ties to
    /// the higher level, then the first id), else the culture's troop: Kysaro's default for the 3D
    /// viewport.</summary>
    public RosterEntry? EliteInfantry(string? cultureId)
    {
        if (string.IsNullOrEmpty(cultureId))
            return null;

        RosterEntry? best = null;
        foreach (var troop in _roster.CultureTroopTree(cultureId!))
        {
            if (troop.IsInfantry && !troop.IsRanged && (best == null || IsBetter(troop, best)))
                best = troop;
        }
        return best ?? CultureTroop(cultureId);
    }

    /// <summary>The kingdom's ruler; a dead one only when <paramref name="aliveOnly"/> is false.</summary>
    public RosterEntry? Ruler(string? kingdomId, bool aliveOnly)
    {
        if (string.IsNullOrEmpty(kingdomId))
            return null;
        var ruler = _roster.Ruler(kingdomId!);
        return ruler != null && (!aliveOnly || ruler.IsAlive) ? ruler : null;
    }

    /// <summary>The living leaders of the kingdom's other clans.</summary>
    public IReadOnlyList<RosterEntry> Lords(string? kingdomId) =>
        string.IsNullOrEmpty(kingdomId)
            ? Array.Empty<RosterEntry>()
            : _roster.OtherClanLeaders(kingdomId!).Where(l => l.IsAlive).ToList();

    public IReadOnlyList<RosterEntry> Wanderers(string? cultureId) =>
        string.IsNullOrEmpty(cultureId) ? Array.Empty<RosterEntry>() : _roster.Wanderers(cultureId!);

    /// <summary>False when the faction's ruler already has a named card: the Leader tab would only offer
    /// him a second time.</summary>
    public bool ShowLeaderCategory(FactionInfo info)
    {
        var ruler = Ruler(info.KingdomId, aliveOnly: false);
        return ruler == null
               || !FactionScreenArt.SpecialCharacters.TryGetValue(info.Key, out var cards)
               || cards.All(c => c.CharacterId != ruler.Id);
    }

    /// <summary>The character the 3D viewport shows: the one <c>faction_characters.json</c> names, else
    /// the culture's elite infantry.</summary>
    public RosterEntry? ViewportCharacter(FactionInfo info)
    {
        var configured = _config.Config.ViewportCharacters.TryGetValue(info.Key, out var id) ? Character(id) : null;
        return configured ?? EliteInfantry(info.CultureId);
    }

    /// <summary>A display-only race for the viewport from <c>faction_viewport.json</c>, when the engine
    /// knows it.</summary>
    public int? ViewportRace(FactionInfo info) =>
        Tweak(info)?.Race is { } race && race < _roster.RaceCount ? race : null;

    public float ViewportOffset(FactionInfo info) => Tweak(info)?.Offset ?? 0f;

    public bool HideViewportWeapons(FactionInfo info) => Tweak(info)?.HideWeapons == true;

    /// <summary>The faction whose minimap pin is nearest the normalized point (<paramref name="x"/>,
    /// <paramref name="y"/>), or null when there is none.</summary>
    public static FactionInfo? NearestFaction(IReadOnlyList<FactionInfo> factions, double x, double y)
    {
        FactionInfo? nearest = null;
        var nearestDistance = double.MaxValue;
        foreach (var info in factions)
        {
            var dx = info.MapX - x;
            var dy = info.MapY - y;
            var distance = dx * dx + dy * dy;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = info;
            }
        }
        return nearest;
    }

    private RosterEntry? CultureTroop(string? cultureId) =>
        string.IsNullOrEmpty(cultureId) ? null : _roster.CultureTroop(cultureId!);

    private ViewportTweak? Tweak(FactionInfo info) =>
        _config.Config.ViewportTweaks.TryGetValue(info.Key, out var tweak) ? tweak : null;

    private static bool IsBetter(RosterEntry candidate, RosterEntry best)
    {
        if (candidate.Tier != best.Tier)
            return candidate.Tier > best.Tier;
        if (candidate.Level != best.Level)
            return candidate.Level > best.Level;
        return string.CompareOrdinal(candidate.Id, best.Id) < 0;
    }
}
