using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IFactionRosterAdapter"/> over <see cref="MBObjectManager"/>, the culture's troop trees and
/// notable templates, and the campaign's kingdoms (#704, from Kysaro's <c>FactionCatalog</c>). Names
/// are read in the player's language at each call, so a language change shows at the next refresh.
/// </summary>
public sealed class FactionRosterAdapter : IFactionRosterAdapter
{
    public RosterEntry? Character(string id) =>
        string.IsNullOrEmpty(id) ? null : Entry(MBObjectManager.Instance?.GetObject<CharacterObject>(id));

    public RosterEntry? CultureTroop(string cultureId)
    {
        var culture = Culture(cultureId);
        return Entry(culture?.EliteBasicTroop ?? culture?.BasicTroop);
    }

    public IReadOnlyList<RosterEntry> CultureTroopTree(string cultureId)
    {
        var culture = Culture(cultureId);
        var result = new List<RosterEntry>();
        if (culture == null)
            return result;

        var seen = new HashSet<CharacterObject>();
        var stack = new Stack<CharacterObject>();
        foreach (var root in new[] { culture.BasicTroop, culture.EliteBasicTroop })
        {
            if (root != null && seen.Add(root))
                stack.Push(root);
        }
        while (stack.Count > 0)
        {
            var troop = stack.Pop();
            if (Entry(troop) is { } entry)
                result.Add(entry);
            foreach (var next in troop.UpgradeTargets ?? Array.Empty<CharacterObject>())
            {
                if (next != null && seen.Add(next))
                    stack.Push(next);
            }
        }
        return result;
    }

    public RosterEntry? Ruler(string kingdomId) => Entry(Kingdom(kingdomId)?.RulingClan?.Leader);

    public IReadOnlyList<RosterEntry> OtherClanLeaders(string kingdomId)
    {
        var kingdom = Kingdom(kingdomId);
        if (kingdom == null)
            return Array.Empty<RosterEntry>();
        return kingdom.Clans
            .Where(c => c != kingdom.RulingClan && c.Leader != null)
            .Select(c => Entry(c.Leader))
            .Where(e => e != null)
            .Select(e => e!)
            .ToList();
    }

    // The culture's wanderers are the notable templates of that occupation: the pool the campaign
    // spawns wanderers from (CultureObject.NotableTemplates, v1.5.3 :208).
    public IReadOnlyList<RosterEntry> Wanderers(string cultureId)
    {
        var culture = Culture(cultureId);
        if (culture?.NotableTemplates == null)
            return Array.Empty<RosterEntry>();
        return culture.NotableTemplates
            .Where(t => t != null && t.Occupation == Occupation.Wanderer)
            .Select(t => new RosterEntry(t, t.StringId, WandererLabel(t), isHero: false))
            .ToList();
    }

    public int RaceCount => FaceGen.GetRaceCount();

    private static CultureObject? Culture(string cultureId) =>
        string.IsNullOrEmpty(cultureId) ? null : MBObjectManager.Instance?.GetObject<CultureObject>(cultureId);

    private static Kingdom? Kingdom(string kingdomId) =>
        string.IsNullOrEmpty(kingdomId) ? null : TaleWorlds.CampaignSystem.Kingdom.All.FirstOrDefault(k => k.StringId == kingdomId);

    private static RosterEntry? Entry(CharacterObject? character) =>
        character == null
            ? null
            : new RosterEntry(character, character.StringId, character.Name?.ToString() ?? character.StringId, isHero: false,
                tier: character.Tier, level: character.Level, isInfantry: character.IsInfantry, isRanged: character.IsRanged);

    private static RosterEntry? Entry(Hero? hero) =>
        hero == null ? null : new RosterEntry(hero, hero.StringId, hero.Name?.ToString() ?? hero.StringId, isHero: true, isAlive: hero.IsAlive);

    // A wanderer template's name carries the {FIRSTNAME} the campaign fills at spawn; the list shows the
    // rest of it ("the Ranger"), or a plain "Wanderer".
    private static string WandererLabel(CharacterObject template)
    {
        var shown = template.Name?.CopyTextObject().SetTextVariable("FIRSTNAME", "").ToString().Trim();
        return string.IsNullOrEmpty(shown) ? new TextObject("{=taom_fui_wanderer}Wanderer").ToString() : shown!;
    }
}
