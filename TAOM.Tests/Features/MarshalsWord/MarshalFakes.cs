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
