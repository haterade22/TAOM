# Clean-room spec: TOR_Core combat mechanics → TAOM CombatMechanics

**Source:** The Old Realms, `TheOldRealms/TOR_Core`, file `CSharpSourceCode/Models/TORAgentApplyDamageModel.cs`, pinned commit `d8ded52a904191f92025c074ad678379c0f72734` (targets Bannerlord 1.3.15).
**License:** GPLv3. **No TOR code is copied into TAOM.** This document records the *mechanics and constants as facts* (uncopyrightable), extracted by reading the upstream file once during planning (2026-07-02). Implementation works exclusively from this spec + the approved plan, in TAOM's own architecture (thin GameModel → pure services → validated config), and must not open TOR files. Attribution also lands in `docs/features/combat-mechanics.md` and the CHANGELOG.

Mechanics 5b (weight-driven charge knockdown) and 6 (per-race modifier table) are **TAOM-original designs** (user-directed, 2026-07-02) and owe TOR nothing beyond the velocity-gate inspiration; they are specified here for completeness.

## Engine ground truth (installed v1.4.6, verified via ilspycmd 2026-07-02)

Decompiles cached at `~/.taom-src/v1.4.6/`. All signatures identical to the v1.4.5 dump.

- `AgentApplyDamageModel` (TaleWorlds.MountAndBlade.ComponentInterfaces): all 9 override points exist — `DecideCrushedThrough(Agent, Agent, float totalAttackEnergy, Agent.UsageDirection, StrikeType, WeaponComponentData defendItem, bool isPassiveUsageHit)` (:64), `CalculateRemainingMomentum` (:66), `DecideWeaponCollisionReaction(..., float momentumRemaining, out MeleeCollisionReaction)` (:44), `DecideAgentShrugOffBlow` (:94), `DecideAgentKnockedBackByBlow` (:98), `DecideAgentKnockedDownByBlow` (:100), `DecideMissileWeaponFlags(Agent, in MissionWeapon, ref WeaponFlags)` (:32), `CalculateShieldDamage(in AttackInformation, float)` (:46), `CalculateStaggerThresholdDamage(Agent, in Blow)` (:36), `GetHorseChargePenetration()` (:112).
- SandBox defaults (`SandboxAgentApplyDamageModel`): crush-through threshold **58f**, ×1.2 vs shield, swing + overhead only (:626-666); `GetHorseChargePenetration` returns **0.4f** (:960-963); shrug-off + knockdown delegate to `MissionCombatMechanicsHelper` (:1187-1204).
- `MissionCombatMechanicsHelper` (v1.4.6): `DecideAgentShrugOffBlow` computes threshold via `MissionGameModels.Current.AgentApplyDamageModel.CalculateStaggerThresholdDamage` — the **registered model** — and requires the victim to survive (`Health − damage ≥ 1`). `DecideAgentKnockedDownByBlow`: ShrugOff flag → false; horse-charge branch requires `BlowFlags.KnockBack` then `DecideCombatEffect(damage, HealthLimit, GetKnockDownResistance(victim), GetHorseChargePenetration())`; `DecideCombatEffect(d, max, res, pen) ≡ d ≥ max × max(0, res − pen)` — deterministic. `GetKnockDownResistance` has a 1-arg overload (charge branch) and a 2-arg `(victim, StrikeType)` overload (weapon knockdowns). Knockback horse-charge branch: dot ≥ 0.7 → true unconditionally. `UpdateMomentumRemaining` **assigns** the model's `CalculateRemainingMomentum` return.
- `Mission` charge path (`ChargeDamageCallback`): attacker = **mount** agent, weapon **null**, KnockBack decided before KnockDown, KnockDown call unconditional at the call site when damage > 0, ShrugOff never decided. Melee path: ShrugOff first (any collider agent), KnockBack→KnockDown only for on-foot human victims, mounted humans → CanDismount.
- `Monster` (TaleWorlds.Core, v1.4.6): `int Weight` (:34; default 1, XML `weight`, `base_monster` inherits), `float RelativeSpeedLimitForCharge` (:84; default **float.MaxValue** when attr absent, :484-489).
- `WeaponFlags.CanPenetrateShield = 0x20000`, `WeaponFlags.MultiplePenetration = 0x40000000`.
- Monster weights (game data): Native human 80, horse 400 (rslc 4.3); LOTRLOME dwarf 100, elf 80, orc 140, uruk/pale_uruk/dg_uruk 160, uruk_hai 180, berserker 190, goblin 160, cave_troll/hill_troll 160; spider 250 (rslc 4), warg 500 (rslc 4), taom_war_elephant/taom_mumakil 9999 (rslc 1.0).

## Mechanic 1 — skill-based extra crush-through-block (CTB)

Runs when the base (58f overhead) crush-through did not already fire; falls through to base on null.

| Constant | Value | Meaning |
|---|---|---|
| extra energy threshold | 25.0 | extra-CTB path gates on `effectiveEnergy > 25` (base 58f path untouched) |
| skill dead zone | 30 | skill delta ≤ 30 → no extra CTB |
| target skill delta | 200 | delta at which chance reaches max |
| max chance | 0.5 | chance at target delta |
| curve exponent | 0.008097 | exponential growth rate |
| energy ramp margin | 0.27 | chance factor ramps 0→1 over `threshold × 0.27` past threshold |
| non-overhead penalty | ×0.5 | non-overhead swings halve the chance |

Formula (upstream behavior, restated):
- eligibility: melee weapon wielded, not passive usage, strike is a swing, energy above threshold
- `delta = attackerSkill − defenderSkill` where attackerSkill = attacker's value of the wielded weapon's `RelevantSkill` and defenderSkill = defender's value of the defend-item's `RelevantSkill` (0 when null)
- `chance = maxChance × (1 − e^(−(delta−deadZone)×exponent)) / (1 − e^(−(targetDelta−deadZone)×exponent))`, clamped [0,1]
- `energyFactor = clamp01((energy − threshold) / (threshold × rampMargin))`; `chance ×= energyFactor`
- non-overhead swing: `chance ×= 0.5`
- roll `random < chance` → crush through
- shield blocks are excluded from the extra path (upstream gates them out) except via mechanic 3

TAOM extensions: `effectiveEnergy = energy × (1 + attackerRaceMods.SwingEnergyBonusFactor)`; `delta += atkMods.CtbAttackBonus − defMods.CtbDefenseBonus`; non-overhead penalty skipped when `atkMods.RemoveNonOverheadPenalty`.

## Mechanic 2 — monster auto-crush-through

Upstream: attacker flagged as a monster → a block with anything other than a **shield** is crushed through automatically; a shield block falls through to the normal energy check. TAOM: attacker identified by `Monster.StringId` config list (`cave_troll`, `hill_troll`, `taom_war_elephant`, `taom_mumakil`, `spider`), with `_settlement`/`_settlement_fast`/`_settlement_slow` suffix normalization.

## Mechanic 3 — orc shield-crush-through

Upstream: AI-controlled orc-race melee attackers may crush through **even shield blocks**, and take no non-overhead penalty. TAOM: race list config (`orc`, `goblin`, `uruk`, `uruk_hai`, `pale_uruk`, `dg_uruk`), AI-only, evaluated through the mechanic-1 curve without the shield gate.

## Mechanic 4 — creature cleave (cut-through)

Upstream: troll/treeman attackers force the melee collision reaction to `SlicedThrough`, and remaining momentum for crush-through OR cut-through is `original × 0.3`. TAOM: creature id list (`cave_troll`, `hill_troll`, `taom_mumakil`), factor 0.3 configurable. Both halves required for reliable chaining (verified): the momentum override enables chained damage at all (default zeroes momentum for ordinary weapons; the value also scales follow-on damage), the reaction override prevents chain-termination on the Bounced/Stuck branches (shield block, axe below 50% victim HealthLimit, shrug-off non-fatal, wrong-bone hit).

## Mechanic 5 — charge knockdown (headline; TAOM-original formula)

Upstream inspiration: `IsHorseCharge && ChargeVelocity > attacker.Monster.RelativeSpeedLimitForCharge` → always knock down. TAOM v1 (user-directed weight generalization):

```
if (!enabled || !isHorseCharge || shrugOffFlagSet) → fall through to base
speedRef    = RelativeSpeedLimitForCharge if finite and < 1e6, else defaultChargeSpeedReference (4.3)
speedFactor = clamp01(chargeVelocity / speedRef)
weightRatio = (chargerWeight + riderWeight·includeRiderWeight) / max(victimWeight, 1)
Branch A: weightRatio ≥ autoKnockdownWeightRatio (8) AND speedFactor ≥ autoMinSpeedFactor (0.4) → knock down
Branch B: requires BlowFlags.KnockBack (vanilla 0.7-dot parity);
          pen = horseChargePenetration (0.4) × clamp(weightRatio / neutralWeightRatio (6.0), 0.25, 2.5) × speedFactor
          knock down iff damage ≥ maxHealth × max(0, knockDownResistance × raceResistMultiplier − pen)
```

Calibration: neutral 6.0 = Native (horse 400 + rider 80)/human 80 → unmodified horse-vs-man ≈ vanilla. Mûmakil 9999/80 ≈ 125 → Branch A. Horse vs cave_troll 480/160 = 3.0 → pen halved. Dwarf resist multiplier 2.5 ("significant" per user).

## Mechanic 6 — per-race combat modifiers (TAOM-original)

Config dict keyed by race NAME (validated via `IRaceManager.IsValidRaceName`): `ctbAttackBonus` / `ctbDefenseBonus` (skill points), `knockdownResistanceMultiplier`, `staggerThresholdMultiplier` (applied in `CalculateStaggerThresholdDamage`; feeds vanilla shrug-off via the registered-model reentrancy), `removeNonOverheadPenalty`, `swingEnergyBonusFactor` ("Brute"). Defaults: dwarf {def 15, kd 2.5, stagger 1.5}, elf {atk 20, no-off-angle-penalty}, orc {energy +0.15}, uruk_hai {energy +0.10, kd 1.25}.

## Mechanic 7 — creature unstoppable (shrug-off)

Upstream: attribute + per-agent damage threshold. TAOM: per-monster-id damage thresholds (cave_troll 15, hill_troll 15, taom_war_elephant 25, taom_mumakil 30, spider 10); `DecideAgentShrugOffBlow` = base (vanilla + career) OR creature check. True sets `BlowFlags.ShrugOff` → engine suppresses knockdown/knockback/dismount (intended).

## Mechanic 8 — shield penetration + damage correction

Upstream: perk/trait-driven runtime `CanPenetrateShield`/`MultiplePenetration` grants in `DecideMissileWeaponFlags`, plus a `CalculateShieldDamage` correction dividing by 0.3 when penetration flags were added at runtime only — a workaround for native underestimation of shield damage for runtime-added flags (TaleWorlds forum threads 470085 + 470117, filed ~1.2.x; **must be re-verified on 1.4.6 in a control battle** — the correction ships config-gated). TAOM: grants come from config item-id + weapon-class lists (default `Javelin` class), applied after base (preserving the vanilla Javelin+Impale perk grant).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/combat-mechanics.md](../features/combat-mechanics.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
