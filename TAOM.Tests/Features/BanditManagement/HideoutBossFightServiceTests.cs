using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.BanditManagement;

namespace TAOM.Tests.Features.BanditManagement;

/// <summary>
/// #564: the hideout boss fight is exactly 1 boss + N bodyguards on both routes. The engine sizes
/// that fight from the whole hideout population, not from the boss party template, so the maths
/// lives here and the two Patch86 prefixes plus <c>TaomBanditDensityModel</c> only relay it.
///
/// Assault route contract (replicates <c>MapEventHelper.GetPriorityListForHideoutMission</c>): the
/// wounded are in neither phase; every healthy hero or culture boss goes to the boss phase; the
/// highest-level regulars fill the remaining bodyguard slots; everything else is phase 1. The boss
/// phase is never empty (<c>SelectBossAgent</c> derefs its pick unguarded) and phase 1 never goes
/// negative.
///
/// Sneak-in route contract (feeds <c>HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight</c>):
/// keep the N highest-level unspawned troops; vanilla pads from its troop-type cache when fewer
/// remain, so the spawn count is N unless nothing exists to pad from.
/// </summary>
[TestClass]
public class HideoutBossFightServiceTests
{
    private IBanditScalingSettingsProvider _settings = null!;
    private HideoutBossFightService _sut = null!;

    [TestInitialize]
    public void Init()
    {
        _settings = Substitute.For<IBanditScalingSettingsProvider>();
        _settings.HideoutBossBodyguards.Returns(4);
        _sut = new HideoutBossFightService(_settings);
    }

    private static HideoutTroopCandidate Regular(int level) => new(level, isHeroOrBoss: false, isWounded: false);
    private static HideoutTroopCandidate Boss(int level = 30) => new(level, isHeroOrBoss: true, isWounded: false);
    private static HideoutTroopCandidate Wounded(int level, bool boss = false) => new(level, boss, isWounded: true);

    private static List<HideoutTroopCandidate> Regulars(params int[] levels) => levels.Select(Regular).ToList();

    // ------------------------------------------------------------------ BodyguardCount / cap

    [TestMethod]
    public void BodyguardCount_ReadsProviderLive_ReturnsProviderValue()
    {
        _settings.HideoutBossBodyguards.Returns(7);
        Assert.AreEqual(7, _sut.BodyguardCount);
        _settings.HideoutBossBodyguards.Returns(2);
        Assert.AreEqual(2, _sut.BodyguardCount, "the value is read on every access, not cached at construction");
    }

    [TestMethod]
    public void BodyguardCount_ProviderBelowZero_ClampsToZero()
    {
        _settings.HideoutBossBodyguards.Returns(-3);
        Assert.AreEqual(0, _sut.BodyguardCount);
    }

    [TestMethod]
    public void BodyguardCount_ProviderAboveTen_ClampsToTen()
    {
        _settings.HideoutBossBodyguards.Returns(50);
        Assert.AreEqual(10, _sut.BodyguardCount);
    }

    [TestMethod]
    public void BossPhaseTroopCap_BodyguardsFour_ReturnsFive()
    {
        Assert.AreEqual(5, _sut.BossPhaseTroopCap);
    }

    [TestMethod]
    public void BossPhaseTroopCap_BodyguardsZero_ReturnsOne()
    {
        _settings.HideoutBossBodyguards.Returns(0);
        Assert.AreEqual(1, _sut.BossPhaseTroopCap, "the boss himself always counts");
    }

    // ------------------------------------------------------------------ PlanAssault

    [TestMethod]
    public void PlanAssault_TypicalHideout_FirstPhaseIsEverythingButBossAndN()
    {
        // 1 boss + 30 regulars: boss phase = boss + 4, phase 1 = 26.
        var troops = new List<HideoutTroopCandidate> { Boss() };
        troops.AddRange(Regulars(Enumerable.Range(1, 30).ToArray()));

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(26, split.FirstPhaseTroopCount);
        Assert.AreEqual(5, split.BossPhaseTroopCount);
        Assert.AreEqual(26, split.FirstPhaseIndices.Count);
    }

    [TestMethod]
    public void PlanAssault_HighestLevelRegulars_GoToBossPhase()
    {
        // Levels 5, 26, 11, 31, 16, 21 with N = 2: the two highest (31 at index 3, 26 at index 1)
        // are held back for the boss; the rest are phase 1.
        _settings.HideoutBossBodyguards.Returns(2);
        var troops = new List<HideoutTroopCandidate> { Regular(5), Regular(26), Regular(11), Regular(31), Regular(16), Boss(), Regular(21) };

        var split = _sut.PlanAssault(troops);

        CollectionAssert.AreEqual(new[] { 0, 2, 4, 6 }, split.FirstPhaseIndices.ToArray());
        Assert.AreEqual(4, split.FirstPhaseTroopCount);
        Assert.AreEqual(3, split.BossPhaseTroopCount);
    }

    [TestMethod]
    public void PlanAssault_TiedLevels_KeepInputOrder()
    {
        // Four level-10 regulars and N = 2: vanilla's OrderByDescending is stable, so the FIRST two
        // in roster order are the ones held back. Indices 2 and 3 stay in phase 1.
        _settings.HideoutBossBodyguards.Returns(2);
        var troops = new List<HideoutTroopCandidate> { Boss(), Regular(10), Regular(10), Regular(10), Regular(10) };

        var split = _sut.PlanAssault(troops);

        CollectionAssert.AreEqual(new[] { 3, 4 }, split.FirstPhaseIndices.ToArray());
    }

    [TestMethod]
    public void PlanAssault_FirstPhaseIndices_AreAscendingAndExcludeBossPhase()
    {
        var troops = new List<HideoutTroopCandidate> { Regular(3), Boss(), Regular(9), Regular(1), Regular(7), Regular(2), Regular(8) };
        _settings.HideoutBossBodyguards.Returns(3);

        var split = _sut.PlanAssault(troops);

        // Held back: boss (1) + levels 9, 8, 7 (indices 2, 6, 4). Phase 1: indices 0, 3, 5.
        CollectionAssert.AreEqual(new[] { 0, 3, 5 }, split.FirstPhaseIndices.ToArray());
    }

    [TestMethod]
    public void PlanAssault_BodyguardsZero_BossFightsAlone()
    {
        _settings.HideoutBossBodyguards.Returns(0);
        var troops = new List<HideoutTroopCandidate> { Boss() };
        troops.AddRange(Regulars(1, 2, 3, 4, 5));

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(1, split.BossPhaseTroopCount);
        Assert.AreEqual(5, split.FirstPhaseTroopCount);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, split.FirstPhaseIndices.ToArray());
    }

    [TestMethod]
    public void PlanAssault_NoBossOrHero_BossPhaseIsNRegulars()
    {
        // A hideout can lack a boss party (TAOM infests at one party). The engine then promotes the
        // highest-level agent to boss, so the boss phase is N regulars, not N + 1.
        var split = _sut.PlanAssault(Regulars(1, 2, 3, 4, 5, 6, 7, 8, 9, 10));

        Assert.AreEqual(4, split.BossPhaseTroopCount);
        Assert.AreEqual(6, split.FirstPhaseTroopCount);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, split.FirstPhaseIndices.ToArray());
    }

    [TestMethod]
    public void PlanAssault_NoBossAndZeroBodyguards_BossPhaseStillOne()
    {
        // SelectBossAgent returns val ?? val2 and the caller derefs it: a zero-agent boss phase is
        // an NRE on both routes, so one regular is always held back.
        _settings.HideoutBossBodyguards.Returns(0);

        var split = _sut.PlanAssault(Regulars(1, 2, 3, 4, 5));

        Assert.AreEqual(1, split.BossPhaseTroopCount);
        Assert.AreEqual(4, split.FirstPhaseTroopCount);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, split.FirstPhaseIndices.ToArray(), "the level-5 regular at index 4 is the stand-in boss");
    }

    [TestMethod]
    public void PlanAssault_FewerRegularsThanN_FirstPhaseKeepsOne()
    {
        // 1 boss + 2 regulars with N = 4: the boss phase cannot absorb everything, because
        // HideoutMissionController asserts phase 2 < total. Phase 1 keeps at least one troop.
        var troops = new List<HideoutTroopCandidate> { Boss(), Regular(2), Regular(9) };

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(2, split.BossPhaseTroopCount, "boss + the level-9 regular");
        Assert.AreEqual(1, split.FirstPhaseTroopCount);
        CollectionAssert.AreEqual(new[] { 1 }, split.FirstPhaseIndices.ToArray());
    }

    [TestMethod]
    public void PlanAssault_WoundedBoss_IsNotCountedOrKept()
    {
        // Vanilla removes the wounded BEFORE counting heroes and bosses, so a wounded boss neither
        // fills a slot nor appears in phase 1. The boss phase is then N regulars.
        var troops = new List<HideoutTroopCandidate> { Wounded(30, boss: true) };
        troops.AddRange(Regulars(1, 2, 3, 4, 5, 6, 7, 8));

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(4, split.BossPhaseTroopCount);
        Assert.AreEqual(4, split.FirstPhaseTroopCount);
        Assert.IsFalse(split.FirstPhaseIndices.Contains(0));
    }

    [TestMethod]
    public void PlanAssault_WoundedRegulars_AreInNeitherPhase()
    {
        var troops = new List<HideoutTroopCandidate> { Boss(), Wounded(50), Regular(1), Wounded(40), Regular(2), Regular(3), Regular(4), Regular(5), Regular(6) };

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(5, split.BossPhaseTroopCount);
        Assert.AreEqual(2, split.FirstPhaseTroopCount, "7 healthy minus boss + 4");
        Assert.IsFalse(split.FirstPhaseIndices.Contains(1));
        Assert.IsFalse(split.FirstPhaseIndices.Contains(3));
        Assert.AreEqual(split.FirstPhaseTroopCount, split.FirstPhaseIndices.Count);
    }

    [TestMethod]
    public void PlanAssault_AllWounded_ReturnsZeroCounts()
    {
        var troops = new List<HideoutTroopCandidate> { Wounded(30, boss: true), Wounded(5), Wounded(6) };

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(0, split.FirstPhaseTroopCount);
        Assert.AreEqual(0, split.BossPhaseTroopCount);
        Assert.AreEqual(0, split.FirstPhaseIndices.Count);
    }

    [TestMethod]
    public void PlanAssault_EmptyInput_ReturnsZeroCounts()
    {
        var split = _sut.PlanAssault(new List<HideoutTroopCandidate>());

        Assert.AreEqual(0, split.FirstPhaseTroopCount);
        Assert.AreEqual(0, split.BossPhaseTroopCount);
        Assert.AreEqual(0, split.FirstPhaseIndices.Count);
    }

    [TestMethod]
    public void PlanAssault_SingleHealthyTroop_MatchesVanillaZeroFirstPhase()
    {
        // Vanilla: firstPhase = min(floor(0.8), max) = 0, the one troop is the boss phase.
        var split = _sut.PlanAssault(Regulars(12));

        Assert.AreEqual(0, split.FirstPhaseTroopCount);
        Assert.AreEqual(1, split.BossPhaseTroopCount);
        Assert.AreEqual(0, split.FirstPhaseIndices.Count);
    }

    [TestMethod]
    public void PlanAssault_OnlyHeroesAndBosses_ClampsSoFirstPhaseIsOne()
    {
        // Three heroes, no regulars: every hero wants the boss phase but phase 2 must stay below
        // the total, so the count says one goes to phase 1. The index list is empty (vanilla's own
        // num4 <= 0 shape): the supplier fills that slot from the non-priority heroes.
        var troops = new List<HideoutTroopCandidate> { Boss(), Boss(20), Boss(25) };

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(2, split.BossPhaseTroopCount);
        Assert.AreEqual(1, split.FirstPhaseTroopCount);
        Assert.AreEqual(0, split.FirstPhaseIndices.Count);
    }

    [TestMethod]
    public void PlanAssault_HideoutUnderTheCap_StillSplitsBossPlusN()
    {
        // 1 boss + 9 regulars (the post-trim minimum of 10): still boss + 4, phase 1 = 5. The old
        // model-only design gave boss + 1..3 here, which is why the prefix exists.
        var troops = new List<HideoutTroopCandidate> { Boss() };
        troops.AddRange(Regulars(1, 2, 3, 4, 5, 6, 7, 8, 9));

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(5, split.BossPhaseTroopCount);
        Assert.AreEqual(5, split.FirstPhaseTroopCount);
    }

    [TestMethod]
    public void PlanAssault_FatBossParty_FirstPhaseAbsorbsTheExcess()
    {
        // An existing save keeps a boss party spawned from the old 67-97 template until that
        // hideout is cleared once, and the campaign-side trim never touches boss parties. Pinned
        // on purpose: the excess goes to phase 1, the boss fight stays boss + 4.
        var troops = new List<HideoutTroopCandidate> { Boss() };
        troops.AddRange(Regulars(Enumerable.Repeat(10, 90).ToArray()));

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(5, split.BossPhaseTroopCount);
        Assert.AreEqual(86, split.FirstPhaseTroopCount);
    }

    [TestMethod]
    public void PlanAssault_TwoBosses_BothGoToBossPhase()
    {
        // A quest override can add a second hero-class boss. Both are held back and N regulars join.
        var troops = new List<HideoutTroopCandidate> { Boss(), Boss(28) };
        troops.AddRange(Regulars(1, 2, 3, 4, 5, 6, 7, 8));

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(6, split.BossPhaseTroopCount);
        Assert.AreEqual(4, split.FirstPhaseTroopCount);
        Assert.IsFalse(split.FirstPhaseIndices.Contains(0));
        Assert.IsFalse(split.FirstPhaseIndices.Contains(1));
    }

    [TestMethod]
    public void PlanAssault_FirstPhaseCount_EqualsIndexCount_WhenRegularsSuffice()
    {
        var troops = new List<HideoutTroopCandidate> { Boss() };
        troops.AddRange(Regulars(Enumerable.Range(1, 40).ToArray()));

        var split = _sut.PlanAssault(troops);

        Assert.AreEqual(split.FirstPhaseTroopCount, split.FirstPhaseIndices.Count);
        Assert.AreEqual(troops.Count, split.FirstPhaseTroopCount + split.BossPhaseTroopCount);
    }

    [TestMethod]
    public void PlanAssault_DoesNotMutateInput()
    {
        var troops = new List<HideoutTroopCandidate> { Regular(3), Boss(), Regular(9), Regular(1) };
        var snapshot = troops.ToArray();

        _sut.PlanAssault(troops);

        CollectionAssert.AreEqual(snapshot, troops);
    }

    // ------------------------------------------------------------------ PlanAmbush

    [TestMethod]
    public void PlanAmbush_MoreUnspawnedThanN_KeepsNHighestLevels()
    {
        var levels = new[] { 5, 26, 11, 31, 16, 21, 3, 8 };

        var selection = _sut.PlanAmbush(levels, hasBossOrigin: true, canPad: true);

        CollectionAssert.AreEqual(new[] { 1, 3, 4, 5 }, selection.KeepIndices.ToArray(), "31, 26, 21, 16 in original order");
        Assert.AreEqual(4, selection.SpawnCount);
    }

    [TestMethod]
    public void PlanAmbush_FewerUnspawnedThanN_KeepsAllAndSpawnCountIsN()
    {
        // Vanilla pads the list up to spawnCount from its troop-type cache, so the fight is still
        // boss + 4.
        var selection = _sut.PlanAmbush(new[] { 7, 9 }, hasBossOrigin: true, canPad: true);

        CollectionAssert.AreEqual(new[] { 0, 1 }, selection.KeepIndices.ToArray());
        Assert.AreEqual(4, selection.SpawnCount);
    }

    [TestMethod]
    public void PlanAmbush_ExactlyN_KeepsAllAndSpawnCountIsN()
    {
        var selection = _sut.PlanAmbush(new[] { 7, 9, 2, 4 }, hasBossOrigin: true, canPad: true);

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, selection.KeepIndices.ToArray());
        Assert.AreEqual(4, selection.SpawnCount);
    }

    [TestMethod]
    public void PlanAmbush_TiedLevels_KeepInputOrder()
    {
        _settings.HideoutBossBodyguards.Returns(2);

        var selection = _sut.PlanAmbush(new[] { 10, 10, 10, 10 }, hasBossOrigin: true, canPad: true);

        CollectionAssert.AreEqual(new[] { 0, 1 }, selection.KeepIndices.ToArray());
    }

    [TestMethod]
    public void PlanAmbush_BodyguardsZeroWithBoss_SpawnCountZero()
    {
        _settings.HideoutBossBodyguards.Returns(0);

        var selection = _sut.PlanAmbush(new[] { 5, 6, 7 }, hasBossOrigin: true, canPad: true);

        Assert.AreEqual(0, selection.KeepIndices.Count);
        Assert.AreEqual(0, selection.SpawnCount, "the boss spawns from his own origin; nothing else");
    }

    [TestMethod]
    public void PlanAmbush_BodyguardsZeroWithoutBoss_SpawnCountOne()
    {
        // No boss origin and N = 0 would leave SelectBossAgent nothing to pick: keep one.
        _settings.HideoutBossBodyguards.Returns(0);

        var selection = _sut.PlanAmbush(new[] { 5, 9, 7 }, hasBossOrigin: false, canPad: true);

        CollectionAssert.AreEqual(new[] { 1 }, selection.KeepIndices.ToArray(), "the level-9 troop stands in as boss");
        Assert.AreEqual(1, selection.SpawnCount);
    }

    [TestMethod]
    public void PlanAmbush_CannotPad_SpawnCountCappedAtUnspawnedCount()
    {
        // Vanilla's padding derefs GetRandomElement on the troop-type cache; when that cache is
        // empty the spawn count must not exceed what is actually there.
        var selection = _sut.PlanAmbush(new[] { 7, 9 }, hasBossOrigin: true, canPad: false);

        CollectionAssert.AreEqual(new[] { 0, 1 }, selection.KeepIndices.ToArray());
        Assert.AreEqual(2, selection.SpawnCount);
    }

    [TestMethod]
    public void PlanAmbush_EmptyInput_ReturnsNoIndices()
    {
        var padded = _sut.PlanAmbush(new int[0], hasBossOrigin: true, canPad: true);
        Assert.AreEqual(0, padded.KeepIndices.Count);
        Assert.AreEqual(4, padded.SpawnCount, "vanilla pads four from the cache");

        var bare = _sut.PlanAmbush(new int[0], hasBossOrigin: true, canPad: false);
        Assert.AreEqual(0, bare.KeepIndices.Count);
        Assert.AreEqual(0, bare.SpawnCount);
    }

    [TestMethod]
    public void PlanAmbush_KeepIndices_AreDistinctAndInRange()
    {
        _settings.HideoutBossBodyguards.Returns(10);
        var levels = Enumerable.Range(0, 60).Select(i => (i * 7919) % 31).ToArray();

        var selection = _sut.PlanAmbush(levels, hasBossOrigin: true, canPad: true);

        Assert.AreEqual(10, selection.KeepIndices.Count);
        Assert.AreEqual(10, selection.KeepIndices.Distinct().Count());
        Assert.IsTrue(selection.KeepIndices.All(i => i >= 0 && i < levels.Length));
        Assert.IsTrue(selection.KeepIndices.SequenceEqual(selection.KeepIndices.OrderBy(i => i)), "original order");
    }
}
