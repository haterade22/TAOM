# Reviewing Kysaro's knowledge notes (2026-11-12)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: knowledge only.** Nothing was installed or run. TAOM took three evidence rules, four traps,
a scene-size section measured against TAOM_Map, a coupling section for Kysaro's modules and a set of
engine facts, each tagged with its evidence. Corrections to his notes went to him as
[PR #1](https://github.com/Kysaro/bannerlord-knowledge-Kysaro/pull/1).

Mike asked for both directions: build what TAOM lacks into TAOM, and send Kysaro a PR with everything
wrong in his notes. Kysaro is TAOM's lead scener and lead UI designer. `adoption-history.md` had no
entry, so this is a first review.

## Source, and what was read

`Kysaro/bannerlord-knowledge-Kysaro` at `f0a4370b5` (private, created 2026-11-12, two commits, no
licence file). It holds 80 notes that his `tools/sync_from_memory.py` copies from his Claude Code memory
folder, in eight topic folders: `ui`, `armor`, `animation`, `map-icons`, `scenes`, `gameplay`, `tooling`,
`lessons`.

| Depth | Files |
|---|---|
| Read in full (main session) | `README.md`, `tools/sync_from_memory.py`, all 28 `lessons/*.md`, `tooling/reference_*.md`, `scenes/project_calembel_siege_crash.md`, `gameplay/project_taom_kysaro_diplomacy.md` (coupling lines) |
| Read in full (three Sonnet readers, each claim checked against TAOM) | `animation/` and `armor/` (17 notes); `ui/` except one, `gameplay/`, `tooling/project_*` (12); `scenes/` except two, `map-icons/` (18) |
| Headings and engine sections only | `ui/project_taom_factionui_module.md` (514 KB of session logs), `scenes/project_terrain_generator.md`, `scenes/project_vertex_paint_module.md` |

Every load-bearing reader claim used below was re-checked in the main session against the v1.5.7
decompile or TAOM source before it was written into a TAOM doc or the PR.

## Security

Safe to learn from; nothing to install. The only runnable file is `tools/sync_from_memory.py`: stdlib
only, it reads his memory folder and deletes and rewrites the topic folders and README. No network
access, no dynamic code, no credentials, hooks, skills or package manifests. No secret in any note. The
notes carry his Windows username, local paths, a private `lotraom-assets` remote URL and part of an
ImagineArt org id; his own note says to ask before any remote goes public, and the PR repeats that.
`audit_claude_config.py --external` was not run: there is no Claude config or skill in the tree.

## What TAOM took

| Item | Where | Evidence |
|---|---|---|
| A readback from the layer that set a value is not verification | `.claude/rules/evidence-over-claims.md` B; lesson in `lessons/testing-qa.md` | his `feedback_verify_dont_assume_success.md` |
| A second rejection of the same result: stop patching, review the base | `evidence-over-claims.md` A.5; lesson | his `feedback_dark_lord_session_bad.md` |
| One dead mechanism is not proof the goal is impossible | `evidence-over-claims.md` C.4; lesson | his `feedback_dont_overgeneralize_native_constraints.md` |
| Scene-load scratch arena, with TAOM_Map measured | `features/battle-load-diagnostics.md` "Scene size"; lesson in `lessons/data-content-cultures.md`; pointer in `modding/module-map.md` "The prefab-entity cap" | his disassembly [Likely]; TAOM counts measured |
| Prefab names a metamesh the session lacks; Kit lowercases names | `reference/tpac-static-prop-authoring.md` "Names"; `/prep-tripo-prop` "Top traps" | his three crashes of 2026-10-29 |
| Blender MCP dry run: data-API copy, never `duplicate()` | `ai-includes/creature-animation-blender-mcp-workflow.md` section 8; `/prep-tripo-prop` "Top traps" | his 2026-10-10 incident |
| Windows PowerShell 5.1 UTF-8 round trip writes mojibake | `reference/powershell-tool.md` (CLAUDE.md links it) | his 2026-10-21 repair |
| QA renders hide what back-face culling shows | `/prep-tripo-prop` "Top traps" | his `feedback_baf_culled_split_review.md` |
| His modules' TAOM couplings and the duplicate-module hazard | `reference/external-mod-couplings.md` "Kysaro's modules" | TAOM side re-read by grep at `abc5d0172` |
| Engine facts (Tier 1 and Tier 2, Mike's choice) | see "Engine facts" below | per-fact tags |

No row was added to the `orientation.md` trap index: it was at its 47-row cap (`lint_docs.py` trap-count), and no merged row fit the 180-character row cap. Each trap lives in its owning doc and is reached from the doc or skill where that work starts.

## Scene size measured (the finding with the most risk)

His Calembel RCA found a fixed 32 MB native scratch arena behind a multi-thread access violation at scene
load; within one content mix the line was about 164,000 XML elements under `<entities>`. The TAOM_Map
count is in [battle-load-diagnostics.md](../features/battle-load-diagnostics.md) "Scene size":
Goblin Town (200,205, a settlement) and Ruined Outpost (171,596, in `sp_battle_scenes.xml`) sit past his
line, and TAOM's docs record no load test of either. His own note says the limit is cumulative "but not
along any metric measured", so a count is a warning sign, not a verdict. **Owed in game:** a Goblin
Town siege and a Ruined Outpost field battle, 3 of 3 each. Mike waived an issue on 2026-11-12.

## Engine facts

Added by an `implementer` agent from a brief that named each fact and its source, then spot-checked in
the main session. `[Certain]` only where the v1.5.7 decompile or TAOM source was re-read; every other fact
is tagged as Kysaro's measurement, unverified in TAOM.

| Doc | Facts | Tag |
|---|---|---|
| `reference/engine/gauntletui-viewmodel-screen.md` | UI debug mode hot reload and inspector; generated prefabs win over XML (TAOM sets `DoNotUseGeneratedPrefabs`, `Dependencies/SubModule.cs:42`); a `DataSource` beside `Command.Click` rebinds the command; `Attach()` before the VM constructor | his, except the TAOM line [Certain] |
| `reference/engine/scene-gameentity-scriptcomponent.md` "Editor-side navmesh and terrain API" | `TerrainEditContext` writers; `SetAbilityOfFaceWithIndex`, `SetAbilityOfFacesWithId`; region map indexed by `TerrainType` (`SandBoxHelpers.cs:425-432`); `GetNavMeshCenterPosition` by `ref`. His: SaveScene needs `SceneEditData`, the edit-data deadlock, decals, terrain data files, Gauntlet not drawn in the editor | API [Certain]; editor gotchas his |
| `reference/bannerlord-animation-system-map.md` | `act_cutscene_kingdom_made_pose_01` to `_06` (no `_07`) and `act_cutscene_declare_dragon_banner_pose_01` to `_15`, counted in Native `action_types.xml`, already bound by 22 Native `action_sets.xml` rows; his `SetActionSet` crash on a clip with no RDC entry; his stale `.rdc` freeze after a reimport | counts [Certain]; the rest his |
| `modding/body-properties.md`, `features/troll-race.md` | face cache keyed by face shape, not race; TAOM's trolls share one key (check owed); race `bone_scales` inflation | his; the shared key [Certain] |
| `reference/armory-guide.md` "Cloth simulation meshes" | quad ring grid, loose `clo_` mesh, alpha-0 pinning, the cloth mapping reimport fix | his |
| `reference/engine/mission-and-missionbehavior-lifecycle.md` | `CheckDeployment` and `IsPlanMade`; `Teams.Add` mid-battle; static lists; action set swap at spawn, with the open conflict against "Monster reads `action_set`" | his |
| `features/siege.md` | `DestructableComponent.OnHit` zeroes non-ram damage; a siege tower with no "wheel" entity | his |
| `reference/engine/mission-frame-threads-and-native-costs.md` | scene-notification gore needs a physics world; reset severed-head visuals before `EndMissionInternal`; the map icon distance conflict | his |
| `reference/unattended-game-runs.md` | `SaveAs` only queues; `HideInquiry` pauses the map; no campaign VM patch before `OnGameInitializationFinished`; launch line | his |
| `modding/items-armor.md` | body-slot cutoffs, the MeshesMask AND, the `.body` submesh | his |
| `kitbash/map-icons.md` | native and TAOM icon measurements as data, his authoring traps; the budget stays Mike's | his |
| `features/signature-strikes.md` | `KnockDown` on a hand-built blow, as an open question beside `CustomAttacksUtils.cs:320` | open |

Two of the brief's claims were wrong and were corrected by the agent: the dragon banner series has a
`_07` (only the kingdom-made series stops at `_06`), and his "particles crash the execution scene" is
superseded in his own later notes by "the scene had no physics world".

## Corrections sent to Kysaro (PR #1)

Fourteen files on branch `taom-corrections-2026-11-12` (commit `8e7ec13`), each fix a marked
"TAOM correction (2026-11-12)" line with its evidence: the TAOM name (Tales From the Age of Men); the
`lotraom-assets` mirror is a copy, not the ModuleData source; TAOM_FactionUI merged (#704); the default
branch is `bannerlord-1.5.x`; 25 character-creation equipment sets; main-menu paths that never existed in
`haterade22/TAOM`; navmesh region map indexed by `TerrainType` and the per-face disable API; terrain
writable since 1.5.3; the `bo_` convention; Armory cloth sims and the `_s` packing; orc, goblin and troll
animation state. The PR body warns that his sync script will overwrite the fixes unless he copies them
into his memory files.

**Open questions in the PR:**

1. TAOM_CustomBattleUI: his note of 2026-11-10 keeps it out of TAOM, and TAOM integrated a muster screen
   with his `Tcb*` names today (`df8ba8484`). His note says loading both stacks screens.
2. The exact metric behind his 164k line.
3. Map icon visible distance: his `FLT_MAX` against TAOM's 200
   ([mission-frame-threads-and-native-costs.md](../reference/engine/mission-frame-threads-and-native-costs.md)).
4. `BlowFlags.KnockDown` on a hand-built blow: his "ignored" against TAOM's use in `CustomAttacksUtils.cs`.
5. The face cache keyed by face shape: TAOM's hill and cave trolls share `BodyProperty.fighter_cave_troll`.

## Skipped, with reason

- His Bannerlord Armor Factory, Tripo credit rules, armour viewer and map-icon kit status: his own tools
  and sessions, with no engine fact beyond those taken above.
- His aesthetic verdicts (palettes, no own trees, organic settlements): his user's taste, not TAOM's.
- The Warhammer III matched kills: marked personal use only, derived from Creative Assembly data.
- "Autonomous means keep going": `think-before-coding.md` "Open-ended work" already covers it.
- "Don't edit TAOM's ModuleData": AGENTS.md "Preserve others' work" covers it for TAOM sessions.
- Small stale status lines inside his session notes (Calembel, map-icon research): his record to keep.

## Owed

- In game: Goblin Town siege load, Ruined Outpost battle load, the troll face cache. No issues
  (Mike, 2026-11-12).
- Kysaro's answers to the five PR questions; fold each answer into the doc that holds the question.
