using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using static TAOM.Tests.Infrastructure.HeroFaceKeys;

namespace TAOM.Tests.Core;

/// <summary>
/// Variag and dwarf hero looks gate over the repo's own ModuleData (lords.xml, named companions and the
/// lords.xslt rebuilds, which replace a lord's face). The Variags of Khand (culture battania) are swarthy,
/// dark-haired men of the south-east; dwarves are fair to ruddy. Skin is bits 48-53 and hair colour bits 30-35
/// of the face key's first 64-bit part (value / 63; vanilla Aserai lords average about 37 skin). A Variag hero's
/// skin stays at 28 or above and hair at 44 or above. Dwarf women use the full human skin scale (dwarf men have
/// their own pale-to-ruddy one), so a dwarf woman's skin stays at 9 or below.
/// </summary>
[TestClass]
public class VariagDwarfAppearanceDataTests
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

    private const int MinVariagSkin = 28;
    private const int MinVariagHair = 44;
    private const int MaxDwarfWomanSkin = 9;

    [TestMethod]
    public void VariagHeroes_AreSwarthyAndDarkHaired() =>
        AssertNone(Of(h => h.Race == "human" && h.Culture == "battania", 15)
            .Where(h => h.Keys.Any(k => Skin(k) < MinVariagSkin || Hair(k) < MinVariagHair))
            .Select(h => $"{h.Id} ({h.File}) skin {h.Values(Skin)}/63 hair {h.Values(Hair)}/63"), "Variag heroes outside skin >= " + MinVariagSkin + ", hair >= " + MinVariagHair);

    [TestMethod]
    public void DwarfWomen_SkinStaysFair() =>
        AssertNone(Of(h => h.Race == "dwarf" && h.IsFemale, 5).Where(h => h.Keys.Any(k => Skin(k) > MaxDwarfWomanSkin))
            .Select(h => $"{h.Id} ({h.File}) skin {h.Values(Skin)}/63"), "Dwarf women above skin " + MaxDwarfWomanSkin);
}
