using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public class NarrativeMenuBuilder
{
    private readonly IModLogger _logger;
    private readonly IEquipmentRosterProvider _equipmentRosterProvider;

    private static readonly Dictionary<string, Func<SkillObject>> SkillMap =
        new Dictionary<string, Func<SkillObject>>(StringComparer.OrdinalIgnoreCase)
        {
            ["OneHanded"] = () => DefaultSkills.OneHanded,
            ["TwoHanded"] = () => DefaultSkills.TwoHanded,
            ["Polearm"] = () => DefaultSkills.Polearm,
            ["Bow"] = () => DefaultSkills.Bow,
            ["Crossbow"] = () => DefaultSkills.Crossbow,
            ["Throwing"] = () => DefaultSkills.Throwing,
            ["Riding"] = () => DefaultSkills.Riding,
            ["Athletics"] = () => DefaultSkills.Athletics,
            ["Crafting"] = () => DefaultSkills.Crafting,
            ["Scouting"] = () => DefaultSkills.Scouting,
            ["Tactics"] = () => DefaultSkills.Tactics,
            ["Roguery"] = () => DefaultSkills.Roguery,
            ["Charm"] = () => DefaultSkills.Charm,
            ["Leadership"] = () => DefaultSkills.Leadership,
            ["Trade"] = () => DefaultSkills.Trade,
            ["Steward"] = () => DefaultSkills.Steward,
            ["Medicine"] = () => DefaultSkills.Medicine,
            ["Engineering"] = () => DefaultSkills.Engineering,
        };

    private static readonly Dictionary<string, Func<CharacterAttribute>> AttributeMap =
        new Dictionary<string, Func<CharacterAttribute>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vigor"] = () => DefaultCharacterAttributes.Vigor,
            ["Control"] = () => DefaultCharacterAttributes.Control,
            ["Endurance"] = () => DefaultCharacterAttributes.Endurance,
            ["Cunning"] = () => DefaultCharacterAttributes.Cunning,
            ["Social"] = () => DefaultCharacterAttributes.Social,
            ["Intelligence"] = () => DefaultCharacterAttributes.Intelligence,
        };

    public NarrativeMenuBuilder(IModLogger logger, IEquipmentRosterProvider equipmentRosterProvider)
    {
        _logger = logger;
        _equipmentRosterProvider = equipmentRosterProvider;
    }

    internal static string BuildEquipmentRosterId(string cultureId, string titleType, bool isFemale)
    {
        return $"player_char_creation_{cultureId}_{titleType}_{(isFemale ? "f" : "m")}";
    }

    public NarrativeMenuOption BuildOption(NarrativeOptionDefinition definition)
    {
        var skills = ResolveSkills(definition.Skills);
        var attribute = ResolveAttribute(definition.Attribute);
        var cultureId = definition.CultureId;
        var occupationType = definition.OccupationType;
        var titleType = definition.TitleType;
        var focusToAdd = definition.FocusToAdd;
        var skillLevelToAdd = definition.SkillLevelToAdd;
        var attributeLevelToAdd = definition.AttributeLevelToAdd;

        return new NarrativeMenuOption(
            definition.StringId,
            new TextObject($"{{=taom_cc_{definition.StringId}_text}}{definition.Text}"),
            new TextObject($"{{=taom_cc_{definition.StringId}_desc}}{definition.Description}"),
            getNarrativeMenuOptionArgs: (NarrativeMenuOptionArgs args) =>
            {
                if (skills.Length > 0)
                {
                    args.SetAffectedSkills(skills);
                    args.SetFocusToSkills(focusToAdd);
                    args.SetLevelToSkills(skillLevelToAdd);
                }

                if (attribute != null)
                {
                    args.SetLevelToAttribute(attribute, attributeLevelToAdd);
                }
            },
            onCondition: string.IsNullOrEmpty(cultureId)
                ? (NarrativeMenuOptionOnConditionDelegate)null
                : (CharacterCreationManager manager) =>
                {
                    var selectedCulture = manager.CharacterCreationContent?.SelectedCulture;
                    return selectedCulture != null &&
                           string.Equals(selectedCulture.StringId, cultureId, StringComparison.OrdinalIgnoreCase);
                },
            onSelect: (string.IsNullOrEmpty(occupationType) && string.IsNullOrEmpty(titleType))
                ? (NarrativeMenuOptionOnSelectDelegate)null
                : (CharacterCreationManager manager) =>
                {
                    if (!string.IsNullOrEmpty(occupationType))
                        manager.CharacterCreationContent.SetParentOccupation(occupationType);
                    if (!string.IsNullOrEmpty(titleType))
                    {
                        manager.CharacterCreationContent.SelectedTitleType = titleType;
                        UpdateYouthEquipment(manager, titleType);
                    }
                },
            onConsequence: null
        );
    }

    public int AddOptionsToMenu(NarrativeMenu menu, IReadOnlyList<NarrativeOptionDefinition> definitions)
    {
        int added = 0;
        foreach (var definition in definitions)
        {
            try
            {
                var option = BuildOption(definition);
                menu.AddNarrativeMenuOption(option);
                added++;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to build narrative option '{definition.StringId}': {ex.Message}");
            }
        }
        return added;
    }

    private SkillObject[] ResolveSkills(string[] skillNames)
    {
        if (skillNames == null || skillNames.Length == 0)
            return Array.Empty<SkillObject>();

        var resolved = new List<SkillObject>();
        foreach (var name in skillNames)
        {
            if (SkillMap.TryGetValue(name, out var factory))
            {
                var skill = factory();
                if (skill != null)
                    resolved.Add(skill);
                else
                    _logger.LogWarning($"Skill '{name}' resolved to null — game may not be initialized");
            }
            else
            {
                _logger.LogWarning($"Unknown skill name: '{name}'");
            }
        }
        return resolved.ToArray();
    }

    private CharacterAttribute ResolveAttribute(string attributeName)
    {
        if (string.IsNullOrEmpty(attributeName))
            return null;

        if (AttributeMap.TryGetValue(attributeName, out var factory))
        {
            var attr = factory();
            if (attr == null)
                _logger.LogWarning($"Attribute '{attributeName}' resolved to null — game may not be initialized");
            return attr;
        }

        _logger.LogWarning($"Unknown attribute name: '{attributeName}'");
        return null;
    }

    private void UpdateYouthEquipment(CharacterCreationManager manager, string titleType)
    {
        var cultureId = manager.CharacterCreationContent.SelectedCulture?.StringId;
        if (string.IsNullOrEmpty(cultureId))
            return;

        var isFemale = Hero.MainHero?.IsFemale ?? false;
        var rosterId = BuildEquipmentRosterId(cultureId, titleType, isFemale);
        var roster = _equipmentRosterProvider.GetRoster(rosterId);
        if (roster == null)
        {
            _logger.LogWarning($"Equipment roster '{rosterId}' not found");
            return;
        }

        foreach (var character in manager.CurrentMenu.Characters)
        {
            if (character.StringId == "player_youth_character")
            {
                character.SetEquipment(roster);
                break;
            }
        }
    }
}
