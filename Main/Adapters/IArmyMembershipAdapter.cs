namespace TAOM.Adapters;

/// <summary>
/// Transient, battle-only army membership for the enlisted player (#443, field reports 4 and 7).
///
/// WHY THIS EXISTS. TAOM keeps <c>MobileParty.MainParty.Army</c> permanently null — every enlistment
/// regime calls <c>ClearArmyAttachment()</c>. That one decision is what puts the player on a
/// different TEAM from the lord he serves: <c>Mission.GetAgentTeam</c> routes a party to
/// <c>PlayerTeam</c> only when <c>IsUnderPlayersCommand || IsInSameArmyAsPlayer</c>, so the player
/// takes the first arm and his commander's troops take neither — landing on <c>PlayerAllyTeam</c>.
/// <c>DefaultBattleMissionAgentSpawnLogic</c> then deploys each team as its own block, sorting
/// <c>PlayerTeam</c> LAST with a 20-unit inter-team gap. That is the "I spawn way in the back" report,
/// exactly.
///
/// Joining the commander's army for the duration of a battle satisfies
/// <c>PartyAgentOrigin.IsInSameArmyAsPlayer</c>, which collapses both onto <c>PlayerTeam</c> — same
/// team, same formations, same deployment block. It also makes <c>MapEvent.IsPlayerSergeant()</c>
/// true, so vanilla derives the battle roles TAOM currently hand-maintains.
///
/// TRANSIENT, and the boundary is not a style choice.
/// <c>PlayerEncounter.FinishEncounterInternal</c> grants the post-defeat escape —
/// <c>TeleportPartyToOutSideOfEncounterRadius()</c> plus <c>SetDoNotAttackMainParty(2)</c> — only
/// when <c>MainParty.AttachedTo == null</c>, and <c>Army.AddPartyToMergedParties</c> sets
/// <c>AttachedTo</c>. So a player still attached when the encounter finishes FORFEITS vanilla's
/// escape and is immediately re-engaged. That is field report 7's second half, and ServeAsSoldier
/// ships with exactly that hole. <see cref="LeaveArmy"/> therefore runs at battle END, before the
/// encounter finishes — not on the next tick, and not from the re-park.
/// </summary>
public interface IArmyMembershipAdapter
{
    /// <summary>
    /// Put the main party in the commander's army, creating one led by the commander if he has
    /// none. Idempotent: returns true when the player is already in the commander's army.
    ///
    /// If the commander is a FOLLOWER in someone else's army, this joins that army rather than
    /// rearranging it. ServeAsSoldier detaches the lord and re-founds him as his own leader
    /// (`Test.cs:2465-2476`); TAOM does not mutate world state the player has no business touching.
    /// </summary>
    bool JoinCommanderArmy(string commanderHeroId);

    /// <summary>
    /// Remove the main party from any army and disband one this adapter created, whether or not
    /// other lords joined it meanwhile. Idempotent and safe to call when never joined.
    ///
    /// The unconditional disband is a crash fix, not tidiness. An army built by the bare
    /// constructor would carry a null <c>AiBehaviorObject</c>, which vanilla dereferences with no
    /// guard in several places (verified on installed 1.4.8) — the map party tooltip, the kingdom
    /// Armies tab, and an ordinary lord conversation. The implementation ALSO seeds that field so a
    /// leaked army is survivable, but the two guards are not interchangeable: the seed keeps a
    /// stray army harmless, the disband keeps it from being stray.
    /// </summary>
    bool LeaveArmy();

    /// <summary>True while the main party belongs to any army.</summary>
    bool IsInArmy { get; }

    /// <summary>
    /// Forget the created-army handle. MUST be called on game load: it is an in-memory
    /// <c>Army</c> reference, so after a reload it points either at a dead campaign's object or at
    /// an instance the freshly-deserialized world no longer contains. Left stale, the identity
    /// check in <c>LeaveArmy</c> can never match again for the rest of the process.
    /// </summary>
    void ResetSessionCaches();
}
