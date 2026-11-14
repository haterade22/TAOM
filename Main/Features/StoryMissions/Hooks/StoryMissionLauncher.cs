using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Features.StoryMissions.Hooks;

/// <summary>
/// Opens a story mission (#1105) inside a loaded campaign: the Long Defeat's siege behavior list (a cut-down copy of vanilla's, see
/// <see cref="LongDefeatMissionLauncher"/>) under <c>MissionState.OpenNew</c> with no map event, as Host Review opens its mission. Its callers are the
/// dev menu and the story offer menu (<see cref="StoryOfferBehavior"/>); both open it from a menu option on the campaign map, after
/// refusing an encounter, a map event and enlistment. It never
/// assigns <c>Game.PlayerTroop</c>, which in a campaign is the player's hero, and each launch gets its own <see cref="LongDefeatSession"/>.
/// The run ends the mission, not the game, so the player lands back on the map. Writes nothing to the campaign. Never throws.
/// </summary>
internal sealed class StoryMissionLauncher
{
    private readonly StoryMissionCatalog _catalog;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private int _missions;

    public StoryMissionLauncher(StoryMissionCatalog catalog, IModLogger logger, IOutcomeLog outcomes)
    {
        _catalog = catalog;
        _logger = logger;
        _outcomes = outcomes;
    }

    /// <summary>True once the mission is open. An unknown id, the attacking side, no campaign, a missing culture or troop, or an engine failure logs an error and returns false.</summary>
    /// <param name="onVerdict">Optional: called once on the main thread with the mission's verdict (the story offer applies it after the mission).</param>
    public bool Launch(string missionId, bool autoplay, Action<StoryOutcome>? onVerdict = null)
    {
        try
        {
            if (!_catalog.TryGet(missionId, out var definition))
                return Refuse($"unknown story mission '{missionId}'");
            if (definition.Side != StorySide.Defending)
                return Refuse($"'{definition.Id}' is an attacking mission; the player cannot take the attacking side yet");
            if (Campaign.Current == null || Game.Current?.PlayerTroop == null)
                return Refuse("a story mission needs a running campaign");

            var objects = MBObjectManager.Instance;
            var defenderCulture = objects?.GetObject<BasicCultureObject>(definition.DefenderCulture);
            var attackerCulture = objects?.GetObject<BasicCultureObject>(definition.AttackerCulture);
            var plan = StoryPlanBuilder.Build(definition, RunOptions.Default with { Autoplay = autoplay });
            var missing = RunPlanTroops.AllTroopIds(plan).Where(id => objects?.GetObject<BasicCharacterObject>(id) == null).ToList();
            if (defenderCulture == null || attackerCulture == null || missing.Count > 0)
                return Refuse($"cannot load '{definition.Id}': defender culture {defenderCulture != null}, attacker culture {attackerCulture != null}, missing troops [{string.Join(", ", missing)}]");

            var session = new LongDefeatSession();
            var defender = Combatant("{=!}Story mission, defenders", defenderCulture, BattleSideEnum.Defender);
            var attacker = Combatant("{=!}Story mission, attackers", attackerCulture, BattleSideEnum.Attacker);
            var key = "story" + Interlocked.Increment(ref _missions);
            _logger.LogInfo($"[StoryMissions] launching {definition.Scene} for '{definition.Id}' ({key}, autoplay {autoplay})");
            var opened = MissionState.OpenNew(StoryMission.MissionName, Record(definition.Scene), opening => CreateBehaviors(opening, plan, session, definition.Side, defender, attacker, key, definition.Music, onVerdict));

            // Both are needed: a dead player may take a soldier, and IsFriendlyMission true makes Tab end the mission and leaves the order bar empty.
            opened.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
            opened.IsFriendlyMission = false;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[StoryMissions] opening the mission failed: {ex}");
            return false;
        }
    }

    private bool Refuse(string why)
    {
        _logger.LogError($"[StoryMissions] {why}; nothing launched");
        return false;
    }

    private IReadOnlyList<MissionBehavior> CreateBehaviors(Mission mission, RunPlan plan, LongDefeatSession session, StorySide side, CustomBattleCombatant defender,
        CustomBattleCombatant attacker, string key, string? music, Action<StoryOutcome>? onVerdict) =>
        new MissionBehavior[]
        {
            new BattleSpawnLogic("battle_set"),
            new MissionOptionsComponent(),
            new MissionCombatantsLogic(null, defender, defender, attacker, Mission.MissionTeamAITypeEnum.Siege, isPlayerSergeant: false),
            new SiegeMissionPreparationHandler(false, false, new[] { 1f, 1f }, true),
            new AgentHumanAILogic(),
            new AmmoSupplyLogic(new List<BattleSideEnum> { BattleSideEnum.Defender }),
            new AssignPlayerRoleInTeamMissionController(true, false, false),
            new MissionAgentPanicHandler(),
            new MissionBoundaryPlacer(),
            new MissionBoundaryCrossingHandler(),
            new AgentMoraleInteractionLogic(),
            new EquipmentControllerLeaveLogic(),
            new MissionSiegeEnginesLogic(Weapons(DefaultSiegeEngineTypes.Catapult, 4, DefaultSiegeEngineTypes.Trebuchet, 2),
                Weapons(DefaultSiegeEngineTypes.Ram, 1, DefaultSiegeEngineTypes.SiegeTower, 2)),
            new LongDefeatMissionLogic(plan, session, _logger, new HostedRunOutcomeLog(_outcomes), mission.EndMission, key),
            new StoryMissionLogic(session, side, _logger, _outcomes, key, music, onVerdict),
        };

    private static List<MissionSiegeWeapon> Weapons(SiegeEngineType first, int firstCount, SiegeEngineType second, int secondCount) =>
        Enumerable.Repeat(first, firstCount).Concat(Enumerable.Repeat(second, secondCount)).Select(t => MissionSiegeWeapon.CreateDefaultWeapon(t)).ToList();

    // "level_3 siege" is vanilla's scene level string for upgrade level 0 (BannerlordMissions.cs:162-167). A campaign mission, with no friendly fire (Host Review's record).
    // For a "forceatmo" scene, Patch16 (AtmospherePersistence) then rewrites PlayingInCampaignMode and the atmosphere in Mission.Initialize. OpenNew reads the
    // values here once before that, in OnMissionIsStarting, and the scene keeps its own atmosphere.
    private static MissionInitializerRecord Record(string scene) =>
        new(scene)
        {
            DoNotUseLoadingScreen = false,
            PlayingInCampaignMode = true,
            DamageToFriendsMultiplier = 0f,
            DamageFromPlayerToFriendsMultiplier = 0f,
            AtmosphereOnCampaign = BannerlordMissions.CreateAtmosphereInfoForMission("spring", 12),
            SceneLevels = "level_3 siege",
            DecalAtlasGroup = 2,
        };

    private static CustomBattleCombatant Combatant(string name, BasicCultureObject culture, BattleSideEnum side) =>
        new(new TextObject(name), culture, new Banner(culture.Banner, culture.Color, culture.Color2)) { Side = side };
}
