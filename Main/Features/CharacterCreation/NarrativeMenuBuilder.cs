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

    public NarrativeMenuBuilder(IModLogger logger, IEquipmentRosterProvider equipmentRosterProvider)
    {
        _logger = logger;
        _equipmentRosterProvider = equipmentRosterProvider;
    }

    internal static string BuildEquipmentRosterId(string cultureId, string titleType, bool isFemale)
        => PlayerEquipmentRosterIds.Build(cultureId, titleType, isFemale);

    public NarrativeMenuOption BuildOption(NarrativeOptionDefinition definition)
    {
        var skills = CharacterCreationBonusResolver.ResolveSkills(definition.Skills, _logger, logNullResolution: true);
        var attribute = CharacterCreationBonusResolver.ResolveAttribute(definition.Attribute, _logger, logNullResolution: true);
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
