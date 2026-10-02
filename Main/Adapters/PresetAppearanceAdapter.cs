using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IPresetAppearanceAdapter"/> over the engine's hero and character objects, ported from
/// Kysaro's TAOM_FactionUI <c>FactionCatalog</c> (#704). A pick's gear is drawn once when it is
/// resolved (a wanderer template's <c>RandomBattleEquipment</c> draws from several sets, so drawing it
/// per copy showed one kit and granted another); a gear set the pick does not have is taken from its
/// culture's elite basic troop, as Kysaro's module did for a hero without gear.
/// </summary>
public sealed class PresetAppearanceAdapter : IPresetAppearanceAdapter
{
    private readonly IModLogger _logger;

    public PresetAppearanceAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public object? Resolve(object picked, bool isHero)
    {
        if (isHero && picked is Hero hero && hero.CharacterObject != null)
        {
            return new ResolvedPick(
                hero.StringId, hero.BodyProperties, hero.CharacterObject.Race, hero.IsFemale,
                OrCultureTroops(hero.BattleEquipment, battle: true, hero.Culture),
                OrCultureTroops(hero.CivilianEquipment, battle: false, hero.Culture),
                hero.Name, hero.FirstName, skill => hero.GetSkillValue(skill));
        }

        if (picked is CharacterObject template)
        {
            var battle = OrCultureTroops(template.RandomBattleEquipment ?? template.FirstBattleEquipment, battle: true, template.Culture);
            var civilian = OrCultureTroops(template.RandomCivilianEquipment ?? template.FirstCivilianEquipment, battle: false, template.Culture);
            return new ResolvedPick(
                template.StringId, template.GetBodyProperties(battle), template.Race, template.IsFemale,
                battle, civilian, name: null, firstName: null, skill => template.GetSkillValue(skill));
        }

        return null;
    }

    public object? CaptureLook()
    {
        var player = CharacterObject.PlayerCharacter;
        var hero = player?.HeroObject;
        if (player == null || hero == null)
            return null;
        return new Look(hero.BodyProperties, player.Race, player.IsFemale, hero.BattleEquipment?.Clone(), hero.CivilianEquipment?.Clone());
    }

    public void RestoreLook(object look)
    {
        if (look is Look saved)
            SetLook(saved.Body, saved.Race, saved.IsFemale, saved.Battle, saved.Civilian);
    }

    public void ApplyLook(object resolved)
    {
        if (resolved is ResolvedPick pick)
            SetLook(pick.Body, pick.Race, pick.IsFemale, pick.Battle, pick.Civilian);
    }

    public void ApplyIdentity(object resolved)
    {
        var player = CharacterObject.PlayerCharacter?.HeroObject;
        if (resolved is not ResolvedPick pick || player == null)
            return;

        if (pick.Name != null)
            player.SetName(pick.Name, pick.FirstName ?? pick.Name);
        CopySkills(pick, player);
        _logger.LogInfo($"[FactionUI] hero preset applied from {pick.Id}");
    }

    public object? DisplayEquipment(object resolved) =>
        resolved is ResolvedPick { Battle: { } battle } ? battle.Clone() : null;

    private static void SetLook(BodyProperties body, int race, bool isFemale, Equipment? battle, Equipment? civilian)
    {
        var player = CharacterObject.PlayerCharacter;
        var hero = player?.HeroObject;
        if (player == null || hero == null)
            return;

        player.UpdatePlayerCharacterBodyProperties(body, race, isFemale);
        if (battle != null)
            hero.BattleEquipment.FillFrom(battle, useSourceEquipmentType: false);
        if (civilian != null)
            hero.CivilianEquipment.FillFrom(civilian, useSourceEquipmentType: false);
    }

    private static Equipment? OrCultureTroops(Equipment? equipment, bool battle, CultureObject? culture)
    {
        if (equipment != null && !equipment.IsEmpty())
            return equipment.Clone();
        var troop = culture?.EliteBasicTroop ?? culture?.BasicTroop;
        var fallback = battle
            ? troop?.RandomBattleEquipment ?? troop?.FirstBattleEquipment
            : troop?.RandomCivilianEquipment ?? troop?.FirstCivilianEquipment;
        return fallback == null || fallback.IsEmpty() ? null : fallback.Clone();
    }

    private void CopySkills(ResolvedPick pick, Hero player)
    {
        var developer = player.HeroDeveloper;
        if (developer == null)
            return;
        foreach (var skill in Skills.All)
        {
            try
            {
                developer.SetInitialSkillLevel(skill, pick.SkillOf(skill));
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[FactionUI] preset skill {skill.StringId} not copied: {ex.Message}");
            }
        }
    }

    /// <summary>A pick resolved once: its look, its gear as drawn, and where its name and skills come from.</summary>
    private sealed class ResolvedPick
    {
        public ResolvedPick(string id, BodyProperties body, int race, bool isFemale, Equipment? battle, Equipment? civilian,
            TextObject? name, TextObject? firstName, Func<SkillObject, int> skillOf)
        {
            Id = id;
            Body = body;
            Race = race;
            IsFemale = isFemale;
            Battle = battle;
            Civilian = civilian;
            Name = name;
            FirstName = firstName;
            SkillOf = skillOf;
        }

        public string Id { get; }

        public BodyProperties Body { get; }

        public int Race { get; }

        public bool IsFemale { get; }

        public Equipment? Battle { get; }

        public Equipment? Civilian { get; }

        /// <summary>A hero's name; null for a template, which leaves the player's own.</summary>
        public TextObject? Name { get; }

        public TextObject? FirstName { get; }

        public Func<SkillObject, int> SkillOf { get; }
    }

    /// <summary>The player's look before a pick was copied, to put back when the pick is dropped.</summary>
    private sealed class Look
    {
        public Look(BodyProperties body, int race, bool isFemale, Equipment? battle, Equipment? civilian)
        {
            Body = body;
            Race = race;
            IsFemale = isFemale;
            Battle = battle;
            Civilian = civilian;
        }

        public BodyProperties Body { get; }

        public int Race { get; }

        public bool IsFemale { get; }

        public Equipment? Battle { get; }

        public Equipment? Civilian { get; }
    }
}
