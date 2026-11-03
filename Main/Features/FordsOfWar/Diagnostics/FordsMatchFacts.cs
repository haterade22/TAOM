using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>The match numbers the verdict rules compare against, read once from the config and the layout. <c>ContactMetres</c> is how near a tower's origin a melee unit counts as in contact with it.</summary>
internal sealed record FordsMatchFacts(float FireRange, float AttackRange, float CaptureRadius, int Lanes, int MaxLivePerSide, int MaxAlivePerSide,
    float BaseDistance = 0f, int AgentLimit = 0, float ContactMetres = FordsTowerGeometry.BarricadeContactMetres, int CapBonusWest = 0, int CapBonusEast = 0,
    float RangeFactorWest = 1f, float RangeFactorEast = 1f)
{
    /// <summary>How far the towers of <paramref name="owner"/> fire.</summary>
    public float FireRangeOf(MatchSide owner) => FireRange * (owner == MatchSide.West ? RangeFactorWest : RangeFactorEast);
}
