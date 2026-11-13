using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.RaceAge.Models;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.RaceAge;

/// <summary>
/// #1141: pregnancy ticks run on the co-op server, where <c>Hero.MainHero</c> is the server's own hero.
/// TAOM's model replaces vanilla's method without calling it, so Coop's own prefix (which asks
/// <c>IsPlayerHero</c>) never runs. The model must ask the player query, or a guest's marriage gets the
/// AI clan brake and the AI race bonus.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // builds engine Heroes
public class TaomPregnancyPlayerTests
{
    private static Hero AHero() => (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

    [DataTestMethod]
    [DataRow(PlayerControl.Player, PlayerControl.NotPlayer, true)]
    [DataRow(PlayerControl.NotPlayer, PlayerControl.Player, true)]
    [DataRow(PlayerControl.NotPlayer, PlayerControl.NotPlayer, false)]
    // Unknown is not a player here: an unreadable registry must not lift the brake for every AI clan.
    [DataRow(PlayerControl.Unknown, PlayerControl.Unknown, false)]
    public void IsPlayerMarriage_AnswersFromTheQuery(PlayerControl ofHero, PlayerControl ofSpouse, bool expected)
    {
        var hero = AHero();
        var spouse = AHero();
        var players = Substitute.For<IPlayerControlQuery>();
        players.OfHero(hero).Returns(ofHero);
        players.OfHero(spouse).Returns(ofSpouse);

        Assert.AreEqual(expected, TaomPregnancyModel.IsPlayerMarriage(hero, spouse, players));
        players.Received(1).OfHero(hero);
    }
}
