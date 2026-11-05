using TAOM.Features.Enlistment.Duties;

namespace TAOM.Features.Enlistment;

/// <summary>Why "speak with your commander" is or is not available right now.</summary>
public enum TalkToCommanderResult
{
    Opened = 0,

    /// <summary>Not serving anyone.</summary>
    NotEnlisted = 1,

    /// <summary>
    /// A battle is running. A conversation would tear down the seeded <c>PlayerEncounter</c> that
    /// the battle service owns — and that encounter is the only thing advancing the player's map
    /// event, so losing it freezes the battle permanently.
    /// </summary>
    InBattle = 2,

    /// <summary>
    /// Detached on a duty: the player is loose on the map and can walk up and click the lord like
    /// anyone else. Offering a teleporting conversation option here would be strictly worse.
    /// </summary>
    OnDuty = 3,

    /// <summary>The commander is dead, captured, or otherwise unreachable.</summary>
    CommanderUnavailable = 4,

    /// <summary>The engine will not accept a map conversation right now (wrong game state).</summary>
    NotOnMap = 5,
}

/// <summary>
/// The player's agency while serving: talk to the commander, ask for work. Both were shipped
/// behaviours with no way to reach them — the enlisted dialog surface (reassignment, quartermaster,
/// discharge in person) existed but nothing could open a conversation, and duties only ever arrived
/// unbidden on the daily tick. Also owns the exit from the service menu once service has ended (#828).
/// </summary>
public interface IEnlistmentPlayerActionService
{
    /// <summary>Pure gate — safe to call from a menu-option condition every frame.</summary>
    TalkToCommanderResult CanTalkToCommander();

    /// <summary>
    /// Open the conversation. Re-runs <see cref="CanTalkToCommander"/> internally: a frame passes
    /// between a menu option's condition and its consequence, and a battle can start in it.
    /// </summary>
    TalkToCommanderResult TalkToCommander();

    /// <summary>Ask the commander for work now. Shares one offer path with the daily tick.</summary>
    DutyRequestResult RequestDutyNow(double nowDays);

    /// <summary>
    /// May the player be offered shore leave right now (field report 1)? Pure gate — safe to call
    /// from a menu-option condition every frame.
    /// </summary>
    bool CanTakeTownLeave();

    /// <summary>
    /// Take shore leave: releases the settlement menu so the player can use the town his column is
    /// resting in. Returns false when the gate has closed since the option was drawn.
    /// </summary>
    bool TakeTownLeave();

    /// <summary>
    /// Is anyone serving right now? Pure read, safe in a menu-option condition every frame. Each
    /// service-menu option gates itself on this, because the wait menu's own condition is `true`
    /// (a false one hides every option and nothing exits the menu, #567 and #828).
    /// </summary>
    bool IsServing { get; }

    /// <summary>
    /// "Go on your way": leave the service menu after a discharge that ran before the map existed
    /// (a load with the MCM switch off, or the identity guard). The wait has already ended by the
    /// time this runs. Still serving: re-enter the service menu, which restarts the wait. Inside a settlement outside a map event: open its menu, or if that fails close
    /// the encounter, walk out and exit; if the walk-out fails, keep the service menu so a working
    /// exit stays on screen. Otherwise exit to the last menu.
    /// </summary>
    void LeaveAfterDischarge();
}
