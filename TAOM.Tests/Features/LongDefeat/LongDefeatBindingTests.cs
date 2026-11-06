using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.AI;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The engine members the tests below name (mission, siege, hook and entity members the Long Defeat adapter, launcher and logic call),
/// against the installed engine. A member that stops resolving fails when its method is first compiled, before any try inside it can
/// run, so each is pinned here. It pins the named members only: the adapter calls others (the deployment points' weapons, a wall's defense side, a formation's AI flag) that no test here names.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatBindingTests
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheMissionMembers_StillResolve()
    {
        Property(typeof(Mission), "AttackerTeam", typeof(Team));
        Property(typeof(Mission), "DefenderTeam", typeof(Team));
        Property(typeof(Mission), "DeploymentPlan", typeof(IMissionDeploymentPlan));
        Property(typeof(Mission), "MainAgent", typeof(Agent));
        Property(typeof(Mission), "IsDeploymentFinished", typeof(bool));
        Property(typeof(Mission), "CurrentTime", typeof(float));
        Field(typeof(Mission), "IsFriendlyMission", typeof(bool));
        Assert.IsFalse(typeof(Mission).GetField("IsFriendlyMission", AnyMember)!.IsInitOnly, "the launcher assigns IsFriendlyMission");
        Method(typeof(Mission), "OnInitialSpawnCompleted", typeof(void), typeof(BattleSideEnum));
        Method(typeof(Mission), "KillAgentCheat", typeof(void), typeof(Agent));
        Method(typeof(Mission), "SetPlayerCanTakeControlOfAnotherAgentWhenDead", typeof(void));
        Method(typeof(Mission), "SetFormationPositioningFromDeploymentPlan", typeof(void), typeof(Formation), typeof(bool));
        Method(typeof(Mission), "GetFormationSpawnFrame", typeof(void), typeof(Team), typeof(FormationClass), typeof(bool),
            typeof(WorldPosition).MakeByRefType(), typeof(Vec2).MakeByRefType(), typeof(bool));
        Method(typeof(Mission), "GetAgentBuildDataToSpawnTroop", typeof(AgentBuildData), typeof(IAgentOriginBase), typeof(bool), typeof(bool),
            typeof(bool), typeof(bool), typeof(int), typeof(int), typeof(Vec3?), typeof(Vec2?), typeof(ItemObject), typeof(FormationClass), typeof(bool));
        Method(typeof(Mission), "SpawnTroopWithAgentBuildData", typeof(Agent), typeof(AgentBuildData), typeof(bool), typeof(bool), typeof(string));
        Assert.IsNotNull(typeof(Mission).GetMethod("GetMissionBehavior", AnyMember), "Mission.GetMissionBehavior<T> no longer resolves");
        Property(typeof(Mission), "ActiveMissionObjects", typeof(MBReadOnlyList<MissionObject>));
        Property(typeof(Mission), "MissionObjects", typeof(MBReadOnlyList<MissionObject>));
        Property(typeof(Mission), "Scene", typeof(Scene));
        Method(typeof(Scene), "GetNavigationMeshForPosition", typeof(UIntPtr), typeof(Vec3).MakeByRefType());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheLauncherOpenNew_StillResolves()
    {
        var open = Method(typeof(MissionState), "OpenNew", typeof(Mission), typeof(string), typeof(MissionInitializerRecord),
            typeof(InitializeMissionBehaviorsDelegate), typeof(bool), typeof(bool));
        Assert.IsTrue(open.IsStatic, "the launcher calls MissionState.OpenNew without an instance");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheTeamAndFormationMembers_StillResolve()
    {
        Property(typeof(Team), "ActiveAgents", typeof(MBReadOnlyList<Agent>));
        Property(typeof(Team), "TeamAI", typeof(TeamAIComponent));
        Property(typeof(Team), "Banner", typeof(Banner));
        Property(typeof(Team), "Side", typeof(BattleSideEnum));
        Property(typeof(Team), "IsPlayerGeneral", typeof(bool));
        Property(typeof(Team), "FormationsIncludingEmpty", typeof(MBList<Formation>));
        Property(typeof(Team), "PlayerOrderController", typeof(OrderController));
        var general = Property(typeof(Team), "GeneralAgent", typeof(Agent));
        Assert.IsNotNull(general.GetSetMethod(), "Team.GeneralAgent lost its public setter");
        Method(typeof(Team), "GetFormation", typeof(Formation), typeof(FormationClass));
        Method(typeof(Team), "DelegateCommandToAI", typeof(void));
        Method(typeof(Team), "AddTacticOption", typeof(void), typeof(TacticComponent));
        Method(typeof(Team), "ClearTacticOptions", typeof(void));
        Method(typeof(TeamAIComponent), "ResetTactic", typeof(void), typeof(bool));
        Field(typeof(OrderController), "Owner", typeof(Agent));
        var owner = Property(typeof(Formation), "PlayerOwner", typeof(Agent));
        Assert.IsNotNull(owner.GetSetMethod(), "Formation.PlayerOwner lost its public setter");
        Method(typeof(Formation), "BeginSpawn", typeof(void), typeof(int), typeof(bool));
        Method(typeof(Formation), "EndSpawn", typeof(void));
        // SetMusterFrame: the formation's order position, and the WorldPosition built from a recorded point.
        Method(typeof(Formation), "SetPositioning", typeof(void), typeof(WorldPosition?), typeof(Vec2?), typeof(int?));
        Assert.IsNotNull(typeof(WorldPosition).GetConstructor(new[] { typeof(Scene), typeof(Vec3) }), "WorldPosition(Scene, Vec3) no longer resolves");
        Assert.IsNotNull(typeof(Vec2).GetConstructor(new[] { typeof(float), typeof(float) }), "Vec2(float, float) no longer resolves");
        Method(typeof(DefaultMissionDeploymentPlan), "AddTroops", typeof(void), typeof(Team), typeof(FormationClass), typeof(int), typeof(int), typeof(bool));
        Method(typeof(DefaultMissionDeploymentPlan), "MakeDefaultDeploymentPlans", typeof(void));
        Assert.IsNotNull(typeof(TacticBreachWalls).GetConstructor(new[] { typeof(Team) }));
        Assert.IsNotNull(typeof(TacticCharge).GetConstructor(new[] { typeof(Team) }));
        Assert.IsNotNull(typeof(TacticDefendCastle).GetConstructor(new[] { typeof(Team) }));
        Assert.IsTrue(typeof(TeamAIComponent).IsAssignableFrom(typeof(TeamAISiegeAttacker)), "the boot check reads the siege team AI types");
        Assert.IsTrue(typeof(TeamAIComponent).IsAssignableFrom(typeof(TeamAISiegeDefender)));
        Property(typeof(TeamAISiegeComponent), "SiegeLanes", typeof(System.Collections.Generic.List<SiegeLane>));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheAgentMembers_StillResolve()
    {
        Property(typeof(Agent), "Position", typeof(Vec3));
        Property(typeof(Agent), "Team", typeof(Team));
        Property(typeof(Agent), "Mission", typeof(Mission));
        Property(typeof(Agent), "IsHuman", typeof(bool));
        Assert.IsNotNull(Property(typeof(Agent), "Health", typeof(float)).GetSetMethod(), "Agent.Health lost its public setter");
        Property(typeof(Agent), "HealthLimit", typeof(float));
        Property(typeof(Agent), "IsRunningAway", typeof(bool));
        Property(typeof(Agent), "Character", typeof(BasicCharacterObject));
        var controller = Property(typeof(Agent), "Controller", typeof(AgentControllerType));
        Assert.IsNotNull(controller.GetSetMethod(), "Agent.Controller lost its public setter");
        Method(typeof(Agent), "IsActive", typeof(bool));
        Method(typeof(AgentBuildData), "IsReinforcement", typeof(AgentBuildData), typeof(bool));
        Method(typeof(AgentBuildData), "Banner", typeof(AgentBuildData), typeof(Banner));
        Method(typeof(AgentBuildData), "SpawnsIntoOwnFormation", typeof(AgentBuildData), typeof(bool));
        Method(typeof(BannerlordConfig), "GetRealBattleSizeForSiege", typeof(int));
        Assert.AreEqual(1, Enum.GetValues(typeof(AgentControllerType)).Cast<AgentControllerType>().Count(c => c == AgentControllerType.AI));
    }

    // The sally (Slice 10, spike S9): the teleport, the fade of the old body, the mounted spawn at a given place, the boundary test, and the callbacks the logic forwards.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheSallyMembers_StillResolve()
    {
        Method(typeof(Agent), "TeleportToPosition", typeof(void), typeof(Vec3));
        Method(typeof(Agent), "FadeOut", typeof(void), typeof(bool), typeof(bool));
        Assert.IsNotNull(Property(typeof(Agent), "LookDirection", typeof(Vec3)).GetSetMethod(), "Agent.LookDirection lost its public setter");
        Property(typeof(Agent), "MountAgent", typeof(Agent));
        Property(typeof(Agent), "RiderAgent", typeof(Agent));
        Property(typeof(Agent), "HasMount", typeof(bool));
        Property(typeof(Agent), "State", typeof(AgentState));
        Property(typeof(Agent), "Formation", typeof(Formation));
        Method(typeof(Mission), "IsPositionInsideBoundaries", typeof(bool), typeof(Vec2));
        Method(typeof(AgentBuildData), "InitialPosition", typeof(AgentBuildData), typeof(Vec3).MakeByRefType());
        Method(typeof(AgentBuildData), "InitialDirection", typeof(AgentBuildData), typeof(Vec2).MakeByRefType());
        Method(typeof(AgentBuildData), "NoHorses", typeof(AgentBuildData), typeof(bool));
        Method(typeof(AgentBuildData), "Equipment", typeof(AgentBuildData), typeof(Equipment));
        Property(typeof(Formation), "OrderGroundPosition", typeof(Vec3));
        Property(typeof(Formation), "OrderPositionIsValid", typeof(bool));
        Property(typeof(CastleGate), "IsGateOpen", typeof(bool));
        Property(typeof(CastleGate), "IsDestroyed", typeof(bool));
        Method(typeof(MissionBehavior), "OnAgentDeleted", typeof(void), typeof(Agent));
        Assert.IsTrue(typeof(MissionBehavior).GetMethod("OnAgentDeleted", AnyMember)!.IsVirtual, "the logic overrides OnAgentDeleted");
    }

    // The endless scale (Slice 2): the health write after spawn, the character maximum it is read against, the blow the damage factor scales,
    // and the damage model seam it rides.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheEndlessScaleMembers_StillResolve()
    {
        foreach (var name in new[] { "BaseHealthLimit", "HealthLimit", "Health" })
            Assert.IsNotNull(Property(typeof(Agent), name, typeof(float)).GetSetMethod(), $"Agent.{name} lost its public setter");
        Method(typeof(BasicCharacterObject), "MaxHitPoints", typeof(int));
        Property(typeof(Team), "ActiveAgents", typeof(MBReadOnlyList<Agent>));
        Field(typeof(AttackInformation), "AttackerAgent", typeof(Agent));
        Field(typeof(AttackInformation), "IsAttackerAgentMount", typeof(bool));
        Method(typeof(TaleWorlds.MountAndBlade.ComponentInterfaces.AgentApplyDamageModel), "ApplyDamageAmplifications", typeof(float),
            typeof(AttackInformation).MakeByRefType(), typeof(AttackCollisionData).MakeByRefType(), typeof(float));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheSiegeEngineMembers_StillResolve()
    {
        Method(typeof(DestructableComponent), "TriggerOnHit", typeof(void), typeof(Agent), typeof(int), typeof(Vec3), typeof(Vec3),
            typeof(MissionWeapon).MakeByRefType(), typeof(int), typeof(ScriptComponentBehavior));
        Property(typeof(DestructableComponent), "HitPoint", typeof(float));
        Field(typeof(DestructableComponent), "MaxHitPoint", typeof(float));
        Method(typeof(DestructableComponent), "Reset", typeof(void));
        Method(typeof(SiegeWeapon), "SetForcedUse", typeof(void), typeof(bool));
        Method(typeof(SiegeWeapon), "SetDisabledSynched", typeof(void));
        Method(typeof(SiegeWeapon), "GetSiegeEngineType", typeof(SiegeEngineType));
        Property(typeof(CastleGate), "IsDestroyed", typeof(bool));
        Property(typeof(CastleGate), "IsGateOpen", typeof(bool));
        Method(typeof(DeploymentPoint), "SetDisabledSynched", typeof(void));
        Assert.IsNotNull(typeof(SiegeWeaponAutoDeployer).GetConstructors().SingleOrDefault(c => c.GetParameters().Length == 2), "SiegeWeaponAutoDeployer(points, controller)");
        Method(typeof(SiegeWeaponAutoDeployer), "DeployAll", typeof(void), typeof(BattleSideEnum));
        Method(typeof(MissionSiegeEnginesLogic), "GetSiegeWeaponsController", typeof(TaleWorlds.MountAndBlade.Missions.IMissionSiegeWeaponsController), typeof(BattleSideEnum));
        // ScriptComponentBehavior.GameEntity is a WeakGameEntity, and that is the type the adapter calls these on.
        Property(typeof(ScriptComponentBehavior), "GameEntity", typeof(WeakGameEntity));
        Method(typeof(WeakGameEntity), "IsVisibleIncludeParents", typeof(bool));
        Method(typeof(WeakGameEntity), "HasTag", typeof(bool), typeof(string));
        Property(typeof(WeakGameEntity), "GlobalPosition", typeof(Vec3));
        Assert.IsNotNull(typeof(WeakGameEntity).GetMethod("GetFirstScriptOfType", AnyMember), "WeakGameEntity.GetFirstScriptOfType<T> no longer resolves");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheMissionLogicHooks_StillResolve()
    {
        Method(typeof(MissionLogic), "OnRetreatMission", typeof(void));
        Method(typeof(MissionBehavior), "OnAgentFleeing", typeof(void), typeof(Agent));
        Method(typeof(MissionBehavior), "OnRemoveBehavior", typeof(void));
        Method(typeof(MissionBehavior), "OnEndMission", typeof(void));
        Method(typeof(MissionBehavior), "OnAgentRemoved", typeof(void), typeof(Agent), typeof(Agent), typeof(AgentState), typeof(KillingBlow));
        Method(typeof(MissionBehavior), "AfterStart", typeof(void));
        Assert.AreEqual(1, typeof(IBattlePowerCalculationLogic).GetMethods().Count(m => m.Name == "GetTotalTeamPower"), "the team power interface changed");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheAffixStatMembers_StillResolve()
    {
        // The stat and armour hooks and the read-back (LongDefeatStatHooks, LongDefeatEffectsAdapter.ReadStats).
        Property(typeof(Agent), "RiderAgent", typeof(Agent));
        Property(typeof(Agent), "IsMount", typeof(bool));
        Property(typeof(Agent), "IsHuman", typeof(bool));
        Property(typeof(Agent), "Mission", typeof(Mission));
        Property(typeof(Agent), "Team", typeof(Team));
        Property(typeof(Agent), "SpawnEquipment", typeof(Equipment));
        Method(typeof(Agent), "GetAgentDrivenPropertyValue", typeof(float), typeof(DrivenProperty));
        Method(typeof(Agent), "UpdateAgentProperties", typeof(void));
        Method(typeof(AgentDrivenProperties), "GetStat", typeof(float), typeof(DrivenProperty));
        Property(typeof(Agent), "AgentDrivenProperties", typeof(AgentDrivenProperties));
        Property(typeof(AgentDrivenProperties), "ArmorEncumbrance", typeof(float));
        Property(typeof(AgentBuildData), "AgentTeam", typeof(Team));
        Method(typeof(AgentStatCalculateModel), "UpdateAgentStats", typeof(void), typeof(Agent), typeof(AgentDrivenProperties));
        Method(typeof(AgentStatCalculateModel), "InitializeAgentStats", typeof(void), typeof(Agent), typeof(Equipment), typeof(AgentDrivenProperties), typeof(AgentBuildData));
        Property(typeof(MissionGameModels), "AgentStatCalculateModel", typeof(AgentStatCalculateModel));
        foreach (var armour in new[] { "GetHeadArmorSum", "GetHumanBodyArmorSum", "GetLegArmorSum", "GetArmArmorSum" })
            Method(typeof(Equipment), armour, typeof(float));
        foreach (var stat in new[]
                 {
                     "SwingSpeedMultiplier", "AIAttackOnDecideChance", "MaxSpeedMultiplier", "CombatMaxSpeedMultiplier", "TopSpeedReachDuration", "AiShooterError",
                     "ThrustOrRangedReadySpeedMultiplier", "ReloadSpeed", "MissileSpeedMultiplier", "AIBlockOnDecideAbility", "AIParryOnDecideAbility",
                     "ArmorHead", "ArmorTorso", "ArmorLegs", "ArmorArms", "MountSpeed",
                 })
            Assert.IsTrue(Enum.IsDefined(typeof(DrivenProperty), stat), "DrivenProperty." + stat + " no longer resolves");
        foreach (var armour in new[] { "ArmorHead", "ArmorTorso", "ArmorLegs", "ArmorArms" })
            Property(typeof(AgentDrivenProperties), armour, typeof(float));
        // The hook tests build bare agents by writing these private fields.
        Assert.IsNotNull(typeof(Agent).GetField("_flagsPointer", AnyMember), "Agent._flagsPointer: the bare-agent fixture no longer matches the engine");
        Assert.IsNotNull(typeof(Agent).GetField("_cachedRiderAgent", AnyMember), "Agent._cachedRiderAgent: the bare-agent fixture no longer matches the engine");
    }

    // The class kit (Slice 5): the override the engine reads first (Mission.cs:4108), the no-horse flag, the slot names and the fit test the adapter calls,
    // and the equipment read back from the spawned lord.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheClassKitMembers_StillResolve()
    {
        Method(typeof(AgentBuildData), "Equipment", typeof(AgentBuildData), typeof(Equipment));
        Method(typeof(AgentBuildData), "NoHorses", typeof(AgentBuildData), typeof(bool));
        Property(typeof(AgentBuildData), "AgentOverridenSpawnEquipment", typeof(Equipment));
        Property(typeof(BasicCharacterObject), "FirstBattleEquipment", typeof(Equipment));
        Method(typeof(Equipment), "Clone", typeof(Equipment), typeof(bool));
        var index = Method(typeof(Equipment), "GetEquipmentIndexFromOldEquipmentIndexName", typeof(EquipmentIndex), typeof(string));
        Assert.IsTrue(index.IsStatic);
        Method(typeof(Equipment), "IsItemFitsToSlot", typeof(bool), typeof(EquipmentIndex), typeof(ItemObject));
        Assert.IsNotNull(typeof(Equipment).GetProperty("Item", new[] { typeof(EquipmentIndex) }), "Equipment[EquipmentIndex] no longer resolves");
        Property(typeof(EquipmentElement), "Item", typeof(ItemObject));
        Assert.IsNotNull(typeof(EquipmentElement).GetConstructor(new[] { typeof(ItemObject), typeof(ItemModifier), typeof(ItemObject), typeof(bool) }), "EquipmentElement(ItemObject, ItemModifier, ItemObject, bool) no longer resolves");
        Property(typeof(ItemObject), "HorseComponent", typeof(HorseComponent));
        Property(typeof(HorseComponent), "IsRideable", typeof(bool));
        Property(typeof(Agent), "HasMount", typeof(bool));
        Property(typeof(Agent), "SpawnEquipment", typeof(Equipment));
        Property(typeof(Game), "PlayerTroop", typeof(BasicCharacterObject));
        foreach (var slot in new[] { "Weapon0", "Weapon1", "Weapon2", "Weapon3", "Head", "Body", "Leg", "Gloves", "Cape", "Horse", "HorseHarness" })
            Assert.IsTrue(Enum.IsDefined(typeof(EquipmentIndex), slot), "EquipmentIndex." + slot + " no longer resolves");
        // The name mapping the adapter relies on: Item0 to Item3 become Weapon0 to Weapon3, every other kit slot name parses as it is.
        Assert.AreEqual(EquipmentIndex.Weapon0, Equipment.GetEquipmentIndexFromOldEquipmentIndexName("Item0"));
        Assert.AreEqual(EquipmentIndex.Weapon3, Equipment.GetEquipmentIndexFromOldEquipmentIndexName("Item3"));
        Assert.AreEqual(EquipmentIndex.HorseHarness, Equipment.GetEquipmentIndexFromOldEquipmentIndexName("HorseHarness"));
        Assert.AreEqual(EquipmentIndex.Head, Equipment.GetEquipmentIndexFromOldEquipmentIndexName("Head"));
    }

    // The troop and class boons (Slice 6): the character flags that name a group, the main agent, the mount a refresh follows and whose speed is read back, the victim side of a blow, and the damage seam they scale.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheTargetBoonMembers_StillResolve()
    {
        Property(typeof(BasicCharacterObject), "IsMounted", typeof(bool));
        Property(typeof(BasicCharacterObject), "IsRanged", typeof(bool));
        Property(typeof(Agent), "MountAgent", typeof(Agent));
        Property(typeof(AgentDrivenProperties), "MountSpeed", typeof(float));
        Property(typeof(Mission), "MainAgent", typeof(Agent));
        Field(typeof(AttackInformation), "VictimAgent", typeof(Agent));
        Field(typeof(AttackInformation), "IsVictimAgentMount", typeof(bool));
        Method(typeof(TaleWorlds.MountAndBlade.ComponentInterfaces.AgentApplyDamageModel), "ApplyDamageReductions", typeof(float),
            typeof(AttackInformation).MakeByRefType(), typeof(AttackCollisionData).MakeByRefType(), typeof(float));
        // The bare-agent fixtures of LongDefeatTargetHooksTests write these private fields.
        Assert.IsNotNull(typeof(Agent).GetField("_character", AnyMember), "Agent._character: the bare-agent fixture no longer matches the engine");
        Assert.IsNotNull(typeof(Mission).GetField("_mainAgent", AnyMember), "Mission._mainAgent: the bare-mission fixture no longer matches the engine");
        Assert.IsNotNull(typeof(BasicCharacterObject).GetField("_isMounted", AnyMember), "BasicCharacterObject._isMounted: the bare-character fixture no longer matches the engine");
        Assert.IsNotNull(typeof(BasicCharacterObject).GetField("_isRanged", AnyMember), "BasicCharacterObject._isRanged: the bare-character fixture no longer matches the engine");
    }

    // The commanders (Slice 7): a hero spawns through the same call as a soldier and is marked unique by the spawn (Mission.cs:4517-4523), the refresh a war cry needs is the one the boons use, and the
    // log line of risk 1 reads his flags, formation and speed.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheCommanderMembers_StillResolve()
    {
        Method(typeof(Mission), "SpawnTroopWithAgentBuildData", typeof(Agent), typeof(AgentBuildData), typeof(bool), typeof(bool), typeof(string));
        Method(typeof(Agent), "UpdateAgentProperties", typeof(void));
        Method(typeof(Agent), "GetAgentFlags", typeof(AgentFlag));
        Assert.IsTrue(Enum.IsDefined(typeof(AgentFlag), nameof(AgentFlag.IsUnique)), "AgentFlag.IsUnique: the engine marks a hero unique at his spawn");
        Property(typeof(Agent), "Formation", typeof(Formation));
        Property(typeof(Agent), "MovementVelocity", typeof(Vec2));
        Property(typeof(BasicCharacterObject), "IsHero", typeof(bool));
        Property(typeof(Agent), "BaseHealthLimit", typeof(float));
        Method(typeof(BasicCharacterObject), "MaxHitPoints", typeof(int));
    }

    // The wrapper's replay (tools/run_long_defeat.py, troop_groups) reads each troop's default_group from fords_troops.xml and applies this table, because the report names no troop group.
    // Each class once: NumberOfDefaultFormations is Skirmisher and NumberOfRegularFormations is General, which Distinct merges.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheFormationClassFlags_AreTheTableTheWrapperReplayAssumes()
    {
        var table = string.Join(",", Enum.GetValues(typeof(FormationClass)).Cast<FormationClass>().Where(c => c < FormationClass.NumberOfAllFormations).Distinct()
            .Select(c => $"{c}:{(c.IsMounted() ? "M" : "")}{(c.IsRanged() ? "R" : "")}"));

        Assert.AreEqual("Infantry:,Ranged:R,Cavalry:M,HorseArcher:MR,Skirmisher:R,HeavyInfantry:,LightCavalry:R,HeavyCavalry:M,NumberOfRegularFormations:,Bodyguard:", table,
            "tools/run_long_defeat.py MOUNTED_GROUPS and RANGED_GROUPS follow this table (the engine's flags, read once from a troop's default_group)");
    }

    // The Hammer of the Underworld and Winged Terror (phase2-plan.md Slice 8): the engines' hit points the hammer writes and the morale the pulse reads and lowers.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheHammerAndTerrorMembers_StillResolve()
    {
        Field(typeof(DestructableComponent), "MaxHitPoint", typeof(float));
        Assert.IsFalse(typeof(DestructableComponent).GetField("MaxHitPoint", AnyMember)!.IsInitOnly, "the hammer assigns MaxHitPoint");
        Assert.IsNotNull(Property(typeof(DestructableComponent), "HitPoint", typeof(float)).GetSetMethod(), "DestructableComponent.HitPoint lost its public setter");
        Property(typeof(DestructableComponent), "IsDestroyed", typeof(bool));
        Property(typeof(UsableMachine), "IsDestroyed", typeof(bool));
        Property(typeof(UsableMachine), "IsDisabled", typeof(bool));
        Method(typeof(SiegeWeapon), "GetSiegeEngineType", typeof(SiegeEngineType));
        Property(typeof(SiegeEngineType), "BaseHitPoints", typeof(int));
        Assert.IsTrue(typeof(SiegeWeapon).IsAssignableFrom(typeof(BatteringRam)) && typeof(SiegeWeapon).IsAssignableFrom(typeof(SiegeTower)), "the hammer treats a ram and a tower as one kind of engine");
        Assert.IsTrue(typeof(UsableMachine).IsAssignableFrom(typeof(CastleGate)), "the gate's state is UsableMachine.IsDestroyed");
        Method(typeof(AgentComponentExtensions), "GetMorale", typeof(float), typeof(Agent));
        Method(typeof(AgentComponentExtensions), "ChangeMorale", typeof(void), typeof(Agent), typeof(float));
        Property(typeof(Agent), "Position", typeof(Vec3));
    }

    // The gates (phase3-plan.md Slice 5, D17 and D18): the vanilla reset the repair calls by reflection, the closing call after it, the destructible's public hit point fields the level writes, the damage-state
    // string whose length is the state count, the gate's state and the engine's own archer-position count the boot line reads (A23).
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheGateMembers_StillResolve()
    {
        // The reflection site: found by name on CastleGate itself, so an override that moves up the hierarchy would make AccessTools find another method.
        var reset = typeof(CastleGate).GetMethod("OnMissionReset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
        Assert.IsNotNull(reset, "CastleGate.OnMissionReset() is no longer declared on CastleGate: a gate repair cannot reset the door (R10)");
        Assert.AreEqual(typeof(void), reset!.ReturnType);
        Assert.IsTrue(reset.IsVirtual, "the repair invokes the override the engine itself runs on a mission reset");
        Method(typeof(CastleGate), "SetAutoOpenState", typeof(void), typeof(bool));
        Property(typeof(CastleGate), "State", typeof(CastleGate.GateState));
        Assert.IsTrue(Enum.IsDefined(typeof(CastleGate.GateState), "Closed"), "CastleGate.GateState.Closed");
        Property(typeof(CastleGate), "IsDisabled", typeof(bool));
        Field(typeof(DestructableComponent), "MaxHitPoint", typeof(float));
        Assert.IsFalse(typeof(DestructableComponent).GetField("MaxHitPoint", AnyMember)!.IsInitOnly, "the level writes MaxHitPoint");
        Field(typeof(DestructableComponent), "DestructionStates", typeof(string));
        Assert.IsNotNull(Property(typeof(DestructableComponent), "HitPoint", typeof(float)).GetSetMethod(), "DestructableComponent.HitPoint lost its public setter");
        Property(typeof(DestructableComponent), "IsDestroyed", typeof(bool));
        Method(typeof(DestructableComponent), "Reset", typeof(void));
        Property(typeof(TeamAISiegeDefender), "ArcherPositions", typeof(System.Collections.Generic.List<ArcherPosition>));
        Property(typeof(Team), "FormationsIncludingEmpty", typeof(MBList<Formation>));
        Property(typeof(Formation), "CountOfUnits", typeof(int));
        Field(typeof(Formation), "FormationIndex", typeof(FormationClass));
        Method(typeof(Mission), "IsPositionInsideBoundaries", typeof(bool), typeof(Vec2));
    }

    // The Elven Volley (phase3-plan.md Slice 6, section 4 "Binding gate"): the scripted missile with all ten parameters (forcedMissileIndex included), the two hit callbacks, the eye and chest points, the ray
    // cast the aim and the clearance test use, the missile list the cancelled-hit lookup walks, and the agent flags the owner rule reads.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheVolleyMembers_StillResolve()
    {
        Method(typeof(Mission), "AddCustomMissile", typeof(Mission.Missile), typeof(Agent), typeof(MissionWeapon), typeof(Vec3), typeof(Vec3), typeof(Mat3), typeof(float), typeof(float), typeof(bool),
            typeof(MissionObject), typeof(int));
        Method(typeof(MissionBehavior), "OnMissileHit", typeof(void), typeof(Agent), typeof(Agent), typeof(bool), typeof(AttackCollisionData));
        Method(typeof(MissionBehavior), "OnAgentHit", typeof(void), typeof(Agent), typeof(Agent), typeof(MissionWeapon).MakeByRefType(), typeof(Blow).MakeByRefType(), typeof(AttackCollisionData).MakeByRefType());
        Method(typeof(Agent), "GetEyeGlobalPosition", typeof(Vec3));
        Method(typeof(Agent), "GetChestGlobalPosition", typeof(Vec3));
        Property(typeof(Agent), "LookDirection", typeof(Vec3));
        Property(typeof(Agent), "Velocity", typeof(Vec3));
        Property(typeof(Agent), "IsAIControlled", typeof(bool));
        Property(typeof(Agent), "IsMainAgent", typeof(bool));
        Property(typeof(Agent), "IsRangedCached", typeof(bool));
        Property(typeof(Agent), "IsRunningAway", typeof(bool));
        Method(typeof(Scene), "RayCastForClosestEntityOrTerrain", typeof(bool), typeof(Vec3), typeof(Vec3), typeof(float).MakeByRefType(), typeof(Vec3).MakeByRefType(), typeof(float), typeof(BodyFlags));
        Property(typeof(Mission), "MissilesList", typeof(MBReadOnlyList<Mission.Missile>));
        Property(typeof(Mission.Missile), "Weapon", typeof(MissionWeapon));
        Property(typeof(Mission.Missile), "Index", typeof(int));
        var all = typeof(Mission).GetProperty("AllAgents", AnyMember);
        Assert.IsNotNull(all, "Mission.AllAgents no longer resolves");
        Assert.IsNotNull(all!.PropertyType.GetProperty("Count"), "Mission.AllAgents lost its Count: the volley's agent count reads it");
        Property(typeof(Blow), "IsMissile", typeof(bool));
        Property(typeof(AttackCollisionData), "AttackBlockedWithShield", typeof(bool));
        Property(typeof(AttackCollisionData), "AffectorWeaponSlotOrMissileIndex", typeof(int));
        Property(typeof(AttackCollisionData), "MissileVelocity", typeof(Vec3));
        Property(typeof(AttackCollisionData), "MissileStartingBaseSpeed", typeof(float));
        Method(typeof(AgentStatCalculateModel), "GetWeaponDamageMultiplier", typeof(float), typeof(Agent), typeof(WeaponComponentData));
        Method(typeof(Mat3), "Orthonormalize", typeof(void));
        Property(typeof(MissionWeapon), "Item", typeof(ItemObject));
    }

    // Morgul Blades and the kill owner (phase2-plan.md Slice 9): the hit callback with the engine's own isBlocked and damagedHp, and the blow the two members allowed to kill build.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheBladesAndKillOwnerMembers_StillResolve()
    {
        Method(typeof(MissionBehavior), "OnScoreHit", typeof(void), typeof(Agent), typeof(Agent), typeof(WeaponComponentData), typeof(bool), typeof(bool),
            typeof(Blow).MakeByRefType(), typeof(AttackCollisionData).MakeByRefType(), typeof(float), typeof(float), typeof(float));
        Method(typeof(Agent), "Die", typeof(void), typeof(Blow), typeof(Agent.KillInfo));
        Method(typeof(Agent), "RegisterBlow", typeof(void), typeof(Blow), typeof(AttackCollisionData).MakeByRefType());
        Method(typeof(Agent), "GetEyeGlobalHeight", typeof(float));
        Property(typeof(Agent), "Index", typeof(int));
        Property(typeof(Agent), "IsMount", typeof(bool));
        Property(typeof(Agent), "RiderAgent", typeof(Agent));
        Property(typeof(Agent), "LookDirection", typeof(Vec3));
        Property(typeof(Agent), "Velocity", typeof(Vec3));
        Property(typeof(Agent), "CurrentMortalityState", typeof(Agent.MortalityState));
        Assert.IsNotNull(Property(typeof(Agent), "Health", typeof(float)).GetSetMethod(), "Agent.Health lost its public setter");
        Assert.IsNotNull(typeof(Blow).GetConstructor(new[] { typeof(int) }), "Blow(int ownerId) no longer resolves");
        foreach (var (name, type) in new[]
        {
            ("DamageType", typeof(DamageTypes)), ("BoneIndex", typeof(sbyte)), ("GlobalPosition", typeof(Vec3)), ("BaseMagnitude", typeof(float)), ("InflictedDamage", typeof(int)),
            ("SwingDirection", typeof(Vec3)), ("Direction", typeof(Vec3)), ("DamageCalculated", typeof(bool)), ("WeaponRecord", typeof(BlowWeaponRecord)),
        })
            Field(typeof(Blow), name, type);
        Method(typeof(BlowWeaponRecord), "FillAsMeleeBlow", typeof(void), typeof(ItemObject), typeof(WeaponComponentData), typeof(int), typeof(sbyte));
        Assert.IsNotNull(typeof(AttackCollisionData).GetMethod("GetAttackCollisionDataForDebugPurpose", BindingFlags.Public | BindingFlags.Static), "AttackCollisionData.GetAttackCollisionDataForDebugPurpose no longer resolves");
        Property(typeof(Monster), "HeadLookDirectionBoneIndex", typeof(sbyte));
        Property(typeof(Monster), "MainHandItemBoneIndex", typeof(sbyte));
    }

    private static PropertyInfo Property(Type type, string name, Type expected)
    {
        var property = type.GetProperty(name, AnyMember);
        Assert.IsNotNull(property, $"{type.FullName}.{name} no longer resolves");
        Assert.AreEqual(expected, property!.PropertyType, $"{type.Name}.{name} changed type");
        Assert.IsNotNull(property.GetGetMethod(true), $"{type.Name}.{name} lost its getter");
        return property;
    }

    private static void Field(Type type, string name, Type expected)
    {
        var field = type.GetField(name, AnyMember);
        Assert.IsNotNull(field, $"{type.FullName}.{name} no longer resolves");
        Assert.AreEqual(expected, field!.FieldType, $"{type.Name}.{name} changed type");
    }

    private static MethodInfo Method(Type type, string name, Type returns, params Type[] parameters)
    {
        var method = type.GetMethod(name, AnyMember, null, parameters, null);
        Assert.IsNotNull(method, $"{type.FullName}.{name}({string.Join(", ", parameters.Select(p => p.Name))}) no longer resolves");
        Assert.AreEqual(returns, method!.ReturnType, $"{type.Name}.{name} changed its return type");
        return method;
    }
}
