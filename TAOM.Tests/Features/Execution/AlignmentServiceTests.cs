using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.Execution;

[TestClass]
public class AlignmentServiceTests
{
    private IAlignmentConfigProvider _configProvider;
    private IModLogger _logger;
    private AlignmentService _sut;

    [TestInitialize]
    public void Setup()
    {
        _configProvider = Substitute.For<IAlignmentConfigProvider>();
        _logger = Substitute.For<IModLogger>();

        _configProvider.LoadAlignments().Returns(new Dictionary<string, string>
        {
            { "empire_w", "free" },
            { "vlandia", "free" },
            { "erebor", "free" },
            { "rivendell", "free" },
            { "empire_s", "evil" },
            { "isengard", "evil" },
            { "gundabad", "evil" },
            { "umbar", "neutral" },
            // Custom-culture keys whose ids differ from their kingdom id (empire_w/empire_s).
            { "gondor", "free" },
            { "mordor", "evil" }
        });

        _sut = new AlignmentService(_configProvider, _logger);
    }

    [TestMethod]
    public void GetKingdomSide_FreeKingdom_ReturnsFree()
    {
        Assert.AreEqual(FactionSide.Free, _sut.GetKingdomSide("empire_w"));
    }

    [TestMethod]
    public void GetKingdomSide_EvilKingdom_ReturnsEvil()
    {
        Assert.AreEqual(FactionSide.Evil, _sut.GetKingdomSide("empire_s"));
    }

    [TestMethod]
    public void GetKingdomSide_NeutralKingdom_ReturnsNeutral()
    {
        Assert.AreEqual(FactionSide.Neutral, _sut.GetKingdomSide("umbar"));
    }

    [TestMethod]
    public void GetKingdomSide_UnknownKingdom_ReturnsNeutral()
    {
        Assert.AreEqual(FactionSide.Neutral, _sut.GetKingdomSide("unknown_kingdom"));
    }

    [TestMethod]
    public void GetKingdomSide_Null_ReturnsNeutral()
    {
        Assert.AreEqual(FactionSide.Neutral, _sut.GetKingdomSide(null));
    }

    [TestMethod]
    public void GetCultureSide_GondorCulture_ReturnsFree()
    {
        // gondor culture id != empire_w kingdom id — must resolve via the explicit gondor entry.
        Assert.AreEqual(FactionSide.Free, _sut.GetCultureSide("gondor"));
    }

    [TestMethod]
    public void GetCultureSide_MordorCulture_ReturnsEvil()
    {
        Assert.AreEqual(FactionSide.Evil, _sut.GetCultureSide("mordor"));
    }

    [TestMethod]
    public void GetCultureSide_NeutralCulture_ReturnsNeutral()
    {
        Assert.AreEqual(FactionSide.Neutral, _sut.GetCultureSide("umbar"));
    }

    [TestMethod]
    public void GetCultureSide_UnknownCulture_ReturnsNeutral()
    {
        // Bandit/minor cultures (umbar_corsairs, gondor_soldiers, ...) are absent → Neutral → never desert.
        Assert.AreEqual(FactionSide.Neutral, _sut.GetCultureSide("umbar_corsairs"));
    }

    [TestMethod]
    public void GetCultureSide_Null_ReturnsNeutral()
    {
        Assert.AreEqual(FactionSide.Neutral, _sut.GetCultureSide(null));
    }

    [TestMethod]
    public void AreEnemyAlignments_FreeVsEvil_ReturnsTrue()
    {
        Assert.IsTrue(_sut.AreEnemyAlignments("empire_w", "empire_s"));
    }

    [TestMethod]
    public void AreEnemyAlignments_EvilVsFree_ReturnsTrue()
    {
        Assert.IsTrue(_sut.AreEnemyAlignments("empire_s", "empire_w"));
    }

    [TestMethod]
    public void AreEnemyAlignments_FreeVsNeutral_ReturnsTrue()
    {
        Assert.IsTrue(_sut.AreEnemyAlignments("empire_w", "umbar"));
    }

    [TestMethod]
    public void AreEnemyAlignments_EvilVsNeutral_ReturnsTrue()
    {
        Assert.IsTrue(_sut.AreEnemyAlignments("empire_s", "umbar"));
    }

    [TestMethod]
    public void AreEnemyAlignments_FreeVsFree_ReturnsFalse()
    {
        Assert.IsFalse(_sut.AreEnemyAlignments("empire_w", "vlandia"));
    }

    [TestMethod]
    public void AreEnemyAlignments_EvilVsEvil_ReturnsFalse()
    {
        Assert.IsFalse(_sut.AreEnemyAlignments("empire_s", "isengard"));
    }

    [TestMethod]
    public void AreEnemyAlignments_NeutralVsNeutral_ReturnsTrue()
    {
        // Neutrals are not allied with each other
        Assert.IsTrue(_sut.AreEnemyAlignments("umbar", "umbar"));
    }

    [TestMethod]
    public void AreSameAlignment_FreeVsFree_ReturnsTrue()
    {
        Assert.IsTrue(_sut.AreSameAlignment("empire_w", "vlandia"));
    }

    [TestMethod]
    public void AreSameAlignment_EvilVsEvil_ReturnsTrue()
    {
        Assert.IsTrue(_sut.AreSameAlignment("empire_s", "isengard"));
    }

    [TestMethod]
    public void AreSameAlignment_FreeVsEvil_ReturnsFalse()
    {
        Assert.IsFalse(_sut.AreSameAlignment("empire_w", "empire_s"));
    }

    [TestMethod]
    public void AreSameAlignment_NeutralVsNeutral_ReturnsFalse()
    {
        // Neutrals are NOT considered same-alignment
        Assert.IsFalse(_sut.AreSameAlignment("umbar", "umbar"));
    }

    [TestMethod]
    public void AreSameAlignment_FreeVsNeutral_ReturnsFalse()
    {
        Assert.IsFalse(_sut.AreSameAlignment("empire_w", "umbar"));
    }

    [TestMethod]
    public void AreSameAlignment_NullKingdom_ReturnsFalse()
    {
        Assert.IsFalse(_sut.AreSameAlignment(null, "empire_w"));
        Assert.IsFalse(_sut.AreSameAlignment("empire_w", null));
    }

    // ----- ResolveSide: kingdom first, culture as the fallback -----

    [TestMethod]
    public void ResolveSide_ClassifiedKingdom_UsesKingdomAndIgnoresCulture()
    {
        Assert.AreEqual(FactionSide.Free, _sut.ResolveSide("empire_w", "mordor"));
    }

    [TestMethod]
    public void ResolveSide_NullKingdom_FallsBackToCulture()
    {
        Assert.AreEqual(FactionSide.Free, _sut.ResolveSide(null, "gondor"));
    }

    [TestMethod]
    public void ResolveSide_EmptyKingdom_FallsBackToCulture()
    {
        // GetPlayerKingdomId() returns "" for a player whose clan has no kingdom.
        Assert.AreEqual(FactionSide.Evil, _sut.ResolveSide("", "mordor"));
    }

    [TestMethod]
    public void ResolveSide_UnclassifiedKingdom_FallsBackToCulture()
    {
        // A player-founded kingdom gets a generated id that is absent from alignment.json.
        Assert.AreEqual(FactionSide.Free, _sut.ResolveSide("new_kingdom", "gondor"));
    }

    [TestMethod]
    public void ResolveSide_NeutralKingdomWithClassifiedCulture_FallsBackToCulture()
    {
        // Neutral is indistinguishable from unclassified in the id table, so the culture wins.
        Assert.AreEqual(FactionSide.Evil, _sut.ResolveSide("umbar", "mordor"));
    }

    [TestMethod]
    public void ResolveSide_NeitherIdClassified_ReturnsNeutral()
    {
        Assert.AreEqual(FactionSide.Neutral, _sut.ResolveSide("new_kingdom", "made_up_culture"));
    }

    [TestMethod]
    public void ResolveSide_BothIdsNull_ReturnsNeutral()
    {
        Assert.AreEqual(FactionSide.Neutral, _sut.ResolveSide(null, null));
    }

    // ----- Side-based predicate overloads carry the same Neutral semantics as the string ones -----

    [TestMethod]
    public void AreEnemyAlignments_Sides_FreeVsEvil_ReturnsTrue()
    {
        Assert.IsTrue(_sut.AreEnemyAlignments(FactionSide.Free, FactionSide.Evil));
    }

    [TestMethod]
    public void AreEnemyAlignments_Sides_FreeVsFree_ReturnsFalse()
    {
        Assert.IsFalse(_sut.AreEnemyAlignments(FactionSide.Free, FactionSide.Free));
    }

    [TestMethod]
    public void AreEnemyAlignments_Sides_NeutralVsNeutral_ReturnsTrue()
    {
        Assert.IsTrue(_sut.AreEnemyAlignments(FactionSide.Neutral, FactionSide.Neutral));
    }

    [TestMethod]
    public void AreSameAlignment_Sides_EvilVsEvil_ReturnsTrue()
    {
        Assert.IsTrue(_sut.AreSameAlignment(FactionSide.Evil, FactionSide.Evil));
    }

    [TestMethod]
    public void AreSameAlignment_Sides_NeutralVsNeutral_ReturnsFalse()
    {
        Assert.IsFalse(_sut.AreSameAlignment(FactionSide.Neutral, FactionSide.Neutral));
    }
}
