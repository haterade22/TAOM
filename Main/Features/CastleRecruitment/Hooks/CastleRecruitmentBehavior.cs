using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.CastleRecruitment.Hooks;

/// <summary>
/// Player-facing + maintenance half of Castle Recruitment (the AI half lives in the Patch42 Harmony
/// patches). Thin event router (ADR-002): the notable spawn + volunteer-fill engine glue is in
/// <see cref="CastleNotableMaintainer"/>; all decisions come from <see cref="ICastleRecruitmentService"/>.
///
/// Responsibilities:
/// 1. Register a "Recruit troops" option on the vanilla "castle" game menu (opens the vanilla
///    recruit screen, which is already settlement-type-agnostic).
/// 2. Drive castle notable population + daily volunteer generation via the maintainer (new games AND
///    existing saves).
/// 3. Suppress campaign issues/quests for castle notables (relations untouched).
/// </summary>
public class CastleRecruitmentBehavior : CampaignBehaviorBase
{
    private const string RecruitOptionId = "taom_castle_recruit_volunteers";

    private readonly ICastleRecruitmentService _service;
    private readonly CastleNotableMaintainer _maintainer;
    private readonly IModLogger _logger;
    private readonly ICoopSessionProvider _coopSession;

    public CastleRecruitmentBehavior(ICastleRecruitmentService service, IModLogger logger,
        ICoopSessionProvider coopSession)
    {
        _service = service;
        _logger = logger;
        _coopSession = coopSession;
        _maintainer = new CastleNotableMaintainer(service, logger);
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
        CampaignEvents.CanHaveCampaignIssuesEvent.AddNonSerializedListener(this, OnCanHaveCampaignIssues);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // No state: castle notables are real Hero objects persisted by the engine's save system;
        // volunteers live in Hero.VolunteerTypes. Nothing TAOM-specific to round-trip.
    }

    // --- Player menu ---

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Registered unconditionally so the MCM master toggle takes effect at runtime (the condition
        // re-checks IsEnabled each open). Vanilla creates the "castle" menu in its own
        // OnSessionLaunched (registered before TAOM behaviors), so the menu exists by now.
        starter.AddGameMenuOption("castle", RecruitOptionId, "{=E31IJyqs}Recruit troops",
            RecruitCondition, RecruitConsequence, isLeave: false, index: 4);
    }

    private bool RecruitCondition(MenuCallbackArgs args)
    {
        if (!_service.IsEnabled)
            return false;
        var settlement = Settlement.CurrentSettlement;
        if (settlement == null || !settlement.IsCastle || !HasAnyRecruiter(settlement))
            return false;

        bool canPlayerDo = Campaign.Current.Models.SettlementAccessModel.CanMainHeroDoSettlementAction(
            settlement, SettlementAccessModel.SettlementAction.RecruitTroops, out bool disableOption, out TextObject disabledText);
        args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
        return MenuHelper.SetOptionProperties(args, canPlayerDo, disableOption, disabledText);
    }

    private void RecruitConsequence(MenuCallbackArgs args) => args.MenuContext.OpenRecruitVolunteers();

    private static bool HasAnyRecruiter(Settlement settlement)
    {
        foreach (Hero notable in settlement.Notables)
        {
            if (notable.IsAlive && notable.CanHaveRecruits)
                return true;
        }
        return false;
    }

    // --- Notable population + volunteer fill (delegated to the maintainer) ---

    // internal for TAOM.Tests (InternalsVisibleTo) — lets the co-op authority gate be asserted directly.
    internal void OnNewGameCreated(CampaignGameStarter starter)
    {
        if (!_coopSession.IsAuthority) return;
        if (_service.IsEnabled)
            _maintainer.EnsureAllCastles();
    }

    // CO-OP: host-only. This is the one castle-recruitment path a client actually reaches — the
    // daily work is on DailyTickSettlementEvent, which BannerlordCoop's PartyTickPatch blocks on a
    // client, but OnGameLoadedEvent fires on every peer because a joining client loads the HOST'S
    // save through the normal SaveManager pipeline.
    //
    // EnsureAllCastles is deficit-based (it spawns only target-minus-existing), so with matching MCM
    // settings it is a no-op. But MCM settings are per-user and BannerlordCoop syncs none of them, so
    // a client whose CastleNotablesPerCastle exceeds the host's spawns the difference locally — and
    // HeroCreator.CreateNotable on a client runs into Coop's MBObjectBase.StringId setter prefix,
    // which suppresses the write and leaves MBObjectManager's Dictionary<string,T> to throw on a
    // null key. Either way the notables would be heroes the host never created.
    // internal for TAOM.Tests (InternalsVisibleTo) — lets the co-op authority gate be asserted directly.
    internal void OnGameLoaded(CampaignGameStarter starter)
    {
        if (!_coopSession.IsAuthority) return;
        if (_service.IsEnabled)
            _maintainer.EnsureAllCastles();
    }

    private void OnDailyTickSettlement(Settlement settlement)
    {
        if (_service.IsEnabled && settlement != null && settlement.IsCastle)
            _maintainer.TickCastle(settlement);
    }

    // --- Issue / quest suppression (relations untouched) ---

    private void OnCanHaveCampaignIssues(Hero hero, ref bool result)
    {
        if (result && _service.IsEnabled && hero.CurrentSettlement?.IsCastle == true)
            result = false;
    }
}
