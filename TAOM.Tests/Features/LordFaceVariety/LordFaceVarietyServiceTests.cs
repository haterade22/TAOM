using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LordFaceVariety;
using static TAOM.Tests.Features.LordFaceVariety.FakeHeroFaceAdapter;

namespace TAOM.Tests.Features.LordFaceVariety;

[TestClass]
public class LordFaceVarietyServiceTests
{
    private IModLogger _logger = null!;
    private List<string> _info = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _info = new List<string>();
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(ci => _info.Add(ci.Arg<string>()));
    }

    private LordFaceVarietyService Build(FakeHeroFaceAdapter adapter) => new(adapter, _logger);

    private static IEnumerable<string> RolledIds(FakeHeroFaceAdapter a) => a.Calls.Select(c => c.Id).OrderBy(x => x);

    // Rule A: a hero still on the template minimum of a range that has spread.

    [TestMethod]
    public void Run_SpreadRangeHeroOnMinimum_RollsInRange()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("h1", key: "MIN", onMin: true, spread: true));

        var report = Build(adapter).Run();

        Assert.AreEqual(1, report.RolledInRange);
        Assert.AreEqual(0, report.RolledAroundOwnFace);
        Assert.AreEqual("range", adapter.Calls.Single().Kind);
        Assert.AreEqual(LordFaceVarietyService.SeedFor("h1"), adapter.Calls.Single().Seed);
    }

    [TestMethod]
    public void Run_SpreadRangeHeroNotOnMinimum_IsNotRolledInRange()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("h1", onMin: false, spread: true));

        Build(adapter).Run();

        Assert.AreEqual(0, adapter.Calls.Count);
    }

    [TestMethod]
    public void Run_FlatRangeHeroOnMinimum_IsNotRolledInRange()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("h1", onMin: true, spread: false));

        Build(adapter).Run();

        Assert.AreEqual(0, adapter.Calls.Count);
    }

    [TestMethod]
    public void Run_TwoRuleAHeroesOnTheSameMinimum_BothRolledInRange()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("a", key: "MIN", onMin: true, spread: true))
            .Add(Hero("b", key: "MIN", onMin: true, spread: true));

        var report = Build(adapter).Run();

        Assert.AreEqual(2, report.RolledInRange);
        Assert.AreEqual(0, report.RolledAroundOwnFace);
        Assert.IsTrue(adapter.Calls.All(c => c.Kind == "range"));
        Assert.AreEqual(0, report.StillShared.Count);
    }

    // Rule B: shared explicit faces, one anchor keeps its face.

    [TestMethod]
    public void Run_SharedGroup_RollsEveryMemberExceptTheFirstOrdinalId()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("zeta")).Add(Hero("alpha")).Add(Hero("mid"));

        var report = Build(adapter).Run();

        CollectionAssert.AreEqual(new[] { "mid", "zeta" }, RolledIds(adapter).ToArray());
        Assert.IsTrue(adapter.Calls.All(c => c.Kind == "own" && c.Variation == LordFaceVarietyService.OwnFaceVariation));
        Assert.AreEqual(2, report.RolledAroundOwnFace);
        Assert.AreEqual(0, report.StillShared.Count);
    }

    [TestMethod]
    public void Run_SharedGroup_RollsAtAdultAge()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("a")).Add(Hero("b"));

        Build(adapter).Run();

        Assert.AreEqual(30f, LordFaceVarietyService.RollAge);
        Assert.AreEqual(LordFaceVarietyService.RollAge, adapter.Calls.Single().Age);
    }

    [TestMethod]
    public void Run_SharedGroup_EachRolledMemberUsesItsOwnSeed()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("a")).Add(Hero("b")).Add(Hero("c")).Add(Hero("d"));

        var report = Build(adapter).Run();

        foreach (var call in adapter.Calls)
            Assert.AreEqual(LordFaceVarietyService.SeedFor(call.Id), call.Seed, "seed of " + call.Id);
        Assert.AreEqual(3, adapter.Calls.Select(c => c.Seed).Distinct().Count());
        Assert.AreEqual(0, report.StillShared.Count, "a shared seed would give the rolled members one face");
    }

    [TestMethod]
    public void Run_SharedGroup_ClanLeaderBeatsOrdinalOrder()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("alpha")).Add(Hero("zeta", leader: true));

        Build(adapter).Run();

        CollectionAssert.AreEqual(new[] { "alpha" }, RolledIds(adapter).ToArray());
    }

    [TestMethod]
    public void Run_SharedGroup_CanonIdBeatsClanLeader()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("alpha", leader: true)).Add(Hero("lord_R1_1"));

        Build(adapter).Run();

        CollectionAssert.AreEqual(new[] { "alpha" }, RolledIds(adapter).ToArray());
    }

    [TestMethod]
    public void Run_SharedGroupWithTwoCanonIds_ListOrderPicksTheAnchor()
    {
        var canon = LordFaceVarietyService.CanonAnchorIds;
        var first = canon[0];
        var last = canon[canon.Count - 1];
        var adapter = new FakeHeroFaceAdapter().Add(Hero(first)).Add(Hero(last));

        Build(adapter).Run();

        CollectionAssert.AreEqual(new[] { last }, RolledIds(adapter).ToArray());
    }

    [TestMethod]
    public void Run_HeroesOfDifferentRaceOrSex_AreNotGroupedAndNotRolled()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("a", race: "elf")).Add(Hero("b", race: "human"))
            .Add(Hero("c", female: true)).Add(Hero("d", race: "dwarf"));

        var report = Build(adapter).Run();

        Assert.AreEqual(0, adapter.Calls.Count);
        Assert.AreEqual(0, report.StillShared.Count);
    }

    [TestMethod]
    public void Run_RuleAHeroLeavesItsGroup_IsNotRolledAgainAndRestRegroup()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("a", key: "X", onMin: true, spread: true))
            .Add(Hero("b", key: "X"))
            .Add(Hero("c", key: "X"));

        var report = Build(adapter).Run();

        Assert.AreEqual(1, adapter.Calls.Count(c => c.Id == "a"));
        Assert.AreEqual("range", adapter.Calls.Single(c => c.Id == "a").Kind);
        Assert.AreEqual(1, report.RolledInRange);
        // b and c still share X after a left it: c is rolled, b is the anchor.
        CollectionAssert.AreEqual(new[] { "c" }, adapter.Calls.Where(c => c.Kind == "own").Select(c => c.Id).ToArray());
    }

    // Skips.

    [TestMethod]
    public void Run_PlayerHero_IsNeverRolledAndNeverAnAnchorCandidate()
    {
        // The player's id sorts first and he leads his clan, so counted in the group he would be the anchor.
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("aaa_player", player: true, leader: true, onMin: true, spread: true))
            .Add(Hero("bbb")).Add(Hero("ccc"));

        Build(adapter).Run();

        CollectionAssert.AreEqual(new[] { "ccc" }, RolledIds(adapter).ToArray());
    }

    [TestMethod]
    public void Run_NonOriginalOnSpreadRange_IsNotRolled()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("clone1", original: false, onMin: true, spread: true));

        Build(adapter).Run();

        Assert.AreEqual(0, adapter.Calls.Count);
    }

    // Rule C: heroes generated at campaign creation.

    [TestMethod]
    public void Run_GeneratedHeroOnFlatRange_IsRolledAroundOwnFace()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("CharacterObject_7", key: "K", original: false, spread: false));

        var report = Build(adapter).Run();

        var call = adapter.Calls.Single();
        Assert.AreEqual("own", call.Kind);
        Assert.AreEqual(LordFaceVarietyService.OwnFaceVariation, call.Variation);
        Assert.AreEqual(LordFaceVarietyService.RollAge, call.Age);
        Assert.AreEqual(LordFaceVarietyService.SeedFor("CharacterObject_7"), call.Seed);
        Assert.AreEqual(1, report.RolledGenerated);
        Assert.AreEqual(0, report.RolledAroundOwnFace);
    }

    [TestMethod]
    public void Run_GeneratedHeroOnSpreadRange_IsNotRolled()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("CharacterObject_7", original: false, spread: true));

        var report = Build(adapter).Run();

        Assert.AreEqual(0, adapter.Calls.Count);
        Assert.AreEqual(0, report.RolledGenerated);
    }

    [TestMethod]
    public void Run_GeneratedHeroSharingALordKey_LordStaysAnchor()
    {
        // The clone's id sorts before the lord's; counted in the group it would take the anchor's place.
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("CharacterObject_1", key: "SHARED", original: false))
            .Add(Hero("lord_x", key: "SHARED"));

        var report = Build(adapter).Run();

        CollectionAssert.AreEqual(new[] { "CharacterObject_1" }, RolledIds(adapter).ToArray());
        Assert.AreEqual(0, report.StillShared.Count);
    }

    [TestMethod]
    public void Run_GeneratedPlayerOrEmptyId_IsSkipped()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("child_player", original: false, player: true)).Add(Hero("", original: false));

        Build(adapter).Run();

        Assert.AreEqual(0, adapter.Calls.Count);
    }

    // Colour-only twins (#1221): keys that differ only in the colour fields of the first part share a face.

    // Key parts 2 to 8 of the dwarf women's face in lords.xml (lord_E5_2).
    private const string FaceTail = "67867061100A07F77BF8877718F181F184CD43B6886870FC"
        + "00766603078BA5AA000000000000000000000000000000000000000042F43080";

    private static string EngineKey(ulong part1) => "key=\"" + part1.ToString("X16") + FaceTail + "\"";

    [TestMethod]
    public void FaceShapeKey_ClearsOnlyTheColourBitsOfTheFirstPart()
    {
        Assert.AreEqual(EngineKey(0xFFC000003FFFFFFFUL), LordFaceVarietyService.FaceShapeKey(EngineKey(ulong.MaxValue)));
        Assert.AreEqual("key:" + 0x0000000000002005UL.ToString("X16") + FaceTail,
            LordFaceVarietyService.FaceShapeKey("key:" + 0x0003A80FC0002005UL.ToString("X16") + FaceTail));
    }

    [TestMethod]
    public void FaceShapeKey_TextWithoutAKey_IsUnchanged()
    {
        Assert.AreEqual("SHARED", LordFaceVarietyService.FaceShapeKey("SHARED"));
        Assert.AreEqual("min:BodyProperty.fighter_x", LordFaceVarietyService.FaceShapeKey("min:BodyProperty.fighter_x"));
        Assert.AreEqual("", LordFaceVarietyService.FaceShapeKey(""));
    }

    [TestMethod]
    public void Run_KeysDifferingOnlyInColour_AreGroupedAndRolled()
    {
        // lord_E5_2 and lord_E1_3, lord_E1_6, lord_E4_4: one face, different hair, skin and bits 36-47.
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("lord_E5_2", key: EngineKey(0x0003800C40002005UL), race: "dwarf", female: true))
            .Add(Hero("lord_E1_3", key: EngineKey(0x0003A80FC0002005UL), race: "dwarf", female: true))
            .Add(Hero("lord_E1_6", key: EngineKey(0x0003A80400002005UL), race: "dwarf", female: true))
            .Add(Hero("lord_E4_4", key: EngineKey(0x0003800080002005UL), race: "dwarf", female: true));

        var report = Build(adapter).Run();

        CollectionAssert.AreEqual(new[] { "lord_E1_6", "lord_E4_4", "lord_E5_2" }, RolledIds(adapter).ToArray());
        Assert.AreEqual(3, report.RolledAroundOwnFace);
    }

    [TestMethod]
    public void Run_KeysDifferingJustOutsideTheColourBits_AreNotGrouped()
    {
        const ulong face = 0x0003800C40002005UL;
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("a", key: EngineKey(face)))
            .Add(Hero("b", key: EngineKey(face ^ (1UL << 29))))
            .Add(Hero("c", key: EngineKey(face ^ (1UL << 54))));

        var report = Build(adapter).Run();

        Assert.AreEqual(0, adapter.Calls.Count);
        Assert.AreEqual(0, report.StillShared.Count);
    }

    [TestMethod]
    public void Run_UniqueFaces_ProduceNoRolls()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("a", key: "1")).Add(Hero("b", key: "2"));

        var report = Build(adapter).Run();

        Assert.AreEqual(0, adapter.Calls.Count);
        Assert.AreEqual(0, report.RolledInRange + report.RolledAroundOwnFace);
    }

    [TestMethod]
    public void Run_EmptyId_IsSkipped()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("", onMin: true, spread: true));

        Build(adapter).Run();

        Assert.AreEqual(0, adapter.Calls.Count);
    }

    // Twins.

    [TestMethod]
    public void Run_Twins_BothKeepTheirSharedFace()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("lord_R1_3", race: "elf")).Add(Hero("lord_R1_4", race: "elf"));

        var report = Build(adapter).Run();

        Assert.AreEqual(0, adapter.Calls.Count);
        Assert.AreEqual(1, report.StillShared.Count);
        CollectionAssert.AreEqual(new[] { "lord_R1_3", "lord_R1_4" }, report.StillShared[0].ToArray());
    }

    [TestMethod]
    public void Run_TwinsSharingWithAThird_OnlyTheThirdIsRolled()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("aaa", race: "elf")).Add(Hero("lord_R1_3", race: "elf")).Add(Hero("lord_R1_4", race: "elf"));

        Build(adapter).Run();

        CollectionAssert.AreEqual(new[] { "aaa" }, RolledIds(adapter).ToArray());
    }

    // Seed.

    [TestMethod]
    public void SeedFor_KnownVector_MatchesFnv1aMaskedToThirtyOneBits()
    {
        // FNV-1a 32-bit of "a" is 0xE40C292C.
        Assert.AreEqual(0x640C292C, LordFaceVarietyService.SeedFor("a"));
    }

    [TestMethod]
    public void SeedFor_SameId_IsStable()
    {
        Assert.AreEqual(LordFaceVarietyService.SeedFor("lord_1_1"), LordFaceVarietyService.SeedFor("lord_1_1"));
    }

    [TestMethod]
    public void SeedFor_ManyIds_AreDistinctAndNeverNegative()
    {
        var seeds = Enumerable.Range(0, 2000).Select(i => LordFaceVarietyService.SeedFor("lord_X" + i)).ToList();

        Assert.AreEqual(seeds.Count, seeds.Distinct().Count());
        Assert.IsTrue(seeds.All(s => s >= 0), "masked to 31 bits so it is never negative; the native roll treats -1 as return the minimum");
    }

    // Self-check and logging.

    [TestMethod]
    public void Run_RollThatLeavesTheFaceShared_IsReportedAndLogged()
    {
        var adapter = new FakeHeroFaceAdapter { StickyRolls = true }
            .Add(Hero("a")).Add(Hero("b"));

        var report = Build(adapter).Run();

        Assert.AreEqual(1, report.StillShared.Count);
        var line = _info.Single(l => l.StartsWith("[LordFaceVariety] varied "));
        StringAssert.Contains(line, "varied 0+1+0, 1 still shared: a|b");
    }

    [TestMethod]
    public void Run_Clean_LogsNoSharedGroups()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("a", key: "M", onMin: true, spread: true)).Add(Hero("b")).Add(Hero("c"));

        Build(adapter).Run();

        StringAssert.Contains(_info.Single(l => l.StartsWith("[LordFaceVariety]")),
            "varied 1+1+0, 0 still shared: none");
    }

    // Throw safety.

    [TestMethod]
    public void Run_OneRollThrows_RestOfThePassStillRuns()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("a", key: "M", onMin: true, spread: true))
            .Add(Hero("b", key: "M2", onMin: true, spread: true))
            .Add(Hero("c", key: "M3", onMin: true, spread: true));
        adapter.ThrowOn.Add("a");

        var report = Build(adapter).Run();

        Assert.AreEqual(2, report.RolledInRange);
        Assert.AreEqual(1, report.Failed);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("for a:")));
    }

    [TestMethod]
    public void Run_RuleBRollThrows_IsLoggedCountedAndStillReportedShared()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("a")).Add(Hero("b")).Add(Hero("c"));
        adapter.ThrowOn.Add("b");

        var report = Build(adapter).Run();

        Assert.AreEqual(1, report.RolledAroundOwnFace);
        Assert.AreEqual(1, report.Failed);
        Assert.AreEqual(1, report.StillShared.Count);
        CollectionAssert.AreEqual(new[] { "a", "b" }, report.StillShared[0].ToArray());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("for b:")));
    }

    [TestMethod]
    public void Run_RuleBRollRefused_CountsAsFailedNotRolled()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("a")).Add(Hero("b"));
        adapter.RefuseOn.Add("b");

        var report = Build(adapter).Run();

        Assert.AreEqual(0, report.RolledAroundOwnFace);
        Assert.AreEqual(1, report.Failed);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no roll for b")));
    }

    [TestMethod]
    public void Run_RuleAFails_HeroFallsIntoItsSharedGroup()
    {
        var adapter = new FakeHeroFaceAdapter()
            .Add(Hero("a", key: "X", onMin: true, spread: true))
            .Add(Hero("b", key: "X"));
        adapter.RefuseOn.Add("a");

        var report = Build(adapter).Run();

        Assert.AreEqual(1, report.Failed);
        Assert.AreEqual(0, report.RolledInRange);
        Assert.AreEqual(1, report.RolledAroundOwnFace);
        Assert.AreEqual("own", adapter.Calls.Single(c => c.Id == "b").Kind);
    }

    [TestMethod]
    public void Run_RollRefused_CountsAsFailedNotRolled()
    {
        var adapter = new FakeHeroFaceAdapter().Add(Hero("a", key: "M", onMin: true, spread: true));
        adapter.RefuseOn.Add("a");

        var report = Build(adapter).Run();

        Assert.AreEqual(0, report.RolledInRange);
        Assert.AreEqual(1, report.Failed);
    }
}
