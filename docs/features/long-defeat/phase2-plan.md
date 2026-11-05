# The Long Defeat: Phase 2 plan

Written 2026-11-05 for branch `feat/long-defeat-phase2` at `92973f27a`, in the shape of [phase1-plan.md](phase1-plan.md), then revised the same day against a refutation pass (101 engine claims: 38 held, 55 partly, 5 unsettled, 3 refuted) and a completeness review. A builder works from this file. The feature doc is [long-defeat.md](../long-defeat.md), the leaderboard plan [leaderboard-plan.md](leaderboard-plan.md), the issue [#1079](https://github.com/haterade22/TAOM/issues/1079).

**Scope (decisions by Mike, binding):** enemy commanders every 4th wave with abilities on a cooldown; affixes from wave 5; boons every 3 cleared waves with rerolls; four classes with start kits; the Cavalry sally (S9 first); the endless scale; the report and leaderboard learning the new fields; an unattended run that still plays a full run. Constraints: ADR-002, ADR-007, TDD, no `#region`, `[Obsolete]` or `#if`, `Main/IoC.cs` and `Main/SubModule.cs` untouched, `{=TAOM_LD_*}` keys, co-op absent.

**Engine source:** `C:/Users/mikew/.taom-src/v1.5.6-202611041234/`, the installed v1.5.6. File names drop the `TaleWorlds.MountAndBlade.` prefix. `pwsh tools/taom-src.ps1 path` prints only its first argument, so look up one type per call.

**What the revision changed:** the three refuted claims are rewritten (findings 5 and 12, the home of the `balance` hasher). Each partly-held claim is corrected where it stands, with its guard, apply order, files and tests. The five unsettled ones are named risks with a settling step. The reviewer's points are designed in, or listed in section 10 with a recommendation. Section 12 lists the verdicts not applied, with the reason.

---

## Findings that shape the design

1. **A class kit is one call, even on a hero.** `Mission.DecideAgentSpawnEquipment` takes the override first: `Equipment equipment = ((agentBuildData.AgentOverridenSpawnEquipment != null) ? agentBuildData.AgentOverridenSpawnEquipment.Clone() : ...` (`Mission.cs:4108`). The hero `FixedEquipment(true)` (`:4284-4286`) matters only when nothing overrides. `AgentBuildData.Equipment(Equipment)` sets that override (`AgentBuildData.cs:232`, read back at `:15`).
2. **A horse comes from the kit Horse slot, at the agent initial position.** `if (item != null && item.HasHorseComponent && item.HorseComponent.IsRideable)`, then `CreateHorseAgentFromRosterElements(..., agentBuildData.AgentInitialPosition.GetValueOrDefault(), ...)` (`Mission.cs:4311`, `:4318`). `NoHorses` clears both horse slots (`:4109-4112`), and `GetAgentBuildDataToSpawnTroop` sets `.NoHorses(!spawnWithHorse)` (`:4468`). The Phase 1 adapter passes `spawnWithHorse: false`, so every soldier is on foot by construction. With `spawnWithHorse` true the engine reads the troop's own Horse slot for a mount key (`:4500-4502`), so the mounted path passes false and calls `.NoHorses(false)` itself (`AgentBuildData.cs:250`). The horse is built before its rider and with the rider's build data: `BuildAgent(agent2, agentBuildData)` (`Mission.cs:4374`), then `BuildAgent(agent, agentBuildData)` (`:4380`).
3. **A given position skips the formation layout.** `if (!agentBuildData.AgentInitialPosition.HasValue)` is the only branch that computes a slot (`Mission.cs:4227`), and `SetInitialFrame` then takes the height (`:4271`). A position needs a direction: `agentBuildData.InitialDirection(initialDirection.Value)` (`:4498`) throws on a null one. The sally point is a recorded point, checked for a navmesh and for the mission boundary (`Mission.IsPositionInsideBoundaries`, `:1667`), for one agent; the Phase 1 rule "never compute a place by hand on a scene with floors" is about computed grids.
4. **Health can be written after spawn.** `BaseHealthLimit` and `HealthLimit` have public setters (`Agent.cs:911`, `:915`), and the engine's own horse spawn writes `agent.BaseHealthLimit = ...; agent.HealthLimit = agent.BaseHealthLimit; agent.Health = agent.HealthLimit;` (`Mission.cs:4607-4609`). In a Custom Game spawn the other writer is the `Character` setter (`Agent.cs:1438-1439`, run from `Mission.cs:4079` before `Build`). Sandbox, multiplayer and naval models also write it but do not run here, and every spawn-time writer finishes before `SpawnAgent` returns, so a post-spawn write wins.
5. **Armour is written once, at spawn, through a managed chain.** `InitializeAgentStats` sets the four armour values (`CustomBattleAgentStatCalculateModel.cs:25-28`); `UpdateAgentStats` (`:55`) does not. The chain: `Mission.cs:4380` (`BuildAgent`), `:4040` (`agent.Build`), `Agent.cs:5209` (`InitializeAgentProperties`), `:3671` (`InitializeDrivenProperties`), `AgentDrivenProperties.cs:1193` (`InitializeAgentStats(agent, spawnEquipment, this, agentBuildData)`). Build data is null only from `SpawnMonster` (`Mission.cs:4457`), and a horse gets its rider's (finding 2), so the armour post-pass guards null and gates on `agent.IsHuman`.
6. **Four affixes need no new arithmetic.** `RaceAbilityStatApplier.Apply` writes `SwingSpeedMultiplier`, `AIAttackOnDecideChance`, `MaxSpeedMultiplier`, `CombatMaxSpeedMultiplier`, `TopSpeedReachDuration`, `AiShooterError`, `ThrustOrRangedReadySpeedMultiplier`, `ReloadSpeed`, `MissileSpeedMultiplier`, `AIBlockOnDecideAbility` and `AIParryOnDecideAbility`, each "assigned afresh on each `UpdateAgentStats`" (its header). `ApplyMount` writes only `MountSpeed` from `MountSpeedPercent`, so a mounted row copies its move speed into that field. The applier runs wherever `Agent.UpdateAgentProperties` runs (`Agent.cs:3692-3699`), which is event-triggered: a mid-wave change needs `agent.UpdateAgentProperties(); agent.MountAgent?.UpdateAgentProperties();` per agent, as `MissionAbilityExecutionContext.cs:129-131` does.
7. **Damage factors go through the damage model,** the only seam with a reduction side. `TaomCustomBattleDamageModel` notes that the Custom Battle model reads no driven-property damage bonus, and `FordsUpgradeHooks.Amplify` is the precedent of a mode's own hook there.
8. **Morale is 0 to 100.** `CommonAIComponent.Morale` clamps with `MBMath.ClampFloat(value, 0f, 100f)` (`CommonAIComponent.cs:47`); `ChangeMorale` adds to it (`AgentComponentExtensions.cs:21-27`). `DreadMissionGate.IsEligible` admits Fords by name and otherwise needs a campaign, so Winged Terror is the director's own pulse.
9. **Engine hit points are writable.** `DestructableComponent.MaxHitPoint` is a public field (`DestructableComponent.cs:66`); `HitPoint` has a public setter clamped at 0 (`:107-126`).
10. **Teleport carries the horse; a fade does not clear the main agent.** `Agent.TeleportToPosition` moves the mount, then the rider (`Agent.cs:4287-4299`), and `LookDirection` has a public setter (`:1493-1495`). `FadeOut` (`:4304-4310`) hands removal to native. `OnAgentDeleted` never clears `MainAgent` (`Mission.cs:2981-2995`); only `OnAgentRemoved` does (`:3017-3021`). A new body with the Player controller becomes main as it builds (`Agent.cs:5206`, then `Mission.MainAgent = this` at `:1255`).
11. **Hits come from native callbacks** on a thread native picks. No log has caught one off the main thread, but the threads doc's "Either" row (`mission-frame-threads-and-native-costs.md:119`) covers them, so they go through `RunOrDefer` as a precaution. `OnScoreHit` is raised beside `OnAgentHit` with the engine's own `isBlocked` and `damagedHp` (`Mission.cs:5671-5672`). The affector can be null (`Agent.cs:5465`), and `KillAgentCheat` raises a hit whose affector is the victim (`Agent.cs:5527`).
12. **The commanders exist in a Custom Game.** `characters/lords.xml` loads for `CustomGame` (`SubModule.xml:198-205`) and defines `lord_I3_1` Lugdush (`:19817`), `lord_I1_1` Ugluk (`:19609`), `lord_I4_1` Lurtz (`:19921`) and `lord_I2_1` Mauhur (`:19713`), all `Culture.isengard`, race `uruk_hai`. `lord_1_63` (Gorbag) is a Custom Battle stub (`custom_battle_lords.xml:18`) that `lords.xslt` rebuilds into a full orc hero, so Phase 5 adds only his `commanders` row, never a `characters/lords.xml` row. A commander spawns with his `lords.xml` skills and first battle set, `IsUnique` (`Mission.cs:4520-4522`), and TAOM race abilities rate him at `HeroFactor`. Nothing raises his health, so the rank sets it (section 2).

---

## 1. Files

### TAOM (Main)

**`Main/Features/LongDefeat/Domain/`** stays pure. `LongDefeatDomainPurityTests` forbids only `TaleWorlds` and `TAOM.Adapters` (`LongDefeatDomainPurityTests.cs:12`), so the domain may use `FordsOfWar.Domain`. Every new `Ld*` record an adapter takes or returns (`LdPlace`, `LdLoadout`, `LdStatTable`, `LdRefresh`, `LdProbe`) lives here, so that test guards it beside `ServiceSignatureGateTests`.

| File | Responsibility |
|---|---|
| `RunRandom.cs` | SplitMix64 over a `ulong` seed; `For(seed, stream, a, b)` derives an independent generator per purpose; `Below(n)` maps a draw to `0..n-1` by `next % n` |
| `EndlessScale.cs` | The growth of `WaveScaling`: health and damage per wave, cap, tier steps and chain |
| `AffixPlan.cs` | Powers, schedule, `Roll(seed, wave)` at the full schedule count |
| `CommanderPlan.cs`, `CommanderClock.cs` | Ranks, abilities, the step rule; one self-firing timer per ability |
| `BoonPlan.cs`, `BoonDeck.cs` | Boon rows; owed picks, offers, the reroll bank, the draw |
| `ClassPlan.cs` | `RunClass` (id, mounted, boon pool) and `ClassKit` per defender culture |
| `RunModifiers.cs` | Folds class, boons, affixes and endless factors into `EffectiveRules`, `GoldFactors` and an `LdStatTable` of fresh `RaceAbilityEffects` copies |
| `SurvivalRun.Commander.cs`, `.Affixes.cs`, `.Boons.cs` | Partials of the aggregate |
| `RunPlanTroops.cs` | `AllTroopIds(plan)`: garrison, shop, authored waves, every tier target, summon mixes, commander troops |

Edited: `WaveSpec.cs` (`WaveSoldier` gains `Role`), `WavePlan.cs` (factors, tiers, a static `Expand(count, mix)` that `Expand(wave)` delegates to), `RunOptions.cs`, `RunPlan.cs`, `RunEffects.cs`, `RunView.cs`, `RunStats.cs`, `Purse.cs` (`Commander`, `Boon` sources), `ShopBot.cs`.

**`Main/Features/LongDefeat/Config/`:** `AffixesReader.cs`, `BoonsReader.cs`, `ClassesReader.cs`; `WavesReader.cs` (scaling growth, `tiers`, `commanders`); `FortressesReader.cs` (`sally`); `LongDefeatConfigCatalog.cs` reads six texts and **owns the `balance` hash** (`HashOf`, `:55-60`, filled at `:39`), which `LongDefeatRunReport` keeps copying from `RunProvenance.Balance`.

**`Main/Features/LongDefeat/Hooks/`** (each under 150 lines, `LongDefeatEntryPointLineCountTests`):

| File | Responsibility |
|---|---|
| `LongDefeatStatHooks.cs` | `UpdateStats(agent, props)` and `InitializeArmour(agent, props, buildData)`, any thread; one volatile `LdStatTable`; armed with the mission like `LongDefeatMorale` |
| `LongDefeatCombatHooks.cs` | `Amplify`, `Reduce` for the damage model, `FordsUpgradeHooks` shape, `Interlocked` counters |
| `LongDefeatMissionLogic.cs` | Edited: `OnScoreHit` forwards a filtered hit, `OnAgentDeleted` logs the main body during S9, arming covers the new hooks; under 150 lines |
| `LongDefeatMissionLauncher.cs` | Edited only to call `RunPlanTroops.AllTroopIds`; it is at 144 lines and must not grow |

**`Main/Features/LongDefeat/` root:** new partials `LongDefeatDirector.Commander.cs`, `.Affixes.cs`, `.Player.cs`; `LongDefeatDirector.cs` and `.Effects.cs` edited (apply order, section 4); `LongDefeatSetup.cs` (class), `LongDefeatSetupPrompt.cs` (second question), `LongDefeatSession.cs` (`TryPickBoon`, `TryReroll`), `LongDefeatRecorder.cs` (`sally` kind), `LongDefeatCommands.cs` (`taom.ld_start <fortress> [waves] [class]`, `taom.ld_record sally`).

**`Main/Features/LongDefeat/Diagnostics/`:** `LongDefeatChecks.cs` gains the section 6 ids; `LongDefeatRunReport.cs` writes schema 2.

**`Main/Adapters/`:**

| File | Responsibility |
|---|---|
| `LongDefeatAgentRegistry.cs` | The `FordsAgentRegistry.cs` shape: `AgentKey`s from a counter that only goes up, agents by reference, one lock, evicted at removal. Holds each agent's role and a felled mark. Never `Agent.Index` |
| `ILongDefeatEffectsAdapter.cs`, `LongDefeatEffectsAdapter.cs` | Stat publish and refresh, health writes, morale pulse, poison damage, engine hit points, read-backs. A split by concern |
| `LongDefeatBattlefieldAdapter.Player.cs` | New partial in the `.Diagnostics.cs` shape: `SpawnPlayer(LdLoadout, LdPlace)`, `TeleportPlayer`, `RemountPlayer`, `SallyPointUsable` |
| `ILongDefeatBattlefieldAdapter.cs`, `LongDefeatBattlefieldAdapter.cs`, `.Diagnostics.cs` | Edited: `SpawnBatch` registers roles and writes health; `Fell` marks felled agents and owns its blow; `Classify` returns role and felled mark |

**Shared models (one line each; `graphify affected` first):** `TaomCustomBattleAgentStatCalculateModel.cs` (`UpdateStats` after `RaceAbilityHooks.ApplyStats`, `InitializeArmour` after the charge-damage line) and `TaomCustomBattleDamageModel.cs` (`Amplify`, `Reduce` around the Fords calls).

**Data:** `long_defeat/{affixes,boons,classes}.json` new; `waves.json`, `fortresses.json` edited; `taom_module_strings.xml` and the 12 language files gain `TAOM_LD_*` rows with the English seed.

### TAOM.Client (`Client/Features/LongDefeat/`)

`LongDefeatBoonVM.cs` and `LongDefeatAffixVM.cs` new; `LongDefeatHudVM.cs` (with a `CanReroll` property) and `LongDefeatHudText.cs` edited; prefabs `LongDefeatHud.xml`, `LongDefeatPanel.xml` edited; `TAOM.Tests/Features/LongDefeat/Client/LongDefeatPrefabTests.cs` gains both VMs in its type list (`:56`).

### Tools and docs

`tools/run_long_defeat.py`, `tools/tests/test_run_long_defeat.py`, `tools/ingame_checks/1079.toml`; `docs/features/long-defeat.md`, `leaderboard-plan.md`, `docs/reference/provenance-register.md` (one row), `docs/features/dev-console.md`, `docs/reference/feature-map.md`.

**Nothing is deleted, and Fords code stays untouched.** `LongDefeatSession` already reads `FordsGameSpeed` from `Features/FordsOfWar/Domain/`; Phase 2 adds `PoisonRules`, `AgentKey` and `MatchSide` from the same namespace.

---

## 2. Domain

Every Phase 1 rule stands. `SurvivalRun` is touched only on the main thread; hits and removals reach it through `RunOrDefer`.

### Randomness

- Phase 1 had none. Phase 2 rolls affixes and boon offers from `RunOptions.Seed`, a `ulong`: arm key `seed` takes a decimal from 0 to 2^63-1 and refuses a negative; with none, the start time's ticks. The report carries it, so the bot can replay every roll.
- **Exact derivation** (the bot must match it): `For(seed, stream, a, b)` starts SplitMix64 at `seed ^ (stream * 0x9E3779B97F4A7C15) ^ (a * 0xBF58476D1CE4E5B9) ^ (b * 0x94D049BB133111EB)`, all unchecked `ulong`. `Below(n)` is `next() % n`. No floating point anywhere in a draw.
- **Streams:** affixes `(1, wave, 0)`; boon offers `(2, pick, draw)`; the bot class `(3, 0, 0)`.
- **Pinned vectors** in `RunRandomTests` and `test_run_long_defeat.py` alike: the first outputs of `For` for several tuples, the affix rolls of seed 1 at waves 5, 10 and 15, and one full offer and reroll sequence for seed 1 over the shipped pool. Matching raw outputs alone would not stop the two sides drifting.

### Endless scale (Phase 1 rule 12 grows)

Past the authored list, with `extra = wave - authored count`:
- **Count:** the Phase 1 formula, unchanged. Live room caps attackers standing at `max(101, min(liveCap, siegeBattleSize - defenders))` (Phase 1 rule 6). At a low battle-size setting the 101 floor lets the field exceed the battle size by design, to keep the engines in use; nothing here bounds attackers plus defenders.
- **Health and damage:** `min(maxFactor, 1 + healthPerWave * extra)`, and the same with `damagePerWave` (assumption A9). Every attacker of the wave takes them.
- **Tiers:** `steps = extra / tierEveryWaves` (integer). Each mix row moves `steps` links along the `tiers` chain and stops at its end; two rows landing on one troop merge their weights.
- An authored wave has factors 1 and no tier steps. The intermission shows the next wave's factors.

### Affixes

- **Count** for wave n: the last schedule row with `fromWave <= n`: 0 before 5, 1 from 5, 2 from 10, 3 from 15.
- **Roll:** `NextAffixes` is rolled at the full schedule count for wave n+1 when wave n clears, and at `Begin` for the first wave. It is a partial Fisher-Yates over the eight ids in the fixed order `lash, drums, eye, discipline, hide, hammer, terror, blades`: for i from 0, `j = i + Below(8 - i)`, swap. The horn does not reroll it.
- **Cut at wave start:** the wave carries the first `max(0, schedule count + affixCount)` of the stored roll, `affixCount` coming from the boons owned when the wave begins. A Legend boon picked in the intermission therefore acts on the next wave with no reroll, and the HUD shows the cut list. The replay rule is the same: a prefix of the roll, its length from the boons taken before the wave began.
- **Publish before spawn:** the wave's stat record and engine hit points ride the `StartWave` effect, which the director applies before any attacker batch (section 4), so the first batch of a `hide` wave is armoured.

| Power (Mordor name) | Domain output | Route |
|---|---|---|
| `lash` (The Overseer's Lash) | attackers' `SwingSpeedPercent`, `AttackEagernessPercent` | stat hook |
| `drums` (Drums in the Deep) | `MoveSpeedPercent`, `AccelerationPercent` | stat hook |
| `eye` (The Lidless Eye) | `AimErrorPercent` (negative), `DrawSpeedPercent`, `ReloadSpeedPercent`, `MissileSpeedPercent` | stat hook |
| `discipline` (Iron Discipline) | `BlockAbilityPercent`, `ParryAbilityPercent` | stat hook |
| `hide` (Troll-Hide) | `ArmourPercent`, humans only | armour hook, at spawn |
| `hammer` (Hammer of the Underworld) | `EngineHitPointsPercent` | effects adapter, at wave start |
| `terror` (Winged Terror) | radius, pulse seconds, amount, floor | `fx.MoralePulse` |
| `blades` (Morgul Blades) | poison per second, seconds | `PoisonRules` in the run, `fx.PoisonTicks` |

- **Morgul Blades:** `SurvivalRun` holds Fords' `PoisonRules` (one dose per victim, a stronger dose replaces a weaker, fractions carry) and calls it only on the main thread, since it holds plain dictionaries. Attackers map to `MatchSide.East`. `DefenderHit(victim, attacker)` applies a dose in a `blades` wave and ignores a key that `Forget` has seen. The run counts **doses** (a victim newly poisoned) apart from **hits** (every blades hit, refreshes included), because `PoisonRules.Totals` counts every `Apply`.
- **Winged Terror:** every `pulseSeconds` of such a wave, `fx.MoralePulse`. A defender within `radius` of a standing attacker loses `amount`, never below `floor`; one already under the floor is left alone. The pulse alone never takes anyone below 35, but it lowers the headroom against casualty losses, which can still rout a defender (decision D7).
- **Hammer:** `fx.EngineHitPoints = factor`, 1 without the affix. It does little once the Helm's Deep gate has fallen, and nothing repairs that gate until Phase 3. On a `StartWave` run, or a run whose wave 1 ram failed, it hardens the ram that still has to break the gate. Its check proves the write and logs the gate's state.

### Commanders

`CommanderPlan.ForWave(n)` is null unless `n % every == 0` (4). Rank index `min(n / every - 1, validRanks - 1)`, where `validRanks` counts the ranks that passed validation. `steps = min(maxSteps, max(0, n / every - validRanks))`: 0 at waves 4, 8 and 12, 1 at 16, 2 at 20. A step multiplies the rank health by `1 + healthPerStep * steps` and cooldowns by `cooldownFactorPerStep ^ steps` (never under 5 s), and adds `summonPerStep * steps` to each summon, all capped in JSON.

1. **Health:** each rank sets its own `health` factor, written after spawn like the endless factor and multiplied with it, because a lord in a Custom Game has no more health than the troops beside him (finding 12).
2. **Spawn through the queue:** when the wave's first batch closes, one soldier of role `Commander` goes to the head of the attacker queue (`InsertRange` at `_queueHead`). He spawns as the next trickle soldier, takes one slot of live room like anyone, and joins the attacker infantry formation, `Place=false`. He is outside the wave's roster count. The attackers' `GeneralAgent` stays unset (assumption A5).
3. **Clocks:** `CommanderClock` follows the shape of `FordsMatch.cs:252` and `:257-258` (ready time is now plus cooldown; time left is ready minus now, at least 0) and adds what Fords lacks: a first delay seeded at `CommanderUp() + firstSeconds`, firing by itself on `Tick` at most once per tick however long the step, and a terminal `Stop()`. Its clock is the run's wave seconds.
4. **Two ability kinds** (assumption A4):
   - `Summon`: `count` soldiers from `WavePlan.Expand(count, mix)`, role `Summoned`, inserted at `_queueHead`. They spawn as live room allows from the attacker reinforcement frame, like the trickle, so a summon never exceeds live room (rule 6, floor 101). The stalemate rule's wave count becomes roster plus summons queued this wave, so stranded summons scale the straggler share instead of turning a fell into a stalemate.
   - `WarCry`: for `durationSeconds`, every attacker gets the ability's percentages in the attackers' stat record; the director publishes and refreshes standing attackers at its start and end.
5. **His end:** `CommanderDown(killed)`. Every clock stops for good, a running war cry ends, queued summons still come (A6). `Classify` reports the commander for any removal: a kill or knock-out is `killed: true`; a removal that is no loss (a deletion, leaving the field) is `killed: false`, so a commander who leaves alive stops summoning and the HUD stops showing him.
6. **Pay:** a kill pays the rank's `bounty` into `PurseSource.Commander`; a felled commander pays nothing.

### Who fell (replaces the `_fellPending` counter)

`Fell` marks each agent it fells in the registry before the kill, and `Classify` returns `(role, felled)` from that mark. `SurvivalRun` books each removal by its own role and mark: roster felled, summon felled, commander felled, each paying nothing. The anonymous `_fellPending` counter goes, and so does adding to `_waveFelled` at decision time; `Fell`'s return count is reconciled with the marks, so no fell stays pending. At every clear, roster kills plus roster felled equals the roster count, which keeps leaderboard rule 5 true even when a summon or the commander is felled.

`RunStats` keeps per wave: roster kills and felled; summons queued, spawned, killed and felled; the commander (troop, rank, steps, outcome `killed`, `felled`, `left` or `standing`, uses per ability, uses after his end); poison doses, hits, damage, owners missing, ticks withheld.

### Boons

**Settled from the design reference** (the Achaea page, as the coordinator read it on 2026-11-05): one reroll per five completed floors, a bank of at most three, three options per offering, boons last the run, some carry drawbacks. Here a floor is a cleared wave. Tailoring and hidden combos stay out of Phase 2; those two words come from the approved plan (`when-in-a-siege-enchanted-moth.md`, "Decisions from Mike", Boons).

- **Owed picks:** each clear that brings `WavesCleared` to a multiple of 3 owes one pick, until the run ends. One offer is open at a time. An open pick blocks nothing and stays open into later intermissions (D2). Picks are made between waves only.
- **Eligible rows,** in `boons.json` file order: the chosen class's rows, a troop category's rows only when the fortress garrison or shop holds a troop of that group (static data, so the replay sees it), fortress, war chest and Legend. A boon owned is never offered again.
- **The draw:** integer weights per row, 10 for every row and `legendWeight` (an integer, shipped 1) for a Legend row. For each card: `r = Below(total weight)`, walk the rows in order to the one `r` falls in, take it, remove it. Fewer than 3 eligible: offer what is left; none: the pick lapses with a log line.
- **Rerolls:** one is earned at each clear that brings `WavesCleared` to a multiple of 5; the bank holds at most 3. `TryReroll` draws with the next draw index and leaves out the 3 just shown, unless fewer than 3 others are eligible, in which case it leaves out none. `StartRerolls` (test only) seeds the bank.
- **Effects,** a closed vocabulary; the catalog refuses any other key:

| Effect keys | Category | Route |
|---|---|---|
| `bountyPercent`, `waveBonusPercent`, `earlyGoldPercent`, `pricePercent` | war chest | `GoldFactors` |
| `gold` (paid once, at the pick) | war chest | `PurseSource.Boon` |
| `garrisonCap`, `intermissionSeconds`, `restShare`, `keepHoldPercent`, `packBonus` | fortress | `EffectiveRules` |
| `affixCount` (negative only) | Legend | the affix cut |
| `commanderCooldownPercent` | Legend, ships with commanders (Slice 7) | `CommanderClock` |
| `swingSpeedPercent`, `moveSpeedPercent`, `drawSpeedPercent`, `reloadSpeedPercent`, `blockPercent`, `parryPercent`, `aimErrorPercent`, with `target` | troops, class | stat hook |
| `damagePercent`, `damageTakenPercent`, with `target` | troops, class | combat hook |
| `healthPercent`, with `target` | troops, class | health write (below) |

- **Groups:** `target` is `infantry`, `archers`, `cavalry` or `player`. A defender's group comes from its character: `IsMounted` is cavalry, else `IsRanged` is archers, else infantry (`BasicCharacterObject.cs:70-72`, immutable, safe on any thread), so every `FormationClass` lands in one group and a rider on foot is cavalry (A8). The main agent takes the `player` record only, never a troop record.
- **Mounted rows:** the cavalry and player rows copy `moveSpeedPercent` into `MountSpeedPercent`, which `ApplyMount` reads; acceleration does not reach a horse.
- **Health:** a `healthPercent` boon reaches those standing at the pick by its own factor only (`ScaleHealth`), so a second boon never applies the first again. Every later spawn takes the combined factor: `SpawnBatch` for defender batches by group, `SpawnPlayer` for the lord, and the takeover hook for a soldier's body.
- **Drawbacks** use the same vocabulary under `drawback` and show on their own line. A boon lasts the run.

### Classes

- `RunClass(Id, Mounted, BoonPool)` and a `ClassKit` per defender culture, keyed by XML slot names (`Item0` to `Item3`, `Head`, `Body`, `Leg`, `Gloves`, `Cape`, `Horse`, `HorseHarness`), mapped through `Equipment.GetEquipmentIndexFromOldEquipmentIndexName` (`Equipment.cs:225`), never `Enum.Parse`, which has no `Item0`.
- **The kit:** a clone of the player troop's `FirstBattleEquipment` with the named slots overwritten; an empty string clears a slot. Before each write, `Equipment.IsItemFitsToSlot` (`Equipment.cs:445`), and a `Horse` must have a rideable horse component. A foot class always clears `Horse` and `HorseHarness`. A null first battle set, or an item the engine does not know, spawns the lord in his own gear with one warning.
- **Chosen at run start:** the menu's second question, `taom.ld_start`'s third word, arm key `class`; with none in the arm file the bot takes `For(seed, 3, 0, 0).Below(classes on offer)`.
- The kit applies at every spawn of the lord (`Begin`, respawn, remount). A soldier's body taken over after death keeps its own gear (Phase 1 rule).
- **Cavalry rides all run** (replaces the first plan's A7; decision D4): `Begin` spawns the lord mounted at the home point into the defender **Cavalry** formation, not Infantry, because one rider in a formation switches every soldier in it to mounted spacing (`Formation.cs:412-414`, `:587`). The home point is the fortress's recorded `muster.foot` point (open ground at Helm's Deep), else the keep point. **No code ever fades a horse:** fading a mount a soldier holds or has chosen crashes (orientation trap "Riderless creatures"), and a loose horse after his death may be reserved by an attacker.

### The sally (Cavalry, spike S9)

- **Points at boot:** the `sally` point and the home point must each have a navmesh and lie inside the mission boundary (`Mission.IsPositionInsideBoundaries`, `Mission.cs:1667`). A failing point logs `[LongDefeat][Sally] point (..) navmesh yes|no boundary yes|no` and Cavalry falls back to "mounted from `Begin`, no sally". `CanSpawnOutsideOfMissionBoundary` stays unset.
- **Out,** at `StartWave`, when the class is mounted and the main body is the lord's own: a living mounted lord is teleported to the sally point and turned to its facing (`TeleportToPosition`, then `LookDirection`). A lord on foot (his horse died) is **remounted** there, in one main-thread step: read his health share, mark the body as a planned removal by identity, `FadeOut(true, false)`, `SpawnPlayer` mounted at the point with the Player controller (the new body becomes main as it builds, finding 10), `RefreshStats(player)` because the horse was built before its rider, then write the saved health share (D3). A lord who died and took over a soldier gets no sally until his respawn.
- **Home,** at the clear: a living lord is teleported to the home point, mounted or not.
- **No false death:** the planned-removal mark matches the body by identity for any removal state the engine reports, because the plan check reads native `FadeOut` as setting `Routed`; Phase 1's `IsLoss` is false for `Routed` and `Deleted`. The remount line fails when the old body is reported `Killed` or `Unconscious` (risk 2).
- **The boundary:** `MissionBoundaryCrossingHandler` watches `Agent.Main` whatever its controller (`MissionBoundaryCrossingHandler.cs:210`) and after its 10 s leeway (`:29`) calls `RetreatMission` (`:148`), which Phase 1 turns into leaving the run (`LongDefeatMissionLogic.cs:64-67`). A rider who strays outside ends the run; the sally line logs seconds outside (risk 17).
- **Fallbacks:** the same code with a `sally` point recorded in the courtyard; if teleport or remount itself misbehaves, "mounted from `Begin`, no sally".

### Signatures

```csharp
internal enum AttackerRole { Roster, Summoned, Commander }

internal sealed partial class SurvivalRun
{
    public RunClass Class { get; }
    public IReadOnlyList<string> NextAffixes { get; }          // the cut list the HUD shows
    public CommanderState? Commander { get; }
    public void CommanderUp();
    public void CommanderDown(bool killed);
    public void AttackerDown(string troopId, AttackerRole role, bool felled);
    public void DefenderHit(AgentKey victim, AgentKey attacker);
    public void Forget(AgentKey agent);
    public PickResult TryPickBoon(string boonId);   // Ok, NotOpen, NoOffer, NotOffered, RunOver
    public RerollResult TryReroll();                 // Ok, NotOpen, NoOffer, NoReroll, RunOver
}
```

- `SoldierBatch` gains `Role` and `HealthFactor` (attackers: endless times rank; defenders: the group boon factor).
- `RunEffects` gains `PublishStats` (an `LdStatTable`) and `EngineHitPoints`, both applied first, then `RefreshAttackers`, `RefreshDefenders`, `RefreshPlayer`, `MoralePulse`, `PoisonTicks`, `Sally { None, Out, Home }`.
- `RunView` gains `ClassId`, `NextAffixes`, the next wave's factors, `Commander`, `Offer`, `OwedPicks`, `Rerolls`, `Boons`.
- **`ShopBot.Decide`** answers an open pick first: reroll when it holds one and every card has a drawback, else the first card without one, else the first card; then buys and sounds the horn as in Phase 1.

**Rules, each with a test:** the affix schedule at waves 4, 5, 9, 10, 14, 15 and 40; the affix cut with `affixCount`; distinct powers; one seed gives one roll whatever the rerolls; picks owed at clears 3, 6 and 9; rerolls earned at 5, 10 and 15, never above 3; a commander only at multiples of 4, steps 0 at waves 4 and 8 with health factor 1 and the summon count unchanged, steps from 16; no ability use after `CommanderDown`; a commander removal that is no loss stops him; summons outside the roster count and inside the stalemate count; roster kills plus felled equals the roster count with a felled summon and a felled commander in the wave; the endless factors at the cap; a tier chain that ends; NaN and infinity refused in every new float.

---

## 3. Config

Six files under `long_defeat/`, each read once per process. The Phase 1 validation rules stand: one warning per bad value naming file and key, floats checked for NaN and infinity before range, whole numbers as JSON integers. The numbers below are first guesses, not a balance measure.

**`waves.json`, the roster gains three blocks** (Isengard shown):

```json
"scaling": { "countPerWave": 10, "maxCount": 300,
  "healthPerWave": 0.05, "damagePerWave": 0.03, "maxFactor": 3.0, "tierEveryWaves": 3 },
"tiers": { "fow_isengard_militia_spearman": "fow_urukhai_warrior", "fow_urukhai_fighter": "fow_urukhai_warrior",
  "fow_urukhai_warrior": "fow_urukhai_spearman", "fow_urukhai_spearman": "fow_urukhai_veteranspearman",
  "fow_urukhai_swordman": "fow_urukhai_infantry", "fow_urukhai_infantry": "fow_urukhai_veteraninfantry",
  "fow_urukhai_veteranspearman": "fow_urukhai_veteranpikeman", "fow_urukhai_skirmisher": "fow_urukhai_arbalest",
  "fow_urukhai_arbalest": "fow_urukhai_crossbowman", "fow_urukhai_crossbowman": "fow_urukhai_veterancrossbowman" },
"commanders": { "every": 4,
  "ranks": [
    { "rank": "captain", "name": "{=TAOM_LD_Rank_Captain}Captain", "troop": "lord_I3_1", "bounty": 150, "health": 2.0, "abilities": [ "war_cry" ] },
    { "rank": "lieutenant", "name": "{=TAOM_LD_Rank_Lieutenant}Lieutenant", "troop": "lord_I1_1", "bounty": 250, "health": 3.0, "abilities": [ "war_cry", "summon_uruks" ] },
    { "rank": "general", "name": "{=TAOM_LD_Rank_General}General", "troop": "lord_I4_1", "bounty": 400, "health": 4.0, "abilities": [ "war_cry", "summon_uruks" ] } ],
  "beyond": { "healthPerStep": 0.25, "cooldownFactorPerStep": 0.9, "summonPerStep": 5, "maxSteps": 10 },
  "abilities": {
    "war_cry": { "kind": "WarCry", "name": "{=TAOM_LD_Ability_WarCry_Isengard}The White Hand Calls",
      "firstSeconds": 20, "cooldownSeconds": 60, "durationSeconds": 15, "swingSpeedPercent": 15, "moveSpeedPercent": 10 },
    "summon_uruks": { "kind": "Summon", "name": "{=TAOM_LD_Ability_Summon_Isengard}Uruk-hai of Isengard",
      "firstSeconds": 30, "cooldownSeconds": 120, "count": 20,
      "mix": [ { "troop": "fow_urukhai_veteraninfantry", "class": "Infantry", "weight": 3 },
               { "troop": "fow_urukhai_veterancrossbowman", "class": "Ranged", "weight": 1 } ] } } }
```

`bounties` gains the four troops a tier step or a summon can reach and the table lacks: `fow_urukhai_veteranspearman`, `fow_urukhai_veteranpikeman`, `fow_urukhai_veteraninfantry`, `fow_urukhai_veterancrossbowman`. `fow_urukhai_pikeman` has no link into it and stays out. Levels from `fords_troops.xml`: each link goes up a level (11, 16, 21, 26, 31). A rank `health` is the commander's own factor (finding 12), multiplied with the endless factor; the numbers are first guesses (A15).

**`affixes.json`** (new). Mordor names are the approved plan's; the Isengard names are proposals (assumption A10). The eight power ids are fixed in code; numbers and names are data.

```json
{ "schedule": [ { "fromWave": 5, "count": 1 }, { "fromWave": 10, "count": 2 }, { "fromWave": 15, "count": 3 } ],
  "powers": {
    "lash": { "swingSpeedPercent": 15, "attackEagernessPercent": 25 },
    "drums": { "moveSpeedPercent": 12, "accelerationPercent": 25 },
    "eye": { "aimErrorPercent": -30, "drawSpeedPercent": 15, "reloadSpeedPercent": 15, "missileSpeedPercent": 10 },
    "discipline": { "blockAbilityPercent": 25, "parryAbilityPercent": 25 },
    "hide": { "armourPercent": 30 },
    "hammer": { "engineHitPointsPercent": 50 },
    "terror": { "radius": 8.0, "pulseSeconds": 2.0, "amount": 3.0, "floor": 35.0 },
    "blades": { "poisonPerSecond": 2.0, "poisonSeconds": 6.0 } },
  "names": {
    "mordor": { "lash": "{=TAOM_LD_Affix_Mordor_Lash}The Overseer's Lash", "drums": "{=TAOM_LD_Affix_Mordor_Drums}Drums in the Deep",
      "blades": "{=TAOM_LD_Affix_Mordor_Blades}Morgul Blades", "eye": "{=TAOM_LD_Affix_Mordor_Eye}The Lidless Eye",
      "discipline": "{=TAOM_LD_Affix_Mordor_Discipline}Iron Discipline", "hide": "{=TAOM_LD_Affix_Mordor_Hide}Troll-Hide",
      "terror": "{=TAOM_LD_Affix_Mordor_Terror}Winged Terror", "hammer": "{=TAOM_LD_Affix_Mordor_Hammer}Hammer of the Underworld" },
    "isengard": { "lash": "{=TAOM_LD_Affix_Isengard_Lash}The White Hand's Whip", "drums": "{=TAOM_LD_Affix_Isengard_Drums}War Drums of Orthanc",
      "blades": "{=TAOM_LD_Affix_Isengard_Blades}Fouled Blades", "eye": "{=TAOM_LD_Affix_Isengard_Eye}The Seeing Stone",
      "discipline": "{=TAOM_LD_Affix_Isengard_Discipline}Bred for War", "hide": "{=TAOM_LD_Affix_Isengard_Hide}Orthanc Plate",
      "terror": "{=TAOM_LD_Affix_Isengard_Terror}Crebain from Dunland", "hammer": "{=TAOM_LD_Affix_Isengard_Hammer}Engines of Orthanc" } } }
```

**`boons.json`** (new): `legendWeight` (an integer, shipped 1, against 10 for every other row), then rows `{ "id", "category" (class, infantry, archers, cavalry, fortress, warchest, legend), "class" (class rows only), "name", "text", "effects": [ { "key", "value", "target" } ], "drawback": [ ... ] }`. Shipped pool: at least 3 rows per class, per troop category, for fortress and for war chest, and 2 Legend rows, so an offer can show 3 (assumption A11). `name` and `text` are `{=TAOM_LD_Boon_<Id>}` and `{=TAOM_LD_Boon_<Id>_Text}`. No name or text comes from the design reference.

**`classes.json`** (new):

```json
{ "classes": [
  { "id": "sword_and_board", "name": "{=TAOM_LD_Class_SwordBoard}Sword and Board", "mounted": false,
    "kits": { "vlandia": { "Item0": "wm_rohan_ws_sword_a02", "Item1": "wm_rohan_shield_a03_gsg", "Item2": "", "Item3": "" } } },
  { "id": "two_handed", "name": "{=TAOM_LD_Class_TwoHanded}Two Handed Infantry", "mounted": false, "kits": { "vlandia": { } } },
  { "id": "archer", "name": "{=TAOM_LD_Class_Archer}Archer", "mounted": false,
    "kits": { "vlandia": { "Item0": "ladder_rohan_bow_t3", "Item1": "bodkin_arrows_a", "Item2": "bodkin_arrows_b", "Item3": "sturgia_sword_2_t3" } } },
  { "id": "cavalry", "name": "{=TAOM_LD_Class_Cavalry}Cavalry", "mounted": true,
    "kits": { "vlandia": { "Item0": "wm_rohan_ws_spear_a03", "Item1": "wm_rohan_shield_a02_ggw", "Item2": "wm_rohan_ws_sword_a01",
      "Horse": "t3_empire_horse", "HorseHarness": "rohan_horse_armor_one" } } } ] }
```

These item ids come from Rohan troops in `fords_troops.xml` (`fow_rohan_edoras_golden_hall_eorlingas_rider`, `fow_rohan_eastfold_bowman`, `fow_rohan_eastemnet_eorlingas_lancer`). **No Rohan troop there carries a two-handed sword, axe or mace**; Rohan troops do carry two-handed polearm spears (`wm_rohan_spear_b`, `wm_rohan_ws_spear_a01` to `a03`), and the Armory holds `wm_rohan_ws_axe_2h_a01` in `LOTRLOME_items/LOTRAOM_weapons.xml`, carried by no troop. The two-handed kit waits for decision D5; the item that ships either passes the own-gear test or sits in its exception list with its source.

**`fortresses.json`:** an optional `sally` point in the `muster` point's shape (`x`, `y`, `z`, `fx`, `fy`; a look length outside 0.5 to 1.5 refused).

| New field | Rule | On failure |
|---|---|---|
| `healthPerWave`, `damagePerWave` | 0 to 1, finite | default |
| `maxFactor` | 1 to 10 | default |
| `tierEveryWaves` | 1 to 50 | default |
| Tier link | both troops known, same culture and `class`, higher level, no cycle | link skipped |
| `every` | 2 to 20 | block skipped, no commanders |
| Rank `troop` | a lord of the roster's `attackerCulture` | rank skipped |
| Ability seconds | 1 to 3600, finite; `durationSeconds` below `cooldownSeconds` | ability skipped |
| Summon `count` | 1 to 50; mix as a wave mix | ability skipped |
| Percentages | -90 to 300, finite | key skipped |
| Schedule | `fromWave` rising, `count` 0 to 8 | schedule default |
| Affix names | a table for each fortress `attackerCulture`, all eight powers | power ids shown as names, one warning |
| `terror.floor` | 0 to 100 | default |
| Boon `key` | in the section 2 vocabulary | effect skipped; a boon left with none skipped |
| Boon `id` | unique | later one skipped |
| Kit slot | `Item0` to `Item3`, `Head`, `Body`, `Leg`, `Gloves`, `Cape`, `Horse`, `HorseHarness` | slot skipped |
| Kit slot item | fits the slot (`Equipment.IsItemFitsToSlot`), checked at launch | slot skipped, one warning |
| Mounted kit | a rideable `Horse` and a `HorseHarness`: TAOM policy (`docs/features/war-ram.md:356`), not an engine need | class skipped |
| `sally` | as `muster`; at boot also a navmesh and inside the mission boundary, as is the home point | block skipped; Cavalry falls back to mounted from `Begin` |

**Shipped-data tests** (`LongDefeatShippedConfigTests` grows):
- Every tier and summon troop exists in `fords_troops.xml` with the roster's `attackerCulture`, compared after stripping the `Culture.` prefix: a Mordor roster can summon only `Culture.mordor` troops, such as `fow_mordor_uruk_*` (the own-troops rule). A commander troop is found in `characters/lords.xml` or as a stub in `characters/custom_battle_lords.xml` (which `lords.xslt` rebuilds), with that culture; the stub path is how `lord_1_63` passes.
- Every reachable attacker troop has a bounty.
- Every kit item is carried by a troop of that kit's culture in `fords_troops.xml` (the own-gear rule), or sits in a named exception list with its source troop. At runtime an unknown item spawns the lord in his own gear, with one warning.
- Every `{=TAOM_LD_...}` key in the six files has a strings row.
- A `FromTexts` fixture of a synthetic Mordor roster with Gorbag (`lord_1_63`) summoning 20 `fow_mordor_uruk_*` passes, and the same fixture with an Uruk-hai summon is refused. Gorbag's real `commanders` row ships with the Mordor roster in Phase 5.
- `RunPlanTroops.AllTroopIds` over the shipped plan holds every tier target, summon troop and commander, so the launcher's `MBObjectManager` check (`LongDefeatMissionLauncher.cs:63`, `Troops` at `:122-126`) refuses a missing one before the run, instead of `SpawnBatch` spawning 0 after the domain has dequeued them.

---

## 4. Engine seams

Both adapter interfaces hold no TaleWorlds type (`ServiceSignatureGateTests`), and every `Ld*` type they use lives in `Domain/` (`LongDefeatDomainPurityTests`). `LongDefeatBindingTests` pins every member below, plus `FixedEquipment` and `IsUnique` on a hero spawn, `GetEquipmentIndexFromOldEquipmentIndexName`, `IsItemFitsToSlot` and `IsPositionInsideBoundaries`.

**`ILongDefeatBattlefieldAdapter`, edited**

| Member | Wraps | Evidence |
|---|---|---|
| `SpawnBatch(batch, attackers)` | As Phase 1, then registers each agent with its role and writes `BaseHealthLimit`, `HealthLimit`, `Health` times `HealthFactor`, for attackers and defenders alike | `Agent.cs:911`, `:915`; `Mission.cs:4607-4609` |
| `SpawnPlayer(LdLoadout, LdPlace)` | `GetAgentBuildDataToSpawnTroop` with `isPlayerSide: true`, the `PlayerTroop`, `spawnWithHorse: false`, and with a place, its position **and** direction; then `.Equipment(kit)` and `.NoHorses(!mounted)`; then the player health factor | `Mission.cs:4461`, `:4468`, `:4495-4498`, `:4504-4507`, `:4108`, `:4109-4112`, `:4227`, `:4311`, `:4318`; `AgentBuildData.cs:232`, `:250` |
| `TeleportPlayer(LdPlace)` | `TeleportToPosition`, then `LookDirection` from the place's facing | `Agent.cs:4287-4299`, `:1493-1495` |
| `RemountPlayer(LdPlace, healthShare)` | The one-step remount of section 2 | `Agent.cs:4304-4310`, `:5206`, `:1255`; risk 2 |
| `SallyPointUsable(LdPlace)` | `Scene.GetNavigationMeshForPosition` and `Mission.IsPositionInsideBoundaries` | Phase 1 `SetMusterFrame`; `Mission.cs:1667` |
| `Fell(count)` | Marks each felled agent in the registry, then kills. With a main agent, `KillAgentCheat` as in Phase 1. With none, the same blow as `KillAgentCheat` (a blunt blow, its debug collision data, `agent.RegisterBlow`) with a **standing defender** as owner, so `Agent.Die` never swaps in a stale hitter | `Mission.cs:6216-6254`; `Agent.cs:4689-4693` |
| `Classify(affected, loss)` | Phase 1, plus the registry's role and felled mark; a commander for any removal | `LongDefeatBattlefieldAdapter.Diagnostics.cs:147-163` |

A commander spawns through the queue as a one-soldier trickle batch. The engine marks a hero `IsUnique` (`Mission.cs:4520-4522`) and fixes his first battle set (`:4284-4286`, `:4108`); TAOM race abilities rate him at `HeroFactor`.

**`ILongDefeatEffectsAdapter`, new**

| Member | Wraps | Evidence |
|---|---|---|
| `PublishStats(LdStatTable)` | One volatile write into each static hook; the table holds fresh `RaceAbilityEffects` copies that nothing else keeps, so publish-then-never-mutate holds by construction | `FordsUpgradeHooks.Set` |
| `RefreshStats(LdRefresh)` | `agent.UpdateAgentProperties(); agent.MountAgent?.UpdateAgentProperties();` on the standing attackers, a defender group, or the main agent. Logs `n` and `ms`; above 2 ms it spreads over ticks, 50 agents a tick | `Agent.cs:3692-3699`; `MissionAbilityExecutionContext.cs:129-131` |
| `ScaleHealth(LdRefresh, factor)` | The three health writes on those standing, by one boon's own factor | as `SpawnBatch` |
| `PulseMorale(radius, amount, floor)` | Reads every standing attacker's position once into an array, then for each standing defender within `radius`: `ChangeMorale(-min(amount, morale - floor))` when `GetMorale() > floor`; counts `drained` and `pushed_below` (before and after, inside the call) | `AgentComponentExtensions.cs:7-27`; `CommonAIComponent.cs:47` |
| `ApplyPoison(victim, poisoner, damage)` | Resolves both keys through the registry; reads `IsActive()` right here; `Health -=` while health stays above the damage, else `Agent.Die` with the owner rule below | `Agent.cs:4678`, `:4689-4693` |
| `SetEngineHitPoints(factor)` | Each live ram and tower: `MaxHitPoint` to vanilla times the factor, `HitPoint` scaled by the same ratio; logs the gate's destroyed state | `DestructableComponent.cs:66`, `:107-126` |
| `ReadBack(LdProbe)` | `GetAgentDrivenPropertyValue` on one sampled agent of a role: a roster attacker for `scale`, a soldier with no live race ability for the stat checks | `Agent.cs:2995` |

**Hooks and models**
- `LongDefeatStatHooks.UpdateStats` runs after `RaceAbilityHooks.ApplyStats` in `TaomCustomBattleAgentStatCalculateModel.UpdateAgentStats`. It resolves `body = agent.IsMount ? agent.RiderAgent : agent` and returns on a null body (a riderless horse, whose own `Character` is null), as `RaceAbilityHooks.ApplyStats` does; it takes the side from `body.Team`, the group from `body.Character`, the player test from `body.IsMainAgent`, never `Formation`; then `ApplyMount` for a horse, `Apply` otherwise. Any thread: it reads one volatile reference and compares `agent.Mission` with the armed mission.
- `LongDefeatStatHooks.InitializeArmour` runs after `base.InitializeAgentStats`: `if (agentBuildData == null || !agent.IsHuman) return;`, then the side from `agentBuildData.AgentTeam`, and for an attacker of a `hide` wave the four armour values are multiplied. The model is global to every Custom Battle, so the null guard protects `SpawnMonster` spawns elsewhere.
- `LongDefeatCombatHooks.Amplify` and `.Reduce` scale a blow by the attacker's wave damage factor and the defenders' boon factors; they never build a blow.
- **The hit path:** `LongDefeatMissionLogic.OnScoreHit` (it carries `isBlocked` and `damagedHp`; `OnAgentHit` has neither) drops the hit at once unless the director's volatile `WantsHits` is set (a `blades` wave, or a sally wave), the affector is not null, the affector is not the victim, the hit was not blocked and `damagedHp > 0`. A mount affector maps to its rider. Only then does the director take both keys from the registry, under its lock, on the calling thread, and park the call through `RunOrDefer`. A key the registry no longer holds drops the hit.
- **Arming:** the logic's `AfterStart` arms `LongDefeatMorale`, `LongDefeatStatHooks` and `LongDefeatCombatHooks`; `OnEndMission` and `OnRemoveBehavior` disarm all three.

### Apply order

`LongDefeatDirector.Apply` today spawns defender batches, the player, then attacker batches, then the tactic and the rest (`LongDefeatDirector.Effects.cs:38-43`). Phase 2 runs `PublishStats` and `EngineHitPoints` **first**, before any batch, because `StartWave` and the wave's first attacker batch arrive in one `Apply` (`SurvivalRun.cs:277`, then `:300`) and armour is written only at spawn. Refreshes run last. A director test pins this order with a `hide` wave whose first batch must read armoured.

### A kill must have a living affector (coordinator, 2026-11-05)

`AgentMoraleInteractionLogic.OnAgentRemoved` calls `CalculateMaxMoraleChangeDueToAgentIncapacitated` for every killed or unconscious human (`AgentMoraleInteractionLogic.cs:42-46`), and `CustomBattleMoraleModel` reads `Formation formation2 = affectorAgent.Formation;` with no null test (`CustomBattleMoraleModel.cs:42`). The launcher adds that logic (`LongDefeatMissionLauncher.cs:115`). A kill whose affector reaches the engine as null throws there.

- **Poison is Phase 2's only new kill path.** Summons, war cries, affix stats, armour, morale pulses, engine hit points and boons kill nothing; the damage factors scale blows the engine owns. `RemountPlayer` is a removal, not a kill, and its line fails if the old body is reported killed (risk 2).
- **Fords' recipe is not copied as it is:** `FordsBattlefieldAdapter.ApplyPoison` passes owner `-1` when the shooter is gone, and `Agent.Die` replaces `-1` or the victim's own index with the last hitter only when that hit is under 5 s old (`Agent.cs:4689-4693`). That hitter's index may since belong to another agent, because indices recycle.
- **The poison owner:** the poisoner when the registry still holds him and he is active, else the commander on the same test, else any human attacker the registry holds as active. With none, the killing tick is withheld and the victim keeps 1 health until a later tick or the clear. A pure `PoisonOwner.Choose` returns a key or "withhold"; its test covers all four cases, never `-1`.
- **`Fell`** stops relying on `KillAgentCheat`'s victim fallback: with no main agent it uses a standing defender as owner (table above). That defender gains a kill count and the defenders a morale gain, which a felled straggler would give the player's body anyway.
- **Gates:** `LongDefeatBlowOwnerTests` reads the Long Defeat source and fails on `new Blow(-1)`, or on any `Agent.Die`, `KillAgentCheat` or `new Blow(` outside `ApplyPoison` and `Fell`. It also pins that `Fell` passes a standing defender when there is no main agent. In game, `ApplyPoison` counts `owner_missing` (an owner not active at the call) and `withheld`, and the `blades` check fails above 0 missing.

---

## 5. UI

Every text is a `TextObject("{=TAOM_LD_...}...")` in `LongDefeatHudText`. No new key: `B`, `H` and `M` keep their Phase 1 jobs. **Sprites and brushes:** only values `FordsCommand.xml` already uses, which `LongDefeatPrefabTests.EverySpriteAndBrush_IsOneTheFordsPanelAlreadyUses_SoNoneIsInvented` enforces (`:145`); in practice `BlankWhiteSquare_9` plus colour. `LongDefeatBoonVM` and `LongDefeatAffixVM` join that test's type list (`:56`), and a new test checks that every `Command.Click` and `@` binding in both prefabs names a member of its VM, because a wrong command name fails silently.

**HUD strip:** the class after the wave number. During a wave, `@CommanderText` ("Captain Lugdush: war cry in 12 s", "Captain Lugdush has fallen") and `@AffixText`. Between waves, `@AffixText` names the next wave's cut affixes and, past the authored list, its health and damage; the hint line adds "A boon awaits: press B" while a pick is open.

**Panel:** a boon group `@IsPickVisible` above the shop: three `LongDefeatBoonVM` cards (name, category, effect line, drawback line in its own colour, `ExecutePick`), and a reroll button with `ExecuteReroll`, `@RerollText` ("Reroll (2)") and `IsEnabled="@CanReroll"`. The end group gains the class, the boons taken and the commanders killed.

**Setup prompt:** after the fortress, a second `Choose`, "Choose your class", one row per class with a one-line kit text. Cavalry stays off the list until Slice 11, or until its fallback is chosen.

**The panel never opens by itself for a pick** (D2): a notice and the hint announce it, since a focus layer would stop the player's movement.

---

## 6. Unattended path

**New arm keys:** `class`, `seed` (0 to 2^63-1), `start_wave` (1 to 100: the run begins at that wave number; `wavesCleared` still starts at 0, so `start_wave=12` with `waves=4` stops after 4 clears), `start_rerolls` (0 to 3). The last three are test options the report records; a board run needs their defaults.

**The bot:** class from `class`, else from the seed; `ShopBot.Decide` answers an open pick before it buys. A Cavalry main agent is AI-driven like the others. **He may not ride home through the attackers:** a closed gate disables its navmesh for the defenders (`CastleGate.cs:596-602`), so an AI lord outside may have no path to his formation and hold outside until the home teleport (D6). The sally line logs his distance to his formation and the gate state.

**`tools/run_long_defeat.py`:**
- Flags `--class`, `--seed`, `--start-wave`, `--start-rerolls`, `--deep`. The wrapper arms `start_rerolls=1` by default, so a 5-wave run proves a reroll at clear 3.
- `--class all` keeps a run counter inside the arm-text closure, so each of `--runs 4` gets the next class.
- The judge takes its required list from the report's `class` and from the done file's `expect`, which the wrapper arms as `deep` for `--deep`.
- **Mechanics runs** (Slices 4, 7 and 13) pass `--garrison-scale 1.5`, a test option the report already stores (D8), so a bot loss at wave 4 does not hide the `affix` check.
- **Standard run** (`--waves 5`): `REQUIRED_CHECKS` gains `class`, `boon`, `reroll`, `commander`; `affix` is required when `wavesCleared` is 4 or more.
- **Deep run** (`--deep` is `--start-wave 12 --waves 4`): waves 12 to 15 bring a general, two then three affixes and the endless factors. The judge accepts `final` or `lost_*` and requires `boot, general, class, commander, affix, scale, end`. Rule 4 of the leaderboard ("numbered from 1") does not apply to such a test run.
- **Cavalry run** (`--class cavalry`): also requires `sally` and `sally-return`; `sally-contact` is reported, not required.
- The judge fails a report whose `schema` is not 2, and any `owner_missing=` above 0.
- **Verdict:** `python tools/ingame_verdict.py --issue 1079 --sessions 12` after Slice 13: `--sessions 4` would read only the newest logs, and the rows only the deep and Cavalry runs write would read NOT REACHED. Sessions built before `fixed_in` never override a pass, so the larger count is safe.

**Manifest `tools/ingame_checks/1079.toml`:** each row lands with its emitter, `absence = "not-reached"` unless noted. Every keyed row's pass pattern is generic (`result=pass `), with its key (`w=`, `power=`, `p=`) in the detail, never one pattern per power or wave.

| Check id | Proves | Key |
|---|---|---|
| `taom.long-defeat.class` | The lord spawned with every kit item read back, horse slots cleared for a foot class, mounted only for Cavalry; `absence = "fail"` after `boot` | mission |
| `taom.long-defeat.scale` | Past the authored list: a sampled **roster** attacker's `HealthLimit` over its character maximum equals the factor within 1%, read at spawn and again late in the wave; tier substitutions as planned; attackers standing never above live room (at least 101) | `w<N>` |
| `taom.long-defeat.affix` | The roll matches the schedule and the cut, distinct, names from the attacker culture's table | `w<N>` |
| `taom.long-defeat.affix-effect` | Per power: a sampled attacker with no live race ability against the expected factor (stat powers); the armour ratio (`hide`); the engine hit point ratio and the gate state (`hammer`, proves the write); `drained` and `pushed_below=0` from the pulse itself (`terror`); doses, hits, damage, `owner_missing=0`, `withheld` (`blades`) | `<power>` |
| `taom.long-defeat.commander` | He spawned at his rank, steps and health; his abilities fired; no use after his end; the outcome | `w<N>` |
| `taom.long-defeat.summon` | Each summon queued its count and every queued soldier spawned before the clear, all of the roster's culture | `w<N>s<k>` |
| `taom.long-defeat.boon` | Three distinct eligible cards; the pick applied (a rule, or a read-back on a standing soldier and on one spawned after) | `p<N>` |
| `taom.long-defeat.reroll` | The bank fell by one; three new cards, none repeated when the pool allowed | `p<N>` |
| `taom.long-defeat.sally` | Mounted within 2 m of the sally point at wave start, `HasMount` true, the read-back distance logged, no player death counted | `w<N>` |
| `taom.long-defeat.sally-return` | At the clear the lord stands within the keep radius of the home point; no player death counted | `w<N>` |
| `taom.long-defeat.sally-contact` | At least one hit in the wave's first 180 s within 40 m of the sally point, counting hits by or on the lord **and his horse** (`affected` is the lord or his mount; `affector` is the lord, or a mount whose rider is the lord), mount hits in their own field | `w<N>` |

**New `[[human]]` entries:** the class kit looks right on the lord; a boon card reads clearly; the commander is visible among the attackers; Winged Terror does not rout the walls by itself; the Cavalry ride outside the walls, the attackers react (`FellFoes` makes up to 8 attacker archers within 20 m hold the rider as their target, `FellFoesAdapter.cs:166-176`, so judge the infantry too), and the return feels right.

---

## 7. The run report and the leaderboard

**Phase 2 changes the arithmetic of six schema 1 rules,** so it cannot stay schema 1 silently:
- Rule 2: `balance` must cover the three new files, or a boon edit would not open a new board.
- Rule 3: the new test options must be at their defaults.
- Rules 8 to 11: war chest boons scale the bounty, the wave bonus and the horn; a commander kill pays its own bounty; a `gold` boon adds a source; tier steps and summons bring troops whose bounties lie outside the last authored mix.

**How schema 1 readers stay unbroken:**
1. **Additive only.** Every schema 1 field keeps its name, type and meaning; the end panel reads the same fields.
2. **The roster arithmetic is kept.** `waves[].kills`, `waves[].felled` and the top-level `kills` count roster soldiers only, booked by identity (section 2, "Who fell"). Rules 4 to 7 hold on a schema 2 report as written; rules 12 to 14 are time and uniqueness rules and read nothing new. Rule 4's "numbered from 1" rests on rule 3 (`startWave` 1).
3. **`schema` becomes 2** in Slice 1, once, with every new field present at its empty value. A schema 1 reader refuses the file at its rule 1, which names itself, instead of judging a Phase 2 run by rules it cannot satisfy: the job the plan gives `schema` ("Picks the rule set").
4. **A schema 2 reader is a schema 1 reader plus the amendments below,** one code path.

Folding the fields into schema 1, as `maxSpeed` was, is decision D9.

**New fields:**

| Field | Meaning |
|---|---|
| `seed` | The run's seed, a decimal `ulong` |
| `class` | The class id |
| `startWave`, `startRerolls` | Test options; 1 and 0 on a board run |
| `boons[]` | Per pick in order: `pick`, `wave` offered, `offered` (each draw's ids, rerolls included), `taken`, `takenBeforeWave` |
| `rerolls` | `earned`, `used`, `banked` |
| `waves[].affixes` | The cut power ids |
| `waves[].healthFactor`, `waves[].damageFactor` | 1 inside the authored list |
| `waves[].summoned`, `waves[].summonKills`, `waves[].summonFelled` | Summon counts |
| `waves[].commander` | `null`, or `troop`, `rank`, `steps`, `outcome` (`killed`, `felled`, `left`, `standing`), `uses` per ability |
| `gold.commander`, `gold.boon` | The two new sources |
| `poison` | `doses`, `hits`, `damage`, `ownerMissing`, `withheld` |

**`balance`** stays in `LongDefeatConfigCatalog.HashOf` (`:55-60`, filled at `:39`): it joins `fortresses.json`, `waves.json`, `shop.json`, `affixes.json`, `boons.json`, `classes.json` in that order with the same separator and line-ending rule, and a missing file joins as empty text. `FromTexts` grows to six texts. The report keeps copying `RunProvenance.Balance`, so there is one hasher.

**"Schema 2" section for `leaderboard-plan.md`** (Slice 1 writes it; each later slice adds its rule):
- Rule 2: six files.
- Rule 3: also `startWave` 1 and `startRerolls` 0.
- Rule 8: `earned` is the sum of five sources.
- Rule 9: each wave bonus times the wave-bonus factor in force at that clear, recomputed from `boons[]`.
- Rule 10: the cap times the horn factor.
- Rule 11: the band over roster and summon kills, taken over the tier-substituted mixes and the summon mixes of the waves played, times the bounty factor in force.
- New rule 15: replay `seed` with the derivation of section 2. Each row's `affixes` is the prefix of that wave's roll, its length from the boons taken before the wave began; each pick's `offered` equals the draws given the boons taken before it; `taken` is one of the last three offered; `rerolls.used` is at most `earned`, with the bank capped at 3.
- New rule 16: `waves[].commander` present exactly at multiples of `every`, with the configured troop and rank.
- New rule 17: `summonKills + summonFelled == summoned` on every cleared row.
- New rule 18: `poison.ownerMissing` is 0 (a flag, not a refusal).
- "What these rules cannot see" gains: whether the player picked the card the report names (the end-panel clip shows the boons), and the Cavalry remount, which the rules cannot see at all.

The C# and Python sides share pinned vectors (section 2), so the bot's replay cannot drift from the game while both suites stay green.

---

## 8. Slices

Builders cannot invoke skills or spawn agents. They iterate with `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~LongDefeat`, adding `|FullyQualifiedName~ConsoleCommandBinding` when a command changes; `|FullyQualifiedName~CustomBattle|FullyQualifiedName~RaceAbilitiesWiring|FullyQualifiedName~CreatureSiegeRole|FullyQualifiedName~CareerTroopHooksWiring|FullyQualifiedName~MountChargeDamage` when a shared model changes; `|FullyQualifiedName~GateTests` when an adapter or entry point changes; and `python -m unittest tools.tests.test_run_long_defeat tools.tests.test_ingame_verdict` when the wrapper or manifest changes. Filtered runs are for iterating; each slice ends with the full suite green. Never `./build.ps1`, no commits. Signatures from `pwsh tools/taom-src.ps1 path <Type>`, one type per call. Before a shared model: `python tools/graphify_taom.py refresh --if-stale`, then `python tools/graphify_taom.py affected "<Model>" --depth 2`. Game runs are main-session only, under the `/game-run` gate. Each slice leaves the game playable as before plus its one mechanic.

Order: pure domain and data first, then the shared models (both lines of the stat model in one slice), then new spawns, then the sally.

| # | Tier | Slice | Tests first | In-game check (main session) |
|---|---|---|---|---|
| 1 | implementer | Foundations | `RunRandomTests` (pinned `For` vectors), `RunOptionsTests` (`start_wave=12, waves=4` stops after 4 clears), autorun key tests (seed range, negative refused), `LongDefeatConfigCatalogTests` (six names, two missing), `LongDefeatRunReportTests` (schema 2 at empty values), `LongDefeatBlowOwnerTests` (source gate), `test_run_long_defeat.py` (flags, class rotation, `expect=deep` judge, schema 2, shared vectors) | `--start-wave 3 --waves 1`: wave 3 first, `schema` 2, `seed`, `startWave` 3 |
| 2 | implementer | Endless scale | `WavePlanTests` (factors at 1, 5 and the cap; tiers at steps 0 to 3; merge; chain end; `Expand(wave)` unchanged after the `Expand(count, mix)` split), catalog rows (culture after the prefix), shipped tiers and the four veteran bounties, `RunPlanTroopsTests`, `LongDefeatCombatHooksTests` | `--start-wave 11 --waves 2`: `scale` pass from a roster sample |
| 3 | implementer | Affixes: roll, the four stat powers and `hide` | `AffixPlanTests` (schedule, cut, distinct, pinned rolls), `RunModifiersTests` (fresh `RaceAbilityEffects` per publish; mounted rows copy move speed), `LongDefeatStatHooksTests` (inert unarmed; rider resolved for a mount; riderless mount untouched; armour: null build data, a horse, a defender untouched), director apply-order test | `--start-wave 5 --waves 1 --seed N`: `affix`, `affix-effect` for the rolled power; a seed giving `hide` |
| 4 | implementer | Boons A: picks, rerolls, war chest, fortress, `affixCount` | `BoonDeckTests` (owed at 3, 6, 9; earned at 5, 10, 15; cap 3; the weighted draw against pinned vectors; reroll exclusion and its fallback; troop pool eligibility), `GoldFactors`, `EffectiveRules`, `ShopBotTests`, session pick and reroll, VM and binding-name tests | `--waves 5 --garrison-scale 1.5`: `boon`, `reroll` at clear 3; `boons[]`, `gold.boon` |
| 5 | implementer | Foot classes | `ClassPlanTests`, kit reader (`Item0` maps to `Weapon0`, a misfit refused), own-gear test, prompt and command tests, director (kit at begin and respawn), adapter pins (`.Equipment` before spawn, `NoHorses(true)`, position always with direction, `isPlayerSide` with the `PlayerTroop`) | `--class archer --waves 2`: `class` pass; Mike clicks the second question once |
| 6 | implementer | Boons B: troop and class boons | `RunModifiersTests` (groups by `IsMounted` and `IsRanged`, player record only), director (takeover refreshes the new body and writes the player health; a pack bought after a health pick spawns with it; a respawn too; a second health boon multiplies only by its own factor) | `--waves 5 --seed N` whose first offer holds a troop boon: `boon` pass on a standing soldier and a later one |
| 7 | implementer | Commanders and summons | `CommanderPlanTests` (steps 0 at waves 4 and 8 with health 1 and the summon unchanged; 1 at 16; `validRanks`), `CommanderClockTests` (first delay, one fire per tick, nothing after `Stop`), `LongDefeatAgentRegistryTests`, `SurvivalRun` roles (felled by identity, a commander that leaves alive, stalemate count with summons), director (commander through the queue), Legend `commanderCooldownPercent` | `--waves 5 --garrison-scale 1.5`: `commander` w4; `--start-wave 8 --waves 1`: `summon` |
| 8 | implementer | Affixes: Hammer and Winged Terror | Director (hit points set at each wave start, factor 1 restores), `PulseMorale` arithmetic over the floor, `pushed_below` counted inside the call | Seeds giving `hammer` and `terror`: `affix-effect` pass; `[LongDefeat][Terror] pulse n= ms=` under 16 ms |
| 9 | implementer | Morgul Blades and the kill owner | `PoisonOwnerTests` (four cases, never -1), `SurvivalRun` (dose only in a blades wave; doses apart from hits; a forgotten key drops the hit), `LongDefeatMissionLogicTests` (`OnScoreHit` filter: null affector, self hit, blocked, zero damage, mount to rider; under 150 lines), `Fell` owner with no main agent | Seed giving `blades`: `affix-effect` pass, `owner_missing=0`; a `--lose` run that fells with the player dead throws nothing |
| 10 | implementer (architect on an unknown failure) | **S9, the sally spike** | Recorder `sally` kind, `sally` parser, `SallyPointUsable` (navmesh and boundary), director (out, home, teleport when mounted, one-step remount otherwise, health share kept, planned removal by identity for `Routed` and `Deleted`, no sally for a taken-over body, refresh after a mounted spawn) | Below |
| 11 | implementer | Cavalry class, or its fallback | Class row, kit, cavalry boons (move speed reaches the horse), prompt lists it, bot class choice includes it | `--class cavalry --waves 3`: `class`, `sally`, `sally-return` |
| 12 | fast-reader | Strings and docs | Strings tests, `lint_docs` | `python tools/lint_docs.py --fail-on-drift` clean |
| 13 | main session | Proof | none | Below |

**Files per slice** (each slice also adds its check id to `LongDefeatChecks`, its manifest row, its `REQUIRED_CHECKS` entry where section 6 says so, and its strings rows):

1. `Domain/RunRandom.cs`, `RunOptions.cs`, `Autorun/LongDefeatAutorunPlan.cs`, `Config/LongDefeatConfigCatalog.cs` (`HashOf` and `FromTexts` over six texts), `Diagnostics/LongDefeatRunReport.cs` (schema 2 fields), `tools/run_long_defeat.py`, `leaderboard-plan.md` ("Schema 2"), `TAOM.Tests/Features/LongDefeat/LongDefeatBlowOwnerTests.cs`.
2. `Domain/EndlessScale.cs`, `WavePlan.cs`, `WaveSpec.cs`, `RunEffects.cs`, `RunPlanTroops.cs`, `Config/WavesReader.cs`, `waves.json`, `Hooks/LongDefeatCombatHooks.cs`, `Hooks/LongDefeatMissionLauncher.cs` (one call), `TaomCustomBattleDamageModel.cs` (two lines), `SpawnBatch` health write.
3. `Domain/AffixPlan.cs`, `RunModifiers.cs`, `SurvivalRun.Affixes.cs`, `Config/AffixesReader.cs`, `affixes.json`, `Hooks/LongDefeatStatHooks.cs` (both methods), `TaomCustomBattleAgentStatCalculateModel.cs` (both lines), `ILongDefeatEffectsAdapter.cs` and `LongDefeatEffectsAdapter.cs` (`PublishStats`, `RefreshStats`, `ReadBack`), `LongDefeatDirector.Affixes.cs`, `LongDefeatDirector.Effects.cs` (apply order), HUD affix line.
4. `Domain/BoonPlan.cs`, `BoonDeck.cs`, `SurvivalRun.Boons.cs`, `Purse.cs`, `ShopBot.cs`, `Config/BoonsReader.cs`, `boons.json` (war chest, fortress, `affixCount` Legend rows), `LongDefeatSession.cs`, `LongDefeatBoonVM.cs`, `LongDefeatHudVM.cs` (`CanReroll`), panel group, `LongDefeatPrefabTests.cs`, the provenance row.
5. `Domain/ClassPlan.cs`, `Config/ClassesReader.cs`, `classes.json` (three foot classes), `LongDefeatSetup.cs`, `LongDefeatSetupPrompt.cs`, `LongDefeatCommands.cs`, `LongDefeatDirector.Player.cs`, `Adapters/LongDefeatBattlefieldAdapter.Player.cs` (`SpawnPlayer`).
6. `RunModifiers.cs`, `LongDefeatStatHooks.cs`, `LongDefeatCombatHooks.cs`, `ScaleHealth`, `SpawnBatch` (defender factor), `SpawnPlayer` (player factor), `LongDefeatDirector.cs` (`AdoptIfNew` emits `RefreshPlayer` and the health write), `boons.json` (class and troop rows).
7. `Domain/CommanderPlan.cs`, `CommanderClock.cs`, `SurvivalRun.Commander.cs`, `SurvivalRun.cs` (felled by identity, stalemate count), `RunStats.cs`, `RunPlanTroops.cs` (summons, commanders), `WavesReader.cs`, `waves.json` (`commanders`), `Adapters/LongDefeatAgentRegistry.cs`, `Classify`, `Fell` (marks), `LongDefeatDirector.Commander.cs`, `boons.json` (`commanderCooldownPercent` row), HUD commander line.
8. `SetEngineHitPoints`, `PulseMorale`, `LongDefeatDirector.Affixes.cs`.
9. `ApplyPoison`, `PoisonOwner`, `Fell` (owner with no main agent), `LongDefeatMissionLogic.OnScoreHit`, the director's hit path, `LongDefeatBlowOwnerTests.cs` (the `Fell` pin).
10. `LongDefeatRecorder.cs`, `LongDefeatCommands.cs` (`sally`), `FortressesReader.cs`, `LongDefeatBattlefieldAdapter.Player.cs` (`TeleportPlayer`, `RemountPlayer`, `SallyPointUsable`, the Cavalry formation), `LongDefeatDirector.Player.cs`, `LongDefeatMissionLogic.cs` (`OnAgentDeleted` log for the main body), `dev-console.md` row.
11. `classes.json` (Cavalry), `boons.json` (Cavalry class rows), prompt list.
12. Strings rows in `taom_module_strings.xml` and the 12 language files, `docs/features/long-defeat.md`, `docs/reference/feature-map.md`, `docs/features/dev-console.md`.

**Provenance (Slice 4, the first commit that builds boons):** one row between the register's start and end markers in `docs/reference/provenance-register.md`, in the shape of the `ServeAsSoldier` row (`:75`): Source "Achaea, help page The Trials of Mnemosyne" (https://www.achaea.com/game-help?id=1668), Tokens `Achaea` `Trials of Mnemosyne`, License `UNKNOWN`, Derivation `comparison-only`, Covers `Main/Features/LongDefeat/Domain/{BoonDeck,BoonPlan,SurvivalRun.Boons}.cs` (the idea of pick 1 of 3 with banked rerolls; names and text are TAOM's own), Status `uncleared`. `tools/check_provenance.py` checks only Derivation and Status, so its pass proves nothing about License or Covers. The Slice 4 reviewer compares the `boons.json` names and texts against the page before that check.

### Slice 10: S9, the sally spike

The first slice of the Cavalry work, and the only spike. Cavalry ships only after it passes, or with a fallback.

1. **Record (Mike):** in a Long Defeat mission, stand on the spot outside the walls, inside the play area, facing the way the sally should face; `taom.ld_record sally`, `taom.ld_export`, paste the `sally` block into `fortresses.json`. A courtyard point recorded the same way is the first fallback.
2. **Boot line:** `[LongDefeat][Sally] point (x, y, z) navmesh yes|no boundary yes|no`, and the same for the home point. A failing point: Cavalry rides from `Begin` with no sally.
3. **Run:** `run_long_defeat.py --class cavalry --waves 3` (the arm key reaches the catalog's Cavalry row while the prompt still hides it).
4. **Lines S9 must show:**
   - `[LongDefeat][Sally] w<N> out: teleport|remount main=<id> mount=<yes|no> at=(..) dist=<m> formation=<class> order_at=(..)`, then `[Check] sally`. The formation and its order position tell a formation that drags the rider home apart from a camera or horse fault (risk 3).
   - Every 5 s while he is more than 20 m outside the keep: `t=<s> hits_given=<n> hits_taken=<n> mount_hits=<n> kills=<n> nearest_attacker=<m> to_formation=<m> gate=<open|closed|broken> outside_boundary=<s>`, then `[Check] sally-contact`.
   - `[LongDefeat][Sally] w<N> home: teleport main=<id> at=(..)`, then `[Check] sally-return`.
   - For a remount: the old body's state read right after `FadeOut`, every `OnAgentRemoved` and `OnAgentDeleted` for it with its state, `MainAgent` after the step, and `deaths` unchanged. The line fails when the old body is reported `Killed` or `Unconscious`.
5. **Pass:** two runs with `sally` and `sally-return` passing on every wave, no counted player death from a planned removal, no boundary exit, no `CrashReport`, and Mike's by-hand ride (`[[human]]`).
6. **Fallback, decided by Mike from the lines:** the courtyard point when the outside point fails on contact, pathing or the boundary; "mounted from `Begin`, no sally" when teleport or remount itself misbehaves (camera, horse left behind, a counted death).

**Slice 13, in order:**
1. `/verify`, `/localize`, `/deep-review`.
2. Mike's word: commit as X.
3. Build the clean tree at X.
4. `run_long_defeat.py --waves 5 --runs 4 --class all --garrison-scale 1.5`, one `--deep`, one `--lose`, two `--class cavalry --waves 3`.
5. Mike plays one run per class he cares to judge, with F5 from the worktree's `TAOM.sln`.
6. Set `fixed_in = X`, remove `allow_dirty`, then `python tools/ingame_verdict.py --issue 1079 --sessions 12`.
7. On Mike's word, commit the manifest edit.

**Cuts, to keep Phase 2 to its scope:** ability kinds beyond Summon and War Cry; a summon placed beside its commander; boon tailoring and hidden combos; stacking boons; picks during a wave; a scripted ride for the AI lord; gate repair and tower posts (Phase 3); the armoury and a bought hero as general (Phase 4); Mordor waves and Gorbag's `commanders` row (Phase 5); translations (#763).

---

## 9. Risks not settled from source

| # | Risk | What settles it |
|---|---|---|
| 1 | A commander spawned mid-wave fights as a soldier: managed code shows no difference beyond `IsUnique` and his fixed set; native handling of `IsUnique` and ladder use is unread | `[LongDefeat][Commander] w4 at=(..) behavior=..` every 5 s, `[Check] commander` |
| 2 | Which callback a faded main agent raises, with which state, and when | The Slice 10 remount line. Static alternative: `python tools/native_decompile.py --rva 0x5F02B0` and `--rva 0x5EB010` (addresses from the plan check) |
| 3 | Teleporting a mounted main agent: camera, horse AI, his formation's order position | The `out` line's `formation` and `order_at`, the `home` line, Mike's ride |
| 4 | Attackers react to a lone rider outside the walls; Fell Foes archers make contact likelier | `[Check] sally-contact`, Mike's ride |
| 5 | **Closed from source.** Siege dismount reads `(troop as CharacterObject)?.HeroObject?.BattleEquipment` (`PlayerMountAdapter.cs:23-24`), null for a Custom Game troop, and acts only in `AfterStart` and `OnEndMission` | none owed |
| 6 | **Closed from source** by finding 5 | none owed |
| 7 | A refresh reaches agents and mounts already standing; a horse built before its rider takes the record only after `RefreshStats(player)` | `affix-effect` read-backs before and after a war cry; a mount read-back after a sally spawn |
| 8 | A null mount key (`MountKey` never set on the mounted path) builds a horse | `mount=yes` on the sally line; else set `MountKey` from the kit's horse |
| 9 | The engine never undoes the health written after spawn | `[Check] scale` read late in the wave as well as at spawn |
| 10 | The bot reaches wave 4 and 4 clears with commanders and affixes in play | Slice 7's own `--waves 5 --garrison-scale 1.5` run, before Slice 13 |
| 11 | Refreshing up to 220 defenders at a pick costs a visible frame | `[LongDefeat][Boon] refresh n= ms=`: under 2 ms in one tick, else spread at 50 a tick; under 16 ms passes |
| 13 | The boon names and texts copy nothing from the design reference | The Slice 4 reviewer compares `boons.json` against the page |
| 14 | A morale pulse over 220 defenders and 150 attackers costs a visible frame | `[LongDefeat][Terror] pulse n= ms=` under 16 ms; positions read once per pulse |
| 15 | `Fell` with the player dead: fixed by design in Slice 9 (a standing defender owns the blow), so no stale hitter and no null affector | A `--lose` run that fells with the player dead: no exception in `AgentMoraleInteractionLogic`, the kill counted to a defender |
| 16 | Summons from the attackers' spawn arrive after the wave is all but over | `[Check] summon` with `spawned_at=` against the wave's seconds |
| 17 | An AI rider strays outside the mission boundary for 10 s and ends the run | `outside_boundary=` on the sally line; the Slice 10 pass needs no boundary exit |
| 18 | Live TAOM race abilities skew read-backs: Uruk-hai `bloodlust` (swing and block, a morale drain on kill, `HeroFactor` on the commander) and Rohan `forth_eorlingas` on a mounted lord (per the plan check, `race_abilities.json`, `RaceAbilityActivator.cs:70`, `:76-78`) | Read-backs sample a soldier with no live race ability; `terror` counts only the pulse's own writes |

Risk 12 of the first plan (a two-handed weapon) is now decision D5.

**Checked and inert here** (per the plan check, not re-read for this revision): Signature Strikes passes its mission gate but registers no Isengard commander (`signature_strikes_config.json` lists `lord_1_17`, the `sauron` race and `nazgul_nine`); Hero Kill Counter needs a campaign; Mixed Formations and the Fell Foes straggler rule need a field battle; the race abilities' heal on kill clamps to the scaled `HealthLimit` (`RaceAbilityDeaths.cs:79`), so it agrees with the endless factor.

---

## 10. Decisions and assumptions for Mike

**Settled:** A1, the reroll rule. The design reference states one reroll per five completed floors, a bank of at most three, three options per offering, boons that last the run, and drawbacks on some; Phase 2 reads a floor as a cleared wave.

**Decisions, each with my recommendation:**

| # | Decision | Recommendation |
|---|---|---|
| D2 | An unmade pick: stays open into later intermissions and blocks nothing; the panel never opens by itself | As written |
| D3 | A remount builds a new body at full health with a full quiver: free healing for Cavalry each wave the horse died | Carry the old body's health share to the new one; accept the ammunition refill |
| D4 | Cavalry on foot between waves (the first plan's A7) conflicts with the home teleport, and dismounting would mean fading a horse, which crashes | Mounted all run, in the defenders' Cavalry formation, home at the recorded `muster.foot` point; never fade a horse |
| D5 | The two-handed kit: a Rohan two-handed spear troops carry (`wm_rohan_spear_b`), or the Armory axe `wm_rohan_ws_axe_2h_a01` that no troop carries | The axe, with an own-gear exception entry naming its file, since the class reads as axe or sword |
| D6 | The AI lord outside a closed gate may hold outside all wave | Accept it and rely on the home teleport; no scripted ride |
| D7 | Winged Terror's floor of 35 never routs alone but lowers headroom against casualty losses | Keep 35; raise it toward 50 if the walls rout in play |
| D8 | Mechanics proof runs use `--garrison-scale 1.5` | Yes, from Slice 4; board runs stay at defaults |
| D9 | Schema 2, or fold the fields into schema 1 as `maxSpeed` was | Schema 2: six rules change meaning, not just fields |
| D10 | Hammer stays in the roll at Helm's Deep, where it does little after the gate falls | Keep it; a roll must replay from seed and static data. A static per-fortress exclusion list is the alternative |
| D11 | Commander health: captain 2, lieutenant 3, general 4 times his base | First guesses, tuned in play |
| D12 | The main agent takes the `player` boon record only, never his troop group's | As written |
| D13 | A felled straggler with the player dead credits a standing defender with the kill and gives the defenders the morale | Accept it |

**Assumptions still open:**

| # | Assumption |
|---|---|
| A3 | Each boon is taken at most once |
| A4 | Two commander ability kinds, Summon and War Cry; War Cry reaches every attacker |
| A5 | Isengard's captain Lugdush (`lord_I3_1`), lieutenant Ugluk (`lord_I1_1`), general Lurtz (`lord_I4_1`), returning stronger from wave 16; Mauhur (`lord_I2_1`) spare; Saruman (`lord_I1_0`) no wave commander; the attackers' `GeneralAgent` unset |
| A6 | Summons march in from the attackers' spawn and count against live room; soldiers already summoned still come after his end; his end stops a running war cry |
| A8 | Cavalry troop boons reach every mounted-class troop, riders bought to fight on foot included |
| A9 | Endless health and damage grow linearly past the authored list (5% and 3% a wave, capped at 3x); tiers step every 3 extra waves |
| A10 | The Isengard affix names in section 3 |
| A11 | Boon pool sizes, row weight 10, `legendWeight` 1 |
| A13 | Test options (`seed`, `start_wave`, `start_rerolls`) live in the report; a board needs their defaults; the wrapper arms `start_rerolls=1` |
| A14 | Commander and summon kills pay bounties; a felled one pays nothing |
| A15 | Affix, tier and rank numbers are first guesses |

---

## 11. What Phases 3 to 5 need from Phase 2

- **Phase 3, fortifications:** the boon vocabulary and `EffectiveRules` take gate and tower levels as new keys, and the fortress category holds fortification boons. `SetEngineHitPoints` is the twin of a repair only for the number on a standing engine: a broken gate also needs `DestructableComponent.Reset` and the `CastleGate` state, plank, standing points and navmesh undone (`DestructableComponent.cs:278-307`, `CastleGate.cs:1042-1051`, per the plan check; UNVERIFIED for gate state). The trolls-and-repaired-gates note stays Phase 3's.
- **Phase 4, locations and your own gear:** `LdLoadout` and `SpawnPlayer(LdLoadout, LdPlace)` are the armoury's swap and the respawn in bought gear; the recorded point with its navmesh and boundary checks is the pattern for the armoury and the yard. A bought hero is marked in the registry, and making him general is a separate `Team.GeneralAgent` write whose effect on the siege team AI nobody has read.
- **Phase 5, Minas Tirith and content:** `affixes.json` already holds the Mordor names; the Mordor roster adds a `commanders` block with Gorbag (`lord_1_63`) summoning `Culture.mordor` troops, which the shipped-data rule already enforces through the stub path; difficulty can scale the endless factors and the affix schedule; the schema 2 report carries class, boons, affixes and commanders per wave for the balance pass.

---

## 12. Check findings not applied

Each verdict below was read against the engine line it cites; the reason it is not applied is given.

- **K3 (second reading), fill the two-handed kit with the Armory axe now:** the item exists (`LOTRAOM_weapons.xml`), but no troop carries it, so it bends the own-gear rule. That is Mike's call (D5), not a builder's.
- **E9 (first reading), take `blocked` from `AttackBlockedWithShield`:** a weapon parry would read as unblocked and could poison. `OnScoreHit` receives the engine's own `isBlocked` and `damagedHp` (`Mission.cs:5672`), so the hit path uses that instead.
- **A4 (first reading), leave Hammer out of the roll where no engine still stands:** a roll must replay from the seed and static data (rule 15); an engine-state filter breaks the replay. D10 keeps it, with a static per-fortress exclusion as the alternative.
- **F7a (first reading), route the attacker damage factor through `GetWeaponDamageMultiplier`:** the defenders' damage-taken boons need the damage model's reduction side anyway, so one seam carries both.
- **S3 (second reading), give the AI lord a scripted target with `SetScriptedPosition`:** cut (D6); the home teleport already brings him back, and a scripted ride adds code for the unattended run only.
- **F10b (first reading), decompile the native fade path before the run:** kept as the static alternative for risk 2, not a gate; the Slice 10 remount line answers the same question in game.
- **B2 (second reading), hook `OnAgentControllerSetToPlayer` for a takeover:** `AdoptIfNew` already sees every new main body each tick (`LongDefeatDirector.cs:213-221`), so the refresh and health write go there and there is one path.
- **B1 (second reading), fill a short reroll from the shown cards in draw order:** "leave out none" is simpler and replays as exactly.
- **C6 (first reading), state the commander's outcome as best effort:** replaced by marking felled agents by identity, which makes it exact.
- **X3 (second reading), run the verdict after step 4 with `--sessions 7`:** one call with `--sessions 12` after Mike's plays covers both.
