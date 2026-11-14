using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// All engine work on the Long Defeat field: the deployment, the siege engines, spawning, measuring, ordering and the player's
/// body. Takes and returns domain types only, so the director never sees an engine type (ADR-007). Every method but
/// <see cref="Classify"/>, <see cref="IsAttacker"/>, <see cref="TryHitKeys"/>, <see cref="TryContact"/> and <see cref="NoteBodyEvent"/> runs on the main thread; those five are safe from the engine's callback
/// threads, since they read an agent's own team and character and write only into the role registry and the lord watch, which each hold one lock.
/// </summary>
internal interface ILongDefeatBattlefieldAdapter
{
    /// <summary>
    /// Gives the deployment plan its troop counts and builds the default plans. False, with <paramref name="problem"/> filled,
    /// when the mission has no teams, no deployment plan of the expected kind or no spawn frame for a class.
    /// </summary>
    bool Boot(LdPlanSizes sizes, out string problem);

    /// <summary>
    /// Breaks every castle gate that is not visible, parents included, and returns how many. A hidden gate never ticks, so it
    /// never opens and keeps a lane closed; it is broken through the destructible's hit path, never removed, because a null inner
    /// gate throws in the wall-assault behavior. Runs before <see cref="DeployEngines"/>.
    /// </summary>
    int BreakHiddenGates();

    /// <summary>Deploys the attacker's engines, sets their hit points, hides ladders a tower covers and disables every defender point.</summary>
    void DeployEngines();

    /// <summary>
    /// Brings back every ram and siege tower the boot deployed that has been destroyed since (#1160): full hit points, enabled, back at the start of its path and in its siege lane again, as vanilla's own
    /// mission reset leaves it. Returns each engine it brought back, read after the restore, and a problem when it could not act. Runs at each wave start, before the Hammer writes the engines' hit points.
    /// </summary>
    LdEngineRestore RestoreEngines();

    /// <summary>Every ram and siege tower the boot deployed, as it stands now.</summary>
    IReadOnlyList<LdEngineState> ReadEngines();

    /// <summary>A test option (<c>break_engines</c>): destroys every ram and siege tower the boot deployed that stands, through the destructible's own hit path, and returns how many fell.</summary>
    int BreakEngines();

    /// <summary>The scene's own defender reinforcement foot frame, used as the keep when nothing is recorded; null when the scene has none.</summary>
    LdPoint? KeepFromScene();

    /// <summary>Starts a batch of <paramref name="count"/> soldiers in the side's formation and places the formation from the deployment plan. For the first batch of a formation only.</summary>
    void PlaceFormation(FormationSlot slot, int count, bool attackers);

    /// <summary>Starts a batch without moving the formation's place, for a batch after the first.</summary>
    void BeginBatch(FormationSlot slot, int count, bool attackers);

    /// <summary>
    /// Moves the defender formation of <paramref name="slot"/> to the recorded muster place and sends the open batch's soldiers there, into
    /// the formation's next free slots. Call it right after <see cref="PlaceFormation"/> or <see cref="BeginBatch"/>. False, with nothing
    /// changed, when the place has no navmesh: the batch then spawns at the scene's own frame. <see cref="EndBatch"/> ends the soldiers' routing; a formation under Advance with no enemy alive keeps the place and walks there.
    /// </summary>
    bool SetMusterFrame(FormationSlot slot, MusterFrame frame);

    /// <summary>
    /// Spawns the batch's soldiers into the open batch of their formation with no position, so the engine's own layout slots apply,
    /// and reads where each landed in the same call. After a successful <see cref="SetMusterFrame"/> the read-back judges against that place.
    /// Each soldier's health is multiplied by <see cref="SoldierBatch.HealthFactor"/> after it spawns, and each attacker is entered in the role registry with the batch's <see cref="SoldierBatch.Role"/>.
    /// </summary>
    LdSpawnReport SpawnBatch(SoldierBatch batch, bool attackers);

    /// <summary>Where every standing human defender is now. Read before a defender batch spawns.</summary>
    IReadOnlyList<LdPoint> StandingDefenderPoints();

    /// <summary>Closes the batch of the side's formation. Called after the batch's last soldier.</summary>
    void EndBatch(FormationSlot slot, bool attackers);

    /// <summary>
    /// Spawns the player's lord. The first call, in the same tick as the garrison, joins the foot formation's open batch (the
    /// director sized it for one more) and the director closes the batch afterward. A respawn is its own batch on the reinforcement
    /// frame. With a <paramref name="loadout"/> he spawns in a copy of his first battle set with the loadout's slots overwritten, and
    /// without a horse; with null, in the set the engine picks for him. The kit is read back from the spawned body in this call. False when the engine could not build the agent.
    /// A mounted <paramref name="loadout"/> (spike S9) spawns him on a horse in the defenders' cavalry formation, not in the foot formation's batch, at <paramref name="place"/> with its direction
    /// when one is given (a position always needs a direction), else at the engine's own frame for that formation. The field remembers the body as the lord's own.
    /// </summary>
    bool SpawnPlayer(bool respawn, LdLoadout? loadout, LdPlace? place = null);

    /// <summary>
    /// What the lord who spawned last was spawned with, read from his spawn equipment for each slot of the loadout he was given, and whether he sat a horse. The facts the
    /// <c>class</c> check judges; <c>Spawned</c> is false when no lord was built.
    /// </summary>
    LdKitFacts ReadKit();

    /// <summary>Whether a point has a navmesh and lies inside the mission boundary: what a point the lord is put at needs. The boot line of the sally and home points is written from this.</summary>
    LdPointFacts SallyPointUsable(LdPlace place);

    /// <summary>
    /// The lord as the field reads him now: the main agent, whether that body is the one the field spawned for him, his horse, whether he is inside the boundary. With <paramref name="detail"/> also his
    /// formation and its order position, his distance to it, the nearest attacker and the gate, which cost a walk over the attackers and so are read only for a log line.
    /// </summary>
    LdLordFacts ReadLord(bool detail);

    /// <summary>Moves the main agent (and his horse) to the place and turns him to its facing. False when there is no main agent to move.</summary>
    bool TeleportPlayer(LdPlace place);

    /// <summary>
    /// A lord on foot is put on a horse at the place in one main-thread step: the old body is marked as a planned removal by identity, faded without its horse, and a new mounted body is spawned with the Player
    /// controller, which makes it the main agent as it builds. The old body's share of health is written onto the new one. The new body's stats are derived by the director when it adopts the body, since the horse is built before its rider.
    /// </summary>
    LdRemount RemountPlayer(LdPlace place, LdLoadout loadout);

    /// <summary>Tells the mission its initial spawn is done, so agents spawned later get their stats and ammunition.</summary>
    void FinishInitialSpawn();

    /// <summary>The wave's first batch is out: force the engines on and give the attackers a fresh breach tactic.</summary>
    void RefreshAttackerTactic();

    /// <summary>Both teams charge, and the defenders' formations are handed back to AI first.</summary>
    void OrderCharge();

    /// <summary>The defenders get a new defend-the-castle tactic after a charge.</summary>
    void RestoreDefenderTactic();

    /// <summary>Every standing defender regains <paramref name="share"/> of the health they are missing.</summary>
    LdRest RestDefenders(float share);

    /// <summary>
    /// Marks up to <paramref name="count"/> standing attackers as felled in the registry, kills them, and returns how many fell. The removal the engine raises for each is booked as felled. Every kill has a living
    /// owner: the player's body while it stands, else a standing defender, whose kill count and morale gain a felled straggler would give the player's body anyway; with no defender standing nobody is felled.
    /// </summary>
    int Fell(int count);

    /// <summary>Counts the field. The keep counts are read only when <paramref name="countKeep"/> is set.</summary>
    LdReading Read(LdPoint? keep, float radius, float heightBand, bool countKeep);

    /// <summary>The positions of up to eight standing attackers nearest <paramref name="keep"/>, nearest first; in no order with no keep.</summary>
    IReadOnlyList<LdPoint> AttackersNearest(LdPoint? keep);

    /// <summary>
    /// Makes the player's body the team's general and the owner of its orders, then hands every formation to AI, so an order
    /// still takes one back. With <paramref name="aiDriven"/> the agent itself is also driven by AI (the unattended run).
    /// </summary>
    void AdoptMain(bool aiDriven);

    /// <summary>The facts the boot and general checks judge, read now.</summary>
    LdBootFacts BootFacts();

    /// <summary>One line of siege-engine state for the log.</summary>
    string DescribeEngines();

    /// <summary>One line about the player's body for the log: who owns the orders, the controller and where he stands. "main=none" with no body.</summary>
    string DescribePlayer();

    /// <summary>One fragment for the log: how many of the defenders' formations are AI controlled, as <c>ai_formations=N of M</c>.</summary>
    string DescribeFormations();

    /// <summary>
    /// What a removed agent was. A kill and a knock-out are losses; a stranger, a horse or a team-less agent are <see cref="LdRemovalKind.None"/>. An attacker comes with the role and
    /// felled mark the registry kept for him, which this call takes out of it; the commander is also reported when removed with no loss.
    /// </summary>
    LdRemoval Classify(object affected, bool loss);

    /// <summary>
    /// Sorts one hit into sally contact: the lord's blow or his horse's on an attacker, an attacker's blow on the lord or on his horse. False for anything else. <paramref name="victimHealth"/> is the victim's health after
    /// the blow. Any thread.
    /// </summary>
    bool TryContact(object? affected, object? affector, float victimHealth, out LdContact contact);

    /// <summary>
    /// A removal or deletion reached an agent (<paramref name="callback"/> is "removed" or "deleted", <paramref name="state"/> the engine's state name). Kept when it is the lord's body, the body a remount faded or the
    /// main agent; the director logs it on the main thread. Any thread.
    /// </summary>
    void NoteBodyEvent(object agent, string callback, string state);

    /// <summary>The body events kept so far, once, in order. Main thread.</summary>
    IReadOnlyList<LdBodyEvent> TakeBodyEvents();

    /// <summary>Whether the agent is on the attacking team.</summary>
    bool IsAttacker(object? agent);

    /// <summary>
    /// The registry keys of one hit: <paramref name="victim"/> is a standing human defender (entered in the registry on its first hit, and evicted at its removal), <paramref name="attacker"/> the attacker who
    /// struck it, which the field registered when it spawned him. False when the hit is not an attacker's blow on a defender, or when the registry no longer holds the attacker. Any thread.
    /// </summary>
    bool TryHitKeys(object? affected, object? affector, out AgentKey victim, out AgentKey attacker);

    /// <summary>
    /// The defenders an upgrade may replace (#1161), counted by troop id without case: live humans on foot of the defending team in a formation, not the player or the lord, not a hero, not
    /// running away, not working an engine and not on a tower post. Main thread.
    /// </summary>
    IReadOnlyDictionary<string, int> StandingDefendersByTroop();

    /// <summary>
    /// Replaces up to <paramref name="count"/> of those defenders of <paramref name="fromTroop"/> with soldiers of <paramref name="toTroop"/>: each new soldier at the old one's place, facing and
    /// formation, its health limit scaled by <paramref name="healthFactor"/> and holding the old one's share of health; the old one is faded, which the run reads as no loss. Main thread.
    /// </summary>
    LdUpgradeReport ReplaceDefenders(string fromTroop, string toTroop, int count, float healthFactor);

    /// <summary>Drops every handle the adapter holds. Safe to call twice.</summary>
    void Clear();
}
