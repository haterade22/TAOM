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

    /// <summary>The raw <c>{=KEY}English</c> name of the lord's class, or empty when he plays none: the HUD strip shows it after the wave number.</summary>
    public string ClassName { get; init; } = "";

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

    /// <summary>The running wave's commander, or null between waves and in a wave with none.</summary>
    public CommanderView? Commander { get; init; }

    /// <summary>The fortification rows: what each one costs and whether it can be bought now, the gates as last read, and whether the unattended bot buys them before packs.</summary>
    public FortView Fort { get; init; } = FortView.None;

    /// <summary>The Elven Volley and the towers' volleys: what the HUD shows in a wave and the bot reads to fire.</summary>
    public VolleyView Volleys { get; init; } = VolleyView.None;
}

/// <summary>
/// One fortification row as the HUD and the bot read it. <c>Level</c> and <c>MaxLevel</c> count levels (for field care, buys used this intermission and the most one allows); <c>Price</c> is what the next
/// buy costs after the price factor; <c>Available</c> is true when the buy would pass every test except the gold (offered, carried, below the top level, not used up, the posts under the cap). <c>Full</c> is
/// true when only that last test fails: the row is for sale and the garrison cap holds it back, so the panel keeps it listed (disabled) instead of letting it vanish and return as soldiers fall.
/// </summary>
internal sealed record FortRow(int Level, int MaxLevel, int Price, bool Available)
{
    public bool Full { get; init; }

    public static FortRow None { get; } = new(0, 0, 0, false);
}

/// <summary>The fortification rows. <c>Repair</c> is the gate row's other buy: its <c>Price</c> is the total over the visible gates. <c>BuysFirst</c> is a test run with a <c>start_gold</c>: the bot buys these before packs (D27).</summary>
internal sealed record FortView(FortRow Gate, FortRow Repair, FortRow Towers, FortRow Care, FortRow Volley, IReadOnlyList<LdGateFacts> Gates, bool BuysFirst)
{
    public static FortView None { get; } = new(FortRow.None, FortRow.None, FortRow.None, FortRow.None, FortRow.None, Array.Empty<LdGateFacts>(), false);

    /// <summary>The share of the missing health one field care gives back (shop.json's), 0 when the shop carries no care row: the panel's line names it.</summary>
    public float CareShare { get; init; }

    /// <summary>The hit points the next gate level brings the gate to, as a percent over the vanilla maximum (cumulative, shop.json's); the top level's when every level is owned, 0 when the shop carries no gate row.</summary>
    public float GatePercent { get; init; }

    /// <summary>The posts each high tower holds at the level the towers row describes: the next one to buy, or the top one offered when every level is owned. 0 when the shop carries no towers row.</summary>
    public int TowerPostsShown { get; init; }

    /// <summary>The Elven Volley level the row describes: the next one to buy, or the top one when every level is owned. Null when the shop carries no volley row.</summary>
    public VolleyLevelPlan? VolleyShown { get; init; }
}

/// <summary>One ability of the commander as the HUD shows it: its raw <c>{=KEY}English</c> name, the seconds to its next use and whether it is running now (a war cry).</summary>
internal sealed record CommanderAbilityView(string Name, float SecondsLeft, bool Running);

/// <summary>The commander as the HUD shows him: his rank's raw <c>{=KEY}English</c> name, how he stands, whether he has spawned, and his abilities.</summary>
internal sealed record CommanderView(string RankName, CommanderOutcome Outcome, bool Up, IReadOnlyList<CommanderAbilityView> Abilities);
