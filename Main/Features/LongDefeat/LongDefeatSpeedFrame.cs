using System;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// What the Long Defeat mission's clock does this frame: the Fords rule (<see cref="FordsGameSpeed"/>) at every speed but one. At 10x with a person driving a live
/// main agent it is a fixed step of the frame's time times 10, capped at 0.1 s, instead of the engine's fast forward: native IMBMission.Tick calls Mission_OnPreTick on
/// every call, so fast forward would read one frame's key presses once per step (nine extra steps a frame) and a tap would repeat. A dead player or an AI-driven main agent
/// keeps fast forward. A fixed step reaches a true 10x only at 100 frames a second or more. Pure, so the four cases are testable without the engine.
/// </summary>
internal static class LongDefeatSpeedFrame
{
    private const float MaxStepSeconds = 0.1f;
    private const int FastForwardLevel = 10;

    internal static (bool FastForward, float FixedStep) For(int speed, float realDt, bool personDriving)
    {
        if (speed == FastForwardLevel && personDriving)
            return (false, realDt > 0f ? Math.Min(realDt * speed, MaxStepSeconds) : 0f);

        var rule = new FordsGameSpeed();
        rule.Set(speed);
        return rule.Frame(realDt);
    }
}
