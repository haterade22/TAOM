using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// Fords of War against the installed engine. Two things are pinned because each fails silently: the tower hit patch TARGET and
/// the parameter NAMES Harmony binds by (a name that matches nothing hands the prefix a default, so every tower would take vanilla
/// damage), and every engine member the three adapters and the patch reference (a member that stops resolving fails when its method
/// is first compiled, before any try inside it can run; PatchShield swallows that at the patched method). Enum values the compiled
/// code folds into literals are pinned too.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FordsOfWarBindingTests
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    // --- the patch target ----------------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void DestructableComponentOnHit_IsTheOnlyDeclaredOverload_WithTheNamedParameters()
    {
        var overloads = typeof(DestructableComponent).GetMethods(AnyMember | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "OnHit").ToList();
        Assert.AreEqual(1, overloads.Count, "a second overload would make the patch target ambiguous");

        var method = overloads.Single();
        Assert.IsTrue(method.IsVirtual && !method.IsStatic, "OnHit is an override");
        Assert.IsTrue(method.IsFamilyOrAssembly, "OnHit is protected internal");
        Assert.AreEqual(typeof(bool), method.ReturnType);
        CollectionAssert.AreEqual(
            new[]
            {
                "attackerAgent", "inflictedDamage", "impactPosition", "impactDirection", "weapon", "affectorWeaponSlotOrMissileIndex",
                "attackerScriptComponentBehavior", "reportDamage", "modifiedDamage", "fireDamage", "modifiedFireDamage",
            },
            method.GetParameters().Select(p => p.Name).ToArray());

        var parameters = method.GetParameters();
        Assert.AreEqual(typeof(Agent), parameters[0].ParameterType);
        Assert.AreEqual(typeof(int), parameters[1].ParameterType);
        Assert.AreEqual(typeof(MissionWeapon), parameters[4].ParameterType.GetElementType(), "weapon is passed by in/ref");
        Assert.IsTrue(parameters[4].ParameterType.IsByRef);
        Assert.IsTrue(parameters[7].IsOut && parameters[8].IsOut && parameters[9].IsOut && parameters[10].IsOut);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TowerHitPrefix_BindsByName_ReturnsVoid_AndTakesTheDamageByRef()
    {
        var target = typeof(DestructableComponent).GetMethod("OnHit", AnyMember | BindingFlags.DeclaredOnly)!;
        var prefix = typeof(FordsTowerHitPatch).GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(prefix, "FordsTowerHitPatch has no public static Prefix");
        Assert.AreEqual(typeof(void), prefix!.ReturnType);

        foreach (var parameter in prefix.GetParameters())
        {
            if (parameter.Name == "__instance")
            {
                Assert.IsTrue(parameter.ParameterType.IsAssignableFrom(target.DeclaringType), "__instance cannot hold the target's type");
                continue;
            }

            var original = target.GetParameters().SingleOrDefault(p => p.Name == parameter.Name);
            Assert.IsNotNull(original, $"the parameter '{parameter.Name}' names no parameter of DestructableComponent.OnHit: Harmony would hand it a default");
            var patchElement = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType;
            var originalElement = original!.ParameterType.IsByRef ? original.ParameterType.GetElementType() : original.ParameterType;
            Assert.AreEqual(originalElement, patchElement, $"parameter '{parameter.Name}' changed type");
        }

        Assert.IsTrue(prefix.GetParameters().Single(p => p.Name == "inflictedDamage").ParameterType.IsByRef,
            "the damage must be taken by reference, or the replacement never reaches the original");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TowerHitPatch_TargetsOnHitInThePatchCategory()
    {
        var target = typeof(FordsTowerHitPatch).GetCustomAttributes<HarmonyPatch>().Single().info;
        Assert.AreEqual(typeof(DestructableComponent), target.declaringType);
        Assert.AreEqual("OnHit", target.methodName);
        var category = typeof(FordsTowerHitPatch).GetCustomAttributes<HarmonyPatchCategory>().Single();
        Assert.AreEqual("Patch131_FordsOfWar", category.info.category);
    }

    // --- the tower adapter ---------------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheTowerAdapterMembers_StillResolve()
    {
        Field(typeof(DestructableComponent), "MaxHitPoint", typeof(float));
        var hitPoint = Property(typeof(DestructableComponent), "HitPoint", typeof(float));
        Assert.IsNotNull(hitPoint.GetSetMethod(true), "DestructableComponent.HitPoint lost its setter");
        Property(typeof(DestructableComponent), "IsDestroyed", typeof(bool));

        var destroyed = typeof(DestructableComponent).GetEvent("OnDestroyed", AnyMember);
        Assert.IsNotNull(destroyed, "DestructableComponent.OnDestroyed no longer resolves");
        var invoke = destroyed!.EventHandlerType!.GetMethod("Invoke")!;
        CollectionAssert.AreEqual(
            new[] { typeof(DestructableComponent), typeof(Agent), typeof(MissionWeapon).MakeByRefType(), typeof(ScriptComponentBehavior), typeof(int) },
            invoke.GetParameters().Select(p => p.ParameterType).ToArray(), "the OnDestroyed handler shape changed");

        Method(typeof(GameEntity), "Instantiate", typeof(GameEntity), typeof(Scene), typeof(string), typeof(MatrixFrame), typeof(bool));
        Assert.IsNotNull(typeof(GameEntity).GetMethod("GetFirstScriptOfType", AnyMember), "GameEntity.GetFirstScriptOfType<T> no longer resolves");
        Method(typeof(Scene), "FindEntityWithTag", typeof(GameEntity), typeof(string));

        Field(typeof(MatrixFrame), "origin", typeof(Vec3));
        Field(typeof(MatrixFrame), "rotation", typeof(Mat3));
        Field(typeof(Mat3), "f", typeof(Vec3));
        Method(typeof(Mat3), "Orthonormalize", typeof(void));
        Method(typeof(Mat3), "OrthonormalizeAccordingToForwardAndKeepUpAsZAxis", typeof(void));
        Method(typeof(Mat3), "ApplyScaleLocal", typeof(void), typeof(float));

        Method(typeof(Mission), "AddCustomMissile", typeof(Mission.Missile), typeof(Agent), typeof(MissionWeapon), typeof(Vec3), typeof(Vec3),
            typeof(Mat3), typeof(float), typeof(float), typeof(bool), typeof(MissionObject), typeof(int));
        Assert.IsTrue(typeof(MissionObject).IsAssignableFrom(typeof(DestructableComponent)), "the volley ignores the tower through its component");

        var weaponCtor = typeof(MissionWeapon).GetConstructor(new[] { typeof(ItemObject), typeof(ItemModifier), typeof(Banner), typeof(short) });
        Assert.IsNotNull(weaponCtor, "MissionWeapon(ItemObject, ItemModifier, Banner, short) no longer resolves");
        Method(typeof(MissionWeapon), "GetModifiedMissileSpeedForCurrentUsage", typeof(int));
        Method(typeof(MissionWeapon), "GetModifiedThrustDamageForCurrentUsage", typeof(int));
        Property(typeof(MissionWeapon), "IsEmpty", typeof(bool));
        Property(typeof(MissionWeapon), "CurrentUsageItem", typeof(WeaponComponentData));

        Property(typeof(WeaponComponentData), "IsRangedWeapon", typeof(bool));
        Property(typeof(WeaponComponentData), "IsAmmo", typeof(bool));
        Property(typeof(WeaponComponentData), "IsConsumable", typeof(bool));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheInvulnerabilityMember_IsOnAgent()
    {
        Method(typeof(Agent), "SetMortalityState", typeof(void), typeof(Agent.MortalityState));
        Property(typeof(Agent), "CurrentMortalityState", typeof(Agent.MortalityState));
        Assert.IsTrue(Enum.IsDefined(typeof(Agent.MortalityState), "Invulnerable"), "Agent.MortalityState.Invulnerable is gone");
        Assert.AreEqual(1, (int)Agent.MortalityState.Invulnerable);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheGunnerAgentMembers_StillResolve()
    {
        Method(typeof(Agent), "SetScriptedPositionAndDirection", typeof(void), typeof(WorldPosition).MakeByRefType(), typeof(float), typeof(bool),
            typeof(Agent.AIScriptedFrameFlags));
        Assert.AreEqual(4, (int)Agent.AIScriptedFrameFlags.ConsiderRotation);
        Method(typeof(Agent), "FadeOut", typeof(void), typeof(bool), typeof(bool));
        Method(typeof(Agent), "IsActive", typeof(bool));
        Property(typeof(Agent), "Position", typeof(Vec3));
        Property(typeof(Agent), "Index", typeof(int));
        Property(typeof(Agent), "Mission", typeof(Mission));

        var ctor = typeof(WorldPosition).GetConstructor(new[] { typeof(Scene), typeof(UIntPtr), typeof(Vec3), typeof(bool) });
        Assert.IsNotNull(ctor, "WorldPosition(Scene, UIntPtr, Vec3, bool) no longer resolves");
        Assert.IsNotNull(typeof(WorldPosition).GetConstructor(new[] { typeof(Scene), typeof(Vec3) }), "WorldPosition(Scene, Vec3) no longer resolves");
        Property(typeof(WorldPosition), "IsValid", typeof(bool));
    }

    // --- the battlefield adapter ---------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheSpawnMembers_StillResolve()
    {
        Method(typeof(Mission), "GetAgentBuildDataToSpawnTroop", typeof(AgentBuildData), typeof(IAgentOriginBase), typeof(bool), typeof(bool),
            typeof(bool), typeof(bool), typeof(int), typeof(int), typeof(Vec3?), typeof(Vec2?), typeof(ItemObject), typeof(FormationClass), typeof(bool));
        Method(typeof(Mission), "SpawnTroopWithAgentBuildData", typeof(Agent), typeof(AgentBuildData), typeof(bool), typeof(bool), typeof(string));
        Method(typeof(Mission), "SetMissionCorpseFadeOutTimeInSeconds", typeof(void), typeof(float));
        Method(typeof(Mission), "OnInitialSpawnCompleted", typeof(void), typeof(BattleSideEnum));
        Property(typeof(Mission), "PlayerTeam", typeof(Team));
        Property(typeof(Mission), "PlayerEnemyTeam", typeof(Team));
        Property(typeof(Agent), "Team", typeof(Team));
        Property(typeof(Mission), "Scene", typeof(Scene));
        Property(typeof(Mission), "Boundaries", typeof(Mission.MBBoundaryCollection));

        Method(typeof(AgentBuildData), "Team", typeof(AgentBuildData), typeof(Team));
        Method(typeof(AgentBuildData), "Formation", typeof(AgentBuildData), typeof(Formation));
        Method(typeof(AgentBuildData), "Banner", typeof(AgentBuildData), typeof(Banner));
        Method(typeof(AgentBuildData), "ClothingColor1", typeof(AgentBuildData), typeof(uint));
        Method(typeof(AgentBuildData), "ClothingColor2", typeof(AgentBuildData), typeof(uint));
        Method(typeof(AgentBuildData), "TroopOrigin", typeof(AgentBuildData), typeof(IAgentOriginBase));
        Method(typeof(AgentBuildData), "NoHorses", typeof(AgentBuildData), typeof(bool));
        Method(typeof(AgentBuildData), "InitialPosition", typeof(AgentBuildData), typeof(Vec3).MakeByRefType());
        Method(typeof(AgentBuildData), "InitialDirection", typeof(AgentBuildData), typeof(Vec2).MakeByRefType());
        Assert.IsNotNull(typeof(AgentBuildData).GetConstructor(new[] { typeof(BasicCharacterObject) }), "AgentBuildData(BasicCharacterObject) no longer resolves");
        Assert.IsNotNull(typeof(BasicBattleAgentOrigin).GetConstructor(new[] { typeof(BasicCharacterObject) }), "BasicBattleAgentOrigin(BasicCharacterObject) no longer resolves");

        Method(typeof(BasicCharacterObject), "HasMount", typeof(bool));
        Property(typeof(BasicCharacterObject), "Level", typeof(int));
        Property(typeof(BasicCharacterObject), "Name", typeof(TaleWorlds.Localization.TextObject));
        Property(typeof(Team), "Banner", typeof(Banner));
        Property(typeof(Team), "Color", typeof(uint));
        Property(typeof(Team), "Color2", typeof(uint));
        Method(typeof(Team), "GetFormation", typeof(Formation), typeof(FormationClass));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheLaneFormationClasses_AreTheFirstFourDefaults()
    {
        Assert.AreEqual(0, (int)FormationClass.Infantry);
        Assert.AreEqual(1, (int)FormationClass.Ranged);
        Assert.AreEqual(2, (int)FormationClass.Cavalry);
        Assert.AreEqual(3, (int)FormationClass.HorseArcher);
        Assert.AreEqual(4, (int)FormationClass.NumberOfDefaultFormations);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheMovementOrderFactories_StillResolve()
    {
        Method(typeof(Formation), "SetMovementOrder", typeof(void), typeof(MovementOrder));
        Method(typeof(MovementOrder), "MovementOrderMove", typeof(MovementOrder), typeof(WorldPosition));
        Method(typeof(MovementOrder), "MovementOrderAttackEntity", typeof(MovementOrder), typeof(GameEntity), typeof(bool));
        Field(typeof(MovementOrder), "MovementOrderCharge", typeof(MovementOrder));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheRemovalCallbackAndResultMembers_StillResolve()
    {
        var removed = typeof(MissionBehavior).GetMethod("OnAgentRemoved", AnyMember);
        Assert.IsNotNull(removed, "MissionBehavior.OnAgentRemoved no longer resolves");
        CollectionAssert.AreEqual(new[] { typeof(Agent), typeof(Agent), typeof(AgentState), typeof(KillingBlow) },
            removed!.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.IsTrue(Enum.IsDefined(typeof(AgentState), "Killed"));
        Assert.IsTrue(Enum.IsDefined(typeof(AgentState), "Unconscious"));

        var ended = typeof(MissionLogic).GetMethod("MissionEnded", AnyMember);
        Assert.IsNotNull(ended, "MissionLogic.MissionEnded no longer resolves");
        Assert.IsTrue(ended!.GetParameters().Single().ParameterType.IsByRef);
        Assert.IsNotNull(typeof(MissionResult).GetMethod("CreateSuccessful", new[] { typeof(IMission), typeof(bool) }), "MissionResult.CreateSuccessful(IMission, bool) is gone");
        Assert.IsNotNull(typeof(MissionResult).GetMethod("CreateDefeated", new[] { typeof(IMission) }), "MissionResult.CreateDefeated(IMission) is gone");
        Assert.IsTrue(typeof(IMission).IsAssignableFrom(typeof(Mission)));
    }

    // --- the scene adapter ---------------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheSceneAdapterMembers_StillResolve()
    {
        Method(typeof(Scene), "GetTerrainHeight", typeof(float), typeof(Vec2), typeof(bool));
        Method(typeof(Scene), "GetGroundHeightAtPosition", typeof(float), typeof(Vec3), typeof(BodyFlags));
        Method(typeof(Scene), "GetNavigationMeshForPosition", typeof(UIntPtr), typeof(Vec3).MakeByRefType());
        Method(typeof(Scene), "GetEntities", typeof(void), typeof(List<GameEntity>).MakeByRefType());
        Property(typeof(GameEntity), "Tags", typeof(string[]));
        Method(typeof(GameEntity), "GetGlobalFrame", typeof(MatrixFrame));

        Assert.IsTrue(typeof(IEnumerable<KeyValuePair<string, ICollection<Vec2>>>).IsAssignableFrom(typeof(Mission.MBBoundaryCollection)),
            "the boundary collection no longer enumerates (name, points) pairs");
        Assert.IsTrue(Enum.IsDefined(typeof(BodyFlags), "CommonCollisionExcludeFlags"));
    }

    // --- helpers -------------------------------------------------------------------------------------------------------

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
