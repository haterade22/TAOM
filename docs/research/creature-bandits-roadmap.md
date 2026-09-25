# Creature Bandits: Feasibility and Roadmap

**Status:** proposal, 2026-09-25. Nothing is built.
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
  invisible to enemy formation orders. `Formation.AddUnit` null-guards `Character` (agent-reported,
  `Formation.cs:2280-2328`), so assign the creature to its formation after spawn (`Formation` setter,
  `Agent.cs:1128`).
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

Now a cheap confirmation, not an open risk. Spawn **one riderless warg** (`Mountable="true"`) with
`Mission.SpawnMonster` from a console command in Custom Battle on v1.5.3, with no team, AI or roster.

- **Renders and animates:** B is open; go to Phase 2.
- **Crashes:** run the A2 and A3 render bisection in
  [wolf-parity-and-render-tests.md](../features/spider/wolf-parity-and-render-tests.md) on the warg. If that
  finds no data cause, switch to Design A.

### Phase 2: a riderless warg that fights (spike, not committed)

1. Console-spawn it on the enemy team: `SetTeam`, then `Formation`; no origin needed yet.
2. Give it an attack: the warg or spider behaviour tree, riderless branch enabled (`HasNoRiderDecorator`).
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
    5. set `Formation`;
    6. attach the AI and behaviour tree;
    7. return the creature and skip the original.
  - Keep creature troops out of the banner-bearer branch.
  - The creature AI component (answer 5).
  - `TaomBattleRewardModel.CanTroopBeTakenPrisoner` returns `false` for creature troops (answer 4).
  - The mount lock, so no agent rides a creature.
  - **No battle-end patch**; answer 3 says the counts already work. Confirm in the smoke.
  - A guard so the prefix fires only for hostile bandit parties in field battles; any other mission spawns
    the troop normally (a harmless unarmed husk) and logs it.
  - An MCM toggle, default off until smoked.
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
- Should creatures appear in hideouts at all?
- Every agent-reported line in Phase 0, and the UNVERIFIED engine claims under "Engine facts".
