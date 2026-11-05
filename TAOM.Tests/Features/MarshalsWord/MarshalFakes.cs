using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The engine side of the queue and the march, as a test can set it: which companies exist, where they stand, what was issued.</summary>
internal sealed class FakeMarshalOrders : IMarshalOrderAdapter
{
    internal readonly List<int> Selected = new();
    internal readonly HashSet<int> Lost = new();
    internal readonly HashSet<int> Arrivals = new();
    internal readonly HashSet<int> Walking = new();
    internal readonly HashSet<int> Ended = new();
    internal readonly HashSet<object> Spent = new();
    internal readonly Dictionary<int, double> Distances = new();
    internal readonly Dictionary<int, double> Speeds = new();
    internal readonly List<(int[] Companies, object Payload)> Issued = new();
    internal readonly List<IReadOnlyList<MarchPace>> Published = new();
    internal readonly List<IReadOnlyList<MarchTarget>> TargetsRead = new();
    internal IReadOnlyList<MarchLeg> Legs = Array.Empty<MarchLeg>();
    internal bool IssueResult = true;
    internal Action<object>? OnIssue;
    internal int Attached;
    internal int Detached;

    internal Exception? FailOnNow;

    private double _now;

    public double Now
    {
        get => FailOnNow != null ? throw FailOnNow : _now;
        set => _now = value;
    }

    public bool IsDeployment { get; set; }

    public bool PlayerCommands { get; set; } = true;

    internal IReadOnlyList<MarkSample> Marks { get; set; } = Array.Empty<MarkSample>();

    public IReadOnlyList<MarkSample> ReadMarks() => Marks;

    internal IReadOnlyList<SquareRead> Squares { get; set; } = Array.Empty<SquareRead>();

    internal SpacingRead? Spacing { get; set; }

    internal int SpacingReads;

    public IReadOnlyList<SquareRead> ReadSquares() => Squares;

    public SpacingRead? ReadSpacing()
    {
        SpacingReads++;
        return Spacing;
    }

    public event Action<IReadOnlyList<int>>? DirectOrder;

    public event Action? PlayerTeamChanged;

    internal void RaiseDirectOrder(params int[] companies) => DirectOrder?.Invoke(companies);

    internal void RaiseTeamChanged() => PlayerTeamChanged?.Invoke();

    internal bool HasDirectOrderListeners => DirectOrder != null;

    public void Attach() => Attached++;

    public void Detach() => Detached++;

    public IReadOnlyList<int> SelectedCompanies() => Selected.ToList();

    public bool IsControllable(int company) => !Lost.Contains(company);

    public bool WalkUnderWay(int company) => Walking.Contains(company);

    public bool Arrived(int company) => Arrivals.Contains(company);

    public bool OrderEnded(int company) => Ended.Contains(company);

    public bool PayloadSpent(object payload) => Spent.Contains(payload);

    public double RemainingDistance(int company, object? payload) => Distances.TryGetValue(company, out var d) ? d : 40.0;

    public double Speed(int company) => Speeds.TryGetValue(company, out var s) ? s : 4.0;

    public bool Issue(IReadOnlyList<int> companies, object payload)
    {
        Issued.Add((companies.ToArray(), payload));
        OnIssue?.Invoke(payload);
        return IssueResult;
    }

    public bool TryPoint(object payload, out float x, out float y, out float z)
    {
        x = y = z = 0f;
        if (payload is not FakePayload fake || !fake.HasPoint)
            return false;
        x = fake.X;
        y = fake.Y;
        z = fake.Z;
        return true;
    }

    public IReadOnlyList<MarchLeg> ReadMarch(IReadOnlyList<MarchTarget> targets)
    {
        TargetsRead.Add(targets.ToList());
        return Legs;
    }

    public void PublishPaces(IReadOnlyList<MarchPace> paces) => Published.Add(paces.ToList());
}

/// <summary>A stand-in for the adapter's payload object: the domain holds it as an opaque object.</summary>
internal sealed class FakePayload
{
    internal FakePayload(string name) => Name = name;

    internal string Name { get; }

    internal bool HasPoint { get; set; }

    internal float X { get; set; }

    internal float Y { get; set; }

    internal float Z { get; set; }

    public override string ToString() => Name;
}

/// <summary>The scene side of the session: what was said and which flags were drawn.</summary>
internal sealed class FakeMarshalScreen : TAOM.Client.Adapters.IMarshalScreenAdapter
{
    internal readonly List<MarshalMessage> Said = new();
    internal readonly List<IReadOnlyList<TAOM.Client.Adapters.MarshalFlagPoint>> Shown = new();
    internal readonly List<string> Calls = new();
    internal int Hidden;
    internal int Dropped;
    internal Exception? HideThrows;

    public bool PlayerTeamExists { get; set; } = true;

    public void Say(MarshalMessage message) => Said.Add(message);

    public void ShowFlags(IReadOnlyList<TAOM.Client.Adapters.MarshalFlagPoint> points) => Shown.Add(points.ToList());

    public void HideFlags()
    {
        Hidden++;
        Calls.Add("hide");
        if (HideThrows != null)
            throw HideThrows;
    }

    public void DropFlags()
    {
        Dropped++;
        Calls.Add("drop");
    }
}

internal sealed class FakeMarshalSettings : IMarshalSettingsProvider
{
    public bool Enabled { get; set; } = true;

    public bool KeepArray { get; set; } = true;

    public bool ArriveTogether { get; set; } = true;

    public bool Outlines { get; set; } = true;

    public bool HollowSquare { get; set; } = true;

    public bool SpacingRepair { get; set; } = true;
}

/// <summary>
/// A <c>WorldPosition</c> whose height is already valid, so that reading its ground makes no native call. The public constructors take a
/// <c>Scene</c>, whose base type initializer needs the engine, so the two fields are set on a boxed copy.
/// </summary>
internal static class TestPositions
{
    internal static WorldPosition At(float x, float y, float z)
    {
        object boxed = default(WorldPosition);
        typeof(WorldPosition).GetField("_position", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(boxed, new Vec3(x, y, z, -1f));
        var state = typeof(WorldPosition).GetField("State")!;
        state.SetValue(boxed, System.Enum.Parse(state.FieldType, "Valid"));
        return (WorldPosition)boxed;
    }
}

/// <summary>The archers' side of the engine as a test sets it: the census each sweep answers, and what was swept.</summary>
internal sealed class FakeMarshalFire : IMarshalFireAdapter
{
    internal readonly List<int[]> Swept = new();
    internal readonly List<int[]> SweptHeld = new();
    internal readonly List<int[]> Counted = new();
    internal readonly List<double> CountedAt = new();
    internal readonly Dictionary<int, FireCensus> Census = new();
    internal readonly Dictionary<int, HoldCensus> HoldCensuses = new();
    internal readonly List<int[]> HoldPasses = new();
    internal readonly List<double> HoldPassAt = new();
    internal int Dropped;
    internal int Released;
    internal System.Exception? ReleaseThrows;

    public bool HasTouched { get; set; }

    public bool CallbackFaulted { get; set; }

    public IReadOnlyList<HoldCensus> Hold(IReadOnlyList<int> held, double now)
    {
        HoldPasses.Add(held.ToArray());
        HoldPassAt.Add(now);
        return held.Where(c => HoldCensuses.ContainsKey(c)).Select(c => HoldCensuses[c]).ToList();
    }

    public IReadOnlyList<FireCensus> Count(IReadOnlyList<int> companies, double now)
    {
        Counted.Add(companies.ToArray());
        CountedAt.Add(now);
        return companies.Where(c => Census.ContainsKey(c)).Select(c => Census[c]).ToList();
    }

    public IReadOnlyList<FireCensus> Sweep(IReadOnlyList<int> ruled, IReadOnlyList<int> held, double now)
    {
        Swept.Add(ruled.ToArray());
        SweptHeld.Add(held.ToArray());
        HasTouched = ruled.Count > 0;
        return ruled.Select(c => Census.TryGetValue(c, out var census) ? census : new FireCensus(c, 0, 0)).ToList();
    }

    public void ReleaseAll()
    {
        Released++;
        if (ReleaseThrows != null)
            throw ReleaseThrows;
    }

    public void Drop() => Dropped++;
}

/// <summary>The session's view of the outlines: how many are painted, and the shows it was told.</summary>
internal sealed class FakeMarshalOutlines : IMarshalOutlines
{
    internal readonly List<bool> Shows = new();
    internal int Cleared;
    internal int Dropped;
    internal System.Exception? ClearThrows;

    public int PaintedCount { get; set; }

    public void Pass(double now, bool show) => Shows.Add(show);

    public void ClearAll()
    {
        Cleared++;
        if (ClearThrows != null)
            throw ClearThrows;
    }

    public void Drop() => Dropped++;
}
