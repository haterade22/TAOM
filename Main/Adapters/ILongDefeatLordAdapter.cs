using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The engine half of the Long Defeat death rule (phase4-plan.md Slice 4, D40 to D47), kept apart from <see cref="ILongDefeatBattlefieldAdapter"/> so the director's fake stays small. The battlefield adapter
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
}
