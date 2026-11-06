using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// Where Morgul Blades is wired (phase2-plan.md Slice 9), read from the source: the director applies the run's poison ticks after the wave's soldiers exist, publishes its want for hits for the engine's
/// threads, and flushes the wave's check at the clear and at the end; the host reads that want without touching the run; the poison ticks reach only the effects adapter.
/// </summary>
[TestClass]
public class LongDefeatBladesWiringTests
{
    private const string Effects = "Main/Features/LongDefeat/LongDefeatDirector.Effects.cs";
    private const string Blades = "Main/Features/LongDefeat/LongDefeatDirector.Blades.cs";

    // The text of the first block after the marker, by brace depth.
    private static string BodyOf(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " is not in the source");
        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(open, i - open + 1);
        }

        Assert.Fail(marker + " has no closing brace");
        return "";
    }

    [TestMethod]
    public void TheDirector_AppliesThePoisonTicksAfterTheAttackerBatches_AndPublishesTheWantForHitsLast()
    {
        var apply = BodyOf(RepoPaths.ReadSource(Effects, stripComments: true), "private void Apply(RunEffects fx)");

        var attackers = apply.IndexOf("foreach (var batch in fx.AttackerBatches)", StringComparison.Ordinal);
        var poison = apply.IndexOf("ApplyPoison(fx.PoisonTicks)", StringComparison.Ordinal);
        var wants = apply.IndexOf("_wantsHits = _run.PoisonWave", StringComparison.Ordinal);
        var finish = apply.IndexOf("Finish();", StringComparison.Ordinal);
        Assert.IsTrue(attackers >= 0 && poison > attackers, "a tick needs the attackers the wave just spawned");
        Assert.IsTrue(wants > poison && wants < finish, "the want for hits follows what the tick did, before the end of the run is written");
    }

    [TestMethod]
    public void TheWavesBladesCheck_IsFlushedAtTheClearAndAtTheEnd_AndTheWantEndsAtShutdown()
    {
        var source = RepoPaths.ReadSource(Effects, stripComments: true);

        StringAssert.Contains(BodyOf(source, "private void OnWaveCleared("), "FlushBlades()");
        StringAssert.Contains(BodyOf(source, "private void Finish()"), "FlushBlades()");
        StringAssert.Contains(BodyOf(RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.cs", stripComments: true), "public void Shutdown()"), "_wantsHits = false");
    }

    [TestMethod]
    public void AHit_IsResolvedToKeysOnTheCallingThread_AndTheDoseIsParkedOnTheMainThread()
    {
        var hit = BodyOf(RepoPaths.ReadSource(Blades, stripComments: true), "public void OnHit(");

        var wants = hit.IndexOf("_wantsHits", StringComparison.Ordinal);
        var keys = hit.IndexOf("TryHitKeys(", StringComparison.Ordinal);
        var parked = hit.IndexOf("_deferred.RunOrDefer(HitSite", StringComparison.Ordinal);
        var dose = hit.IndexOf("_run.DefenderHit(", StringComparison.Ordinal);
        Assert.IsTrue(wants >= 0 && keys > wants && parked > keys && dose > parked, "want, then the registry's keys here, then the run's call inside the parked callback");
    }

    [TestMethod]
    public void TheRemovalCallback_MakesTheRunForgetTheAgentsKey()
    {
        var removed = BodyOf(RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.cs", stripComments: true), "public void OnAgentRemoved(");

        StringAssert.Contains(removed, "_run.Forget(removal.Key)");
        Assert.IsTrue(removed.IndexOf("_run.Forget(", StringComparison.Ordinal) < removed.IndexOf("switch (removal.Kind)", StringComparison.Ordinal), "forgotten before the removal is booked");
    }

    [TestMethod]
    public void OnlyTheEffectsAdapter_AppliesPoison_AndTheRunNeverTouchesTheEngine()
    {
        foreach (var path in new[] { "Main/Features/LongDefeat/Domain/SurvivalRun.Poison.cs", "Main/Features/LongDefeat/Domain/LdPoisonBook.cs", "Main/Features/LongDefeat/Domain/PoisonOwner.cs" })
            Assert.IsFalse(RepoPaths.ReadSource(path, stripComments: true).Contains("TaleWorlds"), path + " is domain code: no engine types");

        var director = RepoPaths.ReadSource(Blades, stripComments: true);
        StringAssert.Contains(director, "_effects.ApplyPoison(");
        Assert.IsFalse(director.Contains("_field.ApplyPoison"), "the field adapter does not kill");
    }
}
