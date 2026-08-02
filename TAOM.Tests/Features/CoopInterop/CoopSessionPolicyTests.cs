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

    // --- MayWriteSaveBackedState -----------------------------------------------------------------

    [TestMethod]
    public void MayWriteSaveBackedState_CoopClient_ReturnsFalse()
    {
        // The regression this pins: SiegeDefenseService.GrantReward set the save-serialized
        // RewardClaimed flag unconditionally, so a co-op client claiming its own siege reward wrote
        // per-peer state into the host's save record.
        Assert.IsFalse(CoopSessionPolicy.MayWriteSaveBackedState(isCoopClient: true));
    }

    [TestMethod]
    public void MayWriteSaveBackedState_HostOrSingleplayer_ReturnsTrue()
    {
        Assert.IsTrue(CoopSessionPolicy.MayWriteSaveBackedState(isCoopClient: false));
    }

    // --- ShouldDeferToHost: the full five-row truth table ----------------------------------------

    [TestMethod]
    public void ShouldDeferToHost_NoCoopModule_DoesNotDefer()
    {
        Assert.IsFalse(CoopSessionPolicy.ShouldDeferToHost(false, false, false, false));
    }

    [TestMethod]
    public void ShouldDeferToHost_UnprobeableCoopMod_Defers()
    {
        // BannerlordTogether: TAOM cannot read host/client role, so the only safe answer is to yield
        // on every peer. Gating on IsAuthority alone would get this row wrong -- it fails open, so
        // BOTH peers would report authoritative and nothing would gate.
        Assert.IsTrue(CoopSessionPolicy.ShouldDeferToHost(true, roleProbeAvailable: false, false, false));
    }

    [TestMethod]
    public void ShouldDeferToHost_CoopInstalledButPlayingSolo_DoesNotDefer()
    {
        // The regression the presence-only gate shipped: a solo player who merely had the Coop
        // module enabled silently lost TAOM's War of the Ring diplomacy rules.
        Assert.IsFalse(CoopSessionPolicy.ShouldDeferToHost(true, true, sessionActive: false, false));
    }

    [TestMethod]
    public void ShouldDeferToHost_CoopHost_DoesNotDefer()
    {
        // The other half of that regression -- the HOST is the authority and must keep enforcing.
        Assert.IsFalse(CoopSessionPolicy.ShouldDeferToHost(true, true, true, isServer: true));
    }

    [TestMethod]
    public void ShouldDeferToHost_CoopClient_Defers()
    {
        Assert.IsTrue(CoopSessionPolicy.ShouldDeferToHost(true, true, true, isServer: false));
    }
}
