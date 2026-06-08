using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elephant;

/// <summary>
/// Drives the war-elephant auto-trample — a behavioral 1-for-1 of ADOD_Beasts'
/// <c>ADODBeastsElephantAgentComponent.OnTickAsAI</c>, re-implemented for v1.4.5. An AI-ridden elephant
/// occasionally plays an attack animation and deals a radial knockdown trample to enemies within
/// <see cref="ElephantConfig.TrampleRadius"/> of the elephant's own position. The pure gate + damage logic is in <see cref="IElephantAttackService"/> (unit-tested).
///
/// This is a THIN boundary: unlike <see cref="TAOM.Features.Warg.WargMissionBehavior"/> (which routes engine work
/// through <c>IAgentAdapter</c>), the radial scan + damage here run directly against the sealed <see cref="Agent"/>
/// in the entry point — a deliberate thin-boundary choice for the 1-for-1 baseline; extracting an adapter-backed
/// trample service is the "improve" step. Damage is 1-for-1 ADOD; the impulse uses TAOM's clean
/// <see cref="CustomAttacksUtils"/> (Pierce damage-type, knockdown/dismount on non-blocking victims). Two impulse-only
/// deviations from ADOD remain as "improve" items (damage amount is identical): ADOD also applied a KnockBack stagger
/// to BLOCKING victims and dismounted blocking-mounted victims. Tracks elephant agents in a shadow list with fail-soft
/// per-error logging. Player-ridden manual trample is also deferred to "improve" — ADOD's AI path is the baseline.
/// </summary>
public class ElephantMissionBehavior : MissionLogic
{
    // ADOD uses act_elephant_attack_1..3; ours are renamed act_war_elephant_* (authored in the action set) so
    // we carry no runtime dependency on ADOD. If the codes are absent the cache resolves to act_none — the
    // trample damage still lands, only the attack animation is skipped (cosmetic).
    private static readonly ActionIndexCache[] AttackAnimations =
    {
        ActionIndexCache.Create("act_war_elephant_attack_1"),
        ActionIndexCache.Create("act_war_elephant_attack_2"),
        ActionIndexCache.Create("act_war_elephant_attack_3"),
    };

    private readonly IElephantAttackService _service;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    private readonly List<Agent> _elephants = new();
    // Reusable radial-scan buffer — Mission.GetNearbyAgents clears it each call; OnMissionTick is single-threaded.
    private readonly MBList<Agent> _trampleScratch = new();
    private bool _scanned;

    public ElephantMissionBehavior()
    {
        _service = IoC.Resolve<IElephantAttackService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        if (agent != null && _service.IsElephantMonster(agent.Monster?.StringId) && !_elephants.Contains(agent))
            _elephants.Add(agent);
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (!_scanned)
            {
                _scanned = true;
                foreach (Agent a in Mission.Current.AllAgents)
                    if (a != null && _service.IsElephantMonster(a.Monster?.StringId) && !_elephants.Contains(a))
                        _elephants.Add(a);
            }

            for (int i = _elephants.Count - 1; i >= 0; i--)
            {
                Agent elephant = _elephants[i];
                if (elephant == null || !elephant.IsActive())
                {
                    _elephants.RemoveAt(i);
                    continue;
                }
                TryAiTrample(elephant);
            }
        }
        catch (Exception ex)
        {
            string key = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[Elephant] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void TryAiTrample(Agent elephant)
    {
        Agent rider = elephant.RiderAgent;
        if (rider == null || rider == Agent.Main) return;          // AI-ridden only (1-for-1 ADOD)

        // Cheap per-frame pre-gate FIRST: the trample fires with probability TrampleChancePerTick (ADOD's 0.001),
        // so ~99.9% of frames short-circuit here BEFORE any native GetName round-trip (this runs
        // per frame per elephant). The SAME roll feeds ShouldAiTrample, so the gate stays a single 1-for-1 ADOD roll.
        float roll = MBRandom.RandomFloat;
        if (roll >= ElephantConfig.TrampleChancePerTick) return;

        if (!elephant.ActionSet.IsValid) return;

        // Use the elephant's own look direction to determine facing — the rider's AI target can be anywhere
        // on the battlefield (20-50+ units away) and is not a useful proximity check here.
        Agent target = rider.GetTargetAgent();
        Vec3 toTarget = target != null
            ? (target.Position - elephant.Position).NormalizedCopy()
            : elephant.LookDirection;
        float facingDot = Vec3.DotProduct(toTarget, elephant.LookDirection);
        // GetName() (v1.4.5; ActionIndexCache has no .Name property). Substring match — robust when the attack
        // action codes are absent (act_none), unlike an Index compare. See class header.
        bool alreadyAttacking = elephant.GetCurrentAction(0).GetName().Contains("attack");

        if (!_service.ShouldAiTrample(facingDot, roll, alreadyAttacking)) return;

        _logger.LogInfo($"[Elephant] Trample firing — rider={rider.Name} facingDot={facingDot:F2}");

        // ADOD's SetActionChannel arg list is exactly the engine defaults, so the 2-arg form is equivalent.
        elephant.SetActionChannel(0, AttackAnimations[MBRandom.RandomInt(AttackAnimations.Length)]);

        // Scan around the ELEPHANT's position (not the rider's distant AI target) for nearby enemies to trample.
        Mission.Current.GetNearbyAgents(elephant.Position.AsVec2, ElephantConfig.TrampleRadius, _trampleScratch);
        foreach (Agent victim in _trampleScratch)
        {
            if (victim == null || victim == elephant || !victim.IsActive() || !victim.IsEnemyOf(rider)) continue;
            // ADOD parity: only a SHIELD block reduces the trample; weapon parries take full damage.
            bool blocking = victim.GetCurrentActionType(1) == Agent.ActionCodeType.DefendShield;
            int damage = _service.ComputeInflictedDamage(blocking);
            _logger.LogInfo($"[Elephant] Trample hit: victim={victim.Name} blocking={blocking} dmg={damage}");
            CustomAttacksUtils.TakeDamage(victim, elephant, damage, ElephantConfig.TrampleBlowMagnitude, knockDown: !blocking);
        }
    }

    public override void OnRemoveBehavior()
    {
        _elephants.Clear();
        base.OnRemoveBehavior();
    }
}
