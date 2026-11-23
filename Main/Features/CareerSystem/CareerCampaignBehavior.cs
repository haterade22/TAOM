using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Features.CareerSystem;

public class CareerCampaignBehavior : CampaignBehaviorBase
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly ICareerLifecycleService _lifecycle;
    private readonly ICareerAbilityService _abilityService;
    private readonly IModLogger _logger;

    public CareerCampaignBehavior(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerPassiveService passiveService,
        ICareerLifecycleService lifecycle,
        ICareerAbilityService abilityService,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
        _passiveService = passiveService;
        _lifecycle = lifecycle;
        _abilityService = abilityService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.HeroLevelledUp.AddNonSerializedListener(this, OnHeroLeveledUp);
        CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Phase 9b #128 P2 R1 — clear stale ability cache from a prior campaign in the same
        // process. _abilities is keyed by hero StringId (which is stable across campaigns for the
        // player), so without this the cached CareerAbility carries old CooldownDuration baked in.
        _abilityService.ClearAll();

        _logger.LogInfo("CareerSystem: OnSessionLaunched fired");
        var hero = Hero.MainHero;
        if (hero == null)
        {
            _logger.LogWarning("CareerSystem: OnSessionLaunched — MainHero is null, aborting");
            return;
        }

        // Repair pass. Until AssignFallbackCareerIfMissing was gated to loaded saves, every campaign
        // was handed a placeholder-culture career plus its root choice BEFORE character
        // creation ran, and CareerCreationHandler then set the real career without clearing
        // that ghost. Two choices at level 1 against a budget of 2 is zero free points, and it
        // was written into the save. Idempotent: a healthy hero has nothing to drop.
        var pruned = _lifecycle.RepairForeignChoices(hero.StringId);
        if (pruned > 0)
        {
            _logger.LogInfo($"CareerSystem: Repaired {hero.Name}'s career data — dropped {pruned} choice(s) belonging to another career");
            // The refund is otherwise silent: the player's points just change on load.
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=taom_career_repair_refund}Career choices that belonged to another career were refunded: {COUNT}.")
                    .SetTextVariable("COUNT", pruned).ToString()));
        }

        // A redesigned career tree refunds a hero's choices once (#769 E12), for every hero that holds
        // career data; only the player is told. After the repair pass so it judges a clean list, before
        // the cache refresh so the passives reflect the refund.
        if (_lifecycle.MigrateAllTreeVersions(hero.StringId) > 0)
        {
            _logger.LogInfo($"CareerSystem: Refunded {hero.Name}'s career choices for a redesigned career tree");
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=taom_career_tree_reset}Your career was redesigned, so your career choices were refunded.").ToString()));
        }

        _logger.LogInfo("CareerSystem: Refreshing passive cache after session launch");
        _passiveService.RefreshCache(_dataService, _registry);

        var careerId = _dataService.GetCareerStringId(hero.StringId);
        if (careerId != null)
            _logger.LogInfo($"CareerSystem: {hero.Name} has career '{careerId}' with {_dataService.GetChoiceCount(hero.StringId)} choices");
        else
            _logger.LogInfo("CareerSystem: Main hero has no career assigned");
    }

    /// <summary>
    /// Legacy-save fallback, and ONLY on the loaded-save path.
    ///
    /// `!HasCareer` reads like "this is an old save" but is equally true on a brand-new campaign,
    /// because OnSessionLaunched fires long before character creation: v1.4.8
    /// Campaign.DoLoadingForGameType raises OnSessionStart at line 1695, and CC is only pushed
    /// afterwards by SandBoxGameManager.OnLoadFinished. Hero.MainHero is still the vanilla
    /// `main_hero` template there — culture `battania`, name "Eren" — so this granted every new
    /// player a Khand career and its root choice, which then ate their level-1 career point
    /// permanently. The engine's own load branch is the discriminator; a state test is not.
    /// </summary>
    private void OnGameLoaded(CampaignGameStarter starter)
    {
        var hero = Hero.MainHero;
        if (hero == null) return;
        if (_lifecycle.ClearUnknownCareer(hero.StringId))
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=taom_career_unknown_reset}Your career no longer exists in this version, so a new one was chosen.").ToString()));
        _lifecycle.AssignFallbackCareerIfMissing(hero.StringId, hero.Culture?.StringId);
    }

    private void OnHeroLeveledUp(Hero hero, bool shouldNotify)
    {
        if (hero != null && _dataService.HasCareer(hero.StringId))
            _passiveService.RefreshCache(_dataService, _registry);

        if (hero != Hero.MainHero) return;
        if (!_dataService.HasCareer(hero.StringId))
        {
            _logger.LogDebug($"CareerSystem: OnHeroLeveledUp — hero '{hero.StringId}' has no career, skipping");
            return;
        }

        var career = _registry.GetCareer(_dataService.GetCareerStringId(hero.StringId));
        var unspent = _registry.GetUnspentPoints(career, hero.Level, _dataService.GetChoiceCount(hero.StringId));
        if (unspent > 0)
            _logger.LogInfo($"CareerSystem: {hero.Name} leveled up — {unspent} career choice(s) available");
    }

    private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
    {
        if (victim == null) return;
        if (victim == Hero.MainHero) return;

        if (_dataService.HasCareer(victim.StringId))
        {
            _logger.LogInfo($"CareerSystem: Hero '{victim.StringId}' killed — clearing career data");
            _dataService.ClearCareer(victim.StringId);
            _passiveService.RefreshCache(_dataService, _registry);
        }
    }
}
