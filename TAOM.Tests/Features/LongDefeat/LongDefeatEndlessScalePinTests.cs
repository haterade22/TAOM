using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The engine-facing lines of the endless scale that no test can run without a mission (phase2-plan.md Slice 2): read off the source, so a
/// refactor that drops the health write, the troop check or the publish fails here. The in-game proof is the <c>scale</c> check.
/// </summary>
[TestClass]
public class LongDefeatEndlessScalePinTests
{
    private const string FieldAdapter = "Main/Adapters/LongDefeatBattlefieldAdapter.cs";
    private const string EffectsAdapter = "Main/Adapters/LongDefeatEffectsAdapter.cs";
    private const string Launcher = "Main/Features/LongDefeat/Hooks/LongDefeatMissionLauncher.cs";

    private static string BodyOf(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " not found");
        var next = source.IndexOf("\n    public ", at + marker.Length, StringComparison.Ordinal);
        var nextPrivate = source.IndexOf("\n    private ", at + marker.Length, StringComparison.Ordinal);
        var end = new[] { next, nextPrivate }.Where(i => i > 0).DefaultIfEmpty(source.Length).Min();
        return source.Substring(at, end - at);
    }

    [TestMethod]
    public void SpawnBatch_ScalesEachSpawnedSoldiersHealth_ByTheBatchFactor_AfterItSpawns()
    {
        var source = RepoPaths.ReadSource(FieldAdapter, stripComments: true);
        var body = BodyOf(source, "public LdSpawnReport SpawnBatch(");

        var spawn = body.IndexOf("Spawn(troop,", StringComparison.Ordinal);
        var scale = body.IndexOf("ScaleHealth(agent, batch.HealthFactor)", StringComparison.Ordinal);
        Assert.IsTrue(spawn >= 0 && scale > spawn, "the write comes after the soldier exists");
    }

    [TestMethod]
    public void ScaleHealth_WritesTheBaseTheLimitAndTheHealth_AndSkipsFactorOne()
    {
        var source = RepoPaths.ReadSource(FieldAdapter, stripComments: true);
        var body = BodyOf(source, "private static void ScaleHealth(");

        StringAssert.Contains(body, "if (factor == 1f)");
        StringAssert.Contains(body, "agent.BaseHealthLimit *= factor;");
        StringAssert.Contains(body, "agent.HealthLimit = agent.BaseHealthLimit;");
        StringAssert.Contains(body, "agent.Health = agent.HealthLimit;");
    }

    [TestMethod]
    public void TheLauncher_ChecksEveryTroopTheRunCanSpawn_ThroughRunPlanTroops()
    {
        var source = RepoPaths.ReadSource(Launcher, stripComments: true);

        StringAssert.Contains(source, "RunPlanTroops.AllTroopIds(plan)");
        Assert.IsFalse(source.Contains("Troops(plan)"), "a second list of the troops would let a tier target slip past the launch check");
    }

    [TestMethod]
    public void TheEffectsAdapter_PublishesThroughTheCombatHook_AndReadsTheCharacterMaximum()
    {
        var source = RepoPaths.ReadSource(EffectsAdapter, stripComments: true);

        StringAssert.Contains(source, "LongDefeatCombatHooks.Publish(_mission, table.AttackerDamage)");
        StringAssert.Contains(source, "character.MaxHitPoints()");
        StringAssert.Contains(source, "agent.HealthLimit");
    }

    [TestMethod]
    public void TheEffectsAdapter_SamplesOnlyAStandingAttacker_NeverARunnerLeftFromAnEarlierWave()
    {
        var source = RepoPaths.ReadSource(EffectsAdapter, stripComments: true);
        var read = BodyOf(source, "public LdScaleSample ReadScale()");

        StringAssert.Contains(read, "!agent.IsRunningAway", "a runner carries the health of the wave it came from, and the wave clears without it");
    }

    [TestMethod]
    public void TheMissionLogic_BuildsTheEffectsAdapterOnItsOwnMission_SoTheDamageFactorHasAReader()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/Hooks/LongDefeatMissionLogic.cs", stripComments: true);

        StringAssert.Contains(source, "() => new LongDefeatEffectsAdapter(Mission)");
    }

    [TestMethod]
    public void TheDirector_PublishesBeforeItSpawnsAnything()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.Effects.cs", stripComments: true);
        var apply = BodyOf(source, "private void Apply(RunEffects fx)");

        Assert.IsTrue(apply.IndexOf("PublishStats(", StringComparison.Ordinal) < apply.IndexOf("SpawnBatch(", StringComparison.Ordinal));
    }
}
