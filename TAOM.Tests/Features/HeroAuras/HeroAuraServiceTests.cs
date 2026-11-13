using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.HeroAuras;
using TAOM.Features.HeroAuras.Domain;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Tests.Features.HeroAuras;

[TestClass]
public class HeroAuraServiceTests
{
    private const int Human = 0;
    private const int Dwarf = 3;

    private IHeroAurasConfigProvider _config = null!;
    private IRaceManager _races = null!;
    private IModLogger _logger = null!;
    private HeroAuraService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<IHeroAurasConfigProvider>();
        _races = Substitute.For<IRaceManager>();
        _races.IsValidRaceName("dwarf").Returns(true);
        _races.GetRaceIdFromName("dwarf").Returns(Dwarf);
        _logger = Substitute.For<IModLogger>();
        _config.GetConfig().Returns(new HeroAurasConfig
        {
            Radius = 15f,
            Auras = new List<HeroAuraDefinition>
            {
                new HeroAuraDefinition
                {
                    Id = "dain", HeroIds = new List<string> { "lord_E1_1" }, Races = new List<string> { "dwarf" },
                    Effects = new RaceAbilityEffects { KnockdownResistancePercent = 30f },
                },
                new HeroAuraDefinition
                {
                    Id = "aragorn", HeroIds = new List<string> { "named_companion_aragorn" }, Radius = 10f,
                    Effects = new RaceAbilityEffects { DamageReductionPercent = 10f, MoraleFloor = 25f },
                },
            },
        });
        _sut = new HeroAuraService(_config, _races, new RaceAbilityService(), _logger);
    }

    [TestMethod]
    public void AuraFor_ListedHero_ReturnsHisAura() =>
        Assert.AreEqual("dain", _sut.AuraFor("lord_E1_1")!.Id);

    [TestMethod]
    public void AuraFor_HeroIdIgnoresCase() =>
        Assert.AreEqual("dain", _sut.AuraFor("LORD_E1_1")!.Id);

    [TestMethod]
    public void AuraFor_UnlistedOrMissingHero_ReturnsNull()
    {
        Assert.IsNull(_sut.AuraFor("lord_1_17"));
        Assert.IsNull(_sut.AuraFor(null));
    }

    [TestMethod]
    public void AuraFor_OwnRadius_WinsOverTheDefault()
    {
        Assert.AreEqual(10f, _sut.AuraFor("named_companion_aragorn")!.Radius, 0.001f);
        Assert.AreEqual(15f, _sut.AuraFor("lord_E1_1")!.Radius, 0.001f);
    }

    [TestMethod]
    public void Reaches_RaceFilter_OnlyThoseRaces()
    {
        var dain = _sut.AuraFor("lord_E1_1")!;

        Assert.IsTrue(dain.Reaches(Dwarf));
        Assert.IsFalse(dain.Reaches(Human));
        Assert.IsFalse(dain.Reaches(null));
    }

    [TestMethod]
    public void Reaches_NoRaceFilter_Everyone()
    {
        var aragorn = _sut.AuraFor("named_companion_aragorn")!;

        Assert.IsTrue(aragorn.Reaches(Human));
        Assert.IsTrue(aragorn.Reaches(Dwarf));
    }

    [TestMethod]
    public void AuraFor_UnknownRaceName_WarnsAndTheAuraReachesNobody()
    {
        _races.IsValidRaceName("dwarf").Returns(false);

        var dain = _sut.AuraFor("lord_E1_1")!;

        Assert.IsFalse(dain.Reaches(Dwarf));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("dwarf")));
    }

    [TestMethod]
    public void EffectsFor_ScalesByStrength()
    {
        var effects = _sut.EffectsFor(_sut.AuraFor("lord_E1_1")!, strength: 2f);

        Assert.AreEqual(60f, effects.KnockdownResistancePercent, 0.001f);
    }

    [TestMethod]
    public void EffectsFor_SameStrength_ReturnsTheSameObject()
    {
        var aura = _sut.AuraFor("lord_E1_1")!;

        Assert.AreSame(_sut.EffectsFor(aura, 1f), _sut.EffectsFor(aura, 1f),
            "the cache gives every soldier under one aura one shared object per strength");
    }

    [TestMethod]
    public void Merge_TwoAuras_StrongestPerChannel()
    {
        var merged = _sut.Merge(new[]
        {
            new RaceAbilityEffects { DamageReductionPercent = 10f },
            new RaceAbilityEffects { DamageReductionPercent = 5f, KnockdownResistancePercent = 30f },
        });

        Assert.AreEqual(10f, merged.DamageReductionPercent, 0.001f);
        Assert.AreEqual(30f, merged.KnockdownResistancePercent, 0.001f);
    }
}
