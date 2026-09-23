# Signature Strikes

## Overview

A configured hero's melee hits carry effects the engine never gives an ordinary swing, keyed on
the swing direction the engine already animated. Two signatures ship:

- **Sauron** (`lord_1_17`, race `sauron`, #605). An overhead is a **Slam**: the struck agent
  always goes down, enemies in a ring around the impact take part of the hit's damage and fall
  too, and a burst of fear drains their morale; it also fires on an overhead into the ground, so a
  player can deliberately slam. A side swing is a **Sweep**: the struck agent and the enemies in
  front are staggered back.
- **The Nine** (the `nazgul_nine` hero set and race `nazghul`, #645). An overhead or a side swing
  is a **Scream**: a ring around the wraith itself, where every enemy takes part of the hit's
  damage, is staggered back and loses morale, and a shriek plays. The struck foe is staggered and
  frightened too. One 15 s timer covers all three directions.

A thrust is a plain hit for both. A troll or another hero is a config entry.

## Why This Exists

Mike wanted Sauron to have specific attacks the way the warg, spider and mumak trees give
creatures theirs: "when he hits with his mace it does X, Y, Z", later widened to any weapon he
swings except a throw or a shot (2026-09-16). Then the Nine (2026-09-23): "a special ability called
SCREAM, which not only does damage but reduces the enemy's morale by X amount. This can be on an
overhead attack and slashing attack. Same as Sauron." His answers: scream plus stagger, no
knockdown, morale 25, every 15 s, a sound generated with ElevenLabs (later replaced by a clip he
supplied; see Sound provenance). The investigation that preceded #605 settled what the engine can
and cannot do:

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
- **TAOM requirement:** the Dark Lord and his wraiths should be battlefield events, for AI and
  player alike, without an authored animation and without fighting the engine's human combat AI.
- **Without this feature:** Sauron already cleaves and crush-throughs (`CombatMechanicsConfig`
  lists `sauron` in the monster crush and cleave sets) and his mace head carries `CanKnockDown`,
  so he is strong; but nothing happens to the soldiers beside the one he hits, a side swing never
  staggers, and a slam that misses the sweet spot is an ordinary hit. The Nine had only DreadAura's
  slow morale pulse.

## Architecture

### Design Challenge

Every TAOM creature tree is attached to a **mount** agent that has no combat AI of its own; the
tree is the only thing choosing attacks. Sauron and the Nine are human agents whose swings are
chosen by the engine's human combat AI (or the player's mouse). A tree playing attack clips on them
would fight that AI, and their skeletons (`as_human_warrior` clones: Sauron 1.40 scale, the nazghul
race 1.18) have no signature clip. The engine's area damage is native-triggered for missiles and
unreachable from a melee swing.

### Solution Approach

Map effects to the **swing direction** the engine already animates, triggered by the engine's own
melee hit, with the ring delivered through the existing synthetic-blow primitive. No Harmony patch:
every seam is an engine virtual.

| Seam | What | Why |
|---|---|---|
| `MissionBehavior.OnMeleeHit(attacker, victim, isCanceled, collisionData)` | fires after every melee collision (`Mission.cs:5445-5447`), world hits included (`victim == null`, `CollisionResult == HitWorld`) | the trigger; carries `AttackDirection`, `CollisionResult`, `InflictedDamage`, `CollisionGlobalPosition` |
| `AgentApplyDamageModel.DecideAgentKnockedDownByBlow` / `DecideAgentKnockedBackByBlow` on the already-registered `TaomCombatMechanicsModel` | the primary-victim verdicts, asked from inside `CreateMeleeBlow` (`:5634-5641`) for an unmounted human | guaranteed knockdown on a slam, knock-back on a sweep or a scream, everything else `base` |
| `CustomAttacksUtils.TakeDamage(victim, attacker, damage, magnitude, knockDown, extraFlags)` | the one synthetic-blow primitive every creature tree uses, extended with a trailing `BlowFlags extraFlags` so a sweep or a scream can set `KnockBack` | ring victims |
| `Mission.GetNearbyEnemyAgents(Vec2, float, Team, MBList<Agent>)` | enemies only, filtered native-side | the ring; allies are never flattened |
| DreadAura's policy-free pieces: `DreadAgentGate.CanAffect`, `IDreadRegistry.ResolveResist`, the CALL to `BattleMoraleModel.CalculateMoraleChangeToCharacter` | the fear burst | tier, hero and race resistance at parity with the aura; NOT `DreadAuraService.ComputeDrain`, which is gated on the Dread Aura toggle and clamped to Dread's own morale floor |
| `SoundEvent.GetEventIdFromString` + `Mission.MakeSound(id, position, false, true, -1, -1)` | a strike's sound, once, at the attacker's head (`Agent.GetEyeGlobalPosition`) | the one-shot call Native's `module_sounds.xml` documents. An unregistered name is expected to answer -1, the engine's null sound id (a native contract managed code cannot prove; the smoke checks it), and the attacker then gives the engine's `Yell` voice instead (`Agent.MakeVoice`). A non-finite eye position plays nothing, the defence `CustomAttacksUtils.IsBlowGeometrySafe` applies to `MakeSound`; what native does with one is unproven, since the spider AV that guard was written for traced to `HandleBlowAux` instead |

**Execution is deferred one frame.** `OnMeleeHit` runs inside the engine's `MeleeHitCallback`
while the swing's momentum is a live `ref` (`:5347`), and registering more blows there re-enters
the hit pipeline. The hit only enqueues a value-type `StrikeRequest` (attacker, primary victim,
ring centre, resolved effect); `OnMissionTick` drains a two-list swap buffer and the runner plays
the sound and rings the enemies. The ring's centre is chosen at the hit: the impact point for an
`Impact` strike (the slam), the attacker's position for a `Self` strike (the scream). The primary
victim takes the ring's fear but never its blow (it already took the real one, and the model
handled its knockdown or knock-back), as the attacker, mounts (the rider is hit as a human and maps
to `CanDismount`; hitting the horse too double-taxes cavalry), invulnerable and fading agents take
nothing. Before #645 the primary victim was skipped outright, so a slam's fear missed the agent
Sauron actually hit.

**One package per swing.** A cleave fires `OnMeleeHit` once per body in the same swing. The
cooldown is stamped at enqueue, per attacker and per kind (`StrikeKindTimes`, a value the context
copies), so the second and third bodies read as inside the cooldown and produce no second ring. The
primary-victim verdicts consult the SAME stamps without writing them, so the guaranteed knockdown
and the ring are one event. Directions that share a kind share its timer: the Nine's three
directions are all `Scream`, so one scream holds the other two for 15 s.

**Identity** is the DreadAura pattern on three axes: hero StringId and named hero set (both
survive a data change that drops the race attribute; `nazgul_nine` resolves through
`INazgulRegistry`) and FaceGen race (finds the hero in a Custom Battle). An agent carries the first
signature whose hero ids or hero sets name it, else the first whose races do; a hero id or race
listed by two signatures is warned about once. With no `HeroObject` (a Custom Battle) the roster
passes the character's own StringId as the hero id: `Hero.Deserialize` binds a lord's hero to the
character of the same id, so the Nine's hero set finds them as commanders there too. Race names
resolve to ids once behind `IRaceManager.IsValidRaceName`; the hot path never calls
`GetRaceNameFromId`. The roster is keyed by `Agent` object reference and stores the agent's
signature index plus its stamps, filled in `OnAgentBuild` plus a one-shot scan of
`Mission.AllAgents` on the first tick (the safety net the DreadAura and warg trackers carry, for an
agent built before the behavior could see it), evicted in `OnAgentDeleted` (the callback the engine
recycles the index from), cleared in `OnCreated` and `OnEndMission` (it is a process singleton
because the model probes it). Never `Agent.Index` (#592).

**No `OnBehaviorInitialize`.** The engine dispatches it to the behaviors already in the list
(`Mission.AfterStart`, v1.5.3 `Mission.cs:3827`) BEFORE `SubModule.OnMissionBehaviorInitialize`
adds TAOM's (`:3831`), and `AddMissionBehavior` calls only `OnCreated` (`:4699`), so it never runs
for any TAOM behavior (#606; the first version gated the whole feature there and was inert in the
first battle, with no log line). The mission gate is read once on first use from whichever
callback arrives first; the combat type comes from native `InitializeMission` before any of them.

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
zero-shield-damage block does nothing (the cleave rule's parity). A scream has no ground-hit
basis, so a wraith's swing into the ground does nothing.

### Component Diagram

```
signature_strikes_config.json
        |
SignatureStrikesConfigProvider (validates every field; drops unknown rows, strikes with no
        |                        cooldown, id-less, repeated or identity-less signatures)
        |                      \
SignatureStrikeRegistry         SignatureStrikesSettingsProvider (MCM toggle + cooldown multiplier)
 (hero id | hero set | race            |
  -> signature index)          SignatureStrikeService (pure, per signature: Evaluate,
        |                        DecideKnockdown, DecideKnockback, ComputeRingDamage, ComputeFearDrain)
SignatureAgentRoster                   /                      \
 (Agent -> signature + stamps)        /                        \
        |                            /                          \
SignatureStrikesMissionLogic --- StrikeContextFactory --- TaomCombatMechanicsModel
 OnAgentBuild -> roster           (one boundary,          DecideAgentKnockedDownByBlow
 OnMeleeHit   -> Evaluate,         both paths)            DecideAgentKnockedBackByBlow
                 stamp, centre, enqueue                    (primary victim, ?? base)
 OnMissionTick -> StrikeRequestBuffer.Swap -> SignatureStrikeRunner
                                               StrikeSoundPlayer (MakeSound | Yell),
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
| `shieldBlockedMultiplier` | float 0..1 | Damage kept on a shield-blocked trigger and on a shield-blocking ring victim; shared by every signature |
| `signatures` | object[] | One entry per group of heroes. Empty is a legitimate "nobody"; `null` reverts to the compiled pair |
| `signatures[].id` | string | Names the signature in the log and is the key a bad field reverts by. Blank or repeated (case-insensitive) drops the entry with a warning |
| `signatures[].heroIds` | string[] | Hero StringIds. Empty is a legitimate "nobody on this axis"; `null` reverts |
| `signatures[].heroSets` | string[] | Named lore groups; only `nazgul_nine` is known. Unknown names are skipped with a warning at first use |
| `signatures[].races` | string[] | FaceGen race names. Unknown names are skipped with a warning at first use. In all three identity lists a null or blank name is removed with a warning and a padded one is trimmed; a signature left with no hero id, hero set or race matches nobody and is dropped with a warning |
| `signatures[].cooldowns.<Kind>` | float 0.5..120 | Seconds per attacker per kind, mission time. The floor keeps a cleaving swing to one package. An unknown kind drops the entry; a bad value reverts to that signature's compiled value, or drops when there is none |
| `signatures[].strikes.<Direction>` | object | One profile per `Overhead`, `Left`, `Right`, `Thrust` (member name only, case-insensitive, normalised; a numeric string is dropped). A direction with no row is a plain hit; an unknown key or `kind` drops the row, and so does a `kind` with no `cooldowns` entry. A profile with no `damageFraction` and no `fearMorale` rings nothing and logs nothing |
| `strikes.*.kind` | `Slam`, `Sweep` or `Scream` | The cooldown is per kind |
| `strikes.*.origin` | `Impact` or `Self` | Ring centre: the hit point, or the attacker. An unknown value reverts to the row's compiled origin |
| `strikes.*.outerRadius` / `innerRadius` | float 0.1..15 / 0..outer | Metres; full effect inside inner, one ninth at outer (the boulder curve) |
| `strikes.*.damageFraction` | float 0..1 | Share of the hit's damage a ring victim takes before falloff |
| `strikes.*.worldHitBaseDamage` | int 0..500 | Damage basis on a ground hit; 0 = a ground hit does nothing for this direction |
| `strikes.*.magnitude` | float 0..500 | Blow impulse on ring victims |
| `strikes.*.knockDown` | bool | Ring victims fall (riders dismount); the struck agent always falls |
| `strikes.*.knockBack` | bool | Ring victims and the struck agent are staggered back |
| `strikes.*.fearMorale` | float 0..100 | One-shot morale drain on ring victims and the struck agent before tier and race resistance; 0 = none |
| `strikes.*.sound` | string | A `module_sounds.xml` name played once per strike; letters, digits and `_ : / . -` only, blank for none |

Every float is NaN/Infinity-rejected before its range check and reverts to that signature's and
that direction's compiled default (the neutral default for an id the compiled set does not know)
with a warning; one summary warning says the file had invalid values.

### Current Values

| Signature | Direction | Kind | Origin | Outer / inner | Damage fraction | Ground hit | Magnitude | Knock down | Knock back | Fear | Sound |
|---|---|---|---|---|---|---|---|---|---|---|---|
| sauron | Overhead | Slam | Impact | 4.0 m / 1.5 m | 0.6 | 60 | 80 | yes | no | 15 | none |
| sauron | Left, Right | Sweep | Impact | 3.0 m / 1.5 m | 0.25 | 0 (none) | 60 | no | yes | 0 | none |
| nazgul | Overhead, Left, Right | Scream | Self | 6.0 m / 2.5 m | 0.3 | 0 (none) | 60 | no | yes | 25 | `LOTR/Mordor/Nazgul/nazgul_scream` |

Cooldowns: Sauron's Slam 20 s and Sweep 12 s, the Nine's Scream 15 s (one timer for all three
directions). Long on purpose: Mike's standing instruction is that Sauron must not be overpowered.
These are first guesses ahead of the in-game smoke; the engine boulder ring is 1.0 m / 1.2 m for
reference. Morale 25 is the value before `BattleMoraleModel.CalculateMoraleChangeToCharacter`
divides it by the character's morale resistance, floored at 1 (v1.5.3 `SandboxBattleMoraleModel.cs:95-98`
in a campaign and `CustomBattleMoraleModel.cs:72-75` in a Custom Battle, the same formula; TAOM's
subclasses of both leave the method alone. `CharacterObject.GetMoraleResistance` is at
`CharacterObject.cs:619-623`): `0.5 * tier + 1` for a
campaign troop (a tier 1 levy loses about 17, a tier 6 veteran about 6), `1.5 * (0.5 * (level / 4
+ 1) + 1)` for a campaign hero, and 1 in a Custom Battle, whose characters are
`BasicCharacterObject`s (`BasicCharacterObject.cs:268-271`), so there the full 25 applies. Then
race resistance (elf 0.4, dwarf 0.5) and falloff, clamped to what the agent has; only AI agents
(`DreadAgentGate`). MCM (group Combat Mechanics): `Signature Strikes` toggle and
`Signature Strike Cooldown Multiplier` (0.5x to 5x, live, every kind).

### Sound provenance

`Main/_Module/ModuleSounds/LOTR/Mordor/Nazgul/nazgul_scream.ogg`, registered as
`LOTR/Mordor/Nazgul/nazgul_scream` (`mission_voice_shout`, pitch range 0.95 to 1.05) in
`Main/_Module/ModuleData/module_sounds.xml`.

- **Source:** the clip Mike supplied, `TAOM_Nazgul Scream 2 .mp3`, 2026-09-23 (4.41 s, stereo,
  48 kHz, MP3). It replaced three takes generated the same morning with the ElevenLabs
  text-to-sound-effects API on the account's Creator plan (their prompts are in this section as of
  `b90fd3a4`); four film-style takes generated after them were not used. The three old files stay in
  the game install until deleted by hand: the module copy (`CopyModule`) runs with `Clean="false"`
  and never removes a file. Delete them after the next deploy (until then the installed
  `module_sounds.xml` still names them) and before packaging a release.
- **Processing:** ffmpeg 8.0.1: downmixed to mono at 44.1 kHz (a positional sound), a static gain of
  6.1 dB putting the mono peak at -1 dBFS before encoding, the input's metadata tags dropped
  (`-map_metadata -1`), encoded Ogg Vorbis `-q:a 5` (47.3 KB; the encoded peak reads -1.6 dB).
  Native's `module_sounds.xml` header lists `.ogg` and `.wav` as supported and caps a
  `mission_voice_shout` sound at 8 s.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SignatureStrikes/SignatureStrikeService.cs` | Pure decisions per signature: `Evaluate`, `DecideKnockdown`, `DecideKnockback`, `ComputeRingDamage`, `ComputeFearDrain` |
| `Main/Features/SignatureStrikes/SignatureStrikeFalloff.cs` | The engine's `1 / lerp(1,3,t)^2` band as a pure static |
| `Main/Features/SignatureStrikes/SignatureStrikeRegistry.cs` | Three-axis identity to a signature index, one id-keyed table per signature, built once |
| `Main/Features/SignatureStrikes/SignatureStrikesConfigProvider.cs` | Validating loader (DreadAura shape), per-signature fallback by id |
| `Main/Features/SignatureStrikes/SignatureStrikesSettingsProvider.cs` | Folds the Combat Mechanics master toggle; clamps the multiplier |
| `Main/Features/SignatureStrikes/Domain/` | `StrikeContext` / `StrikeEffect` record structs, `StrikeKindTimes`, the four enums, the config POCOs, `StrikeNames` |
| `Main/Features/SignatureStrikes/Hooks/SignatureStrikesMissionLogic.cs` | Entry point (`: MissionLogic`): roster lifecycle, enqueue on hit, drain on tick, stand-down on exception |
| `Main/Features/SignatureStrikes/Hooks/StrikeContextFactory.cs` | The one boundary from `AttackCollisionData` to `StrikeContext`, shared with the model |
| `Main/Features/SignatureStrikes/Hooks/SignatureStrikeRunner.cs` | Plays the sound and rings the enemies: query, falloff, blow, fear |
| `Main/Features/SignatureStrikes/Hooks/StrikeSoundPlayer.cs` | Per-mission sound id cache, `MakeSound`, the `Yell` fallback |
| `Main/Features/SignatureStrikes/Hooks/SignatureAgentRoster.cs` | Reference-keyed roster: each agent's signature and stamps |
| `Main/Features/SignatureStrikes/Hooks/StrikeRequestBuffer.cs` | Two-list swap buffer for the one-frame deferral |
| `Main/Features/SignatureStrikes/Hooks/SignatureMissionGate.cs` | `Combat` missions only, no multiplayer, no campaign requirement |
| `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` | Two thin delegations (optional ctor params, the `IRefugeDefenseService` precedent) |
| `Main/Features/AdvancedCombat/CustomAttacksUtils.cs` | `TakeDamage` gains `extraFlags`; `ComposeBlowFlags` extracted and pinned |
| `Main/_Module/ModuleData/signature_strikes/signature_strikes_config.json` | Shipped config |
| `Main/_Module/ModuleData/module_sounds.xml`, `Main/_Module/ModuleSounds/LOTR/Mordor/Nazgul/` | The scream's registration and its one clip |

Registration: `Main/IoC.cs` (`SignatureStrikesIoC.RegisterSignatureStrikesFeature`; DryIoc wires
`INazgulRegistry` into the registry), `Main/SubModule.cs` (the logic after `DreadAuraMissionLogic`;
the two services appended to the `TaomCombatMechanicsModel` constructor call, which is
campaign-only, so a Custom Battle gets the ring but vanilla primary knockdown and knock-back).

## Dependencies

- `IDreadRegistry` ([DreadAura](dread-aura.md)): race resist multiplier for the fear burst
- `DreadAgentGate` ([DreadAura](dread-aura.md)): which agents have morale to drain
- `INazgulRegistry` ([NazgulFamily](nazgul-family.md)): the `nazgul_nine` hero set; the Nine's race is [#644](hero-race.md)
- `CustomAttacksUtils`, `AgentSlotIdentity`, `MissionThreadGuard` (AdvancedCombat)
- `IRaceManager` (Core): race name validation at first use
- `IPathService`, `IModLogger` (Core)

## Tests

- `TAOM.Tests/Features/SignatureStrikes/SignatureStrikeServiceTests.cs`: direction mapping for both signatures, every rejection, shield-block and world-hit basis, cooldown boundary and NaN cases, the Scream's shared timer, the verdict matrix, ring damage rounding and overflow, fear headroom and sentinel
- `SignatureStrikeFalloffTests.cs`: inside, edge (1/9), midpoint, beyond, degenerate radii, NaN/Infinity per argument
- `SignatureStrikeRegistryTests.cs`: the three axes, hero before race, first listed wins, overlaps warned, unknown race and hero set skipped, no coercion, built once
- `SignatureStrikesConfigProviderTests.cs`: one test per validation rule, including the signature list, identity entries, cooldowns, origin and sound
- `ShippedSignatureStrikesConfigTests.cs`: the shipped file parses clean and ships exactly `sauron` and `nazgul`, each signature's identity, direction, cooldown and ring contract, the scream's sound registered with `.ogg` files on disk, no long dashes
- `StrikeKindTimesTests.cs`, `StrikeNamesTests.cs`: every kind round-trips, the name-only parsing
- `StrikeRequestBufferTests.cs`: swap semantics
- `StrikeContextFactoryTests.cs`: the two enum mirrors and the item-type gate
- `SignatureAgentRosterTests.cs`, `SignatureMissionGateTests.cs`: the null and lifecycle paths (an `Agent` cannot be built in a test)
- `SignatureStrikesBindingTests.cs` (`BindingVerification`): `OnMeleeHit`, `GetNearbyEnemyAgents`, `DecideAgentKnockedBackByBlow`, `BlowFlags.KnockBack == 0x10`, the two enum mirrors, `: MissionLogic`, `MakeSound`, `GetEventIdFromString`, `MakeVoice` + `VoiceType.Yell`, `GetEyeGlobalPosition`, the `SubModule.cs` call site
- `TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsBlowFlagsTests.cs`: the flag composer
- `CombatMechanicsModelInvariantsTests` (override set) and `SettingsFingerprintTests` count the feature's two settings

## How to give another hero signature strikes

1. Add an entry to `signatures` with a new `id`, the hero ids, hero sets and/or races, a cooldown
   for every kind its strikes use, and the strike rows. Or add a hero to an existing entry.
2. `ShippedSignatureStrikesConfigTests.ShippedConfig_ShipsExactlySauronAndTheNine` fails on
   purpose: update it with the balance decision and a control battle behind it.
3. No code changes.

To change what a direction does, edit its `strikes` row; to add a Thrust effect, add a `Thrust`
row. To add a NEW kind, append it to `StrikeKind` (never insert: `StrikeKindTimes` indexes by value)
and give it a cooldown; to add a new kind of EFFECT (a different ring shape, a buff), extend
`StrikeEffect` and the runner, and put the new field's gates in the service and the config
validation.

## Reading the log

Every stage writes one line, so a replay proves each one (`bin/Win64_Shipping_Client/Logs/taom_debug_*.log`):

| Line | Meaning |
|---|---|
| `SignatureStrikesConfigProvider: Loaded signature_strikes_config.json` | the shipped JSON parsed clean (a `contained invalid values` warning names the field otherwise) |
| `[SignatureStrikes] mission gate: eligible=True combatType=Combat multiplayer=False` | the mission qualifies; `eligible=False` names why (ArenaCombat, NoCombat, multiplayer) |
| `[SignatureStrikes] Witch-King of Angmar registered for 'nazgul' (id lord_1_15, race 15)` | identity resolved; the signature it carries, the id used (the hero's, or the character's in a Custom Battle) and the race id |
| `[SignatureStrikes] first-tick scan: 2 signature agent(s) on the field` | the safety scan; the count is the roster size |
| `[SignatureStrikes] hit by Sauron: Overhead StrikeAgent dmg=63 weapon=True canceled=False victim=Looter -> Slam` | one per signature-hero melee collision, with the direction the engine reported and the verdict (`-> no effect` with the reason readable off the fields: `Thrust`, `Parried`, `dmg=0`, `weapon=False`, or a cooldown) |
| `[SignatureStrikes] Scream ('nazgul') by Witch-King of Angmar: basis 48, ring=4 hit, 5 feared (morale -41.5), 1 skipped, sound=LOTR/Mordor/Nazgul/nazgul_scream` | the strike landed one tick later; `feared` counts the struck foe too; `sound=none` for a strike with no sound, `sound=yell (sound not registered)` when the name did not resolve, `sound=none (non-finite position)` when the attacker's eye position was NaN or infinite |
| `[SignatureStrikes] module sound 'LOTR/Mordor/Nazgul/nazgul_scream' resolved to event id N` | once per mission per name: the id the engine gave the name, so a wrong sound and an inaudible right one can be told apart |
| `[SignatureStrikes] module sound '...' is not registered (module_sounds.xml); the attacker yells instead` | once per mission per missing name |
| `[SignatureStrikes] disabled for this mission after ...` | a caught exception; the feature stands down for the battle |

No `hit by` line while the hero is swinging means the roster does not hold him (check the gate and
registration lines); `hit by ... -> no effect` on every swing means the gates in the line itself.

## Smoke (owed)

- Route A (everything): new campaign, PlayerSwitcher to Sauron (MCM "Allow Sauron and the Nazgul"
  on), attack a Gondor party. Overhead into a shield wall: struck agent drops, neighbours within
  4 m fall, the log line reads `feared > 0`. Overhead into the ground near a clump: the ring fires
  on the world-hit basis. Side swing: struck agent staggers, neighbours in front stagger. A second
  slam inside 20 s: no ring. Parries and blocks: no `[SignatureStrikes]` line.
- Route B (ring only): Custom Battle, Mordor, commander Sauron. Registers via the hero id (the
  character's own) or the race; the model is campaign-only, so expect ring falls with vanilla
  primary knockdown.
- Route C (the Nine): Custom Battle, Mordor, commander the Witch-king (`lord_1_15`; all nine are
  Mordor commanders, `custom_battle_commanders.json:4`) against Gondor. The log registers him for
  `nazgul`. An overhead and a side swing each log a Scream: ring foes stagger, the struck foe is
  counted in `feared`, the shriek is audible; a second scream inside 15 s, whichever direction,
  does nothing; a thrust does nothing. Note which directions a MOUNTED swing reports in the
  `hit by` line. Then the campaign: PlayerSwitcher to the Witch-king, attack a Gondor party, the
  struck foe is knocked back (the model is campaign-only) and DreadAura still pulses.
- Deploy first and restart the game (the installed `module_sounds.xml` must name
  `nazgul_scream.ogg`). Listen to the scream and check it carries over a battle at its volume, and
  that several of the Nine screaming close together do not sound like one clip on a loop; if they
  do, try widening the pitch range toward 0.9 to 1.1 (whether native picks a new pitch per play is
  unproven; this listen is the test), and add takes if the repeat persists. Native refuses a
  minimum above 1.0 or a maximum below 1.0, so a deeper scream is pitched in the file. The log's
  `resolved to event id` line names the id the shriek played under.
- The -1 contract: with a deliberately misspelled `sound` in a scratch copy of the config, the log
  must show `is not registered` and `sound=yell (sound not registered)`, and a yell must be heard.
  If a misspelled name resolves to an id instead, native does not answer -1 and the fallback needs
  another test.
- Negative: a tournament as Sauron shows no effect lines; `MissionThreadGuard` reports zero
  off-thread calls across a battle.
- Unverified until smoked (Codex review 114 left all three honestly open): whether a terrain hit
  carries a non-null `realHitEntity` (if not, the engine cancels the callback and the ground slam
  never fires); the thread of the native `MeleeHitCallback` (declared non-multithread-callable,
  `MissionThreadGuard` is the tripwire); and native honouring `BlowFlags.KnockBack` on a synthetic blow.
  `Agent.HandleBlowAux` hands the whole `Blow` to native and the engine's own melee path sets the
  same bit, but TAOM has only ever set `KnockDown` this way. If no stagger shows, the fallback is
  a low-magnitude `KnockDown` for the ring while the model-side knock-back on the struck agent
  stays. The ring's blow direction is the victim's local -X axis (`CustomAttacksUtils`), so a
  scream's stagger may push sideways rather than away from the wraith; a radial push is a follow-up.

## Changelog

- 2026-09-23: the scream is the clip Mike supplied (`TAOM_Nazgul Scream 2`), one variation in place
  of the three generated takes.
- 2026-09-23: Codex review 130 fixes: an identity list's null or blank entry is removed with a
  warning and a padded one trimmed, so neither leaves a signature that matches nobody.
- 2026-09-23: #645, the Nine's SCREAM: `signatures` list, `Scream` kind, `origin`, `sound`, per-kind
  stamps, the struck foe takes a strike's fear, Custom Battle character-id fallback, the scream's
  three ElevenLabs takes.
- 2026-09-16: #606, the mission gate moved off the dead `OnBehaviorInitialize` (never fires for a TAOM-added behavior) to a first-use read; stage logging for the replay.
- 2026-09-16: Codex review 114 fixes (stand-down clears the roster, name-only enum parsing, impact finiteness gate, shared damage cast, 0.5 s cooldown floor, inert profiles skip).
- 2026-09-16: feature landed (#605).

## GitHub Issue

- **Issue:** #605 SignatureStrikes: direction-mapped melee effects for Sauron
- **Status:** Open (smoke owed)
- **Issue:** #645 Nazgul SCREAM: a second SignatureStrikes signature for the Nine
- **Status:** Open (smoke owed)
