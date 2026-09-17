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

Thin model → four pure services (ADR-002/007; gamemodels.md rule 4): every override extracts primitives/DTOs at the boundary and delegates. Services precompute `HashSet`/`Dictionary` lookups at construction (per-hit hot path — no LINQ/allocation per call) and take caller-supplied random rolls so probability tests are deterministic.

| Override | Service | Mechanic |
|---|---|---|
| `DecideCrushedThrough` | `CrushThroughService` | monster auto-CTB → orc shield-CTB → skill CTB curve; `?? base` keeps the vanilla 58f path |
| `CalculateRemainingMomentum` | `CreatureCombatService` | cleave momentum 0.3× for listed creatures (default zeroes momentum for ordinary weapons) |
| `DecideWeaponCollisionReaction` | `CreatureCombatService` | force `SlicedThrough` — prevents chain-termination on Bounced/Stuck branches (shield block, axe <50% HP, shrug-off, wrong bone) |
| `DecideAgentShrugOffBlow` | `CreatureCombatService` | base (vanilla + career) OR per-creature damage threshold; true sets `BlowFlags.ShrugOff` which also suppresses knockback/knockdown/dismount (intended) |
| `CalculateStaggerThresholdDamage` | `CreatureCombatService` | × race `staggerThresholdMultiplier`; vanilla shrug-off re-enters this via the REGISTERED model, so the multiplier feeds vanilla stagger automatically |
| `DecideAgentKnockedDownByBlow` | `ChargeKnockdownService` | weight-driven two-branch (below); non-charge hits short-circuit to base |
| `DecideMissileWeaponFlags` | `ShieldPenetrationService` | after base (preserves vanilla Javelin+Impale grant): OR-in `CanPenetrateShield`/`MultiplePenetration` for config-listed ids/classes. **SHIPS OFF since 2026-08-17, lists empty** (see "Shield penetration ships off" below) |
| `CalculateShieldDamage` | `ShieldPenetrationService` | ÷0.3 correction when penetration was granted at runtime only. **SHIPS OFF since 2026-08-17**: the underestimation premise was disproved against 1.4.8 |
| `GetHorseChargePenetration` | (config constant) | single source for the 0.4 constant — feeds both the vanilla fall-through and TAOM's Branch B; folds the mechanic toggle (disabled → vanilla `base` value, so a tuned value doesn't survive the feature being off) |

`DecideAgentKnockedBackByBlow` was deliberately NOT overridden until 2026-09-16 (vanilla 0.7-dot glancing gate kept; the engine calls KnockedDown unconditionally on the charge path, so Branch A works without it). It now IS overridden, for one case only: a signature hero's side swing on an unmounted human (SignatureStrikes, #605) returns true, because vanilla never grants KnockBack to a melee swing. Horse charges and every non-signature hit still return `base`, so the glancing gate is untouched. The same feature's optional `ISignatureStrikeService` is asked first on the non-charge branch of `DecideAgentKnockedDownByBlow` (a signature overhead always floors the struck agent). See `docs/features/signature-strikes.md`.

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

**Existing players keep their own MCM value.** MCM merges over JSON per read
(`CombatMechanicsSettingsProvider:33`), so a profile that saved "Shield Penetration" as on stays on.
The empty grant lists are the second line of defence: `IsGranted` matches nothing, so the mechanic is
inert even with a stale toggle. Pinned by
`ShieldPenetrationServiceTests.ShippedDefaults_MechanicToggledOn_StillGrantsNothingAndLeavesShieldDamageAlone`.

### Charge knockdown formula (v2, #610)

```
speedRef    = charger Monster relative_speed_limit_for_charge if sane, else 4.3 (Native horse)
speedFactor = clamp01(chargeVelocity / speedRef)
weightRatio = (chargerWeight + riderWeight) / max(victimWeight, 1)        // Monster.Weight from monsters.xml
Branch A: weightRatio >= auto (MCM, default 6) && speedFactor >= 0.4   -> knock down (ignores the 0.7-dot gate)
Branch B: requires BlowFlags.KnockBack (0.7-dot parity);
          pen = penetration (MCM, 0.4) x clamp(weightRatio / neutral (MCM, 6.0), minFactor (MCM, 1.0), 2.5) x speedFactor
          knock down iff damage >= maxHealth x max(0, knockDownRes x raceResist - pen)
```

**What vanilla does, for reference (installed v1.5.3):** knock-back needs a head-on contact
(`ChargeDamageDotProduct >= 0.7`, `MissionCombatMechanicsHelper.cs:56-61`); knock-down is asked
only then and is `damage >= maxHP x max(0, (0.4 + 0.001 x Athletics) - 0.4)`
(`:76-96,344-348`, `SandboxAgentStatCalculateModel.cs:437-456`), i.e. `maxHP x 0.001 x Athletics`,
6 to 13 damage for an ordinary troop. Weight, speed and race play no part.

**Calibration since 2026-09-17 (#610, Mike: "cavalry should have a lot more knockdown").** The v1
floor of 0.25 scaled the penetration DOWN for every victim heavier than the charger, and TAOM's
enemies are heavy (orc 140, uruk / goblin / trolls 160, uruk-hai 180 against a horse + man of 480),
so a horse-vs-uruk knockdown needed 30 to 52 damage against vanilla's 7.8, four to seven times
harder, on hits that deal 5 to 15. The "a horse can't floor a troll" intent had landed on every
uruk because trolls and uruks share weight 160. v2: `minPenetrationFactor` 1.0 (never below
vanilla for a heavy victim; lighter victims keep the bonus up to 2.5x), `autoKnockdownWeightRatio`
6 (horse + man vs man is exactly 6.0, so a full-speed contact floors a man from ANY angle), the
race rows do the resisting: `cave_troll` / `hill_troll` 4.0 (threshold about 200 damage, above any
horse; a mumak is Branch A), `dwarf` 2.5, `sauron` 3.0, `uruk_hai` no longer 1.25. Branch B's
`false` is still an owned verdict. The three Branch B knobs are MCM sliders (Combat Mechanics
group) read per hit, so the feel is tunable in a running game; the auto slider's floor follows
the live neutral value. An existing `TAOM.json` keeps the old auto-ratio (8) until the slider is
moved or the group reset (MCM persists per property). `ChargeKnockdownContext` stays the extension
point for future factors (collision angle, tiers, perks, attacker race).

### Charge damage by culture (#610)

The engine builds `MountChargeDamage` from the horse item's `charge_damage` plus the harness
`charge_bonus` times 0.004 (`SandboxAgentStatCalculateModel.cs:1280`, `EquipmentElement.cs:411-429`);
a Monster carries no charge attribute. TAOM cavalry mostly ride shared vanilla horses
(`noble_horse_southern` on 258 rosters), so the kingdom feel is a multiplier on that value:
`chargeDamage.cultureMultipliers` in the JSON, keyed by the RIDER's culture id, applied by
`MountChargeDamageApplier` from both `AgentStatCalculateModel` slots (campaign
`TaomAgentStatCalculateModel`, Custom Battle `TaomCustomBattleAgentStatCalculateModel`) on mount
agents after base. The lookup hops through `Agent.RiderAgent`: a mount agent's own `Character` is
null (`Mission.CreateHorseAgentFromRosterElements` passes `null` to `CreateAgent`, v1.5.3
`Mission.cs:4611`), the same hop vanilla's `UpdateHorseStats` makes for the Riding skill. The
engine reads `MountChargeDamage` off the MOUNT per charge hit (`AttackInformation` takes it from
the attacker, and `Mission.ChargeDamageCallback`'s attacker is the horse, `Mission.cs:6103`), so
the multiply belongs on the mount's properties; a riderless horse gets 1.0. Placement follows the
BASE model's write site, because the applier is a plain `*=`: the Sandbox model rewrites the
property on every `UpdateAgentStats` (`:1280`), so the campaign model multiplies there and the
factor follows the current rider; the Custom Battle base writes it once in `InitializeAgentStats`
(`:48`) and never again, so the Custom Battle model multiplies from its own `InitializeAgentStats`
override and the factor follows the spawn rider (a multiply from `UpdateAgentStats` there compounds
on every re-run, which is what the second review caught). The first cut read the mount's own
`Character` and was a no-op in every battle (RCA `docs/reviews/rca-cavalry-charge-2026-09-17.md`).
Shipped (Mike, 2026-09-17): elves (`mirkwood`, `lothlorien`, `rivendell`, `lindon`)
1.6, Rohan (`vlandia`) 1.5, Rhun (`khuzait`) 1.4, `gondor` 1.3, Dale (`sturgia`) 1.2, the orc
kingdoms and Dunland / Harad / Khand 1.2, `erebor` 1.0. Range 0.1 to 5, a bad row is dropped with
a warning, every key is pinned against the culture registry by `ShippedCombatMechanicsConfigTests`.
MCM toggle `Culture Charge Damage` (folds the master). Harness `charge_bonus` on per-culture
barding remains the data lever for elite units on top of this.

## Configuration

`Main/_Module/ModuleData/combat_mechanics/combat_mechanics_config.json` — per-mechanic enables, all curve constants, creature id lists, unstoppable damage thresholds, shield-pen item/class lists, and the `raceModifiers` table (keyed by race NAME, `raceage/race_age_config.json` precedent). Validated by `CombatMechanicsConfigProvider` (`FiniteFloatValidator` before every range check; ordering invariants; unknown `weaponClasses` entries skipped via `Enum.TryParse<WeaponClass>`; revert-to-default + summary warning). Deserialized with `ObjectCreationHandling.Replace` so JSON lists/dicts replace compiled defaults instead of append-merging. **Reload scope: full application restart** (Singleton provider).

MCM: "Combat Mechanics" group (GroupOrder 24), 17 members as of 2026-09-17: the master, 10 per-mechanic toggles (skill crush-through, monster crush-through, orc shield crush-through, creature cleave, creature stagger immunity, weight-based charge knockdown, shield penetration, race combat modifiers, culture charge damage, signature strikes) and 6 sliders (`CrushThroughMaxChance`, `ChargeAutoKnockdownWeightRatio`, `ChargeNeutralWeightRatio`, `ChargeHorsePenetration`, `ChargeMinPenetrationFactor`, `SignatureStrikeCooldownMultiplier`). MCM merges over JSON per read (`CombatMechanicsSettingsProvider`, `SettingClamp`).

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
| `Main/SubModule.cs` (:913) | Single registration: `AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(...))` |
| `TAOM.Tests/Features/CombatMechanics/*` | Service/provider/resolver tests + `CombatMechanicsModelInvariantsTests` (derivation + abstract parent + exact override set pins) |

## Dependencies

`IRaceManager` (race validation), `ICareerAgentStatService` (inherited career passives), `IPathService`/`IModLogger`, `FiniteFloatValidator`/`SettingClamp`, `Monster.Weight` + `RelativeSpeedLimitForCharge` from monsters.xml (Native + LOTRLOME; the warg Monster moved into LOTRLOME on 2026-08-28).

## Tests

`TAOM.Tests/Features/CombatMechanics/` — full decision-matrix coverage per service (boundaries: dead zone 30/31, energy 25 gate, damage == threshold, roll == chance), config validation (one test per rule, NaN/∞/ordering/sign/unknown-string), validate-before-lookup regressions, and the model invariants pins. Engine-signature drift is covered by `GameModelOverrideBindingTests` + `tools/snapshot_api_surface.ps1 -Check`.

## How-To

- **Add a creature to cleave/unstoppable/monster-CTB**: add its `Monster.StringId` to the relevant list/dict in the JSON. Settlement variants (`X_settlement*`) are normalized automatically.
- **Add race flavor** (e.g. tree-spirits dig in): add a `raceModifiers` row — data, not code. Unknown race names are skipped with a warning.
- **Make a weapon pierce shields**: set `shieldPenetration.enabled` true, flip the "Shield Penetration" MCM toggle on, and add the item id to `shieldPenetration.itemIds` (preferred) or its class to `weaponClasses`. All three ship off/empty. Prefer item ids: a class grant hits every weapon of that class in every culture, player and AI alike. Leave `runtimeShieldDamageCorrectionEnabled` off unless you have measured a native underestimation for that specific class in `ComputeBlowDamageOnShield`. It does not exist for `Javelin` or `ThrowingAxe`, which the engine multiplies by class.
- **Tune charge knockdown**: four MCM sliders, live: `Charge Neutral Weight Ratio` (where the weight term equals vanilla), `Charge Penetration` (the base; 0.5 and up floors an ordinary man on any head-on hit that does damage), `Charge Min Penetration Factor` (1.0 = never below vanilla for heavy victims; 0.25 = the old feel), `Auto-Knockdown Weight Ratio` (6 = horse + man vs man from any angle; 8 = wargs and chariots only). Per-race resistance stays in `raceModifiers` (JSON, restart).
- **Tune charge damage by culture**: `chargeDamage.cultureMultipliers` in the JSON (restart); the `Culture Charge Damage` toggle turns the table off live.

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
