namespace TAOM.Adapters;

/// <summary>
/// One-read snapshot of the main party's presence flags and player captivity. Presence
/// flags are read for DIAGNOSTICS and load-time rescue only — enlistment state decisions
/// come from the persisted state machine, never from these flags (the donor mod's core
/// defect was using them as state).
/// </summary>
public sealed class PlayerPresenceSnapshot
{
    public bool MainPartyExists { get; }
    public bool IsCaptive { get; }
    public bool IsActive { get; }
    public bool IsVisible { get; }
    public string SettlementId { get; }
    public bool IsInMapEvent { get; }

    /// <summary>True when MainParty.AttachedTo is set. Blocks encounters (EncounterManager:38).</summary>
    public bool IsAttachedToParty { get; }

    /// <summary>True when a PlayerEncounter is live. Blocks ALL further main-party encounters (EncounterManager:38).</summary>
    public bool HasPlayerEncounter { get; }

    /// <summary>Distance to the commander's party, or -1 when either is unresolvable. Makes "left behind" measurable.</summary>
    public float DistanceToCommander { get; }

    /// <summary>Hidden + inactive — the parked shape. With no enlistment record, this is an anomaly to rescue.</summary>
    public bool LooksParked => MainPartyExists && !IsActive && !IsVisible;

    /// <summary>
    /// True when the engine would refuse to start ANY encounter for the main party. Mirrors the
    /// blocking clauses of EncounterManager.HandleEncounterForMobileParty. If this is true after a
    /// discharge, the player can no longer talk to anyone — the state must be cleared, not just logged.
    /// </summary>
    public bool EncountersBlocked =>
        MainPartyExists && (!IsActive || IsAttachedToParty || IsInMapEvent || HasPlayerEncounter);

    /// <summary>One-line dump for the diagnostic log. Order matches the engine's gate.</summary>
    public string Describe() =>
        !MainPartyExists
            ? "mainParty=MISSING"
            : $"active={IsActive} visible={IsVisible} attachedTo={IsAttachedToParty} " +
              $"inMapEvent={IsInMapEvent} playerEncounter={HasPlayerEncounter} " +
              $"settlement={SettlementId ?? "-"} captive={IsCaptive} " +
              $"distToCommander={(DistanceToCommander < 0 ? "?" : DistanceToCommander.ToString("F1"))} " +
              $"=> parked={LooksParked} encountersBlocked={EncountersBlocked}";

    public PlayerPresenceSnapshot(
        bool mainPartyExists,
        bool isCaptive = false,
        bool isActive = false,
        bool isVisible = false,
        string settlementId = null,
        bool isInMapEvent = false,
        bool isAttachedToParty = false,
        bool hasPlayerEncounter = false,
        float distanceToCommander = -1f)
    {
        MainPartyExists = mainPartyExists;
        IsCaptive = isCaptive;
        IsActive = isActive;
        IsVisible = isVisible;
        SettlementId = settlementId;
        IsInMapEvent = isInMapEvent;
        IsAttachedToParty = isAttachedToParty;
        HasPlayerEncounter = hasPlayerEncounter;
        DistanceToCommander = distanceToCommander;
    }
}
