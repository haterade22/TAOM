using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.Execution;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// #871: one test per validation rule of the <c>CapturedFortHold</c> section of army_targeting.json, in
/// <c>ArmyTargetingConfigProvider.Validate</c> (csharp-architecture.md, "Config Providers MUST Validate"). A rejected field
/// reverts to its compiled default with a warning, a violated pair reverts both fields, and one summary warning fires.
/// The shipped file is loaded through the real provider and must produce no warning at all.
/// </summary>
[TestClass]
public class CapturedFortHoldConfigTests
{
    private IModLogger _logger = null!;
    private ArmyTargetingConfigProvider _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new ArmyTargetingConfigProvider(Substitute.For<IPathService>(), _logger);
    }

    private static ArmyTargetingConfig With(System.Action<CapturedFortHoldConfig> edit)
    {
        var config = new ArmyTargetingConfig();
        edit(config.CapturedFortHold);
        return config;
    }

    private void AssertWarnedAbout(string fragment) =>
        _logger.Received().LogWarning(Arg.Is<string>(s => s != null && s.Contains(fragment)));

    [TestMethod]
    public void Defaults_AreTheDesignedValues()
    {
        var hold = new CapturedFortHoldConfig();

        CollectionAssert.AreEqual(new[] { "Free" }, hold.Sides);
        Assert.AreEqual(45f, hold.MinCohesionToHold);
        Assert.AreEqual(40f, hold.ReleaseCohesion);
        Assert.AreEqual(1f, hold.ReleaseFoodDays);
        Assert.AreEqual(22f, hold.BaseHours);
        Assert.AreEqual(5f, hold.PerTraitPointHours);
        Assert.AreEqual(12f, hold.MinHours);
        Assert.AreEqual(30f, hold.MaxHours);
    }

    [TestMethod]
    public void Validate_ValidSection_ChangesNothingAndWarnsNothing()
    {
        var result = _sut.Validate(new ArmyTargetingConfig());

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(45f, result.CapturedFortHold.MinCohesionToHold);
    }

    [TestMethod]
    public void Validate_NullSection_BecomesTheDefaults()
    {
        var config = new ArmyTargetingConfig { CapturedFortHold = null! };

        var result = _sut.Validate(config);

        Assert.IsNotNull(result.CapturedFortHold);
        Assert.AreEqual(22f, result.CapturedFortHold.BaseHours);
    }

    [TestMethod]
    public void Validate_NullSides_BecomesFreeOnly()
    {
        var result = _sut.Validate(With(h => h.Sides = null!));

        CollectionAssert.AreEqual(new[] { "Free" }, result.CapturedFortHold.Sides);
    }

    [TestMethod]
    public void Validate_UnknownSide_IsDroppedWithAWarning_TheRestStay()
    {
        var result = _sut.Validate(With(h => h.Sides = new List<string> { "Free", "Shadowy" }));

        CollectionAssert.AreEqual(new[] { "Free" }, result.CapturedFortHold.Sides);
        AssertWarnedAbout("Shadowy");
    }

    [TestMethod]
    public void Validate_NumericAndBlankSides_AreDropped()
    {
        var result = _sut.Validate(With(h => h.Sides = new List<string> { "1", "", "  ", null!, "Neutral" }));

        CollectionAssert.AreEqual(new[] { "Neutral" }, result.CapturedFortHold.Sides);
        _logger.Received(4).LogWarning(Arg.Is<string>(s => s.Contains("Sides")));
    }

    [TestMethod]
    public void Validate_SidesWithOnlyBadEntries_RevertToFree()
    {
        var result = _sut.Validate(With(h => h.Sides = new List<string> { "Orcs" }));

        CollectionAssert.AreEqual(new[] { "Free" }, result.CapturedFortHold.Sides);
        AssertWarnedAbout("reverting to Free");
    }

    [TestMethod]
    public void Validate_EmptySidesList_RevertsToFree()
    {
        var result = _sut.Validate(With(h => h.Sides = new List<string>()));

        CollectionAssert.AreEqual(new[] { "Free" }, result.CapturedFortHold.Sides);
        AssertWarnedAbout("Sides");
    }

    [TestMethod]
    public void Validate_SideNames_AreCanonicalisedAndDeduplicated()
    {
        var result = _sut.Validate(With(h => h.Sides = new List<string> { " free ", "NEUTRAL", "Free" }));

        CollectionAssert.AreEqual(new[] { "Free", "Neutral" }, result.CapturedFortHold.Sides);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Defaults_ListNoFaction()
    {
        CollectionAssert.AreEqual(new string[0], new CapturedFortHoldConfig().Factions);
    }

    [TestMethod]
    public void Validate_NullFactions_BecomesEmpty()
    {
        var result = _sut.Validate(With(h => h.Factions = null!));

        Assert.IsNotNull(result.CapturedFortHold.Factions);
        Assert.AreEqual(0, result.CapturedFortHold.Factions.Count);
    }

    [TestMethod]
    public void Validate_BlankFactionIds_AreDroppedWithAWarning_TheRestStay()
    {
        var result = _sut.Validate(With(h => h.Factions = new List<string> { "empire_s", "", "  ", null! }));

        CollectionAssert.AreEqual(new[] { "empire_s" }, result.CapturedFortHold.Factions);
        _logger.Received(3).LogWarning(Arg.Is<string>(s => s.Contains("Factions")));
    }

    // Kingdom ids are case-sensitive StringIds, so only the spaces are trimmed.
    [TestMethod]
    public void Validate_FactionIds_AreTrimmedAndDeduplicated()
    {
        var result = _sut.Validate(With(h => h.Factions = new List<string> { " empire_s ", "empire_s", "isengard" }));

        CollectionAssert.AreEqual(new[] { "empire_s", "isengard" }, result.CapturedFortHold.Factions);
    }

    [TestMethod]
    public void HoldsArmy_HoldsAListedSideOrAListedRealm()
    {
        var hold = new CapturedFortHoldConfig { Sides = new List<string> { "Free" }, Factions = new List<string> { "empire_s" } };

        Assert.IsTrue(hold.HoldsArmy("empire_w", FactionSide.Free), "listed side");
        Assert.IsTrue(hold.HoldsArmy("empire_s", FactionSide.Evil), "listed realm");
        Assert.IsFalse(hold.HoldsArmy("isengard", FactionSide.Evil), "neither listed");
        Assert.IsFalse(hold.HoldsArmy("", FactionSide.Evil), "no realm");
        Assert.IsFalse(hold.HoldsArmy(null, FactionSide.Evil), "no realm");
    }

    [TestMethod]
    public void HoldsSide_FollowsTheSidesList()
    {
        var hold = new CapturedFortHoldConfig { Sides = new List<string> { "Free", "Neutral" } };

        Assert.IsTrue(hold.HoldsSide(FactionSide.Free));
        Assert.IsTrue(hold.HoldsSide(FactionSide.Neutral));
        Assert.IsFalse(hold.HoldsSide(FactionSide.Evil));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-1f)]
    [DataRow(100.5f)]
    public void Validate_BadMinCohesionToHold_RevertsToDefault(float bad)
    {
        var result = _sut.Validate(With(h => h.MinCohesionToHold = bad));

        Assert.AreEqual(45f, result.CapturedFortHold.MinCohesionToHold);
        AssertWarnedAbout("MinCohesionToHold");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-1f)]
    [DataRow(100.5f)]
    public void Validate_BadReleaseCohesion_RevertsToDefault(float bad)
    {
        var result = _sut.Validate(With(h => h.ReleaseCohesion = bad));

        Assert.AreEqual(40f, result.CapturedFortHold.ReleaseCohesion);
        AssertWarnedAbout("ReleaseCohesion");
    }

    [TestMethod]
    public void Validate_ReleaseCohesionAboveTheHoldFloor_RevertsBothToDefaults()
    {
        var result = _sut.Validate(With(h => { h.MinCohesionToHold = 50f; h.ReleaseCohesion = 60f; }));

        Assert.AreEqual(45f, result.CapturedFortHold.MinCohesionToHold);
        Assert.AreEqual(40f, result.CapturedFortHold.ReleaseCohesion);
        AssertWarnedAbout("ReleaseCohesion");
    }

    [TestMethod]
    public void Validate_ReleaseCohesionEqualToTheHoldFloor_IsAccepted()
    {
        var result = _sut.Validate(With(h => { h.MinCohesionToHold = 50f; h.ReleaseCohesion = 50f; }));

        Assert.AreEqual(50f, result.CapturedFortHold.ReleaseCohesion);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-0.5f)]
    [DataRow(30.5f)]
    public void Validate_BadReleaseFoodDays_RevertsToDefault(float bad)
    {
        var result = _sut.Validate(With(h => h.ReleaseFoodDays = bad));

        Assert.AreEqual(1f, result.CapturedFortHold.ReleaseFoodDays);
        AssertWarnedAbout("ReleaseFoodDays");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0.5f)]
    [DataRow(100.5f)]
    public void Validate_BadBaseHours_RevertsToDefault(float bad)
    {
        var result = _sut.Validate(With(h => h.BaseHours = bad));

        Assert.AreEqual(22f, result.CapturedFortHold.BaseHours);
        AssertWarnedAbout("BaseHours");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-1f)]
    [DataRow(24.5f)]
    public void Validate_BadPerTraitPointHours_RevertsToDefault(float bad)
    {
        var result = _sut.Validate(With(h => h.PerTraitPointHours = bad));

        Assert.AreEqual(5f, result.CapturedFortHold.PerTraitPointHours);
        AssertWarnedAbout("PerTraitPointHours");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(10f)]
    [DataRow(32.5f)]
    [DataRow(40f)]
    public void Validate_BadMinHours_RevertsToDefault(float bad)
    {
        var result = _sut.Validate(With(h => h.MinHours = bad));

        Assert.AreEqual(12f, result.CapturedFortHold.MinHours);
        AssertWarnedAbout("MinHours");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(10f)]
    [DataRow(32.5f)]
    [DataRow(40f)]
    public void Validate_BadMaxHours_RevertsToDefault(float bad)
    {
        var result = _sut.Validate(With(h => h.MaxHours = bad));

        Assert.AreEqual(30f, result.CapturedFortHold.MaxHours);
        AssertWarnedAbout("MaxHours");
    }

    [TestMethod]
    public void Validate_MinHoursAboveMaxHours_RevertsBothToDefaults()
    {
        var result = _sut.Validate(With(h => { h.MinHours = 30f; h.MaxHours = 20f; }));

        Assert.AreEqual(12f, result.CapturedFortHold.MinHours);
        Assert.AreEqual(30f, result.CapturedFortHold.MaxHours);
        AssertWarnedAbout("MinHours");
    }

    [TestMethod]
    public void Validate_HoldHoursAtTheCodeCap_AreAccepted()
    {
        var result = _sut.Validate(With(h => { h.MinHours = 32f; h.MaxHours = 32f; }));

        Assert.AreEqual(32f, result.CapturedFortHold.MaxHours);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Json_SidesReplaceTheDefaultList_NotAppendToIt()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "taom-htw-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "configs"));
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "configs", "army_targeting.json"),
                "{ \"CapturedFortHold\": { \"Sides\": [\"Evil\"] } }");
            var paths = Substitute.For<IPathService>();
            paths.ModuleDataPath.Returns(dir);
            var provider = new ArmyTargetingConfigProvider(paths, _logger);

            var config = provider.GetConfig();

            CollectionAssert.AreEqual(new[] { "Evil" }, config.CapturedFortHold.Sides.ToList(),
                "a file that lists Evil only must not keep the compiled Free");
        }
        finally
        {
            System.IO.Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void MissingSection_LoadsTheCompiledDefaults_FreeArmiesHold()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "taom-htw-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "configs"));
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "configs", "army_targeting.json"), "{ }");
            var paths = Substitute.For<IPathService>();
            paths.ModuleDataPath.Returns(dir);

            var config = new ArmyTargetingConfigProvider(paths, _logger).GetConfig();

            CollectionAssert.AreEqual(new[] { "Free" }, config.CapturedFortHold.Sides.ToList());
            Assert.AreEqual(22f, config.CapturedFortHold.BaseHours);
        }
        finally
        {
            System.IO.Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void Validate_MinHoursEqualToMaxHours_IsAccepted()
    {
        var result = _sut.Validate(With(h => { h.MinHours = 24f; h.MaxHours = 24f; }));

        Assert.AreEqual(24f, result.CapturedFortHold.MinHours);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Validate_AnyRejection_FiresTheSummaryWarning()
    {
        _sut.Validate(With(h => h.BaseHours = float.NaN));

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("contained invalid values")));
    }

    [TestMethod]
    public void TheShippedFile_LoadsThroughTheProviderWithNoWarning_AndTheSectionIsPresent()
    {
        var path = RepoPaths.RepoPath("Main", "_Module", "ModuleData", "configs", "army_targeting.json");
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        var logger = Substitute.For<IModLogger>();
        var provider = new ArmyTargetingConfigProvider(paths, logger);

        var config = provider.GetConfig();

        StringAssert.Contains(System.IO.File.ReadAllText(path), "\"CapturedFortHold\"");
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
        var defaults = new CapturedFortHoldConfig();
        Assert.AreEqual(defaults.MinCohesionToHold, config.CapturedFortHold.MinCohesionToHold);
        Assert.AreEqual(defaults.ReleaseCohesion, config.CapturedFortHold.ReleaseCohesion);
        Assert.AreEqual(defaults.ReleaseFoodDays, config.CapturedFortHold.ReleaseFoodDays);
        Assert.AreEqual(defaults.BaseHours, config.CapturedFortHold.BaseHours);
        Assert.AreEqual(defaults.PerTraitPointHours, config.CapturedFortHold.PerTraitPointHours);
        Assert.AreEqual(defaults.MinHours, config.CapturedFortHold.MinHours);
        Assert.AreEqual(defaults.MaxHours, config.CapturedFortHold.MaxHours);
        CollectionAssert.AreEqual(defaults.Sides, config.CapturedFortHold.Sides.ToList());
    }
}
