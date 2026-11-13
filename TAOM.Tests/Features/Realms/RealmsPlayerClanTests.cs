using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Hooks;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// #1141: the weekly AI claims run on the co-op server, where <c>Clan.PlayerClan</c> and <c>Hero.MainHero</c>
/// are the server's own. A remote player's clan and its leader must still be left to the player, or the server
/// spends that clan's influence and gold on claims.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class RealmsPlayerClanTests
{
    private IPlayerControlQuery _players = null!;
    private RealmsCampaign _realms = null!;
    private RealmsCampaign? _previous;

    [ClassInitialize]
    public static void Init(TestContext _) => TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void SetUp()
    {
        _previous = LiegeArmyDiscount.Behavior;
        _players = Substitute.For<IPlayerControlQuery>();
        _realms = new RealmsCampaign(new NullRealmsPopupAdapter(), () => true, _players);
    }

    [TestCleanup]
    public void TearDown() => LiegeArmyDiscount.Behavior = _previous;

    private static Clan AClan() => (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

    private static Hero AHero() => (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

    [DataTestMethod]
    [DataRow(PlayerControl.Player, true)]
    [DataRow(PlayerControl.Unknown, true)]
    [DataRow(PlayerControl.NotPlayer, false)]
    public void IsAPlayersClan_AnswersFromTheQuery(PlayerControl control, bool expected)
    {
        var clan = AClan();
        _players.OfClan(clan).Returns(control);

        Assert.AreEqual(expected, _realms.IsAPlayersClan(clan));
        _players.Received(1).OfClan(clan);
    }

    [DataTestMethod]
    [DataRow(PlayerControl.Player, true)]
    [DataRow(PlayerControl.Unknown, true)]
    [DataRow(PlayerControl.NotPlayer, false)]
    public void IsAPlayersHero_AnswersFromTheQuery(PlayerControl control, bool expected)
    {
        var hero = AHero();
        _players.OfHero(hero).Returns(control);

        Assert.AreEqual(expected, _realms.IsAPlayersHero(hero));
        _players.Received(1).OfHero(hero);
    }
}
