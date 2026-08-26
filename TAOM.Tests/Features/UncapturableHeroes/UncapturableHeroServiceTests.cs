using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.UncapturableHeroes;
using TAOM.Features.UncapturableHeroes.Domain;

namespace TAOM.Tests.Features.UncapturableHeroes;

/// <summary>
/// Policy tests. Two of these guard rules that have shipped as bugs elsewhere in TAOM: the escape
/// must happen BEFORE the announce gate (harmony-patches.md "toggles gate I/O, never state
/// transitions"), and a failed escape must return false so the caller falls back to vanilla capture
/// rather than leaving the hero neither captured nor free.
/// </summary>
[TestClass]
public class UncapturableHeroServiceTests
{
    private const string HeroId = "lord_1_17";
    private const string HeroName = "Sauron";

    private IUncapturableRegistry _registry = null!;
    private IUncapturableHeroesSettingsProvider _settings = null!;
    private IUncapturableHeroesConfigProvider _configProvider = null!;
    private IHeroCaptivityAdapter _captivity = null!;
    private IInquiryAdapter _inquiry = null!;
    private IModLogger _logger = null!;
    private UncapturableHeroesConfig _config = null!;
    private UncapturableHeroService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new UncapturableHeroesConfig();

        _registry = Substitute.For<IUncapturableRegistry>();
        _registry.IsUncapturable(Arg.Any<string>(), Arg.Any<int?>()).Returns(true);

        _settings = Substitute.For<IUncapturableHeroesSettingsProvider>();
        _settings.IsEnabled.Returns(true);

        _configProvider = Substitute.For<IUncapturableHeroesConfigProvider>();
        _configProvider.GetConfig().Returns(_ => _config);

        _captivity = Substitute.For<IHeroCaptivityAdapter>();
        _captivity.MakeFugitive(Arg.Any<string>()).Returns(true);

        _inquiry = Substitute.For<IInquiryAdapter>();
        _logger = Substitute.For<IModLogger>();

        _sut = new UncapturableHeroService(
            _registry, _settings, _configProvider, _captivity, _inquiry, _logger);
    }

    // ---- The master toggle ------------------------------------------------

    [TestMethod]
    public void ShouldDenyCapture_ToggleOff_ReturnsFalse_AndNeverAsksTheRegistry()
    {
        _settings.IsEnabled.Returns(false);

        Assert.IsFalse(_sut.ShouldDenyCapture(HeroId, 4));
        _registry.DidNotReceive().IsUncapturable(Arg.Any<string>(), Arg.Any<int?>());
    }

    [TestMethod]
    public void ShouldDenyCapture_ToggleOn_AndHeroProtected_ReturnsTrue()
        => Assert.IsTrue(_sut.ShouldDenyCapture(HeroId, 4));

    [TestMethod]
    public void ShouldDenyCapture_ToggleOn_AndHeroNotProtected_ReturnsFalse()
    {
        _registry.IsUncapturable(Arg.Any<string>(), Arg.Any<int?>()).Returns(false);

        Assert.IsFalse(_sut.ShouldDenyCapture(HeroId, 0));
    }

    [TestMethod]
    public void TryPreventCapture_ToggleOff_DoesNothingAtAll()
    {
        _settings.IsEnabled.Returns(false);

        Assert.IsFalse(_sut.TryPreventCapture(HeroId, 4, HeroName, playerRelevant: true));
        _captivity.DidNotReceive().MakeFugitive(Arg.Any<string>());
        _inquiry.DidNotReceiveWithAnyArgs().ShowMessage(default!, default!, default!, default!);
    }

    // ---- The escape happens before the announce gate ----------------------

    [TestMethod]
    public void TryPreventCapture_AnnounceDisabled_StillMakesTheHeroAFugitive()
    {
        // The latch rule. Gating the state transition on a display flag is how a mid-window toggle
        // strands an entity forever.
        _config.AnnounceEscape = false;

        Assert.IsTrue(_sut.TryPreventCapture(HeroId, 4, HeroName, playerRelevant: true));
        _captivity.Received(1).MakeFugitive(HeroId);
        _inquiry.DidNotReceiveWithAnyArgs().ShowMessage(default!, default!, default!, default!);
    }

    [TestMethod]
    public void TryPreventCapture_NotPlayerRelevant_StillMakesTheHeroAFugitive()
    {
        Assert.IsTrue(_sut.TryPreventCapture(HeroId, 4, HeroName, playerRelevant: false));
        _captivity.Received(1).MakeFugitive(HeroId);
        _inquiry.DidNotReceiveWithAnyArgs().ShowMessage(default!, default!, default!, default!);
    }

    [TestMethod]
    public void TryPreventCapture_PlayerRelevant_AnnouncesWithTheHeroName()
    {
        Assert.IsTrue(_sut.TryPreventCapture(HeroId, 4, HeroName, playerRelevant: true));

        _inquiry.Received(1).ShowMessage(
            "taom_uncapturable_escapes_capture", Arg.Any<string>(), "HERO", HeroName);
    }

    // ---- Fail-open --------------------------------------------------------

    [TestMethod]
    public void TryPreventCapture_EscapeFails_ReturnsFalseSoVanillaCaptureProceeds()
    {
        // Without this the prefix would skip vanilla on a hero it never actually freed, leaving
        // him neither captured nor escaped.
        _captivity.MakeFugitive(Arg.Any<string>()).Returns(false);

        Assert.IsFalse(_sut.TryPreventCapture(HeroId, 4, HeroName, playerRelevant: true));
        _inquiry.DidNotReceiveWithAnyArgs().ShowMessage(default!, default!, default!, default!);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(HeroId)));
    }

    [TestMethod]
    public void TryPreventCapture_ConfigReadThrowsAfterTheEscape_StillReportsThePreventedCapture()
    {
        // The nastiest ordering in the feature. By this point the hero IS a fugitive; if the
        // announce path throws, the exception unwinds into the prefix's catch, which returns true,
        // and vanilla then captures a hero the world was just told escaped. The config read is a
        // faulted-Lazy candidate (a Lazy<T> rethrows its cached exception forever), so it has to be
        // inside the guard, not above it.
        _configProvider.GetConfig().Returns(_ => throw new System.InvalidOperationException("faulted lazy"));

        Assert.IsTrue(_sut.TryPreventCapture(HeroId, 4, HeroName, playerRelevant: true));
        _captivity.Received(1).MakeFugitive(HeroId);
    }

    [TestMethod]
    public void OnBattleCaptureDenied_ConfigReadThrows_DoesNotPropagate()
    {
        _configProvider.GetConfig().Returns(_ => throw new System.InvalidOperationException("faulted lazy"));

        _sut.OnBattleCaptureDenied(HeroName, playerRelevant: true);
    }

    [TestMethod]
    public void TryPreventCapture_InquiryThrows_StillReportsThePreventedCapture()
    {
        // The escape already happened. A failed toast must not undo it.
        _inquiry
            .When(a => a.ShowMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new System.InvalidOperationException("no UI in tests"));

        Assert.IsTrue(_sut.TryPreventCapture(HeroId, 4, HeroName, playerRelevant: true));
        _captivity.Received(1).MakeFugitive(HeroId);
    }

    // ---- The battle path announces, but never mutates ----------------------

    [TestMethod]
    public void OnBattleCaptureDenied_PlayerRelevant_AnnouncesTheBattleString()
    {
        _sut.OnBattleCaptureDenied(HeroName, playerRelevant: true);

        _inquiry.Received(1).ShowMessage(
            "taom_uncapturable_escapes_battle", Arg.Any<string>(), "HERO", HeroName);
    }

    [TestMethod]
    public void OnBattleCaptureDenied_NotPlayerRelevant_IsSilent()
    {
        _sut.OnBattleCaptureDenied(HeroName, playerRelevant: false);

        _inquiry.DidNotReceiveWithAnyArgs().ShowMessage(default!, default!, default!, default!);
    }

    [TestMethod]
    public void OnBattleCaptureDenied_AnnounceDisabled_IsSilent()
    {
        _config.AnnounceEscape = false;

        _sut.OnBattleCaptureDenied(HeroName, playerRelevant: true);

        _inquiry.DidNotReceiveWithAnyArgs().ShowMessage(default!, default!, default!, default!);
    }

    [TestMethod]
    public void OnBattleCaptureDenied_NeverTouchesTheCaptivityAdapter()
    {
        // Vanilla performs the battle escape via its own fall-through. Doing it here as well would
        // apply MakeHeroFugitiveAction twice.
        _sut.OnBattleCaptureDenied(HeroName, playerRelevant: true);

        _captivity.DidNotReceive().MakeFugitive(Arg.Any<string>());
    }

    [TestMethod]
    public void OnBattleCaptureDenied_NullHeroName_DoesNotThrow()
    {
        _sut.OnBattleCaptureDenied(null!, playerRelevant: true);

        _inquiry.Received(1).ShowMessage(
            Arg.Any<string>(), Arg.Any<string>(), "HERO", string.Empty);
    }
}
