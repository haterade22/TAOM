using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Arachnophobia;
using TAOM.Features.Elephant;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.TroopProgression;

/// <summary>Arachnophobia Mode (#1110): a hidden troop leaves the volunteer pick and the garrison index roots.</summary>
[TestClass]
public class VolunteerRecruitmentHiddenTroopTests
{
    private const string FirstGondorTroop = "gondor_ano_peasant";

    private IRandomProvider _random;
    private IArachnophobiaService _arachnophobia;
    private VolunteerRecruitmentService _sut;

    private static VolunteerContext Gondor() => new(settlementId: null, boundSettlementId: null, ownerClanId: null, cultureId: "gondor");

    [TestInitialize]
    public void Setup()
    {
        _random = Substitute.For<IRandomProvider>();
        _random.Next(Arg.Any<int>()).Returns(0);
        _arachnophobia = Substitute.For<IArachnophobiaService>();
        _sut = new VolunteerRecruitmentService(_random, Substitute.For<IModLogger>(), _arachnophobia);
    }

    [TestMethod]
    public void GetVolunteerTroopId_FirstTroopHidden_ReturnsAnotherTroop()
    {
        _arachnophobia.IsHidden(FirstGondorTroop).Returns(true);

        string result = _sut.GetVolunteerTroopId(Gondor());

        Assert.IsNotNull(result);
        Assert.AreNotEqual(FirstGondorTroop, result);
    }

    [TestMethod]
    public void GetVolunteerTroopId_TroopHidden_ItsWeightLeavesTheRoll()
    {
        int visibleTotal = 0;
        _random.Next(Arg.Do<int>(total => visibleTotal = total)).Returns(0);
        _sut.GetVolunteerTroopId(Gondor());
        int fullTotal = visibleTotal;

        _arachnophobia.IsHidden(FirstGondorTroop).Returns(true);
        _sut.GetVolunteerTroopId(Gondor());

        Assert.IsTrue(visibleTotal < fullTotal, $"the roll covers {visibleTotal} of {fullTotal}");
    }

    [TestMethod]
    public void GetVolunteerTroopId_EveryTroopHidden_ReturnsNull()
    {
        _arachnophobia.IsHidden(Arg.Any<string>()).Returns(true);
        Assert.IsNull(_sut.GetVolunteerTroopId(Gondor()));
    }

    [TestMethod]
    public void GetVolunteerTroopId_TroopHidden_LeavesTheSharedPoolIntact()
    {
        // The pools are static. With the mode off at the next load, the troop must be back.
        _arachnophobia.IsHidden(FirstGondorTroop).Returns(true);
        _sut.GetVolunteerTroopId(Gondor());

        _arachnophobia.IsHidden(FirstGondorTroop).Returns(false);

        Assert.AreEqual(FirstGondorTroop, _sut.GetVolunteerTroopId(Gondor()));
    }

    [TestMethod]
    public void GetCulturePoolTroopIds_TroopHidden_OmitsIt()
    {
        // GarrisonCultureSwapAdapter.BuildCandidates walks the upgrade tree from these roots.
        _arachnophobia.IsHidden(FirstGondorTroop).Returns(true);

        var ids = _sut.GetCulturePoolTroopIds("gondor");

        Assert.IsTrue(ids.Count > 0);
        Assert.IsFalse(ids.Contains(FirstGondorTroop));
    }

    [TestMethod]
    public void GetCulturePoolTroopIds_NothingHidden_KeepsTheTroop()
        => Assert.IsTrue(_sut.GetCulturePoolTroopIds("gondor").Contains(FirstGondorTroop));
}
