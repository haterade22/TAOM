using SandBox;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerBearers.Models;

// Overrides the engine's banner-bearer policy so TAOM formations actually field standards,
// and so a bearer's race is respected.
//
// Why subclass SandboxBattleBannerBearersModel instead of decorating MBGameModel.BaseModel:
// BannerBearerLogic.OnBehaviorInitialize calls InitializeModel(this) on
// MissionGameModels.Current.BattleBannerBearersModel — i.e. only on the RESOLVED model
// (ours, last-registered-wins). A BaseModel instance therefore never receives the logic, so
// BaseModel.CanFormationDeployBannerBearers would read a null BannerBearerLogic and return
// false forever — silently disabling every banner. Subclassing keeps `this.BannerBearerLogic`
// (the initialized one) behind every base call, and keeps base.GetAgentBannerBearingPriority's
// internal CanAgentBecomeBannerBearer call dispatching to OUR race gate. Same pattern as
// TaomAgentApplyDamageModel : SandboxAgentApplyDamageModel.
//
// EVERY override folds the master toggle by deferring to `base`, never by returning a TAOM
// value or a zero. The model stays registered when the feature is off, and the engine still
// asks it for every formation — including formations a hero captain or the player's
// Order-of-Battle screen banners through vanilla's own path, which TAOM never intercepts. An
// override that answered 0 (or TAOM's tuned threshold) while "disabled" would SUPPRESS that
// vanilla mechanic rather than leave it alone: strictly worse than vanilla, not equal to it.
// Deep-review 2026-07-16 caught exactly that; see docs/reviews/rca-banner-bearers-2026-07-16.md.
//
// Race safety is inherited, not implemented here: BannerBearerLogic.UpdateAgent converts an
// EXISTING agent in place (UpdateSpawnEquipmentAndRefreshVisuals), never respawning it, and
// reinforcement bearers come from Mission.SpawnTroop with a real troop origin. Character,
// Monster and skin are untouched either way, so an orc bearer stays an orc.
public class TaomBattleBannerBearersModel : SandboxBattleBannerBearersModel
{
    private readonly IBannerBearerService _service;

    public TaomBattleBannerBearersModel(IBannerBearerService service)
    {
        _service = service;
    }

    // Constant for a mission (config is a process-lifetime Lazy<T> cache) — required, because
    // BannerBearerLogic.OnAgentAdded/OnAgentRemoved detect this threshold with exact equality
    // (CountOfUnits == minimum) and a mid-mission change would silently stop those edges firing.
    public override int GetMinimumFormationTroopCountToBearBanners() =>
        _service.IsEnabled
            ? _service.MinimumFormationTroopCount
            : base.GetMinimumFormationTroopCountToBearBanners();

    public override bool CanAgentBecomeBannerBearer(Agent agent) =>
        !_service.IsEnabled
            ? base.CanAgentBecomeBannerBearer(agent)
            // Vanilla gate first (IsHuman == the IsHumanoid agent flag, true for every LOTRLOME
            // race; plus !IsMainAgent && !IsHero && IsAIControlled), then TAOM's race gate.
            // -1 for a null Character is deliberately an invalid race id: IsRaceAllowed fails closed.
            : base.CanAgentBecomeBannerBearer(agent) && _service.IsRaceAllowed(agent?.Character?.Race ?? -1);

    public override int GetDesiredNumberOfBannerBearersForFormation(Formation formation) =>
        !_service.IsEnabled
            ? base.GetDesiredNumberOfBannerBearersForFormation(formation)
            // Vanilla's gate also verifies BannerBearerLogic != null and that the formation HAS
            // a banner item — SpawnBannerBearer dereferences the controller's BannerItem with no
            // null check of its own. Hot path (per spawned troop via GetMissingBannerCount):
            // allocation-free.
            : CanFormationDeployBannerBearers(formation)
                ? _service.GetDesiredBearerCount(formation.CountOfUnits, formation.FormationIndex)
                : 0;

    // GetBannerBearerReplacementWeapon is deliberately NOT overridden. Vanilla resolves it from
    // CultureObject.BannerBearerReplacementWeapons and returns null when the culture declares
    // none, which CreateBannerEquipmentForAgent turns into an unarmed bearer. Rather than patch
    // that in C#, every culture in CultureBanners declares the data (taom_spcultures.xml /
    // spcultures.xslt), and ShippedBannerBearerConfigTests pins the invariant at build time.
}
