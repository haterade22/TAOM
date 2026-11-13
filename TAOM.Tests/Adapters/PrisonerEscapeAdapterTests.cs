using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>
/// The prisoner-escape adapter outside a campaign: it must answer empty or false, never throw from a
/// computed engine getter (Hero.MainHero and Clan.PlayerClan both dereference Campaign.Current).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PrisonerEscapeAdapterTests
{
    private readonly PrisonerEscapeAdapter _sut = new PrisonerEscapeAdapter(NSubstitute.Substitute.For<TAOM.Features.CoopInterop.IPlayerControlQuery>());

    [TestMethod]
    public void GetCapturedLords_NoCampaign_ReturnsEmpty()
    {
        Assert.AreEqual(0, _sut.GetCapturedLords(new List<string> { "empire_w" }).Count);
    }

    [TestMethod]
    public void GetCapturedLords_NullKingdomIds_ReturnsEmpty()
    {
        Assert.AreEqual(0, _sut.GetCapturedLords(null!).Count);
    }

    [TestMethod]
    public void Escape_NoCampaign_ReturnsFalse()
    {
        Assert.IsFalse(_sut.Escape("lord_1"));
    }

    [TestMethod]
    public void Escape_EmptyId_ReturnsFalse()
    {
        Assert.IsFalse(_sut.Escape(string.Empty));
    }

    [TestMethod]
    public void ReadDungeon_NoCampaign_ReturnsNull()
    {
        Assert.IsNull(_sut.ReadDungeon("town_ES1"));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void ReadDungeon_EmptyId_ReturnsNull(string? id)
    {
        Assert.IsNull(_sut.ReadDungeon(id!));
    }

    // The factor and the lines come from one ExplainedNumber (#903). The base-1 number shows the engine's own shape:
    // a "Base" line first, then one line per described AddFactor, in the order they were added.
    [TestMethod]
    public void TermLines_DescribedFactors_AreTheLinesAfterTheBaseLine_AsFractionsOfTheBase()
    {
        var number = new ExplainedNumber(1f, true);
        number.AddFactor(0.25f, new TextObject("{=!}Roguery perk"));
        number.AddFactor(-0.4f, new TextObject("{=!}Engineering perk"));

        var lines = PrisonerEscapeAdapter.TermLines(number);

        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual("Roguery perk", lines[0].Description);
        Assert.AreEqual(0.25f, lines[0].Change, 1e-4f);
        Assert.AreEqual("Engineering perk", lines[1].Description);
        Assert.AreEqual(-0.4f, lines[1].Change, 1e-4f);
        Assert.AreEqual(1f + 0.25f - 0.4f, number.ResultNumber, 1e-4f, "the lines sum, with the base, to the factor");
    }

    [TestMethod]
    public void TermLines_NoFactors_IsEmpty()
    {
        Assert.AreEqual(0, PrisonerEscapeAdapter.TermLines(new ExplainedNumber(1f, true)).Count);
    }

    [TestMethod]
    public void TermLines_NumberWithoutDescriptions_IsEmpty()
    {
        var number = new ExplainedNumber(1f);
        number.AddFactor(0.25f, new TextObject("{=!}Roguery perk"));

        Assert.AreEqual(0, PrisonerEscapeAdapter.TermLines(number).Count);
    }
}
