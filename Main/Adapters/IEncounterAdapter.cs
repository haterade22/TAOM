namespace TAOM.Adapters;

/// <summary>Which side of a map event a party fights on. Adapter-level mirror of BattleSideEnum's two real sides.</summary>
public enum PartyBattleSide
{
    Defender = 0,
    Attacker = 1,
}

/// <summary>
/// Wraps the static PlayerEncounter surface for enlisted battle interception: joining the
/// commander's map event, creating an encounter against it, and cleaning up stale ones.
/// </summary>
public interface IEncounterAdapter
{
    bool HasCurrent { get; }

    bool IsInsideSettlement { get; }

    /// <summary>StringId of the encountered mobile party, or null.</summary>
    string EncounteredPartyId { get; }

    /// <summary>
    /// True when the live encounter is one enlistment may safely finish — i.e. it exists, it is
    /// against the given party, and no conversation is in progress.
    ///
    /// Finishing blind destroys encounters that are the PLAYER'S own business. A settlement visit,
    /// a battle they started, or a conversation with someone else all live in
    /// `PlayerEncounter.Current`, and tearing one down strands them. The donor guards every finish
    /// this way (`RFEnlistmentCampaignBehavior.FinalizeEnlistmentConversation` +
    /// `TryClearStaleEncounterForCommanderBattle`); the native rewrite dropped the guard.
    /// </summary>
    bool IsEncounterOwnedBy(string partyId);

    /// <summary>Side of the given party in its current map event, or null when not in one.</summary>
    PartyBattleSide? GetPartyBattleSide(string partyId);

    bool IsPartyInMapEvent(string partyId);

    /// <summary>
    /// True when the main party is genuinely inside a map event. This is the ground truth for
    /// "did the join actually land" — <see cref="JoinBattle"/> cannot be trusted on its own
    /// because the engine's JoinBattleInternal silently no-ops when EncounteredBattle is null.
    /// </summary>
    bool IsMainPartyInMapEvent { get; }

    /// <summary>
    /// True when the given party's map event is still live enough to join. This is a MECHANICAL
    /// check only — it deliberately does NOT consult faction war state.
    ///
    /// Do NOT reinstate <c>MapEvent.CanPartyJoinBattle</c> here. That engine method requires every
    /// party on the opposing side to be at war with the joining party's MapFaction, which encodes
    /// "may this free agent lawfully intervene in someone else's war?". An enlisted soldier keeps
    /// their own clan identity and is normally at war with nobody, so it returns false for every
    /// battle and the player is never pulled in (the 2026-08-07 report). The donor mod hit the same
    /// wall and explicitly tolerated a false result while in hidden service mode.
    /// </summary>
    bool IsCommanderBattleJoinable(string partyId, PartyBattleSide side);

    /// <summary>
    /// Establish (or repair) a player encounter seeded against the given party's map event, so
    /// that PlayerEncounter.EncounteredBattle resolves to it and JoinBattle can work.
    ///
    /// Seeds from the map event's own side LEADER parties rather than from MapEventStarted's
    /// arguments: leaders are PartyBase, so a besieged settlement is representable. Resolving
    /// through MobileParty ids instead drops sieges entirely (a settlement defender has no
    /// MobileParty, so the id is null and the encounter is never created).
    /// </summary>
    bool EnsureEncounterAgainst(string partyId);

    /// <summary>
    /// Join the current encounter's battle on the given side. Returns true ONLY when the main
    /// party is verifiably in a map event afterwards.
    /// </summary>
    bool JoinBattle(PartyBattleSide side);

    /// <summary>Leave the settlement when the player is held inside one that is under siege.</summary>
    bool LeaveSettlementIfUnderSiege();

    bool Finish(bool forcePlayerOutFromSettlement = false);
}
