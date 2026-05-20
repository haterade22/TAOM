using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public class CareerMenuService : ICareerMenuService
{
    private const string CareerMenuId = "narrative_career_menu";
    private const string AgeSelectionMenuId = "narrative_age_selection_menu";

    private readonly ICareerRegistry _registry;
    private readonly ICareerMenuDataProvider _dataProvider;
    private readonly ICareerArchetypeService _archetypeService;
    private readonly IEquipmentRosterProvider _equipmentRosterProvider;
    private readonly IModLogger _logger;

    public string SelectedCareerStringId { get; private set; }

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

    public CareerMenuService(
        ICareerRegistry registry,
        ICareerMenuDataProvider dataProvider,
        ICareerArchetypeService archetypeService,
        IEquipmentRosterProvider equipmentRosterProvider,
        IModLogger logger)
    {
        _registry = registry;
        _dataProvider = dataProvider;
        _archetypeService = archetypeService;
        _equipmentRosterProvider = equipmentRosterProvider;
        _logger = logger;
    }

    public void RegisterCareerMenu(CharacterCreationManager manager)
    {
        SelectedCareerStringId = null;

        var options = BuildCareerMenuOptions();
        if (options.Count == 0)
        {
            _logger.LogWarning("No career menu options built — skipping career menu registration");
            return;
        }

        var characters = new List<NarrativeMenuCharacter>();
        var playerBody = CharacterObject.PlayerCharacter?.GetBodyProperties(null) ?? BodyProperties.Default;
        var playerRace = CharacterObject.PlayerCharacter?.Race ?? 0;
        var isFemale = Hero.MainHero?.IsFemale ?? false;
        characters.Add(new NarrativeMenuCharacter("player_career_character", playerBody, playerRace, isFemale));

        // Chain after age selection: adulthood → age_selection (vanilla) → career → finalize
        var careerMenu = new NarrativeMenu(
            CareerMenuId,
            AgeSelectionMenuId,
            "",
            new TextObject("{=taom_cc_career_title}Career"),
            new TextObject("{=taom_cc_career_desc}Your experiences have set you on a path. Choose the career that will define your legend."),
            characters,
            GetCareerMenuCharacterArgs);

        foreach (var option in options)
        {
            careerMenu.AddNarrativeMenuOption(option);
        }

        manager.AddNewMenu(careerMenu);
        _logger.LogInfo($"Registered career menu with {options.Count} options");
    }

    public List<NarrativeMenuOption> BuildCareerMenuOptions()
    {
        var careers = _registry.GetAllCareers();
        var options = new List<NarrativeMenuOption>();

        foreach (var career in careers)
        {
            var ccData = _dataProvider.GetOptionForCareer(career.Id);
            if (ccData == null)
            {
                _logger.LogWarning($"No CC data for career '{career.Id}' — skipping");
                continue;
            }

            var option = BuildOptionForCareer(career, ccData);
            options.Add(option);
        }

        // Fallback option for cultures with no eligible careers (e.g., shaghana, abanissa).
        // Without this, an empty menu causes KeyNotFoundException in vanilla
        // TrySwitchToNextMenu when SelectedOptions has no entry for the career menu.
        options.Add(BuildFallbackOption());

        return options;
    }

    public void OnCareerOptionSelected(string careerStringId, CharacterCreationManager manager)
    {
        SelectedCareerStringId = careerStringId;
        UpdateCareerEquipmentPreview(careerStringId, manager);
    }

    // Live equipment-preview update on the CC career menu. Mirrors the youth-stage pattern
    // in NarrativeMenuBuilder.UpdateYouthEquipment — finds the player character in the
    // current menu by StringId and calls SetEquipment with the archetype roster. The engine
    // re-renders the 3D agent automatically. Same fallback policy as the runtime grant
    // service (CareerStartingEquipmentService): missing archetype or roster → log + leave
    // whatever's currently rendered (youth equipment) in place.
    private void UpdateCareerEquipmentPreview(string careerId, CharacterCreationManager manager)
    {
        if (manager?.CurrentMenu == null)
            return;

        var cultureId = manager.CharacterCreationContent?.SelectedCulture?.StringId;
        if (string.IsNullOrEmpty(cultureId))
        {
            _logger.LogWarning("CareerMenuService: SelectedCulture is null — skipping career equipment preview");
            return;
        }

        if (!_archetypeService.TryGetArchetype(careerId, out var archetype))
        {
            _logger.LogInfo($"CareerMenuService: no archetype mapped for career '{careerId}' — leaving preview unchanged");
            return;
        }

        var isFemale = Hero.MainHero?.IsFemale ?? false;
        var rosterId = CareerEquipmentRosterIds.Build(cultureId, archetype, isFemale);
        var roster = _equipmentRosterProvider.GetRoster(rosterId);
        if (roster == null)
        {
            _logger.LogInfo($"CareerMenuService: roster '{rosterId}' not found — leaving preview unchanged");
            return;
        }

        foreach (var character in manager.CurrentMenu.Characters)
        {
            if (character.StringId == "player_career_character")
            {
                character.SetEquipment(roster);
                _logger.LogInfo($"CareerMenuService: applied preview roster '{rosterId}'");
                return;
            }
        }

        _logger.LogWarning("CareerMenuService: 'player_career_character' not found in current menu — preview not updated");
    }

    public IReadOnlyList<string> GetEligibleCultureIds(CareerDefinition career)
    {
        return career.EligibleCultureIds;
    }

    private NarrativeMenuOption BuildFallbackOption()
    {
        // Collect all culture IDs that have at least one career option.
        // The fallback is visible only for cultures NOT in this set.
        var coveredCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var career in _registry.GetAllCareers())
        {
            foreach (var cultureId in career.EligibleCultureIds)
                coveredCultures.Add(cultureId);
        }

        return new NarrativeMenuOption(
            "taom_career_none",
            new TextObject("{=taom_cc_career_none}No specialization"),
            new TextObject("{=taom_cc_career_none_desc}You have not yet committed to a particular path. Your career will be determined by your actions in the world."),
            (NarrativeMenuOptionArgs args) => { },
            (CharacterCreationManager manager) =>
            {
                var selectedCulture = manager.CharacterCreationContent?.SelectedCulture;
                if (selectedCulture == null)
                    return true;
                // Show only when the player's culture has no career options
                return !coveredCultures.Contains(selectedCulture.StringId);
            },
            (CharacterCreationManager manager) =>
            {
                SelectedCareerStringId = null;
                _logger.LogInfo("Player selected no career specialization (fallback)");
            },
            null);
    }

    private NarrativeMenuOption BuildOptionForCareer(CareerDefinition career, CareerMenuOptionDefinition ccData)
    {
        var careerId = career.Id;
        var eligibleCultures = career.EligibleCultureIds;
        var skillNames = ccData.Skills;
        var attributeName = ccData.Attribute;
        var focusToAdd = ccData.FocusToAdd;
        var skillLevelToAdd = ccData.SkillLevelToAdd;
        var attributeLevelToAdd = ccData.AttributeLevelToAdd;

        return new NarrativeMenuOption(
            $"taom_career_{careerId}",
            new TextObject(career.DisplayName),
            new TextObject(career.Description),
            (NarrativeMenuOptionArgs args) =>
            {
                // Resolve at runtime when game is initialized
                var skills = ResolveSkills(skillNames);
                var attribute = ResolveAttribute(attributeName);
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
            (CharacterCreationManager manager) =>
            {
                var selectedCulture = manager.CharacterCreationContent?.SelectedCulture;
                if (selectedCulture == null)
                    return false;

                foreach (var cultureId in eligibleCultures)
                {
                    if (string.Equals(selectedCulture.StringId, cultureId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            },
            (CharacterCreationManager manager) =>
            {
                OnCareerOptionSelected(careerId, manager);
                _logger.LogInfo($"Player selected career: {careerId}");
            },
            null);
    }

    private static List<NarrativeMenuCharacterArgs> GetCareerMenuCharacterArgs(
        CultureObject culture, string occupationType, CharacterCreationManager manager)
    {
        var cultureId = culture?.StringId ?? "gondor";
        var isFemale = Hero.MainHero?.IsFemale ?? false;
        var titleType = manager.CharacterCreationContent?.SelectedTitleType ?? "guard";
        var equipmentId = PlayerEquipmentRosterIds.Build(cultureId, titleType, isFemale);

        return new List<NarrativeMenuCharacterArgs>
        {
            new NarrativeMenuCharacterArgs(
                "player_career_character",
                25,
                equipmentId,
                "act_character_creation_male_default_standing",
                "spawnpoint_player_1",
                "", "", null, true, isFemale)
        };
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
            return factory();

        _logger.LogWarning($"Unknown attribute name: '{attributeName}'");
        return null;
    }
}
