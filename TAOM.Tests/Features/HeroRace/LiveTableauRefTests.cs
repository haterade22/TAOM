using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// The sentinel contract behind the tuner's <c>.</c> shorthand.
///
/// <para><c>LastRace</c> returning -1 is what makes <c>.</c> refuse rather than resolve to a race
/// nobody is looking at. That refusal is the whole safety property, so it gets pinned even though
/// the rest of the class cannot be reached from here.</para>
///
/// <para><b>Why the coverage stops here, demonstrated rather than assumed.</b> <c>Set</c>,
/// <c>TryGet</c> and <c>ClearIf</c> all take or return a <c>CharacterTableau</c>, and TAOM.Tests
/// carries no reference to <c>TaleWorlds.MountAndBlade.View</c>. Calling them does not fail at
/// runtime, it fails to COMPILE with CS0012, which is why every tableau test in this folder resolves
/// its types through <c>AccessTools.TypeByName</c> instead. Adding the assembly reference to pick up
/// three assertions is not worth the blast radius on a test host that does not load the engine.</para>
///
/// <para>What covers the rest: <c>HarmonyPatchBindingTests</c> resolves the
/// <c>CharacterTableau.OnFinalize</c> target that drives <c>ClearIf</c>, and the conditional itself
/// is a <c>ReferenceEquals</c>. The behaviour it protects (closing a screen must stop <c>.</c>
/// resolving) is on the in-game checklist in issue #502.</para>
/// </summary>
[TestClass]
public class LiveTableauRefTests
{
    [TestInitialize]
    public void Setup() => LiveTableauRef.Clear();

    [TestCleanup]
    public void Cleanup() => LiveTableauRef.Clear();

    [TestMethod]
    public void LastRace_WithNothingEverShown_IsTheSentinel()
        => Assert.AreEqual(-1, LiveTableauRef.LastRace,
            "The tuner's '.' shorthand refuses on -1. Any other value makes it resolve to a race "
            + "no tableau is showing.");

    [TestMethod]
    public void Clear_ResetsTheRaceToTheSentinel()
    {
        LiveTableauRef.Clear();

        Assert.AreEqual(-1, LiveTableauRef.LastRace);
    }

    // Clear is reachable from an OnFinalize postfix, which can fire repeatedly during teardown.
    [TestMethod]
    public void Clear_IsIdempotent()
    {
        LiveTableauRef.Clear();
        LiveTableauRef.Clear();

        Assert.AreEqual(-1, LiveTableauRef.LastRace);
    }
}
