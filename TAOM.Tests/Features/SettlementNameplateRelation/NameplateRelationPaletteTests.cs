using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Features.SettlementNameplateRelation;

namespace TAOM.Tests.Features.SettlementNameplateRelation;

[TestClass]
public class NameplateRelationPaletteTests
{
    private static void AssertColor(string expectedHex, Color actual, string what)
    {
        var expected = Color.ConvertStringToColor(expectedHex);
        Assert.AreEqual(expected.Red, actual.Red, 0.0001f, what + " red");
        Assert.AreEqual(expected.Green, actual.Green, 0.0001f, what + " green");
        Assert.AreEqual(expected.Blue, actual.Blue, 0.0001f, what + " blue");
        Assert.AreEqual(expected.Alpha, actual.Alpha, 0.0001f, what + " alpha");
    }

    private static void AssertEntry(NameplatePaletteEntry expected, NameplatePaletteEntry actual)
    {
        Assert.AreEqual(expected.Bar.ToUnsignedInteger(), actual.Bar.ToUnsignedInteger(), "bar");
        Assert.AreEqual(expected.Text.ToUnsignedInteger(), actual.Text.ToUnsignedInteger(), "text");
        Assert.AreEqual(expected.Frame.ToUnsignedInteger(), actual.Frame.ToUnsignedInteger(), "frame");
    }

    [TestMethod]
    public void Resolve_Neutral_ReturnsIdentityWhiteBarBlackText()
    {
        var entry = NameplateRelationPalette.Resolve(NameplateRelationPalette.Neutral);

        AssertColor("#FFFFFFFF", entry.Bar, "neutral bar");
        AssertColor("#000000FF", entry.Text, "neutral text");
        AssertColor("#FFFFFFFF", entry.Frame, "neutral frame");
    }

    [TestMethod]
    public void Resolve_SameFaction_ReturnsOwnEntry()
        => AssertEntry(NameplateRelationPalette.DefaultSameFaction,
            NameplateRelationPalette.Resolve(NameplateRelationPalette.SameFaction));

    [TestMethod]
    public void Resolve_Enemy_ReturnsEnemyEntry()
        => AssertEntry(NameplateRelationPalette.DefaultEnemy,
            NameplateRelationPalette.Resolve(NameplateRelationPalette.Enemy));

    [TestMethod]
    public void Resolve_Ally_ReturnsAllyEntry()
        => AssertEntry(NameplateRelationPalette.DefaultAlly,
            NameplateRelationPalette.Resolve(NameplateRelationPalette.Ally));

    [TestMethod]
    public void Resolve_UnknownPositiveInt_ReturnsNeutral()
    {
        AssertEntry(NameplateRelationPalette.DefaultNeutral, NameplateRelationPalette.Resolve(4));
        AssertEntry(NameplateRelationPalette.DefaultNeutral, NameplateRelationPalette.Resolve(99));
        AssertEntry(NameplateRelationPalette.DefaultNeutral, NameplateRelationPalette.Resolve(int.MaxValue));
    }

    [TestMethod]
    public void Resolve_NegativeInt_ReturnsNeutral()
    {
        AssertEntry(NameplateRelationPalette.DefaultNeutral, NameplateRelationPalette.Resolve(-1));
        AssertEntry(NameplateRelationPalette.DefaultNeutral, NameplateRelationPalette.Resolve(int.MinValue));
    }

    [TestMethod]
    public void Select_CustomEntries_ReturnsMatchingEntry()
    {
        var neutral = new NameplatePaletteEntry(Color.White, Color.Black, Color.White);
        var own = new NameplatePaletteEntry(new Color(0f, 1f, 0f), new Color(0f, 0.5f, 0f), new Color(0.5f, 1f, 0.5f));
        var enemy = new NameplatePaletteEntry(new Color(1f, 0f, 0f), new Color(0.5f, 0f, 0f), new Color(1f, 0.5f, 0.5f));
        var ally = new NameplatePaletteEntry(new Color(0f, 0f, 1f), new Color(0f, 0f, 0.5f), new Color(0.5f, 0.5f, 1f));

        AssertEntry(neutral, NameplateRelationPalette.Select(0, neutral, own, enemy, ally));
        AssertEntry(own, NameplateRelationPalette.Select(1, neutral, own, enemy, ally));
        AssertEntry(enemy, NameplateRelationPalette.Select(2, neutral, own, enemy, ally));
        AssertEntry(ally, NameplateRelationPalette.Select(3, neutral, own, enemy, ally));
        AssertEntry(neutral, NameplateRelationPalette.Select(7, neutral, own, enemy, ally));
        AssertEntry(neutral, NameplateRelationPalette.Select(-1, neutral, own, enemy, ally));
    }

    [TestMethod]
    public void DefaultColorStrings_AllMatchRrggbbaa()
    {
        // Color.ConvertStringToColor reads Substring(7, 2): a 7-character #RRGGBB throws inside
        // the attribute loader, which catches it per attribute (Debug.FailedAssert) and leaves the
        // compiled default in place, so a malformed override is silently ignored.
        var pattern = new Regex("^#[0-9A-Fa-f]{8}$");
        var strings = NameplateRelationPalette.DefaultColorStrings;

        Assert.AreEqual(12, strings.Length, "four relations x bar/text/frame");
        foreach (var hex in strings)
            Assert.IsTrue(pattern.IsMatch(hex), hex + " is not #RRGGBBAA");
    }

    [TestMethod]
    public void NeutralBar_IsWhite_NotVanillaBlack()
    {
        // Vanilla tints its white plate sprite black for neutral. Widget.Color multiplies the
        // sprite, so black on TAOM's parchment is a black bar under black text.
        var neutral = NameplateRelationPalette.DefaultNeutral;
        Assert.AreEqual(1f, neutral.Bar.Red, 0.0001f);
        Assert.AreEqual(1f, neutral.Bar.Green, 0.0001f);
        Assert.AreEqual(1f, neutral.Bar.Blue, 0.0001f);
        Assert.AreEqual(1f, neutral.Bar.Alpha, 0.0001f);
    }
}
