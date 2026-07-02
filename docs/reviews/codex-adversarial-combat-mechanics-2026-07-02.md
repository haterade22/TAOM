# Codex adversarial review — CombatMechanics (2026-07-02)

> Final review message extracted from the 1.3MB `codex exec` session log (full log discarded; prompt at `codex-adversarial-combat-mechanics-2026-07-02.prompt.md`). Model gpt-5.5, reasoning xhigh.

No P1/P2 ship-blocking bugs found. The implementation preserves the single `AgentApplyDamageModel` slot correctly, the charge/cleave/crush paths match the normative spec, and the six known suspects mostly resolve as intentional behavior or verified-clean engine contracts. I found two P3 observations only: one config hardening gap for typoed monster/race-id lists, and one wording edge around “including through shield blocks” when shield damage is exactly zero.

**Known Suspects**
1. CONFIRMED behavior, DISPUTED bug. A non-overhead troll hit against a shield at energy 70 does not crush. `CrushThroughService` excludes shields from monster auto-CTB and plain skill CTB, then vanilla base requires `AttackUp`. This matches the spec’s “shield block falls through to base energy check”; base includes the overhead requirement.

2. DISPUTED for normal shield blocks. Vanilla shield blocks write shield damage into `AttackCollisionData.InflictedDamage` via `ComputeBlowDamageOnShield`, so damaging shield blocks can be forced back to `SlicedThrough`. Edge: if shield damage is exactly 0, TAOM’s `inflictedDamage <= 0` guard keeps the vanilla `Bounced` termination.

3. DISPUTED. `BasicCharacterObject.Race` is populated through `FaceGen.GetRaceOrDefault(...)`; `RaceManager` maps `FaceGen.GetRaceNames()` by index through `FaceGenAdapter`. Same FaceGen registry id space. TAOM HeroRace also persists by writing `hero.CharacterObject.Race`.

4. DISPUTED. `ChargeDamageCallback` mutates the same local `blow` with `BlowFlags.KnockBack` before passing `in blow` to `DecideAgentKnockedDownByBlow`. Branch B can see the flag.

5. DISPUTED. The dwarf stagger multiplier applies once through vanilla shrug-off re-entry into the registered model’s `CalculateStaggerThresholdDamage`. Career shrug-off is a separate boolean check after that; creature unstoppable only keys monster ids.

6. CONFIRMED behavior, DISPUTED bug. MCM values win over JSON once `TaomSettings.Instance` exists. This matches `AlignmentDesertionSettingsProvider`. `docs/features/combat-mechanics.md` says MCM merges over JSON per read and does not promise JSON toggles override MCM after MCM load.

**Vanilla Code**
Abridged to the decision lines I used; all snippets are from `C:\Users\mikew\.taom-src\v1.4.6`.

```csharp
// SandBox.GameComponents.SandboxAgentApplyDamageModel.cs
public override bool DecideCrushedThrough(..., float totalAttackEnergy,
    UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsage)
{
    ...
    if (obj == null || isPassiveUsage || (int)strikeType != 0 || (int)attackDirection != 0)
        return false;
    float num = 58f;
    if (defendItem != null && defendItem.IsShield)
        num *= 1.2f;
    return totalAttackEnergy > num;
}

public override void DecideMissileWeaponFlags(..., ref WeaponFlags missileWeaponFlags)
{
    ...
    if ((int)missileWeapon.CurrentUsageItem.WeaponClass == 23 && val.GetPerkValue(Throwing.Impale))
        missileWeaponFlags = (WeaponFlags)((ulong)missileWeaponFlags | 0x20000uL);
}

public override float GetHorseChargePenetration() => 0.4f;

public override float CalculateStaggerThresholdDamage(Agent defenderAgent, in Blow blow)
{
    ...
    return ManagedParameters.Instance.GetManagedParameter(val4) * num;
}

public override float CalculateShieldDamage(in AttackInformation attackInformation, float baseDamage)
{
    ExplainedNumber val = default;
    val..ctor(baseDamage, false, null);
    ...
    return val.ResultNumber;
}
```

```csharp
// TaleWorlds.MountAndBlade.MissionCombatMechanicsHelper.cs
public static bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
{
    if (victimAgent.Health - (float)collisionData.InflictedDamage >= 1f)
    {
        float num = MissionGameModels.Current.AgentApplyDamageModel.CalculateStaggerThresholdDamage(victimAgent, in blow);
        return (float)collisionData.InflictedDamage <= num;
    }
    return false;
}

public static bool DecideAgentKnockedBackByBlow(...)
{
    if (collisionData.IsHorseCharge)
        result = ChargeDamageDotProduct(...) >= 0.7f;
    ...
}

public static bool DecideAgentKnockedDownByBlow(...)
{
    if ((blow.BlowFlag & BlowFlags.ShrugOff) == 0)
    {
        bool flag = (blow.BlowFlag & BlowFlags.KnockBack) != 0;
        if (collisionData.IsHorseCharge && flag)
            result = DecideCombatEffect(inflictedDamage, num,
                AgentStatCalculateModel.GetKnockDownResistance(victimAgent),
                AgentApplyDamageModel.GetHorseChargePenetration());
        ...
    }
}

public static void DecideWeaponCollisionReaction(..., float momentumRemaining, out MeleeCollisionReaction colReaction)
{
    if (!collisionData.IsColliderAgent || registeredBlow.InflictedDamage <= 0)
    {
        colReaction = MeleeCollisionReaction.Bounced;
        return;
    }
    ...
    if ((collisionData.AttackBlockedWithShield || collisionData.CollidedWithShieldOnBack)
        && colReaction == MeleeCollisionReaction.SlicedThrough)
        colReaction = MeleeCollisionReaction.Bounced;
}

public static void UpdateMomentumRemaining(...)
{
    momentumRemaining = MissionGameModels.Current.AgentApplyDamageModel.CalculateRemainingMomentum(...);
}

private static bool DecideCombatEffect(float damage, float maxHealth, float resistance, float penetration)
{
    float num = maxHealth * Math.Max(0f, resistance - penetration);
    return damage >= num;
}

private static void ComputeBlowDamageOnShield(..., out int inflictedDamage)
{
    inflictedDamage = 0;
    if (!victimShield.CurrentUsageItem.WeaponFlags.HasAnyFlag(WeaponFlags.CanBlockRanged)
        || !attackInformation.CanGiveDamageToAgentShield)
        return;
    ...
    num = MissionGameModels.Current.AgentApplyDamageModel.CalculateShieldDamage(in attackInformation, num);
    inflictedDamage = (int)num;
}
```

```csharp
// TaleWorlds.MountAndBlade.Mission.cs
internal void MeleeHitCallback(..., ref float inOutMomentumRemaining, ref MeleeCollisionReaction colReaction, ...)
{
    bool flag = collisionData.CollisionResult == Parried
        || collisionData.CollisionResult == Blocked
        || collisionData.CollisionResult == ChamberBlocked;

    GetAttackCollisionResults(..., ref collisionData, out shieldOnBack, out combatLog);
    Blow b = CreateMeleeBlow(..., flag2);

    if (!flag && ...)
        RegisterBlow(...);

    MissionCombatMechanicsHelper.UpdateMomentumRemaining(ref inOutMomentumRemaining, in b, in collisionData, ...);
    MissionGameModels.Current.AgentApplyDamageModel.DecideWeaponCollisionReaction(
        in b, in collisionData, attacker, victim, in attackerWeapon, isFatalHit, isShruggedOff,
        inOutMomentumRemaining, out colReaction);
}

internal void ChargeDamageCallback(ref AttackCollisionData collisionData, Blow blow, Agent attacker, Agent victim)
{
    GetAttackCollisionResults(attacker, victim, WeakGameEntity.Invalid, 1f, in MissionWeapon.Invalid, ...);
    if ((float)collisionData.InflictedDamage > 0f)
    {
        blow.InflictedDamage = collisionData.InflictedDamage;
        ...
        if (AgentApplyDamageModel.DecideAgentKnockedBackByBlow(attacker, victim, in collisionData, null, in blow))
            blow.BlowFlag |= BlowFlags.KnockBack;
        else
            blow.BlowFlag &= ~BlowFlags.KnockBack;

        if (AgentApplyDamageModel.DecideAgentKnockedDownByBlow(attacker, victim, in collisionData, null, in blow))
            blow.BlowFlag |= BlowFlags.KnockDown;

        RegisterBlow(attacker, victim, WeakGameEntity.Invalid, blow, ...);
    }
}

private CombatLogData GetAttackCollisionResults(...)
{
    AttackInformation attackInformation = new AttackInformation(attackerAgent, victimAgent, ...);
    MissionCombatMechanicsHelper.GetAttackCollisionResults(..., ref attackCollisionData, ...);
    if (attackCollisionData.InflictedDamage > 0)
        attackCollisionData.InflictedDamage = Round(AgentApplyDamageModel.CalculateDamage(...));
}
```

```csharp
// TaleWorlds.Core.Monster.cs / BasicCharacterObject.cs / FaceGen.cs / AgentStatCalculateModel.cs
public int Weight { get; private set; }
public float RelativeSpeedLimitForCharge { get; private set; }
// Deserialize: Weight defaults 1, then reads node.Attributes["weight"].
// Deserialize: RelativeSpeedLimitForCharge defaults float.MaxValue, then reads "relative_speed_limit_for_charge".

public int Race { get; set; }
// BasicCharacterObject.Deserialize:
Race = 0;
XmlAttribute xmlAttribute2 = node.Attributes["race"];
if (xmlAttribute2 != null)
    Race = FaceGen.GetRaceOrDefault(xmlAttribute2.Value);

// FaceGen:
public static int GetRaceOrDefault(string raceId) => _instance?.GetRaceOrDefault(raceId) ?? 0;
public static string[] GetRaceNames() => _instance?.GetRaceNames() ?? null;

public abstract float GetKnockDownResistance(Agent agent, StrikeType strikeType = StrikeType.Invalid);
```

**Deep Scenarios**
A. Horse 400 + rider 80 vs man 80, velocity 4.3, rslc 4.3, damage 50, maxHealth 100, KDR 0.6, KnockBack set:
Weight ratio `480/80 = 6`; Branch A false because `6 < 8`. Branch B penetration `0.4 * clamp(6/6, .25, 2.5) * 1 = 0.4`. Threshold `100 * (0.6 - 0.4) = 20`. `50 >= 20`, TAOM true. Vanilla `DecideCombatEffect(50,100,0.6,0.4)` also true.

B. Same vs dwarf weight 100, raceResist 2.5:
Ratio `480/100 = 4.8`; penetration `0.4 * (4.8/6) = 0.32`. Threshold `100 * (0.6*2.5 - 0.32) = 118`. `50 < 118`, TAOM false.

C. Mumakil 9999 at velocity 0.5, rslc 1.0:
Speed factor `0.5/1.0 = 0.5`; ratio vs man is about `9999/80 = 124.99` before rider weight. Branch A fires because ratio `>= 8` and `0.5 >= 0.4`. This is intended by config/test calibration: the speed gate is relative to the monster’s low charge reference.

D. Cave troll, non-overhead, shield block, energy 70:
Monster auto path returns null because shield. Plain skill path returns null because shield and not orc-qualified. Vanilla base returns false because non-overhead, even though `70 > 69.6`. No crush. Chain path: damaging shield block sets `InflictedDamage` to shield damage; cleave momentum becomes `70 * 0.3 = 21`; TAOM forces `SlicedThrough` if `InflictedDamage > 0`. So it does not crush, but it can chain through a damaging shield block.

E. Player-controlled orc race vs shield:
`orcQualified` requires `context.IsAiControlled`, so player false. Plain skill path refuses shield. Only vanilla overhead `>69.6` can still crush.

F. Skill CTB delta exactly 200, energy 40, max chance 0.5:
Delta normalized to full target. Energy factor `(40 - 25) / (25 * 0.27) = 2.22`, clamped to 1. Chance `0.5`. Roll `0.4999 < 0.5` crushes; roll `0.5 < 0.5` does not. Miss returns null, so vanilla still gets its base check.

Edge: mumakil vs warg/horse:
Warg `victimWeight=500`, mumakil ratio about `9999/500 = 19.998`, so Branch A fires at speed factor `>=0.4`. For cavalry-vs-cavalry, managed `ChargeDamageCallback` does not remap victim; TAOM uses `victimAgent.Monster.Weight`. If the native collision victim is the horse agent, the horse’s monster weight is used; if native supplies the rider, rider weight is used. The managed decompile proves no rider/mount substitution happens in C#.

**Config Cross-Reference**
JSON camelCase binds cleanly to the PascalCase POCOs through Json.NET.

- `enabled` -> `CombatMechanicsConfig.Enabled` -> `CombatMechanicsSettingsProvider.MasterEnabled`.
- `crushThrough.*` -> `CrushThroughConfig` -> `CrushThroughService`; `maxSkillChance` is overridden by MCM `CrushThroughMaxChance`.
- `chargeKnockdown.*` -> `ChargeKnockdownConfig` -> `ChargeKnockdownService`; `horseChargePenetration` also feeds `TaomCombatMechanicsModel.GetHorseChargePenetration`.
- `creatures.*` -> `CreatureCombatConfig` -> `CreatureCombatService`.
- `shieldPenetration.*` -> `ShieldPenetrationConfig` -> `ShieldPenetrationService`; `weaponClasses=["Javelin"]` validates against `WeaponClass.Javelin`.
- `raceModifiers.*` -> `RaceCombatModifiersResolver` -> crush-through, charge resistance, and stagger threshold.

Installed XML cross-reference passed:
`cave_troll`, `hill_troll`, `taom_war_elephant`, `taom_mumakil`, `spider`, `warg`, `horse` all exist in installed module monster XML. Race keys `dwarf`, `elf`, `orc`, `uruk_hai`, plus orc-shield list keys `goblin`, `uruk`, `pale_uruk`, `dg_uruk`, all exist in installed `skins.xml`/Alliance.Wargs skin data.

**Findings Or Observations**
1. P3 observation, `Main/Features/CombatMechanics/CombatMechanicsConfigProvider.cs:110` / `:197` / `:336` — creature/race id lists are only syntax-cleaned, not resolvability-validated. Shipped config is clean, so no current gameplay bug. Failure scenario: user typo `taom_mumakill` in `cleaveMonsterIds`; service builds a dead set entry and logs no unknown-id warning. `raceModifiers` already has lazy unknown-race validation; the monster lists and `orcShieldCrushRaces` do not.

2. P3 observation, `Main/Features/TaomSettings.cs:644` plus `Main/Features/CombatMechanics/CreatureCombatService.cs:82` — the MCM hint says cleave includes shield blocks. That is true for normal damaging shield blocks because vanilla stores shield damage in `InflictedDamage`; it is false for the zero-shield-damage edge because TAOM requires `inflictedDamage > 0`. I would not call this a functional defect, but the wording is broader than the guard.

P1: 0 | P2: 0 | P3 observations: 2  
VERDICT: CLEAN  
Tests were not run; this was a static/decompile adversarial review.
