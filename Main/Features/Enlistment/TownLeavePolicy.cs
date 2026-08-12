using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Shore leave: may an enlisted soldier walk the town his column is resting in, and when is that
/// pass revoked? (Field report 1: "you can't enter towns when your lord does, which causes you not
/// to be able to buy anything".)
///
/// WHAT WAS ACTUALLY BROKEN. TAOM already puts the player genuinely inside the settlement —
/// <c>EnterSettlementAction.ApplyForParty</c> runs on the follow path. What it does NOT do is let him
/// see it: <c>"town"</c>, <c>"castle"</c> and <c>"village"</c> sit in <c>RedirectMenuIds</c>, so the
/// settlement menu is swallowed and replaced by the service wait menu. He is standing in the market
/// and cannot reach it. The config's own comment called this out as "a separate feature needing its
/// own state" — this is that state.
///
/// WE DO NOT COPY SERVEASSOLDIER HERE, and this is the one place its design is actively worse.
/// SAS force-evicts the player from any settlement every tick (<c>Test.cs:2424-2440</c>) and
/// substitutes a free gear-picker conversation for shopping; its nine "return to army camp" options
/// are exit ramps, not entry points. That trades a real town for a menu. TAOM already has the player
/// inside the real one, so the fix is to stop hiding it.
///
/// BOUND TO THE COLUMN, not to the player's wishes. The pass exists only while the commander is
/// resting in that same settlement, and dies the moment he leaves — a soldier does not get to stay
/// behind shopping while his company marches off, which would strand him out of position and is the
/// desertion the feature deliberately makes a separate, deliberate act.
/// </summary>
public static class TownLeavePolicy
{
    /// <summary>
    /// May the player be OFFERED leave right now? Attached service only, and only while he is
    /// actually inside a settlement.
    ///
    /// His OWN presence is the test, not the commander's, and they are the same fact: the attach
    /// path walks the player into the settlement behind his commander, so being inside one while
    /// attached IS the column at rest. It is also the fact that can be read directly, off
    /// <c>PlayerPresenceFlags.IsInSettlement</c>, rather than inferred through a second lookup.
    /// On open ground there is nothing to take leave into.
    /// </summary>
    public static bool CanTakeLeave(EnlistmentState state, bool insideSettlement, bool alreadyOnLeave)
        => state == EnlistmentState.EnlistedAttached && insideSettlement && !alreadyOnLeave;

    /// <summary>
    /// Must an active pass be revoked? True as soon as the column is no longer at rest in a
    /// settlement, or the player is no longer plainly attached.
    ///
    /// Deliberately phrased as "revoke unless still valid" rather than "revoke when the commander
    /// leaves": every state that is not attached-and-in-a-settlement — battle, detachment, discharge,
    /// a commander taken prisoner — must end the pass, and enumerating those was how the original
    /// redirect list grew holes.
    /// </summary>
    public static bool ShouldRevokeLeave(EnlistmentState state, bool insideSettlement, bool onLeave)
        => onLeave && !(state == EnlistmentState.EnlistedAttached && insideSettlement);
}
