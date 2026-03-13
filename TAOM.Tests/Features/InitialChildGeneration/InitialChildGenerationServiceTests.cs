using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.InitialChildGeneration.Config;

namespace TAOM.Tests.Features.InitialChildGeneration;

[TestClass]
public class InitialChildGenerationServiceTests
{
    private IClanPopulationAdapter _clanAdapter;
    private IChildCreatorAdapter _childCreator;
    private IInitialChildGenerationConfigProvider _configProvider;
    private IRandomSource _random;
    private IModLogger _logger;
    private InitialChildGenerationService _sut;
    private InitialChildGenerationConfig _config;

    [TestInitialize]
    public void Setup()
    {
        _clanAdapter = Substitute.For<IClanPopulationAdapter>();
        _childCreator = Substitute.For<IChildCreatorAdapter>();
        _configProvider = Substitute.For<IInitialChildGenerationConfigProvider>();
        _random = Substitute.For<IRandomSource>();
        _logger = Substitute.For<IModLogger>();

        _config = new InitialChildGenerationConfig();
        _configProvider.LoadConfig().Returns(_config);

        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>());

        _sut = new InitialChildGenerationService(
            _clanAdapter, _childCreator, _configProvider, _random, _logger);
    }

    [TestMethod]
    public void GenerateInitialChildren_ExcludedClan_NoChildrenCreated()
    {
        _config.ExcludedClans.Add("clan_evil_1");
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_evil_1", "mordor",
                new[] { "hero_m1" }, new[] { "hero_f1" }, 0)
        });

        _sut.GenerateInitialChildren();

        _childCreator.DidNotReceive().CreateChild(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_ExcludedCulture_NoChildrenCreated()
    {
        _config.ExcludedCultures.Add("mordor");
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_mordor_1", "mordor",
                new[] { "hero_m1", "hero_m2" }, new[] { "hero_f1" }, 0)
        });

        _sut.GenerateInitialChildren();

        _childCreator.DidNotReceive().CreateChild(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_DefaultFormula_CorrectChildCount()
    {
        // 3 males + 3 females = 6 adults => ceil(6/2) = 3 children
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1", "hero_m2", "hero_m3" },
                new[] { "hero_f1", "hero_f2", "hero_f3" }, 0)
        });
        _random.NextDouble().Returns(0.5); // female (< 0.49 = false, so male)
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0); // pick first template, age=minAge

        _sut.GenerateInitialChildren();

        _childCreator.Received(3).CreateChild(
            Arg.Any<string>(), "clan_gondor_1", Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_DefaultFormula_SubtractsExistingChildren()
    {
        // 6 adults => ceil(6/2) = 3, minus 1 existing = 2
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1", "hero_m2", "hero_m3" },
                new[] { "hero_f1", "hero_f2", "hero_f3" }, 1)
        });
        _random.NextDouble().Returns(0.5);
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);

        _sut.GenerateInitialChildren();

        _childCreator.Received(2).CreateChild(
            Arg.Any<string>(), "clan_gondor_1", Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_DefaultFormula_NeverNegative()
    {
        // 2 adults => ceil(2/2) = 1, minus 5 existing => clamped to 0
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1" }, new[] { "hero_f1" }, 5)
        });

        _sut.GenerateInitialChildren();

        _childCreator.DidNotReceive().CreateChild(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_CustomMultiplier_AppliedCorrectly()
    {
        // 6 adults => ceil(6/2) = 3, * 0.5 = ceil(1.5) = 2, minus 0 existing = 2
        _config.Defaults.ChildCountMultiplier = 0.5;
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1", "hero_m2", "hero_m3" },
                new[] { "hero_f1", "hero_f2", "hero_f3" }, 0)
        });
        _random.NextDouble().Returns(0.5);
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);

        _sut.GenerateInitialChildren();

        _childCreator.Received(2).CreateChild(
            Arg.Any<string>(), "clan_gondor_1", Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_FixedChildCount_OverridesFormula()
    {
        _config.ClanOverrides.Add(new ClanOverride
        {
            ClanId = "clan_gondor_1",
            FixedChildCount = 5
        });
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1" }, new[] { "hero_f1" }, 0)
        });
        _random.NextDouble().Returns(0.5);
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);

        _sut.GenerateInitialChildren();

        _childCreator.Received(5).CreateChild(
            Arg.Any<string>(), "clan_gondor_1", Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_CultureOverride_AgeRange()
    {
        _config.CultureOverrides.Add(new CultureOverride
        {
            CultureId = "erebor",
            MinAge = 5,
            MaxAge = 10
        });
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_erebor_1", "erebor",
                new[] { "hero_m1", "hero_m2" }, new[] { "hero_f1", "hero_f2" }, 0)
        });
        _random.NextDouble().Returns(0.5); // male
        _random.Next(5, 11).Returns(7); // age within culture range
        _random.Next(Arg.Is<int>(x => x != 5), Arg.Is<int>(x => x != 11)).Returns(0); // template index

        _sut.GenerateInitialChildren();

        // ceil(4/2) = 2 children, each should use age range [5, 10] => Next(5, 11)
        _random.Received().Next(5, 11);
    }

    [TestMethod]
    public void GenerateInitialChildren_ClanOverride_OverridesCultureOverride()
    {
        _config.CultureOverrides.Add(new CultureOverride
        {
            CultureId = "gondor",
            MinAge = 5,
            MaxAge = 10
        });
        _config.ClanOverrides.Add(new ClanOverride
        {
            ClanId = "clan_gondor_1",
            MinAge = 8,
            MaxAge = 12
        });
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1", "hero_m2" }, new[] { "hero_f1", "hero_f2" }, 0)
        });
        _random.NextDouble().Returns(0.5);
        _random.Next(8, 13).Returns(10);
        _random.Next(Arg.Is<int>(x => x != 8), Arg.Is<int>(x => x != 13)).Returns(0);

        _sut.GenerateInitialChildren();

        // Clan override (8-12) should win over culture override (5-10)
        _random.Received().Next(8, 13);
    }

    [TestMethod]
    public void GenerateInitialChildren_GenderRatio_AllMale()
    {
        _config.Defaults.FemaleRatio = 0.0;
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1", "hero_m2" }, new[] { "hero_f1", "hero_f2" }, 0)
        });
        _random.NextDouble().Returns(0.5); // 0.5 < 0.0 is false => male
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);

        _sut.GenerateInitialChildren();

        // All children should be male (isFemale = false)
        _childCreator.Received(2).CreateChild(
            Arg.Any<string>(), "clan_gondor_1", false, Arg.Any<int>());
        _childCreator.DidNotReceive().CreateChild(
            Arg.Any<string>(), Arg.Any<string>(), true, Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_GenderRatio_AllFemale()
    {
        _config.Defaults.FemaleRatio = 1.0;
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1", "hero_m2" }, new[] { "hero_f1", "hero_f2" }, 0)
        });
        _random.NextDouble().Returns(0.5); // 0.5 < 1.0 is true => female
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);

        _sut.GenerateInitialChildren();

        _childCreator.Received(2).CreateChild(
            Arg.Any<string>(), "clan_gondor_1", true, Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_NoClans_NoErrors()
    {
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>());

        _sut.GenerateInitialChildren();

        _childCreator.DidNotReceive().CreateChild(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_MixedClans_ExcludedAndIncluded()
    {
        _config.ExcludedCultures.Add("mordor");
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_mordor_1", "mordor",
                new[] { "hero_m1", "hero_m2" }, new[] { "hero_f1", "hero_f2" }, 0),
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1", "hero_m2" }, new[] { "hero_f1", "hero_f2" }, 0)
        });
        _random.NextDouble().Returns(0.5);
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);

        _sut.GenerateInitialChildren();

        _childCreator.DidNotReceive().CreateChild(
            Arg.Any<string>(), "clan_mordor_1", Arg.Any<bool>(), Arg.Any<int>());
        _childCreator.Received(2).CreateChild(
            Arg.Any<string>(), "clan_gondor_1", Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_OddAdultCount_CeilApplied()
    {
        // 5 adults => ceil(5/2) = 3
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1", "hero_m2", "hero_m3" },
                new[] { "hero_f1", "hero_f2" }, 0)
        });
        _random.NextDouble().Returns(0.5);
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);

        _sut.GenerateInitialChildren();

        _childCreator.Received(3).CreateChild(
            Arg.Any<string>(), "clan_gondor_1", Arg.Any<bool>(), Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_ZeroMaleClan_FirstChildForcedMale()
    {
        // Vanilla behavior: if no males, first child is always male
        _config.Defaults.FemaleRatio = 1.0; // normally all female
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                Array.Empty<string>(), new[] { "hero_f1", "hero_f2" }, 0)
        });
        _random.NextDouble().Returns(0.5);
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);

        _sut.GenerateInitialChildren();

        // ceil(2/2) = 1 child, and it should be forced male despite FemaleRatio=1.0
        _childCreator.Received(1).CreateChild(
            Arg.Any<string>(), "clan_gondor_1", false, Arg.Any<int>());
    }

    [TestMethod]
    public void GenerateInitialChildren_TemplateSelection_UsesSameGenderHero()
    {
        _clanAdapter.GetMajorFactionClans().Returns(new List<ClanPopulationInfo>
        {
            new ClanPopulationInfo("clan_gondor_1", "gondor",
                new[] { "hero_m1", "hero_m2" }, new[] { "hero_f1", "hero_f2" }, 0)
        });
        // First child: NextDouble=0.3 => female (< 0.49), template index=1 => hero_f2
        // Second child: NextDouble=0.6 => male (>= 0.49), template index=0 => hero_m1
        _random.NextDouble().Returns(0.3, 0.6);
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(
            7,  // age for first child
            1,  // template index for first child (hero_f2)
            7,  // age for second child
            0   // template index for second child (hero_m1)
        );

        _sut.GenerateInitialChildren();

        _childCreator.Received(1).CreateChild("hero_f2", "clan_gondor_1", true, 7);
        _childCreator.Received(1).CreateChild("hero_m1", "clan_gondor_1", false, 7);
    }
}
