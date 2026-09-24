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

    /// <summary>"No bone" sentinel for synthetic blows — see the bone-index comment in TakeDamage.</summary>
    private const sbyte NoBoneIndex = -1;

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
        using (EnterSyntheticBlow())
            _registerBlow(Mission.Current, attacker, victim, realHitEntity, b, ref collisionData, in attackerWeapon, ref combatLogData);
    }

    // The flag belongs to the thread registering the blow, which is the thread the engine raises its hit callbacks on.
    [ThreadStatic] private static int _syntheticBlowDepth;

    /// <summary>
    /// True while <see cref="RegisterBlow"/> is registering one of TAOM's blows. The engine raises its hit callbacks
    /// synchronously inside that call (v1.5.3 <c>Mission.RegisterBlow</c>, <c>Agent.RegisterBlow</c>, <c>HandleBlow</c>,
    /// <c>Mission.OnAgentHit</c>, then every behavior's <c>OnScoreHit</c>), so a listener can tell a blow TAOM wrote
    /// directly, past the damage model, from one the engine computed.
    /// </summary>
    public static bool IsRegisteringSyntheticBlow => _syntheticBlowDepth > 0;

    /// <summary>Marks the calling thread as registering a TAOM blow until the scope is disposed. Scopes nest.</summary>
    public static SyntheticBlowScope EnterSyntheticBlow()
    {
        _syntheticBlowDepth++;
        return new SyntheticBlowScope(entered: true);
    }

    /// <summary>The scope <see cref="EnterSyntheticBlow"/> returns; a default instance disposes as a no-op.</summary>
    public readonly struct SyntheticBlowScope : IDisposable
    {
        private readonly bool _entered;

        internal SyntheticBlowScope(bool entered) => _entered = entered;

        public void Dispose()
        {
            if (_entered) _syntheticBlowDepth--;
        }
    }

    public static void TakeDamage(Agent victim, int damage, float magnitude = 50f, bool knockDown = false)
    {
        TakeDamage(victim, victim, damage, magnitude, knockDown);
    }

    /// <param name="damageType">The blow's damage type: Pierce unless the caller asks otherwise, and only the antler attacks do
    /// (Blunt: the great elk's, #636, and the Animalia elk's and moose's, #646). The damage is written directly, so armour never reduces it whatever the type. The type
    /// sets the combat log's wording and, for Blunt, the killed-or-wounded rule, which <see cref="ComposeWeaponFlags"/>
    /// keeps lethal.</param>
    /// <param name="chargeImpactSound">Play a creature's charge impact instead of the sound the engine picks from the
    /// owner. The engine gives a weaponless blow the charge-damage sound only when its owner is not humanoid
    /// (<c>BlowWeaponRecord.GetHitSound</c>), and a rider owns the elephant-like creatures' blows since #643, so they
    /// ask for it (Mike, 2026-09-23). The warg, spider and signature strikes keep the engine's choice.</param>
    public static void TakeDamage(Agent victim, Agent attacker, int damage, float magnitude = 50f, bool knockDown = false, BlowFlags extraFlags = BlowFlags.None,
        DamageTypes damageType = DamageTypes.Pierce, bool chargeImpactSound = false)
    {
        if (victim == null || attacker == null) return;

        // Registering a blow runs the engine's whole hit pipeline (sound, OnAgentHit, Die, removal);
        // vanilla only ever does that on the main thread. Report, once per site, if we ever do not.
        MissionThreadGuard.NoteCall("CustomAttacksUtils.TakeDamage",
            m => TaleWorlds.Library.Debug.Print(m, 0, TaleWorlds.Library.Debug.DebugColor.Red));

        // Re-validate LIVE state at call time. The bone-collision callback fires several frames
        // after the CustomAttack sweep captured these agents, so the t-0 Health guard is stale: an
        // agent can despawn in the interim, and a blow on a despawning agent's native pointer is not
        // worth the risk. Written for a spider 0x3 AV that rca-spider-dismount-on-hit-2026-06-15.md
        // later traced to Agent.HandleBlowAux, so this is hardening, not that crash's fix. Additive:
        // a healthy blow (every warg bite, every clean spider bite) passes unchanged.
        if (!victim.IsActive() || victim.IsFadingOut() || victim.Index < 0 || victim.Health <= 0) return;
        if (!attacker.IsActive() || attacker.IsFadingOut() || attacker.Index < 0) return;

        // Every guard above reads the engine through a pointer captured at creation, and the engine
        // recycles a deleted agent's index, so a handle captured frames ago by a bone check can pass
        // all of them while its slot belongs to someone else; Health stays the dead agent's, which
        // only helps when it died in combat rather than being deleted alive (#592). Only the slot's
        // current occupant may take or deal a blow: RegisterBlow addresses agents by Index.
        if (!AgentSlotIdentity.IsCurrentOccupant(victim) || !AgentSlotIdentity.IsCurrentOccupant(attacker))
        {
            ReportSkippedStaleBlow();
            return;
        }

        Blow blow = new(attacker.Index)
        {
            DamageType = damageType,
            // -1 ("no bone"), NOT victim.Monster.HeadLookDirectionBoneIndex — see the bone-index comment below.
            BoneIndex = NoBoneIndex,
            GlobalPosition = (attacker.Position + victim.Position) * 0.5f
        };
        blow.GlobalPosition.z += victim.GetEyeGlobalHeight();
        blow.BaseMagnitude = magnitude;
        blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
        blow.WeaponRecord.WeaponFlags |= ComposeWeaponFlags(damageType);
        blow.InflictedDamage = damage;
        blow.SwingDirection = victim.LookDirection;
        MatrixFrame frame = victim.Frame;
        blow.SwingDirection = frame.rotation.TransformToParent(new Vec3(-1f, 0f, 0f, -1f));
        blow.SwingDirection.Normalize();
        blow.Direction = blow.SwingDirection;
        blow.DamageCalculated = true;
        blow.BlowFlag |= ComposeBlowFlags(knockDown, victim.HasMount, extraFlags | ChargeImpactFlags(chargeImpactSound));

        // Native-boundary geometry guard, kept as hardening (why, and what it is not known to
        // prevent: IsBlowGeometrySafe). GlobalPosition is built here from both agents' positions and
        // the victim's eye height and SwingDirection from the victim's frame, so an engine fault in
        // either would reach native with the Blow; HandleBlow clamps BaseMagnitude itself
        // (MathF.Min(b.BaseMagnitude, 1000f) maps NaN and +Inf to 1000 but passes a negative).
        // Reject before it reaches native.
        if (!IsBlowGeometrySafe(blow.GlobalPosition, blow.SwingDirection, blow.BaseMagnitude))
        {
            ReportSkippedNonFiniteBlow();
            return;
        }

        // blow.BoneIndex is forced to NoBoneIndex (-1) above — NOT victim.Monster.HeadLookDirectionBoneIndex.
        // WHY: Agent.HandleBlow -> GetProtectorArmorMaterialOfBone(b.BoneIndex) -> AgentVisuals.GetBoneTypeData
        // makes a managed->native call IMBAgentVisuals.GetBoneTypeData(visuals.Pointer, boneIndex, ...). The
        // engine guards ONLY `boneIndex >= 0`, NOT whether the victim's visuals are still alive. The
        // bone-collision callback fires several frames after the CustomAttack sweep, so the victim can begin
        // NATIVE teardown in the interim — or DURING this RegisterBlow, a TOCTOU the TakeDamage live-state guard
        // cannot fully close. Its native agent/visuals are then freed while the MANAGED MBAgentVisuals wrapper
        // survives (held by weakref) and Monster still returns a valid positive head-look bone. HandleBlow
        // derefs the dead visuals.Pointer -> AV reading 0x0 (crash report 2026-06-25: a horse caught
        // mid-teardown, boneIndex 31; the tell was IsHuman/IsMount reading INVERTED off the freed native
        // struct while the managed wrapper was still non-null). Handing -1 makes the `>= 0` guard short-circuit
        // so GetBoneTypeData is never called — deterministic, where detecting a pointer that can die after any
        // check is not. Cost is cosmetic only: the armor-material hit-sound parameter falls back to None; body
        // part is already hardcoded to BoneBodyPartType.Abdomen below, and damage/knockdown/death never read
        // BoneIndex. (mainHandItemBoneIndex is the ATTACKER's own bone, unaffected by victim teardown.)
        sbyte mainHandItemBoneIndex = attacker.Monster.MainHandItemBoneIndex;
        // The 3 positional ints below map to (affectorWeaponSlotOrMissileIndex, StrikeType, DamageType), and
        // DamageType matches blow.DamageType (LOTRAOM had `2`, Blunt; fixed 2026-05-27). The combat log does not
        // read it: vanilla copies the collision type into CombatLogData.DamageType inside
        // MissionCombatMechanicsHelper.GetAttackCollisionResults (v1.5.3 line 200), which a synthetic blow never
        // runs, and the CombatLogData constructor sets Blunt. So the log's type is set after the constructor below;
        // until 2026-09-23 every creature blow logged as Blunt.
        AttackCollisionData attackCollisionDataForDebugPurpose = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
            false, false, false, true, false, false, false, false, false, false, false, false,
            CombatCollisionResult.StrikeAgent,
            -1, 0, (int)damageType,
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

        // Named, because fifteen of these are bools read by position: crushedThrough prints "Crushed through!"
        // (CombatLogData.cs:185) and was passed knockDown until 2026-09-23, so every creature knockdown the player saw
        // read as a broken guard; isVictimRiderAgentSameAsAttackerAgent compared the victim with itself where vanilla
        // compares the victim's rider with the attacker (Mission.cs:6529). A synthetic blow never crushes a defence.
        CombatLogData combatLogData = new(
            isVictimAgentSameAsAttackerAgent: false,
            isAttackerAgentHuman: attacker.IsHuman,
            isAttackerAgentMine: attacker.IsMine,
            doesAttackerAgentHaveRiderAgent: attacker.RiderAgent != null,
            isAttackerAgentRiderAgentMine: attacker.RiderAgent != null && attacker.RiderAgent.IsMine,
            isAttackerAgentMount: attacker.IsMount,
            isVictimAgentHuman: victim.IsHuman,
            isVictimAgentMine: victim.IsMine,
            isVictimAgentDead: victim.Health <= 0f,
            doesVictimAgentHaveRiderAgent: victim.RiderAgent != null,
            isVictimAgentRiderAgentIsMine: victim.RiderAgent != null && victim.RiderAgent.IsMine,
            isVictimAgentMount: victim.IsMount,
            missionObjectHit: null,
            isVictimRiderAgentSameAsAttackerAgent: victim.RiderAgent == attacker,
            crushedThrough: false,
            chamber: false,
            distance: 0f);
        combatLogData.DamageType = damageType;
        MissionWeapon weapon = MissionWeapon.Invalid;
        // Before the blow, as the engine orders its own sound block inside HandleBlow.
        if (chargeImpactSound && !_initializationFailed && _registerBlow != null)
            PlayChargeImpactSound(attacker, victim, blow.GlobalPosition);
        RegisterBlow(attacker, victim, WeakGameEntity.Invalid, blow, ref attackCollisionDataForDebugPurpose, in weapon, ref combatLogData);
    }

    // The engine's hit-sound block for this blow (v1.5.3 Agent.HandleBlow :5466-5484), which a NoSound blow skips,
    // replayed with the sound GetHitSound gives a non-humanoid owner. A synthetic blow has no bone, so the armour type
    // is None, as the engine would have read it; it is neither a missile nor a sneak attack, so no player-hit sound is
    // due and the alarm is.
    private static void PlayChargeImpactSound(Agent owner, Agent victim, Vec3 position)
    {
        var mission = Mission.Current;
        if (mission == null) return;
        var parameter = new SoundEventParameter("Armor Type", Agent.GetSoundParameterForArmorType(ArmorComponent.ArmorMaterialTypes.None));
        mission.MakeSound(CombatSoundContainer.SoundCodeMissionCombatChargeDamage, position,
            soundCanBePredicted: false, isReliable: true, owner.Index, victim.Index, ref parameter);
        mission.AddSoundAlarmFactorToAgents(owner, in position, 7f);
    }

    /// <summary>
    /// The flags a synthetic blow carries. A knockdown on a mounted victim becomes a dismount (the
    /// engine's own rule for a rider). <paramref name="extraFlags"/> lets a caller add
    /// <see cref="BlowFlags.KnockBack"/> (SignatureStrikes #605); the engine grants that stagger
    /// only to an unmounted human (Mission.CreateMeleeBlow, v1.5.3 Mission.cs:5634), so it is
    /// stripped for a rider here as well. Pure, pinned by CustomAttacksUtilsBlowFlagsTests.
    /// </summary>
    public static BlowFlags ComposeBlowFlags(bool knockDown, bool victimHasMount, BlowFlags extraFlags)
    {
        var flags = extraFlags;
        if (victimHasMount) flags &= ~BlowFlags.KnockBack;
        if (knockDown) flags |= victimHasMount ? BlowFlags.CanDismount : BlowFlags.KnockDown;
        return flags;
    }

    /// <summary>
    /// <c>NoSound</c> for a blow whose charge impact TAOM plays itself (<c>TakeDamage</c>'s <c>chargeImpactSound</c>),
    /// so the engine does not also play the punch it picks for a human owner. Pure, pinned by CreatureImpactSoundTests.
    /// </summary>
    public static BlowFlags ChargeImpactFlags(bool chargeImpactSound)
        => chargeImpactSound ? BlowFlags.NoSound : BlowFlags.None;

    /// <summary>
    /// The weapon flags a synthetic blow carries beyond the empty melee record: <c>CanKillEvenIfBlunt</c> for a Blunt
    /// blow, nothing otherwise. The engine reads the killing blow's weapon flags when it decides killed or wounded, and a
    /// Blunt blow without this flag always wounds (<c>DefaultPartyHealingModel.GetSurvivalChance</c>, v1.5.3), so a
    /// creature's Blunt blow kills as its Pierce blows always have. Pure, pinned by CustomAttacksUtilsBlowFlagsTests.
    /// </summary>
    public static WeaponFlags ComposeWeaponFlags(DamageTypes damageType)
        => damageType == DamageTypes.Blunt ? WeaponFlags.CanKillEvenIfBlunt : 0;

    private static long _nonFiniteBlowSkips;
    private static long _staleBlowSkips;

    // Sample-gated like the non-finite report: one line for the first, then one per hundred.
    private static void ReportSkippedStaleBlow()
    {
        long n = System.Threading.Interlocked.Increment(ref _staleBlowSkips);
        if (n == 1 || n % 100 == 0)
            TaleWorlds.Library.Debug.Print(
                $"[TAOM] CustomAttacksUtils: skipped a blow whose attacker or victim no longer occupies its agent slot (total skipped: {n}, #592).",
                0, TaleWorlds.Library.Debug.DebugColor.Yellow);
    }

    /// <summary>
    /// True when a synthetic blow's geometry is safe to hand to the native blow processor: every
    /// component of <paramref name="globalPosition"/> and <paramref name="swingDirection"/> is finite,
    /// and <paramref name="magnitude"/> is finite and non-negative. These are the only TAOM-supplied
    /// floats in the Blow, and the whole Blow crosses into native through Agent.HandleBlowAux on
    /// every damaging blow and Agent.Die on a lethal one, the position also through
    /// Mission.MakeSound; Mission.OnAgentHit only hands the blow to managed listeners.
    /// Vec3.Normalize() maps a near-zero or NaN vector to (0, 1, 0), but a vector with an infinite
    /// component comes out NaN, so the direction check is live. What native does with a non-finite
    /// value is unproven: the spider 0x3 AV this guard was written for traced to
    /// Agent.HandleBlowAux itself (rca-spider-dismount-on-hit-2026-06-15.md), not to NaN geometry.
    /// Pure + unit-tested.
    /// </summary>
    public static bool IsBlowGeometrySafe(Vec3 globalPosition, Vec3 swingDirection, float magnitude) =>
        FiniteFloatValidator.IsFinite(globalPosition.x)
        && FiniteFloatValidator.IsFinite(globalPosition.y)
        && FiniteFloatValidator.IsFinite(globalPosition.z)
        && FiniteFloatValidator.IsFinite(swingDirection.x)
        && FiniteFloatValidator.IsFinite(swingDirection.y)
        && FiniteFloatValidator.IsFinite(swingDirection.z)
        && FiniteFloatValidator.IsFiniteAtLeast(magnitude, 0f);

    // Sample-gated: a burst of non-finite blows across many creatures must not flood the log from
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
