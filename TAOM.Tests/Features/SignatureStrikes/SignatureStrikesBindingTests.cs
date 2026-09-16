using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SignatureStrikes.Domain;
using TAOM.Features.SignatureStrikes.Hooks;
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

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

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

    [TestMethod]
    public void SubModule_WiresTheLogicAndHandsTheModelItsServices()
    {
        // The model's two signature params are OPTIONAL (null = feature absent), so forgetting to
        // pass them compiles clean and silently drops the guaranteed knockdown. Pin the call site.
        var path = Path.Combine(RepoRoot, "Main", "SubModule.cs");
        if (!File.Exists(path))
            Assert.Inconclusive("Main/SubModule.cs not found; run from the repo checkout.");

        var source = File.ReadAllText(path);

        StringAssert.Contains(source, "new Features.SignatureStrikes.Hooks.SignatureStrikesMissionLogic(");
        StringAssert.Contains(source, "Features.SignatureStrikes.ISignatureStrikeService");
        StringAssert.Contains(source, "Features.SignatureStrikes.Hooks.ISignatureAgentRoster");
    }
}
