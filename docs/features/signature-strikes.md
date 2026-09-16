# Signature Strikes

## Overview

A configured hero's melee hits carry effects the engine never gives an ordinary swing, keyed on
the swing direction the engine already animated. An overhead is a **Slam**: the struck agent
always goes down, enemies in a ring around the impact take part of the hit's damage and fall too,
and a burst of fear drains their morale; it also fires on an overhead into the ground, so a player
can deliberately slam. A side swing is a **Sweep**: the struck agent and the enemies in front are
staggered back. A thrust is a plain hit. Shipped for Sauron (`lord_1_17`, race `sauron`) only;
the Witch-king or a troll is a config row.

## Why This Exists

Mike wanted Sauron to have specific attacks the way the warg, spider and mumak trees give
creatures theirs: "when he hits with his mace it does X, Y, Z", later widened to any weapon he
swings except a throw or a shot (2026-09-16). The investigation that preceded it settled what the
engine can and cannot do:

- **Vanilla behavior:** a boulder kills several people because native calls
  `Mission.MissileAreaDamageCallback` for missiles flagged `AffectsArea` / `AffectsAreaBig`
  (v1.5.3 `Mission.cs:5688-5761`): a 1.2 m ring (2.8 m for the fire pot), full damage inside
  1.0 m, quadratic falloff `1 / lerp(1,3,t)^2` to one ninth at the edge, every ring victim
  inheriting the primary blow's flags (a boulder carries `KnockDown`, set unconditionally from its
  `CanKnockDown` weapon flag in `CreateMissileBlow`, `:5522`). A ballista bolt passes through up to
  three agents (`MultiplePenetration`, `:5913-5959`). Both are missile-only. A melee swing
  **never knocks back** (`SandboxAgentApplyDamageModel.CanWeaponKnockback:917-943` returns false
  for swings) and knocks down only inside the sweet spot (`AttackProgress` 0.22 to 0.55) when
  `damage >= maxHP * max(0, (0.4 + 0.001 * Athletics) - penetration)`
  (`MissionCombatMechanicsHelper.cs:76-96,333-348`). Rams and siege towers damage nobody by
  contact.
- **TAOM requirement:** the Dark Lord on foot should be a battlefield event, for AI and player
  alike, without an authored animation and without fighting the engine's human combat AI.
- **Without this feature:** Sauron already cleaves and crush-throughs (`CombatMechanicsConfig`
  lists `sauron` in the monster crush and cleave sets) and his mace head carries `CanKnockDown`,
  so he is strong; but nothing happens to the soldiers beside the one he hits, a side swing never
  staggers, and a slam that misses the sweet spot is an ordinary hit.

## Architecture

### Design Challenge

Every TAOM creature tree is attached to a **mount** agent that has no combat AI of its own; the
tree is the only thing choosing attacks. Sauron is a human agent whose swings are chosen by the
engine's human combat AI (or the player's mouse). A tree playing attack clips on him would fight
that AI, and his skeleton (`as_human_warrior`, a 1.40-scale elf clone) has no signature clip.
The engine's area damage is native-triggered for missiles and unreachable from a melee swing.

### Solution Approach

Map effects to the **swing direction** the engine already animates, triggered by the engine's own
melee hit, with the ring delivered through the existing synthetic-blow primitive. No Harmony patch:
every seam is an engine virtual.

| Seam | What | Why |
|---|---|---|
| `MissionBehavior.OnMeleeHit(attacker, victim, isCanceled, collisionData)` | fires after every melee collision (`Mission.cs:5445-5447`), world hits included (`victim == null`, `CollisionResult == HitWorld`) | the trigger; carries `AttackDirection`, `CollisionResult`, `InflictedDamage`, `CollisionGlobalPosition` |
| `AgentApplyDamageModel.DecideAgentKnockedDownByBlow` / `DecideAgentKnockedBackByBlow` on the already-registered `TaomCombatMechanicsModel` | the primary-victim verdicts, asked from inside `CreateMeleeBlow` (`:5634-5641`) for an unmounted human | guaranteed knockdown on a slam, knock-back on a sweep, everything else `base` |
| `CustomAttacksUtils.TakeDamage(victim, attacker, damage, magnitude, knockDown, extraFlags)` | the one synthetic-blow primitive every creature tree uses, extended with a trailing `BlowFlags extraFlags` so a sweep can set `KnockBack` | ring victims |
| `Mission.GetNearbyEnemyAgents(Vec2, float, Team, MBList<Agent>)` | enemies only, filtered native-side | the ring; allies are never flattened |
| DreadAura's policy-free pieces: `DreadAgentGate.CanAffect`, `IDreadRegistry.ResolveResist`, the CALL to `BattleMoraleModel.CalculateMoraleChangeToCharacter` | the fear burst | tier, hero and race resistance at parity with the aura; NOT `DreadAuraService.ComputeDrain`, which is gated on the Dread Aura toggle and clamped to Dread's own morale floor |

**Execution is deferred one frame.** `OnMeleeHit` runs inside the engine's `MeleeHitCallback`
while the swing's momentum is a live `ref` (`:5347`), and registering more blows there re-enters
the hit pipeline. The hit only enqueues a value-type `StrikeRequest` (attacker, primary victim,
impact position, resolved effect); `OnMissionTick` drains a two-list swap buffer and the runner
rings the enemies. The primary victim is excluded from the ring (the model handled it), as are the
attacker, mounts (the rider is hit as a human and maps to `CanDismount`; hitting the horse too
double-taxes cavalry), invulnerable and fading agents.

**One package per swing.** A cleave fires `OnMeleeHit` once per body in the same swing. The
cooldown is stamped at enqueue, per attacker and per kind, so the second and third bodies read as
inside the cooldown and produce no second ring. The primary-victim verdicts consult the SAME
stamps without writing them, so the guaranteed knockdown and the ring are one event: a slam is at
most one per `slamCooldownSeconds`, and every other overhead is a vanilla hit.

**Identity** is the DreadAura two-axis pattern: hero StringId (survives a data change that drops
the race attribute) OR FaceGen race (finds the hero in a Custom Battle, where the agent carries no
`HeroObject`). Race names resolve to ids once behind `IRaceManager.IsValidRaceName`; the hot path
never calls `GetRaceNameFromId`. The roster is keyed by `Agent` object reference, filled in
`OnAgentBuild` plus a one-shot scan of `Mission.AllAgents` on the first tick (the safety net the
DreadAura and warg trackers carry, for an agent built before the behavior could see it), evicted in
`OnAgentDeleted` (the callback the engine recycles the index from), cleared at mission start and
end (it is a process singleton because the model probes it). Never `Agent.Index` (#592).

**Weapon gate.** The attacker must hold a weapon whose ITEM type is not Bow, Crossbow, Sling,
Thrown, Pistol, Musket or an ammo type. A javelin swung in melee mode has a melee usage but a
Thrown item, so it is out; so are kicks and shield bashes (`IsAlternativeAttack`) and bare hands.
A missile hit never reaches `OnMeleeHit` at all.

**What does nothing:** a parry, a chamber block, a weapon block, a canceled hit (invulnerable
victim), a horse charge, a thrust, a zero-damage hit, any hit inside the cooldown, any mission
that is not `MissionCombatType.Combat` (arenas and tournaments out), any multiplayer session, the
Combat Mechanics master toggle off, and everything for the rest of a mission in which the logic
stood down on an exception (the stand-down clears the roster, so the model's verdicts stop with
the ring; Codex review 114 F1). A shield block that still damages the shield fires the ring
at `shieldBlockedMultiplier` of the shield damage and never knocks the blocker down; a
zero-shield-damage block does nothing (the cleave rule's parity).

### Component Diagram

```
signature_strikes_config.json
        |
SignatureStrikesConfigProvider (validates every field, drops unknown rows)
        |                      \
SignatureStrikeRegistry         SignatureStrikesSettingsProvider (MCM toggle + cooldown multiplier)
 (hero id | race -> bool)              |
        |                      SignatureStrikeService (pure: Evaluate, DecideKnockdown,
SignatureAgentRoster                    DecideKnockback, ComputeRingDamage, ComputeFearDrain)
 (Agent -> cooldown stamps)            /                      \
        |                             /                        \
SignatureStrikesMissionLogic --- StrikeContextFactory --- TaomCombatMechanicsModel
 OnAgentBuild -> roster           (one boundary,          DecideAgentKnockedDownByBlow
 OnMeleeHit   -> Evaluate,         both paths)            DecideAgentKnockedBackByBlow
                 stamp, enqueue                            (primary victim, ?? base)
 OnMissionTick -> StrikeRequestBuffer.Swap -> SignatureStrikeRunner
                                               GetNearbyEnemyAgents, SignatureStrikeFalloff,
                                               CustomAttacksUtils.TakeDamage(extraFlags),
                                               BattleMoraleModel + ChangeMorale
```

## Configuration

### Config File: `Main/_Module/ModuleData/signature_strikes/signature_strikes_config.json`

A full Bannerlord restart is needed for changes here (the provider is a `Reuse.Singleton` Lazy).
The MCM cooldown multiplier applies live.

| Field | Type | Description |
|-------|------|-------------|
| `enabled` | bool | Feature switch; MCM overrides it |
| `heroIds` | string[] | Hero StringIds with signature strikes. Empty is a legitimate "nobody on this axis"; `null` reverts |
| `races` | string[] | FaceGen race names. Unknown names are skipped with a log warning at first use |
| `slamCooldownSeconds` | float 0.5..120 | Per attacker, mission time. The floor is what keeps a cleaving swing to one package |
| `sweepCooldownSeconds` | float 0.5..120 | Per attacker, mission time |
| `shieldBlockedMultiplier` | float 0..1 | Damage kept on a shield-blocked trigger and on a shield-blocking ring victim |
| `strikes.<Direction>` | object | One profile per `Overhead`, `Left`, `Right`, `Thrust` (member name only, case-insensitive, normalised; a numeric string is dropped). A direction with no row is a plain hit; an unknown key or `kind` drops the row with a warning. A profile with no `damageFraction` and no `fearMorale` rings nothing and logs nothing |
| `strikes.*.kind` | `Slam` or `Sweep` | The cooldown is per kind |
| `strikes.*.outerRadius` / `innerRadius` | float 0.1..15 / 0..outer | Metres; full effect inside inner, one ninth at outer (the boulder curve) |
| `strikes.*.damageFraction` | float 0..1 | Share of the hit's damage a ring victim takes before falloff |
| `strikes.*.worldHitBaseDamage` | int 0..500 | Damage basis on a ground hit; 0 = a ground hit does nothing for this direction |
| `strikes.*.magnitude` | float 0..500 | Blow impulse on ring victims |
| `strikes.*.knockDown` | bool | Ring victims fall (riders dismount); the struck agent always falls |
| `strikes.*.knockBack` | bool | Ring victims and the struck agent are staggered back |
| `strikes.*.fearMorale` | float 0..100 | One-shot morale drain on ring victims before tier and race resistance; 0 = none |

Every float is NaN/Infinity-rejected before its range check and reverts to that direction's
compiled default with a warning; one summary warning says the file had invalid values.

### Current Values

| Direction | Kind | Outer / inner | Damage fraction | Ground hit | Magnitude | Knock down | Knock back | Fear |
|---|---|---|---|---|---|---|---|---|
| Overhead | Slam | 4.0 m / 1.5 m | 0.6 | 60 | 80 | yes | no | 15 |
| Left, Right | Sweep | 3.0 m / 1.5 m | 0.25 | 0 (none) | 60 | no | yes | 0 |

Cooldowns: Slam 20 s, Sweep 12 s. Long on purpose: Mike's standing instruction is that Sauron
must not be overpowered. These are first guesses ahead of the in-game smoke; the engine boulder
ring is 1.0 m / 1.2 m for reference. MCM (group Combat Mechanics): `Signature Strikes` toggle
and `Signature Strike Cooldown Multiplier` (0.5x to 5x, live).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SignatureStrikes/SignatureStrikeService.cs` | Pure decisions: `Evaluate`, `DecideKnockdown`, `DecideKnockback`, `ComputeRingDamage`, `ComputeFearDrain` |
| `Main/Features/SignatureStrikes/SignatureStrikeFalloff.cs` | The engine's `1 / lerp(1,3,t)^2` band as a pure static |
| `Main/Features/SignatureStrikes/SignatureStrikeRegistry.cs` | Two-axis identity, id-keyed race table built once |
| `Main/Features/SignatureStrikes/SignatureStrikesConfigProvider.cs` | Validating loader (DreadAura shape) |
| `Main/Features/SignatureStrikes/SignatureStrikesSettingsProvider.cs` | Folds the Combat Mechanics master toggle; clamps the multiplier |
| `Main/Features/SignatureStrikes/Domain/` | `StrikeContext` / `StrikeEffect` record structs, the three enums, the config POCO, `StrikeNames` |
| `Main/Features/SignatureStrikes/Hooks/SignatureStrikesMissionLogic.cs` | Entry point (`: MissionLogic`): roster lifecycle, enqueue on hit, drain on tick, stand-down on exception |
| `Main/Features/SignatureStrikes/Hooks/StrikeContextFactory.cs` | The one boundary from `AttackCollisionData` to `StrikeContext`, shared with the model |
| `Main/Features/SignatureStrikes/Hooks/SignatureStrikeRunner.cs` | Rings the enemies: query, falloff, blow, fear |
| `Main/Features/SignatureStrikes/Hooks/SignatureAgentRoster.cs` | Reference-keyed roster with cooldown stamps |
| `Main/Features/SignatureStrikes/Hooks/StrikeRequestBuffer.cs` | Two-list swap buffer for the one-frame deferral |
| `Main/Features/SignatureStrikes/Hooks/SignatureMissionGate.cs` | `Combat` missions only, no multiplayer, no campaign requirement |
| `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` | Two thin delegations (optional ctor params, the `IRefugeDefenseService` precedent) |
| `Main/Features/AdvancedCombat/CustomAttacksUtils.cs` | `TakeDamage` gains `extraFlags`; `ComposeBlowFlags` extracted and pinned |
| `Main/_Module/ModuleData/signature_strikes/signature_strikes_config.json` | Shipped config |

Registration: `Main/IoC.cs` (`SignatureStrikesIoC.RegisterSignatureStrikesFeature`),
`Main/SubModule.cs` (the logic after `DreadAuraMissionLogic`; the two services appended to the
`TaomCombatMechanicsModel` constructor call, which is campaign-only, so a Custom Battle gets the
ring but vanilla primary knockdown).

## Dependencies

- `IDreadRegistry` (DreadAura): race resist multiplier for the fear burst
- `DreadAgentGate` (DreadAura): which agents have morale to drain
- `CustomAttacksUtils`, `AgentSlotIdentity`, `MissionThreadGuard` (AdvancedCombat)
- `IRaceManager` (Core): race name validation at first use
- `IPathService`, `IModLogger` (Core)

## Tests

- `TAOM.Tests/Features/SignatureStrikes/SignatureStrikeServiceTests.cs`: direction mapping, every rejection, shield-block and world-hit basis, cooldown boundary and NaN cases, the verdict matrix, ring damage rounding and overflow, fear headroom and sentinel
- `SignatureStrikeFalloffTests.cs`: inside, edge (1/9), midpoint, beyond, degenerate radii, NaN/Infinity per argument
- `SignatureStrikeRegistryTests.cs`: both axes, unknown race skipped, no coercion, built once
- `SignatureStrikesConfigProviderTests.cs`: one test per validation rule
- `ShippedSignatureStrikesConfigTests.cs`: the shipped file parses clean, keeps `lord_1_17` / `sauron` only, the direction and cooldown contract, no long dashes
- `StrikeRequestBufferTests.cs`: swap semantics
- `StrikeContextFactoryTests.cs`: the two enum mirrors and the item-type gate
- `SignatureAgentRosterTests.cs`, `SignatureMissionGateTests.cs`: the null and lifecycle paths (an `Agent` cannot be built in a test)
- `SignatureStrikesBindingTests.cs` (`BindingVerification`): `OnMeleeHit`, `GetNearbyEnemyAgents`, `DecideAgentKnockedBackByBlow`, `BlowFlags.KnockBack == 0x10`, the two enum mirrors, `: MissionLogic`, the `SubModule.cs` call site
- `TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsBlowFlagsTests.cs`: the flag composer
- `CombatMechanicsModelInvariantsTests` (override set) and `SettingsFingerprintTests` (231 / 182) moved with the feature

## How to give another hero signature strikes

1. Add the hero's StringId to `heroIds` and/or the race name to `races` in the JSON.
2. `ShippedSignatureStrikesConfigTests.ShippedConfig_ShipsNoOtherSignatureHero` fails on purpose: update it with the balance decision and a control battle behind it.
3. No code changes.

To change what a direction does, edit its `strikes` row; to add a Thrust effect, add a `Thrust`
row. To add a NEW kind of effect (a different ring shape, a buff), extend `StrikeEffect` and the
runner; the service and the config validation are where the new field's gates go.

## Smoke (owed)

- Route A (everything): new campaign, PlayerSwitcher to Sauron (MCM "Allow Sauron and the Nazgul"
  on), attack a Gondor party. Overhead into a shield wall: struck agent drops, neighbours within
  4 m fall, the log line reads `feared > 0`. Overhead into the ground near a clump: the ring fires
  on the world-hit basis. Side swing: struck agent staggers, neighbours in front stagger. A second
  slam inside 20 s: no ring. Parries and blocks: no `[SignatureStrikes]` line.
- Route B (ring only): Custom Battle, Mordor, commander Sauron. Registers via the race axis; the
  model is campaign-only, so expect ring falls with vanilla primary knockdown.
- Negative: a tournament as Sauron shows no effect lines; `MissionThreadGuard` reports zero
  off-thread calls across a battle.
- Unverified until smoked (Codex review 114 left all three honestly open): whether a terrain hit
  carries a non-null `realHitEntity` (if not, the engine cancels the callback and the ground slam
  never fires); the thread of the native `MeleeHitCallback` (declared non-multithread-callable,
  `MissionThreadGuard` is the tripwire); and native honouring `BlowFlags.KnockBack` on a synthetic blow.
  `Agent.HandleBlowAux` hands the whole `Blow` to native and the engine's own melee path sets the
  same bit, but TAOM has only ever set `KnockDown` this way. If no stagger shows, the fallback is
  a low-magnitude `KnockDown` for the ring while the model-side knock-back on the struck agent
  stays.

## Changelog

- 2026-09-16: Codex review 114 fixes (stand-down clears the roster, name-only enum parsing, impact finiteness gate, shared damage cast, 0.5 s cooldown floor, inert profiles skip).
- 2026-09-16: feature landed (#605).

## GitHub Issue

- **Issue:** #605 SignatureStrikes: direction-mapped melee effects for Sauron
- **Status:** Open (smoke owed)
