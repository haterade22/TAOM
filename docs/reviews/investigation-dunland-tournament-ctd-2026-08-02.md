# Investigation — Dunland tournament CTD (in flight)

**Status:** root cause NOT established. Instrumentation shipped and verified in-game.
**Issue:** [#372](https://github.com/haterade22/TAOM/issues/372) (closed on the instrumentation, per the #350 precedent — the crashes are open).
**Reporter symptom:** crash to desktop entering a tournament in Carreglyn (a Dunland town).
**Artifacts received:** `taom_debug_2026-08-02_21-09-52.log` (the crash),
`taom_debug_2026-08-02_21-45-55.log` (the session after). Machine `FESTERLITTLE`.
**Engine:** v1.4.7.117484.

Separate reporter and separate machine from
[`investigation-rhun-dwarf-ctd-2026-08-02.md`](investigation-rhun-dwarf-ctd-2026-08-02.md)
(`ANDRÉ`). Both are native CTDs with a silent managed side; do not merge them.

Rename to `rca-*.md` once the root cause lands.

## Session facts

| | |
|---|---|
| Campaign | New game, character `Wulfram`, culture `empire` (= Dunland), career `avanc_luth_raider`, day 0 |
| Start | Teleported to `town_EN2`; saved `save001` at 21:14:18 |
| Mission | `MissionOpenNew mission='TournamentFight' scene='arena_empire_a' encountered='Carreglyn'` |
| Missions this session | **Exactly one** — the tournament. No town-centre or tavern mission at any point |
| Last line | `seq=27 t=+9186ms phase=AgentEquipOk agent#0 'Musician'` — then nothing |
| Reporter build | `d68f14cb` (see Established #4) |

## Established

1. **Hard native CTD, not a hang.** `BattleLoadStallWatchdog` runs on a background `Timer` and
   survives a frozen main thread. It never fired. The process died outright.
2. **The log tail is complete — the silence is evidence.** `Main/Core/Logging/FileLogger.cs` drains
   INFO synchronously with a `Flush()` on the calling thread, so `seq=27` was on disk before the
   call returned. The two `[DEBUG] slot=` lines above it are async and survived only *because*
   `seq=27` flushed them — had the crash been inside agent#0's equip, they would be missing.
3. **The absence of `agent#1` is real.** `Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch` gates
   on exactly two booleans (`svc.IsEnabled`, `BattleLoadLoadingWindow.IsOpen`). No agent cap, no
   first-N limit, no mission-type filter.
4. **The reporter is on `d68f14cb`**, a diagnostic build. Two independent markers in their own log:
   the `ACTION-SET PROBE` block exists only between `d68f14cb` and `e0e4fd57`, and
   `registered all 1 taom.* commands` predates `218537ba` (which added six more). Their DLL's
   reported version string is unreliable — `e0e4fd57` exists because the build-stamp detector was
   wrong.
5. **Their `ActionIndexCache` statics are poisoned**, proven twice for the same action name in one
   log: the static `act_inventory_idle_start` reads **-1** (probe, `TableauDiagnostics.cs:333`
   @ `d68f14cb`, all 15 races) while the live `Create("act_inventory_idle_start")` returns **4008**
   (`CharacterSpawnerService.cs:108`). `ActionIndexCacheRepair` (`19ec0e1e`, Aug 1) is **not** in
   their build. `act_none` is hardcoded `-1` (`ActionIndexCache.cs:494`), so on a poisoned machine
   every static equals `act_none` and engine guards shaped `if (action != act_none)` misclassify
   every statically-referenced action as "no action".
   **This is a confounder to control for, not a diagnosis** — the documented symptom is cosmetic
   (bind-pose rendering), and `Mission.cs:4041`'s own `act_none` guard is unaffected by it.

## The "impossible agent" — RESOLVED, and it was a red herring

`agent#0 'Musician' char='musician_dunland'` looked like an agent with no code path. Three separate
analyses concluded that, all from the same mistake: reading only the 13-behavior
`InitializeMissionBehaviorsDelegate` in `OpenTournamentFightMission` (`TournamentMissionStarter.cs:61-102`)
and never checking what the mission actually holds at runtime. **A live tournament has 65
behaviors** — `MissionView`s are registered separately and never appear in that delegate.

The spawner is **`MissionAudienceHandler`** (`SandBox.View.dll`, behavior [32] in the live list). It
populates the arena crowd from a weighted draw over the settlement culture's location characters:

```
Townswoman 0.2 · Townsman 0.2 · Armorer 0.1 · Merchant 0.1 · Musician 0.1
Weaponsmith 0.1 · RansomBroker 0.1 · Barber 0.05 · FemaleDancer 0.05
```

then `Mission.Current.SpawnAgent(...)`. A Musician is a **1-in-10 spectator**. Nothing anomalous.

The in-house repro confirms it: `agent#0 'Townsman' char='townsman_dunland'`, `agent#2 'Armorer'`,
and 648 agents total in the same scene, on a healthy load.

What survives: the reporter's process died in `Mission.BuildAgent`'s native tail for the **first
audience agent**. That is still where it died — it just is not a *strange* agent.

## In-house repro (2026-08-03, `taom_debug_2026-08-03_10-40-34.log`)

**The entry crash did not reproduce.** Same town (`Carreglyn`), same scene (`arena_empire_a`);
the mission reached `BattlePlayable ... agents=648` at +8072 ms and played for ~72 s. Per the plan's
fork, that points the reporter's entry CTD at a machine-specific variable rather than at Dunland
tournament data — consistent with Established #5 and with the clean data audit.

**The new instrumentation works.** 648 × `AgentEquipBegin` / `AgentEquipOk` / `AgentBuildDone`,
perfectly balanced, with `race=` / `monster=` / `actionSet=` populated.

**`from=` shipped broken and is now fixed.** The patch emitted `Type.Name` (short) while the
formatter filtered on namespaces, so every filter was dead and all four slots went to our own prefix
plus Harmony's generated wrappers:

```
from=Agent_..._Patch.CaptureSpawnOrigin <- Agent_..._Patch.Prefix
     <- .TaleWorlds.MountAndBlade.Agent.EquipItemsFromSpawnEquipment_Patch2
     <- .TaleWorlds.MountAndBlade.Mission.BuildAgent_Patch1
```

Fixed by passing full names, normalising `_PatchN` wrappers instead of dropping them (a wrapper
*replaces* the frame it stands for), de-duplicating, and raising the budget to 6 frames. Had it
worked on the first run it would have named `MissionAudienceHandler.SpawnAudienceAgents` and closed
this section a day earlier.

## Second crash — tournament FINISH (same session, in-house)

Distinct from the reporter's entry CTD; recorded here because it is the same mission type.

| | |
|---|---|
| Player | `Wynstan`, culture `vlandia`, Summer 1 1084 |
| Timeline | `BattlePlayable` 10:45:11 → match runs → `CareerSystem: Wynstan leveled up` 10:46:25 → death |
| Exit phases | **none** — no `ExitBegin`, so `Mission.EndMission` never ran |

The last managed line is a TAOM log, but `CareerCampaignBehavior.OnHeroLeveledUp` (`:95-108`) only
reads and logs — it mutates nothing. It is the timestamp of death, not its cause.

**There is no instrumentation between `BattlePlayable` and `ExitBegin`**, so the entire match-end
path — round resolution, the tournament UI's prize/winner tableaus, `MissionAudienceHandler`'s
end-of-match `Cheer(onEnd: true)` — is dark. That is the next blind window, and it is the one this
crash needs.

### Second in-house run — clean (`taom_debug_2026-08-03_12-08-46.log`)

Same town and scene; 429 agents; **full exit sequence completed** (`ExitBegin` →
`ExitTeardownDone` → `ExitStateFinalize*` → `MapResumed` → `FirstMapTick`), total 9.8 s, back on the
map. **So the finish CTD is intermittent — 1 of 2 in-house tournament finishes.** Whatever it is, it
is not deterministic on this machine, which makes the missing match-end instrumentation the binding
constraint rather than a nice-to-have.

**This run also drove a log-volume trim, and the format changed with it.** The file was 644 KB /
4,536 lines for 37 minutes. Two blocks were 93 % of it: the equipment dump (1,146 `slot=` lines
describing **18 distinct loadouts**, because a 429-agent audience draws from 9 kits) and
`[CultureMarketplace]` (1,687 lines, sustained ~30/min with no ceiling — nothing to do with either
CTD, and in a three-hour session it buries exactly the evidence a reporter is uploading). The dump
is now written once per distinct loadout and later agents carry `loadout=#N`; the marketplace line
is one daily digest. Replaying this log through the shipped key projects 644,215 B → 240,832 B.
**All three per-agent stamps are unchanged** — `AgentEquipOk` without
`AgentBuildDone` is the discriminator this investigation turns on, and it costs 145 ms for 429
agents. When reading any log dated after 2026-08-03: the stuck agent usually has no `slot=` block
beneath it, so follow its `loadout=#N` up to the block that does (`triage_battle_load.py` resolves
it for you). See `docs/features/battle-load-diagnostics.md` § "Phase 5c".

Patch60's regression canary is **green**: `ReleaseMovie=8897ms RemoveLayer=0ms`, gen0 +3
(`gc=3076/595/95` → `3079/597/97`). The documented known-good baseline is `ReleaseMovie=8,822ms`,
gen0 +3 (`rca-tournament-exit-hang-2026-07-06.md:49`) — a 0.9 % delta. #331 has not regressed.

**`from=` validated in live output**, and it independently confirms the audience explanation above:

```
from=Agent.EquipItemsFromSpawnEquipment <- Mission.BuildAgent <- Mission.SpawnAgent
     <- MissionAudienceHandler.SpawnAudienceAgents <- MissionAudienceHandler.OnInit
     <- MissionAudienceHandler.EarlyStart
```

agents #0–2 were `merchant_dunland` ('Trader'), `ransom_broker_dunland`, `townsman_dunland` — three
more draws from the same weighted spectator table.

## Suspects (unverified)

1. **`TaomTournamentModel.GetParticipantArmor`** (`Main/Features/Arena/Models/TaomTournamentModel.cs:64-71`).
   Diverges from vanilla `DefaultTournamentModel.cs:86-93` two ways: the
   `CampaignMission.Current.Mode != MissionMode.Tournament` guard is **gone** (vanilla substitutes
   practice-dummy gear only in arena *practice*; TAOM does it in real tournaments too), and the
   lookup is keyed on the **participant's** culture instead of the **settlement's**. TAOM's dummies
   carry a hard-coded `race=`, so the key is culture while the mesh constraint is race. The
   2-slot cloth kit in the crash log is consistent with dummy gear rather than tournament gear.
   Third delta: `ResolveDummyId`'s second argument is hardcoded `null`, making the
   settlement-culture fallback branch unreachable.
2. **`Patch46_TournamentDwarfDismount`** — the only other tournament-only equipment mutation; it
   writes the same `MatchEquipment` object as #1, from a `PrepareForMatch` postfix.
3. **`CareerPerkMissionBehavior.OnAgentBuild`** (`:111-119`) — loops to `NumAllWeaponSlots` (5), so
   it includes slot 4 (`ExtraWeaponSlot`) and issues a native `SetWeaponAmountInSlot` during agent
   build for hero agents. Same slot-4-at-spawn shape as RCA #360, different call site.

## Ruled out (checked, negative — do not re-litigate without new evidence)

| Suspect | Why it's out |
|---|---|
| **Banner bearers / RCA #360** | `BannerBearerAssignmentMissionLogic.cs:56-63` requires `MissionMode.Deployment` **and** a live `BannerBearerLogic`. A TournamentFight has neither, so `Patch63` is unreachable there. |
| **`ActionSetCode_GenerateActionSetNameWithSuffix_Patch`** | Byte-equivalent to v1.4.7 vanilla. Dead weight, not a defect. |
| **Dangling ModuleData refs** | All 84 empire-culture NPC refs, 65 party templates, 11 equipment rosters, every item id (TAOM + Armory + vanilla), all `BodyProperty.*`, all `SkillSet.*`, all `race=`, and the `arena_empire_a` scene resolve. `validate_moduledata.py`, `validate_mesh_refs.py`, `audit_scene_names.py` all green; deployed module byte-identical to the repo. |
| **The stale-tail theory** | Two independent agents proposed the Musician line was left over from a previous mission's loading window. The crash log has exactly one `MissionOpenNew` in the whole session. It is not. |
| **The "impossible agent" theory** | Mine, and wrong. `MissionAudienceHandler` spawns it. See above. The lesson: never reason about a mission's contents from `InitializeMissionBehaviorsDelegate` alone — `MissionView`s are added separately, and this mission has 65 behaviors, not 13. `MissionDiagnosticBehavior` already dumps the live list; read it. |

## Review

`/deep-review`, 6 agents, 2026-08-03 — 2 findings, both authored-in, both fixed; 1 HIGH refuted
(a recommendation to downgrade `AgentBuildDone` to DEBUG, which would have destroyed the stamp's
crash durability for a measured ~0.5 % of load time). Details:
[`rca-battleload-agentbuild-2026-08-03.md`](rca-battleload-agentbuild-2026-08-03.md), REVIEW-LOG #80.

## What shipped in response

`AgentBuildDone` — a postfix on the private `Mission.BuildAgent` — plus `race=` / `monster=` /
`actionSet=` and a bounded `from=` caller chain on the `AgentEquipBegin` line. See
`docs/features/battle-load-diagnostics.md` § "Phase 5b". This does not fix anything; it makes the
next log able to answer two questions this one could not: *did `BuildAgent` finish?* and *what
built this agent?*

## Still unknown

Which native site faulted, and what put a musician in that mission. Neither supplied log answers
either.

## Next

1. **In-house repro** — enter a tournament in an `empire`-culture town on a build with the new
   stamp. Reproducing here kills the poisoned-cache confounder outright (this machine's load timing
   has never poisoned the cache) and points at tournament data or an arena-path behavior.
2. **Ask the reporter** for the Windows Event Log **Fault offset** across 2–3 separate crashes
   (identical offsets = one site), any `taom_crash_*.zip`, and one question worth more than either:
   *the two sessions that end at the character-creation screen — did those crash, or did you quit?*
   If they crashed, the tableau path and the tournament path share a cause and Established #5 stops
   being a confounder and becomes the prime suspect.
   The artifact-request wording in the Rhûn investigation § Phase 1 is reusable verbatim.

## Side-findings (not the crash)

- **Five culture attributes TAOM writes are ignored by the engine.** `CultureObject.Deserialize`
  (`CultureObject.cs:280-345`) has no `armed_trader`, `gear_practice_dummy`, or
  `weapon_practice_stage_{1,2,3}`. TAOM sets all five on every culture.
- **Every Dunland practice fighter and practice dummy is dead content.** The engine builds those ids
  from the culture **StringId** (`"gear_practice_dummy_" + culture.StringId`,
  `DefaultTournamentModel.cs:90`; `ArenaPracticeFightMissionController.cs:370`). Dunland's StringId
  is `empire`, so the lookups resolve to the **vanilla Imperial** characters and TAOM's
  `*_dunland` versions are never loaded. Same for `dale`, `harad`, `khand`, `rhun` — each named for
  the lore culture rather than the StringId. Player-visible: Dunlending tournament and arena
  fighters wear Imperial armour.
- **`AutonomousMovementPlayerController` is registered twice** — `[DefaultView]` on the class *and*
  an explicit `AddTaomBehavior` (`SubModule.cs:1246`) ⇒ two live instances in every mission.
- **Six field-battle behaviors carry no mission-type gate** and run in tournaments:
  `AdvancedCombatBehavior`, `BehaviorTreeMissionLogic`, and the Warg / Spider / Elephant / Mumakil
  behaviors. Each walks `AllAgents` and hooks `OnAgentBuild`.
- **The reporter's build opens every log with a `[SaveDefiners]` ERROR** naming two *vanilla*
  assemblies and telling the user to disable a mod. Fixed at HEAD by `46ce6436`, which softened it
  to a warning; worth knowing when reading anything they send.
