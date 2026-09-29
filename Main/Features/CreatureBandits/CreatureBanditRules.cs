using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// The pure decisions behind every creature bandit patch (#692). Static and allocation-free because the
/// weapon guards run for every agent on the engine's worker threads; the id set is immutable, so the
/// reads are thread-safe. A creature bandit agent is recognised by a fingerprint no vanilla agent has: a
/// riderless, non-humanoid agent whose Character is a creature troop. The engine never sets Character on a
/// Monster-built agent, so only the spawner's own agents match. The fingerprint does not ask IsMount: route A
/// clears Mountable after the build so soldiers can target the creature (<see cref="CombatantFlags"/>).
/// </summary>
public static class CreatureBanditRules
{
    private static readonly HashSet<string> Troops = new(CreatureBanditsConfig.CreatureTroopIds, System.StringComparer.Ordinal);
    private static readonly HashSet<string> Trolls = new(CreatureBanditsConfig.TrollBanditTroopIds, System.StringComparer.Ordinal);

    public static bool IsCreatureTroop(string? troopId) => troopId != null && Troops.Contains(troopId);

    /// <summary>A troll band's troop (#694): a humanoid, so no creature seam takes it, only the prisoner rule.</summary>
    public static bool IsTrollBanditTroop(string? troopId) => troopId != null && Trolls.Contains(troopId);

    /// <summary>Spiders and bandit trolls are never taken prisoner, so they can never be recruited from prisoners.</summary>
    public static bool IsNeverPrisoner(string? troopId) => IsCreatureTroop(troopId) || IsTrollBanditTroop(troopId);

    public static bool IsCreatureBroodClan(string? clanId) => clanId == CreatureBanditsConfig.BroodClanId;

    public static bool IsTrollBandClan(string? clanId) => clanId == CreatureBanditsConfig.TrollClanId;

    /// <summary>
    /// The looter-faction clans TAOM spawns itself, the spider brood's and the troll bands': vanilla's map-wide looter
    /// spawn is capped at zero for both, and a meeting with either goes straight to attack or leave.
    /// </summary>
    public static bool IsCreatureBandClan(string? clanId) => IsCreatureBroodClan(clanId) || IsTrollBandClan(clanId);

    /// <summary>
    /// Whether a troop spawn becomes a riderless creature: a creature troop, on the enemy side, in a
    /// field battle (hideouts, sieges and town missions keep the vanilla spawn), with a creature item to
    /// build it from. Anything else falls through to the vanilla spawn, a husk rider on its mount.
    /// </summary>
    public static bool ShouldSpawnAsCreature(string? troopId, bool isPlayerSide, bool isFieldBattle, bool hasCreatureItem)
        => !isPlayerSide && isFieldBattle && hasCreatureItem && IsCreatureTroop(troopId);

    public static bool IsCreatureBandit(string? characterId, bool isHuman, bool hasRider)
        => !isHuman && !hasRider && IsCreatureTroop(characterId);

    /// <summary>
    /// SandBox's BattleAgentLogic and CustomBattleAgentLogic skip an IsMount agent removed as routed with no attacker
    /// (v1.5.3 BattleAgentLogic.cs:147), before they call Origin.SetRouted, so such a creature is never counted removed
    /// and its side never depletes. That is only a creature route A left Mountable; an unmounted one the engine counts
    /// itself. This is the case the mission behavior counts.
    /// </summary>
    public static bool ShouldBackstopRoutedRemoval(bool isCreatureBandit, bool isMount, AgentState state, bool hasAffector)
        => isCreatureBandit && isMount && state == AgentState.Routed && !hasAffector;

    /// <summary>
    /// Whether the creature may hunt and strike. The deployment controller turns AI ticking off for the whole
    /// deployment, and while agents are being teleported a scripted move teleports (v1.5.3 Agent.cs:2462-2465), so a
    /// hunt would land the creature on the paused army. Every engine AI component asks AllowAiTicking the same way.
    /// </summary>
    public static bool MayFight(bool allowAiTicking, bool isTeleportingAgents) => allowAiTicking && !isTeleportingAgents;

    /// <summary>
    /// The scoreboard's casualty columns for a removed troop, as the engine's battle observer counts a human (v1.5.3
    /// <c>BattleObserverMissionLogic.cs:54-70</c>): killed, unconscious as wounded, or routed; null for any other state,
    /// which vanilla never reports.
    /// </summary>
    public static (int Killed, int Wounded, int Routed)? ScoreboardCasualty(AgentState state) => state switch
    {
        AgentState.Killed => (1, 0, 0),
        AgentState.Unconscious => (0, 1, 0),
        AgentState.Routed => (0, 0, 1),
        _ => null,
    };

    /// <summary>
    /// Whether an agent can have a scoreboard row: a real team, an origin and a troop, which is all the engine's own
    /// observer asks of a human. The origin's combatant may be null: a Custom Battle console spawn's
    /// <c>BasicBattleAgentOrigin</c> has none, and the scoreboard files it under a generic party.
    /// </summary>
    public static bool IsScoreboardRow(bool teamValid, IAgentOriginBase? origin, BasicCharacterObject? character)
        => teamValid && origin != null && character != null;

    /// <summary>
    /// Whether the creature scoreboard bridge credits a kill. The observer credits a death or a knockout only when both
    /// agents are human (<c>BattleObserverMissionLogic.cs:72-75</c>); the bridge adds the credit whenever a troop on
    /// either end is a creature bandit, and never repeats vanilla's.
    /// </summary>
    public static bool ScoreboardBridgeCreditsKill(bool victimHuman, bool victimCreature, bool affectorHuman,
        bool affectorCreature, AgentState state)
        => (state == AgentState.Killed || state == AgentState.Unconscious)
           && (victimHuman || victimCreature) && (affectorHuman || affectorCreature)
           && (victimCreature || affectorCreature);

    /// <summary>
    /// One new brood a day while the clan is under its cap, so the broods return gradually after losses; none while the
    /// MCM switch is off (live broods stay).
    /// </summary>
    public static int BroodsToSpawnToday(int existingBroods, int maxBroods, bool enabled)
        => enabled && existingBroods < maxBroods ? 1 : 0;

    /// <summary>
    /// The living kingdoms owed a troll band today (#694): those no band counts for, while there are fewer bands than
    /// living kingdoms; none while the MCM switch is off. <paramref name="bandKingdomIds"/> holds one entry per band, the
    /// kingdom its home settlement belongs to now, or null for a home outside any kingdom: such a band, or one a capture
    /// moved to another kingdom, still counts toward the cap, so its old kingdom waits until a band dies. The spawner
    /// adds one band a day, near a kingdom drawn from this list.
    /// </summary>
    public static IReadOnlyList<string> KingdomsOwedATrollBand(IReadOnlyList<string> livingKingdomIds,
        IReadOnlyList<string?> bandKingdomIds, bool enabled)
    {
        if (!enabled || bandKingdomIds.Count >= livingKingdomIds.Count) return System.Array.Empty<string>();
        var covered = new HashSet<string?>(bandKingdomIds, System.StringComparer.Ordinal);
        return livingKingdomIds.Where(id => !covered.Contains(id)).ToList();
    }

    /// <summary>
    /// A freed prisoner's winner chances without the refused winners (#694: a brood or troll band takes no freed
    /// prisoner, so it stays spiders or trolls only). The engine draws one winner per prisoner from chances summing to 1
    /// and frees the prisoner when the draw falls past the list, so the kept winners share the refused ones' chance;
    /// with none kept, the prisoner goes free. Null when nothing is refused, so the engine's own list stands.
    /// </summary>
    public static List<KeyValuePair<T, float>>? WithoutRefusedWinners<T>(IReadOnlyList<KeyValuePair<T, float>> chances,
        System.Func<T, bool> refused)
    {
        if (!chances.Any(c => refused(c.Key))) return null;
        var kept = chances.Where(c => !refused(c.Key)).ToList();
        float total = kept.Sum(c => c.Value);
        return total > 0f
            ? kept.Select(c => new KeyValuePair<T, float>(c.Key, c.Value / total)).ToList()
            : new List<KeyValuePair<T, float>>();
    }

    /// <summary>A brood or troll band that chased or fought its way off its patrol gets the order back, unless it is still in a battle.</summary>
    public static bool NeedsPatrolOrder(bool hasHome, bool inBattle, bool isPatrolling) => hasHome && !inBattle && !isPatrolling;

    /// <summary>
    /// The spider Monster carries the horse's flight flags; a creature bandit stands and fights. CanRear too: a blow
    /// carrying MakesRear rears a CanRear mount (v1.5.3 Mission.cs:5644-5646), which interrupts a bite, and nobody
    /// rides a creature bandit for a rear to throw off.
    /// </summary>
    public static AgentFlag WithoutFlightFlags(AgentFlag flags)
        => flags & ~(AgentFlag.RunsAwayWhenHit | AgentFlag.CanWander | AgentFlag.CanGetScared | AgentFlag.CanRear);

    /// <summary>
    /// Route A, the flags for the native creation call only. The engine allocates an agent's weapon state when, and
    /// only when, CanWieldWeapon is in the creation flags (v1.5.3 native FUN_1805b89c0), and a soldier's target
    /// scorer reads that state without a null check. A humanoid already has what it needs and is left alone.
    /// </summary>
    public static AgentFlag CreationFlags(AgentFlag flags)
        => (flags & AgentFlag.IsHumanoid) == 0 ? flags | AgentFlag.CanWieldWeapon : flags;

    /// <summary>
    /// Route A, the flags from the managed agent onward: CanWieldWeapon stripped again before the build. Left live it
    /// sends the creature into the unarmed melee lookup, whose actions its action set does not have (a native index
    /// used unchecked), and managed hit code into its null Equipment.
    /// </summary>
    public static AgentFlag BuildFlags(AgentFlag flags) => flags & ~AgentFlag.CanWieldWeapon;

    /// <summary>
    /// Route A, the fighting flags after the build: the flight flags and Mountable cleared. Native IsEnemy reads a
    /// Mountable agent's side from its rider, so a riderless one is nobody's enemy and soldiers never pick it; without
    /// Mountable it reads the creature's own team. CanWieldWeapon, CanDefend and CanAttack are never touched, so the
    /// native AI reset those bits trigger never runs.
    /// </summary>
    public static AgentFlag CombatantFlags(AgentFlag flags) => WithoutFlightFlags(flags) & ~AgentFlag.Mountable;

    /// <summary>
    /// Whether the native weapon state exists, read without a dereference: the managed wield-index pointer is the
    /// block's address plus 0xEE0, so a missing block leaves the bare offset and a cleared agent leaves 0.
    /// </summary>
    public static bool IsWeaponStateAllocated(ulong primaryWieldIndexPointer) => primaryWieldIndexPointer >= 0x10000;

    /// <summary>
    /// Whether to unmount the creature, or why not. Every skip leaves today's untargetable but safe creature:
    /// no weapon state (the scorer would crash), a live CanWieldWeapon (the unarmed lookup would), no team (vanilla
    /// team AI throws on a teamless non-mount), or a pelvis bone that does not resolve (soldier aim spins on it).
    /// </summary>
    public static CreatureRouteA RouteA(bool weaponState, bool weaponFlagLive, bool teamSet, bool pelvisBoneResolves)
    {
        if (!weaponState) return CreatureRouteA.SkippedNoWeaponState;
        if (weaponFlagLive) return CreatureRouteA.SkippedWeaponFlagLive;
        if (!teamSet) return CreatureRouteA.SkippedNoTeam;
        return pelvisBoneResolves ? CreatureRouteA.On : CreatureRouteA.SkippedNoPelvisBone;
    }
}

public enum CreatureRouteA
{
    On,
    SkippedNoWeaponState,
    SkippedWeaponFlagLive,
    SkippedNoTeam,
    SkippedNoPelvisBone,
}
