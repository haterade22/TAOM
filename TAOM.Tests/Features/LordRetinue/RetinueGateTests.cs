using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CoopInterop;
using TAOM.Features.LordRetinue;

namespace TAOM.Tests.Features.LordRetinue;

[TestClass]
public class RetinueGateTests
{
    private static bool Gate(
        bool isLordParty = true, bool isMainParty = false, bool isAuthority = true, bool defer = false,
        PlayerControl party = PlayerControl.NotPlayer, PlayerControl clan = PlayerControl.NotPlayer)
        => RetinueGate.MayChange(isLordParty, isMainParty, isAuthority, defer, party, clan);

    [TestMethod]
    public void MayChange_AiLordPartyOnTheAuthority_ReturnsTrue()
    {
        Assert.IsTrue(Gate());
    }

    [TestMethod]
    public void MayChange_NotALordParty_ReturnsFalse()
    {
        Assert.IsFalse(Gate(isLordParty: false));
    }

    [TestMethod]
    public void MayChange_MainParty_ReturnsFalse()
    {
        Assert.IsFalse(Gate(isMainParty: true));
    }

    [TestMethod]
    public void MayChange_PeerWithoutAuthority_ReturnsFalse()
    {
        Assert.IsFalse(Gate(isAuthority: false));
    }

    [TestMethod]
    public void MayChange_PeerThatDefersToTheHost_ReturnsFalse()
    {
        Assert.IsFalse(Gate(defer: true));
    }

    [DataTestMethod]
    [DataRow(PlayerControl.Player)]
    [DataRow(PlayerControl.Unknown)]
    public void MayChange_PartyDrivenByAPlayerOrUnreadable_ReturnsFalse(PlayerControl control)
    {
        Assert.IsFalse(Gate(party: control));
    }

    [DataTestMethod]
    [DataRow(PlayerControl.Player)]
    [DataRow(PlayerControl.Unknown)]
    public void MayChange_ClanOfAPlayerOrUnreadable_ReturnsFalse(PlayerControl control)
    {
        Assert.IsFalse(Gate(clan: control));
    }
}
