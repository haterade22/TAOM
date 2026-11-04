using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What the HUD and the shop bot read: an immutable copy of the run's state, never the run itself.</summary>
internal sealed record RunView(
    RunPhase Phase,
    int Wave,
    int WavesCleared,
    int Gold,
    float SecondsLeft,
    int DefendersStanding,
    int DefendersPending,
    int AttackersStanding,
    float KeepProgress,
    IReadOnlyList<ShopOffer> Offers,
    int FootBought = 0,
    int RangedBought = 0)
{
    public bool ShopOpen => Phase == RunPhase.Preparation || Phase == RunPhase.Intermission;
}
