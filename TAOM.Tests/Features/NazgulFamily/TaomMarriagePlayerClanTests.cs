using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.NazgulFamily.Models;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.NazgulFamily;

/// <summary>
/// #1371: a marriage between co-op guests is checked on the server, where <c>Clan.PlayerClan</c> is the server's
/// own clan. The alignment rule must ask the player query which clans are players', or a guest's marriage gets the
/// AI toggle.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // builds engine Clans
public class TaomMarriagePlayerClanTests
{
    private static Clan AClan() => (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

    [DataTestMethod]
    [DataRow(PlayerControl.Player, PlayerControl.NotPlayer, true)]
    [DataRow(PlayerControl.NotPlayer, PlayerControl.Player, true)]
    [DataRow(PlayerControl.NotPlayer, PlayerControl.NotPlayer, false)]
    // Unknown is not a player here, as in TaomPregnancyModel: an unreadable registry keeps the AI rule.
    [DataRow(PlayerControl.Unknown, PlayerControl.Unknown, false)]
    public void InvolvesPlayerClan_AnswersFromTheQuery(PlayerControl ofFirst, PlayerControl ofSecond, bool expected)
    {
        var first = AClan();
        var second = AClan();
        var players = Substitute.For<IPlayerControlQuery>();
        players.OfClan(first).Returns(ofFirst);
        players.OfClan(second).Returns(ofSecond);

        Assert.AreEqual(expected, TaomMarriageModel.InvolvesPlayerClan(first, second, players));
    }
}
