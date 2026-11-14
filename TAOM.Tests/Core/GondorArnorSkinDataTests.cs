using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using static TAOM.Tests.Infrastructure.HeroFaceKeys;

namespace TAOM.Tests.Core;

/// <summary>
/// Gondor and Arnor hero looks gate over the repo's own ModuleData (lords.xml, named companions and the
/// lords.xslt rebuilds, which replace a lord's face). Men of Gondor and the Dunedain of Arthedain, Cardolan and
/// Rhudaur are fair-skinned in the books and films. Skin is bits 48-53 of the face key's first 64-bit part
/// (value / 63; vanilla Sturgian and Vlandian lords average about 6, Aserai 37). A Gondor or Arthedain hero stays at
/// 9 or below, in every BodyProperties and BodyPropertiesMax key. Cardolan and Rhudaur lords hold no key of their
/// own (they use the fighter_gondor template), so AppearanceTemplateDataTests.GondorTemplate_SkinStaysFair gates them.
/// </summary>
[TestClass]
public class GondorArnorSkinDataTests
{
    private static void AssertNone(IEnumerable<string> bad, string what)
    {
        var list = bad.ToList();
        Assert.AreEqual(0, list.Count, what + ": " + string.Join(", ", list));
    }

    private static List<HeroFace> Of(Func<HeroFace, bool> who, int atLeast)
    {
        var heroes = HeroFaces().Where(who).ToList();
        Assert.IsTrue(heroes.Count >= atLeast, $"Expected at least {atLeast} heroes to load, found {heroes.Count}.");
        return heroes;
    }

    private const int MaxFairSkin = 9;

    [TestMethod]
    public void GondorHeroes_SkinStaysFair() => AssertFair("gondor", 80);

    [TestMethod]
    public void ArthedainHeroes_SkinStaysFair() => AssertFair("arthedain", 15);

    private static void AssertFair(string culture, int atLeast) =>
        AssertNone(Of(h => h.Race == "human" && h.Culture == culture, atLeast).Where(h => h.Keys.Any(k => Skin(k) > MaxFairSkin))
            .Select(h => $"{h.Id} ({h.File}) skin {h.Values(Skin)}/63"), culture + " heroes above skin " + MaxFairSkin);
}
