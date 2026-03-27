using System;
using System.Reflection;
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
        if (victim == null || attacker == null || victim.Health <= 0) return;

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

        sbyte mainHandItemBoneIndex = attacker.Monster.MainHandItemBoneIndex;
        AttackCollisionData attackCollisionDataForDebugPurpose = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
            false, false, false, true, false, false, false, false, false, false, false, false,
            CombatCollisionResult.StrikeAgent,
            -1, 0, 2,
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
}
