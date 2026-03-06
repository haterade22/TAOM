using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BannerInjection;

namespace TAOM.Tests.Features.BannerInjection;

[TestClass]
public class BannerInjectionServiceTests
{
    private BannerInjectionService _sut;
    private IKingdomBannerAdapter _kingdomAdapter;
    private IClanBannerAdapter _clanAdapter;
    private IBannerExclusionService _exclusionService;
    private IBannerConfigProvider _configProvider;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _kingdomAdapter = Substitute.For<IKingdomBannerAdapter>();
        _clanAdapter = Substitute.For<IClanBannerAdapter>();
        _exclusionService = Substitute.For<IBannerExclusionService>();
        _configProvider = Substitute.For<IBannerConfigProvider>();
        _logger = Substitute.For<IModLogger>();

        _kingdomAdapter.GetAllKingdoms().Returns(new List<KingdomBannerInfo>());
        _clanAdapter.GetAllClans().Returns(new List<ClanBannerInfo>());
        _configProvider.GetKingdomBannerKeys().Returns(new Dictionary<string, string>());
        _configProvider.GetClanBannerKeys().Returns(new Dictionary<string, string>());

        _sut = new BannerInjectionService(
            _kingdomAdapter, _clanAdapter, _exclusionService, _configProvider, _logger);
    }

    [TestMethod]
    public void InjectBanners_KingdomMismatch_SetsCorrectBanner()
    {
        _configProvider.GetKingdomBannerKeys().Returns(new Dictionary<string, string>
        {
            { "kingdom_erebor", "11.4.4.4345.4345.764.764.1.0.0" }
        });
        _kingdomAdapter.GetAllKingdoms().Returns(new List<KingdomBannerInfo>
        {
            new KingdomBannerInfo("kingdom_erebor", "0.0.0.0.0.0.0.0.0.0")
        });

        _sut.InjectBanners();

        _kingdomAdapter.Received(1).SetBanner("kingdom_erebor", "11.4.4.4345.4345.764.764.1.0.0");
    }

    [TestMethod]
    public void InjectBanners_KingdomAlreadyCorrect_Skips()
    {
        var bannerKey = "11.4.4.4345.4345.764.764.1.0.0";
        _configProvider.GetKingdomBannerKeys().Returns(new Dictionary<string, string>
        {
            { "kingdom_erebor", bannerKey }
        });
        _kingdomAdapter.GetAllKingdoms().Returns(new List<KingdomBannerInfo>
        {
            new KingdomBannerInfo("kingdom_erebor", bannerKey)
        });

        _sut.InjectBanners();

        _kingdomAdapter.DidNotReceive().SetBanner(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void InjectBanners_KingdomNotInConfig_Skips()
    {
        _configProvider.GetKingdomBannerKeys().Returns(new Dictionary<string, string>());
        _kingdomAdapter.GetAllKingdoms().Returns(new List<KingdomBannerInfo>
        {
            new KingdomBannerInfo("kingdom_unknown", "0.0.0.0.0.0.0.0.0.0")
        });

        _sut.InjectBanners();

        _kingdomAdapter.DidNotReceive().SetBanner(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void InjectBanners_KingdomPlayerModified_Skips()
    {
        _configProvider.GetKingdomBannerKeys().Returns(new Dictionary<string, string>
        {
            { "kingdom_erebor", "11.4.4.4345.4345.764.764.1.0.0" }
        });
        _kingdomAdapter.GetAllKingdoms().Returns(new List<KingdomBannerInfo>
        {
            new KingdomBannerInfo("kingdom_erebor", "0.0.0.0.0.0.0.0.0.0")
        });
        _exclusionService.IsPlayerModified("kingdom_erebor").Returns(true);

        _sut.InjectBanners();

        _kingdomAdapter.DidNotReceive().SetBanner(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void InjectBanners_ClanMismatch_SetsCorrectBanner()
    {
        _configProvider.GetClanBannerKeys().Returns(new Dictionary<string, string>
        {
            { "clan_gondor_1", "11.92.92.3000.3000.764.764.1.0.0" }
        });
        _clanAdapter.GetAllClans().Returns(new List<ClanBannerInfo>
        {
            new ClanBannerInfo("clan_gondor_1", "0.0.0.0.0.0.0.0.0.0", false)
        });

        _sut.InjectBanners();

        _clanAdapter.Received(1).SetBanner("clan_gondor_1", "11.92.92.3000.3000.764.764.1.0.0");
    }

    [TestMethod]
    public void InjectBanners_ClanAlreadyCorrect_Skips()
    {
        var bannerKey = "11.92.92.3000.3000.764.764.1.0.0";
        _configProvider.GetClanBannerKeys().Returns(new Dictionary<string, string>
        {
            { "clan_gondor_1", bannerKey }
        });
        _clanAdapter.GetAllClans().Returns(new List<ClanBannerInfo>
        {
            new ClanBannerInfo("clan_gondor_1", bannerKey, false)
        });

        _sut.InjectBanners();

        _clanAdapter.DidNotReceive().SetBanner(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void InjectBanners_ClanPlayerModified_Skips()
    {
        _configProvider.GetClanBannerKeys().Returns(new Dictionary<string, string>
        {
            { "clan_gondor_1", "11.92.92.3000.3000.764.764.1.0.0" }
        });
        _clanAdapter.GetAllClans().Returns(new List<ClanBannerInfo>
        {
            new ClanBannerInfo("clan_gondor_1", "0.0.0.0.0.0.0.0.0.0", false)
        });
        _exclusionService.IsPlayerModified("clan_gondor_1").Returns(true);

        _sut.InjectBanners();

        _clanAdapter.DidNotReceive().SetBanner(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void InjectBanners_RulingClan_Skips()
    {
        _configProvider.GetClanBannerKeys().Returns(new Dictionary<string, string>
        {
            { "clan_ruling", "11.92.92.3000.3000.764.764.1.0.0" }
        });
        _clanAdapter.GetAllClans().Returns(new List<ClanBannerInfo>
        {
            new ClanBannerInfo("clan_ruling", "0.0.0.0.0.0.0.0.0.0", true)
        });

        _sut.InjectBanners();

        _clanAdapter.DidNotReceive().SetBanner(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void InjectBanners_CallsInvalidateVisuals_ForUpdatedKingdom()
    {
        _configProvider.GetKingdomBannerKeys().Returns(new Dictionary<string, string>
        {
            { "kingdom_erebor", "11.4.4.4345.4345.764.764.1.0.0" }
        });
        _kingdomAdapter.GetAllKingdoms().Returns(new List<KingdomBannerInfo>
        {
            new KingdomBannerInfo("kingdom_erebor", "0.0.0.0.0.0.0.0.0.0")
        });

        _sut.InjectBanners();

        _kingdomAdapter.Received(1).InvalidateVisuals("kingdom_erebor");
    }

    [TestMethod]
    public void InjectBanners_CallsInvalidateVisuals_ForUpdatedClan()
    {
        _configProvider.GetClanBannerKeys().Returns(new Dictionary<string, string>
        {
            { "clan_gondor_1", "11.92.92.3000.3000.764.764.1.0.0" }
        });
        _clanAdapter.GetAllClans().Returns(new List<ClanBannerInfo>
        {
            new ClanBannerInfo("clan_gondor_1", "0.0.0.0.0.0.0.0.0.0", false)
        });

        _sut.InjectBanners();

        _clanAdapter.Received(1).InvalidateVisuals("clan_gondor_1");
    }

    [TestMethod]
    public void InjectBanners_DoesNotInvalidateVisuals_ForSkippedKingdom()
    {
        var bannerKey = "11.4.4.4345.4345.764.764.1.0.0";
        _configProvider.GetKingdomBannerKeys().Returns(new Dictionary<string, string>
        {
            { "kingdom_erebor", bannerKey }
        });
        _kingdomAdapter.GetAllKingdoms().Returns(new List<KingdomBannerInfo>
        {
            new KingdomBannerInfo("kingdom_erebor", bannerKey)
        });

        _sut.InjectBanners();

        _kingdomAdapter.DidNotReceive().InvalidateVisuals(Arg.Any<string>());
    }

    [TestMethod]
    public void InjectBanners_LogsSummary()
    {
        _configProvider.GetKingdomBannerKeys().Returns(new Dictionary<string, string>
        {
            { "kingdom_erebor", "11.4.4.4345.4345.764.764.1.0.0" }
        });
        _kingdomAdapter.GetAllKingdoms().Returns(new List<KingdomBannerInfo>
        {
            new KingdomBannerInfo("kingdom_erebor", "0.0.0.0.0.0.0.0.0.0")
        });

        _sut.InjectBanners();

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("1") && s.Contains("kingdom")));
    }

    [TestMethod]
    public void InjectBanners_EmptyConfig_NoErrors()
    {
        _sut.InjectBanners();

        _kingdomAdapter.DidNotReceive().SetBanner(Arg.Any<string>(), Arg.Any<string>());
        _clanAdapter.DidNotReceive().SetBanner(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void InjectBanners_MultipleKingdoms_InjectsAllMismatched()
    {
        _configProvider.GetKingdomBannerKeys().Returns(new Dictionary<string, string>
        {
            { "kingdom_erebor", "11.4.4.4345.4345.764.764.1.0.0" },
            { "kingdom_gondor", "11.8.8.4345.4345.764.764.1.0.0" }
        });
        _kingdomAdapter.GetAllKingdoms().Returns(new List<KingdomBannerInfo>
        {
            new KingdomBannerInfo("kingdom_erebor", "0.0.0.0.0.0.0.0.0.0"),
            new KingdomBannerInfo("kingdom_gondor", "0.0.0.0.0.0.0.0.0.0")
        });

        _sut.InjectBanners();

        _kingdomAdapter.Received(1).SetBanner("kingdom_erebor", "11.4.4.4345.4345.764.764.1.0.0");
        _kingdomAdapter.Received(1).SetBanner("kingdom_gondor", "11.8.8.4345.4345.764.764.1.0.0");
    }
}
