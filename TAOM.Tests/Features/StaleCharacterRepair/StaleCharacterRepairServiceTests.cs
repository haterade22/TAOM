using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StaleCharacterRepair;
using TAOM.Features.StaleCharacterRepair.Domain;

namespace TAOM.Tests.Features.StaleCharacterRepair;

/// <summary>
/// Crash bundle 065939b6 (2026-09-05): a NullReferenceException out of
/// <c>CharacterObject.GetSkillValue</c> during <c>Clan.AfterLoad</c> on save load. The nulls belong
/// to a character the save restored under an id current ModuleData no longer defines. See
/// <see cref="StaleCharacterRepairService"/> for the chain.
/// </summary>
[TestClass]
public class StaleCharacterRepairServiceTests
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

    private void Stale(params string[] ids)
        => _adapter.FindStaleCharacters().Returns(ids.ToList());

    private IEnumerable<string> Warnings()
        => _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogWarning))
            .Select(c => (string)c.GetArguments()[0]!);

    // ---- the healthy case, which is every load on a sound save ---- //
    [TestMethod]
    public void RepairStaleCharacters_NothingStale_RepairsNothingAndStaysSilent()
    {
        Stale();

        Assert.AreEqual(0, _sut.RepairStaleCharacters());
        _adapter.DidNotReceive().TryMakeInert(Arg.Any<string>());
        _adapter.DidNotReceive().ShowNoticeOnSessionStart(Arg.Any<int>());
        Assert.AreEqual(0, Warnings().Count(),
            "this runs on every save load — a line on a healthy save trains the reader to skip it");
    }

    [TestMethod]
    public void RepairStaleCharacters_AdapterReturnsNull_DoesNotThrow()
    {
        _adapter.FindStaleCharacters().Returns((IReadOnlyList<string>)null!);

        Assert.AreEqual(0, _sut.RepairStaleCharacters());
    }

    // ---- the crash case ---- //
    [TestMethod]
    public void RepairStaleCharacters_StaleCharacter_IsRepairedAndCounted()
    {
        Stale("taom_removed_militia");
        _adapter.TryMakeInert("taom_removed_militia").Returns(StubRepairOutcome.Repaired);

        Assert.AreEqual(1, _sut.RepairStaleCharacters());
        _adapter.Received(1).TryMakeInert("taom_removed_militia");
    }

    [TestMethod]
    public void RepairStaleCharacters_ReportsTheIdSoTheDataDefectStaysFindable()
    {
        Stale("taom_removed_militia");
        _adapter.TryMakeInert(Arg.Any<string>()).Returns(StubRepairOutcome.Repaired);

        _sut.RepairStaleCharacters();

        Assert.IsTrue(Warnings().Any(w => w.Contains("taom_removed_militia")));
    }

    [TestMethod]
    public void RepairStaleCharacters_EveryStaleCharacterIsAttempted()
    {
        Stale("a", "b", "c");
        _adapter.TryMakeInert(Arg.Any<string>()).Returns(StubRepairOutcome.Repaired);

        Assert.AreEqual(3, _sut.RepairStaleCharacters());
        foreach (var id in new[] { "a", "b", "c" })
            _adapter.Received(1).TryMakeInert(id);
    }

    // ---- the reporting contract: five outcomes, not one bool ---- //
    [TestMethod]
    public void RepairStaleCharacters_AlreadyHealthy_IsNotCountedAndNotCalledDangerous()
    {
        // The benign race: something filled the fields between the scan and the write. Reporting
        // it as "can still crash the campaign" is the opposite of true.
        Stale("raced");
        _adapter.TryMakeInert("raced").Returns(StubRepairOutcome.AlreadyHealthy);

        Assert.AreEqual(0, _sut.RepairStaleCharacters());
        Assert.IsFalse(Warnings().Any(w => w.Contains("could NOT repair")),
            "an already-healthy character must never be reported as a live crash risk");
    }

    [TestMethod]
    public void RepairStaleCharacters_BindingUnavailable_BlamesTheEngineBindingNotTheSave()
    {
        // An engine rename makes EVERY character fail. Listing the player's troop ids under
        // "could not repair" would point them at their save data and name nothing findable.
        Stale("a", "b");
        _adapter.TryMakeInert(Arg.Any<string>()).Returns(StubRepairOutcome.BindingUnavailable);

        Assert.AreEqual(0, _sut.RepairStaleCharacters());
        Assert.IsTrue(Warnings().Any(w => w.Contains("ENGINE BINDING problem")));
        Assert.IsFalse(Warnings().Any(w => w.Contains("could NOT repair")));
    }

    [TestMethod]
    public void RepairStaleCharacters_BindingUnavailable_IsReportedOnceNotPerCharacter()
    {
        Stale("a", "b", "c", "d");
        _adapter.TryMakeInert(Arg.Any<string>()).Returns(StubRepairOutcome.BindingUnavailable);

        _sut.RepairStaleCharacters();

        Assert.AreEqual(1, Warnings().Count(w => w.Contains("ENGINE BINDING problem")));
    }

    [TestMethod]
    public void RepairStaleCharacters_NotResolvedOrFailed_AreReportedAsStillDangerous()
    {
        Stale("gone", "threw", "ok");
        _adapter.TryMakeInert("gone").Returns(StubRepairOutcome.NotResolved);
        _adapter.TryMakeInert("threw").Returns(StubRepairOutcome.Failed);
        _adapter.TryMakeInert("ok").Returns(StubRepairOutcome.Repaired);

        Assert.AreEqual(1, _sut.RepairStaleCharacters());
        var danger = Warnings().Single(w => w.Contains("could NOT repair"));
        StringAssert.Contains(danger, "gone");
        StringAssert.Contains(danger, "threw");
        Assert.IsFalse(danger.Contains("ok"));
    }

    [TestMethod]
    public void RepairStaleCharacters_RepairThrows_IsContainedAndTheSweepContinues()
    {
        Stale("throws", "after");
        _adapter.TryMakeInert("throws").Returns(_ => throw new InvalidOperationException("boom"));
        _adapter.TryMakeInert("after").Returns(StubRepairOutcome.Repaired);

        Assert.AreEqual(1, _sut.RepairStaleCharacters());
        _adapter.Received(1).TryMakeInert("after");
    }

    [TestMethod]
    public void RepairStaleCharacters_ScanThrows_ReturnsZeroRatherThanBlockingTheLoad()
    {
        _adapter.FindStaleCharacters().Returns(_ => throw new InvalidOperationException("boom"));

        Assert.AreEqual(0, _sut.RepairStaleCharacters());
        Assert.IsTrue(Warnings().Any(w => w.Contains("scan failed")));
    }

    [TestMethod]
    public void RepairStaleCharacters_LoggerThrows_DoesNotPropagate()
    {
        // This runs inside Campaign.OnGameLoaded. A logging fault here would surface to the player
        // as "A problem occured while trying to load the saved game."
        Stale("x");
        _adapter.TryMakeInert("x").Returns(StubRepairOutcome.Repaired);
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException());

        Assert.AreEqual(1, _sut.RepairStaleCharacters());
    }

    // ---- the player notice ---- //
    [TestMethod]
    public void RepairStaleCharacters_SomethingRepaired_QueuesThePlayerNoticeWithTheCount()
    {
        // The stale ids ride into every future save while the repair does not, so a player who
        // never opens the log would overwrite their last recoverable file unwarned.
        Stale("a", "b");
        _adapter.TryMakeInert(Arg.Any<string>()).Returns(StubRepairOutcome.Repaired);

        _sut.RepairStaleCharacters();

        _adapter.Received(1).ShowNoticeOnSessionStart(2);
    }

    [TestMethod]
    public void RepairStaleCharacters_NothingRepaired_DoesNotQueueANotice()
    {
        Stale("gone");
        _adapter.TryMakeInert("gone").Returns(StubRepairOutcome.NotResolved);

        _sut.RepairStaleCharacters();

        _adapter.DidNotReceive().ShowNoticeOnSessionStart(Arg.Any<int>());
    }

    [TestMethod]
    public void RepairStaleCharacters_NoticeThrows_DoesNotPropagate()
    {
        Stale("x");
        _adapter.TryMakeInert("x").Returns(StubRepairOutcome.Repaired);
        _adapter.When(a => a.ShowNoticeOnSessionStart(Arg.Any<int>()))
            .Do(_ => throw new InvalidOperationException());

        Assert.AreEqual(1, _sut.RepairStaleCharacters());
    }

    // ---- the id-naming policy ---- //
    [TestMethod]
    public void Describe_FewIds_NamesThemAll()
        => StringAssert.Contains(
            StaleCharacterRepairService.Describe(new[] { "a", "b" }), "Ids: a, b");

    [TestMethod]
    public void Describe_ManyIds_CapsTheListButKeepsTheTrueCount()
    {
        var ids = Enumerable.Range(0, StaleCharacterRepairService.MaxNamedIds + 5)
            .Select(i => "troop_" + i).ToList();

        var text = StaleCharacterRepairService.Describe(ids);

        StringAssert.Contains(text, "and 5 more");
        Assert.IsFalse(text.Contains("troop_" + (StaleCharacterRepairService.MaxNamedIds + 1)),
            "a save that lost a whole culture must not produce an unreadable wall of ids");
    }

    [TestMethod]
    public void Describe_BlankId_IsRenderedRatherThanLeavingAnEmptyGap()
    {
        // A stale object can carry no StringId at all; "Ids: , b" reads as a formatting bug.
        StringAssert.Contains(
            StaleCharacterRepairService.Describe(new[] { "", "b" }), "(blank id)");
    }

    [TestMethod]
    public void Describe_NoIds_DoesNotThrow()
    {
        Assert.AreEqual("(none)", StaleCharacterRepairService.Describe(new string[0]));
        Assert.AreEqual("(none)", StaleCharacterRepairService.Describe(null!));
    }
}
