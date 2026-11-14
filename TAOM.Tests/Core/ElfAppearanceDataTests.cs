using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using static TAOM.Tests.Infrastructure.HeroFaceKeys;

namespace TAOM.Tests.Core;

/// <summary>
/// Elf hero looks gate over the repo's own ModuleData (lords.xml, named companions and the lords.xslt
/// rebuilds, which replace a lord's face). The elf race in LOTRLOME_Armory uses the full human skin colour
/// scale, white to near black, so the face key decides how an elf looks. Fields (value / 63): skin is bits
/// 48-53 and hair colour bits 30-35 of the key's first 64-bit part; height is bits 19-24 of the eighth part.
/// Elves are fair (skin 0-5, the range of the elf troop templates fighter_rivendell and fighter_mirkwood),
/// Galadhrim are golden or fair haired (hair colour at most 22, light brown), and no elf is shorter than an
/// average man (height at least 32). Chaegermeister (lord_R3_1) is a Patreon tribute hero kept as authored.
/// </summary>
[TestClass]
public class ElfAppearanceDataTests
{
    private const int MaxFairSkin = 5;
    private const int MaxGaladhrimHair = 22;
    private const int MinElfHeight = 32;

    private static readonly HashSet<string> Exceptions = new(StringComparer.Ordinal) { "lord_R3_1" };

    private static List<HeroFace> Elves()
    {
        var elves = HeroFaces().Where(h => h.Race == "elf").ToList();
        Assert.IsTrue(elves.Count > 50, $"Expected the elf heroes to load, found {elves.Count}.");
        return elves;
    }

    private static void AssertNone(IEnumerable<string> bad, string what)
    {
        var list = bad.ToList();
        Assert.AreEqual(0, list.Count, what + ": " + string.Join(", ", list));
    }

    [TestMethod]
    public void Field_ReadsTheKeyBits()
    {
        var key = "0028280FD7FC23C2" + new string('0', 96) + "0000000000780000";
        Assert.AreEqual(40, Skin(key));
        Assert.AreEqual(63, Hair("0000000FC0000000" + new string('0', 112)));
        Assert.AreEqual(15, Height(key));
    }

    [TestMethod]
    public void ElfHeroes_SkinStaysFair() =>
        AssertNone(Elves().Where(h => !Exceptions.Contains(h.Id) && h.Keys.Any(k => Skin(k) > MaxFairSkin))
            .Select(h => $"{h.Id} ({h.File}) skin {h.Values(Skin)}/63"), "Elf heroes outside the fair skin range 0-" + MaxFairSkin);

    [TestMethod]
    public void GaladhrimHeroes_HairStaysGoldenOrFair() =>
        AssertNone(Elves().Where(h => h.Culture == "lothlorien" && h.Keys.Any(k => Hair(k) > MaxGaladhrimHair))
            .Select(h => $"{h.Id} ({h.File}) hair {h.Values(Hair)}/63"), "Galadhrim heroes with hair darker than " + MaxGaladhrimHair);

    [TestMethod]
    public void ElfHeroes_AreNotShorterThanAnAverageMan() =>
        AssertNone(Elves().Where(h => h.Keys.Any(k => Height(k) < MinElfHeight))
            .Select(h => $"{h.Id} ({h.File}) height {h.Values(Height)}/63"), "Elf heroes below height " + MinElfHeight);

    [TestMethod]
    public void Exceptions_StillExistAsElfHeroes()
    {
        var ids = new HashSet<string>(Elves().Select(h => h.Id), StringComparer.Ordinal);
        foreach (var id in Exceptions)
            Assert.IsTrue(ids.Contains(id), $"Exception {id} is no longer an elf hero; remove it from the list.");
    }
}
