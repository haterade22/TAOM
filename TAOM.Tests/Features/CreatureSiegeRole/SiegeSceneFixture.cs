using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CreatureSiegeRole;
using TAOM.Features.CreatureSiegeRole.Domain;
using TAOM.Tests.Features.SiegeForces;
using static TAOM.Tests.Features.CreatureSiegeRole.CreatureSiegeFixtures;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// One creature agent as a test sees it: an NSubstitute <see cref="ICreatureSiegeAgentAdapter"/> whose properties read the
/// fields below, so a test changes the agent by assigning a field, and whose calls are recorded in <see cref="Calls"/> in the
/// order they were made. The substitute models the engine's side effects the service relies on: a strike or a hold sets the
/// scripted flags, <c>ClearCombatTarget</c> clears the attack target, and <c>Release</c> clears both.
/// </summary>
internal sealed class FakeCreature
{
    public FakeCreature(SiegeSide side = SiegeSide.Attacker, SiegePoint? at = null)
    {
        Side = side;
        Position = at ?? (side == SiegeSide.Defender ? new SiegePoint(100f, 190f, 10f) : new SiegePoint(100f, 250f, 10f));

        var a = Substitute.For<ICreatureSiegeAgentAdapter>();
        a.Identity.Returns(_ => Identity);
        a.IsAIControlled.Returns(_ => IsAIControlled);
        a.IsFleeing.Returns(_ => IsFleeing);
        a.Side.Returns(_ => Side);
        a.Position.Returns(_ => Position);
        a.NavigationFaceId.Returns(_ => NavigationFaceId);
        a.IsInLadderQueue.Returns(_ => InLadderQueue);
        a.Formation.Returns(_ => Formation);
        a.HasScriptedPosition.Returns(_ => HasScriptedPosition);
        a.IsAttackingEntity.Returns(_ => IsAttackingEntity);
        a.PathExists(Arg.Any<SiegePoint>()).Returns(ci =>
        {
            var point = (SiegePoint)ci[0];
            PathQueries.Add(point);
            return PathRule(point);
        });
        a.When(x => x.ExcludeFace(Arg.Any<int>())).Do(ci =>
        {
            Calls.Add("exclude " + (int)ci[0]);
            Excluded.Add((int)ci[0]);
        });
        a.When(x => x.Strike(Arg.Any<SiegePoint>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<object>())).Do(ci =>
        {
            Calls.Add("strike");
            if (StrikeThrows) throw new InvalidOperationException("the engine refused the strike");
            StrikePoints.Add((SiegePoint)ci[0]);
            StrikeGates.Add(ci[3]);
            HasScriptedPosition = true;
            IsAttackingEntity = true;
        });
        a.When(x => x.Hold(Arg.Any<SiegePoint>(), Arg.Any<float>(), Arg.Any<float>())).Do(ci =>
        {
            Calls.Add("hold");
            HoldPoints.Add((SiegePoint)ci[0]);
            HasScriptedPosition = true;
        });
        a.When(x => x.ClearCombatTarget()).Do(_ =>
        {
            Calls.Add("clearTarget");
            IsAttackingEntity = false;
        });
        a.When(x => x.Release()).Do(_ =>
        {
            Calls.Add("release");
            if (ReleaseThrows) throw new InvalidOperationException("the engine refused the release");
            HasScriptedPosition = false;
            IsAttackingEntity = false;
        });
        Adapter = a;
    }

    public ICreatureSiegeAgentAdapter Adapter { get; }

    public object Identity { get; } = new();

    public bool IsAIControlled { get; set; } = true;

    public bool IsFleeing { get; set; }

    public SiegeSide Side { get; set; }

    public SiegePoint Position { get; set; }

    public int NavigationFaceId { get; set; } = 333;

    public bool InLadderQueue { get; set; }

    public SiegeFormationState Formation { get; set; } = SiegeFormationState.None;

    public bool HasScriptedPosition { get; set; }

    public bool IsAttackingEntity { get; set; }

    public bool StrikeThrows { get; set; }

    public bool ReleaseThrows { get; set; }

    /// <summary>Whether a path exists from this creature to a point. Everything is reachable unless a test says otherwise.</summary>
    public Func<SiegePoint, bool> PathRule { get; set; } = _ => true;

    public List<string> Calls { get; } = new();

    public List<int> Excluded { get; } = new();

    public List<SiegePoint> StrikePoints { get; } = new();

    public List<object> StrikeGates { get; } = new();

    public List<SiegePoint> HoldPoints { get; } = new();

    public List<SiegePoint> PathQueries { get; } = new();

    public int Count(string call) => Calls.Count(c => c == call);

    /// <summary>The calls that move or release the creature: everything but the exclusion calls.</summary>
    public List<string> Moves => Calls.Where(c => !c.StartsWith("exclude", StringComparison.Ordinal)).ToList();
}

/// <summary>
/// A castle siege as the role service sees it: an NSubstitute mission adapter whose readings come from the properties below,
/// a logger substitute, and the service under test. The default scene is the TAOM siege shape: an outer gate at (100, 200)
/// facing +Y (the attackers' side), an inner gate 20 m behind it, two towers sharing one bridge face, four ladders, a flat
/// ground at height 10, and attackers standing outside at y = 250.
/// </summary>
internal sealed class SiegeSceneFixture : IDisposable
{
    public SiegeSceneFixture(FakeRaceManager? races = null)
    {
        Mission = Substitute.For<ICreatureSiegeMissionAdapter>();
        Logger = Substitute.For<IModLogger>();
        Races = races ?? FakeRaceManager.WithTrolls();

        OuterGate = Gate(name: "outer_gate_a", destruction: new object(), handle: new object());
        InnerGate = Gate(name: "inner_gate_b", y: 180f, middleY: 178f, destruction: new object(), handle: new object());

        Mission.MissionToken.Returns(_ => Token);
        Mission.SceneName.Returns(_ => "taom_test_scene");
        Mission.Time.Returns(_ => Now);
        Mission.IsSiegeBattle.Returns(_ => IsSiege);
        Mission.HasSallyOutController.Returns(_ => HasSallyOut);
        Mission.IsClientOrReplay.Returns(_ => IsClient);
        Mission.IsDeploymentFinished.Returns(_ => DeploymentFinished);
        Mission.OuterGateCandidates().Returns(_ => OuterCandidates ?? (OuterGate == null ? List<SiegeGateReading>() : List(OuterGate)));
        Mission.InnerGateCandidates().Returns(_ => InnerCandidates ?? (InnerGate == null ? List<SiegeGateReading>() : List(InnerGate)));
        Mission.ReadGate(Arg.Any<object>()).Returns(ci => InnerGate != null && ReferenceEquals(ci[0], InnerGate.Handle) ? InnerState : OuterState);
        Mission.ReadRam().Returns(_ => Ram);
        Mission.ReadLadderWallIds().Returns(_ => LadderIds);
        Mission.ReadTowers().Returns(_ => Towers);
        Mission.GroundHeight(Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>()).Returns(_ => GroundZ);
        Mission.CollectCreatures(Arg.Any<Func<int, bool>>()).Returns(ci =>
        {
            var accepts = (Func<int, bool>)ci[0];
            CollectedWith = accepts;
            return Creatures.Select(c => c.Adapter).ToList();
        });

        Service = new CreatureSiegeRoleService(Mission, Races, 2f, Logger);
    }

    public ICreatureSiegeMissionAdapter Mission { get; }

    public IModLogger Logger { get; }

    public FakeRaceManager Races { get; }

    public CreatureSiegeRoleService Service { get; }

    public object Token { get; } = new();

    public float Now { get; set; } = 100f;

    public bool IsSiege { get; set; } = true;

    public bool HasSallyOut { get; set; }

    public bool IsClient { get; set; }

    public bool DeploymentFinished { get; set; } = true;

    public SiegeGateReading? OuterGate { get; set; }

    public SiegeGateReading? InnerGate { get; set; }

    /// <summary>When set, the candidate list the adapter returns instead of the single default gate.</summary>
    public IReadOnlyList<SiegeGateReading>? OuterCandidates { get; set; }

    public IReadOnlyList<SiegeGateReading>? InnerCandidates { get; set; }

    public GateLiveState OuterState { get; set; } = new(false, false, 15000f);

    public GateLiveState InnerState { get; set; } = new(false, false, 12000f);

    public RamReading Ram { get; set; }

    public float GroundZ { get; set; } = 10f;

    public List<int> LadderIds { get; set; } = new() { 333, 444, 555, 666 };

    public List<TowerFaces> Towers { get; set; } = new() { new TowerFaces(1000050, 601), new TowerFaces(1000100, 601) };

    public List<FakeCreature> Creatures { get; } = new();

    /// <summary>The race test the service handed the adapter on the last collect.</summary>
    public Func<int, bool>? CollectedWith { get; private set; }

    public FakeCreature Add(SiegeSide side = SiegeSide.Attacker, SiegePoint? at = null)
    {
        var creature = new FakeCreature(side, at);
        Creatures.Add(creature);
        return creature;
    }

    public bool Activate(bool roleEnabled = true) => Service.TryActivate(roleEnabled);

    /// <summary>Advances the mission clock and runs one tick of the service.</summary>
    public void Pass(float advanceSeconds = 0.5f)
    {
        Now += advanceSeconds;
        Service.Tick();
    }

    public IReadOnlyList<string> Lines(string level) =>
        Logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == level)
            .Select(call => (string)call.GetArguments()[0]!)
            .ToList();

    public IReadOnlyList<string> Infos => Lines(nameof(IModLogger.LogInfo));

    public IReadOnlyList<string> Warnings => Lines(nameof(IModLogger.LogWarning));

    public IReadOnlyList<string> Errors => Lines(nameof(IModLogger.LogError));

    public IReadOnlyList<string> Debugs => Lines(nameof(IModLogger.LogDebug));

    public void Dispose()
    {
        Service.Shutdown();
        CreatureSiegeSnapshot.Clear();
    }
}
