using System;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The match speed the commander chose. 1x leaves the engine's clock alone. 2x and 4x give the mission a longer fixed step each frame, capped so that a slow frame slows the match
/// and one step never jumps too far. 10x is the engine's own fast forward (MissionState.TickMission: nine times the frame's time in steps of at most 0.1 s, then the frame itself),
/// which vanilla offers on the scoreboard after the player falls.
/// </summary>
internal sealed class FordsGameSpeed
{
    private const float MaxStepSeconds = 0.1f;
    private const int FastForwardLevel = 10;

    private static readonly int[] Levels = { 1, 2, 4, FastForwardLevel };

    public int Current { get; private set; } = 1;

    public static bool IsLevel(int speed) => Array.IndexOf(Levels, speed) >= 0;

    public void Cycle() => Current = Levels[(Array.IndexOf(Levels, Current) + 1) % Levels.Length];

    public void Set(int speed)
    {
        if (IsLevel(speed))
            Current = speed;
    }

    /// <summary>What the mission's clock does this frame: the engine's fast forward, or a fixed step in place of the frame's own time (0 for none).</summary>
    public (bool FastForward, float FixedStep) Frame(float realDt)
    {
        if (Current == FastForwardLevel)
            return (true, 0f);

        return Current > 1 && realDt > 0f ? (false, Math.Min(realDt * Current, MaxStepSeconds)) : (false, 0f);
    }
}
