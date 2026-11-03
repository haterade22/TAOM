using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.LongDefeat.Spike;

/// <summary>Opens the spike mission (#1079): a cut-down copy of vanilla's custom siege list; docs/features/long-defeat.md names what it leaves out and why.</summary>
internal sealed class LongDefeatSpikeLauncher
{
    private readonly IModLogger _logger;

    public LongDefeatSpikeLauncher(IModLogger logger)
    {
        _logger = logger;
    }

    public bool Launch(LongDefeatSpikeOptions options)
    {
        try
        {
            var fortress = LongDefeatSpikeFortress.For(options.Fortress);
            if (fortress == null)
            {
                _logger.LogError($"[LongDefeat][Spike] unknown fortress '{options.Fortress}'");
                return false;
            }

            var objects = MBObjectManager.Instance;
            var defenderCulture = objects.GetObject<BasicCultureObject>(fortress.DefenderCulture);
            var attackerCulture = objects.GetObject<BasicCultureObject>(fortress.AttackerCulture);
            var player = objects.GetObject<BasicCharacterObject>(fortress.Player);
            var missing = fortress.Troops.Where(id => objects.GetObject<BasicCharacterObject>(id) == null).ToList();
            if (defenderCulture == null || attackerCulture == null || player == null || missing.Count > 0)
            {
                _logger.LogError($"[LongDefeat][Spike] cannot load '{options.Fortress}': defender culture {defenderCulture != null}, "
                    + $"attacker culture {attackerCulture != null}, player troop {player != null}, missing troops [{string.Join(", ", missing)}]");
                return false;
            }

            Game.Current.PlayerTroop = player;
            var defender = Combatant("{=!}The Long Defeat, Defenders", defenderCulture, BattleSideEnum.Defender);
            var attacker = Combatant("{=!}The Long Defeat, Attackers", attackerCulture, BattleSideEnum.Attacker);
            _logger.LogInfo($"[LongDefeat][S1] launching scene {fortress.Scene}; {options}");
            var mission = MissionState.OpenNew(LongDefeatMission.MissionName, Record(fortress.Scene), _ => Behaviors(options, fortress, defender, attacker));
            mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LongDefeat][Spike] opening the mission failed: {ex}");
            return false;
        }
    }

    private IEnumerable<MissionBehavior> Behaviors(LongDefeatSpikeOptions options, LongDefeatSpikeFortress fortress, CustomBattleCombatant defender,
        CustomBattleCombatant attacker) =>
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
            new LongDefeatSpikeLogic(options, fortress, _logger),
        };

    private static List<MissionSiegeWeapon> Weapons(SiegeEngineType first, int firstCount, SiegeEngineType second, int secondCount)
    {
        var list = new List<MissionSiegeWeapon>();
        for (var i = 0; i < firstCount; i++)
            list.Add(MissionSiegeWeapon.CreateDefaultWeapon(first));
        for (var i = 0; i < secondCount; i++)
            list.Add(MissionSiegeWeapon.CreateDefaultWeapon(second));
        return list;
    }

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
