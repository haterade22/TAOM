using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.ShaderPrecompilation;

namespace TAOM.Features.BannerBearers.Hooks;

// Fills the one gap in the engine's banner-bearer system.
//
// BannerBearerLogic already ships in every field battle, sally-out and siege, but a formation
// only receives a banner from GeneralsAndCaptainsAssignmentLogic, and only when it has a hero
// captain whose FormationBanner is non-null. TAOM's lords carry no banner items, so no
// formation ever gets one and no bearers appear. One SetFormationBanner call per formation is
// the whole feature — the engine then handles promotion, drops, ground pickup, and
// reinforcement bearers (MissionAgentSpawnLogic re-checks GetMissingBannerCount per spawned
// troop and calls SpawnBannerBearer) on its own. No tick loop needed.
public sealed class BannerBearerAssignmentMissionLogic : MissionLogic
{
    private readonly IBannerBearerService _service;
    private readonly IModLogger _logger;

    // Warn once per bad id per mission, not once per formation.
    private readonly HashSet<string> _warnedBannerIds = new HashSet<string>();

    // Reused across formations. OnBattleSideSpawned runs once per battle side, so these never
    // compete with the per-frame paths, and there is no reason to allocate one list per formation.
    private readonly List<string?> _cultureScratch = new List<string?>();
    private readonly List<int> _bannerDataScratch = new List<int>();

    public BannerBearerAssignmentMissionLogic()
    {
        _service = IoC.Resolve<IBannerBearerService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    // Vanilla's own call site for SetFormationBanner is this event
    // (GeneralsAndCaptainsAssignmentLogic.OnBattleSideSpawned -> AssignBestCaptainsForTeam),
    // reached from MissionAgentSpawnLogic via Mission.OnInitialSpawnCompleted(battleSide) AFTER
    // initial troops have spawned. Match it exactly and we inherit vanilla's safety envelope.
    //
    // v1.5.0 deleted MissionBehavior.OnTeamDeployed(Team) outright (zero occurrences in the
    // engine) and moved the same work to OnBattleSideSpawned(BattleSideEnum). Vanilla's own
    // migration was a pure wrap: the old per-team body became the loop body over
    // Mission.GetTeamsOfSide(side), with no other change. This mirrors that exactly, so the
    // deployment-phase guards below still see the phase they were written against.
    public override void OnBattleSideSpawned(BattleSideEnum side)
    {
        base.OnBattleSideSpawned(side);

        foreach (var team in Mission.GetTeamsOfSide(side))
            AssignBannersForTeam(team);
    }

    private void AssignBannersForTeam(Team team)
    {
        if (team == null || !_service.IsEnabled) return;

        // THE FREEZE GUARD — do not remove.
        // SetFormationBanner unconditionally runs UpdateBannerBearersForDeployment, which
        // promotes agents through UpdateAgent, which ends in agent.SetIsAIPaused(true). The
        // ONLY unpause in a battle is DeploymentMissionController.FinishDeployment, which then
        // removes itself from the mission. Called outside the deployment window this looks like
        // it worked — banners appear — and every bearer stands motionless for the whole battle.
        // (It would also risk a KeyNotFoundException: OnDeploymentFinished clears
        // _initialSpawnEquipments, which the demote branch indexes into unguarded.)
        var mission = Mission.Current;
        if (mission == null || mission.Mode != MissionMode.Deployment) return;

        // Headless shader-precompile battles have no DeploymentMissionController to unpause.
        if (ShaderPrecompileRunner.IsWalkInProgress) return;

        var bannerLogic = mission.GetMissionBehavior<BannerBearerLogic>();
        if (bannerLogic == null) return;

        foreach (var formation in team.FormationsIncludingEmpty)
            TryAssignBanner(bannerLogic, formation);
    }

    private void TryAssignBanner(BannerBearerLogic bannerLogic, Formation formation)
    {
        if (formation == null || formation.CountOfUnits <= 0) return;

        // A hero captain may already have supplied one — never override vanilla's choice.
        if (bannerLogic.GetFormationBanner(formation) != null) return;

        // THE SIEGE GUARD — do not remove. SetFormationBanner forces a native banner-tableau rebuild
        // that renders the bearer's heraldry (agent.Origin.Banner); a custom-faction troop whose origin
        // Banner is null or has an empty BannerDataList makes it access-violate (0xC0000005 @
        // TaleWorlds.Native.dll+0x28ac0e — the 100%-repro siege OrderOfBattle CTD at sturgia_town_c;
        // rca-banner-bearers-siege-ctd-2026-07-23). Vanilla only banners player-team OoB formations
        // (always heraldry-backed); TAOM banners every team's, so it confirms every candidate first.
        FormationBannerHeraldry.CollectCandidateBannerDataCounts(formation, _bannerDataScratch);
        if (!_service.HasRenderableHeraldry(_bannerDataScratch)) return;

        var cultureId = ResolveFormationCultureId(formation);
        var itemId = _service.ResolveBannerItemId(cultureId);
        if (string.IsNullOrEmpty(itemId)) return;

        var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);

        // SetFormationBanner's own validation is a stripped Debug.Assert, so a typo'd or
        // non-banner id is accepted SILENTLY and then fails downstream with no diagnostic
        // (OnItemPickup won't register it, GetActiveBanner returns null). Check it here.
        if (!BannerBearerLogic.IsBannerItem(item))
        {
            WarnUnresolvedBanner(itemId!, cultureId);
            return;
        }

        bannerLogic.SetFormationBanner(formation, item);
    }

    // Boundary conversion: pull plain culture ids off the sealed Agent/Character types and let the
    // service decide. Formation.GetFirstUnit() is NOT a culture owner — it is literally
    // Arrangement.GetAllUnits()[0], so a mixed-culture formation (allied army, mercenary-heavy
    // player party) would fly whichever standard landed in slot 0. Codex review 2026-07-16 MED.
    private string? ResolveFormationCultureId(Formation formation)
    {
        _cultureScratch.Clear();
        foreach (var unit in formation.UnitsWithoutLooseDetachedOnes)
        {
            if (unit is Agent agent)
                _cultureScratch.Add(agent.Character?.Culture?.StringId);
        }

        return _service.ResolveMajorityCultureId(_cultureScratch)
            // Every unit detached (UnitsWithoutLooseDetachedOnes empty while CountOfUnits > 0).
            ?? formation.GetFirstUnit()?.Character?.Culture?.StringId;
    }

    private void WarnUnresolvedBanner(string itemId, string cultureId)
    {
        if (!_warnedBannerIds.Add(itemId)) return;

        _logger.LogWarning(
            $"[BannerBearers] banner item '{itemId}' (culture '{cultureId ?? "unknown"}') " +
            "does not resolve to a valid banner item — formations using it get no banner. " +
            "Check CultureBanners/DefaultBannerItemId in banner_bearers_config.json.");
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        _warnedBannerIds.Clear();
    }
}
