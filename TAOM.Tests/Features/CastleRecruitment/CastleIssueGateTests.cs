using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CastleRecruitment.Hooks;

namespace TAOM.Tests.Features.CastleRecruitment;

/// <summary>
/// Castle Recruitment blocks campaign issues for castle notables only. A lord resting in a castle (the engine
/// reports the lord's party settlement as the hero's) keeps his issues.
/// </summary>
[TestClass]
public class CastleIssueGateTests
{
    [TestMethod]
    public void CastleNotable_IsBlocked() =>
        Assert.IsTrue(CastleRecruitmentBehavior.BlocksIssues(enabled: true, inCastle: true, isNotable: true));

    [TestMethod]
    public void LordInCastle_IsNotBlocked() =>
        Assert.IsFalse(CastleRecruitmentBehavior.BlocksIssues(enabled: true, inCastle: true, isNotable: false));

    [TestMethod]
    public void NotableOutsideCastle_IsNotBlocked() =>
        Assert.IsFalse(CastleRecruitmentBehavior.BlocksIssues(enabled: true, inCastle: false, isNotable: true));

    [TestMethod]
    public void FeatureOff_BlocksNothing() =>
        Assert.IsFalse(CastleRecruitmentBehavior.BlocksIssues(enabled: false, inCastle: true, isNotable: true));
}
