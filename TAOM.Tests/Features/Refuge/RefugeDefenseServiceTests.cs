using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Domain;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// Hot-path probe coverage for <see cref="RefugeDefenseService"/>: tier factors, the not-ready
/// and unknown-id zeroes, and the non-finite settings degradation gates.
/// </summary>
[TestClass]
public class RefugeDefenseServiceTests
{
    private IRefugeBook _book;
    private IRefugeSettingsProvider _settings;
    private RefugeDefenseService _sut;

    [TestInitialize]
    public void Setup()
    {
        _book = Substitute.For<IRefugeBook>();
        _settings = Substitute.For<IRefugeSettingsProvider>();
        _settings.RefugeDefenseBonus.Returns(0.2f);
        _settings.StrongholdDefenseBonus.Returns(0.35f);
        _sut = new RefugeDefenseService(_book, _settings);
    }

    private RefugeData ReadyRefuge(RefugeTier tier) => new RefugeData
    {
        PartyId = "taom_refuge_0",
        TierEnum = tier,
        Established = true,
        Building = false,
    };

    [TestMethod]
    public void DefenderDamageReduction_ReadyRefuge_ReturnsRefugeBonus()
    {
        _book.GetByPartyId("taom_refuge_0").Returns(ReadyRefuge(RefugeTier.Refuge));

        Assert.AreEqual(0.2f, _sut.DefenderDamageReduction("taom_refuge_0"), 0.0001f);
    }

    [TestMethod]
    public void DefenderDamageReduction_ReadyStronghold_ReturnsStrongholdBonus()
    {
        _book.GetByPartyId("taom_refuge_0").Returns(ReadyRefuge(RefugeTier.Stronghold));

        Assert.AreEqual(0.35f, _sut.DefenderDamageReduction("taom_refuge_0"), 0.0001f);
    }

    [TestMethod]
    public void DefenderDamageReduction_UnknownPartyId_ReturnsZero()
    {
        _book.GetByPartyId(Arg.Any<string>()).Returns((RefugeData)null);

        Assert.AreEqual(0f, _sut.DefenderDamageReduction("some_lord_party"), 0.0001f);
    }

    [TestMethod]
    public void DefenderDamageReduction_NullPartyId_ReturnsZeroWithoutProbe()
    {
        Assert.AreEqual(0f, _sut.DefenderDamageReduction(null), 0.0001f);
        _book.DidNotReceive().GetByPartyId(Arg.Any<string>());
    }

    [TestMethod]
    public void DefenderDamageReduction_StillRaising_ReturnsZero()
    {
        var raising = ReadyRefuge(RefugeTier.Refuge);
        raising.Established = false;
        raising.Building = true;
        _book.GetByPartyId("taom_refuge_0").Returns(raising);

        Assert.AreEqual(0f, _sut.DefenderDamageReduction("taom_refuge_0"), 0.0001f);
    }

    [TestMethod]
    public void DefenderDamageReduction_StrongholdRebuildWindow_ReturnsZero()
    {
        var rebuilding = ReadyRefuge(RefugeTier.Refuge);
        rebuilding.Building = true;
        rebuilding.BuildingUpgrade = true;
        _book.GetByPartyId("taom_refuge_0").Returns(rebuilding);

        Assert.AreEqual(0f, _sut.DefenderDamageReduction("taom_refuge_0"), 0.0001f,
            "IsReady is Established AND not Building; the rebuild drops the bonus by design");
    }

    [TestMethod]
    public void DefenderDamageReduction_NaNSetting_DegradesToZero()
    {
        _settings.RefugeDefenseBonus.Returns(float.NaN);
        _book.GetByPartyId("taom_refuge_0").Returns(ReadyRefuge(RefugeTier.Refuge));

        Assert.AreEqual(0f, _sut.DefenderDamageReduction("taom_refuge_0"), 0.0001f);
    }

    [TestMethod]
    public void DefenderDamageReduction_InfiniteStrongholdSetting_DegradesToZero()
    {
        _settings.StrongholdDefenseBonus.Returns(float.PositiveInfinity);
        _book.GetByPartyId("taom_refuge_0").Returns(ReadyRefuge(RefugeTier.Stronghold));

        Assert.AreEqual(0f, _sut.DefenderDamageReduction("taom_refuge_0"), 0.0001f);
    }

    [TestMethod]
    public void DefenderDamageReduction_NegativeSetting_DegradesToZero()
    {
        _settings.RefugeDefenseBonus.Returns(-0.5f);
        _book.GetByPartyId("taom_refuge_0").Returns(ReadyRefuge(RefugeTier.Refuge));

        Assert.AreEqual(0f, _sut.DefenderDamageReduction("taom_refuge_0"), 0.0001f,
            "a negative factor would AMPLIFY damage against the defender");
    }

    [TestMethod]
    public void DefenderDamageReduction_SettingAboveOne_DegradesToZero()
    {
        _settings.RefugeDefenseBonus.Returns(1.5f);
        _book.GetByPartyId("taom_refuge_0").Returns(ReadyRefuge(RefugeTier.Refuge));

        Assert.AreEqual(0f, _sut.DefenderDamageReduction("taom_refuge_0"), 0.0001f,
            "a factor above 1 would heal defenders per hit");
    }
}
