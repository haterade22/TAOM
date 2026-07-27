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
using TAOM.Features.StartupResources;

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
    private readonly ICareerMenuService _careerMenuService;
    private readonly IPlayerStartupGoldService _playerStartupGoldService;
    private readonly IPlayerEquipmentService _playerEquipmentService;
    private readonly ICareerStartingEquipmentService _careerStartingEquipmentService;
    // Phase 9b #125 — constructor-injected per feedback_no_service_locator_in_services.md
    // (banned IoC.Resolve in service body). Both used by AssignCareer.
    private readonly CareerSystem.ICareerCreationHandler _careerHandler;
    private readonly CareerSystem.ICareerRegistry _careerRegistry;
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
        ICareerMenuService careerMenuService,
        IPlayerStartupGoldService playerStartupGoldService,
        IPlayerEquipmentService playerEquipmentService,
        ICareerStartingEquipmentService careerStartingEquipmentService,
        CareerSystem.ICareerCreationHandler careerHandler,
        CareerSystem.ICareerRegistry careerRegistry,
        IModLogger logger)
    {
        _dataProvider = dataProvider;
        _narrativeDataProvider = narrativeDataProvider;
        _raceManager = raceManager;
        _heroRosterAdapter = heroRosterAdapter;
        _equipmentRosterProvider = equipmentRosterProvider;
        _careerMenuService = careerMenuService;
        _playerStartupGoldService = playerStartupGoldService;
        _playerEquipmentService = playerEquipmentService;
        _careerStartingEquipmentService = careerStartingEquipmentService;
        _careerHandler = careerHandler;
        _careerRegistry = careerRegistry;
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

    public void RegisterCareerMenu(CharacterCreationManager manager)
    {
        _careerMenuService.RegisterCareerMenu(manager);
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

        int removed = RemoveVanillaOptions(menu, menuId);

        var options = _narrativeDataProvider.LoadMenuOptions(dataFileName);
        int added = builder.AddOptionsToMenu(menu, options);

        // One line per menu instead of three (removed / loaded / added were separate INFO lines
        // across two classes); same three numbers.
        _logger.LogInfo($"[{menuId}] narrative options: -{removed} vanilla, +{added} TAOM (loaded {options.Count})");
    }

    private int RemoveVanillaOptions(NarrativeMenu menu, string menuId)
    {
        var vanillaOptions = menu.CharacterCreationMenuOptions
            .Where(o => !o.StringId.StartsWith("taom_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var option in vanillaOptions)
        {
            menu.RemoveNarrativeMenuOption(option);
        }

        return vanillaOptions.Count;
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
        GrantPlayerStartupResources(selectedCulture.StringId, manager);
    }

    private void GrantPlayerStartupResources(string cultureId, CharacterCreationManager manager)
    {
        var heroId = Hero.MainHero?.StringId;
        if (string.IsNullOrEmpty(heroId))
        {
            _logger.LogWarning("CC Finalize: Hero.MainHero is null — skipping player startup gold + equipment");
            return;
        }

        try
        {
            _playerStartupGoldService.GrantPlayerStartupGold(cultureId, heroId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CC Finalize: player startup gold failed: {ex.Message}");
        }

        var careerIsFemale = Hero.MainHero?.IsFemale ?? false;
        try
        {
            var titleType = manager.CharacterCreationContent?.SelectedTitleType;
            _playerEquipmentService.ApplyPlayerStartingEquipment(cultureId, titleType, careerIsFemale, heroId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CC Finalize: player starting equipment failed: {ex.Message}");
        }

        // Career-archetype equipment overrides the culture-default roster applied above.
        // AssignCareer just persisted the career on line 185; the menu-service property is
        // the direct input that drove that persistence, so reading from it here is
        // equivalent and avoids re-resolving via ICareerDataService.
        try
        {
            var careerId = _careerMenuService.SelectedCareerStringId;
            _careerStartingEquipmentService.ApplyCareerStartingEquipment(cultureId, careerId, careerIsFemale, heroId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CC Finalize: career starting equipment failed: {ex.Message}");
        }
    }

    private void AssignCareer(string cultureId, string heroStringId)
    {
        if (string.IsNullOrEmpty(heroStringId) || string.IsNullOrEmpty(cultureId))
            return;

        try
        {
            // Phase 9b #125 — use ctor-injected dependencies. Previously resolved via IoC.Resolve
            // (service-locator anti-pattern, see feedback_no_service_locator_in_services.md).
            if (_careerHandler == null)
            {
                _logger.LogWarning("CareerSystem: Cannot assign career at CC — handler not injected");
                return;
            }

            // Use player's career menu selection if available
            var selectedCareerId = _careerMenuService?.SelectedCareerStringId;
            if (!string.IsNullOrEmpty(selectedCareerId))
            {
                _careerHandler.OnCareerSelected(heroStringId, selectedCareerId);
                _logger.LogInfo($"CareerSystem: Assigned player-selected career '{selectedCareerId}' during CC");
                return;
            }

            // Fallback: auto-assign first eligible career (legacy/backward compat)
            if (_careerRegistry == null)
            {
                _logger.LogWarning("CareerSystem: Cannot assign career at CC — registry not injected");
                return;
            }

            foreach (var career in _careerRegistry.GetAllCareers())
            {
                foreach (var eligibleCulture in career.EligibleCultureIds)
                {
                    if (string.Equals(eligibleCulture, cultureId, StringComparison.OrdinalIgnoreCase))
                    {
                        _careerHandler.OnCareerSelected(heroStringId, career.Id);
                        _logger.LogInfo($"CareerSystem: Auto-assigned career '{career.Id}' during CC for culture '{cultureId}' (no player selection)");
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

        var fallbackRaceName = cultureData.Races != null && cultureData.Races.Length > 0
            ? cultureData.Races[0]
            : "human";

        try
        {
            // Bannerlord assigns Hero.CharacterObject.Race from FaceGen output before finalize runs.
            // Preserve the player's actual choice when it's in the culture's allowed list; otherwise
            // fall back to the culture default. Without this check, a Mordor player who picks "human"
            // in the FaceGen dropdown would be overridden to "uruk" (Races[0]) at finalize.
            //
            // Codex review #N (2026-05-06) caught: GetRaceNameFromId silently returns "human" for
            // unknown IDs (RaceManager.cs:126-130). Without IsValidRaceId gating, an invalid ID
            // would be coerced to "human", and if the culture allows "human", we would preserve
            // a value the player never picked. Validate the ID before accepting the FaceGen choice.
            var faceGenRaceId = _heroRosterAdapter.GetHeroRace(heroStringId);
            var faceGenRaceIdValid = _raceManager.IsValidRaceId(faceGenRaceId);
            var faceGenRaceName = faceGenRaceIdValid ? _raceManager.GetRaceNameFromId(faceGenRaceId) : null;

            bool faceGenChoiceAllowed = faceGenRaceIdValid
                && cultureData.Races != null
                && cultureData.Races.Length > 0
                && cultureData.Races.Any(r => string.Equals(r, faceGenRaceName, StringComparison.OrdinalIgnoreCase));

            string raceName;
            int raceId;
            if (faceGenChoiceAllowed)
            {
                raceName = faceGenRaceName;
                raceId = faceGenRaceId;
            }
            else
            {
                raceName = fallbackRaceName;
                raceId = _raceManager.GetRaceIdFromName(raceName);
            }

            _heroRosterAdapter.SetHeroRace(heroStringId, raceId);
            if (faceGenChoiceAllowed)
                _logger.LogInfo($"Set player race to '{raceName}' (id: {raceId}) — preserved FaceGen selection");
            else
                _logger.LogInfo($"Set player race to '{raceName}' (id: {raceId}) — fell back to culture default (FaceGen was '{faceGenRaceName}')");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to set player race: {ex.Message}");
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
