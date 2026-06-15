using System;
using System.Reflection;
using TAOM.Core.Validation;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.MountAndBlade.Agent;

namespace TAOM.Features.AdvancedCombat;

public delegate void RegisterBlowDelegate(Mission mission, Agent attacker, Agent victim, WeakGameEntity realHitEntity, Blow b, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, ref CombatLogData combatLogData);

public class CustomAttacksUtils
{
    public static BlowDirection GetDirectionOfBlow(Agent victim, Vec3 blowOrigin)
    {
        Vec3 victimLookDirection = victim.GetMovementDirection().ToVec3();
        Vec3 victimToBlowOrigin = (blowOrigin - victim.Position).NormalizedCopy();
        float angleInRadians = (float)Math.Atan2(
            victimLookDirection.x * victimToBlowOrigin.y - victimLookDirection.y * victimToBlowOrigin.x,
            victimLookDirection.x * victimToBlowOrigin.x + victimLookDirection.y * victimToBlowOrigin.y);
        float angleInDegrees = (float)(angleInRadians * (180f / Math.PI));

        if (angleInDegrees < -135 && angleInDegrees > -180)
            return BlowDirection.Back;
        else if (angleInDegrees < -45 && angleInDegrees > -135)
            return BlowDirection.Right;
        else if (angleInDegrees < 45 && angleInDegrees > -45)
            return BlowDirection.Front;
        else if (angleInDegrees > 45 && angleInDegrees < 135)
            return BlowDirection.Left;
        else
            return BlowDirection.Back;
    }

    private static readonly RegisterBlowDelegate _registerBlow;
    private static readonly bool _initializationFailed;
    private static readonly string _initializationError;

    static CustomAttacksUtils()
    {
        try
        {
            var parameterTypes = new Type[]
            {
                typeof(Agent),
                typeof(Agent),
                typeof(WeakGameEntity),
                typeof(Blow),
                typeof(AttackCollisionData).MakeByRefType(),
                typeof(MissionWeapon).MakeByRefType(),
                typeof(CombatLogData).MakeByRefType()
            };

            MethodInfo blowMethod = typeof(Mission).GetMethod(
                "RegisterBlow",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);

            if (blowMethod == null)
            {
                _initializationFailed = true;
                _initializationError = "Mission.RegisterBlow method not found with expected signature";
                TaleWorlds.Library.Debug.Print($"[TAOM] CustomAttacksUtils: {_initializationError}", 0, TaleWorlds.Library.Debug.DebugColor.Red);
                return;
            }

            _registerBlow = (RegisterBlowDelegate)Delegate.CreateDelegate(typeof(RegisterBlowDelegate), null, blowMethod);
            TaleWorlds.Library.Debug.Print("[TAOM] CustomAttacksUtils: RegisterBlow delegate created successfully", 0, TaleWorlds.Library.Debug.DebugColor.Green);
        }
        catch (Exception ex)
        {
            _initializationFailed = true;
            _initializationError = $"Failed to create RegisterBlow delegate: {ex.Message}";
            TaleWorlds.Library.Debug.Print($"[TAOM] CustomAttacksUtils: {_initializationError}", 0, TaleWorlds.Library.Debug.DebugColor.Red);
        }
    }

    public static void RegisterBlow(Agent attacker, Agent victim, WeakGameEntity realHitEntity, Blow b, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, ref CombatLogData combatLogData)
    {
        if (_initializationFailed || _registerBlow == null)
        {
            TaleWorlds.Library.Debug.Print($"[TAOM] CustomAttacksUtils.RegisterBlow: Skipped - initialization failed: {_initializationError ?? "delegate is null"}", 0, TaleWorlds.Library.Debug.DebugColor.Yellow);
            return;
        }
        _registerBlow(Mission.Current, attacker, victim, realHitEntity, b, ref collisionData, in attackerWeapon, ref combatLogData);
    }

    public static void TakeDamage(Agent victim, int damage, float magnitude = 50f, bool knockDown = false)
    {
        TakeDamage(victim, victim, damage, magnitude, knockDown);
    }

    public static void TakeDamage(Agent victim, Agent attacker, int damage, float magnitude = 50f, bool knockDown = false)
    {
        if (victim == null || attacker == null) return;

        // Re-validate LIVE state at call time. The bone-collision callback fires several frames
        // after the CustomAttack sweep captured these agents, so the t-0 Health guard is stale —
        // an agent can despawn in the interim, and a despawning agent's native pointer is itself a
        // prime candidate for the 0x3 AV (spider auto-bite crash RCA 2026-06-14). Additive: a
        // healthy blow (every warg bite, every clean spider bite) passes unchanged.
        if (!victim.IsActive() || victim.IsFadingOut() || victim.Index < 0 || victim.Health <= 0) return;
        if (!attacker.IsActive() || attacker.IsFadingOut() || attacker.Index < 0) return;

        Blow blow = new(attacker.Index)
        {
            DamageType = DamageTypes.Pierce,
            BoneIndex = victim.Monster.HeadLookDirectionBoneIndex,
            GlobalPosition = (attacker.Position + victim.Position) * 0.5f
        };
        blow.GlobalPosition.z += victim.GetEyeGlobalHeight();
        blow.BaseMagnitude = magnitude;
        blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
        blow.InflictedDamage = damage;
        blow.SwingDirection = victim.LookDirection;
        MatrixFrame frame = victim.Frame;
        blow.SwingDirection = frame.rotation.TransformToParent(new Vec3(-1f, 0f, 0f, -1f));
        blow.SwingDirection.Normalize();
        blow.Direction = blow.SwingDirection;
        blow.DamageCalculated = true;
        if (knockDown)
        {
            if (victim.HasMount) blow.BlowFlag |= BlowFlags.CanDismount;
            else blow.BlowFlag |= BlowFlags.KnockDown;
        }

        // Native-boundary geometry guard (spider auto-bite crash RCA 2026-06-14). GlobalPosition /
        // SwingDirection / BaseMagnitude are the ONLY TAOM-supplied floats that reach native code
        // unguarded — Mission.MakeSound + Mission.OnAgentHit place them into native spatial
        // structures. A NaN/Inf (e.g. Normalize() of a near-zero direction when the spider is caught
        // mid-jump / river-crossing — frames the battle-proven warg never hits) corrupts that native
        // state; a later Mission.Tick walks it and AVs reading 0x3. Reject before it reaches native.
        if (!IsBlowGeometrySafe(blow.GlobalPosition, blow.SwingDirection, blow.BaseMagnitude))
        {
            ReportSkippedNonFiniteBlow();
            return;
        }

        // Bone indices below are NOT re-clamped: blow.BoneIndex (victim) + mainHandItemBoneIndex
        // (attacker) come straight from engine-validated Monster fields (DeserializeBoneIndex with
        // validateHasParentBone:true) and the native side guards `boneIndex >= 0`
        // (Agent.GetProtectorArmorMaterialOfBone). -1 ("no such bone") is a handled sentinel, not a
        // crash. A no-op clamp here would be scaffolding — the finiteness guard above is the real fix.
        sbyte mainHandItemBoneIndex = attacker.Monster.MainHandItemBoneIndex;
        // The 3 positional ints below map to (affectorWeaponSlotOrMissileIndex,
        // StrikeType, DamageType). DamageType MUST match blow.DamageType — vanilla's
        // combat-log message generator reads the collision data's DamageType, not the
        // Blow's, so a mismatch makes hits read "Blunt" in the in-game log even when
        // the Blow correctly says Pierce. LOTRAOM had this set to `2` (Blunt), which
        // was a long-standing bug that only became visible once warg bites started
        // landing reliably (post-cc9e0c4). See plan file 2026-05-27.
        AttackCollisionData attackCollisionDataForDebugPurpose = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
            false, false, false, true, false, false, false, false, false, false, false, false,
            CombatCollisionResult.StrikeAgent,
            -1, 0, (int)DamageTypes.Pierce,
            blow.BoneIndex,
            BoneBodyPartType.Abdomen,
            mainHandItemBoneIndex,
            UsageDirection.AttackLeft,
            -1,
            CombatHitResultFlags.NormalHit,
            0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f,
            Vec3.Up,
            blow.Direction,
            blow.GlobalPosition,
            Vec3.Zero,
            Vec3.Zero,
            victim.Velocity,
            Vec3.Up
        );

        CombatLogData combatLogData = new(false, attacker.IsHuman, attacker.IsMine, attacker.RiderAgent != null, attacker.RiderAgent != null && attacker.RiderAgent.IsMine, attacker.IsMount, victim.IsHuman, victim.IsMine, victim.Health <= 0f, victim.RiderAgent != null, victim.RiderAgent != null && victim.RiderAgent.IsMine, victim.IsMount, null, victim.RiderAgent == victim, knockDown, false, 0f);
        MissionWeapon weapon = MissionWeapon.Invalid;
        RegisterBlow(attacker, victim, WeakGameEntity.Invalid, blow, ref attackCollisionDataForDebugPurpose, in weapon, ref combatLogData);
    }

    private static long _nonFiniteBlowSkips;

    /// <summary>
    /// True when a synthetic blow's geometry is safe to hand to the native blow processor: every
    /// component of <paramref name="globalPosition"/> and <paramref name="swingDirection"/> is finite,
    /// and <paramref name="magnitude"/> is finite and non-negative. A non-finite value here (produced
    /// when an attacker/victim is caught in a transitional frame and Vec3.Normalize() of a near-zero
    /// direction yields NaN) corrupts native spatial structures via MakeSound / OnAgentHit, which a
    /// later Mission.Tick walks → AccessViolation reading 0x3. Pure + unit-tested.
    /// </summary>
    public static bool IsBlowGeometrySafe(Vec3 globalPosition, Vec3 swingDirection, float magnitude) =>
        FiniteFloatValidator.IsFinite(globalPosition.x)
        && FiniteFloatValidator.IsFinite(globalPosition.y)
        && FiniteFloatValidator.IsFinite(globalPosition.z)
        && FiniteFloatValidator.IsFinite(swingDirection.x)
        && FiniteFloatValidator.IsFinite(swingDirection.y)
        && FiniteFloatValidator.IsFinite(swingDirection.z)
        && FiniteFloatValidator.IsFiniteAtLeast(magnitude, 0f);

    // Sample-gated — a transitional-frame storm across many creatures must not flood the log from
    // this per-bite hot path (the C++-port hot-path-logging discipline applied to managed code).
    private static void ReportSkippedNonFiniteBlow()
    {
        long n = System.Threading.Interlocked.Increment(ref _nonFiniteBlowSkips);
        if (n == 1 || n % 100 == 0)
            TaleWorlds.Library.Debug.Print(
                $"[TAOM] CustomAttacksUtils: skipped synthetic blow with non-finite geometry (total skipped: {n}).",
                0, TaleWorlds.Library.Debug.DebugColor.Yellow);
    }
}
