using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CoopInterop.Missions;

namespace TAOM.Tests.Features.CoopInterop.Missions;

/// <summary>
/// The static face of the authority: with no resolver, or a resolver that cannot answer, every mission check
/// answers as singleplayer does ("owned"); with one, it delegates and resolves once.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CoopMissionOwnershipTests
{
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    [TestCleanup]
    public void ClearResolver() => CoopMissionOwnership.UseResolver(null);

    [TestMethod]
    public void IsOwnedHere_NoContainer_Owned()
    {
        // The default resolver reads IoC, which a unit-test host never configures: the lookup fails and the
        // check answers as singleplayer does.
        CoopMissionOwnership.UseResolver(null);

        Assert.IsTrue(CoopMissionOwnership.IsOwnedHere(BareAgent()));
    }

    [TestMethod]
    public void IsOwnedHere_ResolverThrows_OwnedAndAsksAgain()
    {
        var calls = 0;
        CoopMissionOwnership.UseResolver(() => { calls++; throw new ObjectDisposedException("container"); });

        Assert.IsTrue(CoopMissionOwnership.IsOwnedHere(BareAgent()));
        Assert.IsTrue(CoopMissionOwnership.IsOwnedHere(BareAgent()));
        Assert.AreEqual(2, calls, "a failed resolve is not cached");
    }

    [TestMethod]
    public void IsOwnedHere_ResolverReturnsNull_OwnedAndAsksAgain()
    {
        var calls = 0;
        CoopMissionOwnership.UseResolver(() => { calls++; return null; });

        Assert.IsTrue(CoopMissionOwnership.IsOwnedHere(BareAgent()));
        Assert.IsTrue(CoopMissionOwnership.IsOwnedHere(BareAgent()));
        Assert.AreEqual(2, calls, "a null answer is not cached");
    }

    [TestMethod]
    public void IsOwnedHere_WithAuthority_DelegatesLiveAndResolvesOnce()
    {
        var authority = Substitute.For<ICoopMissionAuthority>();
        var agent = BareAgent();
        authority.IsLocallyControlled(agent).Returns(true, false, true);
        var resolves = 0;
        CoopMissionOwnership.UseResolver(() => { resolves++; return authority; });

        Assert.IsTrue(CoopMissionOwnership.IsOwnedHere(agent));
        Assert.IsFalse(CoopMissionOwnership.IsOwnedHere(agent), "the answer is read live, never cached");
        Assert.IsTrue(CoopMissionOwnership.IsOwnedHere(agent));
        Assert.AreEqual(1, resolves);
    }
}
