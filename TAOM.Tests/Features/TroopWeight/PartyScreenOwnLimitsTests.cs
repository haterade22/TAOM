using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.TroopWeight;

/// <summary>
/// #1430: the party screen copies each side's size limit once, at Initialize, but the normal screen edits the live
/// roster, so the party's own (weight-deflated) limit moves with the first heavy troop moved. Whether a side's limit
/// is the party's own is therefore decided once, at screen open, and kept per screen.
/// </summary>
[TestClass]
public class PartyScreenOwnLimitsTests
{
    [TestMethod]
    public void ARecordedScreen_KeepsItsVerdictPerSide()
    {
        var screen = new object();
        PartyScreenOwnLimits.Record(screen, leftIsOwn: false, rightIsOwn: true);

        Assert.AreEqual(true, PartyScreenOwnLimits.IsOwnLimit(screen, rightSide: true));
        Assert.AreEqual(false, PartyScreenOwnLimits.IsOwnLimit(screen, rightSide: false));
    }

    [TestMethod]
    public void AnUnrecordedScreen_HasNoVerdict()
        => Assert.IsNull(PartyScreenOwnLimits.IsOwnLimit(new object(), rightSide: true));

    [TestMethod]
    public void ASecondInitialize_ReplacesTheVerdict()
    {
        var screen = new object();
        PartyScreenOwnLimits.Record(screen, leftIsOwn: true, rightIsOwn: true);
        PartyScreenOwnLimits.Record(screen, leftIsOwn: false, rightIsOwn: false);

        Assert.AreEqual(false, PartyScreenOwnLimits.IsOwnLimit(screen, rightSide: true));
    }

    [TestMethod]
    public void TwoScreens_DoNotShareAVerdict()
    {
        var a = new object();
        var b = new object();
        PartyScreenOwnLimits.Record(a, leftIsOwn: true, rightIsOwn: true);
        PartyScreenOwnLimits.Record(b, leftIsOwn: false, rightIsOwn: false);

        Assert.AreEqual(true, PartyScreenOwnLimits.IsOwnLimit(a, rightSide: true));
        Assert.AreEqual(false, PartyScreenOwnLimits.IsOwnLimit(b, rightSide: true));
    }
}
