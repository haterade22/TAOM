using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Adapters;

/// <summary>
/// #1141: the War Chronicle escape pass and rally run on the co-op server, where <c>Clan.PlayerClan</c> is
/// the server's own clan. A remote player's clan must still count as a player's, or TAOM's help for AI
/// lords and realms reaches a human player.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // builds an engine Clan
public class PlayerClanCheckTests
{
    private static Clan AClan() => (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

    [DataTestMethod]
    [DataRow(PlayerControl.Player, true)]
    [DataRow(PlayerControl.Unknown, true)]
    [DataRow(PlayerControl.NotPlayer, false)]
    public void IsAPlayersClan_AnswersFromTheQuery(PlayerControl control, bool expected)
    {
        var clan = AClan();
        var players = Substitute.For<IPlayerControlQuery>();
        players.OfClan(clan).Returns(control);

        Assert.AreEqual(expected, PlayerClanCheck.IsAPlayersClan(clan, players));
        players.Received(1).OfClan(clan);
    }

    [TestMethod]
    public void IsAPlayersClan_NoClan_IsNotAPlayers()
    {
        var players = Substitute.For<IPlayerControlQuery>();

        Assert.IsFalse(PlayerClanCheck.IsAPlayersClan(null, players));
    }
}
