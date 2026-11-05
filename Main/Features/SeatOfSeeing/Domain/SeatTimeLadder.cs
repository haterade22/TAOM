using System;
using System.Globalization;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>The three keys of mission time: step up, step down, and stay (pause).</summary>
internal enum SeatTimeKey
{
    Quicken,
    Slow,
    Stay,
}

/// <summary>What the per-frame rule asks the screen adapter to do: nothing, write the fixed step again, stop fast time once, or give up the Seat's own slow request and fixed step to another writer's fast forward.</summary>
internal enum SeatTimeAction
{
    None,
    Apply,
    Stop,
    Adopt,
}

/// <summary>The stored level after a frame's read of the engine's fast forward flag, and what that frame owes.</summary>
internal readonly record struct SeatTimeFrame(int Index, SeatTimeAction Action);

/// <summary>The pieces of mission time the Seat can leave on: fast forward, the fixed step, the pause and its slow request.</summary>
internal readonly record struct SeatTimeState(bool FastForward, bool FixedStep, bool Paused, bool SlowRequest)
{
    /// <summary>Nothing of the Seat's is left on; a view that owns the clock writes fast forward and the fixed step itself every frame, so those two are not the Seat's then.</summary>
    internal bool IsClean(bool otherClock) => !Paused && !SlowRequest && (otherClock || (!FastForward && !FixedStep));
}

/// <summary>The six levels of mission time as an index, and the rules that move between them (build-spec section 10).</summary>
internal static class SeatTimeLadder
{
    /// <summary>The one id the Seat's slow request uses, clear of the engine's 813, 624, 864 and 1121.</summary>
    internal const int RequestId = 5303;

    internal const int Normal = 2;
    internal const int FourTimes = 4;
    internal const int Fastest = 5;

    /// <summary>Tries a teardown that left time on gets in all, the first one included.</summary>
    internal const int CancelTries = 3;

    private static readonly float[] Levels = { 0.25f, 0.5f, 1f, 2f, 4f, 10f };

    internal static float Level(int index) => Levels[index];

    internal static string Name(int index) => Levels[index].ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The whole number the Fords speed rule takes for a level of 2 or more.</summary>
    internal static int FastLevel(int index) => (int)Levels[index];

    internal static bool IsSlow(int index) => index < Normal;

    internal static bool UsesFixedStep(int index) => index > Normal && index < Fastest;

    internal static bool IsFastForward(int index) => index == Fastest;

    /// <summary>The highest level a key may reach: 10 needs the Seat up or no main agent, because fast forward repeats the mission tick, and another view that owns the clock holds the ladder at 1.</summary>
    internal static int Ceiling(bool seatUp, bool heroAlive, bool clockOwnedElsewhere)
    {
        if (clockOwnedElsewhere)
            return Normal;
        return seatUp || !heroAlive ? Fastest : FourTimes;
    }

    internal static int Step(int index, SeatTimeKey key, int ceiling) =>
        key == SeatTimeKey.Quicken ? Math.Min(index + 1, ceiling) : Math.Min(Math.Max(index - 1, 0), ceiling);

    /// <summary>
    /// The stored level after a frame's read of the engine's fast forward flag, and what the frame owes. A flag another writer raised makes the level 10
    /// and the Seat gives up what it holds: the engine multiplies the step and the request into the fast forward.
    /// </summary>
    internal static SeatTimeFrame Frame(int index, bool fastForwardFlag)
    {
        if (IsFastForward(index))
            return fastForwardFlag ? new SeatTimeFrame(index, SeatTimeAction.None) : new SeatTimeFrame(Normal, SeatTimeAction.Stop);
        if (fastForwardFlag)
            return new SeatTimeFrame(Fastest, SeatTimeAction.Adopt);
        return new SeatTimeFrame(index, UsesFixedStep(index) ? SeatTimeAction.Apply : SeatTimeAction.None);
    }

    /// <summary>A leave at level 10 with a living hero steps down to 4, because 10 needs the Seat up or no main agent.</summary>
    internal static int AfterLeave(int index, bool heroAlive) => index == Fastest && heroAlive ? FourTimes : index;

    /// <summary>Taking a soldier cancels fast time (the engine clears the flag itself, and the fixed step stops with it); slow time stays.</summary>
    internal static int WithoutFastTime(int index) => index > Normal ? Normal : index;
}
