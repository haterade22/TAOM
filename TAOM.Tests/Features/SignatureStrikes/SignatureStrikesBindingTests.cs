using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SignatureStrikes.Domain;
using TAOM.Features.SignatureStrikes.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// Drift-guards for the engine members a signature strike reaches: the melee-hit virtual it
/// listens on, the enemy query it rings with, the knock-back decider it overrides, and the two
/// engine enums the boundary maps onto its own. No Harmony patch is involved, so these are the
/// only bindings the compiler does not already pin.
/// </summary>
[TestClass]
public class SignatureStrikesBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MissionLogic_IsTheBaseClass_NotMissionBehavior()
    {
        // The BehaviorTreeMissionLogic regression: a MissionBehavior subclass declaring
        // BehaviorType.Logic makes AddMissionBehavior push `this as MissionLogic` (null) into
        // MissionLogics and NRE every tick. docs/reviews/rca-looter-battle-nre-2026-05-24.md
        RequireGame();

        var baseType = typeof(SignatureStrikesMissionLogic).BaseType;

        Assert.IsNotNull(baseType);
        Assert.AreEqual("MissionLogic", baseType!.Name);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MissionBehavior_OnMeleeHit_ResolvesWithTheExpectedSignature()
    {
        RequireGame();

        var behavior = AccessTools.TypeByName("TaleWorlds.MountAndBlade.MissionBehavior");
        var method = AccessTools.Method(behavior, "OnMeleeHit");

        Assert.IsNotNull(method, "MissionBehavior.OnMeleeHit did not resolve; the strike has no trigger.");
        CollectionAssert.AreEqual(
            new[] { "Agent", "Agent", "Boolean", "AttackCollisionData" },
            method!.GetParameters().Select(p => p.ParameterType.Name).ToArray(),
            "OnMeleeHit's parameters drifted. The bool is `isCanceled` and the collision data " +
            "carries AttackDirection, CollisionResult and CollisionGlobalPosition.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AgentApplyDamageModel_DecideAgentKnockedBackByBlow_Resolves()
    {
        RequireGame();

        var model = AccessTools.TypeByName("TaleWorlds.MountAndBlade.ComponentInterfaces.AgentApplyDamageModel");
        var method = AccessTools.Method(model, "DecideAgentKnockedBackByBlow");

        Assert.IsNotNull(method, "AgentApplyDamageModel.DecideAgentKnockedBackByBlow did not resolve.");
        Assert.AreEqual(5, method!.GetParameters().Length);
        Assert.IsTrue(method.IsVirtual, "must stay overridable for the side-swing knock-back verdict");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Mission_GetNearbyEnemyAgents_ResolvesWithTheTeamOverload()
    {
        RequireGame();

        var mission = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Mission");
        var method = AccessTools.Method(mission, "GetNearbyEnemyAgents");

        Assert.IsNotNull(method);
        CollectionAssert.AreEqual(
            new[] { "Vec2", "Single", "Team", "MBList`1" },
            method!.GetParameters().Select(p => p.ParameterType.Name).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BlowFlags_KnockBack_IsStillTheBitTheEngineSetsOnAMeleeBlow()
    {
        RequireGame();

        var flags = AccessTools.TypeByName("TaleWorlds.MountAndBlade.BlowFlags");
        var value = Convert.ToInt32(Enum.Parse(flags, "KnockBack"));

        Assert.AreEqual(0x10, value, "BlowFlags.KnockBack moved; the synthetic sweep blow sets this bit.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CombatCollisionResult_MatchesTheDomainMirror()
    {
        // StrikeCollision mirrors the engine enum by NAME so the boundary switch cannot silently
        // map a renamed member onto the wrong branch.
        RequireGame();

        var engine = AccessTools.TypeByName("TaleWorlds.MountAndBlade.CombatCollisionResult");

        CollectionAssert.AreEquivalent(
            Enum.GetNames(typeof(StrikeCollision)),
            Enum.GetNames(engine),
            "CombatCollisionResult changed. Re-derive StrikeContextFactory's collision mapping.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void UsageDirection_AttackMembersKeepTheirValues()
    {
        // The boundary maps AttackUp/Left/Right/Down onto StrikeDirection by name; the values are
        // pinned so a reordering shows up here rather than as slams on the wrong swing.
        RequireGame();

        var usage = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Agent+UsageDirection");

        Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(usage, "AttackUp")));
        Assert.AreEqual(1, Convert.ToInt32(Enum.Parse(usage, "AttackDown")));
        Assert.AreEqual(2, Convert.ToInt32(Enum.Parse(usage, "AttackLeft")));
        Assert.AreEqual(3, Convert.ToInt32(Enum.Parse(usage, "AttackRight")));
    }

    // ---- The scream's sound (#645) ---------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Mission_MakeSound_ResolvesWithTheOneShotSignature()
    {
        // Native module_sounds.xml documents exactly this one-shot: (id, position, false, true, -1, -1).
        RequireGame();

        var mission = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Mission");
        var vec3 = AccessTools.TypeByName("TaleWorlds.Library.Vec3");
        var method = AccessTools.Method(mission, "MakeSound",
            new[] { typeof(int), vec3, typeof(bool), typeof(bool), typeof(int), typeof(int) });

        Assert.IsNotNull(method, "Mission.MakeSound(int, Vec3, bool, bool, int, int) did not resolve; the scream is silent.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SoundEvent_GetEventIdFromString_IsAStaticIntLookup()
    {
        RequireGame();

        var soundEvent = AccessTools.TypeByName("TaleWorlds.Engine.SoundEvent");
        var method = AccessTools.Method(soundEvent, "GetEventIdFromString", new[] { typeof(string) });

        Assert.IsNotNull(method, "SoundEvent.GetEventIdFromString(string) did not resolve.");
        Assert.IsTrue(method!.IsStatic);
        Assert.AreEqual(typeof(int), method.ReturnType, "-1 is the not-registered answer the runner falls back on");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Agent_MakeVoice_AndTheYellFallbackResolve()
    {
        RequireGame();

        var agent = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Agent");
        var skinVoiceType = AccessTools.TypeByName("TaleWorlds.MountAndBlade.SkinVoiceManager+SkinVoiceType");
        var prediction = AccessTools.TypeByName("TaleWorlds.MountAndBlade.SkinVoiceManager+CombatVoiceNetworkPredictionType");
        var voiceType = AccessTools.TypeByName("TaleWorlds.MountAndBlade.SkinVoiceManager+VoiceType");

        Assert.IsNotNull(AccessTools.Method(agent, "MakeVoice", new[] { skinVoiceType, prediction }),
            "Agent.MakeVoice(SkinVoiceType, CombatVoiceNetworkPredictionType) did not resolve.");
        Assert.IsNotNull(AccessTools.Field(voiceType, "Yell"), "SkinVoiceManager.VoiceType.Yell is gone.");
        CollectionAssert.Contains(Enum.GetNames(prediction), "NoPrediction");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Agent_GetEyeGlobalPosition_ResolvesToAVec3()
    {
        RequireGame();

        var agent = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Agent");
        var method = AccessTools.Method(agent, "GetEyeGlobalPosition", Type.EmptyTypes);

        Assert.IsNotNull(method, "Agent.GetEyeGlobalPosition() did not resolve; the scream has no source.");
        Assert.AreEqual("Vec3", method!.ReturnType.Name);
    }

    [TestMethod]
    public void SubModule_WiresTheLogicAndHandsTheModelItsServices()
    {
        // The model's two signature params are OPTIONAL (null = feature absent), so forgetting to
        // pass them compiles clean and silently drops the guaranteed knockdown. Pin the call site.
        var source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        StringAssert.Contains(source, "new Features.SignatureStrikes.Hooks.SignatureStrikesMissionLogic(");
        StringAssert.Contains(source, "Features.SignatureStrikes.ISignatureStrikeService");
        StringAssert.Contains(source, "Features.SignatureStrikes.Hooks.ISignatureAgentRoster");
    }
}
