using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Pins the authority decision. The whole point of splitting this out of
/// <c>CoopSessionProvider</c> is that the reflection binder cannot run in a test host, but the
/// decision it feeds gates every world-mutating global-tick handler in TAOM — so it must be pinned
/// exactly, including its failure direction.
/// </summary>
[TestClass]
public class CoopSessionPolicyTests
{
    // --- IsAuthority ---------------------------------------------------------------------------

    [TestMethod]
    public void IsAuthority_NoSession_ReturnsTrue()
    {
        // Singleplayer. TAOM must behave exactly as it does today.
        Assert.IsTrue(CoopSessionPolicy.IsAuthority(sessionActive: false, isServer: false));
    }

    [TestMethod]
    public void IsAuthority_NoSessionButStickyServerFlag_ReturnsTrue()
    {
        // ModInformation.IsServer is never reset on session teardown, so it reads true forever
        // after hosting once. With no live session that must not matter.
        Assert.IsTrue(CoopSessionPolicy.IsAuthority(sessionActive: false, isServer: true));
    }

    [TestMethod]
    public void IsAuthority_SessionActiveAndServer_ReturnsTrue()
    {
        Assert.IsTrue(CoopSessionPolicy.IsAuthority(sessionActive: true, isServer: true));
    }

    [TestMethod]
    public void IsAuthority_SessionActiveAndClient_ReturnsFalse()
    {
        // The only case that stands TAOM down. Everything else keeps running.
        Assert.IsFalse(CoopSessionPolicy.IsAuthority(sessionActive: true, isServer: false));
    }

    // --- IsCoopClient --------------------------------------------------------------------------

    [TestMethod]
    public void IsCoopClient_NoSession_ReturnsFalse()
    {
        Assert.IsFalse(CoopSessionPolicy.IsCoopClient(sessionActive: false, isServer: false));
    }

    [TestMethod]
    public void IsCoopClient_NoSessionWithDefaultFalseServerFlag_ReturnsFalse()
    {
        // The exact trap: Common.ModInformation.IsClient is defined as !IsServer and IsServer
        // defaults to false, so reading IsClient alone reports TRUE for a solo player who merely
        // has the Coop module enabled. Requiring a live session is what prevents that.
        Assert.IsFalse(CoopSessionPolicy.IsCoopClient(sessionActive: false, isServer: false));
    }

    [TestMethod]
    public void IsCoopClient_SessionActiveAndClient_ReturnsTrue()
    {
        Assert.IsTrue(CoopSessionPolicy.IsCoopClient(sessionActive: true, isServer: false));
    }

    [TestMethod]
    public void IsCoopClient_SessionActiveAndServer_ReturnsFalse()
    {
        Assert.IsFalse(CoopSessionPolicy.IsCoopClient(sessionActive: true, isServer: true));
    }

    // --- invariant -----------------------------------------------------------------------------

    [TestMethod]
    public void IsAuthorityAndIsCoopClient_AreAlwaysMutuallyExclusive()
    {
        foreach (var session in new[] { true, false })
        foreach (var server in new[] { true, false })
        {
            Assert.AreNotEqual(
                CoopSessionPolicy.IsAuthority(session, server),
                CoopSessionPolicy.IsCoopClient(session, server),
                $"authority and client must never agree (sessionActive={session}, isServer={server})");
        }
    }
}
