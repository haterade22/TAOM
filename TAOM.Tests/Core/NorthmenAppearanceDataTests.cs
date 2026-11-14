using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using static TAOM.Tests.Infrastructure.HeroFaceKeys;

namespace TAOM.Tests.Core;

/// <summary>
/// Rohirrim and Northmen hero looks gate over the repo's own ModuleData (lords.xml, named companions and the
/// lords.xslt rebuilds, which replace a lord's face). The Rohirrim, Bardings, Beornings, Woodmen, Northmen of
/// Rhovanion and the Men of Dorwinion are fair; the Rohirrim are mostly blond. Skin is bits 48-53 and hair
/// colour bits 30-35 of the face key's first 64-bit part (value / 63; hair 0 is platinum blond, 63 black).
/// Skin stays at 9 or below, and a Rohirrim hero's hair at 31 (mid brown) or below. The fighter_rohan template is stricter (22, in
/// AppearanceTemplateDataTests); lord_4_22, lord_4_27_1 and lord_4_3_2 are authored at 27 and 28, so this
/// bar cannot drop to 22 without recolouring them. Every BodyProperties and BodyPropertiesMax key counts.
/// </summary>
[TestClass]
public class NorthmenAppearanceDataTests
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
    private const int MaxRohirrimHair = 31;
    private static readonly HashSet<string> Northmen = new(StringComparer.Ordinal)
        { "vlandia", "sturgia", "anduinvale", "angaladh", "southrhovanion", "dorwinion" };

    [TestMethod]
    public void NorthmenHeroes_SkinStaysFair() =>
        AssertNone(Of(h => h.Race == "human" && Northmen.Contains(h.Culture), 80).Where(h => h.Keys.Any(k => Skin(k) > MaxFairSkin))
            .Select(h => $"{h.Id} ({h.File}) skin {h.Values(Skin)}/63"), "Northmen heroes above skin " + MaxFairSkin);

    [TestMethod]
    public void RohirrimHeroes_HairStaysFairOrBrown() =>
        AssertNone(Of(h => h.Race == "human" && h.Culture == "vlandia", 40).Where(h => h.Keys.Any(k => Hair(k) > MaxRohirrimHair))
            .Select(h => $"{h.Id} ({h.File}) hair {h.Values(Hair)}/63"), "Rohirrim heroes with hair darker than " + MaxRohirrimHair);
}
