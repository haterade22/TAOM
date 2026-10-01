namespace TAOM.Features.BattleCorpses;

/// <summary>The fade time and corpse cap one battle runs with. -1 in both is native's own behaviour.</summary>
/// <param name="FadeSeconds">For <c>Mission.SetMissionCorpseFadeOutTimeInSeconds</c>.</param>
/// <param name="CorpseCap">For <c>Mission.SetOverrideCorpseCount</c>.</param>
public readonly record struct BattleCorpseLimits(bool IsOverridden, float FadeSeconds, int CorpseCap)
{
    public static readonly BattleCorpseLimits EngineDefaults = new(false, -1f, -1);
}
