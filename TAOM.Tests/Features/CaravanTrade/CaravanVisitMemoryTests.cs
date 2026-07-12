using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CaravanTrade;

namespace TAOM.Tests.Features.CaravanTrade;

[TestClass]
public class CaravanVisitMemoryTests
{
    private ICaravanTradeSettingsProvider _settings = null!;
    private CaravanVisitMemory _sut = null!;

    private const string Caravan = "caravan_1";

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ICaravanTradeSettingsProvider>();
        _settings.AntiShuttlePenalty.Returns(0.5f); // strength
        _sut = new CaravanVisitMemory(_settings);
    }

    // ---------------- no-penalty / passthrough ----------------

    [TestMethod]
    public void GetRecencyPenaltyFactor_UnknownCaravan_ReturnsOne()
    {
        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor("nobody", "town_A"), 0.0001f);
    }

    [TestMethod]
    public void GetRecencyPenaltyFactor_TownNotVisited_ReturnsOne()
    {
        _sut.RecordVisit(Caravan, "town_A");
        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_Z"), 0.0001f);
    }

    [TestMethod]
    public void GetRecencyPenaltyFactor_NullOrEmptyIds_ReturnsOne()
    {
        _sut.RecordVisit(Caravan, "town_A");
        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(null!, "town_A"), 0.0001f);
        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, ""), 0.0001f);
    }

    [TestMethod]
    public void RecordVisit_NullIds_DoesNotThrow()
    {
        _sut.RecordVisit(null!, "town_A");
        _sut.RecordVisit(Caravan, null!);
        // No entry recorded -> no penalty.
        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
    }

    // ---------------- the core fix: previous town IS penalized ----------------

    [TestMethod]
    public void GetRecencyPenaltyFactor_PreviousTown_IsPenalized()
    {
        // Regression sentinel for the inert-penalty bug. Caravan went A -> B; now parked at B and
        // deciding the next hop. The PREVIOUS town A must get a live penalty (< 1.0). The old design
        // (isJustLeft == LastVisitedSettlement == the parked/current town) produced NO penalty on any
        // selectable town.
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");

        float previous = _sut.GetRecencyPenaltyFactor(Caravan, "town_A"); // rank 1
        Assert.IsTrue(previous < 1f, $"previous town must be penalized, was {previous}");
    }

    // ---------------- recency decay ----------------

    [TestMethod]
    public void GetRecencyPenaltyFactor_MostRecentTown_ReturnsStrongestPenalty()
    {
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");
        _sut.RecordVisit(Caravan, "town_C");
        _sut.RecordVisit(Caravan, "town_D"); // newest

        float d = _sut.GetRecencyPenaltyFactor(Caravan, "town_D"); // rank 0
        float c = _sut.GetRecencyPenaltyFactor(Caravan, "town_C"); // rank 1
        float a = _sut.GetRecencyPenaltyFactor(Caravan, "town_A"); // rank 3
        Assert.IsTrue(d < c, $"newest ({d}) must be penalized more than older ({c})");
        Assert.IsTrue(c < a, $"middle ({c}) must be penalized more than oldest ({a})");
    }

    [TestMethod]
    public void GetRecencyPenaltyFactor_OlderTown_ReturnsWeakerPenalty()
    {
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");
        _sut.RecordVisit(Caravan, "town_C");
        _sut.RecordVisit(Caravan, "town_D");

        // rank 1 = C: 1 - 0.5*(4-1)/4 = 1 - 0.375 = 0.625.
        Assert.AreEqual(0.625f, _sut.GetRecencyPenaltyFactor(Caravan, "town_C"), 0.0001f);
        // rank 3 = A: 1 - 0.5*(4-3)/4 = 1 - 0.125 = 0.875.
        Assert.AreEqual(0.875f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
    }

    [TestMethod]
    public void GetRecencyPenaltyFactor_RevisitedTown_UsesMostRecentRank()
    {
        // C, A, B, A -> A appears at rank 3 (old) and rank 0 (new). Must use the most recent (rank 0),
        // i.e. the strongest penalty, not the stale weaker one.
        _sut.RecordVisit(Caravan, "town_C");
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");
        _sut.RecordVisit(Caravan, "town_A"); // newest, rank 0

        // rank 0: 1 - 0.5*1 = 0.5.
        Assert.AreEqual(0.5f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
    }

    // ---------------- bounded ring ----------------

    [TestMethod]
    public void GetRecencyPenaltyFactor_RingBoundedToK_EvictsOldest()
    {
        // 5 distinct towns, depth 4 -> the oldest (A) is evicted and no longer penalized.
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");
        _sut.RecordVisit(Caravan, "town_C");
        _sut.RecordVisit(Caravan, "town_D");
        _sut.RecordVisit(Caravan, "town_E");

        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
        Assert.IsTrue(_sut.GetRecencyPenaltyFactor(Caravan, "town_B") < 1f);
    }

    [TestMethod]
    public void RecordVisit_ConsecutiveSameTown_DoesNotConsumeRing()
    {
        // Re-entering the same town repeatedly must not evict the earlier towns.
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");
        _sut.RecordVisit(Caravan, "town_B");
        _sut.RecordVisit(Caravan, "town_B");

        Assert.IsTrue(_sut.GetRecencyPenaltyFactor(Caravan, "town_A") < 1f, "A must still be remembered");
    }

    // ---------------- no stranding ----------------

    [TestMethod]
    public void GetRecencyPenaltyFactor_NeverReturnsZero_NoStranding()
    {
        // Max strength: even the most-recent town keeps a strictly-positive floor so it still
        // outscores a non-candidate (score 0 / -1) and a sparse-region caravan is never stranded.
        _settings.AntiShuttlePenalty.Returns(1.0f);
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");
        _sut.RecordVisit(Caravan, "town_C");
        _sut.RecordVisit(Caravan, "town_D");

        foreach (var t in new[] { "town_A", "town_B", "town_C", "town_D" })
            Assert.IsTrue(_sut.GetRecencyPenaltyFactor(Caravan, t) > 0f, $"{t} must stay positive");

        // rank 0 at strength 1 would be 0 -> floored to 0.05.
        Assert.AreEqual(0.05f, _sut.GetRecencyPenaltyFactor(Caravan, "town_D"), 0.0001f);
    }

    // ---------------- strength gates ----------------

    [TestMethod]
    public void GetRecencyPenaltyFactor_StrengthZero_ReturnsOne()
    {
        _settings.AntiShuttlePenalty.Returns(0f);
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");
        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
    }

    [TestMethod]
    public void GetRecencyPenaltyFactor_NaNStrength_ReturnsOne()
    {
        // NaN gate: a corrupted strength must not emit a NaN/garbage factor.
        _settings.AntiShuttlePenalty.Returns(float.NaN);
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");
        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
    }

    [TestMethod]
    public void GetRecencyPenaltyFactor_OutOfRangeStrength_ReturnsOne()
    {
        _settings.AntiShuttlePenalty.Returns(1.5f);
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");
        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
    }

    // ---------------- Clear ----------------

    [TestMethod]
    public void Clear_RemovesCaravanMemory_ReturnsOneAfter()
    {
        _sut.RecordVisit(Caravan, "town_A");
        _sut.RecordVisit(Caravan, "town_B");
        Assert.IsTrue(_sut.GetRecencyPenaltyFactor(Caravan, "town_A") < 1f);

        _sut.Clear(Caravan);
        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
    }

    [TestMethod]
    public void Clear_UnknownCaravan_DoesNotThrow()
    {
        _sut.Clear("nobody");
        _sut.Clear(null!);
    }

    [TestMethod]
    public void ClearAll_RemovesEveryCaravan_ReturnsOneAfter()
    {
        // Cross-campaign residue guard: a session-launch ClearAll must wipe ALL caravans' rings so a
        // reused StringId in a new campaign starts fresh.
        _sut.RecordVisit("caravan_1", "town_A");
        _sut.RecordVisit("caravan_1", "town_B");
        _sut.RecordVisit("caravan_2", "town_C");
        _sut.RecordVisit("caravan_2", "town_D");
        Assert.IsTrue(_sut.GetRecencyPenaltyFactor("caravan_1", "town_A") < 1f);
        Assert.IsTrue(_sut.GetRecencyPenaltyFactor("caravan_2", "town_C") < 1f);

        _sut.ClearAll();

        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor("caravan_1", "town_A"), 0.0001f);
        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor("caravan_2", "town_C"), 0.0001f);
    }
}
