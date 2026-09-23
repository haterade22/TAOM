namespace TAOM.Features.SignatureStrikes.Domain;

/// <summary>
/// Everything the pure service needs about one melee collision, as primitives read off the
/// engine's <c>AttackCollisionData</c> and the two agents at the boundary
/// (<c>StrikeContextFactory</c>). One struct serves both the ring evaluation and the two
/// primary-victim verdicts so the model and the mission logic cannot describe the same hit
/// differently.
/// </summary>
/// <param name="IsSignatureAttacker">The attacker is on the signature roster (hero id, hero set or race).</param>
/// <param name="SignatureIndex">Which signature the attacker carries: its position in the
/// validated config's <c>Signatures</c> list, resolved once at spawn.</param>
/// <param name="Direction">The swing direction the engine animated.</param>
/// <param name="Collision">Mirror of <c>CombatCollisionResult</c>.</param>
/// <param name="IsCanceled">The engine's <c>isCanceled</c> on <c>OnMeleeHit</c>: invulnerable victim,
/// nothing hit, or a weapon block or parry. Always false on the model path, which the engine only
/// reaches for a real collision.</param>
/// <param name="IsColliderAgent">The thing struck was an agent.</param>
/// <param name="IsAlternativeAttack">A kick or a shield bash.</param>
/// <param name="IsMissile">A ranged hit; never true on <c>OnMeleeHit</c>, gated anyway.</param>
/// <param name="IsHorseCharge">A mount charge, which the model routes elsewhere.</param>
/// <param name="AttackBlockedWithShield">A shield took the hit (the collision is <c>Blocked</c>
/// and the damage is shield damage).</param>
/// <param name="HasMeleeWeapon">The attacker holds a non-empty weapon whose item type is not
/// thrown or ranged. Bare hands, a bow used as a club and a javelin swung in melee mode are out
/// (Mike, 2026-09-16: any weapon except thrown and bow).</param>
/// <param name="VictimIsHuman">The struck agent is humanoid (the engine only asks the knock-down
/// and knock-back deciders for humans).</param>
/// <param name="VictimIsMounted">The struck agent is riding.</param>
/// <param name="HasShrugOff">The blow already carries <c>BlowFlags.ShrugOff</c>.</param>
/// <param name="InflictedDamage">Damage the engine computed for this collision (shield damage on
/// a shield block; not trusted on a world hit).</param>
/// <param name="MissionTime">Mission clock now.</param>
/// <param name="LastStrikeTimes">Mission time of this attacker's last strike of each kind, a copy
/// of the roster's value; NaN means never.</param>
public readonly record struct StrikeContext(
    bool IsSignatureAttacker,
    int SignatureIndex,
    StrikeDirection Direction,
    StrikeCollision Collision,
    bool IsCanceled,
    bool IsColliderAgent,
    bool IsAlternativeAttack,
    bool IsMissile,
    bool IsHorseCharge,
    bool AttackBlockedWithShield,
    bool HasMeleeWeapon,
    bool VictimIsHuman,
    bool VictimIsMounted,
    bool HasShrugOff,
    int InflictedDamage,
    float MissionTime,
    StrikeKindTimes LastStrikeTimes);

/// <summary>
/// The ring the runner should apply, resolved from the direction's validated profile plus the
/// damage basis of the hit that triggered it.
/// </summary>
/// <param name="DamageBasis">The hit's own damage (or the profile's world-hit damage), before the
/// fraction and the falloff.</param>
/// <param name="Magnitude">The blow impulse handed to <c>CustomAttacksUtils.TakeDamage</c>.</param>
/// <param name="Origin">Where the ring is centred: the hit point, or the attacker.</param>
/// <param name="Sound">Module sound played once per strike; null for none.</param>
/// <param name="SignatureId">The signature's config id, for the log line.</param>
public readonly record struct StrikeEffect(
    StrikeKind Kind,
    float OuterRadius,
    float InnerRadius,
    int DamageBasis,
    float DamageFraction,
    float Magnitude,
    bool KnockDown,
    bool KnockBack,
    float FearMorale,
    StrikeOrigin Origin = StrikeOrigin.Impact,
    string? Sound = null,
    string SignatureId = "");
