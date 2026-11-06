# Combat Mechanics

## Overview

Seven battle-feel mechanics layered onto the single `AgentApplyDamageModel` slot: skill-based crush-through-block, monster auto-crush-through, orc shield-crush-through, creature cleave, creature stagger immunity (unstoppable), weight-driven charge knockdown, and config-granted shield penetration — plus a per-race combat-modifier table (dwarf/elf/orc flavor) that feeds several of them. All config/MCM-toggleable; master toggle off restores exactly the pre-feature behavior.

**Design notes:** mechanics specified as behavioral facts and implemented in TAOM's own architecture (no external code). The weight-driven charge knockdown and the race-modifier table are TAOM-original designs.

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

Thin model → `CombatMechanicsHooks` → four pure services (ADR-002/007; gamemodels.md rule 4): every override is base plus one call per feature, and the hooks extract primitives/DTOs at the boundary and delegate (#737). Services precompute `HashSet`/`Dictionary` lookups at construction (per-hit hot path: no LINQ or allocation per call) and take caller-supplied random rolls so probability tests are deterministic.

| Override | Service | Mechanic |
|---|---|---|
| `DecideCrushedThrough` | `CrushThroughService` | monster auto-CTB → orc shield-CTB → skill CTB curve; `?? base` keeps the vanilla 58f path |
| `CalculateRemainingMomentum` | `CreatureCombatService` | cleave momentum 0.3× for listed creatures (default zeroes momentum for ordinary weapons) |
| `DecideWeaponCollisionReaction` | `CreatureCombatService` | force `SlicedThrough` — prevents chain-termination on Bounced/Stuck branches (shield block, axe <50% HP, shrug-off, wrong bone) |
| `DecideAgentShrugOffBlow` | `CreatureCombatService` | base (vanilla + career) OR per-creature damage threshold; true sets `BlowFlags.ShrugOff` which also suppresses knockback/knockdown/dismount (intended) |
| `CalculateStaggerThresholdDamage` | `CreatureCombatService` | × race `staggerThresholdMultiplier`; vanilla shrug-off re-enters this via the REGISTERED model, so the multiplier feeds vanilla stagger automatically |
| `DecideAgentKnockedDownByBlow` | `ChargeKnockdownService` | the weight-driven two-branch rules (below), which `CombatMechanicsHooks.ChargeKnockdown` asks for a horse charge only; every other hit is base |
| `DecideMissileWeaponFlags` | `ShieldPenetrationService` | after base (preserves vanilla Javelin+Impale grant): OR-in `CanPenetrateShield`/`MultiplePenetration` for config-listed ids/classes. **SHIPS OFF since 2026-08-17, lists empty** (see "Shield penetration ships off" below) |
| `CalculateShieldDamage` | `ShieldPenetrationService` | ÷0.3 correction when penetration was granted at runtime only. **SHIPS OFF since 2026-08-17**: the underestimation premise was disproved against 1.4.8 |
| `GetHorseChargePenetration` | (config constant) | single source for the 0.4 constant — feeds both the vanilla fall-through and TAOM's Branch B; folds the mechanic toggle (disabled → vanilla `base` value, so a tuned value doesn't survive the feature being off) |

`DecideAgentKnockedBackByBlow` is deliberately NOT overridden (vanilla 0.7-dot glancing gate kept; the engine calls KnockedDown unconditionally on the charge path, so Branch A works without it).

Shared infrastructure: `RaceCombatModifiersResolver` (lazy race-key validation via `IRaceManager.IsValidRaceName` — the registry is engine state unavailable at load — plus per-raceId caching; invalid race ids resolve to Neutral, never the "human" fallback row) and monster-id normalization (`X_settlement`/`_settlement_fast`/`_settlement_slow` → `X`).

### Shield penetration ships off (2026-08-17)

The mechanic originally shipped ON with `weaponClasses: ["Javelin"]` and a
`runtimeShieldDamageCorrectionDivisor` of 0.3. Both defaults are now off and the grant lists are
empty. Javelins pierce shields only through the engine's own `Throwing.Impale` grant, as in the base
game. The code is untouched and re-enableable per item id or class.

Two things were wrong with the old default:

1. **The 3.33x was correcting a bug that does not exist for javelins.** The divisor was documented as
   a workaround for a native underestimation (TW forums 470085/470117) that supposedly hits missiles
   whose penetration flags are granted at runtime rather than statically. Read against 1.4.8,
   `MissionCombatMechanicsHelper.ComputeBlowDamageOnShield:531` picks the missile shield multiplier
   **by weapon class first** (`ThrowingAxe → 0.3f`, `Javelin → 0.5f`) and consults
   `CanPenetrateShield`/`MultiplePenetration` only for classes matching neither. For `Javelin`, the
   only class the config ever listed, the flags are never read. `baseShieldDamage / 0.3f` was a
   straight 3.33x.
2. **That inflated number is what decides penetration.** The shield damage becomes
   `attackCollisionData.InflictedDamage` (`MissionCombatMechanicsHelper:208`), which
   `Mission.cs:5774-5788` tests as
   `damage > ShieldPenetrationOffset + ShieldPenetrationFactor * shieldArmor` = `30.0 + 3.0 * armor`
   (`Native/ModuleData/managed_core_parameters.xml:232-236`). Against an armor-30 shield the bar is
   120: a javelin landing ~40 in vanilla is blocked, at 3.33x it lands ~133 and passes through into
   the soldier behind. Shields also broke ~3.3x faster, and a broken shield permits penetration
   unconditionally.

The grant also carried no attacker filter, so every AI skirmisher ignored the player's shield too.
That cuts both ways on the revert: because the old grant applied to AI and player alike, removing it
restores the base-game matchup on both sides at once. No culture gains a defensive edge it did not
previously have, which is why no compensating buff is owed. For scale, 82 of 882 troops across 9 of
18 cultures carry a Javelin-class weapon at all; only Dunland (63% of its roster) and Harad (39%) are
javelin-identity, and nine cultures carry none.

**"Vanilla parity" is narrower than it sounds.** `CharacterObject.GetPerkValue` returns `false` for
any non-hero, so vanilla's Impale grant only ever reached HEROES. No line troop can pierce a shield
with a javelin in the base game, at any skill, under any perk. `Impale` is a tier-10 perk gated at
Throwing 250 and mutually exclusive with `WeakSpot`; only 6 of 145 lord skill sets and 1 of 170
wanderer sets in TAOM reach that threshold. **Run the owed A/B with a hero thrower.** A troop
thrower reads "blocked" whether or not the change worked, and would prove nothing.

Orc shield-crush does not fill the gap for the orc cultures either: it is melee-only
(`DecideCrushedThrough` is reached from `GetDefendCollisionResults`, never from the missile path, and
TAOM's own gate requires `HasMeleeWeapon` + `IsSwing`). The 8 orc javelin carriers are melee
berserkers with a javelin in a secondary slot, so they keep their crush on the mace swing and lose
nothing at range.

`GetDefendCollisionResults` is an engine callback marked `[MBCallback(null, true)]` (multi-thread callable; v1.4.8
`Mission.cs:6454-6456`), so `DecideCrushedThrough` (`MissionCombatMechanicsHelper.cs:311`) may run off the main thread;
yotthani's v1.5.3 decompile shows native calling it from inside the parallel melee sweep (not re-traced on v1.4.8). The
crush path logs nothing per call and reads only state built at construction, apart from the crush roll's draw on the
engine's shared generator (`randomRoll: MBRandom.RandomFloat` in `CombatMechanicsHooks`), which the port programme
keeps (decision D16: concurrent draws cannot plausibly leave that generator stuck). Keep anything added to the path
read-only (the thread table in `.claude/rules/harmony-patches.md`).

**Existing players keep their own MCM value.** MCM merges over JSON per read
(`CombatMechanicsSettingsProvider.ShieldPenetrationEnabled`), so a profile that saved "Shield Penetration" as on stays on.
The empty grant lists are the second line of defence: `IsGranted` matches nothing, so the mechanic is
inert even with a stale toggle. Pinned by
`ShieldPenetrationServiceTests.ShippedDefaults_MechanicToggledOn_StillGrantsNothingAndLeavesShieldDamageAlone`.

### Charge knockdown formula (v2, #610)

```
speedRef    = charger Monster relative_speed_limit_for_charge if sane, else 4.3 (Native horse)
speedFactor = clamp01(chargeVelocity / speedRef)
weightRatio = (chargerWeight + riderWeight) / max(victimWeight, 1)        // Monster.Weight from monsters.xml
Branch A: weightRatio >= auto (MCM ChargeAutoKnockdownRatio, default 6) && speedFactor >= 0.4 -> knock down (ignores the 0.7-dot gate)
Branch B: requires BlowFlags.KnockBack (0.7-dot parity);
          pen = 0.4 × clamp(weightRatio / 6.0, 1.0, 2.5) × speedFactor
          knock down iff damage ≥ maxHealth × max(0, knockDownRes × raceResist − pen)
```

Calibration (v2, #610, ported from the 1.5.x line): neutral 6.0 is Native horse 400 + rider 80 over a human 80, so an ordinary horse against a man meets vanilla's rule. The v1 floor of 0.25 scaled the penetration down for every victim heavier than the charger; the 1.5.x line measured that a horse-vs-orc knockdown then needed 30 to 52 damage on hits that deal 5 to 15. v2: `minPenetrationFactor` 1.0, so the weight term never pushes a light charger below vanilla (lighter victims keep the bonus up to 2.5x); `autoKnockdownWeightRatio` 6, so a full-speed horse and man contact floors a man from any angle; the race rows do the resisting: `cave_troll` and `hill_troll` 4.0 (a threshold near 200 damage, above any horse; a mumak is Branch A), `dwarf` 2.5, `sauron` 3.0, and `uruk_hai` no longer 1.25. A race row applies only in Branch B, so only while the ratio stays under the auto threshold. Sauron's Monster weight is 80 in the installed Armory v2.0.26 (`monsters.xml`), so a Native horse (400) with a rider (80) is exactly 480 / 80 = 6.0: at the default threshold of 6 and speed factor 0.4 or more, that charge floors him through Branch A and his 3.0 row never applies. It applies to a riderless horse (5.0), a lighter charger, or a slower hit. The default moved from 8 to 6 with the 1.5.x port and stays for parity. Vanilla for reference (v1.4.8 `MissionCombatMechanicsHelper.cs:54-59`, `:74-90`, `:336-340`): a horse charge knocks back only on a head-on contact (dot product at least 0.7) and knocks down only then, when damage >= maxHP x max(0, knockdown resistance - 0.4). Branch B's `false` is still an owned verdict; `ChargeKnockdownContext` stays the extension point for future factors.

### Custom Battle

Custom Battle (and the editor's test battle) installs the engine's `CustomAgentApplyDamageModel` on a `BasicGameStarter` (v1.4.8 `CustomGame.InitializeGameModels`). The campaign model derives from `SandboxAgentApplyDamageModel`, whose `ApplyDamageReductions` reads `DefaultSkillEffects.MountedWeaponDamagePenalty` for a mounted attacker through `Campaign.Current` (v1.4.8 `SandboxAgentApplyDamageModel.cs:509-512`), null outside a campaign. So Custom Battle gets a twin, `TaomCustomBattleDamageModel` on the Custom Battle base, registered from `SubModule.RegisterCustomBattleModels`. It makes the campaign model's `CombatMechanicsHooks` calls in the same order; `CustomBattleDamageModelTests` fails when the campaign model gains an override or a hook call the twin lacks. Campaign-only by design: the career damage passives (they read heroes) and the Refuge reduction (it reads the defender's party). The MCM toggles apply in both modes.

Where the Custom Battle base answers differently (v1.4.8 `CustomAgentApplyDamageModel.cs`), the same delegate lands on a different number: crush-through falls back to a stricter base that needs a `CanCrushThrough` weapon (`:115-133`); the stagger threshold is the flat managed parameter (`:257-261`); shield damage is multiplied by 1.25 before the runtime correction (`:294-305`); the missile flags start with no perk grant (`:111-113`). Custom Battle results measured before this change are pre-#788 baselines.

## Configuration

`Main/_Module/ModuleData/combat_mechanics/combat_mechanics_config.json` — per-mechanic enables, all curve constants, creature id lists, unstoppable damage thresholds, shield-pen item/class lists, and the `raceModifiers` table (keyed by race NAME, `raceage/race_age_config.json` precedent). Validated by `CombatMechanicsConfigProvider` (`FiniteFloatValidator` before every range check; ordering invariants; unknown `weaponClasses` entries skipped via `Enum.TryParse<WeaponClass>`; revert-to-default + summary warning). Deserialized with `ObjectCreationHandling.Replace` so JSON lists/dicts replace compiled defaults instead of append-merging. **Reload scope: full application restart** (Singleton provider).

MCM: "Combat Mechanics" group (GroupOrder 24): master + 8 per-mechanic toggles + `CrushThroughMaxChance` slider + `ChargeAutoKnockdownRatio` slider (renamed from `ChargeAutoKnockdownWeightRatio` when its default moved from 8 to 6, so the 6 reaches saved settings). MCM merges over JSON per read (`CombatMechanicsSettingsProvider`, `SettingClamp`).

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` | The 10 overrides, each base plus one call per feature; under ADR-002's 150 lines (`CombatMechanicsModelInvariantsTests` pins the exact set, the ceiling and which agent reaches which seam) |
| `Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs` | Custom Battle's twin (#788) on `CustomAgentApplyDamageModel`: the nine 1.4.x Combat Mechanics overrides, no career passives, no Refuge; pinned by `CustomBattleDamageModelTests` |
| `Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs` | The model's boundary to the four services, one method per seam, with the context builders and primitive extractors (#737) |
| `Main/Features/CombatMechanics/CrushThroughService.cs` | Skill CTB curve + monster auto-CTB + orc shield-CTB |
| `Main/Features/CombatMechanics/ChargeKnockdownService.cs` | Weight-driven two-branch charge knockdown |
| `Main/Features/CombatMechanics/CreatureCombatService.cs` | Cleave (momentum + reaction), unstoppable, stagger multiplier |
| `Main/Features/CombatMechanics/ShieldPenetrationService.cs` | Penetration flag grants + runtime-flag shield-damage correction |
| `Main/Features/CombatMechanics/RaceCombatModifiersResolver.cs` | Race-name-keyed modifier table → per-raceId cache |
| `Main/Features/CombatMechanics/CombatMechanicsConfigProvider.cs` | JSON load + full semantic validation |
| `Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs` | MCM-over-JSON merge, master-toggle folding |
| `Main/Features/CombatMechanics/Domain/*.cs` | `CrushThroughContext`, `ChargeKnockdownContext`, `RaceCombatModifiers` |
| `Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs` | Parent (abstract since 2026-07-02) |
| `Main/SubModule.cs` | Single registration: `AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(...))`, handed `CombatMechanicsHooks` from the container |
| `TAOM.Tests/Features/CombatMechanics/*` | Service/provider/resolver tests + `CombatMechanicsModelInvariantsTests` (derivation + abstract parent + exact override set + line ceiling + agent-per-seam pins) + `CombatMechanicsHooksTests` + `CombatMechanicsContainerWiringTests` |

## Dependencies

`IRaceManager` (race validation), `ICareerAgentStatService` (inherited career passives), `IPathService`/`IModLogger`, `FiniteFloatValidator`/`SettingClamp`, `Monster.Weight` + `RelativeSpeedLimitForCharge` from monsters.xml (Native + LOTRLOME; the warg Monster moved into LOTRLOME on 2026-08-28).

## Tests

`TAOM.Tests/Features/CombatMechanics/` — full decision-matrix coverage per service (boundaries: dead zone 30/31, energy 25 gate, damage == threshold, roll == chance), config validation (one test per rule, NaN/∞/ordering/sign/unknown-string), validate-before-lookup regressions, and the model invariants pins. Engine-signature drift is covered by `GameModelOverrideBindingTests` + `tools/snapshot_api_surface.ps1 -Check`.

## How-To

- **Add a creature to cleave/unstoppable/monster-CTB**: add its `Monster.StringId` to the relevant list/dict in the JSON. Settlement variants (`X_settlement*`) are normalized automatically.
- **Add race flavor** (e.g. tree-spirits dig in): add a `raceModifiers` row — data, not code. Unknown race names are skipped with a warning.
- **Make a weapon pierce shields**: set `shieldPenetration.enabled` true, flip the "Shield Penetration" MCM toggle on, and add the item id to `shieldPenetration.itemIds` (preferred) or its class to `weaponClasses`. All three ship off/empty. Prefer item ids: a class grant hits every weapon of that class in every culture, player and AI alike. Leave `runtimeShieldDamageCorrectionEnabled` off unless you have measured a native underestimation for that specific class in `ComputeBlowDamageOnShield`. It does not exist for `Javelin` or `ThrowingAxe`, which the engine multiplies by class.
- **Tune charge knockdown**: `neutralWeightRatio` anchors "vanilla feel" (horse+rider vs man); `autoKnockdownWeightRatio` (also an MCM slider) sets the bowled-over threshold; per-race resistance in `raceModifiers`.

## Performance

All overrides are per-hit. Services precompute lookups at construction (monster-id settlement variants are expanded into the lookup sets at construction — the per-call path is a bare `HashSet`/`Dictionary` probe), take `in` structs, and allocate nothing per call; the race resolver caches per race id; weapon-class names come from a static enum-name cache. Engine-sourced floats (momentum, charge velocity, knockdown resistance) are NaN-guarded with positive-polarity gates — NaN always fails the gate or defers to vanilla.

## Known limitations / follow-ups

- The native shield-damage underestimation (TW forums 470085/470117) was re-checked against 1.4.8 on 2026-08-17 and does not apply to any class the config could reach: `ComputeBlowDamageOnShield` multiplies `Javelin` and `ThrowingAxe` by class and never reads the penetration flags. `runtimeShieldDamageCorrectionEnabled` now ships false. Owed: the in-game A/B (#320 item 4) confirming shields stop javelins again, and that an `Impale` perk holder still pierces.
- Monster-id lists (`monsterCrushMonsterIds`, `cleaveMonsterIds`, `unstoppableDamageThresholds` keys) and `orcShieldCrushRaces` are syntax-cleaned but not resolvability-validated — a typoed id is inert (never matches) and logs no warning, unlike `raceModifiers` keys which get lazy unknown-name warnings. Deliberate (Codex P3, 2026-07-02): the monster registry is engine state, and adding an adapter for a typo diagnostic fails the simplicity criterion. Double-check ids against the Monster XMLs when editing.
- Cleave chains through shield blocks only when the block takes damage — a zero-shield-damage block (`InflictedDamage == 0`) keeps vanilla's Bounced termination (Codex P3; MCM hint wording matches).
- Per-race `knockdownResistanceMultiplier` applies only to the owned charge branch in v1; extending it to vanilla weapon knockdowns belongs in `TaomAgentStatCalculateModel.GetKnockDownResistance`.
- Troll Monster weight (160) equals uruk — if horse-vs-troll knockdowns still feel too likely, raise troll weights in LOTRLOME `monsters.xml` (data change, separate commit).
- Creature synthetic blows (spider/elephant BT via `RegisterBlow`) bypass `DecideCrushedThrough` but do route through the shrug-off/knockdown deciders — spot-check in control battles that unstoppable thresholds don't neuter the creatures' own received-stagger feel.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/dread-aura.md](./dread-aura.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)

<!-- backlinks-end -->
