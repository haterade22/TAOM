using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>The gold grant behind <c>taom.fords_gold</c>: the match adds to a slot's purse, the session aims it at the human slot.</summary>
[TestClass]
public class FordsGrantGoldTests
{
    private static FordsSession Filled(FordsMatch match)
    {
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, Substitute.For<IFordsBattlefieldAdapter>(), default, default, new List<Vec3f>());
        return session;
    }

    [TestMethod]
    public void GrantGold_AddsToTheSlotAndReportsWhatWasAdded()
    {
        var match = Create();

        var added = match.GrantGold(WestSlot, 250);

        Assert.AreEqual(250, added);
        Assert.AreEqual(1250, match.GetView(WestSlot).Gold);
        Assert.AreEqual(1000, match.GetView(EastSlot).Gold, "the other slot's purse is untouched");
    }

    [TestMethod]
    public void GrantGold_PastTheCap_AddsOnlyWhatFits()
    {
        var match = Create();
        var max = Config().Economy.MaxGold;

        var added = match.GrantGold(WestSlot, max);

        Assert.AreEqual(max - 1000, added);
        Assert.AreEqual(max, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void GrantGold_NonPositiveOrUnknownSlot_AddsNothing()
    {
        var match = Create();

        Assert.AreEqual(0, match.GrantGold(WestSlot, 0));
        Assert.AreEqual(0, match.GrantGold(WestSlot, -50));
        Assert.AreEqual(0, match.GrantGold(99, 100));
        Assert.AreEqual(1000, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void Session_AddGold_GoesToTheHumanSlot()
    {
        var session = Filled(Create());

        var added = session.AddGold(300);

        Assert.AreEqual(300, added);
        Assert.AreEqual(1300, session.GetView()!.Gold);
    }

    [TestMethod]
    public void Session_AddGold_WhileInactive_AddsNothing()
    {
        Assert.AreEqual(0, new FordsSession().AddGold(300));
    }
}
