using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Spider;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.Spider;

[TestClass]
public class SpiderSpawnerServiceTests
{
    private IObjectManagerAdapter _objectManager;
    private IMissionAdapterFactory _adapterFactory;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _objectManager = Substitute.For<IObjectManagerAdapter>();
        _adapterFactory = Substitute.For<IMissionAdapterFactory>();
        _logger = Substitute.For<IModLogger>();
    }

    private SpiderSpawnerService MakeSut(
        Func<string, Monster> monsterLookup = null,
        Func<AgentBuildData, Agent> spawnDelegate = null)
    {
        return new SpiderSpawnerService(
            _objectManager,
            _adapterFactory,
            _logger,
            monsterLookup ?? (_ => null),
            spawnDelegate ?? (_ => null));
    }

    // ---------------------------------------------------------------------
    // Pre-spawn validation: missing dependencies should not crash, only log.
    // ---------------------------------------------------------------------

    [TestMethod]
    public void SpawnSpiders_AnchorCharacterNotFound_ReturnsEmptyAndLogs()
    {
        _objectManager.GetBasicCharacter(SpiderSpawnerService.SpiderCharacterId).Returns((BasicCharacterObject)null);
        var sut = MakeSut();

        var result = sut.SpawnSpiders(team: null, referencePosition: Vec3.Zero, count: 5, radius: 10f);

        Assert.AreEqual(0, result.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("anchor character")));
    }

    [TestMethod]
    public void SpawnSpiders_CountZero_ReturnsEmptyImmediately()
    {
        var sut = MakeSut();

        var result = sut.SpawnSpiders(team: null, referencePosition: Vec3.Zero, count: 0, radius: 10f);

        Assert.AreEqual(0, result.Count);
        // No anchor lookup attempted since count was zero.
        _objectManager.DidNotReceive().GetBasicCharacter(Arg.Any<string>());
    }

    [TestMethod]
    public void SpawnSpiders_NegativeCount_ReturnsEmptyImmediately()
    {
        var sut = MakeSut();

        var result = sut.SpawnSpiders(team: null, referencePosition: Vec3.Zero, count: -3, radius: 10f);

        Assert.AreEqual(0, result.Count);
    }

    // ---------------------------------------------------------------------
    // Spawn-position math (pure function)
    // ---------------------------------------------------------------------

    [TestMethod]
    public void ComputeSpawnPosition_DistanceFromReference_WithinRadiusBounds()
    {
        var rng = new Random(42); // deterministic seed
        var reference = new Vec3(100f, 200f, 50f);
        const float radius = 20f;

        for (int i = 0; i < 100; i++)
        {
            var pos = SpiderSpawnerService.ComputeSpawnPosition(reference, radius, rng);
            float dx = pos.x - reference.x;
            float dy = pos.y - reference.y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            Assert.IsTrue(distance >= radius * 0.5f - 0.01f,
                $"distance {distance} should be >= {radius * 0.5f} (inner ring)");
            Assert.IsTrue(distance <= radius + 0.01f,
                $"distance {distance} should be <= {radius} (outer ring)");
        }
    }

    [TestMethod]
    public void ComputeSpawnPosition_PreservesZAndWComponents()
    {
        var rng = new Random(42);
        var reference = new Vec3(0f, 0f, 7.5f, 1.25f);
        const float radius = 5f;

        var pos = SpiderSpawnerService.ComputeSpawnPosition(reference, radius, rng);

        Assert.AreEqual(reference.z, pos.z);
        Assert.AreEqual(reference.w, pos.w);
    }

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [TestMethod]
    public void Constructor_PublicCtor_InjectsDefaultDelegates()
    {
        // The public constructor wires real Mission/MBObjectManager delegates internally.
        // It should not throw at construction time even if those static singletons aren't ready.
        var sut = new SpiderSpawnerService(_objectManager, _adapterFactory, _logger);
        Assert.IsNotNull(sut);
    }
}
