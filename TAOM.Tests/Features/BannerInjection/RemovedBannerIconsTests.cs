using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BannerInjection;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BannerInjection;

/// <summary>
/// #1285: a banner in a save can name an icon the v2.0.40 repack removed, and the engine then draws nothing for that
/// layer. On load the authority swaps each removed id for the surviving icon of its old group.
/// </summary>
[TestClass]
public class RemovedBannerIconsTests
{
    // background layer (pattern 11), then two icon layers; the second has a negative rotation
    private const string Code = "11.163.4.1528.1528.764.764.1.0.0.10002.116.116.483.483.764.764.0.0.0.10003.40.40.200.200.600.600.1.1.-90";

    [TestMethod]
    public void Table_EveryKeyIsGone_AndEveryReplacementIsALoadedIcon()
    {
        var doc = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "banner_icons.xml"));
        var icons = doc.Descendants("BannerIconGroup").Where(g => (string?)g.Attribute("is_pattern") != "true")
            .SelectMany(g => g.Elements("Icon")).Select(i => (int)i.Attribute("id")!).ToHashSet();

        var back = RemovedBannerIcons.Replacement.Keys.Where(icons.Contains).ToList();
        var gone = RemovedBannerIcons.Replacement.Values.Distinct().Where(v => !icons.Contains(v)).ToList();

        Assert.AreEqual(0, back.Count, "a removed id is in banner_icons.xml again; drop it from RemovedBannerIcons: " + string.Join(",", back));
        Assert.AreEqual(0, gone.Count, "a replacement is no longer an icon; pick another: " + string.Join(",", gone));
        Assert.AreEqual(159, RemovedBannerIcons.Replacement.Count);
    }

    [TestMethod]
    public void Remap_ARemovedIcon_IsReplaced_AndEverythingElseKept()
    {
        var map = new Dictionary<int, int> { [10002] = 10001 };

        Assert.AreEqual(
            "11.163.4.1528.1528.764.764.1.0.0.10001.116.116.483.483.764.764.0.0.0.10003.40.40.200.200.600.600.1.1.-90",
            BannerIconRemap.Remap(Code, map));
    }

    [TestMethod]
    public void Remap_OnlyTheMeshIdOfALayer_IsRead()
    {
        // 10002 also sits in a colour, size and position field: only a layer's first number is an icon
        var map = new Dictionary<int, int> { [764] = 1, [116] = 2, [40] = 3 };

        Assert.IsNull(BannerIconRemap.Remap(Code, map));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    [DataRow("11.163.4")]
    [DataRow("x.163.4.1528.1528.764.764.1.0.0")]
    public void Remap_NoChangeOrNotABannerCode_ReturnsNull(string? code) =>
        Assert.IsNull(BannerIconRemap.Remap(code, new Dictionary<int, int> { [11] = 12 }));

    [TestMethod]
    public void RemapRemovedIcons_SetsOnlyTheBannersThatNameARemovedIcon()
    {
        var kingdoms = Substitute.For<IKingdomBannerAdapter>();
        var clans = Substitute.For<IClanBannerAdapter>();
        var config = Substitute.For<IBannerConfigProvider>();
        kingdoms.GetAllKingdoms().Returns(new List<KingdomBannerInfo>
        {
            new("gondor", "11.163.4.1528.1528.764.764.1.0.0.10000.116.116.483.483.764.764.0.0.0"),
            new("player_realm", Code),
        });
        clans.GetAllClans().Returns(new List<ClanBannerInfo> { new("player_faction", Code, isRulingClan: true) });
        var sut = new BannerInjectionService(kingdoms, clans, Substitute.For<IBannerExclusionService>(), config,
            Substitute.For<IModLogger>());

        var changed = sut.RemapRemovedIcons();

        Assert.AreEqual(2, changed);
        var fixedCode = BannerIconRemap.Remap(Code, RemovedBannerIcons.Replacement);
        kingdoms.Received(1).SetBanner("player_realm", fixedCode!);
        kingdoms.DidNotReceive().SetBanner("gondor", Arg.Any<string>());
        clans.Received(1).SetBanner("player_faction", fixedCode!);
        clans.Received(1).InvalidateVisuals("player_faction");
    }
}
