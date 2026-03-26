using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources;
using TAOM.Features.StartupResources.Config;

namespace TAOM.Tests.Features.StartupResources;

[TestClass]
public class StartupInfluenceServiceTests
{
    private IClanStartupAdapter _clanAdapter;
    private IStartupResourcesConfigProvider _configProvider;
    private IModLogger _logger;
    private StartupInfluenceService _sut;

    [TestInitialize]
    public void Setup()
    {
        _clanAdapter = Substitute.For<IClanStartupAdapter>();
        _configProvider = Substitute.For<IStartupResourcesConfigProvider>();
        _logger = Substitute.For<IModLogger>();

        _clanAdapter.GetEligibleClans().Returns(new List<ClanStartupInfo>());
        _configProvider.LoadConfig().Returns(new StartupResourcesConfig());

        _sut = new StartupInfluenceService(_clanAdapter, _configProvider, _logger);
    }

    [TestMethod]
    public void DistributeStartupInfluence_ClanInConfiguredCulture_AddsInfluence()
    {
        var clans = new List<ClanStartupInfo>
        {
            new ClanStartupInfo("clan_gondor_1", "gondor")
        };
        _clanAdapter.GetEligibleClans().Returns(clans);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 100 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupInfluence();

        _clanAdapter.Received(1).AddInfluence("clan_gondor_1", 100f);
    }

    [TestMethod]
    public void DistributeStartupInfluence_CultureNotInConfig_NoInfluenceAdded()
    {
        var clans = new List<ClanStartupInfo>
        {
            new ClanStartupInfo("clan_unknown_1", "unknown_culture")
        };
        _clanAdapter.GetEligibleClans().Returns(clans);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 100 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupInfluence();

        _clanAdapter.DidNotReceiveWithAnyArgs().AddInfluence(default, default);
    }

    [TestMethod]
    public void DistributeStartupInfluence_MultipleClans_DistributesCorrectAmounts()
    {
        var clans = new List<ClanStartupInfo>
        {
            new ClanStartupInfo("clan_gondor_1", "gondor"),
            new ClanStartupInfo("clan_isengard_1", "isengard")
        };
        _clanAdapter.GetEligibleClans().Returns(clans);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 100 },
                new CultureResourceEntry { CultureId = "isengard", Gold = 2000000, Influence = 2000 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupInfluence();

        _clanAdapter.Received(1).AddInfluence("clan_gondor_1", 100f);
        _clanAdapter.Received(1).AddInfluence("clan_isengard_1", 2000f);
    }

    [TestMethod]
    public void DistributeStartupInfluence_NoClans_NoErrors()
    {
        _sut.DistributeStartupInfluence();

        _clanAdapter.DidNotReceiveWithAnyArgs().AddInfluence(default, default);
    }

    [TestMethod]
    public void DistributeStartupInfluence_ZeroInfluence_SkipsClan()
    {
        var clans = new List<ClanStartupInfo>
        {
            new ClanStartupInfo("clan_gondor_1", "gondor")
        };
        _clanAdapter.GetEligibleClans().Returns(clans);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 0 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupInfluence();

        _clanAdapter.DidNotReceiveWithAnyArgs().AddInfluence(default, default);
    }

    [TestMethod]
    public void DistributeStartupInfluence_LogsTotals()
    {
        var clans = new List<ClanStartupInfo>
        {
            new ClanStartupInfo("clan_gondor_1", "gondor")
        };
        _clanAdapter.GetEligibleClans().Returns(clans);

        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", Gold = 500000, Influence = 100 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.DistributeStartupInfluence();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("100") && s.Contains("1")));
    }
}
