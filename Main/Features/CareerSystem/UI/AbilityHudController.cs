using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Features.CareerSystem.UI;

// Boundary class for the in-battle CareerAbilityHUD Gauntlet layer (Issue #102).
// Owns the layer/movie/VM, the per-mission HUD string cache, and the screen attach + detach
// dance. Per the test-coverage rule "Entry Points: Not required" -- verified in-battle.
public class AbilityHudController : IAbilityHudController
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerAbilityService _abilityService;
    private readonly IModLogger _logger;

    private GauntletLayer _hudLayer;
    private CareerAbilityHudVM _hudVM;
    private GauntletMovieIdentifier _hudMovie;
    private bool _hudInitialized;

    // Codex #102 review (MED) — capture the screen the layer was attached to at TryInitialize
    // time so Cleanup() removes from the same screen, not whatever happens to be TopScreen
    // when the mission ends (e.g., a battle-result or pause modal may sit above the
    // MissionScreen at that point). ScreenBase.RemoveLayer calls HandleFinalize() unconditionally
    // without ownership check (verified against installed v1.4.5 TaleWorlds.ScreenSystem.ScreenBase
    // line 308-316), so cleaning up through the wrong screen finalizes the layer AND leaves a
    // finalized layer in the owning screen's list -- a real corruption path.
    private ScreenBase _attachedScreen;

    // Per-mission cache so per-frame Refresh does not allocate a new TextObject + string
    // interpolation for the ability name and sprite path on every tick (Codex Review #31).
    private string _cachedHudHeroId;
    private string _cachedHudAbilityName;
    private string _cachedHudAbilitySprite;

    public AbilityHudController(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerAbilityService abilityService,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
        _abilityService = abilityService;
        _logger = logger;
    }

    public void TryInitialize()
    {
        if (_hudInitialized) return;

        var topScreen = ScreenManager.TopScreen;
        if (topScreen == null) return;

        _hudVM = new CareerAbilityHudVM();
        _hudLayer = new GauntletLayer("CareerAbilityHUD", 50);
        _hudMovie = _hudLayer.LoadMovie("AbilityHUD", _hudVM);
        topScreen.AddLayer(_hudLayer);
        _attachedScreen = topScreen;
        _hudInitialized = true;
        _logger?.LogInfo("CareerSystem: HUD layer initialized");
    }

    public void Refresh(string heroStringId)
    {
        if (_hudVM == null) return;

        if (!_dataService.HasCareer(heroStringId))
        {
            _hudVM.Update(false, null, null, 0f, false);
            return;
        }

        var ability = _abilityService.GetOrCreateAbility(heroStringId, _registry, _dataService);
        if (ability == null)
        {
            _hudVM.Update(false, null, null, 0f, false);
            return;
        }

        if (!string.Equals(heroStringId, _cachedHudHeroId, StringComparison.Ordinal))
            RefreshHudCache(heroStringId, ability);

        _hudVM.Update(true, _cachedHudAbilityName, _cachedHudAbilitySprite, ability.ReadyProgress01, ability.IsReady);
    }

    public void Cleanup()
    {
        if (!_hudInitialized) return;

        // Deep-review #102 HIGH — engine-touching teardown lives in try/catch so a throw from
        // ScreenBase.RemoveLayer / GauntletLayer.ReleaseMovie / VM.OnFinalize cannot leave the
        // Singleton with _hudInitialized=true forever (which would silently kill the HUD for
        // every subsequent mission this session). Field-reset block runs in finally.
        try
        {
            if (_attachedScreen != null && _hudLayer != null)
                _attachedScreen.RemoveLayer(_hudLayer);

            if (_hudMovie != null && _hudLayer != null)
                _hudLayer.ReleaseMovie(_hudMovie);

            _hudVM?.OnFinalize();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"CareerSystem: HUD cleanup engine call threw — {ex.Message}");
        }
        finally
        {
            _hudLayer = null;
            _hudVM = null;
            _hudMovie = null;
            _attachedScreen = null;
            _hudInitialized = false;

            _cachedHudHeroId = null;
            _cachedHudAbilityName = null;
            _cachedHudAbilitySprite = null;
        }
    }

    private void RefreshHudCache(string heroStringId, CareerAbility ability)
    {
        var careerId = _dataService.GetCareerStringId(heroStringId);
        var career = careerId != null ? _registry.GetCareer(careerId) : null;
        var rawName = career?.DisplayName ?? ability.TemplateId;

        _cachedHudHeroId = heroStringId;
        _cachedHudAbilityName = new TextObject(rawName).ToString();
        _cachedHudAbilitySprite = career != null
            ? $"CareerSystem\\Abilities\\{career.AbilityTemplateId}"
            : null;
    }
}
