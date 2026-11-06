using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TAOM.Adapters;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// Opens the Long Defeat mission (#1079): a cut-down copy of vanilla's custom siege list. It leaves out the spawn logic and the
/// logic that needs it (<c>GeneralsAndCaptainsAssignmentLogic</c> throws in <c>OnInitialSpawnCompleted</c> without it), and the
/// battle-end logic, which would end a siege this mode runs on purpose. The client's game manager calls <see cref="Launch"/> from
/// <c>OnLoadFinished</c>. Never throws.
/// </summary>
internal sealed class LongDefeatMissionLauncher
{
    private readonly LongDefeatConfigCatalog _catalog;
    private readonly LongDefeatSession _session;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly ILongDefeatMenuAdapter _menu;
    private readonly LongDefeatRunRecords _records;
    private int _missions;

    public LongDefeatMissionLauncher(LongDefeatConfigCatalog catalog, LongDefeatSession session, IModLogger logger, IOutcomeLog outcomes,
        ILongDefeatMenuAdapter menu, LongDefeatRunRecords records)
    {
        _catalog = catalog;
        _session = session;
        _logger = logger;
        _outcomes = outcomes;
        _menu = menu;
        _records = records;
    }

    /// <summary>True once the mission is open. An unknown fortress id, a missing culture or troop, or an engine failure logs an error and returns false.</summary>
    public bool Launch(LongDefeatSetup setup)
    {
        try
        {
            if (!_catalog.TryGetFortress(setup.FortressId, out var fortress))
            {
                _logger.LogError($"[LongDefeat] unknown fortress '{setup.FortressId}'; nothing launched");
                _session.RecordFault("Launch");
                return false;
            }

            var objects = MBObjectManager.Instance;
            var defenderCulture = objects?.GetObject<BasicCultureObject>(fortress.DefenderCulture);
            var attackerCulture = objects?.GetObject<BasicCultureObject>(fortress.AttackerCulture);
            var player = objects?.GetObject<BasicCharacterObject>(fortress.PlayerTroop);
            var plan = _catalog.BuildPlan(fortress, setup.Options);
            var missing = RunPlanTroops.AllTroopIds(plan).Where(id => objects?.GetObject<BasicCharacterObject>(id) == null).ToList();
            if (defenderCulture == null || attackerCulture == null || player == null || missing.Count > 0)
            {
                _logger.LogError($"[LongDefeat] cannot load '{fortress.Id}': defender culture {defenderCulture != null}, attacker culture {attackerCulture != null}, "
                    + $"player troop {player != null}, missing troops [{string.Join(", ", missing)}]");
                _session.RecordFault("Launch");
                return false;
            }

            plan = plan with
            {
                TroopGroups = LongDefeatTroopGroups.Read(plan),
                Provenance = plan.Provenance with
                {
                    Build = BuildStampReport.ReadInformationalVersion(typeof(LongDefeatMissionLauncher).Assembly),
                    Mods = ModuleHelper.GetActiveModules().Select(m => m.Id + " " + m.Version).ToList(),
                    CheatMode = TaleWorlds.Engine.NativeConfig.CheatMode,
                },
            };
            Game.Current.PlayerTroop = player;
            var defender = Combatant("{=!}The Long Defeat, Defenders", defenderCulture, BattleSideEnum.Defender);
            var attacker = Combatant("{=!}The Long Defeat, Attackers", attackerCulture, BattleSideEnum.Attacker);
            _logger.LogInfo($"[LongDefeat] launching {fortress.Scene} for fortress {fortress.Id} ({setup.Trigger})");
            var records = setup.RecordsFolder == null ? _records : LongDefeatRunRecords.ForFolder(setup.RecordsFolder, _logger.LogFilePath);
            var mission = MissionState.OpenNew(LongDefeatMission.MissionName, Record(fortress.Scene), _ => CreateBehaviors(plan, defender, attacker, records));

            // Both are needed: a dead player may take a soldier, and IsFriendlyMission true makes Tab end the mission and leaves the order bar empty.
            mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
            mission.IsFriendlyMission = false;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LongDefeat] opening the mission failed: {ex}");
            _session.RecordFault("Launch");
            return false;
        }
    }

    internal IReadOnlyList<MissionBehavior> CreateBehaviors(RunPlan plan, CustomBattleCombatant defender, CustomBattleCombatant attacker,
        LongDefeatRunRecords records) =>
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
            new LongDefeatMissionLogic(plan, _session, _logger, _outcomes, _menu, "ld" + Interlocked.Increment(ref _missions), records),
        };

    private static List<MissionSiegeWeapon> Weapons(SiegeEngineType first, int firstCount, SiegeEngineType second, int secondCount) =>
        Enumerable.Repeat(first, firstCount).Concat(Enumerable.Repeat(second, secondCount)).Select(t => MissionSiegeWeapon.CreateDefaultWeapon(t)).ToList();

    // "level_3 siege" is vanilla's scene level string for upgrade level 0 (BannerlordMissions.cs:162-167).
    private static MissionInitializerRecord Record(string scene) =>
        new(scene)
        {
            DoNotUseLoadingScreen = false,
            PlayingInCampaignMode = false,
            AtmosphereOnCampaign = BannerlordMissions.CreateAtmosphereInfoForMission("spring", 12),
            SceneLevels = "level_3 siege",
            DecalAtlasGroup = 2,
        };

    private static CustomBattleCombatant Combatant(string name, BasicCultureObject culture, BattleSideEnum side) =>
        new(new TextObject(name), culture, new Banner(culture.Banner, culture.Color, culture.Color2)) { Side = side };
}
