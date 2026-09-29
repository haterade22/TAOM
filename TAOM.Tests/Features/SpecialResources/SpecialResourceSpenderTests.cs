using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// <see cref="ISpecialResourceSpender"/>: the narrow, affordability-checked spend the armour acquisition
/// armoury uses to forge lord kit (docs/features/armour-acquisition.md). It never overdraws, never lets
/// a non-finite amount near the saved balance, and reports "no resource" rather than spending when the
/// hero's kingdom and culture map to none. Real storage, so the balance read back is the saved one.
/// </summary>
[TestClass]
public class SpecialResourceSpenderTests
{
    private const string HeroId = "hero_player";

    private ISpecialResourceConfigProvider _config = null!;
    private SpecialResourceStorageService _storage = null!;
    private ISpecialResourceSpender _spender = null!;

    private static readonly SpecialResource Castar = new(
        id: "castar",
        kingdomIds: new[] { "empire" },
        cultureIds: new[] { "gondor" },
        displayName: "Castar",
        iconSpriteName: "taom_castar_icon",
        cap: 10000f,
        startingAmount: 0f,
        dailyPerTown: 0.5f,
        perBattleVictoryBase: 10f,
        perRaid: 8f,
        perSiegeVictory: 15f,
        perPrisoner: 1f);

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ISpecialResourceConfigProvider>();
        _config.GetByKingdomId("empire").Returns(Castar);
        _config.GetByCultureId("gondor").Returns(Castar);
        _storage = new SpecialResourceStorageService();
        _spender = new SpecialResourceService(_config, _storage, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void GetBalance_ResolvedResource_ReportsNameAndAmount()
    {
        _storage.Set(HeroId, "castar", 42f);

        var balance = _spender.GetBalance(HeroId, "empire", "gondor");

        Assert.IsNotNull(balance);
        Assert.AreEqual("Castar", balance!.DisplayName);
        Assert.AreEqual(42f, balance.Amount);
    }

    [TestMethod]
    public void GetBalance_NoResourceForTheKingdomOrCulture_IsNull()
    {
        Assert.IsNull(_spender.GetBalance(HeroId, "unknown_kingdom", "unknown_culture"));
    }

    [TestMethod]
    public void TrySpend_BalanceCoversIt_DebitsAndReturnsTrue()
    {
        _storage.Set(HeroId, "castar", 200f);

        Assert.IsTrue(_spender.TrySpend(HeroId, "empire", "gondor", 150f));
        Assert.AreEqual(50f, _storage.Get(HeroId, "castar"));
    }

    [TestMethod]
    public void TrySpend_BalanceShort_ChangesNothingAndReturnsFalse()
    {
        _storage.Set(HeroId, "castar", 100f);

        Assert.IsFalse(_spender.TrySpend(HeroId, "empire", "gondor", 150f));
        Assert.AreEqual(100f, _storage.Get(HeroId, "castar"));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-5f)]
    [DataRow(0f)]
    public void TrySpend_NotAPositiveFiniteAmount_IsRefused(float amount)
    {
        _storage.Set(HeroId, "castar", 100f);

        Assert.IsFalse(_spender.TrySpend(HeroId, "empire", "gondor", amount));
        Assert.AreEqual(100f, _storage.Get(HeroId, "castar"));
    }

    [TestMethod]
    public void TrySpend_NoResourceForTheKingdomOrCulture_IsRefused()
    {
        Assert.IsFalse(_spender.TrySpend(HeroId, "unknown_kingdom", "unknown_culture", 1f));
    }
}
