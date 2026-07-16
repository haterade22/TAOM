using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;
using TAOM.Features.PrisonerRecruitment;

namespace TAOM.Tests.Features.PrisonerRecruitment;

/// <summary>
/// Ids here are deliberately opaque ("rk"/"rc"/"pc") for the side matrix so the same-culture rule
/// cannot fire and mask a side-rule bug. The lore-id tests below use real TAOM culture StringIds --
/// never lore names: empire=Dunland, aserai=Harad, battania=Khand, vlandia=Rohan.
/// </summary>
[TestClass]
public class PrisonerRecruitmentMoraleServiceTests
{
    private const string RecruiterKingdomId = "rk";
    private const string RecruiterCultureId = "rc";
    private const string PrisonerCultureId = "pc";

    private IAlignmentService _alignment = null!;
    private IPrisonerRecruitmentSettingsProvider _settings = null!;
    private PrisonerRecruitmentMoraleService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _alignment = Substitute.For<IAlignmentService>();
        _settings = Substitute.For<IPrisonerRecruitmentSettingsProvider>();

        _settings.IsEnabled.Returns(true);
        _settings.ApplyToPlayer.Returns(true);
        _settings.ApplyToAi.Returns(true);

        _sut = new PrisonerRecruitmentMoraleService(_alignment, _settings);
    }

    /// <summary>
    /// NSubstitute returns default(FactionSide) == Free for unconfigured calls, so configure every
    /// id the service may touch -- including the recruiter culture used by the kingdom fallback.
    /// </summary>
    private void SetSides(FactionSide recruiter, FactionSide prisoner)
    {
        _alignment.GetKingdomSide(RecruiterKingdomId).Returns(recruiter);
        _alignment.GetCultureSide(RecruiterCultureId).Returns(recruiter);
        _alignment.GetCultureSide(PrisonerCultureId).Returns(prisoner);
    }

    // --- Rule 2: same alignment side (Neutral never waives) ---

    [DataTestMethod]
    [DataRow(FactionSide.Evil, FactionSide.Evil, true)]
    [DataRow(FactionSide.Free, FactionSide.Free, true)]
    [DataRow(FactionSide.Neutral, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Evil, FactionSide.Free, false)]
    [DataRow(FactionSide.Free, FactionSide.Evil, false)]
    [DataRow(FactionSide.Free, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Evil, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Free, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Evil, false)]
    public void ShouldWaiveMoralePenalty_SideMatrix_MatchesSpec(
        FactionSide recruiter, FactionSide prisoner, bool expected)
    {
        SetSides(recruiter, prisoner);

        var result = _sut.ShouldWaiveMoralePenalty(
            RecruiterKingdomId, RecruiterCultureId, PrisonerCultureId, isPlayerRecruiter: true);

        Assert.AreEqual(expected, result);
    }

    /// <summary>The headline case: Isengard recruiting a Mordor prisoner -- both evil, no morale lost.</summary>
    [DataTestMethod]
    [DataRow("mordor")]
    [DataRow("gundabad")]
    [DataRow("dolguldur")]
    [DataRow("empire")]     // Dunland
    [DataRow("khuzait")]    // Rhun
    [DataRow("aserai")]     // Harad
    public void ShouldWaiveMoralePenalty_IsengardRecruitingEvilPrisoner_Waives(string prisonerCultureId)
    {
        _alignment.GetKingdomSide("isengard").Returns(FactionSide.Evil);
        _alignment.GetCultureSide("isengard").Returns(FactionSide.Evil);
        _alignment.GetCultureSide(prisonerCultureId).Returns(FactionSide.Evil);

        Assert.IsTrue(_sut.ShouldWaiveMoralePenalty(
            "isengard", "isengard", prisonerCultureId, isPlayerRecruiter: true));
    }

    [TestMethod]
    public void ShouldWaiveMoralePenalty_IsengardRecruitingGondorPrisoner_DoesNotWaive()
    {
        _alignment.GetKingdomSide("isengard").Returns(FactionSide.Evil);
        _alignment.GetCultureSide("isengard").Returns(FactionSide.Evil);
        _alignment.GetCultureSide("gondor").Returns(FactionSide.Free);

        Assert.IsFalse(_sut.ShouldWaiveMoralePenalty(
            "isengard", "isengard", "gondor", isPlayerRecruiter: true));
    }

    // --- Rule 1: own faction (same culture) -- the only waiver Neutral factions ever get ---

    /// <summary>
    /// Khand (battania) is Neutral, so the side rule cannot waive. Without the same-culture rule a
    /// Khand player would lose morale recruiting their own troops. This test is rule 1's reason to exist.
    /// </summary>
    [TestMethod]
    public void ShouldWaiveMoralePenalty_NeutralFactionRecruitingOwnCulture_Waives()
    {
        _alignment.GetKingdomSide("battania").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("battania").Returns(FactionSide.Neutral);

        Assert.IsTrue(_sut.ShouldWaiveMoralePenalty(
            "battania", "battania", "battania", isPlayerRecruiter: true));
    }

    [TestMethod]
    public void ShouldWaiveMoralePenalty_NeutralFactionRecruitingOtherNeutralCulture_DoesNotWaive()
    {
        _alignment.GetKingdomSide("battania").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("battania").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("umbar").Returns(FactionSide.Neutral);

        Assert.IsFalse(_sut.ShouldWaiveMoralePenalty(
            "battania", "battania", "umbar", isPlayerRecruiter: true));
    }

    [TestMethod]
    public void ShouldWaiveMoralePenalty_SameCultureDifferentCasing_Waives()
    {
        _alignment.GetKingdomSide(Arg.Any<string>()).Returns(FactionSide.Neutral);
        _alignment.GetCultureSide(Arg.Any<string>()).Returns(FactionSide.Neutral);

        Assert.IsTrue(_sut.ShouldWaiveMoralePenalty(
            "battania", "Battania", "battania", isPlayerRecruiter: true));
    }

    /// <summary>Kingdomless Neutral-culture hero still waives their own culture via rule 1.</summary>
    [TestMethod]
    public void ShouldWaiveMoralePenalty_SameCultureNoKingdom_Waives()
    {
        _alignment.GetKingdomSide(null).Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("umbar").Returns(FactionSide.Neutral);

        Assert.IsTrue(_sut.ShouldWaiveMoralePenalty(
            null, "umbar", "umbar", isPlayerRecruiter: true));
    }

    // --- Recruiter side resolution: kingdom first, culture fallback ---

    /// <summary>Kingdomless Isengard player: without the culture fallback this feature never fires for them.</summary>
    [TestMethod]
    public void ShouldWaiveMoralePenalty_NullKingdomEvilCulture_UsesCultureFallback_Waives()
    {
        _alignment.GetKingdomSide(null).Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("isengard").Returns(FactionSide.Evil);
        _alignment.GetCultureSide("mordor").Returns(FactionSide.Evil);

        Assert.IsTrue(_sut.ShouldWaiveMoralePenalty(null, "isengard", "mordor", isPlayerRecruiter: true));
    }

    /// <summary>A player-founded kingdom is "new_kingdom" -- unclassified, so the culture must carry it.</summary>
    [TestMethod]
    public void ShouldWaiveMoralePenalty_UnknownKingdomIdEvilCulture_UsesCultureFallback_Waives()
    {
        _alignment.GetKingdomSide("new_kingdom").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("isengard").Returns(FactionSide.Evil);
        _alignment.GetCultureSide("mordor").Returns(FactionSide.Evil);

        Assert.IsTrue(_sut.ShouldWaiveMoralePenalty("new_kingdom", "isengard", "mordor", isPlayerRecruiter: true));
    }

    /// <summary>
    /// A classified kingdom outranks the hero's own culture: a Gondor-cultured hero serving Mordor
    /// (kingdom empire_s) waives a Mordor prisoner.
    /// </summary>
    [TestMethod]
    public void ShouldWaiveMoralePenalty_ClassifiedKingdomOverridesCulture_Waives()
    {
        _alignment.GetKingdomSide("empire_s").Returns(FactionSide.Evil);
        _alignment.GetCultureSide("gondor").Returns(FactionSide.Free);
        _alignment.GetCultureSide("mordor").Returns(FactionSide.Evil);

        Assert.IsTrue(_sut.ShouldWaiveMoralePenalty("empire_s", "gondor", "mordor", isPlayerRecruiter: true));
    }

    // --- Settings gates ---

    [TestMethod]
    public void ShouldWaiveMoralePenalty_Disabled_NeverWaives()
    {
        _settings.IsEnabled.Returns(false);
        SetSides(FactionSide.Evil, FactionSide.Evil);

        Assert.IsFalse(_sut.ShouldWaiveMoralePenalty(
            RecruiterKingdomId, RecruiterCultureId, PrisonerCultureId, isPlayerRecruiter: true));
    }

    /// <summary>The master toggle must beat rule 1 too, not just the side rule.</summary>
    [TestMethod]
    public void ShouldWaiveMoralePenalty_DisabledWithSameCulture_NeverWaives()
    {
        _settings.IsEnabled.Returns(false);
        _alignment.GetKingdomSide("battania").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("battania").Returns(FactionSide.Neutral);

        Assert.IsFalse(_sut.ShouldWaiveMoralePenalty(
            "battania", "battania", "battania", isPlayerRecruiter: true));
    }

    [TestMethod]
    public void ShouldWaiveMoralePenalty_ApplyToPlayerFalse_PlayerRecruiter_DoesNotWaive()
    {
        _settings.ApplyToPlayer.Returns(false);
        SetSides(FactionSide.Evil, FactionSide.Evil);

        Assert.IsFalse(_sut.ShouldWaiveMoralePenalty(
            RecruiterKingdomId, RecruiterCultureId, PrisonerCultureId, isPlayerRecruiter: true));
    }

    [TestMethod]
    public void ShouldWaiveMoralePenalty_ApplyToPlayerFalse_AiRecruiter_StillWaives()
    {
        _settings.ApplyToPlayer.Returns(false);
        _settings.ApplyToAi.Returns(true);
        SetSides(FactionSide.Evil, FactionSide.Evil);

        Assert.IsTrue(_sut.ShouldWaiveMoralePenalty(
            RecruiterKingdomId, RecruiterCultureId, PrisonerCultureId, isPlayerRecruiter: false));
    }

    [TestMethod]
    public void ShouldWaiveMoralePenalty_ApplyToAiFalse_AiRecruiter_DoesNotWaive()
    {
        _settings.ApplyToAi.Returns(false);
        SetSides(FactionSide.Evil, FactionSide.Evil);

        Assert.IsFalse(_sut.ShouldWaiveMoralePenalty(
            RecruiterKingdomId, RecruiterCultureId, PrisonerCultureId, isPlayerRecruiter: false));
    }

    [TestMethod]
    public void ShouldWaiveMoralePenalty_ApplyToAiFalse_PlayerRecruiter_StillWaives()
    {
        _settings.ApplyToAi.Returns(false);
        _settings.ApplyToPlayer.Returns(true);
        SetSides(FactionSide.Evil, FactionSide.Evil);

        Assert.IsTrue(_sut.ShouldWaiveMoralePenalty(
            RecruiterKingdomId, RecruiterCultureId, PrisonerCultureId, isPlayerRecruiter: true));
    }

    // --- Null / empty contract: garrisons, militia and caravans have no LeaderHero ---

    [TestMethod]
    public void ShouldWaiveMoralePenalty_NullLeaderHero_NullKingdomAndCulture_DoesNotWaive()
    {
        _alignment.GetKingdomSide(null).Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("mordor").Returns(FactionSide.Evil);

        Assert.IsFalse(_sut.ShouldWaiveMoralePenalty(null, null, "mordor", isPlayerRecruiter: false));
    }

    [TestMethod]
    public void ShouldWaiveMoralePenalty_NullPrisonerCulture_DoesNotWaive()
    {
        SetSides(FactionSide.Evil, FactionSide.Evil);

        Assert.IsFalse(_sut.ShouldWaiveMoralePenalty(
            RecruiterKingdomId, RecruiterCultureId, null, isPlayerRecruiter: true));
    }

    /// <summary>Two empty culture ids must not read as "same culture" and waive.</summary>
    [TestMethod]
    public void ShouldWaiveMoralePenalty_EmptyStringIds_DoesNotWaive()
    {
        _alignment.GetKingdomSide("").Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("").Returns(FactionSide.Neutral);

        Assert.IsFalse(_sut.ShouldWaiveMoralePenalty("", "", "", isPlayerRecruiter: true));
    }
}
