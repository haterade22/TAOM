using TAOM.Adapters;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// The single answer to "may enlistment finish this PlayerEncounter?".
///
/// PURE by design — no adapters, no logger, no engine types — because this is the decision that,
/// when wrong, either destroys the player's own settlement visit or leaves them permanently unable
/// to interact with anything. It is the one piece of this feature that deserves exhaustive
/// table-driven tests, and it cannot have them if it reaches for the world.
/// </summary>
public interface IEncounterOwnershipPolicy
{
    EncounterFinishVerdict Evaluate(EncounterFinishIntent intent, EncounterOwnershipSnapshot snapshot);
}

public sealed class EncounterOwnershipPolicy : IEncounterOwnershipPolicy
{
    public EncounterFinishVerdict Evaluate(EncounterFinishIntent intent, EncounterOwnershipSnapshot snapshot)
    {
        // R0 — nothing live. Every intent agrees there is nothing to do.
        if (!snapshot.HasEncounter)
            return EncounterFinishVerdict.NothingToFinish;

        // R1 — the player is in their OWN map event. No intent may tear down a battle the player
        // is fighting; that is their game, not our bookkeeping. Universal, checked before intent.
        if (snapshot.PlayerInMapEvent)
            return EncounterFinishVerdict.DeferPlayerOwnBattle;

        // R1b — the same protection, one moment later, and R1 alone does not give it.
        // `MapEventSide.Clear()` nulls `MainParty.MapEvent` BEFORE the encounter closes, so the loot
        // and aftermath menus run inside a still-open encounter that reads as "no battle anywhere".
        // R1 has already passed by then. Finishing in that window tears down the player's own loot
        // screen and forces `TimeControlMode.Stop` + `GameMenu.ExitToLast()`.
        //
        // Universal on purpose, discharge included: handing the player back interactable is urgent,
        // but not more urgent than not deleting the battle result they are reading. The window is
        // short and every caller retries.
        //
        // EnlistmentReconciler's `noBattleAnywhere` and ServiceBattleService.OnCommanderBattleEnded
        // already encode this belief by treating an open encounter as "battle still live". This is
        // that belief moved into the one place that is supposed to own the decision.
        if (snapshot.IsBattleEncounter)
            return EncounterFinishVerdict.DeferPlayerOwnBattle;

        // Discharge outranks the remaining rules: service is ending and the player must come back
        // interactable. Leaving an encounter live here is the save-breaker — EncounterManager
        // refuses every main-party encounter while PlayerEncounter.Current is set. A conversation
        // still blocks it, because finishing mid-dialogue drops the player out of their own
        // conversation and the hourly sweep will clear it moments later anyway.
        if (intent == EncounterFinishIntent.Discharge)
        {
            return snapshot.ConversationInProgress
                ? EncounterFinishVerdict.SkipConversationInProgress
                : EncounterFinishVerdict.Finish;
        }

        // R2 — never finish under a running conversation.
        if (snapshot.ConversationInProgress)
            return EncounterFinishVerdict.SkipConversationInProgress;

        // R2b — shore leave INVERTS R3, and only R3. The settlement-shaped encounter is the one
        // TakeTownLeave opened to make the vanilla town menu safe, so ending the pass is the one
        // moment it is ours to close. A PARTY encounter under this intent is still someone else's
        // (a lord the player is talking to, a battle being seeded), so it falls through to R4.
        //
        // R2c inverts R3 the same way and for the same reason, from the other direction: the
        // encounter outlived the settlement. `LeaveSettlementAction.ApplyForParty` (installed
        // v1.4.8) finishes the PlayerEncounter only when the leaving party leads its army and the
        // main party is attached to it, which an enlisted player never is, so a settlement exit can
        // leave one behind. R3 protects a town visit the player OWNS; once the party is out of the
        // settlement there is no visit left to protect, and the encounter blocks map movement, every
        // future encounter, and the battle-latch break.
        if (intent == EncounterFinishIntent.ShoreLeaveEnd && !snapshot.HasEncounteredMobileParty)
            return EncounterFinishVerdict.Finish;

        // R2c checks the precondition rather than trusting the caller for it. The first version took
        // the caller's word that the player was outside a settlement, and the caller read that from
        // a DIFFERENT snapshot captured earlier in the tick. This snapshot already carries a freshly
        // read PlayerInsideSettlement and the policy simply never looked at it. Enforcing it here
        // removes the contract, kills the staleness question, and means a caller that passes this
        // intent wrongly gets a skip instead of a destroyed town visit.
        if (intent == EncounterFinishIntent.StrandedOutsideSettlement
            && !snapshot.HasEncounteredMobileParty
            && !snapshot.PlayerInsideSettlement)
            return EncounterFinishVerdict.Finish;

        // R3 — a settlement encounter has no encountered MOBILE party. This is what keeps the oath
        // from destroying a town visit: swear in a keep and the encounter is the settlement's.
        if (!snapshot.HasEncounteredMobileParty)
            return EncounterFinishVerdict.SkipNotOurs;

        // R4 — it is a party encounter, but only the commander's is ours to close.
        return snapshot.EncounteredPartyIsCommanderRelated
            ? EncounterFinishVerdict.Finish
            : EncounterFinishVerdict.SkipNotOurs;
    }
}
