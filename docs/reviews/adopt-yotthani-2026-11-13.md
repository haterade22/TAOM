# Adopting yotthani's delta of 2026-11-13: crowd figures, the big-battle question, the folders earlier reviews had skipped, and one TAOM defect

Written for: TAOM maintainers deciding what to take from yotthani's two repositories, and for yotthani (the
"For yotthani" section is meant to be forwarded). Procedure: `/upstream-review`, the tenth review of these sources
(the ninth of MithrilForge, the seventh of `yotthani/bannerlord`), after
[adopt-yotthani-2026-10-29.md](adopt-yotthani-2026-10-29.md). The request had two parts. First: yotthani had told
Mike of a large battle gain from replacing the engine's agents with the mod's own units ("up to 10 k units", no
engine patch); find what that is and how it works. Second, added during the run: review both repositories for any
performance or other optimisation TAOM can use, and above all for knowledge (lessons, notes). TAOM is on Bannerlord
v1.5.7 and v2.0.38.

**Summary.** No upstream code was taken. The own-agent method is not in either repository: what is pushed is the
crowd figure system TAOM first read on 2026-10-15, unchanged since, in which every figure mirrors one real engine
agent, so it cannot pass the 2,040-agent cap. The battle of 8,000 against 8,000 that yotthani's notes mention is a
colleague's, made with a native patch. The knowledge part was the larger result. A completeness check found that
the review's area briefs list only folders that changed, so folders that the earlier reviews had named and skipped
(the 2026-09-30 pass was scoped to DualWield and FaceLearner, and the 2026-10-08 record lists several as "separate
mods, not optimisation"), or had read only in part, stayed outside this review too; a second pass read them. The verified facts went into the owning TAOM docs, each with its source, evidence kind and
engine version, and with TAOM's own v1.5.7 reading where a verifier made one. The verifiers refuted or narrowed
more than ten reader claims, and one of them found a TAOM defect that no upstream text names: the Custom Battle muster
screen cannot start a field battle as "Captain" (#1184).

## Sources, and what was read

| Source | Commit range | Read |
|---|---|---|
| `yotthani/bannerlord` (private, shared with Mike by its author; MIT by the maintainer's statement of 2026-10-09) | `6265f4a2` to `c45d837487f840a499391c583a534b4be27af1a4`, 438 non-merge commits | The delta: `HoN/RaceArmorFit`, `HoN/DwarfScaleTest`, `HoN/SmithingExtended`, `HoN/RingSystem*`, `HoN/CampaignProbe` 0.3, `HoN/DualWield`, the smithing research docs, the sim app specs (engine-facing parts). The whole current state, not only the delta: `HoN/VanillaTuning`, `HoN/PerfProbe` (with `bigbattle/`), `HoN/ShaderCacheKeeper`, `HoN/SaveBench`. Folders that earlier reviews had named and skipped, or read only in part: `docs/realm-ui/research`, `RealmUI`, `HoN/TAOM_UI`, `BannerlordThemeSwitcher`, `HoN/TAOM_RacePortraits`, `TAOM_POI`, `LOTRAOM_FactionMap`, `bn faces` notes, `HoN/docs`, and the nineteen HoN battle and campaign modules, compared with TAOM's ports |
| `yotthani/MithrilForge` (private, MIT) | `d09f44a4` to `ea7a7bfb9359029ebd76cad665376db55811a350`, 129 non-merge commits | `docs/engine/crowd-rendering.md` (new on main), the +22 lines of `docs/engine/modding-kit.md`, `src/MithrilForge.RaceFit`, `src/MithrilForge.Crowd` (refit, cloth, fields), `src/MithrilForge.Tpac*`, `out/`; and a heading-level coverage audit of every `docs/engine` and `docs/perf-audit` document against TAOM's docs |

Nine Sonnet readers went through the areas (crowds and big battles; MithrilForge knowledge coverage; the
performance fixes and probes; the `bannerlord` delta; the MithrilForge tool delta; working practices; UI; notes
and lessons; the ported modules), with a completeness critic between the two passes. Two Opus verifiers then
re-read 33 load-bearing claims against the pinned files, the installed v1.5.7 DLLs, the v1.5.7 native client
(Ghidra) and TAOM at `15b0427ca`, and tried to refute each. The main session spot-checked the readers' and
verifiers' main TAOM-side statements against the code.

Not read, by rank, for a later pass: the RealmUI and TAOM_UI widget bodies; most `bn faces` checkpoint files and
the ML docs; `ui-inventory.md` sections A.2 to C; the forge and wear specs of the smithing work; MithrilForge
`wave0-plan.md`, the raw profiles and `tools/physx`.

## Security

- **Safe to learn from:** yes. Both repositories were read through blobless clones, read-only; nothing was checked
  out, built, installed or run, and upstream text was treated as data.
- **Instruction files:** yotthani's plan files open with a line that directs the reader's AI to a third-party
  plugin, `HoN/skills-lock.json` pins a third-party skill from GitHub, and one settings file holds wide shell
  allow-lists. None was followed or fetched. `HoN/BannerlordCommonLib/Sharing/DataSharing.cs` uploads data to
  GitHub gists, a Discord webhook and a paste site: not adopted.
- **Licences:** MithrilForge is MIT. `yotthani/bannerlord` is MIT by the maintainer's statement of 2026-10-09 and
  still has no root `LICENSE` file (asked for again below).
- **Comparison only:** no code, data or asset was taken, so the provenance rows stay `comparison-only`.

## What TAOM verified (2026-11-13)

| Claim | Verdict and evidence |
|---|---|
| The mod replaces engine agents with its own units | Not in the repositories. `CrowdProxy.cs` keeps every real agent for movement, AI, hits and death, hides it, and draws a figure in its place (class summary, lines 13-17). The file is byte-identical between `784fef93` and the head. No own-unit simulation in the `*.cs` of six folders, and no branch ahead of main has a commit subject about one (branch contents were not read) |
| "The engine handles everything related to agents in a single thread" (as relayed to Mike) | Not so for the managed tick on v1.5.7: `Mission.TickAgentsAndTeamsImp` (`Mission.cs:3611-3622`) runs `Agent.TickParallel` on the worker pool, then a serial `Agent.Tick` loop, then `Team.Tick`. yotthani's notes put the tick on a task thread (a code review of 02.10.2026) and the agent render items on about 13 workers (a profile) |
| The agent cap | 2,040 on v1.5.7: `IMBAgent.GetMaximumNumberOfAgents` at RVA `0x6E7060` returns `0x7f8` |
| The agent-less figure API | Present on v1.5.7. `CreateAgentSkeleton` and `CreateSkeletonWithActionSet` are extension methods in `GameEntityExtensions`, `FillAnimationSystemData` in `MonsterExtensions`; TAOM's docs used the same member shorthand; this review corrects the ones the batch touches |
| Harmony's `__originalMethod` costs a lookup on every call | Confirmed for the 0Harmony 2.4.2 TAOM ships, finalizers included. Two TAOM save-load diagnostics pay it once per object and six times per container; the cost on this runtime is not measured |
| ButterLib replaces `TypeExtensions.IsContainer` | Confirmed in the copy in `TAOM.Dependencies`; whether it is active on v1.5.7 is UNVERIFIED |
| Weapon damage factors derive from weight and length | **Refuted on v1.5.7:** `WeaponComponentData.SetDamageFactors` computes that term into a local that is never read. Cut 0.4, Pierce 0.32, Blunt 0.8, and 1 for ranged and ammo. Upstream read v1.5.6, which TAOM did not check |
| `ItemObject`'s copy constructor shares `ItemComponent`; `RegisterObject` renames a taken id | Both confirmed on v1.5.7. No TAOM code copies or registers items at run time |
| Menu options added in `OnGameStart` are dropped | Mechanism confirmed. All 12 TAOM options on the four vanilla settlement menus are added from `OnSessionLaunched` listeners (Grep of `Main/`, 2026-11-13) |
| A party "at sea" ticked on TAOM 2.0.36 | Does not refute Patch57's premise: v1.5.7 `AiHourlyTick` returns at once for a party that is not at sea. The upstream log came from v1.5.6 with 26 modules (27 with RealmUI) and a probe that is not in the repository, so the evidence is weak and the at-sea count stays open |
| `bone_type` binds by position | Partly: native `FUN_18056c5a0` matches `bone_type` against a 28-entry name table and stores the position. Whether that number is then the skeleton bone index was not read |
| The three TAOM map cost shapes yotthani measured on 2.0.32 | All three still exist in today's code. The walk over all settlements at an election is vanilla's; TAOM adds one pass over the clan's own settlements |
| TAOM's shared `Monster` eye-height write is a crash cause | **No defect shown:** eye heights reach native as a value copy at spawn, and TAOM writes one clamped value to the base `dwarf` monster on main-thread paths. The source itself calls the cause "suspected" |
| The engine knows a run-time texture by file name | **Refuted at the load call:** the native helper at `0xA0400` caches by name and full path. The swap yotthani saw is unexplained |
| A large XML comment beside a custom component drops the settlement | Real, with a different mechanism: a comment that is a direct child of `<Components>` becomes a null component, and `LoadXML` swallows the exception. The live TAOM_Map has 59 comments, none inside a `Settlement` |
| The nameplate fade needs all eight widgets | Confirmed on v1.5.7; TAOM's three nameplate prefabs bind all eight |
| The four GUI override rules (`OverrideBrush`, per-entry SpriteData merge, prefab names ignore folders, a duplicate `.fnt` throws) | Confirmed on v1.5.7 |
| Couplings into TAOM from yotthani's newer modules | Confirmed; `external-mod-couplings.md` lacked them. `WarPhase` has a fourth value, `WarEnded` = 3, that the upstream source comment does not list; how RingSystem treats 3 was not read |
| The save base id 726900601 keeps the original mod's formation presets importable | **Refuted** by the code review: TAOM reads the key `TAOM_FormationPresets` and the original mod wrote `FormationPresets`, so nothing is imported. The id protects TAOM's own saves: with another base id the preset list of an older save loads as null and TAOM substitutes an empty list (Codex's source trace on v1.5.7, not run) |
| EquipPresets saved by the original mod import into TAOM | Not as the layouts stand: the behaviour name, the key, the base id and the class ids match, but `HoNPresetItemReference` saves `SlotIndex` as property 1 and `ItemStringId` as property 2 in TAOM and the other way round in the original (`HoN/EquipPresets/Data/HoNPresetItemReference.cs`@c45d8374, the 2026-04-29 release). What the engine does with that on load is UNVERIFIED |
| An upstream fix is missing from TAOM's ported modules | None found. The one real fix after the ports (a wrong parameter name on the cavalry postfix) is in TAOM's Patch31 |
| The skeleton buffer guard after the 1.5.6 hotfix | Not exposed: TAOM finds the sites by byte pattern, and `SkeletonBufferInstalledBinaryTests` pins v1.5.6.125270 and v1.5.7. A v1.5.7.125802 session on 2026-11-13 logged both guards ON (`taom_debug_2026-11-13_15-50-23.log`, lines 42 and 43); that shows the install, not a caught overflow |

## Decisions

| Item | Decision | Where |
|---|---|---|
| The Custom Battle "Captain" start | Issue filed; a code fix with its own review. Found by the verifier while checking how Custom Battle finds a sergeant's commander: `TcbVM.cs:123` passes a null general and v1.5.7 `BannerlordMissions.cs:144` dereferences it. Read from code, not run | #1184 |
| Patches that switch themselves off at run time | Issue filed: five feature latches and thirteen degrading transpilers never reach the player. yotthani's `PatchHealth` is the precedent | #1185 |
| The module id, the enum values other mods read, and the formation preset save base id | Two test classes built beside this record: `ExternalModContractTests` (module id, `FactionSide` and `WarPhase` by value) and `FormationPresetSaveableTypeDefinerTests`. Reviewed by one Claude lens and one Codex pass; see [rca-external-mod-contract-tests-2026-11-13.md](rca-external-mod-contract-tests-2026-11-13.md) | #1186 |
| EquipPresets property ids against the original mod | No code: TAOM's own saves have used TAOM's layout since 2026-05-07, so it stays. The couplings doc says the import is not supported; an issue holds the decision | #1194 |
| `__originalMethod` on the save-load finalizers | No code now: about 55 ms and 155 MB of garbage per load if the save-side count of 646,000 objects held on load (TAOM's inference; the load-side count is not measured), against seven new patch classes. The Harmony doc records the fix shape and "measure first" | `submodule-lifecycle-and-harmony.md` |
| The focus-ray throttle | Unchanged from 2026-10-08: measure first. yotthani's own figure is 0.2 to 0.3 ms a frame on 1.5.3 under foreign load | this record |
| Working practices | One taken: check the machine for foreign CPU load before a timing run. Compare-by-fight-phase waits for a measurement of TAOM's own run-to-run spread. The rest is equal to or weaker than what TAOM does | `perf-method.md` |
| Knowledge items | Taken, as restated and credited facts in the owning docs | the docs this batch touches |

## Not taken

| Item | Reason |
|---|---|
| The crowd figure system and its baker | yotthani's research product, unchanged since 2026-10-15. TAOM now holds its measured costs and its traps; a TAOM spike of hidden agents plus figures is Mike's decision |
| "Limitless" (a third-party native patch for more than 2,040 agents; by Bloc, per Mike, 2026-11-13) | Not examined. A one-off source goes through `/adopt-external` with its own security pass |
| RaceArmorFit, SmithingExtended, RingSystem, RealmUI, TAOM_UI, the sim app | yotthani's products. Only their engine facts and lessons are restated |
| `save-fast`, `save-no-forced-gc`, `war-score-cache`, `particle-step-chunk`, `battle-size` | Declined before, and nothing new: yotthani retired the particle chunk patch, and the save rewrite measured slower in game |
| The RBM damage pipeline, the balance tuning, RAFS/RAFF/RAFI formats | Not facts about the engine or TAOM |
| Plan and spec ceremonies (review-focus lists, coverage lists, decision markers) | Text without a gate; TAOM's plans already carry stop conditions and a drift check |

## Findings for Mike (no code here)

1. **The own-agent method is not pushed.** If yotthani shares a branch, it is one more area for this procedure.
   The questions to send are in "For yotthani".
2. **In-game checks owed:** Captain plus Battle on the muster screen (#1184); Escape and Back in the faction
   map's culture stage; the first seven seconds of a 1,000-soldier fight in the heartbeat (yotthani saw that phase
   about 1.6 times slower, game 1.5.3 and 1.5.4, one to three runs per size).
3. **`mordor_ashes`** in the live TAOM_Map is built like the Black Gate ash prefab of #738 (`emission_rate` 6000,
   a 500 by 1000 m box). No text file in TAOM_Map names it, so no scene places it by name. Check it before one does.
4. **Enum order is a contract.** RingSystem and TAOM_UI read `WarPhase` and `FactionSide` as integers. A reorder
   breaks those mods without a build error; `external-mod-couplings.md` now says so.
5. **Peace before Full War** between Free and Shadow realms is still open, as `diplomacy.md` already says. The
   verifier found no barter hole: `PeaceBarterable.Apply` goes through the `MakePeaceAction` that TAOM prefixes.
6. **Worktrees:** `git worktree list` showed more than 30 on 2026-11-13, this review's among them; the session
   banner asks for a prune above 15.
7. **Two TAOM comments claim a save import that does not happen:** `FormationPresetSaveableTypeDefiner.cs:9-10` and
   `SaveDefinerCollisionDetectorTests.cs:15-17`. Both are outside this change; #1186 lists them.

## The doc review

Four finders read the knowledge batch (58 docs and this record): two for fidelity to the source, one for
consistency with TAOM's code and docs, one for routing and form. They reported 11 HIGH, 40 MEDIUM and 32 LOW
findings. An Opus verifier then re-read every HIGH and MEDIUM finding against the pinned upstream files, the v1.5.7
engine and the worktree. Verdicts: all 11 HIGH confirmed, 37 MEDIUM confirmed, 4 partly, none refuted in whole; it
added five of its own. Every confirmed and partly confirmed fix is applied. The ones that changed a conclusion:

- **A reversed cause:** the crowd notes said hiding the engine's visuals object made helmets flicker. The source
  says the visuals object drew over the real soldier, and hiding it every frame is the fix (`CrowdProxy.cs`@c45d8374
  lines 1833-1837).
- **Citations that pointed at the wrong place:** seven facts cited a file or line range that does not hold them
  (a file with 146 lines cited at 174-190; string-table rows cited for a load-order rule; three figures cited to one
  file that come from two others). Each now cites the line that was read.
- **A quoted log string that does not exist** in the pinned repository ("pair missing"); without a pair the upstream
  code returns silently. The row now says that.
- **Hedges made stronger on the way:** "acceptable" for a variant that is only a plan; a `z` clamp that covers only
  width and depth; "the fix that held" for a crash that the same release notes say stayed; "retracted" for "not
  reproduced"; and, in this record, three sentences of the main session's own.
- **Game versions the sources do not state** were removed or marked as not stated (the POI how-to, the big-battle
  figures, DwarfScaleTest, which has no README at all).
- **`MissileBlockedWithWeapon`:** two docs disagreed. On v1.5.7 a missile blocked with a weapon sets no damage and no
  absorbed value and registers no blow in the managed branch (`Mission.cs:5861-5864`); the "filled" values belong to
  a shield block. Both docs now say that, with one owner.
- **Two trap lines moved:** the settlement comment trap sat in a sheet that never loads for the live
  `settlements.xml`, and went into the existing orientation row; a patch-category line duplicated
  `harmony-patches.md` and was removed (that rule file is at its size cap, so the detail stays in
  `lessons/harmony-il.md`).
- **An owed check that was already done:** the docs said no session had run on v1.5.7. Today's log shows both
  skeleton buffer guards on (`taom_debug_2026-11-13_15-50-23.log`, lines 42 and 43).
- **Pronouns:** 23 added lines used "he" or "his" for yotthani; all now use the name.

Not done by the doc review: the LOW findings the verifier accepted on the finder's quoted evidence were applied
without a second read at the source. The code review of #1186 is in
[rca-external-mod-contract-tests-2026-11-13.md](rca-external-mod-contract-tests-2026-11-13.md).

## For yotthani

Thank you for the crowd rendering document: the cost table, the list of what does not work and the dated crash
notes saved TAOM from repeating each of them. Thank you as well for the RaceArmorFit ledger of mesh edits that
crash and the POI notes on settlements. TAOM restated what it could check, credited to you in each doc. What we
would add, in case it helps:

1. **The battle with your own units.** Mike passed on that you replace the engine's agents with your own in battle
   and reach about 10,000 units. We could not find that code in either repository: `CrowdProxy.cs` mirrors one real
   agent per figure, and the 10,000 we found in the docs are draw tests (the flip book, and 10,000 meshes with their own morph
   time, which hung the game). Is it on a branch you have not
   pushed? And does the figure mean units drawn, simulated or fighting?
2. **`SetDamageFactors` on v1.5.7** no longer uses weight and length. The term is computed into a local that is
   never read (IL `IL_00ce` to `IL_00df`); the factors are 0.4 for cut, 0.32 for pierce, 0.8 for blunt and 1 for
   ranged and ammo. The sim app's 0.4 checks note
   (`docs/balance-sim/app-0.4-checks.md`, lines 64 to 65) still derives them from weight and length, as the 1.5.6
   decompile does.
3. **The texture swap.** On v1.5.7 the native load-from-path helper caches by name and full path, so two files
   with one name in two folders should not collide there. We could not find what caused what you saw; on our side it stays
   open. The two files do share `Texture.Name`.
4. **The dropped settlement.** The cause we found is a comment as a direct child of `<Components>`: it becomes a
   null component, and `MBObjectManager.LoadXML` swallows the exception, so that settlement and every later one stay
   unready. The size of the comment and the position of `component_name` do not matter.
5. **The prefab named like the settlement id** is the fallback for a settlement with no entity of that name in the
   map scene (`SettlementVisual.cs:371-376`), not a requirement in every case.
6. **The dwarf eye height.** TAOM's write reaches native as a value copy at spawn (`MonsterExtensions.cs:134-146`),
   so the shared-instance crash your race portraits spec suspects does not follow from it on v1.5.7.
7. **`WarPhase` has a fourth value**, `WarEnded` = 3. RingSystem reads the phase as an integer; its source comment
   lists 0 to 2 (`TaomHunterWarProvider.cs`, line 39), and we did not read what it does with 3.
8. **`menu_groups.json`** lists `taom_sl_order` beside `taom_sl_order_supplies`; only the second is an option id now.
9. **Two counts of TAOM's UI disagree** in your own docs: 49 prefabs and 269 brushes in `ui-inventory.md`, 73 and
   352 in the census.
10. **EquipPresets save layout.** TAOM's port saves `SlotIndex` as property 1 and `ItemStringId` as property 2 of
    `HoNPresetItemReference`; your release of 2026-04-29 has them the other way round, under the same save id. TAOM
    keeps its layout (its saves have used it since May), so presets saved by the standalone mod are not
    import-compatible. Your guard that switches the standalone mod off beside TAOM is the right protection.
11. **The asks of 2026-10-15 stand:** a root `LICENSE` file in `yotthani/bannerlord`.

Thank you again.
