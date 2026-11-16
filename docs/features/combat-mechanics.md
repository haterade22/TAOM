# Combat Mechanics

## Overview

Seven battle-feel mechanics layered onto the single `AgentApplyDamageModel` slot: skill-based crush-through-block, monster auto-crush-through, orc shield-crush-through, creature cleave, creature stagger immunity (unstoppable), weight-driven charge knockdown, and config-granted shield penetration, plus a per-race combat-modifier table (dwarf/elf/orc flavor) that feeds several of them. All config/MCM-toggleable; master toggle off restores exactly the pre-feature behavior. An eighth mechanic since #610 rides the `AgentStatCalculateModel` slot instead: the per-culture cavalry charge multiplier (`IChargeDamageService` + `MountChargeDamageApplier`, applied from both the campaign and the Custom Battle stat model). A ninth, Hammerhand (#895), rides the damage model again: a shield bash or kick can knock a man down (see [Hammerhand](#hammerhand-shield-bash-and-kick-knockdown-895)). A tenth, Cleaving Blows, rides `CalculateRemainingMomentum` and `DecideWeaponCollisionReaction`: a two-handed swing by the agent a human controls can cut through several enemies (see [Cleaving Blows](#cleaving-blows)).

**Custom Battle (#788):** the seven mechanics, Hammerhand (#895), the per-race table (its `baseHitPoints` row excepted: Custom Battle reads health from the Monster's `hit_points`) and the horse charge penetration slider run in Custom Battle too, through `TaomCustomBattleDamageModel` (see [Custom Battle](#custom-battle)). Before #788 they were campaign-only: the campaign model derives from SandBox's, which cannot run outside a campaign, and Custom Battle's only TAOM damage model carried the creature, race-ability and siege hooks and none of these. The career passives, the career crush hold and the Refuge reduction stay campaign-only (they read heroes and parties).

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

Thin model → `CombatMechanicsHooks` → six pure services (ADR-002/007; gamemodels.md rule 4): every override is base plus one call per feature, and the hooks extract primitives/DTOs at the boundary and delegate (#737). Services precompute `HashSet`/`Dictionary` lookups at construction (per-hit hot path: no LINQ or allocation per call) and take caller-supplied random rolls so probability tests are deterministic.

| Override | Service | Mechanic |
|---|---|---|
| `DecideCrushedThrough` | `CrushThroughService` | race-ability verdict first (Stand Fast holds, Berserk forces; [race-abilities.md](race-abilities.md)) → monster auto-CTB → orc shield-CTB → skill CTB curve; `?? base` keeps the vanilla 58f path; campaign model only, the whole verdict then passes through `CareerTroopHooks.ApplyCrushHold` (#769 E10): a crush-through on a defending troop whose leader holds the `TroopCrushHold` pip can become a hold, and a hold is never turned into a crush |
| `ApplyDamageAmplifications` | `RaceAbilityHooks` | a live race ability's melee or ranged damage, after the career amplification the parent model applies |
| `CalculateRemainingMomentum` | `CreatureCombatService` | cleave momentum 0.3× for listed creatures (default zeroes momentum for ordinary weapons); then `CleavingBlowService` (Cleaving Blows) for a qualifying swing |
| `DecideWeaponCollisionReaction` | `CreatureCombatService` | force `SlicedThrough`: prevents chain-termination on Bounced/Stuck branches (shield block, axe <50% HP, shrug-off, wrong bone); then `CleavingBlowService` (Cleaving Blows) for a qualifying swing |
| `DecideAgentShrugOffBlow` | `CreatureCombatService` | base (vanilla + career) OR per-creature damage threshold OR a live race ability's shrug-off (`RaceAbilityHooks.ShrugsOff`); true sets `BlowFlags.ShrugOff`, which suppresses knock-back, knockdown and dismount for weapon hits (intended); an alternative attack still knocks back (`MissionCombatMechanicsHelper` :63-66), and Hammerhand ignores the flag because vanilla sets it on nearly every bash and kick (see Hammerhand) |
| `CalculateStaggerThresholdDamage` | `CreatureCombatService` | × race `staggerThresholdMultiplier`; vanilla shrug-off re-enters this via the REGISTERED model, so the multiplier feeds vanilla stagger automatically |
| `DecideAgentKnockedDownByBlow` | `ChargeKnockdownService` | a signature slam first (`SignatureStrikeVerdicts`), then the weight-driven two-branch rules (below), which `CombatMechanicsHooks.ChargeKnockdown` asks for a horse charge only, then Hammerhand (`ShieldBashKnockdownService`, below), which `CombatMechanicsHooks.ShieldBashKnockdown` asks for a shield bash or kick only; every other hit is base |
| `DecideMissileWeaponFlags` | `ShieldPenetrationService` | after base (preserves vanilla Javelin+Impale grant): OR-in `CanPenetrateShield`/`MultiplePenetration` for config-listed ids/classes. **SHIPS OFF since 2026-08-17, lists empty** (see "Shield penetration ships off" below). Since #1361 `CombatMechanicsHooks.PenetrationFlags` then adds `CanPenetrateShield` (never `MultiplePenetration`) for a ranged hero kit's shooter; that grant is not behind the MCM switch (`ShieldPenetrationService.cs:51`) |
| `CalculateShieldDamage` | `ShieldPenetrationService` | ÷0.3 correction when penetration was granted at runtime only. **SHIPS OFF since 2026-08-17**: the underestimation premise was disproved against 1.4.8 |
| `ApplyGeneralDamageModifiers` | `ShieldPierceMath` (#1361) | after `base`: for a missile a shield blocked, shot by a ranged hero kit with `pierceShields` (`CombatMechanicsHooks.ShooterPiercesShields`, set by the hero abilities), the damage is raised to `ceil(offset + factor x shield armour) + 1`, the engine's own pass-through threshold read from `ManagedParameters` (30 and 3, `Mission.cs:5836`); every other hit is returned unchanged. It is the last step of `CalculateDamage`, so no later reduction undoes it; [hero-abilities.md](hero-abilities.md) "The ranged kit" |
| `ApplyDamageScaling` | `CreatureSiegeHooks` (Creature Siege Role) | after `base`: a creature's melee blow on one of the mission's castle gates is multiplied (`siege/creature_siege_role.json`, provisional 2.0); every other hit is returned unchanged; [creature-siege-role.md](creature-siege-role.md) |
| `GetHorseChargePenetration` | `ICombatMechanicsSettingsProvider.ChargeHorsePenetration` | single source for the penetration (JSON 0.4, MCM-live since #610): feeds both the vanilla fall-through and TAOM's Branch B; folds the mechanic toggle (disabled: vanilla `base` value 0.4, so a tuned value doesn't survive the feature being off) |

`DecideAgentKnockedBackByBlow` was deliberately NOT overridden until 2026-09-16 (vanilla 0.7-dot glancing gate kept; the engine calls KnockedDown unconditionally on the charge path, so Branch A works without it). It now IS overridden, for one case only: a signature strike whose profile sets `knockBack`, landing on an unmounted human, returns true (SignatureStrikes: Sauron's side-swing Sweep, #605; the Nine's Scream on an overhead or a side swing, #645), because vanilla never knocks back an ordinary swing (v1.5.4: `MissionCombatMechanicsHelper.DecideAgentKnockedBackByBlow` grants every kick or bash before it asks `CanWeaponKnockback`, which allows a weapon hit only on the head to shoulders, from a weapon without `CanKnockDown`, and only for a missile, a crush-through or a `WideGrip` thrust). Horse charges and every non-signature hit still return `base`, so the glancing gate is untouched. The same feature's optional `ISignatureStrikeService` is asked first on the non-charge branch of `DecideAgentKnockedDownByBlow` (a signature strike whose profile sets `knockDown`, Sauron's Slam, floors the struck agent; the Nine's Scream does not). See `docs/features/signature-strikes.md`.

Shared infrastructure: `RaceCombatModifiersResolver` (lazy race-key validation via `IRaceManager.IsValidRaceName` — the registry is engine state unavailable at load — plus per-raceId caching; invalid race ids resolve to Neutral, never the "human" fallback row) and monster-id normalization (`X_settlement`/`_settlement_fast`/`_settlement_slow` → `X`).

### Custom Battle

Custom Battle (and the editor's test battle) installs the engine's `CustomAgentApplyDamageModel` on a `BasicGameStarter`; other game types that build one, such as the naval Custom Battle, install other models (see below). The campaign model derives from `SandboxAgentApplyDamageModel`, and registering a SandBox-derived model there would throw on the first mounted hit: `ApplyDamageReductions` reads `DefaultSkillEffects.MountedWeaponDamagePenalty`, which resolves through `Campaign.Current` (`SandboxAgentApplyDamageModel.cs:641-644`), null outside a campaign. So Custom Battle gets a twin, `TaomCustomBattleDamageModel : CustomAgentApplyDamageModel` (#788), hand-wired in `SubModule.RegisterCustomBattleModels` with `basicStarter.AddModel<AgentApplyDamageModel>`. It takes `CombatMechanicsHooks` and the two optional Signature Strikes services, resolved the way the campaign registration resolves them, and overrides the same 13 methods as the campaign model, each `base` plus the same hook in the same order (`CustomBattleDamageModelTests` compares the IL call order of the two, method by method). No feature module declares an `AgentApplyDamageModel` for Custom Battle: the module step runs after `RegisterCustomBattleModels`, the last model added wins, and a module fault latches for the session, so a module-declared twin would shadow this one or vanish. The old `TaomCustomBattleCreatureDamageModel` (declared by `CreatureBanditsModule` since #692, carrying only the creature bandit, race-ability and siege hooks) is gone; its hooks are in the twin.

**Campaign-only by design:** the career damage passives (`TaomAgentApplyDamageModel` reads `HeroObject` and party leaders, and Custom Battle has no heroes or careers), the career crush hold (`CareerTroopHooks.ApplyCrushHold`, which `CustomBattleDamageModelTests.CampaignOnly` lists) and the Refuge reduction (reads the defender's party). The MCM toggles and sliders are global settings, so they apply in Custom Battle too; the race table depends on face-gen races, not on a campaign.

**Custom Battle only:** the Fords of War army upgrades, hero damage factors and pierce rule against cavalry (`FordsUpgradeHooks`, `FordsPierceHooks`; `heroes.damageFactor` 3, `heroes.damageTakenFactor` 0.4, `pierceVsMountedFactor` 2), the last step of the reduction override but one, and the Long Defeat's damage factors: the wave's and the boons' damage factor (`LongDefeatCombatHooks.Amplify`), the last step of the amplification override, which passes a fall through, and the boons' damage-taken factor (`LongDefeatCombatHooks.Reduce`), the last step of the reduction override. Each is inert in every other battle, and the twin test names them in `TwinOnly`.

**Where the Custom Battle base answers differently from SandBox's**, so the same TAOM delegate lands on a different number:

| Method | Custom Battle base | SandBox base | Effect on TAOM's delegate |
|---|---|---|---|
| `CalculateStaggerThresholdDamage` | the flat managed-parameter threshold per damage type | the same threshold times the Spartan / Dauntless Steed / Deft Hands perk bonuses | the per-race multiplier scales the flat number; only the base shrug-off check re-enters this method through the registered model, in both modes; the unstoppable check compares the damage against a per-creature threshold and never reads it |
| `DecideCrushedThrough` | needs a `CanCrushThrough` weapon, an overhead swing and energy above 58 (x1.2 against a shield) | the same without the weapon flag test | TAOM's skill, monster and orc rules only add a true and need no flag, as in the campaign; where they decline, Custom Battle's stricter base decides, so a weapon without the flag never crushes through on energy alone there |
| `CalculateShieldDamage` | multiplies by 1.25 and applies the banner reduction | applies the banner reduction only | the runtime shield-damage correction (ships off) rides on a 1.25x larger number |
| `DecideMissileWeaponFlags` | grants nothing | grants the Impale flag to a javelin thrower with the perk | penetration flags start from the weapon's own flags with no perk grant; ships off, lists empty |
| `GetKnockDownPenetration`, `GetKnockBackPenetration`, `GetDismountPenetration` | flat values, no perks | add Polearm, Two-Handed, Crossbow and Throwing perk bonuses | not overridden by TAOM; they apply to the weapon knockdown, knock-back and dismount fall-throughs only. A charge's fall-through reads `GetHorseChargePenetration` (the weapon is null in `ChargeDamageCallback`), and the Signature Strikes verdicts read no penetration |
| `CalculateRemainingMomentum`, `DecideWeaponCollisionReaction`, `GetHorseChargePenetration` | the engine's default momentum, `MissionCombatMechanicsHelper`'s reaction, 0.4 | the same | none: the cleave, slice-through and charge-penetration delegates behave as in the campaign. (`DecidePassiveAttackCollisionReaction` does differ between the bases, Bounced against a 5% chance of reaction 0 plus the Skewer perk on a fatal mounted hit, but TAOM overrides neither) |
| `ApplyDamageReductions` | banner reductions for any character, and a naval-battle rule: minus 15% for an AI archer's arrow or bolt | banner and perk and trait reductions, only for a `CharacterObject` victim, and, for a mounted attacker not using a crossbow, the mounted-weapon riding penalty (the line that throws outside a campaign) | the creature bandit and race-ability reductions ride after either base; the campaign's mounted-attacker penalty is absent in Custom Battle |

Charge knockdown reads the victim's knockdown resistance through `MissionGameModels.Current.AgentStatCalculateModel`, which in Custom Battle is `TaomCustomBattleAgentStatCalculateModel` over the Custom Battle base. The two stat models use the same formula (0.4 + 0.001 x Athletics, +0.1 mounted, +0.15 against a thrust; `CustomBattleAgentStatCalculateModel.cs:134-151` against `SandboxAgentStatCalculateModel.cs:437-455`, whose skill effect is 0.4 base and 0.001 per point), so there is no gap to measure. In a campaign, `TaomAgentStatCalculateModel.GetKnockDownResistance` ends with the career knockdown counter (`CareerTroopHooks.KnockDownResistance`, `TroopKnockdownResistance`, #769 E10), so the engine's weapon, charge and dismount knockdown checks all read it, and Hammerhand, which never asks that method, takes the counter's `1 + m` as its victim resistance factor (`CombatMechanicsHooks.BuildShieldBashContext`); TAOM's forced knockdowns (Sauron's overhead slam, the struck foe and the ring, while the Nine's strikes never knock down; the elephant-like creature attacks: elephant, mumakil trample and tusk swing, war ram, great elk, Animalia elk and moose; the troll brute-force ring) set the flag without reading resistance and ignore it, and the heavy-charge Branch A ignores the resistance. TAOM's synthetic creature blows (the warg, spider, elephant, mumakil, war ram, elk and troll brute-force behaviours register a pre-computed blow through `CustomAttacksUtils` into `Mission.RegisterBlow` and `Agent.HandleBlow`) ask no damage-model method in either mode, and the twin does not touch them. `NavalCustomGame` also builds a `BasicGameStarter` and installs `NavalDLCCustomAgentApplyDamageModel`, which the twin would replace; TAOM declares the Naval DLC incompatible, so this is moot unless that changes.

**Baselines that move:** the Custom Battle A/B measurements (culture doctrine, see [culture-doctrine.md](culture-doctrine.md)) and the Signature Strikes smoke ([signature-strikes.md](signature-strikes.md)) both run in Custom Battle. With #788 the crush-through, cleave, unstoppable, stagger, charge knockdown and Signature Strikes knockdown rules are live there, so any earlier Custom Battle result is a pre-#788 baseline: re-run it before comparing. The same holds for the Race Abilities Custom Battle runs ([race-abilities.md](race-abilities.md), "How to verify in game" and the 2026-10-04 test): test 1 now mixes the berserkers' forced crush-through with the orc shield crush-through, so only the `crush forced` counter isolates the ability.

### Missile blocked with a weapon

`AttackCollisionData.MissileBlockedWithWeapon` is set by native code only. [TAOM-verified, v1.5.7 ilspy] In `Mission.MissileHitCallback` the branch `else if (collisionData.MissileBlockedWithWeapon)` (`Mission.cs:5861-5864`) calls `GetAttackCollisionResults` and sets `MissileCollisionReaction.BounceBack`, with no `RegisterBlow`. `MissionCombatMechanicsHelper.cs:219-223` sets `InflictedDamage = 0` and `AbsorbedByArmor = 0` and skips `ComputeBlowDamage`. `OnMissileHit` still fires (`Mission.cs:6023-6026`). The trigger is `AgentFlag.CanDeflectArrowsWith2HSword` (`AgentFlag.cs:34`); in the decompiled set only `SandboxAgentStatCalculateModel.cs:45-47` sets it, for the perk `TwoHanded.ProjectileDeflection` (the cached types, not whole DLLs). So on the managed path a weapon-blocked missile registers no blow and reaches no `OnAgentHit`. `Mission.OnAgentHitBlocked` (`Mission.cs:5537-5550`) is a native callback that can build a missile blow and call `OnAgentHit` with `isBlocked: true`; which collisions native code sends there cannot be read from managed code. TAOM's `LongDefeatVolleyHooks.AgentHit` (`LongDefeatVolleyHooks.cs:22`) reads `collisionData.MissileBlockedWithWeapon`, which is false on the managed path and harmless if native code ever sets it there; the `OnMissileHit` hook (`LongDefeatMissionLogic.cs:97-100`) is the path that sees the flag. yotthani read the same path from a decompile (`yotthani/MithrilForge` `docs/engine/combat.md`@ea7a7bfb line 46, section "Missile blocked with weapon"; the section names no game version; the v1.5.7 branch is `Mission.cs:5861`). yotthani's Custom Battle spike lists `MissileBlockedWithWeapon` beside `AttackBlockedWithShield` among the blocked hit rows (`yotthani/bannerlord` `docs/balance-sim/app-0.3-spike.md`@c45d8374 line 39, v1.5.6, observation of `OnAgentHit` rows). Only the Sandbox model sets the flag in the decompiled set, and TAOM's code never sets it, so in Custom Battle those rows are shield blocks [TAOM's reading, v1.5.7, not run]. The remark that the AI raises only a shield against missiles is yotthani's, "apparently", not re-checked by TAOM.

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

`GetDefendCollisionResults` is an engine callback marked `[MBCallback(null, true)]` (multi-thread callable,
v1.5.4), and native calls it from inside the parallel melee sweep, so `DecideCrushedThrough` may run off the
main thread. The whole crush path (race-ability verdict, `CrushThroughService`, settings and race lookups) writes
only the race-ability telemetry (`RaceAbilityTelemetry.Add`, `Interlocked`) and, from the career hold, the troop counter tally and the hook fault set under a lock; it logs nothing per call but a first hook fault; keep it that way (the thread table in
`.claude/rules/harmony-patches.md`). It is not read-only: `CombatMechanicsHooks` draws `MBRandom.RandomFloat`
(`Game.Current.RandomGenerator`, unsynchronized) for the skill roll on this multi-threaded callback. That is the
campaign model's long-standing behaviour and, since #788, Custom Battle's too. Two concurrent draws corrupt the
generator's XorShift state (reproduced against the installed v1.5.5 DLL, 2026-10-15); how often native runs two
crush checks at once is unverified, and moving this draw to a locked generator is a follow-up. The campaign
model's career hold at the end of the chain (`CareerTroopHooks.ApplyCrushHold`, #769 E10) draws its own
`System.Random` under its own lock, never `MBRandom`, and only when the verdict is a crush-through and the
defending troop's leader holds a `TroopCrushHold` pip for its class. On a hold it also counts the hold in the stat service's
troop counter tally under a lock, and a first fault logs once; those, with the race-ability telemetry, are the only TAOM writes on this path.

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
the live neutral value. The slider is `ChargeAutoKnockdownRatio`, renamed in #1017 from
`ChargeAutoKnockdownWeightRatio` so a saved 8 is reset to the new 6 (MCM persists per property). `ChargeKnockdownContext` stays the extension
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
1.6, Rohan (`vlandia`) 1.5, Rhun (`khuzait`) 1.4, `gondor`, `arthedain` and `cardolan` 1.3, Dale
(`sturgia`) and `rhudaur` 1.2, the orc kingdoms and Dunland / Harad / Khand 1.2, `erebor` and
`eredluin` 1.0. Range 0.1 to 5, a bad row is dropped with
a warning, every key is pinned against the culture registry by `ShippedCombatMechanicsConfigTests`.
MCM toggle `Culture Charge Damage` (folds the master). Harness `charge_bonus` on per-culture
barding remains the data lever for elite units on top of this.

### Hammerhand: shield bash and kick knockdown (#895)

Named for Helm Hammerhand of Rohan, who felled Freca with a single blow. A shield bash or an empty-handed kick can now
knock a man down. Vanilla never does: a bash or kick always knocks back, but a shield lacks the `CanKnockDown` weapon flag,
so the weapon-knockdown rule never says yes, and a kick has no weapon at all.

**Rule** (`ShieldBashKnockdownService.Decide`, a pure service over a `ShieldBashContext` of primitives; it returns `true` or
`null`, never `false`, so it adds a knockdown and never takes a vanilla one away):

1. `null` when the toggle is off (folded into the Combat Mechanics master), the hit is not an alternative attack, it is a
   horse charge, the attacker is not a humanoid (`Agent.IsHuman`), or a Bash is struck with something that is not a shield
   (SmallShield or LargeShield).
2. `null` when the victim is immune (below).
3. chance (in percent) = the attacker's effective Athletics minus the victim's, clamped to 10 to 75.
4. divided by the victim race's `knockdownResistanceMultiplier` (dwarf 2.5, Sauron 3, trolls 4), after the clamp. A row of 0
   means no resistance, so the man always falls, as in `ChargeKnockdownService`, while the clamped chance is above 0 (with
   `minChancePercent` 0 and no lead, 0 divided by 0 is NaN and nobody falls).
5. divided by a live race ability's knockdown resistance factor (`RaceAbilityHooks.KnockDownResistance(victim, 1f)`: 1 with no
   ability, 2 for +100%), first scaled by the career `TroopKnockdownResistance` counter of the victim's party leader, class
   filtered (`1 + m`, #769 E10; the service applies it only here, after the gates above, so a declined bash is not counted).
   A non-finite or non-positive factor counts as 1.
6. times `kickChanceFactor` (0.5) when the attacker's hand is empty (a kick).
7. times `playerVictimChanceFactor` (0.5) when the victim is the agent the player controls now (after a takeover, the soldier
   taken over).
8. `true` when `roll * 100 < chance` (strict, roll 0 to 1), else `null` and vanilla's `false` stands. A NaN roll never wins.

Every humanoid attacker counts, the AI included: a soldier's bash can floor the player, at half the rate it would floor a
soldier. The victim may be on any team; in single player a friendly melee hit is cancelled (`Mission.cs:6351-6372`), and
`Agent.HandleBlow` returns before the native step when `InflictedDamage <= 0` (`Agent.cs:5486-5489`), so an ally falls only where
friendly fire is on.

**Immunities, and why the ShrugOff flag is not used.** Vanilla `DecideAgentShrugOffBlow` sets ShrugOff whenever the victim
survives and `InflictedDamage` is at most the stagger threshold (`MissionCombatMechanicsHelper.cs:16-25`). A bash or kick has
magnitude 1 or 2 (`SandboxAgentApplyDamageModel.CalculateAlternativeAttackDamage`, :1172-1195), so it does 1 to 3 damage against
a blunt threshold of 5.0 (Native `managed_core_parameters.xml`, `DamageInterruptAttackthresholdBlunt`). Nearly every bash and
kick therefore carries ShrugOff, and a Hammerhand that declined on the flag would almost never fire. Native `HandleBlowAux`
still builds the hit reaction for a `NoIgnore` blow (every bash and kick is `NoIgnore`, `Mission.cs:5569`), and the reaction
selector honours `KnockDown` (Ghidra decompile, RVA `0x5EB4E0` and `0x601350`; the visual itself is still owed). So the flag is
never read. Three deliberate immunities replace it, folded into one context bool `VictimIsImmune` by the facade:

- **Creature stagger immunity:** `CreatureCombatService.IsUnstoppable(victim monster id, inflicted damage)`. Trolls are immune
  while Creature Stagger Immunity is on (the default; their threshold, 15, is above any bash), so the 2.5% to 18.75% a troll
  would face holds only with that toggle off.
- **A live race ability that shrugs off blows:** `RaceAbilityHooks.IsShruggingOff(victim)` (the dwarves' Stand Fast). It is the
  answer `ShrugsOff` gives without adding to the race ability's shrug-offs telemetry, since Hammerhand only asks.
- **The hero career shrug-off passive:** `victim.IsHero && ICareerAgentStatService.ShouldShrugOffBlow(hero id)`, the test
  `TaomAgentApplyDamageModel.DecideAgentShrugOffBlow` makes. Custom Battle has no campaign heroes, so it answers false there.

**Wiring.** `CombatMechanicsHooks.ShieldBashKnockdown` is the facade, with one cost gate: `collisionData.IsAlternativeAttack` and a
`Character` on both fighters, else `null` before any skill read. The builder then reads both fighters' Athletics through the
registered `AgentStatCalculateModel.GetEffectiveSkill` (`int`; no perk or race ability touches Athletics in v1.5.5), the
immunities, the victim's race id and ability resistance factor, `Agent.IsHuman` of the attacker, `Agent.IsMainAgent` of the victim
and `MBRandom.RandomFloat`. The toggle, the horse charge, the shield class (`IsShieldClass`), the humanoid test and the immunity
verdict are the service's. Both damage models add one `??` link in `DecideAgentKnockedDownByBlow`: Signature Strikes, then the
charge rules, then Hammerhand, then base. Signature Strikes already declines alternative attacks
(`SignatureStrikeService.cs:142`), so the two never overlap. `ICareerAgentStatService` is a container registration
(`CareerSystemIoC`), so the hooks take it by constructor in both game types.

**Config.** A `shieldBash` block in `combat_mechanics_config.json`: `enabled` true, `minChancePercent` 10, `maxChancePercent` 75,
`kickChanceFactor` 0.5, `playerVictimChanceFactor` 0.5. Percents must be finite and in 0 to 100 with min at most max; both factors
finite and in 0 to 1. An invalid value reverts to its compiled default with a warning (a swapped band reverts both ends). The
service trusts the provider's validation. Since the troop skill curve (#343, [troop-skill-balance.md](troop-skill-balance.md)) a foot troop's Athletics is its tier's value on the curve plus its culture's percentage, so the lead of a higher tier over a lower one grew: measured on 2026-11-15 over every pair of foot troops (no Horse slot, not in the Cavalry or HorseArcher group), the median lead one tier apart went from 15 to 30 and two tiers apart from 30 to 60, and the mean chance in the 10 to 75 band from 22 to 30 percent and from 33 to 53 percent (same tier 14 and 14, five tiers apart 67 to 74). The rule gives a mounted troop half the Athletics value of a foot troop, so a bash against a dismounted rider lands more often than against a foot troop of his tier. The band was not changed (2026-11-15, the session's call under the maintainer's word to act on its recommendation: an elite that knocks a lower tier down more often fits the curve). No sliders. MCM toggle `Hammerhand (Shield Bash Knockdown)` (Combat Mechanics group,
Order 17, on by default, folds the master, JSON `enabled` when MCM is absent, on a client without MCM settings only; a dedicated server uses `taom-server-settings.json` ([server-settings](server-settings.md))). It is co-op relevant: it changes the simulation, so
it is counted in the settings fingerprint.

**Engine facts** (v1.5.5, read from the installed DLLs):

- `Mission.CreateMeleeBlow` types an alternative attack `AgentAttackType.Bash` when the weapon is not empty and `Kick` when it
  is (`Mission.cs:5559`; empty means `MissionWeapon.CurrentUsageItem == null`, `MissionWeapon.cs:67-75,136`, so a kick reaches the
  damage model with a null weapon). The only melee knockdown ask is `Mission.cs:5631`, inside `CreateMeleeBlow`, which
  `MeleeHitCallback` and `OnAgentHitBlocked` use. Lines `:5917` and `:5937` are `MissileHitCallback`, which never carries a kick
  or bash. A blocked bash cannot floor anyone: `MeleeHitCallback` registers only unblocked hits (`:5424`) and `OnAgentHitBlocked`
  only notifies observers.
- The ask is made only for a human victim on foot; a rider goes to the dismount check instead.
- `MissionCombatMechanicsHelper.DecideAgentKnockedDownByBlow` refuses on ShrugOff, and sends a non-charge hit to the weapon rule only
  when the weapon is non-null (`MissionCombatMechanicsHelper.cs:76-96`). Every alternative attack knocks back (`:63-66`).
- The horse charge callback passes a null weapon and `IsHorseCharge` (`Mission.cs:6121`); Hammerhand declines it.
- `AgentStatCalculateModel.GetEffectiveSkill(Agent, SkillObject)` is virtual and SandBox overrides it
  (`SandboxAgentStatCalculateModel.cs:237`); the base dereferences `agent.Character`, hence the facade's Character gate.

**UNVERIFIED** (the in-game spike below settles them):

1. Whether the engine plays the visual knockdown for a Bash or Kick blow that carries the `KnockDown` flag. The native reading says
   yes; the visual is owed.
2. Whether a shield bash always reaches the damage model with the shield as `attackerWeapon` rather than the main-hand weapon.
   Likely: five engine consumers expect it.

**Owed in game** (nothing here has been seen running). Do the spike first; the rest waits on it.

1. **Spike:** turn on the MCM page `TAOM — Blow Diagnostics`. In Custom Battle, take a high-Athletics shield hero against recruits and land 20
   shield bashes and 20 kicks. Read the `[BlowDiag]` lines in the newest `Logs/taom_debug_*.log`: `KnockDown` should appear in the
   flags on about the computed share and the victim should fall. `KnockDown` in the flags but nobody falls means the engine ignores
   it; no `KnockDown` at all means the shield is not arriving as `attackerWeapon` (or a gate is wrong).
2. Expected rates at an Athletics lead of 75 or more: a bash about 15 of 20, a kick about 7 of 20; a dwarf victim (Race Abilities
   off, or before Stand Fast glows) about 6 of 20 and 3 of 20.
3. A troll victim: nobody falls (creature immunity).
4. An AI soldier bashing the player: it happens, rarely; note how often over a battle.
5. Toggle off, same setup as 2: nobody falls in 20 bashes.

Read Athletics on the character screen or the encyclopedia unit page.

**Localization.** The MCM name and hint are plain English like the rest of the Combat Mechanics group and this batch's other
features (Fell Foes, Lost Footing); no `/localize` run.

### Cleaving Blows

A swing with a two-handed weapon can cut through one enemy and strike the next. Owner ask, 2026-11-16: on for the agent a human
controls; an AI path for two-handed troops is built and tested but ships off. Our own design, no third-party code. There is no
GitHub issue number: the maintainer waived a new issue for this work.

**What the engine does.** The momentum a hit leaves is what the next hit of the swing is scaled by (`ComputeBlowMagnitudeMelee`
multiplies the base magnitude by the momentum remaining). `Mission.MeleeHitCallback` asks `CalculateRemainingMomentum` twice: first
with `isCrushThrough: true` and a default `Blow` when the hit crushed through, then with the real blow after `RegisterBlow`. Right
after the second ask it calls `DecideWeaponCollisionReaction`. Outside a crush-through (0.3) and a couched or braced hit (0.5),
vanilla carries momentum only for a two-handed axe or mace (`CanHitMultipleTargets`) on a non-thrust hit with the weapon's own
bone: `orig x (1 - absorbed / inflicted) x 0.5`, 0 below 0.25.

**Who counts.** The attacker must be human and on foot (`includeMountedAttackers` ships false), and either

- player controlled (`Agent.IsPlayerControlled`: the main agent, a soldier taken over, a co-op human on his own peer), or
- AI controlled (`IsAIControlled`), only while the AI switch is on and the current weapon usage class is in `aiWeaponClasses`.

Any other attacker (a co-op puppet) gets vanilla. The weapon class is that of the current usage item, so a bastard sword in its
one-handed grip does not count.

**Which weapons.** Carry factor per class (`weaponClassCarry`): TwoHandedAxe 0.60, TwoHandedMace 0.60, TwoHandedSword 0.50,
TwoHandedPolearm 0.40 (swings only). Any other class is vanilla. AI classes (`aiWeaponClasses`): TwoHandedAxe, TwoHandedMace,
TwoHandedSword, so AI spear and glaive lines keep vanilla even with the AI switch on. An AI class needs a carry factor.

**Which hits.** All of these must hold, else the rule answers `null` and the chain falls through to vanilla: a swing (never a
thrust), not an alternative attack (bash, kick), not a passive attack (couched or braced), not a horse charge, a collider agent,
not shield blocked (`AttackBlockedWithShield` or `CollidedWithShieldOnBack`), not `HitWithAnotherBone`, not shrugged off (the
`ShrugOff` blow flag, which also covers troll stagger immunity and a live Stand Fast), damage above 0, `AbsorbedByArmor >= 0` and
finite, `originalMomentum > 0` and finite, and never the crush-through call (vanilla's 0.3 stands). Each gate is a positive
requirement, so a NaN fails it.

**How much.**

```
armourShare = absorbed / (absorbed + inflicted)
carry       = classFactor * (1 - armourShare)
            + (isFatal ? killCarryBonus : 0)
            + skillCarryBonus * SkillGapCurve(attackerSkill - victimSkill)   // 0 on a fatal hit
carry       = min(carry, maxCarryFactor)
next        = originalMomentum * carry
if !(next >= minRemainingMomentum) next = 0
if hit >= maxTargetsPerSwing       next = 0
```

A returned 0 is an answer, not a decline: the rule owns the hit and the swing ends. The skill term reuses the crush-through skill
window (`crushThrough.skillDeadZone`, `skillTargetDelta`, `skillCurveExponent`) through `SkillGapCurve`, extracted from
`CrushThroughService` with no change of result. The attacker's skill is his value for his weapon's `RelevantSkill`; the victim's is
his value for his wielded item (offhand, else primary; 0 for a horse or no character). On a fatal hit the skill term is 0: the
victim's weapon may already be dropped (`Die` then `OnWeaponDrop` empties the slot, `Agent.cs:3458-3462`, `:5529-5532`), so his skill
could read 0 and give every kill the full term; the kill bonus covers kills. No race term. For a player's two-handed axe
or mace the result is never below vanilla on a carried hit (`a/(a+i) <= a/i`, 0.6 > 0.5, the bonuses are not negative, the floor is
vanilla's), and vanilla's best case is 3 targets (1, 0.5, 0.25), so the default cap of 3 never cuts a hit vanilla would land;
`CleavingBlowServiceTests` pins both.

**Reaction.** When the rule qualifies and `momentumRemaining > 0`, `CleavingBlowReaction` forces `SlicedThrough`, so an axe no
longer sticks in a man it carried through. When the rule returned 0 (floor or cap) the vanilla reaction stands: an axe sticks in a man
it does not kill when the hit does under half his health, and slices through otherwise. Shield blocks are excluded and stay Bounced.

**The AI switch.** JSON `cleavingBlows.aiEnabled` false (the compiled default is also false, so a malformed edit cannot switch it
on); MCM `EnableCleavingBlowsForAi`, default false. `CleavingBlowsAiEnabled` folds the master and `CleavingBlowsEnabled`, so AI
needs the feature on. The `aiWeaponClasses` list is the extension point for "two-handed units"; a per-troop or per-culture list is
not built. Tested both ways: off, an AI two-handed axe hit gets `null`; on, the same hit carries; on, an AI one-hander or
polearm still gets `null`; the player's result never depends on the switch.

**The chain counter (max targets per swing).** The key is the attacker `Agent` object (handed to the service as `object`), never
`Agent.Index`: a deleted agent's index is handed to whoever spawns next
(`docs/reviews/lessons/adapters-taleworlds-api.md`). The store is a `ConditionalWeakTable<object, Chain>` with
`Chain { LastCarried, Hits }`, so entries die with the agent object and nothing clears between missions. Per qualifying hit,
under `lock (chain)`: `hit = (LastCarried > 0 && originalMomentum == LastCarried) ? Hits + 1 : 1` (exact float equality on
purpose: the engine hands back the value we returned), then `next` is 0 when `hit >= maxTargets`, and `Hits` and `LastCarried` are
stored. A fresh swing starts at 1.0, which a carried value (at most 0.75) never equals, so the counter resets by itself. The lock
is the default the thread rules ask for; the engine declares the callback single-threaded (see the thread row in
[mission-frame-threads-and-native-costs.md](../reference/engine/mission-frame-threads-and-native-costs.md)), uncontended. The rule
is deterministic: no random roll.

**No double application.** Momentum, both models: `CleaveMomentum` (creatures) `??` `CleavingBlowMomentum` `??` `base`. A listed
creature (troll, mumak, Sauron) is answered by creature cleave and never reaches this rule. Reaction: creature `CollisionReaction`,
then `CleavingBlowReaction`; both only force `SlicedThrough`. The crush-through call is always declined here; the real call for the
same hit sees the 0.3-reduced momentum and the rule applies on top of it, as vanilla does for an axe. With Creature Cleave off and
the AI switch on, a humanoid creature with a listed two-handed weapon falls to this rule: one application.

**Custom Battle.** The same two delegate lines are in `TaomCustomBattleDamageModel` in the same order;
`CustomBattleDamageModelTests` compares the IL call order of both models. The settings are global MCM values.

**Co-op.** No mission runs on the co-op authority process, so the damage model never runs on a dedicated server. Each client runs
its own battle: its own human is `Controller Player` (kind Player), other peers' humans are puppets (kind None, vanilla), so each
human cleaves on the peer that owns him. The three new MCM properties join the settings fingerprint by include-by-default
(`CoopSettingsRelevance`).

**Configuration** (`combat_mechanics_config.json`, block `cleavingBlows`, restart to reload):

| Field | Default | Rule (bad value reverts to the default with a warning) |
|---|---|---|
| `enabled` | true | |
| `aiEnabled` | false | |
| `weaponClassCarry` | axe 0.6, mace 0.6, sword 0.5, polearm 0.4 | key a `WeaponClass` member (case-sensitive, not a number), value finite in (0, 1]; a bad row is dropped; the map replaces the defaults |
| `aiWeaponClasses` | axe, mace, sword | each a `WeaponClass` member with a `weaponClassCarry` row; a bad entry is dropped |
| `killCarryBonus` | 0.15 | finite in [0, 1] |
| `skillCarryBonus` | 0.10 | finite in [0, 1] |
| `maxCarryFactor` | 0.75 | finite in (0, 0.95] |
| `minRemainingMomentum` | 0.25 | finite in [0, 1], and below `maxCarryFactor` (else both revert) |
| `maxTargetsPerSwing` | 3 | int in [3, 5], the slider's range (3 is vanilla's best case for an axe: 1, 0.5, 0.25) |
| `includeMountedAttackers` | false | |

MCM, group "Combat Mechanics": `EnableCleavingBlows` (default on), `EnableCleavingBlowsForAi` (default off), and
`CleavingBlowsMaxTargets` (3 to 5, default 3; a floor of 2 would reach fewer men than vanilla's axe, which lands 3 hits). Their names and hints are `{=KEY}` text, six keys (`taom_cleave_mcm_enable`,
`taom_cleave_mcm_enable_hint`, `taom_cleave_mcm_ai`, `taom_cleave_mcm_ai_hint`, `taom_cleave_mcm_max_targets`,
`taom_cleave_mcm_max_targets_hint`) registered in `taom_module_strings.xml`. The twelve language files do not have them yet, so
`LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey` is red for exactly these six keys until the languages
are seeded.

**Engine facts** (v1.5.7, read from the installed DLLs): `CalculateRemainingMomentum` is reached only from
`MissionCombatMechanicsHelper.UpdateMomentumRemaining`, called twice from `Mission.MeleeHitCallback`; `MeleeHitCallback` is
`[MBCallback(null, false)]` (not multi-thread callable), pinned by `CleavingBlowWiringTests.MeleeHitCallback_IsDeclaredSingleThreaded`;
`DecideWeaponCollisionReaction` runs right after the momentum update with `isFatalHit = victim.Health <= 0f`; the vanilla reaction
is Bounced for a shield block, no damage, another bone or a non-fatal shrugged-off hit, Stuck for an axe that does not kill and
does under half the victim's health, else SlicedThrough.

**UNVERIFIED:**

1. A swing starts at momentum 1.0 and native hands the value we return back unchanged to the next hit of the same swing. The cap is
   exact only if this holds; if native changes it, the count restarts and only the floor bounds the swing.
2. Whether co-op ever computes a remote human's blow on a non-owner peer; if so that peer gives vanilla momentum for it.
3. The feel and the numbers in play (nothing here has been seen running).

**Owed in game** (Custom Battle, local build; Options > Gameplay > Report Damage on, so the "Delivered N damage" lines count the
hits; MCM Combat Mechanics: master on, Cleaving Blows on, AI off, Max Targets 3; the player on foot with a two-handed axe against
tier 1 to 2 infantry, walking into a packed group):

1. Side swings into 2 or 3 packed men: up to 3 "Delivered" lines per swing, each smaller than the one before; the axe passes through
   a man it does not kill instead of sticking.
2. The same with a two-handed sword: it now also hits a second man (vanilla never does).
3. A thrust with a two-handed polearm, and a swing from horseback: one man only (the horseback line of the MCM hint holds only while
   `includeMountedAttackers` is false).
4. A swing a shield blocks: it stops at the shield.
5. Max Targets 3: never more than 3 lines per swing over 20 swings.
6. Cap proof: Max Targets 3 against 4 or more weak men in a tight row, each killed by one blow (the kill bonus keeps the momentum
   above the floor). If 4 lines ever appear, native changed the momentum between hits and the counter restarted: report it (the
   floor still bounds the swing).
7. Cleaving Blows off: the sword stops at the first man; the axe sticks in a man it does not kill (vanilla).
8. AI switch on, the player standing back: AI two-handed troops visibly cut through a packed line; AI spearmen and one-handers do
   not. Optional: the MCM page "TAOM - Blow Diagnostics" on; `[BlowDiag]` lines show bursts of blows with falling `mag=` from one
   swing.
9. Trolls (Creature Cleave on) behave as before.
10. Kill skill premise: MCM page "TAOM - Blow Diagnostics" on, kill a few men with the axe and read the `[BlowDiag] cleave-fatal
    attackerSkill=... victimSkill=...` lines in the newest log. A `victimSkill=0` on a man who carried a weapon confirms that a
    dead man's weapon reads empty (the reason the skill term is 0 on a kill); a normal value means the term could be restored.

A campaign field battle with the same weapon, checks 1 and 7, covers the campaign model.

## Configuration

**The skill window (#343, 2026-11-15).** `crushThrough.skillDeadZone` is 20 and `skillTargetDelta` 135, scaled by 260/390 from 30 and 200 when the troop skill curve's top fell from 390 to 260 ([troop-skill-balance.md](troop-skill-balance.md)); the compiled defaults match, and `ShippedCombatMechanicsConfigTests` pins both. For a player with a weapon skill of about 300, the extra chance is now at its full value against any troop whose weapon skill is 165 or less (Gondor's tier 6 main weapon and every tier below it) and is zero against a troop within 20 points; before, the full value needed a gap of 200. The window between the two limits shrank from 170 skill points to 115, so `skillCurveExponent` grew by 170/115, from 0.008097 to 0.01197 (2026-11-15, the session's call under the maintainer's word to act on its recommendation): the curve has the same shape over the new window, and halfway through it the chance is 0.666 of its full value as before (`ShippedConfig_SkillCurveExponent_KeepsTheCurveShapeOverTheScaledWindow`). The race bonuses are added to the gap (`CrushThroughService.EvaluateSkillCurve`), so they were scaled by 260/390 too: `ctbAttackBonus` 20 to 13 for elves and Sauron, `ctbDefenseBonus` 15 to 10 for dwarves (compiled defaults the same, pinned by `ShippedCombatMechanicsConfigTests`). No other value in the file is added to or compared with the gap: `extraCrushThroughEnergyThreshold` and `energyRampMarginFactor` act on swing energy.

`Main/_Module/ModuleData/combat_mechanics/combat_mechanics_config.json`: per-mechanic enables, all curve constants, creature id lists, unstoppable damage thresholds, shield-pen item/class lists, and the `raceModifiers` table (keyed by race NAME, `raceage/race_age_config.json` precedent). Validated by `CombatMechanicsConfigProvider` (`FiniteFloatValidator` before every range check; ordering invariants; unknown `weaponClasses` entries skipped via `Enum.TryParse<WeaponClass>`; revert-to-default + summary warning). Deserialized with `ObjectCreationHandling.Replace` so JSON lists/dicts replace compiled defaults instead of append-merging. **Reload scope: full application restart** (Singleton provider). A `raceModifiers` row may also set `baseHitPoints` (0 to 1,000; 0 or absent means the engine's 100): `TaomCharacterStatsModel.MaxHitpoints` adds `baseHitPoints` less 100 through `IRaceCombatModifiersResolver.BaseHitPointsBonus`, so it reaches every campaign agent built from a character of that race. Only `cave_troll` and `hill_troll` set it (200, 2026-09-25). Custom Battle does not read the campaign model: its health is the race Monster's `hit_points` in the Armory, which `TrollHitPointsLiveDataTests` (LiveInstall) keeps equal to this value.

MCM: "Combat Mechanics" group (GroupOrder 24), 21 members as of 2026-11-16: the master, 12 per-mechanic toggles (skill crush-through, monster crush-through, orc shield crush-through, creature cleave, creature stagger immunity, weight-based charge knockdown, shield penetration, race combat modifiers, culture charge damage, signature strikes, Hammerhand, Cleaving Blows), the Cleaving Blows AI toggle and 7 sliders (`CrushThroughMaxChance`, `ChargeAutoKnockdownRatio` (renamed from `ChargeAutoKnockdownWeightRatio` in #1017, so the default of 6 reaches saved settings), `ChargeNeutralWeightRatio`, `ChargeHorsePenetration`, `ChargeMinPenetrationFactor`, `SignatureStrikeCooldownMultiplier`, `CleavingBlowsMaxTargets`); the three new members are `EnableCleavingBlows`, `EnableCleavingBlowsForAi` and `CleavingBlowsMaxTargets`. MCM merges over JSON per read (`CombatMechanicsSettingsProvider`, `SettingClamp`).

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` | The 13 overrides, each base plus one call per feature; under ADR-002's 150 lines (`CombatMechanicsModelInvariantsTests` pins the exact set, the ceiling and which agent reaches which seam) |
| `Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs` | Custom Battle's twin (#788) on `CustomAgentApplyDamageModel`: the same 13 overrides, no career passives and no Refuge; `CustomBattleDamageModelTests` pins the base, the override set, the call order against the campaign model and the registration |
| `Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs` | The campaign model's and the twin's boundary to the six services, one method per seam, with the context builders and primitive extractors (#737) |
| `Main/Features/CombatMechanics/ChargeDamageService.cs` | Per-culture charge multiplier table (#610): case-insensitive, built once, 1.0 for null/unlisted/off |
| `Main/Features/CombatMechanics/Hooks/MountChargeDamageApplier.cs` | The mount-side post-pass shared by both `AgentStatCalculateModel` slots; the rider hop (`RiderAgent`) with the engine evidence |
| `Main/Features/CombatMechanics/CrushThroughService.cs` | Skill CTB curve + monster auto-CTB + orc shield-CTB |
| `Main/Features/CombatMechanics/ChargeKnockdownService.cs` | Weight-driven two-branch charge knockdown |
| `Main/Features/CombatMechanics/ShieldBashKnockdownService.cs` | Hammerhand (#895): the shield bash and kick knockdown chance; `true` or `null`, never `false` |
| `Main/Features/CombatMechanics/CleavingBlowService.cs` | Cleaving Blows: the carry formula, the gates and the per-swing chain counter; `float?` momentum (null declines, 0 ends the swing) and the slice-through verdict |
| `Main/Features/CombatMechanics/SkillGapCurve.cs` | The skill-gap curve shared by crush-through and Cleaving Blows |
| `Main/Features/CombatMechanics/CreatureCombatService.cs` | Cleave (momentum + reaction), unstoppable, stagger multiplier |
| `Main/Features/CombatMechanics/ShieldPenetrationService.cs` | Penetration flag grants + runtime-flag shield-damage correction |
| `Main/Features/CombatMechanics/RaceCombatModifiersResolver.cs` | Race-name-keyed modifier table → per-raceId cache |
| `Main/Features/CombatMechanics/CombatMechanicsConfigProvider.cs` | JSON load + full semantic validation |
| `Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs` | MCM-over-JSON merge, master-toggle folding |
| `Main/Features/CombatMechanics/Domain/*.cs` | `CrushThroughContext`, `ChargeKnockdownContext`, `ShieldBashContext`, `CleavingBlowContext` (with `CleaveAttackerKind`), `RaceCombatModifiers` |
| `Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs` | Parent (abstract since 2026-07-02) |
| `Main/SubModule.cs` (`RegisterSpecialResourcesAndCareers`, `RegisterCustomBattleModels`) | One registration per game type: `campaignStarter.AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(...))` and `basicStarter.AddModel<AgentApplyDamageModel>(new TaomCustomBattleDamageModel(...))`, each handed `CombatMechanicsHooks` from the container; the charge service is passed to `TaomAgentStatCalculateModel` and to the Custom Battle stat model |
| `TAOM.Tests/Features/CombatMechanics/*` | Service/provider/resolver tests + `CombatMechanicsModelInvariantsTests` (derivation + abstract parent + exact override set + line ceiling + agent-per-seam pins) + `CombatMechanicsHooksTests` + `CombatMechanicsContainerWiringTests` + `CustomBattleDamageModelTests` (the Custom Battle twin) + `ShieldBashKnockdownServiceTests` and `ShieldBashWiringTests` (Hammerhand: the decision matrix, and the source pins for the cheap gate and the chain order in both models) + `SkillGapCurveTests`, `CleavingBlowServiceTests` and `CleavingBlowWiringTests` (Cleaving Blows: the curve, the decision matrix with the chain counter, and the model order, thread declaration and counter source pins) |

## Dependencies

`IRaceManager` (race validation), `ICareerAgentStatService` (inherited career passives; since #611 also the mount-side career bonuses on the stat model, beside the culture multiplier; since #769 E10 also the troop crush hold, `CareerTroopHooks.ApplyCrushHold`, campaign model only, and the knockdown counter on the stat model), `IPathService`/`IModLogger`, `FiniteFloatValidator`/`SettingClamp`, `Monster.Weight` + `RelativeSpeedLimitForCharge` from monsters.xml (Native + LOTRLOME; the warg Monster moved into LOTRLOME on 2026-08-28).

## Tests

`TAOM.Tests/Features/CombatMechanics/`: full decision-matrix coverage per service (the troll health row: `RaceCombatModifiersResolverTests`, `CombatMechanicsConfigProviderTests`, `ShippedCombatMechanicsConfigTests`, `TrollHitPointsLiveDataTests`) (boundaries: dead zone 20/21, energy 25 gate, damage == threshold, roll == chance), config validation (one test per rule, NaN/∞/ordering/sign/unknown-string), validate-before-lookup regressions, and the model invariants pins; `CustomBattleDamageModelTests` (Custom Battle's twin: base, override set equal to the campaign model's, IL call order against it, the registration and the no-module-declaration rule). Engine-signature drift is covered by `GameModelOverrideBindingTests` + `tools/snapshot_api_surface.ps1 -Check`.

## How-To

- **Add a creature to cleave/unstoppable/monster-CTB**: add its `Monster.StringId` to the relevant list/dict in the JSON. Settlement variants (`X_settlement*`) are normalized automatically.
- **Add race flavor** (e.g. tree-spirits dig in): add a `raceModifiers` row — data, not code. Unknown race names are skipped with a warning.
- **Make a weapon pierce shields**: set `shieldPenetration.enabled` true, flip the "Shield Penetration" MCM toggle on, and add the item id to `shieldPenetration.itemIds` (preferred) or its class to `weaponClasses`. All three ship off/empty. Prefer item ids: a class grant hits every weapon of that class in every culture, player and AI alike. Leave `runtimeShieldDamageCorrectionEnabled` off unless you have measured a native underestimation for that specific class in `ComputeBlowDamageOnShield`. It does not exist for `Javelin` or `ThrowingAxe`, which the engine multiplies by class.
- **Tune charge knockdown**: four MCM sliders, live: `Charge Neutral Weight Ratio` (where the weight term equals vanilla), `Charge Penetration` (the base; 0.5 and up floors an ordinary man on any head-on hit that does damage), `Charge Min Penetration Factor` (1.0 = never below vanilla for heavy victims; 0.25 = the old feel), `Auto-Knockdown Weight Ratio` (6 = horse + man vs man from any angle; 8 = wargs and chariots only). Per-race resistance stays in `raceModifiers` (JSON, restart).
- **Tune Hammerhand**: the `shieldBash` block in the JSON (restart): the chance band, `kickChanceFactor` and `playerVictimChanceFactor`. The `Hammerhand (Shield Bash Knockdown)` toggle turns it off live. Per-race resistance is the same `raceModifiers.knockdownResistanceMultiplier` the charge rules use.
- **Tune Cleaving Blows**: the `cleavingBlows` block in the JSON (restart): the class factors, the bonuses, `maxCarryFactor`, the floor, and the AI class list. The `Cleaving Blows` toggle and the `Max Targets` slider apply live; the AI toggle ships off.
- **Tune charge damage by culture**: `chargeDamage.cultureMultipliers` in the JSON (restart); the `Culture Charge Damage` toggle turns the table off live.

## Performance

All overrides are per-hit. Services precompute lookups at construction (monster-id settlement variants are expanded into the lookup sets at construction; the per-call path is a bare `HashSet`/`Dictionary` probe), take `in` structs, and allocate nothing per call; the race resolver caches per race id; weapon-class names come from a static enum-name cache. Engine-sourced floats (momentum, charge velocity, knockdown resistance, and the charge victim's maximum health, #728; the inflicted damage is an engine `int`) are NaN-guarded with positive-polarity gates, so NaN always fails the gate or defers to vanilla.

## Known limitations / follow-ups

- `baseHitPoints` rides the Race Combat Modifiers toggle: turning it off in MCM drops campaign trolls to 100 health while Custom Battle keeps the Monster's 200, and the setting's hint does not say so (new player-facing text, so a translator run). The +100 is added without a description, so a hero tooltip would show base 100 against a total of 200; no hero has a troll race today. `EngineBaseHitPoints` (100) is copied from `DefaultCharacterStatsModel`'s `new ExplainedNumber(100f)` (v1.5.3): re-check it on an engine bump.
- The native shield-damage underestimation (TW forums 470085/470117) was re-checked against 1.4.8 on 2026-08-17 and does not apply to any class the config could reach: `ComputeBlowDamageOnShield` multiplies `Javelin` and `ThrowingAxe` by class and never reads the penetration flags. `runtimeShieldDamageCorrectionEnabled` now ships false. Owed: the in-game A/B (#320 item 4) confirming shields stop javelins again, and that an `Impale` perk holder still pierces.
- Monster-id lists (`monsterCrushMonsterIds`, `cleaveMonsterIds`, `unstoppableDamageThresholds` keys) and `orcShieldCrushRaces` are syntax-cleaned but not resolvability-validated — a typoed id is inert (never matches) and logs no warning, unlike `raceModifiers` keys which get lazy unknown-name warnings. Deliberate (Codex P3, 2026-07-02): the monster registry is engine state, and adding an adapter for a typo diagnostic fails the simplicity criterion. Double-check ids against the Monster XMLs when editing.
- Cleave chains through shield blocks only when the block takes damage — a zero-shield-damage block (`InflictedDamage == 0`) keeps vanilla's Bounced termination (Codex P3; MCM hint wording matches).
- Cleaving Blows: the target count restarts after a hit the rule declines in mid swing (a shrugged-off hit, a crush-through), so only the floor bounds such a swing; the cap is exact only if native hands the momentum back unchanged (UNVERIFIED, in-game check 6); mounted attackers are off (`includeMountedAttackers`); the AI path ships off.
- Per-race `knockdownResistanceMultiplier` feeds the owned charge branch and Hammerhand's bash and kick chance; extending it to vanilla weapon knockdowns belongs in `TaomAgentStatCalculateModel.GetKnockDownResistance`.
- Troll Monster weight (160) equals uruk, so weight alone cannot tell them apart; since #610 the `cave_troll` / `hill_troll` race rows carry `knockdownResistanceMultiplier` 4.0 and the min penetration factor 1.0 gives every heavy victim vanilla's rule, so a horse floors an uruk like a man and never a troll. Raising troll weights in LOTRLOME `monsters.xml` is no longer needed for this.
- TAOM's synthetic creature blows (spider/elephant BT via `RegisterBlow`) ask no damage-model method in either mode, so none of this feature's overrides touch them, Custom Battle's twin included. Spot-check in control battles that the unstoppable thresholds don't neuter the creatures' feel when ordinary hits land on them.
- Custom Battle (#788): the in-game feel of the crush-through, cleave, stagger and charge knockdown rules there, and the re-baselined Custom Battle A/B and Signature Strikes smoke, are owed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/culture-doctrine.md](./culture-doctrine.md)
- [docs/features/dread-aura.md](./dread-aura.md)
- [docs/features/hero-abilities.md](./hero-abilities.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/reviews/rca-hammerhand-2026-10-20.md](../reviews/rca-hammerhand-2026-10-20.md)

<!-- backlinks-end -->
