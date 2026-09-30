using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>Realm names are lettered in aniron only when aniron can draw every letter.</summary>
[TestClass]
public class RealmNameLetteringTests
{
    [TestMethod]
    public void Letter_LatinName_IsSpacedCapitalsInAniron()
    {
        Assert.AreEqual(("G O N D O R", true), RealmNameLettering.Letter("Gondor"));
    }

    [TestMethod]
    public void Letter_TwoWords_KeepAWiderGapBetweenThem()
    {
        Assert.AreEqual(("D O L   G U L D U R", true), RealmNameLettering.Letter("Dol Guldur"));
    }

    [TestMethod]
    public void Letter_RussianName_UsesAnironsCyrillic()
    {
        Assert.IsTrue(RealmNameLettering.Letter("Гондор").Aniron);
    }

    [TestMethod]
    public void Letter_PolishLetterAnironLacks_FallsBackToThePlainFont()
    {
        Assert.AreEqual(("Łotrówka", false), RealmNameLettering.Letter("Łotrówka"));
    }

    [TestMethod]
    public void Letter_ChineseName_FallsBackToThePlainFont()
    {
        Assert.AreEqual(("刚铎", false), RealmNameLettering.Letter("刚铎"));
    }

    [TestMethod]
    public void Letter_Empty_IsNothing()
    {
        Assert.AreEqual((string.Empty, false), RealmNameLettering.Letter("  "));
    }

    [TestMethod]
    public void AnironCovers_MatchesTheGlyphsTheShippedFontHas()
    {
        string fnt = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "GUI", "Fonts", "aniron.fnt"));
        var glyphs = new HashSet<int>(XDocument.Load(fnt).Descendants("char")
            .Select(c => (int)c.Attribute("id")!).Where(id => id != 0)); // id 0 is the missing-glyph box

        var drift = Enumerable.Range(1, 0xFFFF)
            .Where(cp => cp < 0xD800 || cp > 0xDFFF)
            .Where(cp => RealmNameLettering.AnironCovers(((char)cp).ToString()) != glyphs.Contains(cp))
            .Take(10)
            .Select(cp => $"U+{cp:X4}")
            .ToList();

        Assert.AreEqual(0, drift.Count, "the lettering's glyph table and aniron.fnt disagree at " + string.Join(", ", drift));
    }
}
