using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BannerInjection;
using TAOM.Features.CustomBattles.Muster;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// Every Custom Battle faction starts with its kingdom's full banner, read from the shipped kingdom data
/// (taom_spkingdoms.xml and spkingdoms.xslt). Gondor and Mordor rule the reused kingdoms empire_w and empire_s.
/// </summary>
[TestClass]
public class MusterKingdomBannerShippedDataTests
{
    private static BannerConfigProvider Provider()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(TAOM.Tests.Core.CultureDataFixture.ModuleDataPath());
        return new BannerConfigProvider(pathService, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void CultureKingdomBanners_ShippedData_CoverThirtyCulturesWithWholeBanners()
    {
        var map = Provider().GetCultureKingdomBannerKeys();

        Assert.AreEqual(30, map.Count, string.Join(", ", map.Keys.OrderBy(k => k)));
        foreach (var pair in map)
            Assert.IsNotNull(MusterBannerDesign.KingdomCode(map, pair.Key), $"{pair.Key}: '{pair.Value}' is not a whole banner code");
    }

    [TestMethod]
    public void CultureKingdomBanners_ShippedData_GondorAndMordorUseTheirReusedKingdoms()
    {
        var provider = Provider();
        var kingdoms = provider.GetKingdomBannerKeys();
        var map = provider.GetCultureKingdomBannerKeys();

        Assert.AreEqual(kingdoms["empire_w"], map["gondor"]);
        Assert.AreEqual(kingdoms["empire_s"], map["mordor"]);
        foreach (var culture in map.Keys.Where(c => c != "gondor" && c != "mordor"))
            Assert.AreEqual(kingdoms[culture], map[culture], $"{culture} rules the kingdom of its own id");
    }

    [TestMethod]
    public void CultureKingdomBanners_ShippedData_EveryMainCultureHasOne()
    {
        // the Custom Battle factions are the main cultures; one without a kingdom would fall back to its culture banner
        var map = Provider().GetCultureKingdomBannerKeys();

        var missing = TAOM.Tests.Core.CultureDataFixture.MainCultureIds().Where(c => !map.ContainsKey(c)).OrderBy(c => c).ToList();

        Assert.AreEqual(0, missing.Count, "no kingdom banner: " + string.Join(", ", missing));
    }
}
