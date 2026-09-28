using System.Threading;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Diagnostics;

/// <summary>
/// Turns an engine callback about a creature into counters and a queued event (#692). Runs on whatever thread the
/// engine chose (#634): reads only managed values and flag pointers (the same reads CreatureBanditAgents.Is makes
/// on worker threads), bumps Interlocked counters, and enqueues; never logs INFO, never keeps an Agent, never runs the
/// localizer (a name travels as its TextObject). The engine's behavior loops are unguarded, so a throw here would skip
/// every later behavior's callback: each capture swallows its own failure.
/// </summary>
internal static class CreatureDiagCapture
{
    private static float Now => Mission.Current?.CurrentTime ?? float.NaN;
    private static int ThreadId => Thread.CurrentThread.ManagedThreadId;

    internal static string TroopOf(Agent? agent)
    {
        if (agent == null) return "-";
        if (agent.Character != null) return agent.Character.StringId;
        return agent.IsMount ? "mount:" + (agent.Monster?.StringId ?? "?") : "-";
    }

    private static bool IsPlayer(Agent? agent) => agent != null && (agent.IsMainAgent || agent.RiderAgent?.IsMainAgent == true);

    internal static void Hit(int victimSerial, int attackerSerial, Agent affected, Agent? affector, in Blow blow,
        in AttackCollisionData collision)
    {
        try { HitCore(victimSerial, attackerSerial, affected, affector, in blow, in collision); }
        catch { /* a diagnostic never breaks the engine's hit loop */ }
    }

    private static void HitCore(int victimSerial, int attackerSerial, Agent affected, Agent? affector, in Blow blow,
        in AttackCollisionData collision)
    {
        bool main = MissionThreadGuard.IsOnMainThread;
        CreatureBanditDiag.NoteCallbackThread("OnAgentHit");
        int damage = blow.InflictedDamage;
        bool missile = blow.IsMissile;
        bool blocked = collision.AttackBlockedWithShield;
        bool charge = collision.IsHorseCharge;

        var victim = CreatureBanditDiag.Ledger.Get(victimSerial);
        if (victim != null)
        {
            bool byPlayer = IsPlayer(affector);
            // Aimed: the soldier was targeting a creature at the last census (a managed lookup, safe off-thread).
            bool aimed = affector != null && CreatureBanditDiag.SoldiersAimingAtCreatures.ContainsKey(affector);
            Interlocked.Increment(ref victim.HitsTaken);
            Interlocked.Add(ref victim.DamageTaken, damage);
            Interlocked.Increment(ref missile ? ref victim.MissileHitsTaken : ref victim.MeleeHitsTaken);
            if (charge) Interlocked.Increment(ref victim.ChargeHitsTaken);
            if (byPlayer) Interlocked.Increment(ref victim.PlayerHitsTaken);
            if (blocked) Interlocked.Increment(ref victim.BlockedHitsTaken);
            if (aimed) Interlocked.Increment(ref victim.AimedHitsTaken);
            CreatureBanditDiag.Enqueue(new CreatureDiagEvent(CreatureDiagEventKind.HitTaken, victimSerial, Now, ThreadId, main,
                TroopOf(affector), affector?.NameTextObject, affector?.Index ?? -1, affector?.IsMount ?? false, byPlayer, damage,
                missile, blocked, charge, (int)blow.WeaponRecord.WeaponClass, healthAfter: affected.Health, aimed: aimed));
        }

        var attacker = CreatureBanditDiag.Ledger.Get(attackerSerial);
        if (attacker != null)
        {
            // A bite is our synthetic blow ([ThreadStatic], valid on this callback's own thread); anything else the
            // creature is credited with (a charge collision) is a native hit, counted apart so bite balance stays clean.
            bool synthetic = CustomAttacksUtils.IsRegisteringSyntheticBlow;
            if (synthetic)
            {
                Interlocked.Increment(ref attacker.BitesLanded);
                Interlocked.Add(ref attacker.DamageDealt, damage);
            }
            else
            {
                Interlocked.Increment(ref attacker.NativeHitsDealt);
                Interlocked.Add(ref attacker.NativeDamageDealt, damage);
            }
            CreatureBanditDiag.Enqueue(new CreatureDiagEvent(CreatureDiagEventKind.BiteLanded, attackerSerial, Now, ThreadId, main,
                TroopOf(affected), affected.NameTextObject, affected.Index, affected.IsMount, IsPlayer(affected), damage, missile,
                blocked, charge, detail: synthetic ? "synthetic" : "native", healthAfter: affected.Health));
        }
    }

    /// <summary>A soldier climbed onto a creature: every creature-bandit rule stops applying to it (#692).</summary>
    internal static void Mounted(int serial, Agent rider)
    {
        try
        {
            CreatureBanditDiag.NoteCallbackThread("OnAgentMount");
            CreatureBanditDiag.Enqueue(new CreatureDiagEvent(CreatureDiagEventKind.Mounted, serial, Now, ThreadId,
                MissionThreadGuard.IsOnMainThread, TroopOf(rider), rider.NameTextObject, rider.Index, byPlayer: IsPlayer(rider)));
        }
        catch { /* a diagnostic never breaks the engine callback */ }
    }

    internal static void Removed(int victimSerial, int killerSerial, Agent affected, Agent? affector, AgentState state,
        in KillingBlow blow)
    {
        try { RemovedCore(victimSerial, killerSerial, affected, affector, state, in blow); }
        catch { /* a diagnostic never breaks the engine's removal loop */ }
    }

    private static void RemovedCore(int victimSerial, int killerSerial, Agent affected, Agent? affector, AgentState state,
        in KillingBlow blow)
    {
        bool main = MissionThreadGuard.IsOnMainThread;
        CreatureBanditDiag.NoteCallbackThread("OnAgentRemoved");
        if (victimSerial > 0)
            CreatureBanditDiag.Enqueue(new CreatureDiagEvent(CreatureDiagEventKind.Removed, victimSerial, Now, ThreadId, main,
                TroopOf(affector), affector?.NameTextObject, affector?.Index ?? -1, affector?.IsMount ?? false, IsPlayer(affector),
                blow.InflictedDamage, blow.IsMissile, weaponClass: blow.WeaponClass, detail: state.ToString(),
                healthAfter: affected.Health));

        var killer = CreatureBanditDiag.Ledger.Get(killerSerial);
        if (killer != null && (state == AgentState.Killed || state == AgentState.Unconscious))
        {
            Interlocked.Increment(ref killer.Kills);
            CreatureBanditDiag.Enqueue(new CreatureDiagEvent(CreatureDiagEventKind.KilledByCreature, killerSerial, Now, ThreadId, main,
                TroopOf(affected), affected.NameTextObject, affected.Index, affected.IsMount, IsPlayer(affected), detail: state.ToString()));
        }
    }

    internal static void Simple(CreatureDiagEventKind kind, int serial, string site, string detail = "-")
    {
        try
        {
            CreatureBanditDiag.NoteCallbackThread(site);
            var record = CreatureBanditDiag.Ledger.Get(serial);
            if (record != null && kind == CreatureDiagEventKind.Panicked) Interlocked.Increment(ref record.Panicked);
            if (record != null && kind == CreatureDiagEventKind.Fled) Interlocked.Increment(ref record.Fled);
            CreatureBanditDiag.Enqueue(new CreatureDiagEvent(kind, serial, Now, ThreadId, MissionThreadGuard.IsOnMainThread, detail: detail));
        }
        catch { /* a diagnostic never breaks the engine callback */ }
    }
}
