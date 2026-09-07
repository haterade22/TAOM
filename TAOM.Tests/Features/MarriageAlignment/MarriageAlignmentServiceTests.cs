using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;
using TAOM.Features.MarriageAlignment;

namespace TAOM.Tests.Features.MarriageAlignment;

[TestClass]
public class MarriageAlignmentServiceTests
{
    private const string CultureA = "gondor";
    private const string CultureB = "mistymountainorcs";

    private IAlignmentService _alignment = null!;
    private IMarriageAlignmentSettingsProvider _settings = null!;
    private MarriageAlignmentService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _alignment = Substitute.For<IAlignmentService>();
        _settings = Substitute.For<IMarriageAlignmentSettingsProvider>();

        // Defaults: feature on, applies to both halves, steering on. Each test overrides as needed.
        _settings.IsEnabled.Returns(true);
        _settings.ApplyToAi.Returns(true);
        _settings.ApplyToPlayer.Returns(true);
        _settings.SteerAiPartnerSearch.Returns(true);

        _sut = new MarriageAlignmentService(_alignment, _settings);
    }

    private void SetSides(FactionSide sideA, FactionSide sideB)
    {
        // NSubstitute returns default(FactionSide) == Free for unconfigured calls, so configure both.
        _alignment.GetCultureSide(CultureA).Returns(sideA);
        _alignment.GetCultureSide(CultureB).Returns(sideB);
    }

    [DataTestMethod]
    [DataRow(FactionSide.Free, FactionSide.Evil, true)]
    [DataRow(FactionSide.Evil, FactionSide.Free, true)]
    [DataRow(FactionSide.Free, FactionSide.Free, false)]
    [DataRow(FactionSide.Evil, FactionSide.Evil, false)]
    [DataRow(FactionSide.Free, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Free, false)]
    [DataRow(FactionSide.Evil, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Evil, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Neutral, false)]
    public void IsMarriageBlocked_MatchesSideMatrix(FactionSide sideA, FactionSide sideB, bool expected)
    {
        SetSides(sideA, sideB);

        var result = _sut.IsMarriageBlocked(CultureA, CultureB, involvesPlayerClan: false);

        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow(FactionSide.Free, FactionSide.Evil, false)]
    [DataRow(FactionSide.Evil, FactionSide.Free, false)]
    [DataRow(FactionSide.Free, FactionSide.Free, true)]
    [DataRow(FactionSide.Evil, FactionSide.Evil, true)]
    [DataRow(FactionSide.Free, FactionSide.Neutral, true)]
    [DataRow(FactionSide.Neutral, FactionSide.Free, true)]
    [DataRow(FactionSide.Evil, FactionSide.Neutral, true)]
    [DataRow(FactionSide.Neutral, FactionSide.Evil, true)]
    [DataRow(FactionSide.Neutral, FactionSide.Neutral, true)]
    public void AreCulturesCompatible_MatchesSideMatrix(FactionSide sideA, FactionSide sideB, bool expected)
    {
        SetSides(sideA, sideB);

        var result = _sut.AreCulturesCompatible(CultureA, CultureB);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void AreCulturesCompatible_IgnoresEnableToggles()
    {
        // The pool filter checks ShouldSteerAiPartnerSearch once; the bare side rule must not
        // double-apply the toggles, or a disabled feature would report Free/Evil as compatible
        // and the two halves of the rule would disagree.
        _settings.IsEnabled.Returns(false);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsFalse(_sut.AreCulturesCompatible(CultureA, CultureB));
    }

    [TestMethod]
    public void IsMarriageBlocked_FeatureDisabled_NeverBlocks()
    {
        _settings.IsEnabled.Returns(false);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsFalse(_sut.IsMarriageBlocked(CultureA, CultureB, involvesPlayerClan: false));
        Assert.IsFalse(_sut.IsMarriageBlocked(CultureA, CultureB, involvesPlayerClan: true));
    }

    [TestMethod]
    public void IsMarriageBlocked_ApplyToPlayerOff_BlocksAiButNotPlayerClan()
    {
        _settings.ApplyToPlayer.Returns(false);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsFalse(_sut.IsMarriageBlocked(CultureA, CultureB, involvesPlayerClan: true));
        Assert.IsTrue(_sut.IsMarriageBlocked(CultureA, CultureB, involvesPlayerClan: false));
    }

    [TestMethod]
    public void IsMarriageBlocked_ApplyToAiOff_BlocksPlayerClanButNotAi()
    {
        _settings.ApplyToAi.Returns(false);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsFalse(_sut.IsMarriageBlocked(CultureA, CultureB, involvesPlayerClan: false));
        Assert.IsTrue(_sut.IsMarriageBlocked(CultureA, CultureB, involvesPlayerClan: true));
    }

    [TestMethod]
    public void IsMarriageBlocked_NullCultureId_ResolvesNeutralAndNeverBlocks()
    {
        // AlignmentService.GetSide returns Neutral for null/empty, which the substitute mirrors.
        _alignment.GetCultureSide(null).Returns(FactionSide.Neutral);
        _alignment.GetCultureSide(CultureB).Returns(FactionSide.Evil);

        Assert.IsFalse(_sut.IsMarriageBlocked(null, CultureB, involvesPlayerClan: false));
        Assert.IsTrue(_sut.AreCulturesCompatible(null, CultureB));
    }

    [TestMethod]
    public void IsMarriageBlocked_UnknownCultureId_ResolvesNeutralAndNeverBlocks()
    {
        _alignment.GetCultureSide("not_a_culture").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide(CultureB).Returns(FactionSide.Evil);

        Assert.IsFalse(_sut.IsMarriageBlocked("not_a_culture", CultureB, involvesPlayerClan: false));
    }

    [DataTestMethod]
    [DataRow(true, true, true, true)]
    [DataRow(false, true, true, false)]
    [DataRow(true, false, true, false)]
    [DataRow(true, true, false, false)]
    public void ShouldSteerAiPartnerSearch_RequiresAllThreeToggles(
        bool enabled, bool applyToAi, bool steer, bool expected)
    {
        _settings.IsEnabled.Returns(enabled);
        _settings.ApplyToAi.Returns(applyToAi);
        _settings.SteerAiPartnerSearch.Returns(steer);

        Assert.AreEqual(expected, _sut.ShouldSteerAiPartnerSearch);
    }
}
