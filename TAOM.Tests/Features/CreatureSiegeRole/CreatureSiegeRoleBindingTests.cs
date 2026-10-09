using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.CreatureSiegeRole.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// The creature siege role against the installed engine (v1.5.4). Every engine member the hooks, both adapters, the mission
/// behavior and the four model overrides reference is pinned here, because a member that stops resolving fails when its method is
/// first compiled, before any try inside it can run (lessons/harmony-il.md "A patch's own try/catch cannot survive a JIT-time
/// member-resolution failure"), and the role then sits inert or throws every tick with no line saying why. The enum values the
/// compiled code folds into literals are read at run time, and the protected <c>DynamicNavmeshIdStart</c> field the tower read
/// binds through reflection is checked by type. Model types are named by string: a <c>typeof</c> in a DataRow of a type deriving
/// from a SandBox class is not discovered, because the module assemblies load after test discovery.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CreatureSiegeRoleBindingTests
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    // --- helpers ------------------------------------------------------------------------------------------------------------

    private static void Prop(Type type, string name, Type expected)
    {
        var property = type.GetProperty(name, AnyMember);
        Assert.IsNotNull(property, $"{type.FullName}.{name} no longer resolves");
        Assert.AreEqual(expected, property!.PropertyType, $"{type.Name}.{name} changed type");
        Assert.IsNotNull(property.GetGetMethod(true), $"{type.Name}.{name} lost its getter");
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

    private static void EnumHas(Type enumType, params string[] names)
    {
        Assert.IsTrue(enumType.IsEnum, $"{enumType.Name} is no longer an enum");
        foreach (var name in names)
            Assert.IsTrue(Enum.GetNames(enumType).Contains(name), $"{enumType.Name}.{name} no longer exists");
    }

    private static int Value(Type enumType, string name) => Convert.ToInt32(Enum.Parse(enumType, name));

    // --- the agent primitives -----------------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheAgentPrimitives_StillResolveWithTheirShapes()
    {
        Prop(typeof(Agent), "IsAIControlled", typeof(bool));
        Prop(typeof(Agent), "IsRunningAway", typeof(bool));
        Prop(typeof(Agent), "IsInLadderQueue", typeof(bool));
        Prop(typeof(Agent), "Team", typeof(Team));
        Prop(typeof(Agent), "Position", typeof(Vec3));
        Prop(typeof(Agent), "Formation", typeof(Formation));
        Prop(typeof(Agent), "Character", typeof(BasicCharacterObject));
        Prop(typeof(Agent), "Mission", typeof(Mission));
        Prop(typeof(BasicCharacterObject), "Race", typeof(int));
        Prop(typeof(Team), "Side", typeof(BattleSideEnum));
        Prop(typeof(Formation), "IsAIControlled", typeof(bool));

        Method(typeof(Agent), "IsRetreating", typeof(bool));
        Method(typeof(Agent), "GetCurrentNavigationFaceId", typeof(int));
        Method(typeof(Agent), "GetWorldPosition", typeof(WorldPosition));
        Method(typeof(Agent), "GetScriptedFlags", typeof(Agent.AIScriptedFrameFlags));
        Method(typeof(Agent), "GetScriptedCombatFlags", typeof(Agent.AISpecialCombatModeFlags));
        Method(typeof(Agent), "SetAgentExcludeStateForFaceGroupId", typeof(void), typeof(int), typeof(bool));
        Method(typeof(Agent), "DisableScriptedMovement", typeof(void));
        Method(typeof(Agent), "DisableScriptedCombatMovement", typeof(void));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheScriptedMovementCalls_HaveTheParameterShapeTheAdapterPasses()
    {
        var position = Method(typeof(Agent), "SetScriptedPositionAndDirection", typeof(void),
            typeof(WorldPosition).MakeByRefType(), typeof(float), typeof(bool), typeof(Agent.AIScriptedFrameFlags));
        Assert.IsTrue(position.GetParameters()[0].ParameterType.IsByRef, "the position is passed by ref");

        var target = Method(typeof(Agent), "SetScriptedTargetEntity", typeof(void),
            typeof(WeakGameEntity), typeof(Agent.AISpecialCombatModeFlags), typeof(bool));
        Assert.AreEqual("ignoreIfAlreadyAttacking", target.GetParameters()[2].Name, "the adapter passes this flag by name");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheFormationOrderRead_StillResolves()
    {
        var method = typeof(Formation).GetMethod("GetReadonlyMovementOrderReference", AnyMember, null, Type.EmptyTypes, null);
        Assert.IsNotNull(method, "Formation.GetReadonlyMovementOrderReference() no longer resolves");
        Assert.AreEqual(typeof(MovementOrder).MakeByRefType(), method!.ReturnType, "the order is read by reference");
        Field(typeof(MovementOrder), "OrderEnum", typeof(MovementOrder.MovementOrderEnum));
    }

    // --- the scene ----------------------------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheSceneQueries_AndTheWorldPositionConstructor_StillResolve()
    {
        Method(typeof(Scene), "DoesPathExistBetweenPositions", typeof(bool), typeof(WorldPosition), typeof(WorldPosition));

        // The ground probe is a downward ray: its hit flag is how a miss is told from a height (the native height query
        // answers 0 on a miss).
        var ray = typeof(Scene).GetMethod("RayCastForClosestEntityOrTerrain", AnyMember, null,
            new[] { typeof(Vec3), typeof(Vec3), typeof(float).MakeByRefType(), typeof(Vec3).MakeByRefType(), typeof(float), typeof(BodyFlags) },
            null);
        Assert.IsNotNull(ray, "Scene.RayCastForClosestEntityOrTerrain(Vec3, Vec3, out float, out Vec3, float, BodyFlags) no longer resolves");
        Assert.AreEqual(typeof(bool), ray!.ReturnType);
        Assert.IsTrue(ray.GetParameters()[4].IsOptional, "the adapter omits the ray thickness");

        var ctor = typeof(WorldPosition).GetConstructor(new[] { typeof(Scene), typeof(UIntPtr), typeof(Vec3), typeof(bool) });
        Assert.IsNotNull(ctor, "WorldPosition(Scene, UIntPtr, Vec3, bool) no longer resolves");
    }

    // --- the mission and the siege team AI ----------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheMissionMembers_StillResolveWithTheirTypes()
    {
        Prop(typeof(Mission), "SceneName", typeof(string));
        Prop(typeof(Mission), "CurrentTime", typeof(float));
        Prop(typeof(Mission), "IsSiegeBattle", typeof(bool));
        Prop(typeof(Mission), "IsDeploymentFinished", typeof(bool));
        Prop(typeof(Mission), "Scene", typeof(Scene));
        Prop(typeof(Mission), "AttackerTeam", typeof(Team));
        Prop(typeof(Mission), "DefenderTeam", typeof(Team));
        var agents = typeof(Mission).GetProperty("Agents", AnyMember);
        Assert.IsNotNull(agents, "Mission.Agents no longer resolves");
        Assert.AreEqual("AgentReadOnlyList", agents!.PropertyType.Name, "Mission.Agents changed type");
        Assert.IsNotNull(agents.PropertyType.GetProperty("Count"), "the agent list lost Count");
        Assert.IsTrue(agents.PropertyType.GetProperties().Any(p => p.GetIndexParameters().Length == 1 && p.PropertyType == typeof(Agent)),
            "the agent list lost its Agent indexer");
        Prop(typeof(Mission), "ActiveMissionObjects", typeof(MBReadOnlyList<MissionObject>));
        Prop(typeof(Team), "TeamAI", typeof(TeamAIComponent));
        Prop(typeof(GameNetwork), "IsClientOrReplay", typeof(bool));

        var generic = typeof(Mission).GetMethods(AnyMember).Where(m => m.Name == "GetMissionBehavior" && m.IsGenericMethodDefinition);
        Assert.IsTrue(generic.Any(m => m.GetParameters().Length == 0), "Mission.GetMissionBehavior<T>() no longer resolves");
        Assert.IsTrue(typeof(SallyOutMissionController).IsClass, "SallyOutMissionController no longer resolves");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheSiegeTeamAiMembers_StillResolveWithTheirTypes()
    {
        Prop(typeof(TeamAISiegeComponent), "OuterGate", typeof(CastleGate));
        Prop(typeof(TeamAISiegeComponent), "InnerGate", typeof(CastleGate));
        Prop(typeof(TeamAISiegeComponent), "PrimarySiegeWeapons", typeof(List<IPrimarySiegeWeapon>));
        Prop(typeof(TeamAISiegeComponent), "Ladders", typeof(MBReadOnlyList<SiegeLadder>));
        Assert.IsTrue(typeof(TeamAIComponent).IsAssignableFrom(typeof(TeamAISiegeComponent)), "the adapter casts TeamAI to the siege component");
    }

    // --- the gates, the ram, the ladders and the towers ---------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheGateMembers_StillResolveWithTheirTypes()
    {
        Prop(typeof(CastleGate), "State", typeof(CastleGate.GateState));
        Prop(typeof(CastleGate), "IsDestroyed", typeof(bool));
        Prop(typeof(CastleGate), "IsDisabled", typeof(bool));
        Prop(typeof(CastleGate), "DestructionComponent", typeof(DestructableComponent));
        Prop(typeof(CastleGate), "GameEntity", typeof(WeakGameEntity));
        Prop(typeof(DestructableComponent), "HitPoint", typeof(float));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheRamLadderAndTowerMembers_StillResolveWithTheirTypes()
    {
        Prop(typeof(BatteringRam), "IsDeactivated", typeof(bool));
        Prop(typeof(BatteringRam), "IsUsed", typeof(bool));
        Prop(typeof(BatteringRam), "UserCountIncludingInStruckAction", typeof(int));
        Prop(typeof(SiegeLadder), "OnWallNavMeshId", typeof(int));
        Prop(typeof(SiegeLadder), "IsDisabled", typeof(bool));
        Prop(typeof(SiegeTower), "IsDisabled", typeof(bool));
        Method(typeof(SiegeTower), "GetGateNavMeshId", typeof(int));
        Assert.IsTrue(typeof(IPrimarySiegeWeapon).IsAssignableFrom(typeof(BatteringRam)), "the ram is no longer a primary siege weapon");
        Assert.IsTrue(typeof(IPrimarySiegeWeapon).IsAssignableFrom(typeof(SiegeTower)), "the tower is no longer a primary siege weapon");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheProtectedDynamicNavmeshIdStart_IsAnIntFieldOfMissionObject_AndBindsThroughAccessTools()
    {
        var field = typeof(MissionObject).GetField("DynamicNavmeshIdStart", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "MissionObject.DynamicNavmeshIdStart no longer resolves");
        Assert.IsTrue(field!.IsFamily, "the field is protected: the tower read depends on reflection reaching it");
        Assert.AreEqual(typeof(int), field.FieldType);

        // The very binding the mission adapter makes. Null in the adapter means every tower reads as "no navmesh".
        var reference = AccessTools.FieldRefAccess<MissionObject, int>("DynamicNavmeshIdStart");
        Assert.IsNotNull(reference);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheGameEntityMembers_StillResolve()
    {
        Prop(typeof(WeakGameEntity), "IsValid", typeof(bool));
        Prop(typeof(WeakGameEntity), "Name", typeof(string));
        Prop(typeof(WeakGameEntity), "GlobalPosition", typeof(Vec3));
        Method(typeof(WeakGameEntity), "HasTag", typeof(bool), typeof(string));
        Method(typeof(WeakGameEntity), "GetGlobalFrame", typeof(MatrixFrame));
        Method(typeof(WeakGameEntity), "IsVisibleIncludeParents", typeof(bool));
        Method(typeof(WeakGameEntity), "CollectChildrenEntitiesWithTag", typeof(List<WeakGameEntity>), typeof(string));
    }

    // --- the hooks' inputs --------------------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheHitStructures_StillCarryTheFieldsTheGateHookReads()
    {
        Field(typeof(AttackInformation), "HitObjectDestructibleComponent", typeof(DestructableComponent));
        Field(typeof(AttackInformation), "AttackerAgentCharacter", typeof(BasicCharacterObject));
        Field(typeof(AttackInformation), "IsFriendlyFire", typeof(bool));
        Field(typeof(AttackInformation), "IsAttackerAgentMount", typeof(bool));
        Prop(typeof(AttackCollisionData), "IsMissile", typeof(bool));
    }

    // --- the enum values ----------------------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheEnumMembersTheAdaptersNameAreStillThere()
    {
        EnumHas(typeof(Agent.AIScriptedFrameFlags), "None", "GoToPosition", "ConsiderRotation");
        EnumHas(typeof(Agent.AISpecialCombatModeFlags), "None", "AttackEntity");
        EnumHas(typeof(MovementOrder.MovementOrderEnum), "Charge", "ChargeToTarget", "Retreat");
        EnumHas(typeof(CastleGate.GateState), "Open");
        EnumHas(typeof(BattleSideEnum), "Attacker", "Defender");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheFlagValuesTheAdapterTests_AreSingleBitsAndDistinct()
    {
        // `(flags & GoToPosition) != 0` is only a test for one flag when the value is one bit; a renumbering to a composite
        // would make the role misread every scripted state.
        var goTo = Value(typeof(Agent.AIScriptedFrameFlags), "GoToPosition");
        var rotation = Value(typeof(Agent.AIScriptedFrameFlags), "ConsiderRotation");
        var attack = Value(typeof(Agent.AISpecialCombatModeFlags), "AttackEntity");

        foreach (var flag in new[] { goTo, rotation, attack })
            Assert.IsTrue(flag > 0 && (flag & (flag - 1)) == 0, $"flag value {flag} is not a single bit");
        Assert.AreNotEqual(goTo, rotation, "the two scripted-frame flags must stay distinct");
        Assert.AreEqual(0, Value(typeof(Agent.AIScriptedFrameFlags), "None"));
        Assert.AreEqual(0, Value(typeof(Agent.AISpecialCombatModeFlags), "None"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheSideEnumValues_TheCompiledCodeFolds_AreTheInstalledEnginesValues()
    {
        Assert.AreEqual(0, Value(typeof(BattleSideEnum), "Defender"));
        Assert.AreEqual(1, Value(typeof(BattleSideEnum), "Attacker"));
    }

    // --- the models ---------------------------------------------------------------------------------------------------------

    private static Type ModelType(string fullName)
    {
        var type = typeof(TAOM.IoC).Assembly.GetType(fullName);
        Assert.IsNotNull(type, $"{fullName} is gone");
        return type!;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheDetachmentCostSeam_IsOverriddenByTheTwoAgentStatModels_OfTheEnginesVirtual()
    {
        foreach (var name in new[]
                 {
                     "TAOM.Features.CareerSystem.Models.TaomAgentStatCalculateModel",
                     "TAOM.Features.CultureDoctrine.Models.TaomCustomBattleAgentStatCalculateModel",
                 })
        {
            var ours = ModelType(name).GetMethod("GetDetachmentCostMultiplierOfAgent",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.IsNotNull(ours, $"{name} does not declare the detachment cost override");
            Assert.AreEqual(typeof(float), ours!.ReturnType);
            Assert.AreNotSame(ours, ours.GetBaseDefinition(), $"{name}: the method shadows the engine's instead of overriding it");
            Assert.IsTrue(ours.GetBaseDefinition().IsVirtual, $"{name}: the engine's method is no longer virtual");
            CollectionAssert.AreEqual(new[] { typeof(Agent), typeof(IDetachment) }, ours.GetParameters().Select(p => p.ParameterType).ToArray());
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheGateDamageSeam_IsOverriddenByTheTwoDamageModels_WithTheInParameterShape()
    {
        foreach (var name in new[]
                 {
                     "TAOM.Features.CombatMechanics.Models.TaomCombatMechanicsModel",
                     "TAOM.Features.CombatMechanics.Models.TaomCustomBattleDamageModel",
                 })
        {
            var ours = ModelType(name).GetMethod("ApplyDamageScaling",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.IsNotNull(ours, $"{name} does not declare ApplyDamageScaling");
            Assert.AreEqual(typeof(float), ours!.ReturnType);
            Assert.AreNotSame(ours, ours.GetBaseDefinition(), $"{name}: the method shadows the engine's instead of overriding it");

            var parameters = ours.GetParameters();
            Assert.AreEqual(3, parameters.Length);
            Assert.AreEqual(typeof(AttackInformation).MakeByRefType(), parameters[0].ParameterType);
            Assert.AreEqual(typeof(AttackCollisionData).MakeByRefType(), parameters[1].ParameterType);
            Assert.AreEqual(typeof(float), parameters[2].ParameterType);
            Assert.IsTrue(parameters[0].IsIn && parameters[1].IsIn, "both structures are `in` parameters");
        }
    }

    // --- everything the feature's own methods reference compiles against the installed engine -------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void EveryMethodTheFeatureOwnsThatTouchesTheEngine_CompilesAgainstTheInstalledEngine()
    {
        // RuntimeHelpers.PrepareMethod forces the JIT, which resolves every member a body references. A drifted member throws
        // here instead of in the first wall battle. RequiresGameIL, like SiegeForcesBindingTests: the claim is about the
        // INSTALLED engine, and CI's binding gate runs against metadata-only reference assemblies.
        var prepared = 0;
        foreach (var type in new[]
                 {
                     typeof(CreatureSiegeAgentAdapter), typeof(CreatureSiegeMissionAdapter), typeof(CreatureSiegeHooks),
                     typeof(CreatureSiegeRoleMissionBehavior),
                 })
            prepared += PrepareAll(type);

        foreach (var (name, method) in new[]
                 {
                     ("TAOM.Features.CareerSystem.Models.TaomAgentStatCalculateModel", "GetDetachmentCostMultiplierOfAgent"),
                     ("TAOM.Features.CultureDoctrine.Models.TaomCustomBattleAgentStatCalculateModel", "GetDetachmentCostMultiplierOfAgent"),
                     ("TAOM.Features.CombatMechanics.Models.TaomCombatMechanicsModel", "ApplyDamageScaling"),
                     ("TAOM.Features.CombatMechanics.Models.TaomCustomBattleDamageModel", "ApplyDamageScaling"),
                 })
        {
            var info = ModelType(name).GetMethod(method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.IsNotNull(info, $"{name}.{method}");
            RuntimeHelpers.PrepareMethod(info!.MethodHandle);
            prepared++;
        }

        Assert.IsTrue(prepared >= 40, $"expected to compile every method of the four types and the four overrides, compiled {prepared}");
    }

    private static int PrepareAll(Type type)
    {
        const BindingFlags declared = AnyMember | BindingFlags.DeclaredOnly;
        var count = 0;
        foreach (var method in type.GetMethods(declared))
        {
            if (method.IsAbstract || method.ContainsGenericParameters) continue;
            RuntimeHelpers.PrepareMethod(method.MethodHandle);
            count++;
        }

        foreach (var ctor in type.GetConstructors(declared))
        {
            if (ctor.IsStatic || ctor.ContainsGenericParameters) continue;
            RuntimeHelpers.PrepareMethod(ctor.MethodHandle);
            count++;
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            count += PrepareAll(nested);
        return count;
    }
}
