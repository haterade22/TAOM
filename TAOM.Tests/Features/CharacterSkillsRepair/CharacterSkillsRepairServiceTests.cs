using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CharacterSkillsRepair;

namespace TAOM.Tests.Features.CharacterSkillsRepair;

/// <summary>
/// Crash bundle 065939b6 (2026-09-05): a NullReferenceException out of
/// <c>CharacterObject.GetSkillValue</c> during <c>Clan.AfterLoad</c> on save load. The null is
/// <c>BasicCharacterObject.DefaultCharacterSkills</c> on a character restored from a save whose XML
/// definition no longer exists; vanilla derefs it with no guard. See
/// <see cref="CharacterSkillsRepairService"/> for the four-link chain.
/// </summary>
[TestClass]
public class CharacterSkillsRepairServiceTests
{
    private ICharacterSkillsAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private CharacterSkillsRepairService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<ICharacterSkillsAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CharacterSkillsRepairService(_adapter, _logger);
    }

    private void Broken(params string[] ids)
        => _adapter.FindCharactersWithNoSkillSet().Returns(ids.ToList());

    private IEnumerable<string> Warnings()
        => _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogWarning))
            .Select(c => (string)c.GetArguments()[0]!);

    // ---- the healthy case, which is every load on a sound save ---- //
    [TestMethod]
    public void RepairMissingSkillSets_NothingBroken_RepairsNothingAndStaysSilent()
    {
        Broken();

        Assert.AreEqual(0, _sut.RepairMissingSkillSets());
        _adapter.DidNotReceive().TryGiveEmptySkillSet(Arg.Any<string>());
        Assert.AreEqual(0, Warnings().Count(),
            "this runs on every load — a line on a healthy save trains the reader to skip it");
    }

    [TestMethod]
    public void RepairMissingSkillSets_AdapterReturnsNull_DoesNotThrow()
    {
        _adapter.FindCharactersWithNoSkillSet().Returns((IReadOnlyList<string>)null!);

        Assert.AreEqual(0, _sut.RepairMissingSkillSets());
    }

    // ---- the crash case ---- //
    [TestMethod]
    public void RepairMissingSkillSets_BrokenCharacter_IsRepaired()
    {
        Broken("taom_removed_militia");
        _adapter.TryGiveEmptySkillSet("taom_removed_militia").Returns(true);

        Assert.AreEqual(1, _sut.RepairMissingSkillSets());
        _adapter.Received(1).TryGiveEmptySkillSet("taom_removed_militia");
    }

    [TestMethod]
    public void RepairMissingSkillSets_RepairIsReportedWithTheIdSoTheDataDefectStaysFindable()
    {
        Broken("taom_removed_militia");
        _adapter.TryGiveEmptySkillSet(Arg.Any<string>()).Returns(true);

        _sut.RepairMissingSkillSets();

        Assert.IsTrue(Warnings().Any(w => w.Contains("taom_removed_militia")),
            "the repair keeps the campaign alive but hides a data defect — the id must be logged");
    }

    [TestMethod]
    public void RepairMissingSkillSets_EveryBrokenCharacterIsAttempted()
    {
        Broken("a", "b", "c");
        _adapter.TryGiveEmptySkillSet(Arg.Any<string>()).Returns(true);

        Assert.AreEqual(3, _sut.RepairMissingSkillSets());
        foreach (var id in new[] { "a", "b", "c" })
            _adapter.Received(1).TryGiveEmptySkillSet(id);
    }

    // ---- partial and total failure: the campaign must still load ---- //
    [TestMethod]
    public void RepairMissingSkillSets_OneRepairFails_TheOthersStillRun_AndTheFailureIsNamed()
    {
        Broken("ok_one", "bad", "ok_two");
        _adapter.TryGiveEmptySkillSet("bad").Returns(false);
        _adapter.TryGiveEmptySkillSet("ok_one").Returns(true);
        _adapter.TryGiveEmptySkillSet("ok_two").Returns(true);

        Assert.AreEqual(2, _sut.RepairMissingSkillSets());
        Assert.IsTrue(Warnings().Any(w => w.Contains("could NOT repair") && w.Contains("bad")),
            "an unrepaired character can still crash the campaign — it must not be silent");
    }

    [TestMethod]
    public void RepairMissingSkillSets_RepairThrows_IsContainedAndTheSweepContinues()
    {
        Broken("throws", "after");
        _adapter.TryGiveEmptySkillSet("throws").Returns(_ => throw new InvalidOperationException("boom"));
        _adapter.TryGiveEmptySkillSet("after").Returns(true);

        Assert.AreEqual(1, _sut.RepairMissingSkillSets());
        _adapter.Received(1).TryGiveEmptySkillSet("after");
    }

    [TestMethod]
    public void RepairMissingSkillSets_ScanThrows_ReturnsZeroRatherThanBlockingTheLoad()
    {
        _adapter.FindCharactersWithNoSkillSet().Returns(_ => throw new InvalidOperationException("boom"));

        Assert.AreEqual(0, _sut.RepairMissingSkillSets());
        Assert.IsTrue(Warnings().Any(w => w.Contains("scan failed")));
    }

    [TestMethod]
    public void RepairMissingSkillSets_LoggerThrows_DoesNotPropagate()
    {
        // The repair runs inside Campaign.OnGameLoaded. A logging fault there would surface to the
        // player as "A problem occured while trying to load the saved game."
        Broken("x");
        _adapter.TryGiveEmptySkillSet("x").Returns(true);
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException());

        Assert.AreEqual(1, _sut.RepairMissingSkillSets());
    }

    // ---- the id-naming policy ---- //
    [TestMethod]
    public void Describe_FewIds_NamesThemAll()
        => StringAssert.Contains(
            CharacterSkillsRepairService.Describe(new[] { "a", "b" }), "Ids: a, b");

    [TestMethod]
    public void Describe_ManyIds_CapsTheListButKeepsTheTrueCount()
    {
        var ids = Enumerable.Range(0, CharacterSkillsRepairService.MaxNamedIds + 5)
            .Select(i => "troop_" + i).ToList();

        var text = CharacterSkillsRepairService.Describe(ids);

        StringAssert.Contains(text, "and 5 more");
        Assert.IsFalse(text.Contains("troop_" + (CharacterSkillsRepairService.MaxNamedIds + 1)),
            "a save that lost a whole culture must not produce an unreadable wall of ids");
    }

    [TestMethod]
    public void Describe_NoIds_DoesNotThrow()
    {
        Assert.AreEqual("(none)", CharacterSkillsRepairService.Describe(new string[0]));
        Assert.AreEqual("(none)", CharacterSkillsRepairService.Describe(null!));
    }
}
