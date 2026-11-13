using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.UncapturableHeroes;
using TAOM.Features.UncapturableHeroes.Hooks;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.UncapturableHeroes;

/// <summary>
/// #1139: a co-op player's hero belongs to vanilla and Coop, never to this feature. On the Coop
/// server <c>Hero.MainHero</c> is the server's own hero, so both seams ask
/// <see cref="IPlayerControlQuery"/> who the players are. Without the query a defeated player of an
/// uncapturable race became a fugitive and Coop's captivity flow never ran.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // NamedHero runs the engine's StringId setter
public class UncapturablePlayerHeroTests
{
    private IUncapturableHeroService _service = null!;
    private IPlayerControlQuery _players = null!;

    [TestInitialize]
    public void SetUp()
    {
        _service = Substitute.For<IUncapturableHeroService>();
        _service.ShouldDenyCapture(Arg.Any<string>(), Arg.Any<int?>()).Returns(true);
        _service.TryPreventCapture(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(true);
        _players = Substitute.For<IPlayerControlQuery>();

        var logger = Substitute.For<IModLogger>();
        Hero_CanBecomePrisoner_Patch.Initialize(_service, logger, _players);
        TakePrisonerAction_Apply_Patch.Initialize(_service, logger, _players);
    }

    [TestCleanup]
    public void TearDown()
    {
        Hero_CanBecomePrisoner_Patch.ResetForUnload();
        TakePrisonerAction_Apply_Patch.ResetForUnload();
    }

    private static Hero NamedHero(string id)
    {
        var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        hero.StringId = id;
        return hero;
    }

    [DataTestMethod]
    [DataRow(PlayerControl.Player)]
    [DataRow(PlayerControl.Unknown)]
    public void ApplyPrefix_HeroIsAPlayerOrUnknown_LeavesTheCaptureToVanilla(PlayerControl control)
    {
        var hero = NamedHero("lord_nazgul_1");
        _players.OfHero(hero).Returns(control);

        var runVanilla = TakePrisonerAction_Apply_Patch.Prefix(null!, hero);

        Assert.IsTrue(runVanilla);
        // Without a game Hero.MainHero throws and the seam's catch also defers, so the deferral alone
        // proves nothing: the seam must have asked the query.
        _players.Received(1).OfHero(hero);
        _service.DidNotReceiveWithAnyArgs().TryPreventCapture(default!, default, default!, default);
    }

    [TestMethod]
    public void ApplyPrefix_HeroIsNotAPlayer_AsksTheService()
    {
        var hero = NamedHero("lord_nazgul_1");
        _players.OfHero(hero).Returns(PlayerControl.NotPlayer);

        var runVanilla = TakePrisonerAction_Apply_Patch.Prefix(null!, hero);

        Assert.IsFalse(runVanilla, "The service made him a fugitive, so vanilla must not capture him.");
        _service.Received(1).TryPreventCapture("lord_nazgul_1", Arg.Any<int?>(), Arg.Any<string>(), false);
    }

    [DataTestMethod]
    [DataRow(PlayerControl.Player)]
    [DataRow(PlayerControl.Unknown)]
    public void CanBecomePrisonerPostfix_HeroIsAPlayerOrUnknown_KeepsVanillasAnswer(PlayerControl control)
    {
        var hero = NamedHero("lord_nazgul_1");
        _players.OfHero(hero).Returns(control);
        var result = true;

        Hero_CanBecomePrisoner_Patch.Postfix(hero, ref result);

        Assert.IsTrue(result);
        _players.Received(1).OfHero(hero);
        _service.DidNotReceiveWithAnyArgs().ShouldDenyCapture(default!, default);
    }

    [TestMethod]
    public void CanBecomePrisonerPostfix_HeroIsNotAPlayer_AsksTheService()
    {
        var hero = NamedHero("lord_nazgul_1");
        _players.OfHero(hero).Returns(PlayerControl.NotPlayer);
        var result = true;

        Hero_CanBecomePrisoner_Patch.Postfix(hero, ref result);

        Assert.IsFalse(result);
        _service.Received(1).ShouldDenyCapture("lord_nazgul_1", Arg.Any<int?>());
    }
}
