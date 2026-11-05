namespace TAOM.Features.Messengers;

/// <summary>
/// Which way a messenger conversation opens (#898). Entering the target's settlement through <c>PlayerEncounter.EnterSettlement</c>
/// makes <c>EnterSettlementAction.ApplyForParty</c> attach a main party that sits in an army it does not lead, when that army's
/// leader is in the same settlement and the party is not attached yet (<c>Army.AddPartyToMergedParties</c> sets
/// <c>AttachedTo</c>). <c>PlayerEncounter.Finish</c> then skips <c>LeaveSettlement</c> for an attached party, and the player
/// stays in the town. In that state the conversation uses the field path instead.
/// </summary>
public static class MessengerEntryPolicy
{
    public static bool WouldAttachToArmy(bool hasArmy, bool leadsArmy, bool leaderInTargetSettlement, bool alreadyAttached) =>
        hasArmy && !leadsArmy && leaderInTargetSettlement && !alreadyAttached;
}
