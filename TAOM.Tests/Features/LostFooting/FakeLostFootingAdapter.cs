using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Diagnostics;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Tests.Features.LostFooting;

/// <summary>A hand-driven agent list for the Lost Footing service and behavior tests.</summary>
internal sealed class FakeLostFootingAdapter : ILostFootingAdapter
{
    public List<AgentProbe> Agents { get; } = new List<AgentProbe>();

    public SceneFacts Scene { get; set; } = new SceneFacts { SceneId = "test_scene", MissionType = "field", WaterLevel = 5f };

    public int SceneReads { get; private set; }

    public List<(int Start, int Take)> Slices { get; } = new List<(int, int)>();

    public List<object> Confirmed { get; } = new List<object>();

    public OffMeshDetail Detail { get; set; } = new OffMeshDetail { Confirmed = true, HasSnap = true, SnapDistance = 2f, SnapHasNavMesh = true };

    public Exception? ThrowOnRead { get; set; }

    public int AgentCount => Agents.Count;

    public SceneFacts ReadScene()
    {
        SceneReads++;
        return Scene;
    }

    public void ReadSlice(int start, int take, List<AgentProbe> into)
    {
        Slices.Add((start, take));
        if (ThrowOnRead != null) throw ThrowOnRead;
        into.AddRange(Agents.Skip(start).Take(take));
    }

    public OffMeshDetail Confirm(object handle)
    {
        Confirmed.Add(handle);
        return Detail;
    }
}

/// <summary>A clock that advances a fixed step on every read, so a timed section has a known length.</summary>
internal sealed class SteppingStampClock : IStampClock
{
    private long _now;

    public SteppingStampClock(long stepTicks) => Step = stepTicks;

    public long Step { get; set; }

    public long Now
    {
        get
        {
            var value = _now;
            _now += Step;
            return value;
        }
    }

    /// <summary>1000 ticks a second, so a tick is a millisecond.</summary>
    public long Frequency => 1000;
}

/// <summary>Records each rescue try and answers with a set result (#894 phase 2).</summary>
internal sealed class FakeLostFootingRescueAdapter : ILostFootingRescueAdapter
{
    public List<(object Handle, string Move, float Meters, float Step, bool NeedLand)> Calls { get; } = new List<(object, string, float, float, bool)>();

    public RescueResult Near { get; set; } = new RescueResult { Moved = true, Meters = 1f, ToX = 0f, ToY = 0f, ToZ = 0f };

    public RescueResult Formation { get; set; } = new RescueResult { Moved = true, Meters = 20f, ToX = 0f, ToY = 0f, ToZ = 0f };

    public RescueResult MoveNear(object handle, float maxMeters, float maxStep, bool needLand)
    {
        Calls.Add((handle, "near", maxMeters, maxStep, needLand));
        return Near;
    }

    public RescueResult MoveToFormation(object handle, float minPlayerMeters, bool needLand)
    {
        Calls.Add((handle, "formation", minPlayerMeters, float.NaN, needLand));
        return Formation;
    }
}
