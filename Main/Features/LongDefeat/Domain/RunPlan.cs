using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>A garrison row: <c>Count</c> soldiers of one troop in one formation at the start of a run.</summary>
internal sealed record GarrisonRow(string TroopId, FormationSlot Slot, int Count);

/// <summary>What a leaderboard reads to judge a run's report (#1080): the build, the hash of the balance files, the active modules with their versions, and cheat mode at launch.</summary>
internal sealed record RunProvenance(string Build, string Balance, IReadOnlyList<string> Mods, bool CheatMode)
{
    public static RunProvenance Unknown { get; } = new("", "", new string[0], false);
}

/// <summary>What the <c>SurvivalRun</c> constructor takes: one fortress's garrison, its waves and shop, and the rules and options.</summary>
internal sealed record RunPlan(
    string FortressId,
    IReadOnlyList<GarrisonRow> Garrison,
    WavePlan Waves,
    ShopPlan Shop,
    RunRules Rules,
    RunOptions Options)
{
    /// <summary>Gold paid for each attacker troop that dies, by troop id. A troop missing from it pays nothing.</summary>
    public IReadOnlyDictionary<string, int> Bounties { get; init; } = new Dictionary<string, int>();

    /// <summary>The recorded keep point of the fortress, or null: the director then uses the scene's reinforcement frame and <see cref="RunRules.KeepRadius"/>.</summary>
    public KeepPoint? Keep { get; init; }

    /// <summary>The recorded muster places of the fortress's defenders, or null: every defender then spawns at the scene's own frames.</summary>
    public MusterPoints? Muster { get; init; }

    /// <summary>The recorded sally point of the fortress, or null: a mounted class then rides from the start with no sally (phase2-plan.md, "The sally").</summary>
    public MusterFrame? Sally { get; init; }

    public RunProvenance Provenance { get; init; } = RunProvenance.Unknown;

    /// <summary>The affix schedule, numbers and the fortress's attacker culture names. <see cref="AffixPlan.None"/>: no wave carries an affix.</summary>
    public AffixPlan Affixes { get; init; } = AffixPlan.None;

    /// <summary>The boons a pick can offer. <see cref="BoonPlan.None"/>: every pick lapses.</summary>
    public BoonPlan Boons { get; init; } = BoonPlan.None;

    /// <summary>
    /// Each defender troop's group, from its character (<see cref="BoonTargets.OfCharacter"/>): the launcher reads it from the engine's troop objects, because the domain knows no character.
    /// A troop missing from it falls back on its formation slot in <see cref="GroupOf"/>.
    /// </summary>
    public IReadOnlyDictionary<string, BoonTarget> TroopGroups { get; init; } = new Dictionary<string, BoonTarget>();

    /// <summary>The raw <c>{=KEY}English</c> name of the class the lord plays, or empty when he plays none: what the HUD strip and the end panel show.</summary>
    public string ClassName { get; init; } = "";

    /// <summary>The group of a defender troop: its entry in <see cref="TroopGroups"/>, else archers for the ranged slot and infantry for the other. A rider bought on foot is cavalry (assumption A8).</summary>
    public BoonTarget GroupOf(string troopId, FormationSlot slot) =>
        TroopGroups.TryGetValue(troopId, out var group) ? group : slot == FormationSlot.Ranged ? BoonTarget.Archers : BoonTarget.Infantry;

    /// <summary>The fortification kinds and levels this build can deliver (D31). Empty in the shipped game until each slice lands its effect; a test sets what it needs.</summary>
    public FortOffer Offered { get; init; } = FortOffer.Offered;

    /// <summary>The recorded high towers of the fortress; empty when none is recorded, and the towers row is then hidden.</summary>
    public IReadOnlyList<LdTower> Towers { get; init; } = System.Array.Empty<LdTower>();

    /// <summary>The recorded origins of the Elven Volley, or null: the volley row is then hidden.</summary>
    public LdVolleyOrigins? Volley { get; init; }

    /// <summary>The kit the lord spawns with (at the start and at every respawn), or null: the lord then plays in his own gear and no class is reported.</summary>
    public LdLoadout? Loadout { get; init; }

    /// <summary>The shop's armoury (its reach and gear sets), or null: no armoury row (phase4-plan.md section 5). Read from <see cref="Shop"/>, the one copy, as <see cref="ShopPlan.Fort"/> is.</summary>
    public ArmouryPlan? Armoury => Shop.Locations?.Armoury;

    /// <summary>The shop's engineer's yard (its reach and engine kinds), or null: no yard row.</summary>
    public YardPlan? Yard => Shop.Locations?.Yard;

    /// <summary>The shop's Marshal (his troop and price), or null: no Marshal row.</summary>
    public MarshalPlan? Marshal => Shop.Locations?.Marshal;

    /// <summary>The recorded armoury place, or null. Nothing reads it yet; a later slice hides the armoury when it is null.</summary>
    public MusterFrame? ArmouryPoint { get; init; }

    /// <summary>The recorded engineer's yard place, or null. Nothing reads it yet; a later slice hides the yard when it is null.</summary>
    public MusterFrame? YardPoint { get; init; }

    /// <summary>The places this build can deliver (D46). Nothing in Slice 1, so every row of shop.json stays hidden; a test sets what it needs.</summary>
    public LocationOffer Locations { get; init; } = LocationOffer.Offered;

    /// <summary>The ids of gear sets with an item the spawn would refuse, found once at launch (<see cref="LongDefeatGearItems"/>). Nothing reads it yet; a later slice hides those sets for the run.</summary>
    public IReadOnlyList<string> HiddenGearSets { get; init; } = System.Array.Empty<string>();
}
