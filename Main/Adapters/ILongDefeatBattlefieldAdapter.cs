using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// All engine work on the Long Defeat field: the deployment, the siege engines, spawning, measuring, ordering and the player's
/// body. Takes and returns domain types only, so the director never sees an engine type (ADR-007). Every method but
/// <see cref="Classify"/> and <see cref="IsAttacker"/> runs on the main thread; those two are safe from the engine's callback
/// threads, since they read an agent's own team and character and write nothing.
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

    /// <summary>The scene's own defender reinforcement foot frame, used as the keep when nothing is recorded; null when the scene has none.</summary>
    LdPoint? KeepFromScene();

    /// <summary>Starts a batch of <paramref name="count"/> soldiers in the side's formation and places the formation from the deployment plan. For the first batch of a formation only.</summary>
    void PlaceFormation(FormationSlot slot, int count, bool attackers);

    /// <summary>Starts a batch without moving the formation's place, for a batch after the first.</summary>
    void BeginBatch(FormationSlot slot, int count, bool attackers);

    /// <summary>
    /// Spawns the batch's soldiers into the open batch of their formation with no position, so the engine's own layout slots apply,
    /// and reads where each landed in the same call.
    /// </summary>
    LdSpawnReport SpawnBatch(SoldierBatch batch, bool attackers);

    /// <summary>Where every standing human defender is now. Read before a defender batch spawns.</summary>
    IReadOnlyList<LdPoint> StandingDefenderPoints();

    /// <summary>Closes the batch of the side's formation. Called after the batch's last soldier.</summary>
    void EndBatch(FormationSlot slot, bool attackers);

    /// <summary>
    /// Spawns the player's lord. The first call, in the same tick as the garrison, joins the foot formation's open batch (the
    /// director sized it for one more) and the director closes the batch afterward. A respawn is its own batch on the reinforcement
    /// frame. False when the engine could not build the agent.
    /// </summary>
    bool SpawnPlayer(bool respawn);

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

    /// <summary>Kills up to <paramref name="count"/> standing attackers and returns how many fell.</summary>
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

    /// <summary>What a removed agent was. A kill and a knock-out are losses; a stranger, a horse or a team-less agent are <see cref="LdRemovalKind.None"/>.</summary>
    LdRemoval Classify(object affected, bool loss);

    /// <summary>Whether the agent is on the attacking team.</summary>
    bool IsAttacker(object? agent);

    /// <summary>Drops every handle the adapter holds. Safe to call twice.</summary>
    void Clear();
}
