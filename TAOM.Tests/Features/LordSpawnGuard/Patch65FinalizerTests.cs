using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.LordSpawnGuard.Hooks;

namespace TAOM.Tests.Features.LordSpawnGuard;

/// <summary>
/// Behavior tests for <see cref="Patch65_LandlessCultureSpawnGuard"/>'s finalizer — the backstop
/// that converts vanilla `SpawnLordParty`'s unguarded
/// `Settlement.All.First(x =&gt; x.Culture == hero.Culture)` from a CTD into a skipped party (#374).
///
/// The exception filter is the whole safety contract: suppressing only
/// <see cref="InvalidOperationException"/> is what keeps every other engine fault visible. Nothing
/// pinned that before — a refactor widening the type check would silently swallow every exception
/// out of `SpawnLordParty` with a green suite. Mirrors
/// <c>Patch62MovieReleaseAvGuardTests</c>, the repo's existing precedent for this shape.
///
/// The target binding (<c>HeroSpawnCampaignBehavior.SpawnLordParty</c>) is covered by
/// <c>Patch65LandlessCultureSpawnGuardBindingTests</c> and <c>HarmonyPatchBindingTests</c>.
///
/// `hero` is passed null throughout: the finalizer's reporting path swallows its own faults, so a
/// null hero exercises the filter without needing a live campaign.
/// </summary>
[TestClass]
public class Patch65FinalizerTests
{
    [TestMethod]
    public void Finalizer_InvalidOperationException_IsSuppressed()
    {
        // The exact exception vanilla's First() throws on an empty match.
        var ioe = new InvalidOperationException("Sequence contains no matching element");
        MobileParty result = null;

        var returned = Patch65_LandlessCultureSpawnGuard.Finalizer(ioe, null, ref result);

        Assert.IsNull(returned, "InvalidOperationException must be suppressed (finalizer returns null).");
        Assert.IsNull(result, "The suppressed path must leave a null party — the caller null-checks it.");
    }

    [TestMethod]
    public void Finalizer_OtherException_PropagatesUntouched()
    {
        // An NRE out of SpawnLordParty is a different bug and must stay visible.
        var nre = new NullReferenceException("unrelated");
        MobileParty result = null;

        var returned = Patch65_LandlessCultureSpawnGuard.Finalizer(nre, null, ref result);

        Assert.AreSame(nre, returned, "Only InvalidOperationException is suppressed; everything else propagates.");
    }

    [TestMethod]
    public void Finalizer_NoException_PassesThroughNull()
    {
        MobileParty result = null;

        var returned = Patch65_LandlessCultureSpawnGuard.Finalizer(null, null, ref result);

        Assert.IsNull(returned, "The success path must not invent an exception.");
    }
}
