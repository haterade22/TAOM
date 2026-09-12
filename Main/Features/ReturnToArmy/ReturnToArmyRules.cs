namespace TAOM.Features.ReturnToArmy;

/// <summary>
/// The whole policy behind Patch87, pure and free of engine types so it can be tested without a
/// campaign. Answers one question for vanilla's "Return to Army" consequence: run vanilla, or leave
/// the settlement instead?
///
/// Vanilla is right whenever the army is genuinely here. A member merged into the army
/// (<c>AttachedTo != null</c>) waits with it and leaves when the leader leaves. It is also right for
/// a village, where its own body already leaves. It is a dead end for an unattached member in a
/// town or castle (#566), and that is the one row this returns <see cref="Verdict.LeaveSettlement"/>
/// for. The first two rows are unreachable in play (the option's own condition hides it for anyone
/// not in an army, and for the army's leader) and are pinned so the prefix can never act on a party
/// the option was never offered to.
/// </summary>
public static class ReturnToArmyRules
{
    public enum Verdict
    {
        /// <summary>Vanilla's own consequence runs untouched.</summary>
        RunVanilla,

        /// <summary>Leave the settlement the way vanilla's "Leave" does, and skip vanilla.</summary>
        LeaveSettlement,
    }

    public static Verdict Decide(bool inArmy, bool isArmyLeader, bool attachedToArmy, bool inVillage)
    {
        if (!inArmy || isArmyLeader)
            return Verdict.RunVanilla;

        if (attachedToArmy)
            return Verdict.RunVanilla;

        if (inVillage)
            return Verdict.RunVanilla;

        return Verdict.LeaveSettlement;
    }
}
