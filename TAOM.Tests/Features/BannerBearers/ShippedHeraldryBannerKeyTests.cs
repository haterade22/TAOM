using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.BannerBearers;

/// <summary>
/// Pins the heraldry every banner relies on. On v1.4.8 the engine itself banners a formation captained
/// by a lord who carries a banner item, with no heraldry check (GeneralsAndCaptainsAssignmentLogic.cs:262-264),
/// and the banner items a lord can be given are those of his own culture or of none
/// (DefaultBannerItemModel.cs:31). The native banner tableau access-violates when a bearer's banner parses
/// to zero groups of ten integers (an empty banner data list,
/// docs/reviews/rca-banner-bearers-siege-ctd-2026-07-23.md), so every shipped <c>banner_key</c> must be a
/// non-zero whole multiple of ten dot-separated integers. The engine (Banner.TryGetBannerDataFromCode,
/// Banner.cs:576-593) reads whole groups of ten and ignores a trailing partial group, so this gate is a
/// stricter form of its rule, the safe direction.
/// </summary>
[TestClass]
public class ShippedHeraldryBannerKeyTests
{
    private static readonly Regex Attribute = new Regex(@"(?<![A-Za-z_])banner_key=""([^""]*)""", RegexOptions.Compiled);

    private static readonly Regex InlineXslt =
        new Regex(@"<xsl:attribute\s+name=""banner_key""[^>]*>([^<]*)</xsl:attribute>", RegexOptions.Compiled);

    private static string[] Keys(string relativePath, params Regex[] patterns)
    {
        var parts = new[] { CultureDataFixture.ModuleDataPath() }.Concat(relativePath.Split('/')).ToArray();
        var text = File.ReadAllText(Path.Combine(parts));
        return patterns.SelectMany(p => p.Matches(text).Cast<Match>()).Select(m => m.Groups[1].Value.Trim()).ToArray();
    }

    private static bool IsRenderable(string key)
    {
        var parts = key.Split('.');
        return parts.Length % 10 == 0 && parts.All(p => int.TryParse(p, out _));
    }

    private static void AssertAllRenderable(string relativePath, params Regex[] patterns)
    {
        var keys = Keys(relativePath, patterns);
        Assert.IsTrue(keys.Length > 0, relativePath + ": the scan found no banner_key, so this gate checks nothing");

        var bad = keys.Where(k => !IsRenderable(k)).Select(k => k.Length > 60 ? k.Substring(0, 60) : k).ToList();

        Assert.AreEqual(0, bad.Count,
            relativePath + ": a banner_key that is not a non-zero whole multiple of ten integers gives an empty banner "
            + "data list, the precondition of the native banner-tableau crash on the engine's own captain-banner "
            + "path: " + string.Join(" | ", bad));
    }

    [TestMethod]
    public void Clans_EveryBannerKeyIsRenderable() => AssertAllRenderable("characters/clans.xml", Attribute);

    [TestMethod]
    public void Kingdoms_EveryBannerKeyIsRenderable() => AssertAllRenderable("taom_spkingdoms.xml", Attribute);

    [TestMethod]
    public void ClanTransform_EveryBannerKeyIsRenderable() => AssertAllRenderable("spclans.xslt", Attribute, InlineXslt);

    [TestMethod]
    public void KingdomTransform_EveryBannerKeyIsRenderable() => AssertAllRenderable("spkingdoms.xslt", Attribute, InlineXslt);

    [DataTestMethod]
    [DataRow("11.251.166.1528.1528.764.764.1.0.0.10016.1.1.1.1.1.1.1.1.1", true)]
    [DataRow("11.251.166.1528.1528.764.764.1.0.0", true)]
    [DataRow("", false)]
    [DataRow("11.251.166", false)]
    [DataRow("11.251.166.1528.1528.764.764.1.0.x", false)]
    public void TheRule_AcceptsWholeGroupsOfTenIntegersOnly(string key, bool expected)
        => Assert.AreEqual(expected, IsRenderable(key));
}
