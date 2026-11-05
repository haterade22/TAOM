using System;
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

    /// <summary>The affixes the running wave carries; empty between waves.</summary>
    public IReadOnlyList<AffixLabel> WaveAffixes { get; init; } = Array.Empty<AffixLabel>();

    /// <summary>The affixes the next wave will carry, rolled from the seed: what the intermission's line names.</summary>
    public IReadOnlyList<AffixLabel> NextAffixes { get; init; } = Array.Empty<AffixLabel>();

    /// <summary>The next wave's attacker health and damage factors (1 inside the authored list), for the intermission's line.</summary>
    public float NextHealthFactor { get; init; } = 1f;

    public float NextDamageFactor { get; init; } = 1f;

    /// <summary>The garrison cap in force, boons included: a pack that would carry the garrison past it is refused by <c>TryBuy</c>.</summary>
    public int GarrisonCap { get; init; } = int.MaxValue;

    /// <summary>The cards of the open boon pick, or none. An unmade pick stays open into later intermissions, but is taken between waves only.</summary>
    public IReadOnlyList<BoonCard> BoonOffer { get; init; } = Array.Empty<BoonCard>();

    /// <summary>Picks owed that wait behind the open one.</summary>
    public int OwedPicks { get; init; }

    /// <summary>Rerolls banked, and rerolls used so far this run.</summary>
    public int Rerolls { get; init; }

    public int RerollsUsed { get; init; }

    /// <summary>The boons taken so far, in order.</summary>
    public IReadOnlyList<BoonCard> Boons { get; init; } = Array.Empty<BoonCard>();
}
