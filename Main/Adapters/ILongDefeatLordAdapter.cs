using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The lord's body: the death rule (phase4-plan.md Slice 4, D40 to D47), the gear swap and the bot's errand (Slice 5), kept apart from <see cref="ILongDefeatBattlefieldAdapter"/> so the director's fake stays small. The battlefield adapter
/// implements both, and the run host hands the director the same instance as each (<c>LongDefeatLordPinTests</c>). Takes and returns domain types only (ADR-007). Every member runs on the main thread, from the
/// director's tick; none is called from a removal or hit callback. With no such adapter the director keeps the old respawn.
/// </summary>
internal interface ILongDefeatLordAdapter
{
    /// <summary>
    /// The rise (D40): finds the soldier the lord took, if the main agent is not his own body, then spawns the lord in <paramref name="loadout"/> at <paramref name="place"/> (the keep, or the home point of a
    /// mounted class), and only when that spawn succeeded hands the soldier back to the AI. The spawn comes first because the engine never clears the main agent when a body is handed back. A refused spawn hands nothing back.
    /// </summary>
    LdRise RiseLord(LdPlace? place, LdLoadout? loadout);

    /// <summary>
    /// The bot's take-over (D44a): among the player's side's soldiers the engine's own <c>CanTakeControlOfAgent</c> accepts, the one nearest to where the lord's last live body stood, taken through the
    /// engine's own <c>TakeControlOfAgent</c>. His troop, formation and order are logged before the take. With no soldier it takes nobody and says so once.
    /// </summary>
    LdTake TakeOverForBot();

    /// <summary>Writes the player's team general (D41): the live main agent, a live held hero by his id, or nobody. Never a dead body.</summary>
    void SetGeneral(LdGeneralSeat seat);

    /// <summary>Who the engine names as the player's team general now, for the <c>general-down</c> check.</summary>
    LdSeatRead ReadSeat();

    /// <summary>The controller of the main agent and the number of other <c>Player</c>-controlled bodies on the player's side, for the <c>rise</c> check.</summary>
    LdControlRead ReadControl();

    /// <summary>
    /// The forced fall (D47): fells the lord's own live body, the main agent, with the blow <c>Fell</c> builds, owned by the nearest standing attacker, and marks the body first so its removal reports itself
    /// forced. False, with the reason logged, when his own body does not stand or no attacker does; nobody is felled then.
    /// </summary>
    bool FellLord();

    /// <summary>Drops what the field keeps for the death rule: the last position the lord stood at and the body marked forced. Called when the run ends.</summary>
    void ReleaseLord();

    /// <summary>
    /// The live gear swap, variant A (spike S11): puts <paramref name="loadout"/> on the lord's own live body, the main agent, through the engine's own equipment update, then runs the mission's stat model over it.
    /// Reads the driven armour, health and mount before and after. Refuses, with the reason, when the main agent is not the lord's own live body; nothing is written then.
    /// </summary>
    LdSwap SwapGear(LdLoadout loadout);

    /// <summary>The bot's errand (D44): sets the scripted walk of the live, AI-driven main agent to <paramref name="place"/> and holds that body until <see cref="EndErrand"/>.</summary>
    LdErrandMove StartErrand(LdPlace place);

    /// <summary>The errand's teleport (D44): reads the held body's distance and scripted flag, puts him at <paramref name="place"/>, then sets the scripted position again so the AI does not walk him back.</summary>
    LdErrandMove TeleportErrand(LdPlace place);

    /// <summary>Releases the held body's scripted walk if he still stands and drops him. Safe with no errand open.</summary>
    LdErrandMove EndErrand();

    /// <summary>The driven armour of the live main agent, or <see cref="LdArmour.Unread"/> with none: the read at the rise that does not depend on the swap's own writes.</summary>
    LdArmour ReadLordArmour();
}
