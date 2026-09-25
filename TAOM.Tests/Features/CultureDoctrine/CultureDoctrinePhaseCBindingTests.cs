using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// Drift guards for the Phase C and D surface of the culture doctrine (#608): the
/// <c>BehaviorComponent</c> lifecycle the five TAOM behaviours override, the query members
/// they read, the order setters they call, the morale seams the two morale models override
/// and the caller that consults them, the agent-stat post-pass, and the troop-class override.
/// Every one of these is inheritance or a public call, so a rename would surface as a
/// JIT-time failure on the AI thread or a silently vanilla battle, never as a compile error
/// against the pinned reference assemblies.
/// </summary>
[TestClass]
public class CultureDoctrinePhaseCBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type Resolve(string fullName)
    {
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve.");
        return type;
    }

    private static Type Mb(string simpleName) => Resolve("TaleWorlds.MountAndBlade." + simpleName);

    private static Type? MemberType(Type type, string name)
    {
        var property = AccessTools.Property(type, name);
        if (property != null) return property.PropertyType;
        var field = AccessTools.Field(type, name);
        return field?.FieldType;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BehaviorComponent_LifecycleTheTaomBaseOverrides_IsStillVirtual()
    {
        RequireGame();

        var component = Mb("BehaviorComponent");
        Assert.IsNotNull(component.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { Mb("Formation") }, null), "BehaviorComponent(Formation) is gone.");
        foreach (var (name, family) in new[] { ("GetAiWeight", true), ("CalculateCurrentOrder", true), ("OnBehaviorActivatedAux", true), ("TickOccasionally", false), ("OnBehaviorCanceled", false), ("ResetBehavior", false), ("GetBehaviorString", false) })
        {
            var method = AccessTools.Method(component, name);
            Assert.IsNotNull(method, "BehaviorComponent." + name + " is gone.");
            Assert.IsTrue(method.IsVirtual, "BehaviorComponent." + name + " is no longer virtual.");
            Assert.AreEqual(family, method.IsFamily, "BehaviorComponent." + name + " changed accessibility.");
        }
        var order = AccessTools.Property(component, "CurrentOrder");
        Assert.IsTrue(order != null && order.SetMethod != null && order.SetMethod.IsFamily, "BehaviorComponent.CurrentOrder lost its protected setter.");
        var facing = AccessTools.Field(component, "CurrentFacingOrder");
        Assert.IsTrue(facing != null && facing.IsFamily, "BehaviorComponent.CurrentFacingOrder is no longer a protected field.");
        Assert.IsNotNull(MemberType(component, "WeightFactor"));
        Assert.IsNotNull(MemberType(component, "BehaviorCoherence"));
        var penalty = AccessTools.Property(component, "NavmeshlessTargetPositionPenalty");
        Assert.IsTrue(penalty != null && penalty.GetMethod.IsVirtual, "NavmeshlessTargetPositionPenalty is no longer virtual; BehaviorCycleCharge cannot disable it.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void FormationAI_TickOnlyTicksTheActiveBehaviour_AndActivationCancelsTheOld()
    {
        RequireGame();

        // The TAOM base restores orders in Canceled() and steps machines in the active tick only;
        // both rest on FormationAI keeping the cancel-then-activate sequence and ticking one
        // behaviour per tick (FormationAI.cs:49-69, 240-256).
        var ai = Mb("FormationAI");
        var setter = AccessTools.PropertySetter(ai, "ActiveBehavior");
        Assert.IsNotNull(setter, "FormationAI.ActiveBehavior lost its setter.");
        var body = PatchProcessor.ReadMethodBody(setter).Select(i => i.Value as MethodInfo).Where(m => m != null).Select(m => m!.Name).ToList();
        CollectionAssert.Contains(body, "OnBehaviorCanceled");
        CollectionAssert.Contains(body, "OnBehaviorActivated");
        var tick = AccessTools.Method(ai, "TickOccasionally", new[] { typeof(float) });
        Assert.IsNotNull(tick, "FormationAI.TickOccasionally(float) is gone.");
        var ticks = PatchProcessor.ReadMethodBody(tick).Select(i => i.Value as MethodInfo).Count(m => m != null && m.Name == "TickOccasionally" && m.DeclaringType == Mb("BehaviorComponent"));
        Assert.AreEqual(1, ticks, "FormationAI.TickOccasionally no longer ticks exactly the active behaviour once.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void QueryMembers_TheBehavioursRead_StillExist()
    {
        RequireGame();

        var q = Mb("FormationQuerySystem");
        foreach (var name in new[]
        {
            "IsUnderCavalryChargeFromFront", "HasShield", "HasThrowingUnitRatio", "IsUnderRangedAttack", "UnderRangedAttackRatio",
            "MakingRangedAttackRatio", "MaximumMissileRange", "MissileRangeAdjusted", "ClosestSignificantlyLargeEnemyFormation",
            "IsCavalryFormation", "IsRangedCavalryFormation", "IsInfantryFormation", "MainFormation", "Team",
        })
            Assert.IsNotNull(MemberType(q, name), "FormationQuerySystem." + name + " is gone.");
        Assert.AreEqual(typeof(bool), MemberType(q, "IsUnderCavalryChargeFromFront"));
        Assert.AreEqual(typeof(float), MemberType(q, "HasThrowingUnitRatio"));

        var formation = Mb("Formation");
        foreach (var name in new[] { "CachedAveragePosition", "CachedMedianPosition", "CachedCurrentVelocity", "CachedFormationIntegrityData", "CachedClosestEnemyFormation", "Direction", "Width", "Depth", "CountOfUnits", "FormationIndex", "IsAIControlled", "AI", "QuerySystem" })
            Assert.IsNotNull(MemberType(formation, name), "Formation." + name + " is gone.");
        var integrity = AccessTools.Inner(formation, "FormationIntegrityDataGroup");
        Assert.IsNotNull(integrity, "Formation.FormationIntegrityDataGroup is gone.");
        Assert.IsNotNull(MemberType(integrity, "DeviationOfPositionsExcludeFarAgents"));
        Assert.IsNotNull(MemberType(integrity, "AverageMaxUnlimitedSpeedExcludeFarAgents"));

        var team = Mb("TeamQuerySystem");
        foreach (var name in new[] { "AverageEnemyPosition", "AveragePosition", "MedianTargetFormation" })
            Assert.IsNotNull(MemberType(team, name), "TeamQuerySystem." + name + " is gone.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OrderSetters_AndTheOrdersTheBehavioursIssue_StillExist()
    {
        RequireGame();

        var formation = Mb("Formation");
        foreach (var (name, parameter) in new[] { ("SetMovementOrder", "MovementOrder"), ("SetFacingOrder", "FacingOrder"), ("SetArrangementOrder", "ArrangementOrder"), ("SetFiringOrder", "FiringOrder") })
        {
            var method = AccessTools.Method(formation, name, new[] { Mb(parameter) });
            Assert.IsTrue(method != null && method.IsPublic, "Formation." + name + "(" + parameter + ") is gone or not public.");
        }
        Assert.IsNotNull(AccessTools.Method(formation, "SetFormOrder"), "Formation.SetFormOrder is gone.");

        var movement = Mb("MovementOrder");
        Assert.IsNotNull(AccessTools.Field(movement, "MovementOrderCharge"));
        Assert.IsNotNull(AccessTools.Method(movement, "MovementOrderChargeToTarget", new[] { formation }));
        Assert.IsNotNull(AccessTools.Method(movement, "MovementOrderMove", new[] { Resolve("TaleWorlds.Engine.WorldPosition") }));
        Assert.IsNotNull(AccessTools.Method(movement, "GetPosition", new[] { formation }));
        var facing = Mb("FacingOrder");
        Assert.IsNotNull(AccessTools.Field(facing, "FacingOrderLookAtEnemy"));
        Assert.IsNotNull(AccessTools.Method(facing, "FacingOrderLookAtDirection", new[] { Resolve("TaleWorlds.Library.Vec2") }));
        var arrangement = Mb("ArrangementOrder");
        foreach (var name in new[] { "ArrangementOrderLine", "ArrangementOrderLoose", "ArrangementOrderShieldWall", "ArrangementOrderSquare", "ArrangementOrderSkein" })
            Assert.IsNotNull(AccessTools.Field(arrangement, name), "ArrangementOrder." + name + " is gone.");
        var firing = Mb("FiringOrder");
        Assert.IsNotNull(AccessTools.Field(firing, "FiringOrderFireAtWill"));
        Assert.IsNotNull(AccessTools.Field(firing, "FiringOrderHoldYourFire"));
        var form = Mb("FormOrder");
        Assert.IsNotNull(AccessTools.Field(form, "FormOrderWide"));
        Assert.IsNotNull(AccessTools.Field(form, "FormOrderDeep"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TacticComponent_SplitHelper_IsStillProtected()
    {
        RequireGame();

        var tactic = Mb("TacticComponent");
        var split = AccessTools.Method(tactic, "SplitFormationClassIntoGivenNumber");
        Assert.IsTrue(split != null && split.IsFamily, "TacticComponent.SplitFormationClassIntoGivenNumber is gone or no longer protected; the vanguard split cannot keep HeavyCavalry apart.");
        var cancel = AccessTools.Method(tactic, "OnCancel");
        Assert.IsTrue(cancel != null && cancel.IsVirtual && cancel.IsFamilyOrAssembly, "TacticComponent.OnCancel is no longer protected internal virtual; the volley release has no seam.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void MoraleModel_Seams_AndTheirCaller_StillExist()
    {
        RequireGame();

        var model = Resolve("TaleWorlds.MountAndBlade.ComponentInterfaces.BattleMoraleModel");
        var agent = Mb("Agent");
        var canPanic = AccessTools.Method(model, "CanPanicDueToMorale", new[] { agent });
        Assert.IsTrue(canPanic != null && canPanic.IsAbstract, "BattleMoraleModel.CanPanicDueToMorale(Agent) is gone or no longer abstract.");
        var initial = AccessTools.Method(model, "GetEffectiveInitialMorale", new[] { agent, typeof(float) });
        Assert.IsTrue(initial != null && initial.IsAbstract, "BattleMoraleModel.GetEffectiveInitialMorale(Agent, float) is gone.");

        foreach (var name in new[] { "SandBox.GameComponents.SandboxBattleMoraleModel", "TaleWorlds.MountAndBlade.CustomBattleMoraleModel" })
        {
            var type = Resolve(name);
            Assert.IsTrue(type.IsPublic && !type.IsSealed, name + " must stay public and unsealed.");
            Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes), name + " lost its parameterless constructor.");
        }

        // The panic gate: CommonAIComponent.CanPanic asks the model (CommonAIComponent.cs:174-176),
        // from OnTickParallel, so the service behind it must stay allocation-free.
        var common = Mb("CommonAIComponent");
        var gate = AccessTools.Method(common, "CanPanic");
        Assert.IsNotNull(gate, "CommonAIComponent.CanPanic is gone.");
        Assert.IsTrue(PatchProcessor.ReadMethodBody(gate).Any(i => i.Value is MethodInfo m && m.Name == "CanPanicDueToMorale"), "CommonAIComponent.CanPanic no longer consults BattleMoraleModel.CanPanicDueToMorale.");
        var init = AccessTools.Method(common, "InitializeMorale");
        Assert.IsNotNull(init, "CommonAIComponent.InitializeMorale is gone.");
        Assert.IsTrue(PatchProcessor.ReadMethodBody(init).Any(i => i.Value is MethodInfo m && m.Name == "GetEffectiveInitialMorale"), "InitializeMorale no longer consults GetEffectiveInitialMorale.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AgentStatModel_PostPassSurface_StillExists()
    {
        RequireGame();

        var custom = Mb("CustomBattleAgentStatCalculateModel");
        Assert.IsTrue(custom.IsPublic && !custom.IsSealed, "CustomBattleAgentStatCalculateModel must stay public and unsealed.");
        var update = AccessTools.Method(custom, "UpdateAgentStats");
        Assert.IsTrue(update != null && update.IsVirtual, "CustomBattleAgentStatCalculateModel.UpdateAgentStats is no longer virtual.");

        var props = Mb("AgentDrivenProperties");
        foreach (var name in new[]
        {
            "AIAttackOnDecideChance", "AiDefendWithShieldDecisionChanceValue", "AiUseShieldAgainstEnemyMissileProbability", "AiShooterError",
            "AiRangerLeadErrorMin", "AiRangerLeadErrorMax", "AiRangerVerticalErrorMultiplier", "AiRangerHorizontalErrorMultiplier", "AiChargeHorsebackTargetDistFactor",
        })
        {
            var property = AccessTools.Property(props, name);
            Assert.IsTrue(property != null && property.SetMethod != null && property.SetMethod.IsPublic, "AgentDrivenProperties." + name + " is gone or read-only.");
            Assert.AreEqual(typeof(float), property!.PropertyType);
        }
        Assert.IsNotNull(MemberType(Resolve("TaleWorlds.Core.BasicCharacterObject"), "Culture"), "BasicCharacterObject.Culture is gone.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TroopClassOverride_IsAFuncEvent_AndTheEngineConsultsItFirst()
    {
        RequireGame();

        var mission = Mb("Mission");
        var evt = mission.GetEvent("GetAgentTroopClass_Override");
        Assert.IsNotNull(evt, "Mission.GetAgentTroopClass_Override is gone.");
        Assert.AreEqual(typeof(Func<,,>).MakeGenericType(Resolve("TaleWorlds.Core.BattleSideEnum"), Resolve("TaleWorlds.Core.BasicCharacterObject"), Resolve("TaleWorlds.Core.FormationClass")), evt!.EventHandlerType);
        var get = AccessTools.Method(mission, "GetAgentTroopClass");
        Assert.IsNotNull(get, "Mission.GetAgentTroopClass is gone.");
        var body = PatchProcessor.ReadMethodBody(get).ToList();
        Assert.IsTrue(body.Any(i => i.Value is MethodInfo m && m.Name == "GetFormationClass"), "GetAgentTroopClass no longer reads GetFormationClass; re-read the body the override replaces.");
        Assert.IsTrue(body.Any(i => i.Value is MethodInfo m && m.Name == "DismountedClass"), "GetAgentTroopClass no longer dismounts; the routing rule reproduces a rule that is gone.");
        foreach (var name in new[] { "IsSiegeBattle", "IsNavalBattle", "IsNavalRaidBattle", "IsSallyOutBattle" })
            Assert.AreEqual(typeof(bool), MemberType(mission, name), "Mission." + name + " is gone.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void SergeantBehaviourText_IsLookedUpByTypeName()
    {
        RequireGame();

        // BehaviorComponent.GetBehaviorString: GameTexts.FindText("str_formation_ai_sergeant_instruction_behavior_text", GetType().Name).
        // Every TAOM behaviour ships a row under that id; a missing variation renders an ERROR string.
        var method = AccessTools.Method(Mb("BehaviorComponent"), "GetBehaviorString");
        var strings = PatchProcessor.ReadMethodBody(method).Select(i => i.Value as string).Where(s => s != null).ToList();
        CollectionAssert.Contains(strings, "str_formation_ai_sergeant_instruction_behavior_text");
    }
}
