using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.AlignmentRecruitment;

[TestClass]
public class RecruitmentAlignmentServiceTests
{
    private const string RecruiterId = "rk";
    private const string SourceId = "sk";

    private IAlignmentService _alignment = null!;
    private IRecruitmentAlignmentSettingsProvider _settings = null!;
    private RecruitmentAlignmentService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _alignment = Substitute.For<IAlignmentService>();
        _settings = Substitute.For<IRecruitmentAlignmentSettingsProvider>();

        // Defaults: feature on, symmetric, applies to AI. Each test overrides as needed.
        _settings.IsEnabled.Returns(true);
        _settings.ApplyToAi.Returns(true);
        _settings.ApplyToPlayer.Returns(true);
        _settings.GoodRejectsEvilOnly.Returns(false);

        _sut = new RecruitmentAlignmentService(_alignment, _settings);
    }

    private void SetSides(FactionSide recruiter, FactionSide source)
    {
        // NSubstitute returns default(FactionSide) == Free for unconfigured calls, so configure both.
        _alignment.GetKingdomSide(RecruiterId).Returns(recruiter);
        _alignment.GetKingdomSide(SourceId).Returns(source);
    }

    // --- Symmetric mode: one case per (recruiterSide x sourceSide) cell ---

    [DataTestMethod]
    [DataRow(FactionSide.Free, FactionSide.Evil, true)]
    [DataRow(FactionSide.Evil, FactionSide.Free, true)]
    [DataRow(FactionSide.Free, FactionSide.Free, false)]
    [DataRow(FactionSide.Evil, FactionSide.Evil, false)]
    [DataRow(FactionSide.Free, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Evil, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Evil, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Free, false)]
    public void IsRecruitmentBlocked_SymmetricMode_MatchesMatrix(FactionSide recruiter, FactionSide source, bool expected)
    {
        _settings.GoodRejectsEvilOnly.Returns(false);
        SetSides(recruiter, source);

        var result = _sut.IsRecruitmentBlocked(RecruiterId, SourceId, isPlayerRecruiter: true);

        Assert.AreEqual(expected, result);
    }

    // --- GoodRejectsEvil mode: only Free recruiter + Evil source blocks ---

    [DataTestMethod]
    [DataRow(FactionSide.Free, FactionSide.Evil, true)]
    [DataRow(FactionSide.Evil, FactionSide.Free, false)]
    [DataRow(FactionSide.Free, FactionSide.Free, false)]
    [DataRow(FactionSide.Evil, FactionSide.Evil, false)]
    [DataRow(FactionSide.Free, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Evil, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Free, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Evil, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Neutral, false)]
    public void IsRecruitmentBlocked_GoodRejectsEvilMode_MatchesMatrix(FactionSide recruiter, FactionSide source, bool expected)
    {
        _settings.GoodRejectsEvilOnly.Returns(true);
        SetSides(recruiter, source);

        var result = _sut.IsRecruitmentBlocked(RecruiterId, SourceId, isPlayerRecruiter: true);

        Assert.AreEqual(expected, result);
    }

    // --- Master toggle ---

    [TestMethod]
    public void IsRecruitmentBlocked_Disabled_NeverBlocks()
    {
        _settings.IsEnabled.Returns(false);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsFalse(_sut.IsRecruitmentBlocked(RecruiterId, SourceId, isPlayerRecruiter: true));
    }

    // --- ApplyToAi gate (per-branch: player vs AI x applyToAi on/off) ---

    [TestMethod]
    public void IsRecruitmentBlocked_ApplyToAiFalse_AiRecruiter_NeverBlocks()
    {
        _settings.ApplyToAi.Returns(false);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsFalse(_sut.IsRecruitmentBlocked(RecruiterId, SourceId, isPlayerRecruiter: false));
    }

    [TestMethod]
    public void IsRecruitmentBlocked_ApplyToAiFalse_PlayerRecruiter_StillBlocks()
    {
        _settings.ApplyToAi.Returns(false);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsTrue(_sut.IsRecruitmentBlocked(RecruiterId, SourceId, isPlayerRecruiter: true));
    }

    [TestMethod]
    public void IsRecruitmentBlocked_ApplyToAiTrue_AiRecruiter_Blocks()
    {
        _settings.ApplyToAi.Returns(true);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsTrue(_sut.IsRecruitmentBlocked(RecruiterId, SourceId, isPlayerRecruiter: false));
    }

    // --- ApplyToPlayer gate (symmetric to ApplyToAi: player vs AI x applyToPlayer on/off) ---

    [TestMethod]
    public void IsRecruitmentBlocked_ApplyToPlayerFalse_PlayerRecruiter_NeverBlocks()
    {
        _settings.ApplyToPlayer.Returns(false);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsFalse(_sut.IsRecruitmentBlocked(RecruiterId, SourceId, isPlayerRecruiter: true));
    }

    [TestMethod]
    public void IsRecruitmentBlocked_ApplyToPlayerFalse_AiRecruiter_StillBlocks()
    {
        _settings.ApplyToPlayer.Returns(false);
        _settings.ApplyToAi.Returns(true);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsTrue(_sut.IsRecruitmentBlocked(RecruiterId, SourceId, isPlayerRecruiter: false));
    }

    [TestMethod]
    public void IsRecruitmentBlocked_ApplyToPlayerTrue_PlayerRecruiter_Blocks()
    {
        _settings.ApplyToPlayer.Returns(true);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.IsTrue(_sut.IsRecruitmentBlocked(RecruiterId, SourceId, isPlayerRecruiter: true));
    }

    // --- Null/unknown ids resolve to Neutral via the alignment service => never block ---

    [TestMethod]
    public void IsRecruitmentBlocked_NullIds_ResolveNeutral_NeverBlocks()
    {
        _alignment.GetKingdomSide(null).Returns(FactionSide.Neutral);

        Assert.IsFalse(_sut.IsRecruitmentBlocked(null, null, isPlayerRecruiter: true));
    }
}
