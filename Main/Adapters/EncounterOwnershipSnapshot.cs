namespace TAOM.Adapters;

/// <summary>
/// One flat read of everything needed to decide "is this encounter ours to finish?".
///
/// Produced by the adapter, consumed by a PURE policy, so the decision is fully unit-testable and
/// there is exactly one place that answers the question. Every field is read defensively and
/// INDEPENDENTLY — a throw reading one field must not blank the others, because a snapshot that
/// silently reports "nothing live" would make the oath stop closing the encounter it genuinely
/// owns, which is how the whole can't-interact class of bug started.
/// </summary>
public readonly struct EncounterOwnershipSnapshot
{
    /// <summary>A PlayerEncounter exists at all.</summary>
    public bool HasEncounter { get; }

    /// <summary>A conversation is running. Finishing under it drops the player out of their own dialogue.</summary>
    public bool ConversationInProgress { get; }

    /// <summary>
    /// The encountered party is a MOBILE party. A settlement visit has none — that single fact is
    /// what distinguishes "the player is in a town" from "the player is meeting a lord", and it is
    /// load-bearing rather than defensive.
    /// </summary>
    public bool HasEncounteredMobileParty { get; }

    /// <summary>StringId of the encountered mobile party, or null.</summary>
    public string EncounteredPartyId { get; }

    /// <summary>The encountered party is the commander (or, later, army-related to them).</summary>
    public bool EncounteredPartyIsCommanderRelated { get; }

    /// <summary>The MAIN party is in a map event — a battle of the player's own that we must never tear down.</summary>
    public bool PlayerInMapEvent { get; }

    /// <summary>The player is inside a settlement, so the finish must force them out or they stay encounter-blocked.</summary>
    public bool PlayerInsideSettlement { get; }

    /// <summary>A field could not be read. The policy treats this conservatively rather than guessing.</summary>
    /// <summary>
    /// At least one field could not be read; the adapter has already logged which. DIAGNOSTIC ONLY:
    /// <see cref="TAOM.Features.Enlistment.EncounterOwnershipPolicy"/> deliberately does not branch
    /// on it, because deferring on a read failure would leave the encounter live, which is the
    /// save-breaker this feature exists to avoid. Do not read this as "handled conservatively".
    /// </summary>
    public bool ReadFailed { get; }

    public EncounterOwnershipSnapshot(
        bool hasEncounter,
        bool conversationInProgress = false,
        bool hasEncounteredMobileParty = false,
        string encounteredPartyId = null,
        bool encounteredPartyIsCommanderRelated = false,
        bool playerInMapEvent = false,
        bool playerInsideSettlement = false,
        bool readFailed = false)
    {
        HasEncounter = hasEncounter;
        ConversationInProgress = conversationInProgress;
        HasEncounteredMobileParty = hasEncounteredMobileParty;
        EncounteredPartyId = encounteredPartyId;
        EncounteredPartyIsCommanderRelated = encounteredPartyIsCommanderRelated;
        PlayerInMapEvent = playerInMapEvent;
        PlayerInsideSettlement = playerInsideSettlement;
        ReadFailed = readFailed;
    }

    public static EncounterOwnershipSnapshot None => default;
}
