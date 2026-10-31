using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// A director over three NSubstitute adapters. The scene adapter hands back bases at x 0 and x 1000 and passes layouts through;
/// the battlefield adapter numbers its spawns from 1 and records them; the tower adapter records its towers and keeps the
/// destroyed handler the director registered, so a test can play the engine's part. No countdown, no passive income, no free
/// waves unless a test asks for them.
/// </summary>
internal sealed class FordsDirectorFixture
{
    public readonly IFordsBattlefieldAdapter Battlefield = Substitute.For<IFordsBattlefieldAdapter>();
    public readonly IFordsTowerAdapter Towers = Substitute.For<IFordsTowerAdapter>();
    public readonly IFordsSceneAdapter Scene = Substitute.For<IFordsSceneAdapter>();
    public readonly IModLogger Logger = Substitute.For<IModLogger>();
    public readonly FordsSession Session = new();
    public FordsRunReport Report = null!;

    public readonly List<(string Troop, MatchSide Side, int Lane, Vec3f Position)> Spawned = new();
    public readonly List<(TowerSlot Slot, TowerPrefab Prefab, int HitPoints)> TowersSpawned = new();

    public FordsMissionDirector Director = null!;
    public FordsMatchPlan Plan = null!;
    public Action<string>? DestroyedHandler;
    public List<FordsUnitSample> Samples = new();

    private int _nextKey;

    public static FordsDirectorFixture Create(Func<FordsMatchConfig, FordsMatchConfig>? tweak = null,
        MapLayoutMode mode = MapLayoutMode.Generated, int lanes = 3)
    {
        var fixture = new FordsDirectorFixture();
        fixture.Build(tweak, mode, lanes);
        return fixture;
    }

    private void Build(Func<FordsMatchConfig, FordsMatchConfig>? tweak, MapLayoutMode mode, int lanes)
    {
        var d = FordsMatchConfig.Default;
        var config = d with
        {
            CountdownSeconds = 0f,
            Economy = d.Economy with { StartingGold = 600, IncomePerMinute = 0 },
            Waves = d.Waves with { FreeWaveIntervalSeconds = 0f, MaxSpawnsPerTick = 1000 },
        };
        if (tweak != null)
            config = tweak(config);

        var map = new MapEntry("test_scene", "Test", mode, new LayoutOptions(LaneCount: lanes),
            TowerPrefab.DefaultTower, TowerPrefab.DefaultKeep, CampDefaults.Default);
        Plan = new FordsMatchPlan(config, map, FordsMatchFixture.Factions(), "hard", lanes);

        Report = new FordsRunReport(Logger);
        Report.BeginRun("console", new FordsMatchSetup(map.SceneId, "west", "east", "hard", lanes));
        Battlefield.CharacterExists(Arg.Any<string>()).Returns(true);
        Battlefield.Prepare(Arg.Any<int>()).Returns(true);
        Battlefield.Spawn(Arg.Any<string>(), Arg.Any<MatchSide>(), Arg.Any<int>(), Arg.Any<Vec3f>(), Arg.Any<Vec3f>())
            .Returns(call =>
            {
                Spawned.Add((call.ArgAt<string>(0), call.ArgAt<MatchSide>(1), call.ArgAt<int>(2), call.ArgAt<Vec3f>(3)));
                return new FordsSpawnedAgent(new AgentKey(++_nextKey), 10, false);
            });
        Battlefield.SampleUnits().Returns(_ => Samples);
        Battlefield.Move(Arg.Any<MatchSide>(), Arg.Any<int>(), Arg.Any<Vec3f>()).Returns(true);

        Scene.TryGetBasePoints(Arg.Any<float>(), out Arg.Any<Vec3f>(), out Arg.Any<Vec3f>())
            .Returns(call =>
            {
                call[1] = new Vec3f(0f, 0f, 0f);
                call[2] = new Vec3f(1000f, 0f, 0f);
                return true;
            });
        Scene.BoundaryPolygon().Returns(new List<Vec3f> { new(0f, -100f, 0f), new(1000f, -100f, 0f), new(1000f, 100f, 0f) });
        Scene.Fit(Arg.Any<MapLayout>()).Returns(call => new FordsLayoutFit(call.Arg<MapLayout>(), 0, 0, 0));

        Towers.SpawnTower(Arg.Any<TowerSlot>(), Arg.Any<TowerPrefab>(), Arg.Any<int>())
            .Returns(call =>
            {
                TowersSpawned.Add((call.Arg<TowerSlot>(), call.Arg<TowerPrefab>(), call.ArgAt<int>(2)));
                return true;
            });
        Towers.SpawnGunners(Arg.Any<TowerSlot>(), Arg.Any<string>(), Arg.Any<int>()).Returns(call => call.ArgAt<int>(2));
        Towers.When(t => t.SetDestroyedHandler(Arg.Any<Action<string>>()))
            .Do(call => DestroyedHandler = call.Arg<Action<string>>());

        Director = new FordsMissionDirector(Plan, Battlefield, Towers, Scene, Session, Logger, new SeededFordsRandom(1), Report);
    }

    public TowerSlot TowerSlot(string id) => TowersSpawned.Single(t => t.Slot.Id == id).Slot;

    public FordsUnitSample Sample(int key, MatchSide side, int lane, Vec3f position)
    {
        var sample = new FordsUnitSample(new AgentKey(key), side, lane, position);
        Samples.Add(sample);
        return sample;
    }

    /// <summary>Makes the battlefield fake report <paramref name="victim"/> removed, killed by <paramref name="killer"/>.</summary>
    public void ReportsRemoval(AgentKey victim, AgentKey? killer)
    {
        AgentKey victimOut;
        AgentKey? killerOut;
        Battlefield.TryRemoveUnit(Arg.Any<object>(), Arg.Any<object?>(), Arg.Any<bool>(), out victimOut, out killerOut)
            .Returns(call =>
            {
                call[3] = victim;
                call[4] = killer;
                return true;
            });
    }

    public void ReportsTowerId(string? towerId) => Towers.TowerIdOf(Arg.Any<object?>()).Returns(towerId);
}
