using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Activities.Hooks;
using TAOM.Features.FieldCamp.UI;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// Which reason the Make Camp icon's tooltip names while the button is blocked (a blocked click did nothing and the
/// tooltip never said why, so the button looked broken). The picker is pure over
/// <see cref="ICampMenuActivationQuery"/>; text assertions use Contains on the English fallback so they hold whether
/// or not the localization layer strips the <c>{=key}</c> prefix in a bare test process.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CampButtonBlockTests
{
    private ICampMenuActivationQuery _activation = null!;

    [TestInitialize]
    public void Setup()
    {
        _activation = Substitute.For<ICampMenuActivationQuery>();

        // All-clear baseline; each test flips the guards it is about.
        _activation.IsMapScreenClear.Returns(true);
        _activation.IsMainPartyStationary.Returns(true);
        _activation.IsMainPartyInSettlement.Returns(false);
        _activation.IsMainPartyInEncounter.Returns(false);
        _activation.IsMainPartyDisorganized.Returns(false);
        _activation.DefersToCoopHost.Returns(false);
    }

    // ---- Pick: one state per guard ----

    [TestMethod]
    public void Pick_AllClear_ReturnsNone()
    {
        Assert.AreEqual(CampButtonBlock.None, CampButtonBlocks.Pick(_activation));
    }

    [TestMethod]
    public void Pick_InSettlement_ReturnsInSettlement()
    {
        _activation.IsMainPartyInSettlement.Returns(true);

        Assert.AreEqual(CampButtonBlock.InSettlement, CampButtonBlocks.Pick(_activation));
    }

    [TestMethod]
    public void Pick_InEncounter_ReturnsInEncounter()
    {
        _activation.IsMainPartyInEncounter.Returns(true);

        Assert.AreEqual(CampButtonBlock.InEncounter, CampButtonBlocks.Pick(_activation));
    }

    [TestMethod]
    public void Pick_MapNotClear_ReturnsMenuOpen()
    {
        _activation.IsMapScreenClear.Returns(false);

        Assert.AreEqual(CampButtonBlock.MenuOpen, CampButtonBlocks.Pick(_activation));
    }

    [TestMethod]
    public void Pick_PartyMoving_ReturnsMoving()
    {
        _activation.IsMainPartyStationary.Returns(false);

        Assert.AreEqual(CampButtonBlock.Moving, CampButtonBlocks.Pick(_activation));
    }

    [TestMethod]
    public void Pick_Disorganized_ReturnsDisorganized()
    {
        _activation.IsMainPartyDisorganized.Returns(true);

        Assert.AreEqual(CampButtonBlock.Disorganized, CampButtonBlocks.Pick(_activation));
    }

    // ---- Pick: when several guards hold, the cause wins over the symptom ----

    [TestMethod]
    public void Pick_InSettlementWithTownMenuOpen_ReportsTheSettlementNotTheMenu()
    {
        // Standing in a town always has its game menu open, so the map is not clear too.
        _activation.IsMainPartyInSettlement.Returns(true);
        _activation.IsMapScreenClear.Returns(false);

        Assert.AreEqual(CampButtonBlock.InSettlement, CampButtonBlocks.Pick(_activation));
    }

    [TestMethod]
    public void Pick_InEncounterWithMenuOpen_ReportsTheEncounterNotTheMenu()
    {
        _activation.IsMainPartyInEncounter.Returns(true);
        _activation.IsMapScreenClear.Returns(false);

        Assert.AreEqual(CampButtonBlock.InEncounter, CampButtonBlocks.Pick(_activation));
    }

    [TestMethod]
    public void Pick_MenuOpenAndMoving_ReportsTheMenu()
    {
        _activation.IsMapScreenClear.Returns(false);
        _activation.IsMainPartyStationary.Returns(false);

        Assert.AreEqual(CampButtonBlock.MenuOpen, CampButtonBlocks.Pick(_activation));
    }

    [TestMethod]
    public void Pick_MovingAndDisorganized_ReportsMoving()
    {
        // A party leaving a town is both: it can only fix "moving" by stopping, and that is the first step.
        _activation.IsMainPartyStationary.Returns(false);
        _activation.IsMainPartyDisorganized.Returns(true);

        Assert.AreEqual(CampButtonBlock.Moving, CampButtonBlocks.Pick(_activation));
    }

    // ---- Tooltip wording ----

    [TestMethod]
    public void Tooltip_NotBlocked_IsTheCaptionAlone()
    {
        Assert.AreEqual("Make Camp", CampButtonBlocks.Tooltip("Make Camp", CampButtonBlock.None));
    }

    [DataTestMethod]
    [DataRow(CampButtonBlock.InSettlement, "inside a settlement")]
    [DataRow(CampButtonBlock.InEncounter, "in an encounter")]
    [DataRow(CampButtonBlock.MenuOpen, "menu or window is open")]
    [DataRow(CampButtonBlock.Moving, "on the move")]
    [DataRow(CampButtonBlock.Disorganized, "still disorganized")]
    public void Tooltip_Blocked_KeepsTheCaptionAndNamesTheReasonOnASecondLine(CampButtonBlock block, string reasonWords)
    {
        var text = CampButtonBlocks.Tooltip("Make Camp", block);

        var lines = text.Split('\n');
        Assert.AreEqual(2, lines.Length, $"expected the caption plus one reason line, got: {text}");
        StringAssert.Contains(lines[0], "Make Camp");
        StringAssert.Contains(lines[1], "Cannot camp now");
        StringAssert.Contains(lines[1], reasonWords);
    }

    [TestMethod]
    public void Tooltip_ContributorCaption_KeepsTheContributorCaptionOnTheFirstLine()
    {
        var text = CampButtonBlocks.Tooltip("Refuge Options", CampButtonBlock.Moving);

        StringAssert.StartsWith(text, "Refuge Options");
    }

    // ---- Shared words: the tooltip and the new-campaign start message must not drift ----

    [DataTestMethod]
    [DataRow(CampBlockReason.InSettlement, "inside a settlement")]
    [DataRow(CampBlockReason.Moving, "on the move")]
    [DataRow(CampBlockReason.External, "something nearby forbids a camp here")]
    [DataRow(CampBlockReason.TerrainUnsuitable, "a camp cannot be pitched here")]
    public void StartBlocked_Reason_KeepsItsWords(CampBlockReason reason, string words)
    {
        StringAssert.Contains(CampRestStartTexts.StartBlocked(reason), words);
    }
}
