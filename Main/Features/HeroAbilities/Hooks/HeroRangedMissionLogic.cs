using System;
using System.Threading;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Features.SignatureStrikes.Hooks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// Mission entry point for the ranged hero kit (#1361, docs/features/hero-abilities.md): the extra arrows, the arrow's
/// kill roll and the check lines. It needs no gate of its own: only the gated host fills the roster
/// (HeroAbilityRuntime.Ranged), and every callback ignores a shooter the roster does not list. The missile callbacks
/// may run off the main thread (#634), so they only enqueue; the tick drains. A missile's removal goes into the same
/// queue, so the tallies forget its index after its own hits and before the next missile's on that index. The work is
/// <see cref="HeroMultiShotRunner"/>'s, <see cref="HeroArrowKills"/>'s and <see cref="HeroKeepDistance"/>'s (#1415), the verdicts
/// <see cref="HeroRangedChecks"/>'.
/// MUST be <c>: MissionLogic</c> (rca-looter-battle-nre-2026-05-24.md). Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class HeroRangedMissionLogic : MissionLogic
{
    private static int _missions;

    private readonly HeroAbilityRuntime _runtime;
    private readonly HeroMissileQueue _queue = new HeroMissileQueue();
    private readonly HeroMultiShotRunner _runner;
    private readonly HeroArrowKills _kills;
    private readonly HeroKeepDistance _keep = new HeroKeepDistance();
    // Character.StringId names the kit hero, as the host keys him.
    private readonly HeroRangedTallies<Agent> _tallies = new HeroRangedTallies<Agent>(hero => hero.Character?.StringId ?? "unknown");
    private readonly string _key;
    // Set on the main thread; the callbacks read it from any thread.
    private volatile bool _disabled;
    private float _nextSample;

    public HeroRangedMissionLogic(HeroAbilityRuntime runtime, SignatureInstantKill executor)
    {
        _runtime = runtime;
        _key = "hr" + Interlocked.Increment(ref _missions);
        _runner = new HeroMultiShotRunner(runtime.Logger);
        _kills = new HeroArrowKills(executor);
    }

    public override void AfterStart()
    {
        base.AfterStart();
        Mission.OnMissileRemovedEvent += QueueMissileRemoved;
    }

    public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
    {
        base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);
        var kit = _disabled || shooterAgent == null ? null : _runtime.Ranged.KitOf(shooterAgent);
        if (kit != null)
            _queue.Enqueue(HeroMissileEvent.Shot(shooterAgent!, kit, position, velocity));
    }

    public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
    {
        base.OnMissileHit(attacker, victim, isCanceled, collisionData);
        var kit = _disabled || attacker == null || victim == null ? null : _runtime.Ranged.KitOf(attacker);
        if (kit != null)
            _queue.Enqueue(HeroMissileEvent.Hit(attacker!, kit, victim, victim.Team, collisionData.AffectorWeaponSlotOrMissileIndex,
                LongDefeatVolleyHooks.MissileItem(Mission, collisionData.AffectorWeaponSlotOrMissileIndex) ?? "",
                collisionData.AttackBlockedWithShield, collisionData.MissileBlockedWithWeapon, collisionData.IsShieldBroken));
    }

    // Native hands a freed missile index to the next missile at once. A missile's hit callbacks come before its removal
    // (only Mission.OnMissileRemoved, Mission.cs:5761, takes it from the table they read), and the queue keeps that order.
    private void QueueMissileRemoved(int missileIndex)
    {
        if (!_disabled && !_runtime.Ranged.IsEmpty)
            _queue.Enqueue(HeroMissileEvent.Removed(missileIndex));
    }

    public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
    {
        base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData);
        var kit = _disabled || affectorAgent == null || affectedAgent == null || !blow.IsMissile ? null : _runtime.Ranged.KitOf(affectorAgent);
        if (kit != null)
            _queue.Enqueue(HeroMissileEvent.Body(affectorAgent!, kit, affectedAgent, affectedAgent.Team, attackCollisionData.AffectorWeaponSlotOrMissileIndex,
                affectorWeapon.Item?.StringId ?? "", attackCollisionData.AttackBlockedWithShield, attackCollisionData.MissileBlockedWithWeapon,
                attackCollisionData.VictimHitBodyPart == BoneBodyPartType.Head, blow.InflictedDamage));
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        MissionThreadGuard.MarkMainThread();
        if (_disabled)
            return;
        try
        {
            var now = Mission.CurrentTime;
            while (_queue.TryDequeue(out var e))
                Handle(in e, now);
            _keep.Tick(Mission, _runtime.Ranged, _tallies, now);
            if (now >= _nextSample)
            {
                _nextSample = now + HeroAbilityService.PassSeconds;
                Sample(now);
            }
        }
        catch (Exception ex)
        {
            _disabled = true;
            _queue.Clear();
            _runtime.Logger.LogError($"[HeroAbilities] ranged kit disabled for this mission after {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void OnRemoveBehavior()
    {
        Mission.OnMissileRemovedEvent -= QueueMissileRemoved;
        foreach (var tally in _tallies.All)
            HeroRangedChecks.WriteEnd(_runtime.Outcomes, _key, tally);
        _tallies.Clear();
        _queue.Clear();
        _runner.Clear();
        _keep.Clear();
        base.OnRemoveBehavior();
    }

    private void Handle(in HeroMissileEvent e, float now)
    {
        if (e.Kind == HeroMissileKind.Removed)
        {
            _tallies.MissileRemoved(e.MissileIndex);
            return;
        }
        var t = _tallies.Of(e.Hero, e.Kit);
        if (e.Kind == HeroMissileKind.Shot)
        {
            t.Shots++;
            if (_runtime.Bursts.TryGetValue(e.Hero, out var burst) && burst.Until > now)
                _runner.Fire(Mission, in e, burst.ExtraArrows, t);
            return;
        }
        var heroTeam = e.Hero.Team;
        if (heroTeam == null || e.VictimTeam == null || !heroTeam.IsEnemyOf(e.VictimTeam))
            return;
        if (e.Kind == HeroMissileKind.MissileHit)
            t.MissileHit(e.Victim, e.MissileIndex, e.IsExtraArrow, e.BlockedShield, e.BlockedWeapon, e.ShieldBroken);
        else
            _kills.OnBodyHit(in e, t);
    }

    // Once a second for each listed hero: the running extra-arrow window, and the precision check at the first sample
    // with a ranged weapon in hand. AgentDrivenProperties holds what the models wrote at the last refresh.
    private void Sample(float now)
    {
        foreach (var hero in _runtime.Ranged.Keys)
        {
            var t = _tallies.Of(hero, _runtime.Ranged.KitOf(hero));
            HeroKeepDistance.NoteWeapon(hero, t);
            if (_runtime.Bursts.TryGetValue(hero, out var burst) && burst.Until > now)
                t.NoteWindow(burst.Until);
            if (t.PrecisionWritten || !AgentSlotIdentity.IsCurrentOccupant(hero) || !hero.IsActive())
                continue;
            var p = hero.AgentDrivenProperties;
            if (p == null || hero.WieldedWeapon.CurrentUsageItem?.IsRangedWeapon != true)
                continue;
            t.PrecisionWritten = true;
            LongDefeatChecks.Record(_runtime.Outcomes, HeroRangedChecks.PrecisionId, HeroRangedChecks.KeyOf(_key, t),
                HeroRangedChecks.Precision(t.HeroId, t.ZeroAimError, HeroPrecisionApplier.CountNonZero(p), p.AiShooterError, p.WeaponInaccuracy));
        }
    }
}
