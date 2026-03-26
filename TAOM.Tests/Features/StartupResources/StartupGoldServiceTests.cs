using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources;
using TAOM.Features.StartupResources.Config;

namespace TAOM.Tests.Features.StartupResources;

[TestClass]
public class StartupGoldServiceTests
{
    private IStartupHeroAdapter _heroAdapter;
    private IGoldGiftAdapter _goldGiftAdapter;
    private IStartupResourcesConfigProvider _configProvider;
    private IModLogger _logger;
    private StartupGoldService _sut;

    [TestInitialize]
    public void Setup()
    {
        _heroAdapter = Substitute.For<IStartupHeroAdapter>();
        _goldGiftAdapter = Substitute.For<IGoldGiftAdapter>();
        _configProvider = Substitute.For<IStartupResourcesConfigProvider>();
        _logger = Substitute.For<IModLogger>();

        _heroAdapter.GetAliveLordHeroes().Returns(new List<LordHeroInfo>());
        _configProvider.LoadConfig().Returns(new StartupResourcesConfig());

        _sut = new StartupGoldService(_heroAdapter, _goldGiftAdapter, _configProvider, _logger);
    }

    [TestMethod]
    public void DistributeStartupGold_LordInConfiguredCulture_GivesGold()
    {
        var heroes = new List<LordHeroInfo>
        {
            new LordHeroInfo("lord_gondor_1", "gondor", false)
        };
        _heroAdapter.GetAliveLordHeroes().Returns(heroes);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 100 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupGold();

        _goldGiftAdapter.Received(1).GiveGoldToHero("lord_gondor_1", 500000);
    }

    [TestMethod]
    public void DistributeStartupGold_PlayerClanLord_SkipsHero()
    {
        var heroes = new List<LordHeroInfo>
        {
            new LordHeroInfo("player_lord", "gondor", true)
        };
        _heroAdapter.GetAliveLordHeroes().Returns(heroes);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 100 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupGold();

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
    }

    [TestMethod]
    public void DistributeStartupGold_CultureNotInConfig_NoGoldGiven()
    {
        var heroes = new List<LordHeroInfo>
        {
            new LordHeroInfo("lord_unknown_1", "unknown_culture", false)
        };
        _heroAdapter.GetAliveLordHeroes().Returns(heroes);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 100 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupGold();

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
    }

    [TestMethod]
    public void DistributeStartupGold_MultipleLords_DistributesCorrectAmounts()
    {
        var heroes = new List<LordHeroInfo>
        {
            new LordHeroInfo("lord_gondor_1", "gondor", false),
            new LordHeroInfo("lord_rivendell_1", "rivendell", false),
            new LordHeroInfo("player_lord", "gondor", true)
        };
        _heroAdapter.GetAliveLordHeroes().Returns(heroes);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 100 },
                new CultureResourceEntry { CultureId = "rivendell", Gold = 6000000, Influence = 2000 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupGold();

        _goldGiftAdapter.Received(1).GiveGoldToHero("lord_gondor_1", 500000);
        _goldGiftAdapter.Received(1).GiveGoldToHero("lord_rivendell_1", 6000000);
        _goldGiftAdapter.DidNotReceive().GiveGoldToHero("player_lord", Arg.Any<int>());
    }

    [TestMethod]
    public void DistributeStartupGold_NoHeroes_NoErrors()
    {
        _sut.DistributeStartupGold();

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
    }

    [TestMethod]
    public void DistributeStartupGold_CaseInsensitiveCulture_MatchesCorrectly()
    {
        var heroes = new List<LordHeroInfo>
        {
            new LordHeroInfo("lord_1", "Gondor", false)
        };
        _heroAdapter.GetAliveLordHeroes().Returns(heroes);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 100 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupGold();

        _goldGiftAdapter.Received(1).GiveGoldToHero("lord_1", 500000);
    }

    [TestMethod]
    public void DistributeStartupGold_ZeroGold_SkipsHero()
    {
        var heroes = new List<LordHeroInfo>
        {
            new LordHeroInfo("lord_gondor_1", "gondor", false)
        };
        _heroAdapter.GetAliveLordHeroes().Returns(heroes);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 0, Influence = 100 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupGold();

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
    }

    [TestMethod]
    public void DistributeStartupGold_LogsTotals()
    {
        var heroes = new List<LordHeroInfo>
        {
            new LordHeroInfo("lord_gondor_1", "gondor", false)
        };
        _heroAdapter.GetAliveLordHeroes().Returns(heroes);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 100 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupGold();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("500000") && s.Contains("1")));
    }
}
