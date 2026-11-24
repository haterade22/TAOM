using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.FactionPower;

namespace TAOM.Tests.Features.FactionPower;

[TestClass]
[TestCategory("RequiresGame")]
public class FactionPowerServiceTests
{
    private const float Tolerance = 0.001f;

    private readonly FakeFactionPowerSettings _settings = new FakeFactionPowerSettings();

    private FactionPowerService Create() => new FactionPowerService(_settings);

    private static ExplainedNumber Limit(float value = 100f) => new ExplainedNumber(value);

    [TestMethod]
    public void MultiplierIsOneForNullUnknownAndAHundred()
    {
        _settings.Percents["mordor"] = 100;

        Assert.AreEqual(1f, Create().Multiplier(null));
        Assert.AreEqual(1f, Create().Multiplier("atlantis"));
        Assert.AreEqual(1f, Create().Multiplier("mordor"));
    }

    [TestMethod]
    public void MultiplierFollowsTheStore()
    {
        _settings.Percents["mordor"] = 150;

        Assert.AreEqual(1.5f, Create().Multiplier("mordor"));
    }

    [TestMethod]
    public void ALordParty_IsScaledByTheRealmMultiplier()
    {
        var limit = Limit();

        Create().ScaleLordLimit(1.5f, isMainParty: false, isLordParty: true, hasLeaderHero: true, isPlayerClan: false, ref limit);

        Assert.AreEqual(150f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void TheMainParty_IsNeverScaled()
    {
        var limit = Limit();

        Create().ScaleLordLimit(1.5f, isMainParty: true, isLordParty: true, hasLeaderHero: true, isPlayerClan: false, ref limit);

        Assert.AreEqual(100f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void APlayerClanCompanionParty_IsNeverScaled()
    {
        var limit = Limit();

        Create().ScaleLordLimit(1.5f, isMainParty: false, isLordParty: true, hasLeaderHero: true, isPlayerClan: true, ref limit);

        Assert.AreEqual(100f, limit.ResultNumber, Tolerance);
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public void ACaravanVillagerPatrolBanditOrLeaderlessParty_IsUntouched(bool isLordParty, bool hasLeaderHero)
    {
        var limit = Limit();

        Create().ScaleLordLimit(1.5f, isMainParty: false, isLordParty: isLordParty, hasLeaderHero: hasLeaderHero, isPlayerClan: false, ref limit);

        Assert.AreEqual(100f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void AtAMultiplierOfOne_NothingChanges_AndNoLineIsAdded()
    {
        var limit = new ExplainedNumber(100f, true);
        limit.AddFactor(0.4f);
        var before = (limit.BaseNumber, limit.SumOfFactors, limit.ResultNumber, limit.GetLines().Count);

        Create().ScaleLordLimit(1f, false, true, true, false, ref limit);
        Create().ScaleGarrisonLimit(1f, false, ref limit);

        Assert.AreEqual(before, (limit.BaseNumber, limit.SumOfFactors, limit.ResultNumber, limit.GetLines().Count));
    }

    [TestMethod]
    public void AGarrison_IsScaledByTheRealmMultiplier()
    {
        var limit = Limit(400f);

        Create().ScaleGarrisonLimit(0.5f, isPlayerClan: false, ref limit);

        Assert.AreEqual(200f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void APlayerFiefGarrison_IsNeverScaled()
    {
        var limit = Limit(400f);

        Create().ScaleGarrisonLimit(1.5f, isPlayerClan: true, ref limit);

        Assert.AreEqual(400f, limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void TheGarrisonScale_RidesOnTopOfTheExistingFactors()
    {
        var limit = Limit(200f);
        limit.AddFactor(1f);

        Create().ScaleGarrisonLimit(1.5f, isPlayerClan: false, ref limit);

        Assert.AreEqual(600f, limit.ResultNumber, Tolerance);
    }
}
