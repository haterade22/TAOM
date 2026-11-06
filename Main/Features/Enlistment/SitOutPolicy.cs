using TAOM.Adapters;

namespace TAOM.Features.Enlistment;

/// <summary>
/// What the encounter menu's 'Send troops' / 'Leave it to the others' option does for an enlisted
/// soldier who is not leading his side (#830). Only <see cref="RefusedField"/>
/// and <see cref="RefusedSiege"/> disable the option.
/// </summary>
public enum SitOutVerdict
{
    /// <summary>No opinion: gate closed, or a field the decision needs could not be read. Vanilla stands.</summary>
    Unchanged = 0,

    /// <summary>The soldier is wounded. The option stays enabled; the payout keeps full base XP and withholds renown.</summary>
    WoundedFree,

    /// <summary>Vanilla's Attack is unavailable, so this option is the only way forward and stays enabled.</summary>
    ForcedOpen,

    /// <summary>His side holds the field by a wide margin. The option stays enabled; it costs trust.</summary>
    OverwhelmingCost,

    /// <summary>A field battle that is not lopsided: the option is disabled, he must fight.</summary>
    RefusedField,

    /// <summary>A siege assault or a sally-out (the lord's hall phase is part of the assault): the option is disabled.</summary>
    RefusedSiege,
}

/// <summary>
/// The pure sit-out decision. Rules, in order:
/// wounded stays free; an unavailable Attack forces this option open; a siege assault or sally-out
/// refuses; any other battle is refused unless the player's side holds at least the threshold share.
/// Invariant: every Refused verdict implies the Attack option is available, so the player never loses
/// his last enabled way out of an open encounter.
/// </summary>
public static class SitOutPolicy
{
    public static SitOutVerdict Decide(SitOutSnapshot s, float threshold)
    {
        if (s.Wounded == null)
            return SitOutVerdict.Unchanged;
        if (s.Wounded.Value)
            return SitOutVerdict.WoundedFree;

        if (s.AttackAvailable == null)
            return SitOutVerdict.Unchanged;
        if (!s.AttackAvailable.Value)
            return SitOutVerdict.ForcedOpen;

        if (s.IsSiege == null)
            return SitOutVerdict.Unchanged;
        if (s.IsSiege.Value)
            return SitOutVerdict.RefusedSiege;

        if (s.PlayerSideShare == null)
            return SitOutVerdict.Unchanged;

        // A positive requirement, so NaN (from either side) falls through to the refusal.
        var share = s.PlayerSideShare.Value;
        if (share >= threshold && share <= 1f)
            return SitOutVerdict.OverwhelmingCost;

        return SitOutVerdict.RefusedField;
    }
}
