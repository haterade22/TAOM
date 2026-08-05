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

    /// <summary>Hidden + inactive — the parked shape. With no enlistment record, this is an anomaly to rescue.</summary>
    public bool LooksParked => MainPartyExists && !IsActive && !IsVisible;

    public PlayerPresenceSnapshot(
        bool mainPartyExists,
        bool isCaptive = false,
        bool isActive = false,
        bool isVisible = false,
        string settlementId = null,
        bool isInMapEvent = false)
    {
        MainPartyExists = mainPartyExists;
        IsCaptive = isCaptive;
        IsActive = isActive;
        IsVisible = isVisible;
        SettlementId = settlementId;
        IsInMapEvent = isInMapEvent;
    }
}
