using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources;
using TAOM.Features.StartupResources.Config;

namespace TAOM.Tests.Features.StartupResources;

[TestClass]
public class PlayerStartupGoldServiceTests
{
    private IGoldGiftAdapter _goldGiftAdapter;
    private IStartupResourcesConfigProvider _configProvider;
    private IModLogger _logger;
    private PlayerStartupGoldService _sut;

    [TestInitialize]
    public void Setup()
    {
        _goldGiftAdapter = Substitute.For<IGoldGiftAdapter>();
        _configProvider = Substitute.For<IStartupResourcesConfigProvider>();
        _logger = Substitute.For<IModLogger>();
        _configProvider.LoadConfig().Returns(new StartupResourcesConfig());

        _sut = new PlayerStartupGoldService(_goldGiftAdapter, _configProvider, _logger);
    }

    [TestMethod]
    public void GrantPlayerStartupGold_ConfiguredCulture_GivesGold()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", PlayerGold = 5000 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("gondor", "player_hero_id");

        _goldGiftAdapter.Received(1).GiveGoldToHero("player_hero_id", 5000);
    }

    [TestMethod]
    public void GrantPlayerStartupGold_CaseInsensitiveCulture_MatchesCorrectly()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", PlayerGold = 5000 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("Gondor", "player_hero_id");

        _goldGiftAdapter.Received(1).GiveGoldToHero("player_hero_id", 5000);
    }

    [TestMethod]
    public void GrantPlayerStartupGold_CultureNotInConfig_NoGoldGivenAndWarns()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", PlayerGold = 5000 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("unknown_culture", "player_hero_id");

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("unknown_culture")));
    }

    [TestMethod]
    public void GrantPlayerStartupGold_ZeroPlayerGold_SkipsGrant()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", PlayerGold = 0 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("gondor", "player_hero_id");

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
    }

    [TestMethod]
    public void GrantPlayerStartupGold_NullCultureId_NoOpAndWarns()
    {
        _sut.GrantPlayerStartupGold(null, "player_hero_id");

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GrantPlayerStartupGold_EmptyCultureId_NoOpAndWarns()
    {
        _sut.GrantPlayerStartupGold(string.Empty, "player_hero_id");

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GrantPlayerStartupGold_NullHeroId_NoOpAndWarns()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", PlayerGold = 5000 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("gondor", null);

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GrantPlayerStartupGold_HappyPath_LogsAmount()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "mordor", PlayerGold = 7500 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("mordor", "player_hero_id");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("7500") && s.Contains("mordor")));
    }
}
