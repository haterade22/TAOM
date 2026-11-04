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
        Property(typeof(Agent), "IsRunningAway", typeof(bool));
        Property(typeof(Agent), "Character", typeof(BasicCharacterObject));
        var controller = Property(typeof(Agent), "Controller", typeof(AgentControllerType));
        Assert.IsNotNull(controller.GetSetMethod(), "Agent.Controller lost its public setter");
        Method(typeof(Agent), "IsActive", typeof(bool));
        Method(typeof(AgentBuildData), "IsReinforcement", typeof(AgentBuildData), typeof(bool));
        Method(typeof(AgentBuildData), "Banner", typeof(AgentBuildData), typeof(Banner));
        Method(typeof(BannerlordConfig), "GetRealBattleSizeForSiege", typeof(int));
        Assert.AreEqual(1, Enum.GetValues(typeof(AgentControllerType)).Cast<AgentControllerType>().Count(c => c == AgentControllerType.AI));
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
