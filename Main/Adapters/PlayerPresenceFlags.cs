namespace TAOM.Adapters;

/// <summary>
/// The allocation-free presence read for a per-tick pump. Same fields as
/// <see cref="PlayerPresenceSnapshot"/>, no allocation, no engine object retained.
///
/// TECH DEBT, recorded deliberately: this duplicates <see cref="PlayerPresenceSnapshot"/>'s
/// <c>LooksParked</c> predicate. The right end state is one <c>readonly struct</c> serving both
/// paths, but collapsing them changes null semantics across four services — callers written
/// against a class use <c>GetPresence()?.X ?? true</c>, and mocks returning <c>null</c> become
/// <c>default</c>, which silently flips several guards. `PlayerPresenceFlagsTests` pins the two
/// predicates as equivalent over all flag combinations until that collapse happens.
/// </summary>
public readonly struct PlayerPresenceFlags
{
    public bool MainPartyExists { get; }
    public bool IsCaptive { get; }
    public bool IsActive { get; }
    public bool IsVisible { get; }
    public bool IsInMapEvent { get; }
    public bool HasPlayerEncounter { get; }
    public string SettlementId { get; }

    /// <summary>Hidden + inactive — the parked shape. Must stay identical to <see cref="PlayerPresenceSnapshot.LooksParked"/>.</summary>
    public bool LooksParked => MainPartyExists && !IsActive && !IsVisible;

    public bool IsInSettlement => !string.IsNullOrEmpty(SettlementId);

    public PlayerPresenceFlags(
        bool mainPartyExists,
        bool isCaptive = false,
        bool isActive = false,
        bool isVisible = false,
        bool isInMapEvent = false,
        bool hasPlayerEncounter = false,
        string settlementId = null)
    {
        MainPartyExists = mainPartyExists;
        IsCaptive = isCaptive;
        IsActive = isActive;
        IsVisible = isVisible;
        IsInMapEvent = isInMapEvent;
        HasPlayerEncounter = hasPlayerEncounter;
        SettlementId = settlementId;
    }
}
