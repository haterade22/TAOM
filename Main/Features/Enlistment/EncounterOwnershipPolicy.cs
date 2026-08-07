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
