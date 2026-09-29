# Creature Bandits: Feasibility and Roadmap

**Status:** building on branch `feat/creature-bandits` (#692), 2026-09-27. The spider brood is built and unit
tested; nothing has run in game yet. See "Build status" below.
**Decision (Mike, 2026-09-25):** the goal is **Design B, truly riderless creatures, for hostile bandit
parties only**, never a recruitable troop. Design A (an invisible rider) is the fallback if B's first gate
fails.
**Source of this doc:** promoted from a plan-mode research pass so the findings survive across sessions,
the same way [cultural-feats-roadmap.md](cultural-feats-roadmap.md) was.

## Why this exists

A player asked for bandit parties made of creatures (giant spiders, wargs, elephants, war rams, elk) that
fight as agents on their own, with no rider, or with a rider nobody can see. TAOM tried the riderless shape
once, for the giant spider in June 2026, and paused it. This doc re-asks the question against the installed
v1.5.3 engine, records what is and is not possible, and lays out the route.

Prior work to read first:

- [spider.md](../features/spider.md), "Why this exists": the three spider architectures, including the
  detached riderless combatant (2026-06-04 to 06-05) that was paused and then deleted on 2026-06-10.
- [rca-spider-troop-2026-06-04.md](../reviews/rca-spider-troop-2026-06-04.md): the investigation of that
  pause.
- [spider/wolf-parity-and-render-tests.md](../features/spider/wolf-parity-and-render-tests.md): the
  ADOD_Beasts wolf comparison and the render bisection that was never finished.
- [elephant.md](../features/elephant.md), "How this differs from the paused spider": why the ridden-mount
  lane is the cheap one.

## Engine facts

Verified in the installed v1.5.3 decompile (`pwsh tools/taom-src.ps1`, cache `v1.5.3`) on 2026-09-25:

| Fact | Source |
|---|---|
| `Agent.Build` forces any Monster without `AgentFlag.IsHumanoid` to `AgentControllerType.AI` | `Agent.cs:5206` |
| `Agent.Build` sets `Formation = IsMount ? null : agentBuildData?.AgentFormation`, so a mount leaves the build with no formation | `Agent.cs:5207` |
| `Mission.SpawnAgent` calls `SetTeam` on the rider only; the mount is a second agent from `CreateHorseAgentFromRosterElements` | `Mission.cs:4215`, `4324`, `4608` |
| `Mission.SpawnMonster(EquipmentElement, EquipmentElement, …)` spawns a standalone creature (no rider, no team, no formation) | `Mission.cs:4448` |
| `Agent.SetTeam` and the `Formation` setter are public, so both can be assigned after spawn | `Agent.cs:2211`, `Agent.cs:1128` |
| `Agent.Origin` has a public setter | `Agent.cs:903` |
| `PartyAgentOrigin.SetKilled` removes one of the troop from the party's `MemberRoster`; `SetWounded` moves one to wounded | `PartyAgentOrigin.cs:168-199` |
| Party troops spawn through `Mission.SpawnTroop(IAgentOriginBase, …)`, which builds `AgentBuildData` then calls `SpawnAgent` | `Mission.cs:4517-4525` |
| `MBAgentVisuals.SetVisible(bool)` hides rendering only; the hit capsule, collision and AI targetability stay | `MBAgentVisuals.cs:166` |

Reported by a research agent and **UNVERIFIED** (re-read before relying on them):

- `Agent.SetMortalityState` accepts `Invulnerable` and `Immortal` (`Agent.cs:3088`).
- No `AgentFlag` member removes an agent from enemy target selection.

**Corrected 2026-09-25:** an earlier revision said the battle strength check counts only `IsHuman` agents
(`Mission.cs:3360`). That line is the `flee_team` debug cheat, not battle logic. The real checks are
count-based; see Phase 0, answer 3.

Known TAOM facts that bear on this:

- A non-lethal `CanDismount` blow on a rider of a non-vanilla mount crashed natively, which is why the
  spider is a locked mount (`Main/Features/Spider/Hooks/Agent_HandleBlowAux_SpiderDismountGuard_Patch.cs`;
  `CanAgentRideMount=false` and `MountDifficulty=999` in `TaomAgentStatCalculateModel`).
- Every TAOM creature attack is a behaviour tree on the mount agent that applies damage itself through
  `CustomAttacksUtils.TakeDamage`, layered on top of the rider's cavalry AI. The rider's AI steers; the
  tree bites. A riderless creature loses the steering, not the bite.

## Can a creature fight with no rider? (Design B)

**Yes; it is not impossible.** Earlier docs say the engine "does not have" a riderless non-humanoid
combatant shape. That overstates it. The engine has no *native AI or roster support* for one, but the pieces
are reachable:

- `SpawnMonster` builds the creature; the public `SetTeam`, `Formation` and `Origin` setters place it on a
  side and tie it to its party.
- ADOD_Beasts ships riderless wolves built on `SpawnMonster`, driven by about 1,000 lines of managed C#
  and no render code. Its `NativeHook.dll` (EasyHook hooks on `Agent_AiTick`, `Agent_Tick`, `UpdateFlags`)
  is **imported but never called**: the only reference in the decompiled `ADOD_Beasts.dll` is
  `using NativeHook;` (line 20). Earlier TAOM docs read the hooks as load-bearing; they are not. Riderless
  rendering and riderless behaviour both work from managed code.
- The June spider AV in `Agent.PreloadForRendering` was blamed on a per-mesh bone cap. That cause was
  refuted on 2026-06-13 and no replacement was ever established. The later spider mount AVs came from
  missing `quad_movement` clip tags in the spider's own clips, a data defect, not the riderless design.

## What "bandits only" removes

The June attempt was a **recruitable** spider: in settlement volunteer pools, fielded by AI lords, shown in
the player's party screen, upgradeable. Restricting creatures to hostile bandit parties drops most of that
surface:

| Concern | Recruitable (June design) | Bandits only |
|---|---|---|
| Volunteer pools, upgrades, player party screen, player riding | Needed | Gone |
| AI lords fielding creatures in wars and sieges | Needed | Gone |
| Player-side formation orders on a creature | Needed | Gone: always the enemy, driven by our AI |
| Siege, arena, tournament, town and village missions | Needed | Gone: field battles only (hideouts decided separately, below) |
| Casualties back to the party roster | Needed | Still needed: set `Agent.Origin` to the troop's origin |
| Prisoners and ransom | Needed | Still needed: a creature must never become a prisoner or be sold |
| Battle end and side strength | Needed | Still needed: an all-creature bandit side must not "lose" at spawn |
| Movement and target-seeking AI | Needed | Still needed, but simpler: hunt the nearest enemy, no formation tactics |
| Encyclopedia and map tooltips | Needed | Reduced: hide the troop from the encyclopedia; the party tooltip shows a name only |

## Design comparison

| Design | Verdict | Cost | Reason |
|---|---|---|---|
| **B. Riderless creature, bandits only:** `SpawnMonster` plus team, formation and origin set after spawn | **Chosen** | High | The player's actual request. Bandits-only removes recruitment, allied control and most mission types. |
| **A. Invisible rider:** a normal mounted troop whose rider is hidden and cannot be hurt | **Fallback** | Medium | The ridden-mount lane is proven and gives roster, casualties and battle end for free, but a hidden rider may still block projectiles and draw enemy aim. |
| **C. Creature as a humanoid race:** an `IsHumanoid` Monster on a quadruped rig, as the troll is a race | Rejected | Very high | Humanoid AI drives the humanoid action vocabulary, so a quadruped would need the whole combat set mapped. A Monster that is both humanoid and quadruped is unproven. The hill troll works because its rig is a biped. |

## Design B plan (bandits only)

### Phase 0: research (answered 2026-09-25)

Three read-only research agents traced the v1.5.3 decompile; the load-bearing lines were then re-read by
the main session. Lines marked **(re-read)** were checked directly; the rest are agent-reported, so re-read
them before building on them.

**1. Spawn chain: where to swap a creature in.**
- `MissionBattleSideSpawnContext.SpawnTroops` calls `current.SpawnTroop(item7, …)` for each troop origin,
  **ignores the returned agent**, and always does `_numSpawnedTroops++` (`MissionBattleSideSpawnContext.cs:375-389`,
  re-read). The side's bookkeeping is counts, not agents.
- `Mission.SpawnTroop` builds `AgentBuildData`, then `SpawnTroopWithAgentBuildData` reads
  `agent.Character.IsHero` and calls `WieldInitialWeapons` (`Mission.cs:4517-4537`, re-read). A creature
  returned *through* that method would NRE.
- **Chosen interception:** a Harmony prefix on `Mission.SpawnTroop` that matches only creature troops (by
  `troopOrigin.Troop`), spawns the creature with `SpawnMonster`, sets `__result`, and returns `false` so the
  original (and its `Character` reads) never runs. Every other troop passes straight through.
- **Rejected:** spawning the husk and then removing it. `Agent.OnRemove` fires `Origin?.OnAgentRemoved`,
  `Team.OnAgentRemoved` and every behaviour's `OnAgentRemoved` (`Agent.cs:5172-5191`, re-read), so the husk
  would be recorded as a casualty.
- **Also guard:** the banner-bearer branch (`MissionBattleSideSpawnContext.cs:377-380`, re-read) bypasses
  `SpawnTroop`. Creature troops must never be picked as banner bearers.
- The spawn position and direction can come from the same `AgentBuildData` the original would have built
  (`GetAgentBuildDataToSpawnTroop`); whether it is callable without reflection is UNVERIFIED.

**2. Casualties: they already flow if the creature carries its troop's origin.**
- The campaign casualty path is `SandBox` `BattleAgentLogic.OnAgentRemoved` (`BattleAgentLogic.cs:132-181`,
  re-read). Its only gate is `affectedAgent.Origin == null`. Killed calls `SetKilled`, unconscious calls
  `SetWounded`, anything else calls `SetRouted`; there is **no `IsHuman` check**. (A mount routed with no
  attacker is skipped at line 147.)
- Party troops carry a `PartyGroupAgentOrigin`. Its `SetKilled` and `SetWounded` call
  `PartyGroupTroopSupplier.OnTroopKilled` and `OnTroopWounded`, which count the loss and report it to the
  party group (`PartyGroupAgentOrigin.cs:108-128`, `PartyGroupTroopSupplier.cs:109-119`, re-read).
- So: set `creature.Origin = troopOrigin` (public setter, `Agent.cs:903`) and the kill reaches the bandit
  party's roster and the `MapEvent` like any troop's.
- **Crash to prevent:** on a kill, the same method calls `CheckUpgrade(…, val3, val)` with `val` the victim's
  `Character` (`BattleAgentLogic.cs:171-173`, re-read), and `TroopUpgradeTracker.CheckUpgradedCount` does
  `if (!character.IsHero …)` with no null check (`TroopUpgradeTracker.cs:77-80`, re-read). A creature with
  a null `Character` NREs there on its first death.
- **Fix:** after spawn, set `creature.Character = troop` (public setter, `Agent.cs:1426`, re-read). The
  setter overwrites `Health`, `BaseHealthLimit` and `HealthLimit` from the troop (`Agent.cs:1437-1439`,
  re-read), so restore the creature's own values right after. This also restores kill XP: the XP path
  requires both characters non-null (agent-reported, `BattleAgentLogic.cs:120`). Set it *after* the build,
  so `OnAgentBuild` listeners still see a character-less mount; then audit TAOM's own
  `OnAgentRemoved` and `OnAgentHit` listeners for "has a `Character`, so it is humanoid" assumptions.
- **Routing:** the rout branch has no humanoid gate. Whether a non-humanoid can ever be routed by morale
  is native and UNVERIFIED; creatures must never flee, so the spike watches for it.

**3. Battle end: no patch needed if the origin is set.**
- `IsSideDepleted` is count arithmetic: the phase is the last one, `NumberOfActiveTroops == 0`, and nothing
  remains to spawn (`DefaultBattleMissionAgentSpawnLogic.cs:324-328`, re-read). `NumberOfActiveTroops` is
  spawned minus removed (agent-reported, `MissionBattleSideSpawnContext.cs:51`). The spawn loop counts the
  creature as spawned (answer 1), and its origin counts it as removed on death (answer 2), so the side stays
  alive while creatures live and is depleted when they die.
- Without the origin, the creature would be counted as spawned but never as removed, and the side would
  never deplete. Setting the origin is what makes battle end work.
- `BattleEndLogic`'s retreat check walks `team.ActiveAgents` with no `IsHuman` filter (agent-reported,
  `BattleEndLogic.cs:279-361`), and `SetTeam` adds the creature to `ActiveAgents` (`Agent.cs:2211-2221`,
  re-read). A creature that never flees keeps its side from "retreating".
- **Targeting:** formation-level targeting only sees formations, so a creature with no formation is
  invisible to enemy formation orders. **Do not put the creature in a formation** (corrected 2026-09-27,
  see "Spider review", finding 4): June tried exactly that and it crashed twice.
- **Enemy soldiers probably will not pick a creature as a target.** Target choice is native
  (`Agent.GetTargetAgent`, `SetTargetAgent`, `ImmediateEnemy` are thin `MBAPI` calls, `Agent.cs:730`,
  `2907-2920`, re-read), so the managed source cannot prove it either way. The ADOD wolf mod is the best
  evidence: to get soldiers to fight its wolf, it spawns an invisible destructible prefab
  (`adod_wolf_target`) on the wolf (`ADOD_Beasts.decompiled.cs:687-690`, re-read). It also adds an
  `ADODBeastsHumanAIAgentController` to every AI human (line 695); every 0.25 s that controller points
  the soldier at the prefab with `SetScriptedTargetEntityAndPosition` in attack-entity mode, plus scripted
  movement (lines 420-523, re-read). A mod does not build that if soldiers attack the wolf on their own.
  **Caveat:** the wolf has no team, so this shows a teamless mount is ignored, not a mount on the hostile
  team. Vanilla shows the same pattern: soldiers ride loose horses (`HumanAIComponent.cs:267-302`, which
  checks `CanAgentRideMount`, so the spider's lock blocks it) but do not fight them. Horses still take
  *incidental* hits from swings and arrows aimed at riders; a riderless creature gets no aimed attacks
  at all.
- **So the feature needs its own "fight the creature" layer.** Two candidates, cheapest first, both for the
  spike:
  1. A mission behaviour that, for enemy AI humans near a creature, calls `SetTargetAgent(creature)`
     (`Agent.cs:2912`). Whether native combat AI swings at a non-humanoid target agent is UNVERIFIED; ADOD
     chose not to rely on it.
  2. ADOD's shape, as a design reference only: a destructible proxy entity riding on each creature, with
     soldiers pointed at it through `SetScriptedTargetEntityAndPosition`, and hits on the proxy forwarded to
     the creature. Heavier (a prefab, per-soldier scripted movement, damage forwarding), but proven to work.

**4. Prisoners, auto-resolve, encyclopedia.**
- After a battle, defeated troops become prisoners only if
  `BattleRewardModel.CanTroopBeTakenPrisoner(character)` allows it (`MapEvent.cs:1855`, re-read); vanilla
  always returns `true` (`DefaultBattleRewardModel.cs:424-427`, re-read).
- TAOM already registers `TaomBattleRewardModel` (`SubModule.cs:1152`; `gamemodel-registry.md`). A model
  slot takes one registration, so **add the override to that class**: return `false` for creature troops.
  Gating at capture keeps them out of ransom, sale and prisoner recruitment too; whether those screens
  re-check is UNVERIFIED.
- Auto-resolve power comes from troop tier only, never equipment (agent-reported,
  `DefaultMilitaryPowerModel.cs:244-253`), so a creature troop's strength off-screen is set by its `level`
  and tier.
- Hide the troop with `is_hidden_encyclopedia="true"` on its `NPCCharacter` (agent-reported,
  `CharacterObject.cs:552-553`).

**5. Movement: managed code is enough.**
- ADOD's wolf uses `SetScriptedPosition` with the `GoToPosition` flag toward its target, `SetMovementDirection`
  to face during attacks, and `SetMaximumSpeedLimit` for pace (agent-reported, decompiled
  `ADOD_Beasts.decompiled.cs:1550-1832`). The native hook DLL is never called (re-read, see above).
  `comparison-only` per its [provenance row](../reference/provenance-register.md).
- ADOD never puts its wolf on a team; it binds the wolf to its owner and gives every human AI agent a
  custom controller so they notice it. TAOM's team-based design is therefore **not proven by ADOD** on
  targeting (answer 3).
- **Despawn:** `SpawningBehaviorBase`'s 30-second riderless-mount fade is **multiplayer-only**
  (agent-reported; it lives under `Missions\Multiplayer\SpawnBehaviors`). Nothing in single player fades a
  living riderless creature, and TAOM's `MountDespawnMissionBehavior` only fades dead ones.
- **Reusable TAOM code still on trunk (agent-reported):** `HasNoRiderDecorator`, the riderless team
  fallback in `SpiderEngageDecorator` (`spider.RiderAgent?.Team?.Side ?? spider.Team?.Side`), and the
  `attacker.RiderAgent ?? attacker` attribution in `SpiderAttackService`. The June spawner, wield guards and
  move task are in git at `d8e05986` (deleted by `a47b89cb`).

**The render question, re-read.** The June spike *did* render riderless creatures: its committed warg
stand-in spawned detached, rendered and did not crash (`rca-spider-troop-2026-06-04.md:40-41`). The AV was
specific to the spider on the `Mountable="false"` path (same RCA, line 97). So **keep creatures
`Mountable="true"`**: that is the render lane every riderless horse uses when its rider dies. Then stop enemies
mounting them with the lock the spider already uses (`CanAgentRideMount=false`, `MountDifficulty=999` in
`TaomAgentStatCalculateModel`). The same RCA warns that a `Mountable` warg's own mount AI wanders
(line 68); scripted `GoToPosition` movement should override it, which is what the spike checks.

### Phase 1: the render confirmation (decides B or A)

Now a cheap confirmation, not an open risk. **Cheapest first, no code:** in Custom Battle, fight
`taom_spider_creature` spider riders and kill a rider. The spider left behind is a riderless
`Mountable="true"` spider on v1.5.3, exactly the render lane Design B uses. Watch that it renders,
animates while idle (see "Spider review", finding 5) and does not crash. Then, if needed, spawn **one riderless
warg** (`Mountable="true"`) with `Mission.SpawnMonster` from a console command, with no team, AI or roster.

- **Renders and animates:** B is open; go to Phase 2.
- **Crashes:** run the A2 and A3 render bisection in
  [wolf-parity-and-render-tests.md](../features/spider/wolf-parity-and-render-tests.md) on the warg. If that
  finds no data cause, switch to Design A.

### Phase 2: a riderless warg that fights (spike, not committed)

1. Console-spawn it on the enemy team with `SetTeam`; no formation (finding 4), no origin needed yet.
2. Give it an attack: the spider behaviour tree with a riderless attack branch (finding 1).
3. Movement: pick the nearest enemy, `SetScriptedPosition` with `GoToPosition` toward it, face with
   `SetMovementDirection` in range, attack, re-target on death.
4. Watch list:
   - enemy soldiers attack it: first with no help (expected: they ignore it), then with `SetTargetAgent`,
     then with the proxy entity if that fails (answer 3);
   - its own mount AI does not wander off, and nobody mounts it;
   - it paths around obstacles and does not freeze;
   - it takes damage, dies with a death animation, and never flees;
   - the June native wield garbage (`0xee0`, `0xee4`) does not return.

### Phase 3: the feature

- **Code:** `Main/Features/CreatureBandits/`, following ADR-002 and ADR-007, with TDD on every service.
  - The `Mission.SpawnTroop` prefix from answer 1, for creature troops only:
    1. `SpawnMonster`;
    2. `SetTeam`;
    3. `Origin = troopOrigin`;
    4. `Character = troop`, then restore health;
    5. **no** formation (finding 4);
    6. attach the AI and behaviour tree;
    7. return the creature and skip the original.
  - The spider-specific fixes in "Spider review" below: findings 1 to 8.
  - Keep creature troops out of the banner-bearer branch.
  - The creature AI component (answer 5).
  - `TaomBattleRewardModel.CanTroopBeTakenPrisoner` returns `false` for creature troops (answer 4).
  - The mount lock, so no agent rides a creature.
  - **No battle-end patch**; answer 3 says the counts already work. Confirm in the smoke.
  - A guard so the prefix fires only for hostile bandit parties in field battles; any other mission spawns
    the troop normally (a harmless unarmed husk) and logs it.
  - An MCM toggle, default off until smoked. Built instead as "Spawn Spider Broods", **default on** (Mike,
    2026-09-28): players get broods in a new campaign, and one who hits a problem can stop new spawns without a
    patch. It gates the daily spawn only; the looter cap and the battle swap stay.
- **Data:**
  - Per creature, one hidden creature troop, which carries the creature item in its `Horse` slot as the
    spawn source and is hidden from the encyclopedia.
  - A bandit culture (`is_bandit="true"` in `taom_spcultures.xml`), or creature stacks in an existing one.
  - Raider and boss party templates in `taom_partyTemplates.xml`. A research agent reported that a new
    bandit culture plugs into `BanditSpawnCampaignBehavior`, `TaomBanditDensityModel`, Patch39 and Patch86
    with no new C# (UNVERIFIED; spot-check first).
- **Rollout order:** wargs (wild warg packs), then spiders (Mirkwood), then the rest. Elephants last: a
  riderless war elephant is a big AI and balance jump.
- **Hideouts:** decide separately. Hideout fights are staged missions with their own spawn and boss logic
  (Patch86). Start with creatures only in roaming parties.
- **Gates:**
  - `python tools/validate_moduledata.py` (`BROKEN_TROOP_REF`, `MOUNT_WITHOUT_HARNESS`; decide whether a
    riderless creature troop needs a harness exemption in `_HARNESSLESS_BY_DESIGN`);
  - `python tools/audit_mount_parity.py`;
  - `HideoutBossPartyTemplateTests`;
  - `/localize` for the names;
  - `/deep-review`, then `/ship`.
- **Docs:** a `docs/features/creature-bandits.md` from `TEMPLATE.md` and a feature-map row once shipped;
  a trap-index line for any trap the spike finds.

## Spider review (2026-09-27)

A design review of the first concrete troop: a riderless giant spider in a bandit party. Three read-only
passes covered the spider's code and data, native engine behaviour (with `tools/native_decompile.py`), and
the campaign side. As in Phase 0, **(re-read)** marks lines the main session checked itself; the rest are
agent-reported. Ranked most severe first.

### Battle blockers

**1. The spider never attacks without a rider (re-read).** `SpiderBehaviorTree.BuildTree` puts the whole
engage, pounce and swipe subtree under `HasRiderDecorator` (`SpiderBehaviorTree.cs:50`), and the next gate,
`IsAiControlledDecorator`, returns `false` when `RiderAgent` is null (`IsAiControlledDecorator.cs:14-17`).
A riderless spider only reaches the "no rider" branch, a 4-second sleep in a loop (`SpiderBehaviorTree.cs:66-67`).
**Fix:** a riderless attack branch gated on `IsAiAgentDecorator` (the agent's own AI flag), shaped like
`TrollBruteForceBehaviorTree` (`TrollBruteForceBehaviorTree.cs:33-43`), TAOM's first tree on an agent with
no rider. Gate that branch on "this agent is a creature bandit" as well, so today's spiders whose rider has
died keep sleeping instead of biting as a teamless agent (finding 7). What already works with no rider
(agent-reported): the tree attaches by `Monster.StringId == "spider"` (`SpiderMissionBehavior.cs:16-18`),
`SpiderEngageDecorator` falls back to `spider.Team` (`SpiderEngageDecorator.cs:49`), `SpiderAttackService`
credits `attacker.RiderAgent ?? attacker` (`SpiderAttackService.cs:62`), and the pounce and swipe tasks
never read `RiderAgent`.

**2. The first hit makes it flee (re-read).** `CommonAIComponent.OnHit` calls `Panic()` whenever a riderless,
AI-controlled mount takes 1 or more damage (`CommonAIComponent.cs:198-201`), bypassing the morale check.
Mounts do get this component (agent-reported). A panicked mount retreats, and `Mission.CanAgentRout`
lets any riderless agent that is retreating, running away or wandering (`CanWander`) fade out at the map
edge (`Mission.cs:5325`, used at `CommonAIComponent.cs:99-106`). The live spider Monster sets
`RunsAwayWhenHit="true"` and `CanWander="true"` (`lotr_monster_spider.xml:120-124`), the same set as the
vanilla horse. The June spider never met this because it was `Mountable="false"`, so `IsMount` was false;
keeping `Mountable="true"` for the render lane brings it in. **Fix:**
- a Harmony prefix on `CommonAIComponent.OnHit` that skips the panic for creature-bandit agents;
- a postfix on `Mission.CanAgentRout` returning `false` for them;
- clear `RunsAwayWhenHit` and `CanWander` on the agent (`Agent.SetAgentFlags` is public and the native side
  replaces the whole flag word, agent-reported `Agent.cs:2495`).

`Mission.CanAgentRout_AdditionalCondition` (`Mission.cs:1552`, re-read) is a public event, but it is a
multicast `Func<Agent, bool>` that returns only the last subscriber's answer, so it cannot be shared with
other mods safely. Use the postfix.

**3. A creature that flees off the map is never counted, and the battle cannot end (re-read).**
`BattleAgentLogic.OnAgentRemoved` returns early for a mount removed as routed with no attacker
(`BattleAgentLogic.cs:147`; routed is `AgentState` 2, agent-reported), before `Origin.SetRouted`. A
fade-out at the edge has no attacker. The troop is then counted as spawned but never as removed, so
`IsSideDepleted` never becomes true for the bandit side (answer 3). This qualifies answer 3: **no battle-end
patch is needed only if creatures never rout.** **Fix:** finding 2 stops the rout. As a backstop, a
creature listener's `OnAgentRemoved` calls `Origin.SetRouted` for a routed creature, which is safe to repeat
because `PartyGroupAgentOrigin` ignores a second removal (`PartyGroupAgentOrigin.cs:130-136`, re-read).
That callback can arrive off the main thread, so any TAOM collection it touches goes through
`DeferredCallbackQueue.RunOrDefer`.

**4. No formation (re-read, June RCA).** Adding the riderless spider to a formation crashed twice in June:
a native AV in `GetMissileRange` reached from `TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime`
(`rca-spider-troop-2026-06-04.md:28`), and a null-`HumanAIComponent` NRE in the per-agent formation tick
(same RCA, line 61). The verdict was "Rejected". `Agent.Build` nulls a mount's formation anyway
(`Agent.cs:5207`). The creature stays on its team with no formation, moved by its own AI, and enemy
soldiers reach it only through the targeting layer in answer 3.

### Battle, medium

**5. The spider has no riderless idle animations (agent-reported).** The live `as_spider` binds none of
`act_horse_riderless_idle_1` to `_4` (Armory `action_sets.xml:65879-65950`), and neither does `as_warg`.
Vanilla `as_horse` binds all four (Native `action_sets.xml:16727-16742`). A riderless spider stands idle
often, between moves and after a kill. This class has shipped before (the chariot port,
`docs/reviews/lessons/data-content-cultures.md:192`). **Fix:** bind the four on `as_spider` and `as_warg` in
the live Armory, with an in-repo gate (the trap index's "Unversioned modules" rule); the action-set parity
audit is the natural home.

**6. The native weapon state is uninitialised on a mount-built agent (re-read, June RCA).** `SpawnMonster`
uses the mount build, which never runs the native per-slot weapon setup (`rca-spider-troop-2026-06-04.md:30`).
June needed `Agent_SpiderNativeWieldGuard_Patch` on the closed set `GetMissileRange`,
`GetPrimaryWieldedItemIndex` and `GetOffhandWieldedItemIndex`, answering `0` or `None` (same RCA, line 62).
The June crash in `WieldInitialWeapons` (line 26) does not recur here: the `SpawnTroop` prefix skips
`SpawnTroopWithAgentBuildData`, which is where v1.5.3 wields (`Mission.cs:4534-4536`, re-read). **Fix:** restore
the three-method guard from git (between `d8e05986` and `a47b89cb`), keyed on creature-bandit agents,
before the first in-game spike.

**7. #594 is still open (agent-reported, `gh issue view 594`).** Spider and warg bites treat a mount whose
`Team` is null as everyone's enemy (`SpiderAttackService.cs:43-45`), and the radial bite (`:112-116`) hits every
agent in its arc (RCA `rca-warg-clip-on-horse-2026-09-13.md:363`). The bandit spider has a team, so it bites
correctly, but more riderless mounts on the field widen #594. **Fix:** land #594 first or with this.

**8. Custom Battle has no creature mount lock (agent-reported).** The campaign model locks mounting by the
mount's `Monster.StringId`, so it holds with no rider (`TaomAgentStatCalculateModel.cs:73-78`, `104-111`).
`TaomCustomBattleAgentStatCalculateModel` has no such lock (`plans/_audit/2026-09-23-opus/lane-2.findings.md:335`),
and soldiers look for loose mounts to ride (`HumanAIComponent.cs:267-302`, re-read). The Phase 1 and 2
spikes run in Custom Battle, so **add the lock there first**, or an AI soldier may ride the test spider.

**Tuning:** the pounce picks its charge variant from `MovementVelocity.Y` (`SpiderConfig.ChargeVelocityThreshold`,
agent-reported); check it under scripted movement. June's lesson on movement: search the whole mission for
the nearest enemy, since a 16 m engage gate left spiders wandering at battle start
(`rca-spider-troop-2026-06-04.md:63`).

### Campaign side

**9. The map icon is the troop at roster index 0 (agent-reported).** With no leader hero, the bandit party's
icon comes from `PartyBaseHelper.GetVisualPartyLeader`, which returns `MemberRoster.GetCharacterAtIndex(0)`;
`MobilePartyVisual` draws that character and its mount (`MobilePartyVisual.cs:1014-1020`, re-read). Index 0
follows template stack order. A spider stack first shows the husk character riding a spider on the map.

**10. The encounter conversation uses the highest-tier troop (agent-reported).** `PlayerEncounter` asks
`ConversationHelper.GetConversationCharacterPartyLeader`, which walks the roster for the highest tier
(`PlayerEncounter.cs:1208-1210`). Keep the husk's tier below the party's humanoid boss and chief, or the
player talks to the husk.

**11. Hideouts need humanoids (agent-reported).** `HideoutCampaignBehavior` finds the boss by
`Culture.BanditBoss` and pads a thin hideout with `Culture.BanditBandit` (`HideoutCampaignBehavior.cs:591-697`),
and Patch86 reads the same boss reference. `bandit_boss`, `bandit_chief` and `bandit_bandit` stay humanoid.
Whether hideout missions spawn through `Mission.SpawnTroop` is UNVERIFIED, so the prefix must check the
mission type itself and fire only in field battles.

**12. There is no Dol Guldur bandit culture (agent-reported).** `mirkwood_stalkers` is the only Mirkwood
bandit culture and its troops are Silvan elves (`taom_spcultures.xml:4244-4258`); `dolguldur` is a settled
culture with no `is_bandit` (`:2374`). Spider bandits need a new culture.

**Closed, no change needed:**
- **Loot (re-read):** the loot roll skips `NotMerchandise` items (`DefaultBattleRewardModel.cs:109`), and all
  three spider items are `is_merchandise="false"` (Armory `LOTRAOM_horses.xml:600`, `629`, `654`).
- **The mount lock** keys on the mount, not the rider (finding 8).
- **Spider dismount patches:** Patch47 and Patch48 key on a rider's mount and do nothing without one
  (agent-reported).
- **Party-screen transfer lock:** unneeded. `IsNotTransferableInPartyScreen` has no XML attribute, only
  `SetTransferableInPartyScreen` (agent-reported, `CharacterObject.cs:747-769`), and a bandit-only creature
  never reaches the player's roster once `CanTroopBeTakenPrisoner` blocks capture.

**Data gates for the husk troop:**
- its id added to `_HARNESSLESS_BY_DESIGN` (`tools/taom_schema.py:1052-1074`, which already lists
  `taom_spider_creature`);
- a `<face>` block (`CharacterFaceCoverageTests`);
- `is_hidden_encyclopedia="true"`.

### Decision for Mike: who leads a spider band?

- **Goblin-led band:** a Dol Guldur goblin stack first in the template. The map icon and conversation show a
  goblin; the spiders fight alongside. Simplest; no new visuals.
- **Spider-only brood:** closer to the Mirkwood spiders of the books, but the icon then shows the husk
  character riding a spider (finding 9). It needs a meshless "invisible" race for the husk so the map
  icon and tableaus show a spider alone (untested), and a humanoid-free answer for the encounter
  conversation (finding 10).

## Build status (2026-09-27)

**Mike's decisions:** the brood is led by a spider, the pale one (`spider_mount_pale`), with no goblins. KEYforce
will add a mountain spider mesh as a second troop.

**Built on `feat/creature-bandits`, unit tested; route A spiked in Custom Battle, the campaign path not yet run in
game** (feature doc:
[creature-bandits.md](../features/creature-bandits.md)):

| Piece | Where | Covers |
|---|---|---|
| Rules and config | `Main/Features/CreatureBandits/CreatureBanditRules.cs`, `CreatureBanditsConfig.cs` | the troop catalogue, the creature fingerprint, spawn, backstop and brood decisions |
| Riderless spawn | `Hooks/CreatureBanditSpawner.cs`, `Patch93_CreatureBanditSpawn` | Phase 0 answers 1 to 3; finding 4 (no formation) |
| Stays in the fight | `Patch93_CreatureBanditNoPanic` (hit panic), the morale models' `CanPanicDueToMorale` (morale panic; a patch until the 2026-09-28 review), `Patch93_CreatureBanditNoRout`; `RunsAwayWhenHit`, `CanWander`, `CanGetScared` and `CanRear` cleared at spawn | finding 2 |
| Routed backstop | `CreatureBanditMissionBehavior` | finding 3 |
| Weapon-state guards | three `Patch93_*WieldGuard` prefixes, on PatchShield's hot-method list | finding 6 (logs whether they ever fire) |
| Hunt and bite | `SpiderBehaviorTree` creature-bandit branch, `IsCreatureBanditDecorator`, `CreatureHuntTask` (hostile teams only; releases the scripted move while a bite plays) | finding 1 |
| No prisoners | `TaomBattleRewardModel.CanTroopBeTakenPrisoner` | answer 4 |
| Custom Battle lock | `TaomCustomBattleAgentStatCalculateModel.CanAgentRideMount` | finding 8 |
| Mirkwood spawning | `CreatureBroodSpawnBehavior`; `TaomBanditDensityModel.GetMaxSupportedNumberOfLootersForClan` returns 0 for the brood | finding 12; vanilla would spawn this looter faction map-wide |
| Map icon, no parley | `Patch94_CreatureBroodMapIcon`, `Patch94_CreatureBroodNoParley` | findings 9 and 10 |
| Data | `characters/creature_bandits.xml` (3 troops), culture and clan `mirkwood_spiders`, `mirkwood_spiders_brood_template` | finding 11 (no hideouts) |
| Wiring | `CreatureBanditsModule` in `FeatureModules.All`; Harmony registry Patch93 and Patch94 | |
| Console spawns in Custom Battle | `MissionSpawnCheats` looks troops up as `BasicCharacterObject` | Custom Battle registers no `CharacterObject` |
| Diagnostics (temporary) | `Diagnostics/` folder, `CreatureBanditDiagnosticsBehavior` | every playtest question below; strip after sign-off |

**Built after the first Custom Battle spike (2026-09-27):** the targeting layer, route A (see "Targeting" below).
It passed its spike on 2026-09-28: 9 spiders over 2 Custom Battles, all unmounted with a weapon state, all targeted
by melee soldiers and archers (up to 78 soldiers on one spider), every logged hit aimed, all 9 killed by soldiers,
no crash, animations normal.

**Built after that spike, not yet run in game (2026-09-28):** the creature's own behaviour tree
(`CreatureBanditBehaviorTree`, split from the ridden spider's) and its own numbers, C# defaults with MCM options
(top-level group "Creature Bandits"): 200 HP; bite 1 soldier, pounce 2, swipe up to 3 at half damage; crit-only
knockdown; cooldowns; damage taken half from missiles and full from melee, per type, in both the campaign and
Custom Battle damage models. The spike had shown an uncapped bite striking 23 soldiers (69 kills by 3 spiders in about
10 s) and archers killing a 120 HP spider in 3 to 5 s. The `mission` diag line names the active damage model and the
tuning; `spawn-wired` repeats the tuning; the `attack` line shows each strike's cap.

**Not built yet:**
- **Riderless idle animations** (finding 5): the live Armory's `as_spider` still lacks
  `act_horse_riderless_idle_1` to `_4`. Live-Armory edit, owed with an in-repo gate.
- **#594** (finding 7), a separate issue.
- **Translations:** the four spider names and the three troll names (#694) are registered and seeded in all 12
  languages with their English text; the paid translation run waits for Mike's approval (#695).
- **Hideouts:** none in this cut.

**New campaign only:** the clan is defined in XML, so an older save has no brood clan and spawns nothing (the
spawner logs it once).

**In-game order:**
1. Custom Battle: `taom.spawn_troops taom_spider_brood_forest 3 enemy`. Expect three riderless spiders on the
   enemy side that hunt, bite, never flee and die. Watch whether soldiers attack them unaided. Console spawns
   cannot test the battle end: their origin records no casualty and the spawn logic never counted them, so the
   battle ends when the original enemy army falls. Battle-end accounting is tested in step 2.
2. A new campaign: find a brood near Felegoth, Caras Laerolin, the Mirkwood castles or Dol Guldur. The map
   icon should be a spider; attacking should skip the talk; the battle as in step 1; no spiders among the
   prisoners.
3. Read the game's `bin/Win64_Shipping_Client/Logs/taom_debug_<timestamp>.log` (not under `Modules/`). Every creature line starts with
   `[CreatureBandits][diag]` and carries `t=` (mission time) and `cb=` (the creature's serial in that mission);
   ridden spiders keep their old `[Spider][diag]` lines. The lines that answer each question:
   - **Did it spawn and wire?** `mission`, then `spawn-begin`, `spawn-built`, `spawn-wired` per creature (the
     last one on disk before a crash names the step). A fallback to the vanilla husk is a plain
     `[CreatureBandits] Spawn of '<troop>' fell back` WARNING, which outlives the diagnostics.
   - **Do soldiers fight it?** `creature-final-ai`: `nearTargetingIt` out of `nearSoldierSamples` (hostile
     soldiers within 10 m, sampled each second); `creature-final`: `aimedHits` out of `hitsTaken`. The
     `formation` lines show whether enemy formations see a creature at all.
   - **Does it bite?** `creature-final-ai`: `engagePasses` against `engageRejRange`, `engageRejCone`,
     `engageScanEmpty` (with `bestMissDist` and `bestMissAngle`); `stalledInContactSeconds` and
     `minStalledDist` for a spider pressed against its target that never bites; `attack` and `attack-whiff`
     lines (a whiff needs an enemy in the arc; brood-mates count as `alliesInArc`).
   - **Does it flee or stall?** `state` WARNINGs (panic, running away, fingerprint lost, rider), `stuck`,
     `panicked`, `fled`, `mounted`, `occupant-lost`.
   - **Is each death counted?** `removed` per creature, `backstop` WARNINGs, the `sides` lines (live agents and
     depletion per side) and one `side-stall` WARNING if a side stays empty but undepleted.
   - **Totals:** `creature-final` and `creature-final-ai` per creature, then one `summary` line. The campaign
     side writes `campaign-start`, `daily`, `brood`, `brood-spawn`, `brood-stray`, `brood-battle-start`,
     `brood-battle-end` and `brood-destroyed`, plus `map-icon`, `no-parley` and `prisoner-refused` the first
     times each fires.

## Targeting (route A, 2026-09-27)

**What the first spike measured.** Three console-spawned spiders over 65 s: of about 6,000 samples of hostile
soldiers within 10 m, none targeted a spider, and no hit of any kind landed on one; the spiders made 54 kills.

**Why, from the v1.5.3 native code** (decompiled with `tools/native_decompile.py`, each link re-checked):
1. A soldier picks targets in two native candidate builders, melee `FUN_18068b1e0` and ranged `FUN_18068b750`.
   Both accept a candidate only when it is an enemy (`IsEnemy`), active, not the soldier, and has no rider. Neither
   tests `IsHumanoid` or `CanAttack`. The combat tick (`FUN_1806a5c70`) also needs `IsEnemy` or an attack entity.
2. `IsEnemy` (`FUN_1805d52e0`) reads a `Mountable` agent's side from its rider. A riderless creature resolves to no
   side, so it is nobody's enemy, whatever `SetTeam` wrote. The spawn line's `enemyOfPlayer=0 playerEnemyOf=0` shows it.
3. Clearing `Mountable` fixes `IsEnemy`, but the candidate scorer `FUN_18068c9d0` (line 129) then reads the
   creature's native weapon state (`+0xad8`) without a null check. The engine allocates that block only when the
   agent is created with `CanWieldWeapon` (`FUN_1805b89c0`), so a Monster-built creature has none: the June crash
   class.
4. A live `CanWieldWeapon` is its own crash: every frame the agent looks up the unarmed ("fist") melee actions,
   `as_spider` has none, and the missing index is used unchecked.

**The fix.** Create the creature with `CanWieldWeapon` (weapon state allocated), strip the flag before the build,
then clear `Mountable` once it has a team (`Patch93_CreatureBanditWeaponState`, `CreatureRouteAUnmount`; the
Harmony registry's Patch93 entry has the detail). Two skeptic reviews found no remaining blocker. Rejected:
forcing `SetTargetAgent` (the target field changes but the soldier never swings, since the combat tick still needs
`IsEnemy`), `IsHumanoid` or `CanAttack` on the creature, formation membership, and, for now, ADOD's proxy entity
(heavy, and its API is absent from every engine from 1.4.5 to 1.5.3).

**Spike pass criteria** (3 Custom Battles): every `spawn-built` line has `cwwAtCreate=1 weaponState=alloc`, every
`spawn-wired` line `routeA=on cwwLive=0 isMount=0`; no crash or hang; every spider logs `routeA-alive`,
`first-targeted` and `first-hit-survived`; `nearTargetingIt/nearSoldierSamples` at least 0.25; hits taken from
soldiers with `aimedHits` at least half of them, melee in every run and missiles in two; a soldier kills a spider;
no `panicked` or `fled`; locomotion and bite animation unchanged. Also check the game's `rgl_log` for
`does not contain` or `Weapon wield interrupted`: either means a weapon-flag path ran for the creature.

**Owed before shipping, not before the spike:** decisions on the `IsMount` consumers that change once the creature
is not a mount (corpse fade in `MountDespawnMissionBehavior`, field-commission merit, the troll ring, Sauron's
strikes and the warg scans now seeing it, victim weight becoming the Monster's 250); co-op (`SetAgentFlags` is not
networked, so clients keep `Mountable`); and the two creation paths the skeptics flagged, a throw inside
`CreateAgent` after the native call and a console spawn overlapping the async agent tick.

## Design A (fallback)

Used only if Phase 1 fails. A normal mounted troop with a "husk" rider:

1. A `MissionBehavior` that, on `OnAgentBuild` for a tagged husk rider, calls `AgentVisuals.SetVisible(false)`,
   sets `MortalityState.Invulnerable`, and kills the rider when its mount dies. An invisible rider must never
   end up on foot.
2. Check that an unarmed or hidden-weapon rider's cavalry AI still closes on enemies (UNVERIFIED).
3. Watch list:
   - projectiles and blows stopping on the rider's capsule;
   - enemy AI aiming at the rider;
   - the rider's nameplate, banner, shadow and voice;
   - visibility surviving an equipment refresh;
   - tableaus still showing a human.
4. Fallbacks:
   - forward blows that hit the rider to the mount (`CustomAttacksUtils.TakeDamage`);
   - an invisible, meshless race.

## Open questions

- Enemy soldiers most likely ignore a creature (answer 3). Does `SetTargetAgent(creature)` make them fight
  it, or is ADOD's proxy-entity approach needed? (Phase 2)
- Does scripted `GoToPosition` movement override a `Mountable` creature's own wandering mount AI? (Phase 2)
- Can a non-humanoid be routed by morale? (Phase 2)
- Is `GetAgentBuildDataToSpawnTroop` callable from the prefix for the spawn position, or does it need
  reflection? (Phase 3)
- Do ransom, sale or prisoner-recruitment screens re-check `CanTroopBeTakenPrisoner`? (Phase 3)
- Should creatures appear in hideouts at all? Do hideout missions spawn through `Mission.SpawnTroop`?
- Who leads a spider band: goblins, or a spider-only brood with a meshless husk? ("Spider review")
- With panic and rout blocked (findings 2 and 3), does native AI still move a riderless mount on its own
  against our scripted position? (Phase 2)
- Every agent-reported line in Phase 0, and the UNVERIFIED engine claims under "Engine facts".
