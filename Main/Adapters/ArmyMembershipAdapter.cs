using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <inheritdoc />
public sealed class ArmyMembershipAdapter : IArmyMembershipAdapter
{
    private readonly IModLogger _logger;

    /// <summary>
    /// The army this adapter created because the commander had none, or null. Only an army we
    /// created is ours to disband — tearing down a lord's real army because our soldier left it
    /// would be exactly the world-state mutation this feature refuses to make.
    ///
    /// In-memory on purpose, and <see cref="ResetSessionCaches"/> clears it on load: an
    /// <c>Army</c> handle from a previous campaign is a dead object, and a same-process reload of
    /// the SAME campaign produces a fresh <c>Army</c> instance that this stale reference would
    /// never match — leaving <see cref="DisbandCreatedArmy"/> unable to identify its own army for
    /// the rest of the process.
    /// </summary>
    private Army _createdArmy;

    public ArmyMembershipAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool IsInArmy
    {
        get
        {
            try { return MobileParty.MainParty?.Army != null; }
            catch { return false; }
        }
    }

    public bool JoinCommanderArmy(string commanderHeroId)
    {
        try
        {
            var main = MobileParty.MainParty;
            var commanderParty = FindCommanderParty(commanderHeroId);
            if (main == null || commanderParty == null)
                return false;

            var army = commanderParty.Army ?? CreateArmyLedBy(commanderParty);
            if (army == null)
                return false;

            if (main.Army == army)
                return true;

            // BOTH calls are required, and this order matches the engine's own (Army.Tick sets
            // Army when a party joins, then calls AddPartyToMergedParties once it closes).
            //
            // Verified on installed 1.4.8, PartyAgentOrigin.IsInSameArmyAsPlayer needs both halves:
            // it requires `army == MobileParty.MainParty.Army` — membership, which only the Army
            // setter gives (OnAddPartyInternal does the _parties.Add) — AND, when the leader is not
            // the main party, `MobileParty.MainParty.AttachedTo == army.LeaderParty`, which only
            // AddPartyToMergedParties sets. Either call alone leaves the property false and the
            // player back on his own team.
            //
            // No influence is charged: OnAddPartyInternal's ChangeClanInfluenceAction branch is
            // gated on `mobileParty != MobileParty.MainParty`, and ours is the main party.
            main.Army = army;
            army.AddPartyToMergedParties(main);

            _logger?.LogInfo(
                $"[Enlistment] joined '{commanderHeroId}'s army for the battle " +
                $"(leader '{army.LeaderParty?.StringId}', {army.Parties?.Count ?? 0} parties" +
                $"{(_createdArmy == army ? ", created for this battle" : "")})");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] JoinCommanderArmy('{commanderHeroId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool LeaveArmy()
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main == null)
                return false;

            var left = main.Army;

            // AttachedTo FIRST, and it is not redundant even though the Army setter's
            // OnRemovePartyInternal also nulls it. The carve-out below can leave Army SET, and this
            // is the field PlayerEncounter.FinishEncounterInternal reads when deciding whether to
            // grant the post-defeat escape — so it has to be cleared on both paths, not just the one
            // that happens to go through the setter.
            //
            // Clearing it first is also the safe order for the pairing: an AttachedTo that outlives
            // its Army is what DefaultEncounterGameMenuModel.GetGenericStateMenu derefs unguarded
            // (it reads mainParty.Army.LeaderParty INSIDE an `if (AttachedTo != null)`), on every
            // map frame. The reverse — Army set, AttachedTo null — is an ordinary un-merged member
            // and harmless.
            if (main.AttachedTo != null)
                main.AttachedTo = null;

            // Leader carve-out, same rule as ClearArmyAttachment: clearing Army while we LEAD one
            // disbands it out from under its members — OnRemovePartyInternal calls
            // DisbandArmyAction.ApplyByLeaderPartyRemoved when the removed party is the leader.
            if (main.Army != null && main.Army.LeaderParty != main)
                main.Army = null;

            DisbandCreatedArmy();

            if (left != null)
                _logger?.LogInfo($"[Enlistment] left the commander's army (leader '{left.LeaderParty?.StringId}')");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] LeaveArmy failed: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public void ResetSessionCaches()
    {
        // No disband here, deliberately. This runs on load, when the handle either points at a
        // dead campaign's object or at nothing that matches the freshly-deserialized world. There
        // is no army we can still prove is ours, so the only safe action is to forget.
        _createdArmy = null;
    }

    /// <summary>
    /// Stand an army up around a commander who has none, so <c>IsInSameArmyAsPlayer</c> has
    /// something to be true about. Requires a kingdom — a clanless or kingdomless lord simply does
    /// not get the team merge, and the caller falls back to today's behaviour.
    ///
    /// DELIBERATELY the bare <c>Army</c> constructor and NOT <c>Kingdom.CreateArmy</c>. Verified on
    /// installed 1.4.8: <c>CreateArmy</c> calls <c>army.Gather(...)</c>, whose non-player branch runs
    /// <c>FindBestGatheringSettlementAndMoveTheLeader</c> — it would pick a fortification and send
    /// our commander marching to it, mid-battle, as a side effect of a cosmetic team fix. It also
    /// dispatches <c>OnArmyCreated</c>, which is what raises the player-facing "has formed an army"
    /// notification for an army that exists for one fight.
    ///
    /// The constructor alone is sufficient and complete: it sets <c>LeaderParty</c>, assigns
    /// <c>LeaderParty.Army = this</c> (which registers the leader in <c>_parties</c>), and the
    /// <c>Kingdom</c> setter self-registers through <c>AddArmyInternal</c>. What it skips is the
    /// gathering objective we do not want, so <c>AiBehaviorObject</c> stays null.
    ///
    /// THE NULL OBJECTIVE IS A LIABILITY, NOT A FEATURE. It is closed twice over: the army is
    /// disbanded unconditionally when we leave it (<see cref="DisbandCreatedArmy"/>), AND the field
    /// is seeded here (<see cref="SeedBehaviorObject"/>) so that an army which somehow outlives the
    /// battle is merely odd rather than fatal.
    /// </summary>
    private Army CreateArmyLedBy(MobileParty commanderParty)
    {
        var kingdom = commanderParty.ActualClan?.Kingdom;
        var leader = commanderParty.LeaderHero;
        if (kingdom == null || leader == null || !leader.IsActive)
        {
            _logger?.LogInfo(
                "[Enlistment] commander has no army and none can be raised for him " +
                "(no kingdom, no party leader, or the leader is inactive) — joining without the team merge");
            return null;
        }

        // A previous created army still on the books means a LeaveArmy was missed. Overwriting the
        // handle would orphan it with its null objective forever, so end it before raising another.
        DisbandCreatedArmy();

        _createdArmy = new Army(kingdom, commanderParty, Army.ArmyTypes.Patrolling);
        SeedBehaviorObject(_createdArmy, commanderParty);
        return _createdArmy;
    }

    /// <summary>
    /// Give the army a non-null <c>AiBehaviorObject</c>. <c>Gather()</c> normally does this and we
    /// deliberately skip <c>Gather()</c>, so without this the field is null for the object's whole
    /// life — and vanilla dereferences it with NO null guard in several places, because vanilla can
    /// never reach them with a null. Verified against installed 1.4.8:
    ///
    /// <list type="bullet">
    /// <item><c>Army.GetLongTermBehaviorTextForAILeadedParty</c> — 5 of 7 switch cases
    /// (<c>GoToSettlement</c> reads <c>AiBehaviorObject.Name</c>; <c>BesiegeSettlement</c> reads
    /// <c>settlement.IsVillage</c>; <c>RaidSettlement</c>, <c>DefendSettlement</c> reads
    /// <c>settlement.Position</c>, <c>PatrolAroundPoint</c>). Reached from
    /// <c>MobileParty.GetBehaviorText()</c> (map party tooltip) and <c>KingdomArmyItemVM</c>
    /// (kingdom Armies tab).</item>
    /// <item><c>Army.GetNotificationText</c> — reads <c>AiBehaviorObject.Name</c> unconditionally
    /// whenever the leader is not the main party.</item>
    /// <item><c>LordConversationsCampaignBehavior.conversation_lord_tell_objective_gathering_on_condition</c>
    /// — <c>partyBelongedTo.Army.AiBehaviorObject.Name</c>, gated ONLY on <c>Army != null &amp;&amp;
    /// IsWaitingForArmyMembers()</c>. That second call returns TRUE FOREVER for a bare-ctor army,
    /// because <c>_armyGatheringStartTime</c> stays 0 and is only ever set by
    /// <c>CheckAndSetArmyGatheringTime</c>, which itself requires <c>AiBehaviorObject is
    /// Settlement</c>. So talking to any lord in such an army is an unconditional CTD — and this
    /// feature's own wait menu offers "talk to your commander".</item>
    /// </list>
    ///
    /// WHY THIS IS INERT FOR THE ARMY'S REAL LIFETIME. The objective only drives behaviour through
    /// <c>MoveLeaderToGatheringLocationIfNeeded</c> and <c>CheckAndSetArmyGatheringTime</c>, and
    /// BOTH require <c>LeaderParty.MapEvent == null</c>. The army is created because the commander
    /// is already in a map event and disbanded when that event ends, so neither can fire while it
    /// legitimately exists. They can only fire for an army that leaked — where being walked toward
    /// a settlement is enormously preferable to crashing the game.
    ///
    /// Set BEFORE the main party joins: the setter's tracking branch requires
    /// <c>Parties.Contains(MobileParty.MainParty)</c>, which is false at construction, so this
    /// cannot start a settlement-tracking side effect.
    /// </summary>
    private void SeedBehaviorObject(Army army, MobileParty commanderParty)
    {
        try
        {
            // Any real settlement satisfies the invariant; the commander's own is the least
            // surprising thing for the engine to name if a leaked army is ever asked what it is
            // doing. HomeSettlement is null-safe on both types (verified 1.4.8).
            var anchor = commanderParty.CurrentSettlement
                ?? commanderParty.HomeSettlement
                ?? commanderParty.LeaderHero?.HomeSettlement;

            if (anchor == null)
            {
                _logger?.LogWarning(
                    "[Enlistment] could not anchor the battle army's AiBehaviorObject — no settlement " +
                    "resolvable for the commander. The unconditional disband is the only guard left, so a " +
                    "leaked army here would crash vanilla's unguarded reads of that field.");
                return;
            }

            army.AiBehaviorObject = anchor;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] could not seed the battle army's AiBehaviorObject: {ex.Message}");
        }
    }

    /// <summary>
    /// End the army we raised, UNCONDITIONALLY — not "if nobody else joined it".
    ///
    /// The earlier revision left it standing whenever another lord had attached, on the reasoning
    /// that it had become a real army. It had not: it carries a null <c>AiBehaviorObject</c>
    /// forever (see <see cref="CreateArmyLedBy"/>), which crashes
    /// <c>Army.GetLongTermBehaviorTextForAILeadedParty</c> the moment anything asks it what it is
    /// doing. Two live callers do: <c>MobileParty.GetBehaviorText()</c> — the commander's party IS
    /// the army leader, so its <c>Army.LeaderParty == this</c> arm is taken — which drives the map
    /// party tooltip, and <c>KingdomArmyItemVM</c>, which drives the kingdom Armies tab.
    ///
    /// Nor does it disband itself. <c>Army.CheckInactivity</c> DECREMENTS the inactivity counter for
    /// <c>Besiege/Raid/Defend/AssaultSettlement</c>, so an army around a lord who goes besieging
    /// never times out — and <c>_aiBehaviorObject</c> is <c>[SaveableField(16)]</c>, so it survives
    /// every reload. A stray army raised for one skirmish becomes a permanent crash on the kingdom
    /// screen.
    ///
    /// The lords lose nothing. <c>DisbandArmyAction.ApplyByObjectiveFinished</c> is an ordinary
    /// vanilla dispersion — every party is detached, repositioned around the leader and set to
    /// hold, and each lord resumes his own business on the next AI tick.
    /// </summary>
    private void DisbandCreatedArmy()
    {
        var army = _createdArmy;
        if (army == null)
            return;

        // Clear the handle FIRST: the disband must not be retried on a later pass if it throws,
        // and DisperseInternal re-entering through an event handler must not find it either.
        _createdArmy = null;

        try
        {
            // Already gone — vanilla dispersed it (cohesion, food, no active war, leader removed).
            // DisperseInternal nulls Kingdom and every member's Army, and resets its own
            // _armyIsDispersing flag, so a second call would re-fire OnArmyDispersed for an army
            // that no longer exists rather than being absorbed by that guard.
            if (army.Kingdom == null || army.LeaderParty?.Army != army)
            {
                _logger?.LogInfo("[Enlistment] the army raised for this battle was already dispersed");
                return;
            }

            DisbandArmyAction.ApplyByObjectiveFinished(army);
            _logger?.LogInfo("[Enlistment] disbanded the army raised for this battle");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] could not disband the army raised for this battle: {ex.Message}");
        }
    }

    private static MobileParty FindCommanderParty(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;

        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero?.StringId == heroId)
                return hero.PartyBelongedTo;
        }
        return null;
    }
}
