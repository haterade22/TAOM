using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TAOM.Core.Logging;
using TAOM.Features.Music;
using TAOM.Features.FactionMap.ViewModels;
using TAOM.Features.FactionMap.Widgets;

namespace TAOM.Features.FactionMap.Hooks;

public class CultureStageViewCreatedHook : IOnCultureStageViewCreated
{
    private readonly IFactionConfigProvider _configProvider;
    private readonly IFactionRegistryService _registry;
    private readonly IFactionSelectionService _selectionService;
    private readonly IFactionHoverService _hoverService;
    private readonly ICultureResolverService _cultureResolver;
    private readonly ILandmarkService _landmarkService;
    private readonly ICultureSettingService _cultureSettingService;
    private readonly ICultureStageProgressionService _progressionService;
    private readonly IModLogger _logger;

    private static FactionSelectionVM? _factionVM;
    private static object? _originalDataSource;

    public CultureStageViewCreatedHook(
        IFactionConfigProvider configProvider,
        IFactionRegistryService registry,
        IFactionSelectionService selectionService,
        IFactionHoverService hoverService,
        ICultureResolverService cultureResolver,
        ILandmarkService landmarkService,
        ICultureSettingService cultureSettingService,
        ICultureStageProgressionService progressionService,
        IModLogger logger)
    {
        _configProvider = configProvider;
        _registry = registry;
        _selectionService = selectionService;
        _hoverService = hoverService;
        _cultureResolver = cultureResolver;
        _landmarkService = landmarkService;
        _cultureSettingService = cultureSettingService;
        _progressionService = progressionService;
        _logger = logger;
    }

    public static FactionSelectionVM? CurrentVM => _factionVM;

    public void OnCreated(object viewInstance)
    {
        try
        {
            // Phase 9b #175 F6 — call Cleanup() BEFORE ResetSession() so backward navigation
            // (CC stage → prior → forward again with sequence construct-new → finalize-old) cannot
            // leave the stale _factionVM alive briefly while the new session initializes. If
            // CurrentVM read happens during that window, the tick patch would tick the OLD VM
            // with the NEW widget state (just cleared by ResetSession) producing stale
            // HoveredFactionName="" for 0-1 frames.
            Cleanup();
            PolygonWidget.ResetSession();

            var regions = _configProvider.LoadRegions();
            var factions = _configProvider.LoadFactions();
            _registry.Initialize(regions, factions);
            FactionMapStaticBridge.Initialize(_registry);

            var layerField = AccessTools.Field(viewInstance.GetType(), "GauntletLayer");
            var gauntletLayer = layerField?.GetValue(viewInstance) as GauntletLayer;
            if (gauntletLayer == null)
            {
                _logger.LogError("GauntletLayer not found in CultureStageView");
                return;
            }

            var movieField = AccessTools.Field(viewInstance.GetType(), "_movie");
            var originalMovie = movieField?.GetValue(viewInstance) as GauntletMovieIdentifier;

            var dataSourceField = AccessTools.Field(viewInstance.GetType(), "_dataSource");
            _originalDataSource = dataSourceField?.GetValue(viewInstance);

            if (originalMovie == null)
            {
                _logger.LogError("Original movie not found");
                return;
            }

            gauntletLayer.ReleaseMovie(originalMovie);

            Action<CultureObject> onCultureConfirmed = (culture) =>
            {
                if (culture == null)
                {
                    _logger.LogError("Culture is null!");
                    return;
                }
                _cultureSettingService.SetCultureOnCharacterCreation(culture, viewInstance, _originalDataSource);
                _progressionService.InvokeNextStage(viewInstance, culture.StringId, Hero.MainHero?.IsFemale ?? false);
            };

            Action onPreviousStage = () =>
            {
                var prevStageMethod = AccessTools.Method(viewInstance.GetType(), "PreviousStage");
                prevStageMethod?.Invoke(viewInstance, null);
            };

            _factionVM = new FactionSelectionVM(
                onCultureConfirmed,
                onPreviousStage,
                _selectionService,
                _hoverService,
                _cultureResolver,
                _landmarkService,
                OnFactionMapCultureSelected);

            if (_originalDataSource != null)
            {
                var titleProp = AccessTools.Property(_originalDataSource.GetType(), "Title");
                if (titleProp != null)
                    _factionVM.Title = titleProp.GetValue(_originalDataSource) as string ?? "Choose your Realm";
            }

            _progressionService.LoadFactionMapResources();

            var newMovie = gauntletLayer.LoadMovie("CharacterCreationCultureStage", _factionVM);
            movieField?.SetValue(viewInstance, newMovie);

            _logger.LogInfo($"FactionMap injected (movie={newMovie != null})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CultureStageView constructor patch error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public static void Cleanup()
    {
        _factionVM = null;
        _originalDataSource = null;
    }

    private void OnFactionMapCultureSelected(string cultureId)
    {
        if (string.IsNullOrWhiteSpace(cultureId))
            return;

        try
        {
            IoC.Resolve<ICharacterCreationMusicContextService>()?.SelectCulture(cultureId);
            CharacterCreationMusicSmokeTrace.CultureSelected(cultureId, "faction_map_region_selected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Patch46-Music] faction map selected-culture signal failed: {ex.Message}");
        }
    }
}
