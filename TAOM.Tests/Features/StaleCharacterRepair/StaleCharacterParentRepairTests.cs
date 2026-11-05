using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StaleCharacterRepair;

namespace TAOM.Tests.Features.StaleCharacterRepair;

/// <summary>
/// Crash bundle 3fbdc0c5 (2026-11-02): a NullReferenceException in <c>CharacterObject.get_StealthEquipments</c>
/// when <c>lord_MM15_6</c> fathered a child. The parent's raw culture was null, hidden by the hero's own culture
/// until <c>FillFrom</c> copied it onto the child's new character.
/// </summary>
[TestClass]
public class StaleCharacterParentRepairTests
{
    private IStaleCharacterAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private StaleCharacterRepairService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IStaleCharacterAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new StaleCharacterRepairService(_adapter, _logger);
    }

    private static string CheckLine(string id, string result, string detail) => $"[Check] id={id} result={result} {detail}";

    private static IHeroCharacterStateAdapter Parent(string id, bool hollow, bool repaired = true)
    {
        var parent = Substitute.For<IHeroCharacterStateAdapter>();
        parent.HeroId.Returns(id);
        parent.IsCharacterHollow.Returns(hollow);
        parent.DescribeState().Returns("hero=" + id + " original=True");
        parent.TryRepairCharacter().Returns(repaired);
        return parent;
    }

    private IEnumerable<string> Warnings()
        => _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogWarning))
            .Select(c => (string)c.GetArguments()[0]!);

    [TestMethod]
    public void CheckIds_EachId_IsValidForTheVerdictReader()
    {
        Assert.IsTrue(Regex.IsMatch(StaleCharacterRepairService.ParentRepairCheckId, @"^[a-z0-9]+(\.[a-z0-9-]+)+$"));
        Assert.IsTrue(Regex.IsMatch(StaleCharacterRepairService.BirthCompletedCheckId, @"^[a-z0-9]+(\.[a-z0-9-]+)+$"));
    }

    // ---- the repair at the birth ---- //
    [TestMethod]
    public void RepairOffspringParent_Hollow_RepairsWritesPassAndReturnsTrue()
    {
        var parent = Parent("lord_MM15_6", hollow: true);

        Assert.IsTrue(_sut.RepairOffspringParent(parent));

        parent.Received(1).TryRepairCharacter();
        _logger.Received(1).LogInfo(CheckLine(StaleCharacterRepairService.ParentRepairCheckId, "pass", "hero=lord_MM15_6 original=True"));
    }

    [TestMethod]
    public void RepairOffspringParent_Hollow_WritesTheStateFromBeforeTheWrite()
    {
        // These facts are the only evidence of how the character became hollow, and the repair overwrites them.
        var parent = Parent("lord_x", hollow: true);
        parent.TryRepairCharacter().Returns(_ =>
        {
            parent.DescribeState().Returns("hero=lord_x after-the-write");
            return true;
        });

        _sut.RepairOffspringParent(parent);

        _logger.Received(1).LogInfo(CheckLine(StaleCharacterRepairService.ParentRepairCheckId, "pass", "hero=lord_x original=True"));
    }

    [TestMethod]
    public void RepairOffspringParent_HealthyParent_ReturnsFalseAndWritesNothing()
    {
        var parent = Parent("lord_MM5_6", hollow: false);

        Assert.IsFalse(_sut.RepairOffspringParent(parent));

        parent.DidNotReceive().TryRepairCharacter();
        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "every healthy birth runs this seam");
        Assert.AreEqual(0, Warnings().Count());
    }

    [TestMethod]
    public void RepairOffspringParent_NotRepaired_WritesFailAndReturnsFalse()
    {
        Assert.IsFalse(_sut.RepairOffspringParent(Parent("lord_x", hollow: true, repaired: false)));

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.StartsWith("[Check] id=" + StaleCharacterRepairService.ParentRepairCheckId + " result=fail ") && m.Contains("hero=lord_x")));
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains(" result=pass")));
    }

    [TestMethod]
    public void RepairOffspringParent_AdapterThrows_SwallowsWritesFailAndReturnsFalse()
    {
        var parent = Parent("lord_x", hollow: true);
        parent.TryRepairCharacter().Returns(_ => throw new InvalidOperationException("boom"));

        Assert.IsFalse(_sut.RepairOffspringParent(parent), "a true here forges the proof path");

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.StartsWith("[Swallowed] feature=StaleCharacterRepair site=RepairOffspringParent first=InvalidOperationException")));
        _logger.Received(1).LogWarning(CheckLine(StaleCharacterRepairService.ParentRepairCheckId, "fail", "hero=lord_x threw=InvalidOperationException"));
    }

    [TestMethod]
    public void RepairOffspringParent_DescribeStateThrows_StillRepairsBecauseTheLogMustNotBlockTheRepair()
    {
        var parent = Parent("lord_x", hollow: true);
        parent.DescribeState().Returns(_ => throw new InvalidOperationException("boom"));

        Assert.IsTrue(_sut.RepairOffspringParent(parent));

        parent.Received(1).TryRepairCharacter();
        _logger.Received(1).LogInfo(CheckLine(StaleCharacterRepairService.ParentRepairCheckId, "pass", "hero=lord_x state=unavailable"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.StartsWith("[Swallowed] feature=StaleCharacterRepair site=DescribeState first=InvalidOperationException")));
    }

    // ---- the line that says whether the birth finished ---- //
    [TestMethod]
    public void ReportBirthAfterRepair_NoFailure_WritesPassKeyedByTheChild()
    {
        _sut.ReportBirthAfterRepair("hero_77", null!);

        _logger.Received(1).LogInfo(CheckLine(StaleCharacterRepairService.BirthCompletedCheckId, "pass", "child=hero_77"));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains(" result=fail")));
    }

    [TestMethod]
    public void ReportBirthAfterRepair_Failure_WritesFailAndNoPass()
    {
        _sut.ReportBirthAfterRepair(null!, new NullReferenceException());

        _logger.Received(1).LogWarning(CheckLine(StaleCharacterRepairService.BirthCompletedCheckId, "fail", "threw=NullReferenceException"));
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains(" result=pass")));
    }

    [TestMethod]
    public void ReportBirthAfterRepair_NoChildAndNoFailure_WritesFailAndNoPass()
    {
        // PatchShield can swallow a missing-API exception before this Finalizer runs, and the target then returns null.
        _sut.ReportBirthAfterRepair(null!, null!);

        _logger.Received(1).LogWarning(CheckLine(StaleCharacterRepairService.BirthCompletedCheckId, "fail", "threw=none"));
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains(" result=pass")));
    }

    // ---- the census at load ---- //
    [TestMethod]
    public void ReportHollowHeroCharacters_NoneFound_StaysSilent()
    {
        _adapter.FindHollowHeroCharacters().Returns(new List<IHeroCharacterStateAdapter>());

        _sut.ReportHollowHeroCharacters();

        Assert.AreEqual(0, Warnings().Count(), "this runs on every save load");
    }

    [TestMethod]
    public void ReportHollowHeroCharacters_Found_LogsTheCountAndEachHerosStateWithoutWriting()
    {
        var parent = Parent("lord_MM15_6", hollow: true);
        _adapter.FindHollowHeroCharacters().Returns(new List<IHeroCharacterStateAdapter> { parent });

        _sut.ReportHollowHeroCharacters();

        var line = Warnings().Single(w => w.Contains("census:"));
        StringAssert.Contains(line, "1 hero(es)");
        StringAssert.Contains(line, "hero=lord_MM15_6 original=True");
        parent.DidNotReceive().TryRepairCharacter();
    }

    [TestMethod]
    public void ReportHollowHeroCharacters_OneDescribeStateThrows_StillNamesTheOthers()
    {
        var bad = Parent("lord_bad", hollow: true);
        bad.DescribeState().Returns(_ => throw new InvalidOperationException("boom"));
        var good = Parent("lord_good", hollow: true);
        _adapter.FindHollowHeroCharacters().Returns(new List<IHeroCharacterStateAdapter> { bad, good });

        _sut.ReportHollowHeroCharacters();

        var line = Warnings().Single(w => w.Contains("census:"));
        StringAssert.Contains(line, "hero=lord_bad state=unavailable");
        StringAssert.Contains(line, "hero=lord_good original=True");
    }

    [TestMethod]
    public void ReportHollowHeroCharacters_ManyFound_CapsTheNamesAndKeepsTheTrueCount()
    {
        var many = Enumerable.Range(0, StaleCharacterRepairService.MaxNamedIds + 5)
            .Select(i => Parent("lord_" + i, hollow: true)).ToList();
        _adapter.FindHollowHeroCharacters().Returns(many);

        _sut.ReportHollowHeroCharacters();

        var line = Warnings().Single(w => w.Contains("census:"));
        StringAssert.Contains(line, (StaleCharacterRepairService.MaxNamedIds + 5) + " hero(es)");
        Assert.IsFalse(line.Contains("hero=lord_" + StaleCharacterRepairService.MaxNamedIds + " "));
    }

    [TestMethod]
    public void ReportHollowHeroCharacters_AdapterThrows_DoesNotBlockTheLoad()
    {
        _adapter.FindHollowHeroCharacters().Returns(_ => throw new InvalidOperationException("boom"));

        _sut.ReportHollowHeroCharacters();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.StartsWith("[Swallowed] feature=StaleCharacterRepair site=ReportHollowHeroCharacters first=InvalidOperationException")));
    }
}
