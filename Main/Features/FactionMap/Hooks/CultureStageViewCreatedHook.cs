using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.TwoDimension;
using TAOM.Core.Logging;
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
        IModLogger logger)
    {
        _configProvider = configProvider;
        _registry = registry;
        _selectionService = selectionService;
        _hoverService = hoverService;
        _cultureResolver = cultureResolver;
        _landmarkService = landmarkService;
        _cultureSettingService = cultureSettingService;
        _logger = logger;
    }

    public static FactionSelectionVM? CurrentVM => _factionVM;

    public void OnCreated(object viewInstance)
    {
        try
        {
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

                // Replicate vanilla NextStage() logic directly because the vanilla method
                // accesses _dataSource.CurrentSelectedCulture.Culture which is null since
                // we replaced the vanilla VM with FactionSelectionVM.
                try
                {
                    var charCreationMgrField = AccessTools.Field(viewInstance.GetType(), "_characterCreationManager");
                    var charCreationMgr = charCreationMgrField?.GetValue(viewInstance);
                    if (charCreationMgr != null)
                    {
                        var contentProp = AccessTools.Property(charCreationMgr.GetType(), "CharacterCreationContent");
                        var content = contentProp?.GetValue(charCreationMgr);
                        if (content != null)
                        {
                            var setNameMethod = AccessTools.Method(content.GetType(), "SetMainCharacterName",
                                new[] { typeof(string) });
                            var generatedName = NameGenerator.Current
                                .GenerateFirstNameForPlayer(culture, Hero.MainHero.IsFemale)
                                .ToString();
                            setNameMethod?.Invoke(content, new object[] { generatedName });
                        }
                    }

                    var affirmativeField = AccessTools.Field(viewInstance.GetType(), "_affirmativeAction");
                    var affirmativeAction = affirmativeField?.GetValue(viewInstance) as Delegate;
                    affirmativeAction?.DynamicInvoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"NextStage error: {ex.Message}\n{ex.StackTrace}");
                }
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
                _landmarkService);

            if (_originalDataSource != null)
            {
                var titleProp = AccessTools.Property(_originalDataSource.GetType(), "Title");
                if (titleProp != null)
                    _factionVM.Title = titleProp.GetValue(_originalDataSource) as string ?? "Choose your Realm";
            }

            try { UIResourceManager.BrushFactory.LoadBrushFile("FactionMap"); }
            catch (Exception brushEx) { _logger.LogError($"Could not load FactionMap brushes: {brushEx.Message}"); }

            try
            {
                var spriteData = UIResourceManager.SpriteData;
                if (spriteData != null && spriteData.SpriteCategories.ContainsKey("ui_group1"))
                {
                    var cat = spriteData.SpriteCategories["ui_group1"];
                    if (!cat.IsLoaded)
                        cat.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
                }
            }
            catch (Exception spriteEx) { _logger.LogError($"Could not load sprite category: {spriteEx.Message}"); }

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
}
