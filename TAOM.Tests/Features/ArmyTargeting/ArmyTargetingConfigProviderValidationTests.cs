using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;

namespace TAOM.Tests.Features.ArmyTargeting;

/// <summary>
/// One test per rejection branch in <c>ArmyTargetingConfigProvider.Validate</c>.
///
/// Required by <c>csharp-architecture.md</c> "Config Providers MUST Validate": *Tests must cover
/// semantically-invalid-but-parseable values for every validated field, not just missing-file and
/// malformed-JSON cases. One test per validation rule.* Parse success is not validation success,
/// and this file drives AI target selection, so a sign-flipped weight changes campaign behaviour
/// with no error.
///
/// Its sibling <c>WarTheaterConfigInvariantsTests</c> covers the accept path against the SHIPPED
/// file. That catches a polarity inversion (the shipped values would start tripping the branch and
/// its no-warning assertion would fail) but it cannot catch a widened bound or a deleted rule,
/// because nothing there ever proves a bad value is actually reverted. These do.
/// </summary>
[TestClass]
public class ArmyTargetingConfigProviderValidationTests
{
    private IModLogger _logger;
    private ArmyTargetingConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new ArmyTargetingConfigProvider(Substitute.For<IPathService>(), _logger);
    }

    /// <summary>A config that passes every rule, so each test perturbs exactly one field.</summary>
    private static ArmyTargetingConfig Valid() => new ArmyTargetingConfig
    {
        Theaters = new List<string> { "north", "central", "south", "east" },
        KingdomTheaters = new Dictionary<string, List<string>>
        {
            ["empire_w"] = new List<string> { "south", "central" },
            ["gundabad"] = new List<string> { "north" },
        },
        FactionPriorityTargets = new Dictionary<string, List<string>>(),
        FactionAggressionMultipliers = new Dictionary<string, float>(),
    };

    private static ArmyTargetingConfig Defaults() => new ArmyTargetingConfig();

    private void AssertWarnedAbout(string fragment)
    {
        _logger.Received().LogWarning(Arg.Is<string>(s => s != null && s.Contains(fragment)));
    }

    private void AssertSummaryWarningFired()
    {
        _logger.Received().LogWarning(Arg.Is<string>(s => s != null && s.Contains("contained invalid values")));
    }

    // ------------------------------------------------------------------ accept path

    [TestMethod]
    public void Validate_ValidConfig_LogsInfoAndChangesNothing()
    {
        var config = Valid();
        var result = _sut.Validate(config);

        Assert.AreEqual(Defaults().BorderRescueRadiusInTownGaps, result.BorderRescueRadiusInTownGaps, 0.0001f);
        Assert.AreEqual(2, result.KingdomTheaters["empire_w"].Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received().LogInfo(Arg.Any<string>());
    }

    // ------------------------------------------------------------------ reach radii

    // ------------------------------------------------------------------ reach floor

    // ------------------------------------------------------------------ theater weights

    [TestMethod]
    public void Validate_NaNPrimaryWeight_RevertsToDefault()
    {
        var config = Valid();
        config.PrimaryTheaterWeight = float.NaN;

        Assert.AreEqual(Defaults().PrimaryTheaterWeight, _sut.Validate(config).PrimaryTheaterWeight, 0.0001f);
        AssertWarnedAbout("PrimaryTheaterWeight");
    }

    [TestMethod]
    public void Validate_NegativeSecondaryWeight_RevertsToDefault()
    {
        // A negative multiplier inverts preference outright: the AI would seek what it should avoid.
        var config = Valid();
        config.SecondaryTheaterWeight = -1f;

        Assert.AreEqual(Defaults().SecondaryTheaterWeight, _sut.Validate(config).SecondaryTheaterWeight, 0.0001f);
        AssertWarnedAbout("SecondaryTheaterWeight");
    }

    [TestMethod]
    public void Validate_ZeroForeignWeight_RevertsToDefault()
    {
        var config = Valid();
        config.ForeignTheaterWeight = 0f;

        var result = _sut.Validate(config);

        Assert.AreEqual(Defaults().ForeignTheaterWeight, result.ForeignTheaterWeight, 0.0001f);
        Assert.IsTrue(result.ForeignTheaterWeight > 0f, "zero is a veto, which this design rejects");
        AssertWarnedAbout("ForeignTheaterWeight");
    }

    [TestMethod]
    public void Validate_ForeignWeightOutranksSecondary_RevertsAllThreeWeights()
    {
        // Every value is individually in range, so only the ordering rule catches it. This is the
        // plausible hand edit that inverts the whole feature into "prefer the far war".
        var config = Valid();
        config.ForeignTheaterWeight = 2.5f;

        var result = _sut.Validate(config);

        Assert.AreEqual(Defaults().ForeignTheaterWeight, result.ForeignTheaterWeight, 0.0001f);
        Assert.AreEqual(Defaults().SecondaryTheaterWeight, result.SecondaryTheaterWeight, 0.0001f);
        Assert.AreEqual(Defaults().PrimaryTheaterWeight, result.PrimaryTheaterWeight, 0.0001f);
        AssertWarnedAbout("must be ordered");
    }

    [TestMethod]
    public void Validate_SecondaryWeightOutranksPrimary_RevertsAllThreeWeights()
    {
        var config = Valid();
        config.SecondaryTheaterWeight = 3f;

        var result = _sut.Validate(config);

        Assert.AreEqual(Defaults().SecondaryTheaterWeight, result.SecondaryTheaterWeight, 0.0001f);
        Assert.AreEqual(Defaults().PrimaryTheaterWeight, result.PrimaryTheaterWeight, 0.0001f);
        Assert.IsTrue(result.ForeignTheaterWeight <= result.SecondaryTheaterWeight);
        Assert.IsTrue(result.SecondaryTheaterWeight <= result.PrimaryTheaterWeight);
        AssertWarnedAbout("must be ordered");
    }

    // ------------------------------------------------------------------ priority targets

    [TestMethod]
    public void Validate_NullPriorityTargetId_IsSkippedWithWarning()
    {
        // A null id would be used as a Dictionary key while the service builds its index, throwing
        // ArgumentNullException during model registration, i.e. at campaign start.
        var config = Valid();
        config.FactionPriorityTargets["empire_s"] = new List<string> { null, "town_EW3" };

        var result = _sut.Validate(config);

        CollectionAssert.AreEqual(new List<string> { "town_EW3" }, result.FactionPriorityTargets["empire_s"]);
        AssertWarnedAbout("null or blank priority target");
        AssertSummaryWarningFired();
    }

    [TestMethod]
    public void Validate_BlankPriorityTargetId_IsSkippedWithWarning()
    {
        var config = Valid();
        config.FactionPriorityTargets["empire_s"] = new List<string> { "   ", "town_EW3" };

        Assert.AreEqual(1, _sut.Validate(config).FactionPriorityTargets["empire_s"].Count);
        AssertWarnedAbout("null or blank priority target");
    }

    [TestMethod]
    public void Validate_DuplicatePriorityTargetId_KeepsFirstPositionOnly()
    {
        // The duplicate is what makes the boost go NEGATIVE downstream: the deduped dictionary
        // Count stops climbing while the raw index does not, so t exceeds 1.
        var config = Valid();
        config.FactionPriorityTargets["empire_s"] = new List<string> { "town_A", "town_A", "town_B" };

        var result = _sut.Validate(config);

        CollectionAssert.AreEqual(new List<string> { "town_A", "town_B" }, result.FactionPriorityTargets["empire_s"]);
        AssertWarnedAbout("more than once");
    }

    [TestMethod]
    public void Validate_NullPriorityList_CoercesToEmpty()
    {
        var config = Valid();
        config.FactionPriorityTargets["empire_s"] = null;

        Assert.IsNotNull(_sut.Validate(config).FactionPriorityTargets["empire_s"]);
    }

    // ------------------------------------------------------------------ theater membership

    [TestMethod]
    public void Validate_UndeclaredTheaterName_SkipsItAndKeepsTheRest()
    {
        // The dead-key trap, applied to theater names: an undeclared name would match no other
        // kingdom, so the front would silently vanish for whoever listed it.
        var config = Valid();
        config.KingdomTheaters["empire_w"] = new List<string> { "south", "nrth" };

        var result = _sut.Validate(config);

        CollectionAssert.AreEqual(new List<string> { "south" }, result.KingdomTheaters["empire_w"]);
        AssertWarnedAbout("undeclared theater");
        AssertSummaryWarningFired();
    }

    [TestMethod]
    public void Validate_NullTheaterList_CoercesToEmptyWithoutWarning()
    {
        // Null and empty both mean "deliberately passive", which is a legitimate authored state
        // (bluecraig, lindon), so this must not be reported as invalid.
        var config = Valid();
        config.KingdomTheaters["bluecraig"] = null;

        var result = _sut.Validate(config);

        Assert.IsNotNull(result.KingdomTheaters["bluecraig"]);
        Assert.AreEqual(0, result.KingdomTheaters["bluecraig"].Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Validate_NullDictionaries_CoerceToEmptyWithoutThrowing()
    {
        // A hand-edited file can omit any section entirely. The service constructor indexes all of
        // these, so a null here would NRE at startup rather than degrading to vanilla.
        var config = Valid();
        config.Theaters = null;
        config.KingdomTheaters = null;
        config.FactionPriorityTargets = null;
        config.FactionAggressionMultipliers = null;

        var result = _sut.Validate(config);

        Assert.IsNotNull(result.Theaters);
        Assert.IsNotNull(result.KingdomTheaters);
        Assert.IsNotNull(result.FactionPriorityTargets);
        Assert.IsNotNull(result.FactionAggressionMultipliers);
    }
}
