using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The lines that carry the Hammer of the Underworld and Winged Terror into the engine and that no unit test can run without a mission (phase2-plan.md Slice 8): the engine write's
/// arithmetic and the lack of a Reset, the pulse's reads and its single write, and the order the director applies them in. Read off the source, so deleting one fails here.
/// </summary>
[TestClass]
public class LongDefeatPowersWiringTests
{
    private const string EffectsAdapter = "Main/Adapters/LongDefeatEffectsAdapter.cs";
    private const string Effects = "Main/Features/LongDefeat/LongDefeatDirector.Effects.cs";

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
    public void TheEngineWrite_SetsTheTypesVanillaValueTimesTheFactor_ScalesTheCurrentHitPoints_AndNeverResets()
    {
        var source = RepoPaths.ReadSource(EffectsAdapter, stripComments: true);
        var write = BodyOf(source, "public LdEngineReport SetEngineHitPoints(");
        var harden = BodyOf(source, "private static void Harden(");

        StringAssert.Contains(write, "LiveEngines<BatteringRam>()");
        StringAssert.Contains(write, "LiveEngines<SiegeTower>()");
        StringAssert.Contains(harden, "weapon.GetSiegeEngineType().BaseHitPoints");
        StringAssert.Contains(harden, "component.MaxHitPoint = vanilla * factor");
        StringAssert.Contains(harden, "component.HitPoint *= component.MaxHitPoint / before");
        Assert.IsFalse((write + harden).Contains("Reset("), "Reset would restore an engine the defenders destroyed");
    }

    [TestMethod]
    public void TheLiveEngines_AreTheOnesTheBattlefieldAdapterKept_NotDisabledAndVisible()
    {
        var effects = RepoPaths.ReadSource(EffectsAdapter, stripComments: true);
        var field = RepoPaths.ReadSource("Main/Adapters/LongDefeatBattlefieldAdapter.cs", stripComments: true);

        StringAssert.Contains(BodyOf(effects, "private IEnumerable<T> LiveEngines<T>()"), "!w.IsDisabled && w.GameEntity.IsVisibleIncludeParents()");
        StringAssert.Contains(field, "!w.IsDisabled && w.GameEntity.IsVisibleIncludeParents()");
    }

    [TestMethod]
    public void ThePulse_ReadsTheAttackersOnce_LowersOnlyDefenders_ThroughOneChangeMorale_AndTimesItself()
    {
        var source = RepoPaths.ReadSource(EffectsAdapter, stripComments: true);
        var pulse = BodyOf(source, "public LdPulseResult PulseMorale(");

        StringAssert.Contains(pulse, "StandingAttackers()");
        StringAssert.Contains(pulse, "_mission.DefenderTeam?.ActiveAgents");
        StringAssert.Contains(pulse, "agent.GetMorale()");
        StringAssert.Contains(pulse, "agent.ChangeMorale(-drain)");
        Assert.AreEqual(1, pulse.Split(new[] { "ChangeMorale(" }, StringSplitOptions.None).Length - 1, "the one write is the defenders' lambda");
        Assert.IsFalse(pulse.Contains("SetMorale("), "the engine's clamped add is the write");
        StringAssert.Contains(pulse, "Stopwatch.StartNew()");
        StringAssert.Contains(pulse, "Milliseconds = watch.Elapsed.TotalMilliseconds");
    }

    [TestMethod]
    public void TheGateReport_ReadsEveryMissionObject_BecauseADestroyedGateLeavesTheActiveList()
    {
        var source = RepoPaths.ReadSource(EffectsAdapter, stripComments: true);
        var write = BodyOf(source, "public LdEngineReport SetEngineHitPoints(");

        StringAssert.Contains(write, "_mission.MissionObjects.FindAllWithType<CastleGate>()");
        Assert.IsFalse(write.Contains("ActiveMissionObjects.FindAllWithType<CastleGate>"), "Mission.OnObjectDisabled drops a destroyed gate from ActiveMissionObjects (Mission.cs:3060), so it could never read destroyed");
    }

    [TestMethod]
    public void ThePulse_NeverDrainsTheMainAgent_SoAnAutoplayLordIsTreatedAsAHumanOne()
    {
        var source = RepoPaths.ReadSource(EffectsAdapter, stripComments: true);
        var pulse = BodyOf(source, "public LdPulseResult PulseMorale(");

        StringAssert.Contains(pulse, "a.IsHuman && a.IsActive() && !a.IsMainAgent");
    }

    [TestMethod]
    public void TheHammerCheck_TakesItsExpectedFactorFromThePlan_NotFromTheValueTheWriteWasGiven()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.Powers.cs", stripComments: true);
        var record = BodyOf(source, "private void RecordHammer(");

        StringAssert.Contains(record, "engineHitPointsPercent");
        Assert.IsFalse(record.Contains("_engineFactor"), "a check that shared the production factor could not catch a change in it");
    }

    [TestMethod]
    public void TheDirector_WritesTheEnginesAfterThePublish_AndBeforeAnySoldierSpawns()
    {
        var source = RepoPaths.ReadSource(Effects, stripComments: true);
        var apply = BodyOf(source, "private void Apply(RunEffects fx)");

        var publish = apply.IndexOf("PublishStats(", StringComparison.Ordinal);
        var engines = apply.IndexOf("SetEngineHitPoints(engines)", StringComparison.Ordinal);
        var spawn = apply.IndexOf("SpawnBatch(", StringComparison.Ordinal);
        Assert.IsTrue(publish >= 0 && engines > publish && spawn > engines, "the first batch of a hide wave and the engines of a hammer wave are written before anyone spawns");
    }

    [TestMethod]
    public void TheDirector_PulsesAfterTheAttackerBatches_AndFlushesTheTerrorCheckAtTheClearAndTheEnd()
    {
        var source = RepoPaths.ReadSource(Effects, stripComments: true);
        var apply = BodyOf(source, "private void Apply(RunEffects fx)");

        var attackers = apply.IndexOf("foreach (var batch in fx.AttackerBatches)", StringComparison.Ordinal);
        var pulse = apply.IndexOf("PulseMorale(pulse)", StringComparison.Ordinal);
        Assert.IsTrue(attackers >= 0 && pulse > attackers, "a pulse needs the attackers the wave just spawned");
        StringAssert.Contains(BodyOf(source, "private void OnWaveCleared("), "FlushTerror()");
        StringAssert.Contains(BodyOf(source, "private void Finish()"), "FlushTerror()");
    }

    [TestMethod]
    public void TheHammerAndTerror_KillNothing_SoTheBlowOwnerGateHasNothingToAllow()
    {
        foreach (var path in new[] { EffectsAdapter, "Main/Features/LongDefeat/LongDefeatDirector.Powers.cs", "Main/Features/LongDefeat/Domain/LdMoralePulse.cs" })
        {
            var source = RepoPaths.ReadSource(path, stripComments: true);
            Assert.IsFalse(source.Contains("KillAgentCheat") || source.Contains(".Die(") || source.Contains("new Blow("), path + " must not kill");
        }
    }
}
