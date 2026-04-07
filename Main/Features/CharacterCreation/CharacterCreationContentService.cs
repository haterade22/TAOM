using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public class CharacterCreationContentService : ICharacterCreationContentService
{
    private const string ParentMenuId = "narrative_parent_menu";
    private const string ChildhoodMenuId = "narrative_childhood_menu";
    private const string EducationMenuId = "narrative_education_menu";
    private const string YouthMenuId = "narrative_youth_menu";
    private const string AdulthoodMenuId = "narrative_adulthood_menu";

    private readonly ICultureCreationDataProvider _dataProvider;
    private readonly INarrativeDataProvider _narrativeDataProvider;
    private readonly IRaceManager _raceManager;
    private readonly IHeroRosterAdapter _heroRosterAdapter;
    private readonly IEquipmentRosterProvider _equipmentRosterProvider;
    private readonly IModLogger _logger;

    // Vanilla cultures already registered by SandBox handler — skip these
    private static readonly HashSet<string> VanillaCultureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "empire", "vlandia", "sturgia", "aserai", "battania", "khuzait"
    };

    public CharacterCreationContentService(
        ICultureCreationDataProvider dataProvider,
        INarrativeDataProvider narrativeDataProvider,
        IRaceManager raceManager,
        IHeroRosterAdapter heroRosterAdapter,
        IEquipmentRosterProvider equipmentRosterProvider,
        IModLogger logger)
    {
        _dataProvider = dataProvider;
        _narrativeDataProvider = narrativeDataProvider;
        _raceManager = raceManager;
        _heroRosterAdapter = heroRosterAdapter;
        _equipmentRosterProvider = equipmentRosterProvider;
        _logger = logger;
    }

    public void RegisterCustomCultures(CharacterCreationManager manager)
    {
        var cultures = _dataProvider.LoadCultures();
        int registered = 0;

        foreach (var cultureData in cultures)
        {
            if (VanillaCultureIds.Contains(cultureData.CultureId))
                continue;

            var cultureObject = GetCultureObject(cultureData.CultureId);
            if (cultureObject == null)
            {
                _logger.LogWarning($"Culture '{cultureData.CultureId}' not found in MBObjectManager — skipping");
                continue;
            }

            try
            {
                manager.CharacterCreationContent.AddCharacterCreationCulture(
                    cultureObject,
                    cultureData.FocusToAdd,
                    cultureData.SkillLevelToAdd);
                registered++;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to register culture '{cultureData.CultureId}': {ex.Message}");
            }
        }

        _logger.LogInfo($"Registered {registered} custom cultures for character creation");
    }

    public void RegisterNarrativeMenus(CharacterCreationManager manager)
    {
        var builder = new NarrativeMenuBuilder(_logger, _equipmentRosterProvider);

        ReplaceMenuOptions(manager, builder, ParentMenuId,    "parents");
        ReplaceMenuOptions(manager, builder, ChildhoodMenuId, "childhood");
        ReplaceMenuOptions(manager, builder, EducationMenuId, "education");
        ReplaceMenuOptions(manager, builder, YouthMenuId,     "youth");
        ReplaceMenuOptions(manager, builder, AdulthoodMenuId, "adulthood");
    }

    private void ReplaceMenuOptions(
        CharacterCreationManager manager,
        NarrativeMenuBuilder builder,
        string menuId,
        string dataFileName)
    {
        var menu = manager.GetNarrativeMenuWithId(menuId);
        if (menu == null)
        {
            _logger.LogError($"Narrative menu '{menuId}' not found — SandBox handler may not have run");
            return;
        }

        RemoveVanillaOptions(menu, menuId);

        var options = _narrativeDataProvider.LoadMenuOptions(dataFileName);
        int added = builder.AddOptionsToMenu(menu, options);

        _logger.LogInfo($"[{menuId}] Added {added} TAOM narrative options");
    }

    private void RemoveVanillaOptions(NarrativeMenu menu, string menuId)
    {
        var vanillaOptions = menu.CharacterCreationMenuOptions
            .Where(o => !o.StringId.StartsWith("taom_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var option in vanillaOptions)
        {
            menu.RemoveNarrativeMenuOption(option);
        }

        _logger.LogInfo($"[{menuId}] Removed {vanillaOptions.Count} vanilla narrative options");
    }

    public void OnCharacterCreationFinalize(CharacterCreationManager manager)
    {
        var selectedCulture = manager.CharacterCreationContent.SelectedCulture;
        if (selectedCulture == null)
        {
            _logger.LogWarning("No culture selected at finalization");
            return;
        }

        var cultureData = _dataProvider.GetCultureData(selectedCulture.StringId);
        if (cultureData == null)
        {
            _logger.LogWarning($"No culture data found for '{selectedCulture.StringId}' — using defaults");
            return;
        }

        // BL's ApplyCulture() should have set Hero.Culture = SelectedCulture already.
        // Log and force-set as safety net for custom cultures.
        var heroCultureBefore = Hero.MainHero?.Culture?.StringId ?? "null";
        _logger.LogInfo($"CC Finalize: SelectedCulture='{selectedCulture.StringId}', Hero.Culture before='{heroCultureBefore}'");

        if (Hero.MainHero != null && Hero.MainHero.Culture?.StringId != selectedCulture.StringId)
        {
            Hero.MainHero.Culture = selectedCulture;
            _logger.LogInfo($"CC Finalize: Force-set Hero.Culture to '{selectedCulture.StringId}' (was '{heroCultureBefore}')");
        }

        TeleportToStartingSettlement(cultureData);
        SetPlayerRace(cultureData, Hero.MainHero?.StringId);
        AssignCareer(selectedCulture.StringId, Hero.MainHero?.StringId);
    }

    private void AssignCareer(string cultureId, string heroStringId)
    {
        if (string.IsNullOrEmpty(heroStringId) || string.IsNullOrEmpty(cultureId))
            return;

        try
        {
            var registry = IoC.Resolve<CareerSystem.ICareerRegistry>();
            var handler = IoC.Resolve<CareerSystem.ICareerCreationHandler>();
            if (registry == null || handler == null)
            {
                _logger.LogWarning("CareerSystem: Cannot assign career at CC — services not resolved");
                return;
            }

            foreach (var career in registry.GetAllCareers())
            {
                foreach (var eligibleCulture in career.EligibleCultureIds)
                {
                    if (string.Equals(eligibleCulture, cultureId, StringComparison.OrdinalIgnoreCase))
                    {
                        handler.OnCareerSelected(heroStringId, career.Id);
                        _logger.LogInfo($"CareerSystem: Assigned career '{career.Id}' during CC for culture '{cultureId}'");
                        return;
                    }
                }
            }

            _logger.LogInfo($"CareerSystem: No eligible career found for culture '{cultureId}' during CC");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerSystem: Failed to assign career during CC: {ex.Message}");
        }
    }

    internal void SetPlayerRace(CultureCreationData cultureData, string heroStringId)
    {
        if (string.IsNullOrEmpty(heroStringId))
        {
            _logger.LogWarning("Cannot set player race — hero string ID is null");
            return;
        }

        var raceName = cultureData.Races != null && cultureData.Races.Length > 0
            ? cultureData.Races[0]
            : "human";

        try
        {
            var raceId = _raceManager.GetRaceIdFromName(raceName);
            _heroRosterAdapter.SetHeroRace(heroStringId, raceId);
            _logger.LogInfo($"Set player race to '{raceName}' (id: {raceId})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to set player race to '{raceName}': {ex.Message}");
        }
    }

    private void TeleportToStartingSettlement(CultureCreationData cultureData)
    {
        if (string.IsNullOrEmpty(cultureData.StartingSettlement))
            return;

        try
        {
            var settlement = Settlement.Find(cultureData.StartingSettlement);
            if (settlement != null)
            {
                var position = settlement.GatePosition;
                MobileParty.MainParty.Position = position.IsNonZero() ? position : settlement.Position;
                _logger.LogInfo($"Teleported to starting settlement: {cultureData.StartingSettlement}");
            }
            else
            {
                _logger.LogWarning($"Starting settlement not found: {cultureData.StartingSettlement}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to teleport to starting settlement: {ex.Message}");
        }
    }

    private static CultureObject GetCultureObject(string cultureId)
    {
        return MBObjectManager.Instance?.GetObject<CultureObject>(cultureId);
    }
}
