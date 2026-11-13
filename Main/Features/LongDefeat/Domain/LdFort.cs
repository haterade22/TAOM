using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// One visible gate as the adapter reads it (read each tick, plain numbers and flags): the damage-state index it stores, its hit points, its maximum, the vanilla maximum read at boot before any hit,
/// and whether it is destroyed or open.
/// </summary>
internal readonly record struct LdGateFacts(int StateIndex, float HitPoints, float MaxHitPoints, float VanillaMax, bool Destroyed, bool Open)
{
    /// <summary>The share of the maximum that is gone: 1 for a destroyed gate, 0 for a whole one, and 0 for facts that are not numbers.</summary>
    public float MissingShare
    {
        get
        {
            if (Destroyed)
                return 1f;
            if (!(MaxHitPoints > 0f) || float.IsInfinity(MaxHitPoints) || float.IsNaN(HitPoints))
                return 0f;
            return Math.Max(0f, Math.Min(1f, 1f - HitPoints / MaxHitPoints));
        }
    }
}

/// <summary>What a fortification buy answers. <c>NotOffered</c> is a kind or level this build cannot deliver yet (<see cref="FortOffer"/>), or field care in the preparation, when nobody is hurt; <c>Hidden</c> a row the fortress or shop does not carry.</summary>
internal enum FortResult
{
    Ok,
    NotOpen,
    NotOffered,
    NoGold,
    GarrisonFull,
    MaxLevel,
    NothingToRepair,
    Used,
    Hidden,
    RunOver,
}

internal enum PurchaseKind
{
    Pack,
    GateLevel,
    GateRepair,
    TowerLevel,
    FieldCare,
    VolleyLevel,
    Upgrade,
}

/// <summary>
/// One thing bought, in order. <c>BeforeWave</c> is the wave that starts next; <c>Id</c> is the offer of a pack, the step of an upgrade (#1161) and empty for the rest; <c>Level</c> is the level a
/// level kind reached and 0 for the rest; <c>BasePrice</c> is shop.json's price and <c>Price</c> what was paid after the boons' price factor (D29), which an upgrade does not take: its price is
/// <c>BasePrice</c> times <see cref="Count"/>, the soldiers the field replaced (0 for every other kind); <c>Share</c> is the missing share a repair mended and the share of the missing health care
/// returned, 0 for the rest.
/// </summary>
internal sealed record FortPurchase(int BeforeWave, PurchaseKind Kind, string Id, int Level, int BasePrice, int Price, float Share, IReadOnlyList<float>? Shares = null)
{
    public int Count { get; init; }
}

/// <summary>What the report lists of the fortifications: every purchase in order, and the levels owned at the end.</summary>
internal sealed record RunFort(IReadOnlyList<FortPurchase> Purchases, int GateLevel, int TowerLevel, int VolleyLevel)
{
    public static RunFort None { get; } = new(Array.Empty<FortPurchase>(), 0, 0, 0);
}

/// <summary>
/// The posts to man for the high towers, as the director will hand the adapter: <c>Posts</c> per tower, up to <c>Room</c> soldiers more under the garrison cap. The refill is free and comes before any pack. <c>HealthFactor</c> is the health boons' factor for the troop's group, as a pack of that troop would carry it.
/// </summary>
internal sealed record LdPostFill(string Troop, int Posts, int Room, float HealthFactor = 1f);

/// <summary>A wave's gate row: the level in force at its start, the hit points the gates held then and could hold, whether one fell in the wave and when. Sums over the visible gates (Helm's Deep has one).</summary>
internal sealed record GateWaveRecord(int Level, float StartHitPoints, float MaxHitPoints, bool Fell, float? FellAtSeconds)
{
    public static GateWaveRecord None { get; } = new(0, 0f, 0f, false, null);
}

/// <summary>A wave's post counts: filled at its start, left short by the cap, manned when it began, lost in it. Written at 0 until the posts exist (Slice 7).</summary>
internal sealed record PostsWaveRecord(int Filled, int Short, int Manned, int Lost)
{
    public static PostsWaveRecord None { get; } = new(0, 0, 0, 0);
}

/// <summary>A wave's volley counts: uses, arrows loosed, hits, kills and uses withheld. Written at 0 until the volleys exist (Slices 3, 6 and 8).</summary>
internal sealed record VolleyWaveRecord(int Uses, int Arrows, int Hits, int Kills, int Withheld)
{
    public static VolleyWaveRecord None { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>An attacker a volley may be aimed at, as the adapter reads him: the registry key, the chest point (the engine's <c>GetChestGlobalPosition</c>) and his velocity, which is horizontal (z is 0).</summary>
internal readonly record struct LdVolleyTarget(AgentKey Key, Vec3f Chest, Vec3f Velocity);

/// <summary>One arrow of a volley: where it leaves, the unit direction, the speed it is added with and the attacker it is aimed at.</summary>
internal readonly record struct LdVolleyShot(Vec3f Origin, Vec3f Direction, float Speed, AgentKey Target);

/// <summary>
/// What <see cref="SurvivalRun.ArmVolley"/> answers: <c>Armed</c> (the director fires it on the next tick), or why not. <c>NotSold</c> is a build or fortress that sells no Elven Volley (the offer is zero or no
/// origin is recorded), so the shop shows no row and a press of the key says nothing; <c>NotOwned</c> is a volley that is for sale and not yet bought.
/// </summary>
internal enum VolleyArm
{
    Armed,
    NotSold,
    NotOwned,
    Cooldown,
    NotInWave,
    RunOver,
}

/// <summary>Why the director could not fire an armed volley: no attacker in reach, no usable origin, no standing defender to own the arrows, or an arrow item the game did not load. The volley stays ready (D30).</summary>
internal enum VolleyWithheldReason
{
    NoTargets,
    NoOrigin,
    NoOwner,
    NoItem,
}

/// <summary>The arrow a volley looses as the field found it: the speed it is added with (the item's own missile speed), which is all the aim needs (<see cref="VolleyAim.Plan"/>). The item's thrust and reach are logged once by the field.</summary>
internal sealed record LdVolleyItem(float Speed);

/// <summary>A line in the scene from a point along a unit direction: the lord's eye ray.</summary>
internal readonly record struct LdVolleyRay(Vec3f Origin, Vec3f Direction);

/// <summary>
/// What the field did with a volley. <c>Withheld</c> is set when nothing was added (no owner, no item); <c>Added</c> counts the missiles added, <c>AgentsBefore</c> and <c>AgentsAfter</c> the mission's agents
/// around the calls (a missile is no agent, so they must be equal), <c>OwnerTroop</c> and <c>OwnerMultiplier</c> the owner's troop id and his weapon damage multiplier (D25), and <c>Missiles</c> the index of each
/// missile the engine added, so a hit can be matched to the use that loosed it.
/// </summary>
internal sealed record LdVolleyFire(VolleyWithheldReason? Withheld, int Added, int AgentsBefore, int AgentsAfter, string OwnerTroop, float OwnerMultiplier, IReadOnlyList<int>? Missiles = null)
{
    public static LdVolleyFire Refused(VolleyWithheldReason why) => new(why, 0, 0, 0, string.Empty, 0f);
}

/// <summary>What an arrow's hit was, by the victim's side and the engine's own flags (A24): a foe struck, a foe's shield block, a friend struck, or a friendly hit the engine cancelled.</summary>
internal enum LdVolleyHitKind
{
    Foe,
    Blocked,
    Friendly,
    Cancelled,
}

/// <summary>
/// One missile hit of a volley item, as the logic's filter reads it: the item id, its kind, whether a foe fell (the victim under 1 health after the blow, E16), whether the engine's affector was null or stood on the
/// wrong side (A24, counted and never judged), and the numbers the engine gave the hit (A25). <c>Missile</c> is the index of the missile that struck (<c>AffectorWeaponSlotOrMissileIndex</c>, E11), which names the
/// use it belongs to when several towers' windows are open at once (their arrows share one item). The engine looks the striking missile up by this same key and throws when it is unknown (<c>Mission.cs:5778</c>),
/// so the hooks always carry one; -1 is for a test's hit, and the director drops a hit whose missile no open window loosed.
/// </summary>
internal readonly record struct LdVolleyHit(string ItemId, LdVolleyHitKind Kind, bool Kill, bool AffectorNull, bool AffectorSideWrong, float StartingBaseSpeed, float ImpactSpeed, int Missile = -1);

/// <summary>A defender who might own a volley's arrows, as the adapter reads him: still the live occupant of his slot, AI-controlled, the main agent or not, an archer, and how far he stands from the nearest attacker.</summary>
internal readonly record struct LdOwnerCandidate(bool Live, bool AiControlled, bool IsMain, bool Archer, float NearestAttacker);

/// <summary>
/// Who owns a volley's arrows (phase3-plan.md D22). Eligible: live, AI-controlled and not the main agent, because a kill needs a real affector (E17) and a friendly hit is cancelled only for an AI shooter (E14).
/// Archers first and, among the chosen kind, the one farthest from the nearest attacker (an owner who dies while his arrow flies is an UNSETTLED native case, A24). A distance that is not a number ranks last.
/// </summary>
internal static class VolleyOwnerRule
{
    /// <summary>The index of the chosen candidate, or -1 when none is eligible. A tie goes to the earlier.</summary>
    public static int Choose(IReadOnlyList<LdOwnerCandidate> candidates)
    {
        var best = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (!c.Live || !c.AiControlled || c.IsMain)
                continue;
            if (best < 0 || Beats(c, candidates[best]))
                best = i;
        }

        return best;
    }

    private static bool Beats(LdOwnerCandidate a, LdOwnerCandidate b)
    {
        if (a.Archer != b.Archer)
            return a.Archer;

        return Far(a) > Far(b);
    }

    private static float Far(LdOwnerCandidate c) => float.IsNaN(c.NearestAttacker) ? float.NegativeInfinity : c.NearestAttacker;
}

/// <summary>A volley's source: the Elven Volley (<c>elven</c>) or one tower's level 3 volley (<c>tower-&lt;id&gt;</c>), the key a check line and a report row name it by.</summary>
internal readonly record struct VolleySource(string Id)
{
    private const string TowerPrefix = "tower-";

    public static VolleySource Elven { get; } = new("elven");

    public static VolleySource Tower(string towerId) => new(TowerPrefix + towerId);

    public bool IsTower => Id != null && Id.StartsWith(TowerPrefix, StringComparison.Ordinal);

    /// <summary>The tower's id, or empty for the Elven Volley.</summary>
    public string TowerId => IsTower ? Id.Substring(TowerPrefix.Length) : string.Empty;
}

/// <summary>
/// A shot the run armed, as the director will fire it: from which source, the arrow item, the aim mode, how many arrows, the group radius, the reach, the recorded origins and the height an arrow leaves them
/// at. <c>Wave</c> and <c>Use</c> (the number of the use the wave has fired plus one, so a withheld shot asks again under the same number) name its check line.
/// </summary>
internal sealed record VolleyOrder(VolleySource Source, string ItemId, VolleyAimMode Aim, int Arrows, float Radius, float Range, float OriginHeight, IReadOnlyList<LdPoint> Origins, int Wave, int Use);

/// <summary>One volley source as the HUD and the bot read it: whether the next shot may be armed now, and the seconds to the next use by cooldown.</summary>
internal sealed record VolleySourceState(VolleySource Source, bool Ready, float SecondsLeft);

/// <summary>
/// The volleys as the HUD and the bot read them. The Elven Volley: <c>Owned</c> from its first level, <c>Ready</c> only in a wave with no cooldown, no armed shot waiting and no withheld shot holding the next ask,
/// <c>SecondsLeft</c> on the cooldown, and the reason of the last shot the director could not fire with how long ago (wave seconds), until a shot fires or the wave ends. <c>Towers</c> lists each tower volley running.
/// </summary>
internal sealed record VolleyView(bool Owned, bool Ready, float SecondsLeft, VolleyWithheldReason? Withheld, float WithheldSecondsAgo, IReadOnlyList<VolleySourceState> Towers)
{
    public static VolleyView None { get; } = new(false, false, 0f, null, 0f, Array.Empty<VolleySourceState>());
}

/// <summary>The volley counts of the wave a run ended in, which has no row in the report (a row is written at a clear): what the director's end-of-run check line reads.</summary>
internal sealed record VolleyFlush(int Wave, VolleyWaveRecord Elven, VolleyWaveRecord Tower);

/// <summary>What a cleared wave adds to its report row about the fortifications.</summary>
internal sealed record FortWave(GateWaveRecord Gate, PostsWaveRecord Posts, VolleyWaveRecord Volley, VolleyWaveRecord TowerVolley)
{
    public static FortWave None { get; } = new(GateWaveRecord.None, PostsWaveRecord.None, VolleyWaveRecord.None, VolleyWaveRecord.None);
}
