using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects;
using TAOM.Adapters;
using TAOM.Features.StagedSiege.Hooks;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// The staged siege against the installed engine. Three things are pinned, because each fails silently: the two patch TARGETS and
/// the parameter NAMES Harmony binds by; every engine member the patch helpers, the adapter, the tactic, the timer and the mission
/// behavior reference (a member that stops resolving fails when its method is first compiled, before any try inside it can run,
/// and PatchShield swallows that at the patched method and skips the original: for <c>InitWithSinglePhase</c> that is no spawn
/// phases in every battle; lessons/harmony-il.md "A patch's own try/catch cannot survive a JIT-time member-resolution failure");
/// and the enum values the compiled code folds into literals.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class StagedSiegeBindingTests
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // --- the two patch targets -----------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void InitWithSinglePhase_LastParameterIsInMissionSpawnSettingsNamedSpawnSettings()
    {
        var overloads = typeof(DefaultBattleMissionAgentSpawnLogic).GetMethods(AnyMember)
            .Where(m => m.Name == "InitWithSinglePhase").ToList();
        Assert.AreEqual(1, overloads.Count, "a second overload would make the patch target ambiguous");

        var method = overloads.Single();
        Assert.IsTrue(method.IsPublic && !method.IsStatic);
        Assert.AreEqual(typeof(void), method.ReturnType);
        var last = method.GetParameters().Last();
        Assert.AreEqual("spawnSettings", last.Name, "Harmony binds the prefix's ref parameter by this name");
        Assert.IsTrue(last.ParameterType.IsByRef, "the MissionSpawnSettings parameter is `in`");
        Assert.AreEqual(typeof(MissionSpawnSettings), last.ParameterType.GetElementType());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnSettingsPrefix_BindsByName_ReturnsVoid_HasLowPriority()
    {
        var target = typeof(DefaultBattleMissionAgentSpawnLogic).GetMethod("InitWithSinglePhase", AnyMember)!;

        AssertBindsByName(typeof(StagedSiegeSpawnSettingsPatch), "Prefix", target);

        var prefix = typeof(StagedSiegeSpawnSettingsPatch).GetMethod("Prefix")!;
        Assert.IsTrue(prefix.GetParameters().Single(p => p.Name == "spawnSettings").ParameterType.IsByRef,
            "the settings must be taken by reference, or the swap never reaches the original");
        Assert.AreEqual(typeof(object), prefix.GetParameters().Single(p => p.Name == "__instance").ParameterType,
            "__instance is an object: the prefix body names no engine type");
        var priority = typeof(StagedSiegeSpawnSettingsPatch).GetCustomAttribute<HarmonyPriority>();
        Assert.IsNotNull(priority, "the prefix must declare its priority (Low: after Patch102)");
        Assert.AreEqual(Priority.Low, priority!.info.priority);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetFormationSpawnFrame_IsTheOnlyOverload_WithTheNamedParameters()
    {
        var overloads = typeof(Mission).GetMethods(AnyMember).Where(m => m.Name == "GetFormationSpawnFrame").ToList();
        Assert.AreEqual(1, overloads.Count, "a second overload would make the patch target ambiguous");

        var method = overloads.Single();
        Assert.IsTrue(method.IsPublic && !method.IsStatic);
        Assert.AreEqual(typeof(void), method.ReturnType);
        CollectionAssert.AreEqual(
            new[] { "team", "formationClass", "isReinforcement", "spawnPosition", "spawnDirection", "useDefaultClassIfNotFound" },
            method.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(typeof(Team), method.GetParameters()[0].ParameterType);
        Assert.AreEqual(typeof(bool), method.GetParameters()[2].ParameterType);
        Assert.IsTrue(method.GetParameters()[3].IsOut && method.GetParameters()[3].ParameterType.GetElementType() == typeof(WorldPosition));
        Assert.IsTrue(method.GetParameters()[4].IsOut && method.GetParameters()[4].ParameterType.GetElementType() == typeof(Vec2));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnFramePostfix_BindsByName_ReturnsVoid()
    {
        var target = typeof(Mission).GetMethod("GetFormationSpawnFrame", AnyMember)!;

        AssertBindsByName(typeof(StagedSiegeSpawnFramePatch), "Postfix", target);

        var postfix = typeof(StagedSiegeSpawnFramePatch).GetMethod("Postfix")!;
        Assert.IsTrue(postfix.GetParameters().Single(p => p.Name == "spawnPosition").ParameterType.IsByRef);
        Assert.IsTrue(postfix.GetParameters().Single(p => p.Name == "spawnDirection").ParameterType.IsByRef);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SetCustomReinforcementSpawnTimer_TakesTheInterface()
    {
        var method = typeof(DefaultBattleMissionAgentSpawnLogic).GetMethod("SetCustomReinforcementSpawnTimer", AnyMember);
        Assert.IsNotNull(method, "DefaultBattleMissionAgentSpawnLogic.SetCustomReinforcementSpawnTimer no longer resolves");
        CollectionAssert.AreEqual(new[] { typeof(ICustomReinforcementSpawnTimer) }, method!.GetParameters().Select(p => p.ParameterType).ToArray());

        Assert.IsTrue(typeof(ICustomReinforcementSpawnTimer).IsAssignableFrom(typeof(StagedReinforcementTimer)));
        var check = typeof(ICustomReinforcementSpawnTimer).GetMethod("Check");
        var reset = typeof(ICustomReinforcementSpawnTimer).GetMethod("ResetTimer");
        Assert.AreEqual(typeof(bool), check!.ReturnType);
        CollectionAssert.AreEqual(new[] { typeof(BattleSideEnum) }, check.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.AreEqual(typeof(void), reset!.ReturnType);
        CollectionAssert.AreEqual(new[] { typeof(BattleSideEnum) }, reset.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MissionSpawnSettings_HasTheTwelveParameterCtor()
    {
        var ctor = typeof(MissionSpawnSettings).GetConstructors().SingleOrDefault(c => c.GetParameters().Length == 12);
        Assert.IsNotNull(ctor, "the prefix builds the settings through the 12-parameter constructor");
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(MissionSpawnSettings.InitialSpawnMethod), typeof(MissionSpawnSettings.ReinforcementTimingMethod),
                typeof(MissionSpawnSettings.ReinforcementSpawnMethod), typeof(float), typeof(float), typeof(float), typeof(float),
                typeof(int), typeof(float), typeof(float), typeof(float), typeof(float),
            },
            ctor!.GetParameters().Select(p => p.ParameterType).ToArray());

        foreach (var (name, type) in new[]
                 {
                     ("InitialTroopsSpawnMethod", typeof(MissionSpawnSettings.InitialSpawnMethod)),
                     ("ReinforcementTroopsSpawnMethod", typeof(MissionSpawnSettings.ReinforcementSpawnMethod)),
                     ("ReinforcementTroopsTimingMethod", typeof(MissionSpawnSettings.ReinforcementTimingMethod)),
                     ("GlobalReinforcementInterval", typeof(float)), ("ReinforcementWavePercentage", typeof(float)),
                     ("DefenderAdvantageFactor", typeof(float)), ("MaximumBattleSideRatio", typeof(float)),
                 })
            Property(typeof(MissionSpawnSettings), name, type);
    }

    // --- every member the adapter, the tactic and the behavior reference -------------------------------------------------

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

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheAdapterMembers_StillResolve()
    {
        Property(typeof(Mission), "SceneName", typeof(string));
        Property(typeof(Mission), "CurrentTime", typeof(float));
        Property(typeof(Mission), "IsSiegeBattle", typeof(bool));
        Property(typeof(Mission), "IsDeploymentFinished", typeof(bool));
        Property(typeof(Mission), "AttackerTeam", typeof(Team));
        Property(typeof(Mission), "DefenderTeam", typeof(Team));
        Property(typeof(Mission), "Scene", typeof(Scene));
        Property(typeof(Mission), "MainAgent", typeof(Agent));
        Property(typeof(Mission), "ActiveMissionObjects", typeof(MBReadOnlyList<MissionObject>));
        Property(typeof(GameNetwork), "IsClientOrReplay", typeof(bool));
        Property(typeof(Campaign), "Current", typeof(Campaign));
        Property(typeof(PlayerEncounter), "EncounterSettlement", typeof(Settlement));
        Property(typeof(Settlement), "CurrentSiegeState", typeof(Settlement.SiegeState));
        Property(typeof(DefaultBattleMissionAgentSpawnLogic), "PlayerSide", typeof(BattleSideEnum));
        Property(typeof(DefaultBattleMissionAgentSpawnLogic), "SpawnSettings", typeof(MissionSpawnSettings).MakeByRefType());
        foreach (var name in new[]
                 {
                     "NumberOfActiveAttackerTroops", "NumberOfActiveDefenderTroops", "NumberOfRemainingAttackerTroops",
                     "NumberOfRemainingDefenderTroops",
                 })
            Property(typeof(DefaultBattleMissionAgentSpawnLogic), name, typeof(int));

        Method(typeof(Mission), "GetNearbyAgents", typeof(MBList<Agent>), typeof(Vec2), typeof(float), typeof(MBList<Agent>));
        Method(typeof(Mission), "CreateMissionObjectFromPrefab", typeof(MissionObject), typeof(string), typeof(MatrixFrame), typeof(bool),
            typeof(float), typeof(Action<GameEntity>));

        Property(typeof(Agent), "IsHuman", typeof(bool));
        Property(typeof(Agent), "IsRunningAway", typeof(bool));
        Property(typeof(Agent), "Team", typeof(Team));
        Property(typeof(Agent), "Position", typeof(Vec3));
        Method(typeof(Agent), "IsActive", typeof(bool));
        Method(typeof(Agent), "TeleportToPosition", typeof(void), typeof(Vec3));

        Property(typeof(Team), "Side", typeof(BattleSideEnum));
        Property(typeof(Team), "Color", typeof(uint));
        Property(typeof(Team), "Color2", typeof(uint));
        Property(typeof(Team), "HasTeamAi", typeof(bool));
        Property(typeof(Team), "IsPlayerTeam", typeof(bool));
        Property(typeof(Team), "IsPlayerGeneral", typeof(bool));
        Property(typeof(Team), "ActiveAgents", typeof(MBReadOnlyList<Agent>));
        Property(typeof(Team), "Mission", typeof(Mission));
        Property(typeof(Team), "FormationsIncludingEmpty", typeof(MBList<Formation>));
        Method(typeof(Team), "GetFormation", typeof(Formation), typeof(FormationClass));
        Method(typeof(Team), "AddTacticOption", typeof(void), typeof(TacticComponent));
        Method(typeof(Team), "ResetTactic", typeof(void));

        Method(typeof(FlagCapturePoint), "SetTeamColorsWithAllSynched", typeof(void), typeof(uint), typeof(uint));
        Method(typeof(CastleGate), "OpenDoor", typeof(void));
        Property(typeof(CastleGate), "IsDestroyed", typeof(bool));
        Property(typeof(CastleGate), "IsDisabled", typeof(bool));

        Property(typeof(Formation), "CountOfUnits", typeof(int));
        Property(typeof(Formation), "IsAIControlled", typeof(bool));
        Property(typeof(Formation), "IsPlayerTroopInFormation", typeof(bool));
        Property(typeof(Formation), "IsSplittableByAI", typeof(bool));
        Property(typeof(Formation), "HasPlayerControlledTroop", typeof(bool));
        Property(typeof(Formation), "CachedAveragePosition", typeof(Vec2));
        Property(typeof(Formation), "QuerySystem", typeof(FormationQuerySystem));
        Property(typeof(Formation), "AI", typeof(FormationAI));
        Field(typeof(Formation), "FormationIndex", typeof(FormationClass));
        Property(typeof(FormationQuerySystem), "IsInfantryFormation", typeof(bool));
        Property(typeof(FormationQuerySystem), "IsRangedFormation", typeof(bool));
        Method(typeof(Formation), "TransferUnits", typeof(void), typeof(Formation), typeof(int));
        Method(typeof(Formation), "SetControlledByAI", typeof(void), typeof(bool), typeof(bool));
        Method(typeof(Formation), "SetMovementOrder", typeof(void), typeof(MovementOrder));
        Method(typeof(Formation), "SetFacingOrder", typeof(void), typeof(FacingOrder));
        Method(typeof(Formation), "ApplyActionOnEachUnit", typeof(void), typeof(Action<Agent>), typeof(Agent));

        Method(typeof(FormationAI), "AddAiBehavior", typeof(void), typeof(BehaviorComponent));
        Method(typeof(FormationAI), "ResetBehaviorWeights", typeof(void));
        Assert.IsNotNull(typeof(FormationAI).GetMethod("GetBehavior"), "FormationAI.GetBehavior<T>");
        Assert.IsNotNull(typeof(FormationAI).GetMethod("SetBehaviorWeight"), "FormationAI.SetBehaviorWeight<T>");
        Field(typeof(BehaviorDefend), "DefensePosition", typeof(WorldPosition));
        Assert.IsNotNull(typeof(BehaviorDefend).GetConstructor(new[] { typeof(Formation) }), "BehaviorDefend(Formation)");

        Field(typeof(MovementOrder), "MovementOrderCharge", typeof(MovementOrder));
        Method(typeof(MovementOrder), "MovementOrderMove", typeof(MovementOrder), typeof(WorldPosition));
        Method(typeof(FacingOrder), "FacingOrderLookAtDirection", typeof(FacingOrder), typeof(Vec2));
        Assert.IsNotNull(typeof(WorldPosition).GetConstructor(new[] { typeof(Scene), typeof(UIntPtr), typeof(Vec3), typeof(bool) }),
            "WorldPosition(Scene, UIntPtr, Vec3, bool)");

        Method(typeof(TacticComponent), "StopUsingAllMachines", typeof(void));
        Method(typeof(TacticComponent), "TickOccasionally", typeof(void));
        Method(typeof(TacticComponent), "GetTacticWeight", typeof(float));
        Method(typeof(TacticComponent), "OnApply", typeof(void));
        Method(typeof(TacticComponent), "OnCancel", typeof(void));

        Method(typeof(MissionResult), "CreateDefenderPushedBack", typeof(MissionResult));
        Method(typeof(MissionResult), "CreateDefeated", typeof(MissionResult), typeof(IMission));
        Property(typeof(MissionResult), "BattleState", typeof(BattleState));
        Property(typeof(MissionResult), "PlayerVictory", typeof(bool));
        Property(typeof(MissionResult), "PlayerDefeated", typeof(bool));
        Method(typeof(IMissionAgentSpawnLogic), "StopSpawner", typeof(void), typeof(BattleSideEnum));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheFoldedEnumValues_MatchTheEngine()
    {
        // `x == BattleSideEnum.Attacker` and the enum arguments of the settings constructor are folded to constants at COMPILE
        // time. Parse the installed enums at run time: a renumbering fails here, not silently in a battle.
        Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(typeof(BattleSideEnum), "Defender")));
        Assert.AreEqual(1, Convert.ToInt32(Enum.Parse(typeof(BattleSideEnum), "Attacker")));
        Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(typeof(MissionSpawnSettings.ReinforcementSpawnMethod), "Balanced")));
        Assert.AreEqual(1, Convert.ToInt32(Enum.Parse(typeof(MissionSpawnSettings.ReinforcementTimingMethod), "CustomTimer")));
        Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(typeof(Settlement.SiegeState), "OnTheWalls")));
        Assert.AreEqual(4, Convert.ToInt32(Enum.Parse(typeof(FormationClass), "Skirmisher")));
        Assert.AreEqual(5, Convert.ToInt32(Enum.Parse(typeof(FormationClass), "HeavyInfantry")));
        Assert.AreEqual(6, Convert.ToInt32(Enum.Parse(typeof(FormationClass), "LightCavalry")));
        Assert.AreEqual(7, Convert.ToInt32(Enum.Parse(typeof(FormationClass), "HeavyCavalry")));
    }

    // --- everything the feature's own methods reference compiles against the installed engine ------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void EveryEngineTouchingMethod_CompilesAgainstTheInstalledEngine()
    {
        // RuntimeHelpers.PrepareMethod forces the JIT, which resolves every member a body references. A drifted member throws
        // here instead of at the first assault (lessons/harmony-il.md: a body's own try cannot catch it). RequiresGameIL, like
        // SiegeForcesBindingTests: the claim is about the INSTALLED engine, and CI's binding gate runs against the
        // metadata-only reference assemblies. The client view (StagedSiegeMissionView) is left out on purpose: its base type
        // lives in TaleWorlds.MountAndBlade.View.dll, which sits only under Modules/Native/bin, outside this test's assembly
        // probe, so preparing it fails alone and passes only after another test loaded the DLL. The Client build checks it.
        var prepared = 0;
        foreach (var type in new[]
                 {
                     typeof(StagedSiegeMissionAdapter), typeof(StagedSiegeSpawnSettingsPatch), typeof(StagedSiegeSpawnFramePatch),
                     typeof(StagedReinforcementTimer), typeof(TacticStagedObjective), typeof(StagedSiegeMissionBehavior),
                     typeof(StagedSiegeMissionEdge), typeof(StagedSiegeCommandQueue),
                 })
            prepared += PrepareAll(type);

        Assert.IsTrue(prepared >= 40, $"expected to compile every method of the eight types and their closures, compiled {prepared}");
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

    // --- helpers ---------------------------------------------------------------------------------------------------------

    private static void AssertBindsByName(Type patchType, string methodName, MethodBase target)
    {
        var patch = patchType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(patch, $"{patchType.Name} has no public static {methodName}");
        Assert.AreEqual(typeof(void), patch!.ReturnType);

        foreach (var parameter in patch.GetParameters())
        {
            if (parameter.Name == "__instance")
            {
                Assert.IsTrue(parameter.ParameterType.IsAssignableFrom(target.DeclaringType), "__instance cannot hold the target's type");
                continue;
            }

            var original = target.GetParameters().SingleOrDefault(p => p.Name == parameter.Name);
            Assert.IsNotNull(original, $"the parameter '{parameter.Name}' names no parameter of {target.DeclaringType!.Name}.{target.Name}: Harmony would hand it null/default");
            var patchElement = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType;
            var originalElement = original!.ParameterType.IsByRef ? original.ParameterType.GetElementType() : original.ParameterType;
            Assert.AreEqual(originalElement, patchElement, $"parameter '{parameter.Name}' changed type");
        }
    }
}
