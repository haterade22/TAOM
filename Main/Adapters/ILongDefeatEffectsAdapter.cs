using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The engine writes and reads that are not the field itself: what the director publishes into the engine's hooks, and what it reads back
/// to prove a number reached the agents. Takes and returns domain types only (ADR-007). Main thread. Slices 2 and 3 hold the wave's published
/// table (damage factor, stat record, armour) and the two reads that prove it; Slice 6 adds the troop and class boons' refresh, health writes and read-back; Slice 8 adds the engines' hit points and the morale pulse; Slice 9 adds the poison tick.
/// </summary>
internal interface ILongDefeatEffectsAdapter
{
    /// <summary>Publishes the wave's numbers into the engine's hooks. One write each, read by the engine's threads from then on.</summary>
    void PublishStats(LdStatTable table);

    /// <summary>
    /// One standing human attacker as the engine holds it now: its troop, its health limit, the maximum its character gives it, and the blows
    /// the damage factor has raised since the wave's factor was published. An empty troop with no attacker standing; the blow count is read either way.
    /// </summary>
    LdScaleSample ReadScale();

    /// <summary>
    /// One standing human attacker that is not running away and has no live race ability, with each named driven property read as the engine holds
    /// it and as the stat model gives the same agent with the affixes left out (an armour property: as its spawn equipment sums to). No attacker, an empty sample.
    /// </summary>
    LdStatSample ReadStats(IReadOnlyList<string> stats);

    /// <summary>
    /// Sets every live ram's and siege tower's maximum hit points to the vanilla hit points of its type (<c>SiegeEngineType.BaseHitPoints</c>) times <paramref name="factor"/>, and its current hit
    /// points by the same ratio, so a damaged engine keeps its share and a destroyed one stays destroyed. A factor of 1 restores the vanilla value after a hammer wave. Returns each engine as it
    /// stands after the write, and the state of the visible gates, for the hammer's check. The gates are read from every mission object, because a destroyed gate leaves
    /// <c>ActiveMissionObjects</c> (<c>Mission.OnObjectDisabled</c>, <c>Mission.cs:3060</c>); the engines are read from the active list, where leaving it on destruction is what "live" means.
    /// </summary>
    LdEngineReport SetEngineHitPoints(float factor);

    /// <summary>
    /// One Winged Terror pulse: every standing human defender within the pulse's radius of a standing human attacker loses morale, never below the pulse's floor. Reads the attackers' positions
    /// once, counts the defenders drained and left below the floor inside the call, and times itself.
    /// </summary>
    LdPulseResult PulseMorale(LdMoralePulse pulse);

    /// <summary>
    /// One Morgul Blades tick: takes <paramref name="damage"/> health off the defender <paramref name="victim"/> (his key in the registry), ignoring armour. While his health stays above the damage that is all.
    /// A tick that would kill him needs a real owner: <paramref name="poisoner"/> while the registry holds him and he is active, else the commander, else any attacker the registry holds as active
    /// (<see cref="PoisonOwner"/>), never an owner of -1. With none standing the tick is withheld and the victim keeps 1 health. The owner's <c>IsActive()</c> is read again at the call, and one that is not active
    /// is counted (<c>OwnerMissing</c>) and kills nothing. A victim already gone answers an empty result.
    /// </summary>
    LdPoisonResult ApplyPoison(AgentKey victim, AgentKey poisoner, int damage);

    /// <summary>
    /// Derives the driven properties of every standing human of one group of the player's side again (and of each one's mount), so a stat record just published reaches soldiers already
    /// built: the engine derives them at spawn and on events only, and its controller setter calls no update. <see cref="BoonTarget.Player"/> is the main agent, a troop group excludes him.
    /// </summary>
    LdTouched RefreshStats(BoonTarget target);

    /// <summary>
    /// Derives the driven properties of every standing human attacker (and each one's mount) again, so a stat table just published (a war cry beginning or ending) reaches soldiers already
    /// built: the engine derives them at spawn and on events only (<c>Agent.UpdateAgentProperties</c>, <c>Agent.cs:3692-3699</c>).
    /// </summary>
    LdTouched RefreshAttackers();

    /// <summary>
    /// The standing commander as the engine holds him: his troop, his health limit and the maximum his character gives him (their ratio is the rank's factor times the endless factor). An
    /// empty troop with no commander standing.
    /// </summary>
    LdScaleSample ReadCommander();

    /// <summary>One line for the log about the standing commander: where he is and what his formation does, for risk 1 (does a hero spawned mid-wave fight as a soldier). Empty with none standing.</summary>
    string DescribeCommander();

    /// <summary>
    /// Multiplies the health limit of every standing human of a troop group by one boon's own factor, keeping each one's health share. The limit, its base and
    /// the health are the three writes the engine's own horse spawn makes (<c>Mission.cs:4607-4609</c>). Not for the lord: a relative write on a body adopted this tick would apply
    /// the factor twice, so his health is only ever set absolutely (<see cref="SetPlayerHealth"/>).
    /// </summary>
    LdTouched ScaleHealth(BoonTarget target, float factor);

    /// <summary>
    /// Sets the main agent's health limit to its character's maximum times <paramref name="factor"/>, keeping his health share: an absolute write, so a soldier taken over from the
    /// garrison (who spawned with his group's factor) ends with the player's and the lord's own fresh body with the same.
    /// </summary>
    void SetPlayerHealth(float factor);

    /// <summary>
    /// One standing human of the group that is not running away and has no live race ability: the oldest, or with <paramref name="newest"/> the one spawned last (a team's agent list keeps
    /// spawn order), with its health limit and each named driven property as the engine holds it and as the stat model gives the same agent with the boons left out. None when no such soldier stands.
    /// </summary>
    LdTargetSample ReadTarget(BoonTarget target, IReadOnlyList<string> stats, bool newest);
}
