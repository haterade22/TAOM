namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// What the order panel's own gate reads (<c>MissionOrderVM.CheckCanBeOpened</c>, v1.5.7 :740-770), read once by the adapter. The command mouse
/// gives an order only where the panel would open, so a click can never take delegated companies back from the AI of a hero who is gone.
/// </summary>
internal readonly record struct SeatCommandFacts(
    bool MainAgentExists, bool Deployment, bool EnginePlayerControlled, bool HeroFightsOn, bool HasBots, bool HasTroopsUnderThem,
    bool PlayerGeneralOrSergeant, bool MissionEnding, bool BattleInRetreat);

/// <summary>Why an order from the mouse is refused; <c>None</c> means it may be given.</summary>
internal enum SeatCommandRefusal
{
    None,
    HeroGone,
    NotPlayerControlled,
    NoTroops,
    MissionEnding,
}

/// <summary>When the command mouse is live, and when one order may be given (Seat build-spec 6.4).</summary>
internal static class SeatCommandRule
{
    /// <summary>
    /// The mouse owns the Seat's buttons: the setting is on, the view is in the Seat, the mission kind lets the player order, and no network
    /// session runs (co-op included). The cursor shows and the click shield covers the screen while this holds, even after the hero has fallen.
    /// </summary>
    internal static bool IsLive(bool settingOn, bool seated, bool ordersPermitted, bool networkSession) =>
        settingOn && seated && ordersPermitted && !networkSession;

    /// <summary>
    /// The panel's test, in its order: no main agent, a main agent the player does not control (the hero who fights on counts as controlled,
    /// as <c>Patch140_OrderPanelCheck</c> counts him), no troops under the player, then an ending mission that is not a retreat. Deployment skips
    /// the control test, as the panel does.
    /// </summary>
    internal static SeatCommandRefusal Refusal(SeatCommandFacts facts)
    {
        if (!facts.MainAgentExists)
            return SeatCommandRefusal.HeroGone;
        if (!facts.Deployment && !(facts.EnginePlayerControlled || facts.HeroFightsOn))
            return SeatCommandRefusal.NotPlayerControlled;
        if (!facts.HasBots || !facts.HasTroopsUnderThem || !facts.PlayerGeneralOrSergeant)
            return SeatCommandRefusal.NoTroops;
        if (facts.MissionEnding && !facts.BattleInRetreat)
            return SeatCommandRefusal.MissionEnding;
        return SeatCommandRefusal.None;
    }

    /// <summary>
    /// The click shield covers the whole screen: the cursor shows, the command mouse is live, the middle button is not looking, and no other
    /// overlay that takes the mouse buttons has the pointer (the battle action bar and the other views keep their clicks).
    /// </summary>
    internal static bool ShieldHolds(bool cursorShown, bool commandLive, bool lookHeld, bool anotherLayerClaims) =>
        cursorShown && commandLive && !lookHeld && !anotherLayerClaims;

    internal static bool MayCommand(SeatCommandFacts facts) => Refusal(facts) == SeatCommandRefusal.None;

    /// <summary>The word of an outcome line for a refusal.</summary>
    internal static string Reason(SeatCommandRefusal refusal) => refusal switch
    {
        SeatCommandRefusal.HeroGone => "hero-gone",
        SeatCommandRefusal.NotPlayerControlled => "not-controlled",
        SeatCommandRefusal.NoTroops => "no-troops",
        SeatCommandRefusal.MissionEnding => "mission-ending",
        _ => "none",
    };
}
