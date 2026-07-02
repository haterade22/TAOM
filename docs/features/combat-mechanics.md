# Combat Mechanics

## Overview

Seven battle-feel mechanics layered onto the single `AgentApplyDamageModel` slot: skill-based crush-through-block, monster auto-crush-through, orc shield-crush-through, creature cleave, creature stagger immunity (unstoppable), weight-driven charge knockdown, and config-granted shield penetration — plus a per-race combat-modifier table (dwarf/elf/orc flavor) that feeds several of them. All config/MCM-toggleable; master toggle off restores exactly the pre-feature behavior.

**Derived-from:** clean-room adaptation of mechanics in The Old Realms' `TORAgentApplyDamageModel` (`TheOldRealms/TOR_Core`, commit `d8ded52`, GPLv3 — no code copied; constants/formulas recorded as facts in [docs/reviews/adopt-tor-combat-mechanics-2026-07-02.md](../reviews/adopt-tor-combat-mechanics-2026-07-02.md), the normative spec). The weight-driven charge knockdown and the race-modifier table are TAOM-original designs.

## Why This Exists

- LOTR battles need monsters to FEEL massive: a troll's swing should not bounce off a militia shield, arrows should not flinch-lock a mûmakil, and a mûmakil charge should flatten a shield wall — while a horse should *not* be able to floor a troll.
- Vanilla crush-through is a flat 58-energy overhead check; a Blademaster (300 skill) vs a Looter (10 skill) blocks identically to a mirror match. The skill-based extra chance makes skill gaps matter at high momentum.
- Vanilla charge knockdown ignores mass entirely (`GetHorseChargePenetration()` is a flat 0.4). The weight formula generalizes it from `monsters.xml` data, so every current and future mount gets sane knockdown behavior for free.
- Race identity: dwarves are stout (knockdown resistance 2.5×, stagger threshold 1.5×), elves precise (CTB attack bonus, no off-angle penalty), orcs brutal (swing-energy bonus, AI orcs crush shields).

## Architecture

One derived GameModel in the engine's single `AgentApplyDamageModel` slot:

```
TaomCombatMechanicsModel : TaomAgentApplyDamageModel (CareerSystem, now abstract) : SandboxAgentApplyDamageModel
```

Career damage passives ride along via inheritance; the CareerSystem parent is `abstract` since 2026-07-02 (registered only through this derived model — see `GameModelOverrideBindingTests`, which exempts abstract models from the registration gate).

Thin model → four pure services (ADR-002/007; gamemodels.md rule 4): every override extracts primitives/DTOs at the boundary and delegates. Services precompute `HashSet`/`Dictionary` lookups at construction (per-hit hot path — no LINQ/allocation per call) and take caller-supplied random rolls so probability tests are deterministic.

| Override | Service | Mechanic |
|---|---|---|
| `DecideCrushedThrough` | `CrushThroughService` | monster auto-CTB → orc shield-CTB → skill CTB curve; `?? base` keeps the vanilla 58f path |
| `CalculateRemainingMomentum` | `CreatureCombatService` | cleave momentum 0.3× for listed creatures (default zeroes momentum for ordinary weapons) |
| `DecideWeaponCollisionReaction` | `CreatureCombatService` | force `SlicedThrough` — prevents chain-termination on Bounced/Stuck branches (shield block, axe <50% HP, shrug-off, wrong bone) |
| `DecideAgentShrugOffBlow` | `CreatureCombatService` | base (vanilla + career) OR per-creature damage threshold; true sets `BlowFlags.ShrugOff` which also suppresses knockback/knockdown/dismount (intended) |
| `CalculateStaggerThresholdDamage` | `CreatureCombatService` | × race `staggerThresholdMultiplier`; vanilla shrug-off re-enters this via the REGISTERED model, so the multiplier feeds vanilla stagger automatically |
| `DecideAgentKnockedDownByBlow` | `ChargeKnockdownService` | weight-driven two-branch (below); non-charge hits short-circuit to base |
| `DecideMissileWeaponFlags` | `ShieldPenetrationService` | after base (preserves vanilla Javelin+Impale grant): OR-in `CanPenetrateShield`/`MultiplePenetration` for config-listed ids/classes |
| `CalculateShieldDamage` | `ShieldPenetrationService` | ÷0.3 correction when penetration was granted at runtime only (native underestimation workaround, config-gated) |
| `GetHorseChargePenetration` | (config constant) | single source for the 0.4 constant — feeds both the vanilla fall-through and TAOM's Branch B; folds the mechanic toggle (disabled → vanilla `base` value, so a tuned value doesn't survive the feature being off) |

`DecideAgentKnockedBackByBlow` is deliberately NOT overridden (vanilla 0.7-dot glancing gate kept; the engine calls KnockedDown unconditionally on the charge path, so Branch A works without it).

Shared infrastructure: `RaceCombatModifiersResolver` (lazy race-key validation via `IRaceManager.IsValidRaceName` — the registry is engine state unavailable at load — plus per-raceId caching; invalid race ids resolve to Neutral, never the "human" fallback row) and monster-id normalization (`X_settlement`/`_settlement_fast`/`_settlement_slow` → `X`).

### Charge knockdown formula (v1)

```
speedRef    = charger Monster relative_speed_limit_for_charge if sane, else 4.3 (Native horse)
speedFactor = clamp01(chargeVelocity / speedRef)
weightRatio = (chargerWeight + riderWeight) / max(victimWeight, 1)        // Monster.Weight from monsters.xml
Branch A: weightRatio ≥ 8 && speedFactor ≥ 0.4                → knock down (ignores the 0.7-dot gate)
Branch B: requires BlowFlags.KnockBack (0.7-dot parity);
          pen = 0.4 × clamp(weightRatio / 6.0, 0.25, 2.5) × speedFactor
          knock down iff damage ≥ maxHealth × max(0, knockDownRes × raceResist − pen)
```

Calibration: neutral 6.0 = Native (horse 400 + rider 80)/human 80 → horse-vs-man ≈ vanilla; mûmakil (9999) vs man ≈ ratio 125 → Branch A; horse vs troll (160) = ratio 3 → penetration halved; dwarf `raceResist` 2.5. Branch B's `false` is an owned verdict — deliberately stricter than vanilla for light chargers. `ChargeKnockdownContext` is the designated extension point for the planned future factors (collision angle, tiers, perks, attacker race).

## Configuration

`Main/_Module/ModuleData/combat_mechanics/combat_mechanics_config.json` — per-mechanic enables, all curve constants, creature id lists, unstoppable damage thresholds, shield-pen item/class lists, and the `raceModifiers` table (keyed by race NAME, `raceage/race_age_config.json` precedent). Validated by `CombatMechanicsConfigProvider` (`FiniteFloatValidator` before every range check; ordering invariants; unknown `weaponClasses` entries skipped via `Enum.TryParse<WeaponClass>`; revert-to-default + summary warning). Deserialized with `ObjectCreationHandling.Replace` so JSON lists/dicts replace compiled defaults instead of append-merging. **Reload scope: full application restart** (Singleton provider).

MCM: "Combat Mechanics" group (GroupOrder 24) — master + 8 per-mechanic toggles + `CrushThroughMaxChance` slider + `ChargeAutoKnockdownWeightRatio` slider. MCM merges over JSON per read (`CombatMechanicsSettingsProvider`, `SettingClamp`).

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` | The 9 thin overrides + boundary extractors |
| `Main/Features/CombatMechanics/CrushThroughService.cs` | Skill CTB curve + monster auto-CTB + orc shield-CTB |
| `Main/Features/CombatMechanics/ChargeKnockdownService.cs` | Weight-driven two-branch charge knockdown |
| `Main/Features/CombatMechanics/CreatureCombatService.cs` | Cleave (momentum + reaction), unstoppable, stagger multiplier |
| `Main/Features/CombatMechanics/ShieldPenetrationService.cs` | Penetration flag grants + runtime-flag shield-damage correction |
| `Main/Features/CombatMechanics/RaceCombatModifiersResolver.cs` | Race-name-keyed modifier table → per-raceId cache |
| `Main/Features/CombatMechanics/CombatMechanicsConfigProvider.cs` | JSON load + full semantic validation |
| `Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs` | MCM-over-JSON merge, master-toggle folding |
| `Main/Features/CombatMechanics/Domain/*.cs` | `CrushThroughContext`, `ChargeKnockdownContext`, `RaceCombatModifiers` |
| `Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs` | Parent (abstract since 2026-07-02) |
| `Main/SubModule.cs` (~:593) | Single registration: `AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(...))` |
| `TAOM.Tests/Features/CombatMechanics/*` | Service/provider/resolver tests + `CombatMechanicsModelInvariantsTests` (derivation + abstract parent + exact override set pins) |

## Dependencies

`IRaceManager` (race validation), `ICareerAgentStatService` (inherited career passives), `IPathService`/`IModLogger`, `FiniteFloatValidator`/`SettingClamp`, `Monster.Weight` + `RelativeSpeedLimitForCharge` from monsters.xml (Native + LOTRLOME + Alliance.Wargs).

## Tests

`TAOM.Tests/Features/CombatMechanics/` — full decision-matrix coverage per service (boundaries: dead zone 30/31, energy 25 gate, damage == threshold, roll == chance), config validation (one test per rule, NaN/∞/ordering/sign/unknown-string), validate-before-lookup regressions, and the model invariants pins. Engine-signature drift is covered by `GameModelOverrideBindingTests` + `tools/snapshot_api_surface.ps1 -Check`.

## How-To

- **Add a creature to cleave/unstoppable/monster-CTB**: add its `Monster.StringId` to the relevant list/dict in the JSON. Settlement variants (`X_settlement*`) are normalized automatically.
- **Add race flavor** (e.g. tree-spirits dig in): add a `raceModifiers` row — data, not code. Unknown race names are skipped with a warning.
- **Make a weapon pierce shields**: add its item id to `shieldPenetration.itemIds` or its class to `weaponClasses`.
- **Tune charge knockdown**: `neutralWeightRatio` anchors "vanilla feel" (horse+rider vs man); `autoKnockdownWeightRatio` (also an MCM slider) sets the bowled-over threshold; per-race resistance in `raceModifiers`.

## Performance

All overrides are per-hit. Services precompute lookups at construction (monster-id settlement variants are expanded into the lookup sets at construction — the per-call path is a bare `HashSet`/`Dictionary` probe), take `in` structs, and allocate nothing per call; the race resolver caches per race id; weapon-class names come from a static enum-name cache. Engine-sourced floats (momentum, charge velocity, knockdown resistance) are NaN-guarded with positive-polarity gates — NaN always fails the gate or defers to vanilla.

## Known limitations / follow-ups

- The native shield-damage underestimation (TW forums 470085/470117) needs a 1.4.6 control-battle re-verify; `runtimeShieldDamageCorrectionEnabled` ships true and should be flipped if the engine fixed it.
- Monster-id lists (`monsterCrushMonsterIds`, `cleaveMonsterIds`, `unstoppableDamageThresholds` keys) and `orcShieldCrushRaces` are syntax-cleaned but not resolvability-validated — a typoed id is inert (never matches) and logs no warning, unlike `raceModifiers` keys which get lazy unknown-name warnings. Deliberate (Codex P3, 2026-07-02): the monster registry is engine state, and adding an adapter for a typo diagnostic fails the simplicity criterion. Double-check ids against the Monster XMLs when editing.
- Cleave chains through shield blocks only when the block takes damage — a zero-shield-damage block (`InflictedDamage == 0`) keeps vanilla's Bounced termination (Codex P3; MCM hint wording matches).
- Per-race `knockdownResistanceMultiplier` applies only to the owned charge branch in v1; extending it to vanilla weapon knockdowns belongs in `TaomAgentStatCalculateModel.GetKnockDownResistance`.
- Troll Monster weight (160) equals uruk — if horse-vs-troll knockdowns still feel too likely, raise troll weights in LOTRLOME `monsters.xml` (data change, separate commit).
- Creature synthetic blows (spider/elephant BT via `RegisterBlow`) bypass `DecideCrushedThrough` but do route through the shrug-off/knockdown deciders — spot-check in control battles that unstoppable thresholds don't neuter the creatures' own received-stagger feel.
