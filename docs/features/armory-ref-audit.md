# Armory Reference Audit

## Overview

One command that answers "did the last art drop break anything the game names?", written to a
committed report so the answer has a history. `tools/audit_armory_refs.py` composes four engines
that already existed and had to be remembered separately: the mesh and collision-body validator,
the generated Armory catalogue and its rename-aware diff, the deleted-mesh impact join
(item to troop, five reference shapes), and the ModuleData item registry. Its report is
`docs/audits/armory-ref-audit.md`. The 1.4.x line has no skill and no startup trigger for it (the
1.5.x `/armory-audit` skill and `ARMORY ART DRIFT` session-start line were not ported): run it after
any Armory sync or install.

## Why This Exists

On 2026-09-11 an Armory art drop (mirror commits `24ccacee` and `0244af88`, synced into the
install on 14 Sept) re-exported the elven bows from `weapons/bow/High Elven/wm_elven_bows_geo.tpac`
(`wm_elven_bow_v1..v4`, bodies `bo_wm_elven_bow_v1..v4`) into
`elven_weapons/wm_elven_bows_a_geo.tpac` (`wm_elven_bow_a01..a04`, `bo_..._a01..a04`) and
re-exported the three Swan Knight spear flags onto one `gondor_flags` atlas as
`wm_gondor_flag_a01..a04`. Nothing repointed the XML. Eighteen `body_name` refs and fourteen
`mesh` refs across `LOTRAOM_weapons.xml`, the Rivendell starter kit, three ranged ladders and the
crafting pieces kept the old names.

A missing collision body is not a visual defect. `PreloadHelper.WaitForMeshesToBeLoaded`
(`TaleWorlds.MountAndBlade.View`) is

```
do { num = sum(mesh.CheckResources()) + count(PhysicsShape.GetFromResource(name, true) == null); Thread.Sleep(1); }
while (num != 0);
```

so one body name the engine cannot resolve spins the game-loop thread forever, on the first frame
after the loading window drops, with no error and `Process.Responding` still true (the window
pump is another thread). `ArenaPreloadView` feeds it every battle equipment set of
`TournamentBehavior.GetAllPossibleParticipants()`, and `FightTournamentGame.GetParticipantCharacters`
adds `CharacterObject.PlayerCharacter` first, unconditionally, so an elf player carrying
`starter_highelf_longbow` hung the Rivendell tournament every time while a human in the same
process played it (#599, dump-confirmed via `procdump -ma` and WinDbgX `!clrstack`). The
campaign preload views call the same wait, so any battle fielding the 31 affected troops would
have hung too.

Every gate that would have caught it existed. `validate_mesh_refs.py --scan-bodies` names all 32
in three seconds. A session had even run it on 2026-09-13 and written the exact numbers into a
memory note, with no issue and no fix. The Armory is unversioned in this repo, so a sync touches nothing the commit hook can see, and the
gates had to be remembered. This feature makes them one command and one report, and the validator
carries the hang class as an error.

## Architecture

### Design Challenge

Four tools, four scopes, four invocations, and each was right about its own question and silent
about the others: the mesh validator does not know which troops carry an item; the catalogue diff
flags a deleted row as "referenced" from the flag at the last regen (it kept saying "18 will
break" after the 18 were repaired); the ModuleData validator resolves item ids but never meshes;
the generators' `--verify` is per generator. And nothing ran any of them after a sync.

### Solution Approach

- **One tool, four engines, no re-derivation.** `audit_armory_refs.py` imports the engines and
  composes their outputs: `validate_mesh_refs.extract_refs` + `build_present_set` + `classify`
  (Tier B meshes, Tier C PhysicsShapes) over the whole Armory ModuleData plus this repo's;
  `generate_armory_catalogue.build_rows` + `classify_changes`, with the referenced flag re-derived
  from the LIVE refs; `audit_deleted_mesh_impact.sweep_consumers` + `resolve_roster_hop` to join
  every broken item to every troop, lord, roster and config that carries it, directly or through
  a standalone `<EquipmentSet id>`; `taom_schema.build_registries` for dead `Item.<id>` refs; the
  two scaffolders' `--verify` as subprocesses.
- **A committed report.** Deterministic markdown (sorted rows, run date and HEAD in the header) at
  `docs/audits/armory-ref-audit.md`, so `git diff` after a re-run is the change log. Verdict
  `CLEAN` or `BROKEN`; generator drift is a WARNING with its `DRIFT:` lines, because a verdict that
  stays BROKEN over a scaffolder disagreement trains readers to ignore it.
- **The validator carries the hang class, on the line that has plan 108 Step B4.** That step is held on the 1.4.x line (D27 rule 8): `validate_moduledata.py` here emits neither code below, the hook has no such `--code` line, and the tests this page names for them are absent. Until it lands, `tools/audit_armory_refs.py` (below) and the Patch90 runtime guard carry the class. What B4 brings: `validate_moduledata.py` gained
  `MISSING_COLLISION_BODY` (ERROR) and `MISSING_VISUAL_MESH` (WARNING), emitted from the same
  `validate_mesh_refs` engine, skipped never faked without the install, and a run that found no
  tpacs to scan is itself a finding. The commit hook's `--code` allowlist carries the ERROR;
  `CommitGateCoverageTests` now scans both emitting files so the next code cannot be added without
  its hook line. **Correction, 2026-09-18 (#622):** `main()` never called the pass, so the hook line
  blocked nothing from 2026-09-15 to 2026-09-18. It is wired now, `CommitGateCoverageTests` checks
  that every `*_issues` pass is reached from `main()`, and the MCP tool and `/verify` still do not
  run it (#623). `COLLISION_BODY_BORROWED` (ERROR, #633) followed: a body that is another kit's `bo_`
  twin resolves, so `MISSING_COLLISION_BODY` passes it ([moduledata-validation.md](moduledata-validation.md)).
- **A cooked install skips the catalogue (1.4.x).** `generate_armory_catalogue.build_rows`
  inventories the loose `Assets/` tree only, and the public 1.4.8 release ships the Armory cooked
  (`AssetPackages/` only). On such an install the drift section says SKIPPED with the reason, the
  verdict comes from the other checks, and `--regen-catalogue` is refused, so the committed
  catalogue is never overwritten with an empty one (`has_loose_tree`, `Summary.catalogue_checked`).

### Component Diagram

```
after any Armory sync, by hand  ---------------------------->  tools/audit_armory_refs.py
                                                                    |-- validate_mesh_refs (Tier B + C)
                                                                    |-- generate_armory_catalogue (rows + diff)
                                                                    |-- audit_deleted_mesh_impact (item -> troop join)
                                                                    |-- taom_schema.build_registries (item ids)
                                                                    '-- generate_ranged_ladder_items / generate_starter_kit --verify
                                                                    v
                                                    docs/audits/armory-ref-audit.md  (committed)
validate_moduledata.py  --MISSING_COLLISION_BODY-->  check-moduledata-validation.sh  (MCP, /verify: #623; plan 108 Step B4, held)
```

## Configuration

None. Paths default to the install (`BANNERLORD_GAME_DIR`, then the E: literal), the
`LOTRLOME_Armory` module, and `Main/_Module/ModuleData` as the consumer side; all overridable on
the CLI.

## Key Files

| File | Role |
|------|------|
| `tools/audit_armory_refs.py` | the composer; `flag_breakage`, `join_impact`, `render_report`, `exit_code` are pure |
| `tools/tests/test_audit_armory_refs.py` | ten synthetic tests, no install |
| `docs/audits/armory-ref-audit.md` | the committed report |
| `tools/validate_moduledata.py` (plan 108 Step B4, held) | `missing_collision_body_issues`, `borrowed_body_issues`, `BODY_CODE`, `MESH_CODE`, `BORROWED_BODY_CODE`, `_loaded_tpacs` |
| `tools/tests/test_validate_moduledata.py` (plan 108 Step B4, held) | `MissingCollisionBodyTests`; `CommitGateCoverageTests` scans both emitting files |
| `.claude/hooks/check-moduledata-validation.sh` (plan 108 Step B4, held) | `--code MISSING_COLLISION_BODY`, `--code COLLISION_BODY_BORROWED` |
| `tools/validate_mesh_refs.py` | the engine: `extract_refs`, `tpac_paths_for_modules` / `module_tpacs` (loose tree first), `build_present_set`, `classify` |
| `docs/reference/armory-catalogue/catalogue.tsv` | the baseline the drift is measured against |

## Dependencies

`tools/validate_mesh_refs.py`, `tools/generate_armory_catalogue.py`,
`tools/audit_deleted_mesh_impact.py`, `tools/taom_schema.py`, `tools/_gamedir.py`. Stdlib only.

## Tests

- `python -m unittest tools/tests/test_audit_armory_refs.py` (10): live-ref flagging beats the
  committed flag; direct and roster-hop consumers both appear; the report names body, item and
  troop on one row and is byte-stable; exit code semantics; generator drift warns; a cooked install
  skips the catalogue and refuses `--regen-catalogue`.
- Once plan 108 Step B4 lands: `python -m unittest tools.tests.test_validate_moduledata.MissingCollisionBodyTests
  tools.tests.test_validate_moduledata.CommitGateCoverageTests` (11), and
  `python -m unittest tools.tests.test_collision_body_borrowed` (14).
- `bash tools/test_hooks.sh` covers the hook's timeout and inner bound.
- Negative proof on real tpacs (2026-09-15): a probe item with `body_name="bo_this_body_does_not_exist"`
  produced exactly one `MISSING_COLLISION_BODY` ERROR naming the body, the item and the file.

## How to run the audit after a sync

```bash
python tools/audit_armory_refs.py --regen-catalogue   # loose tree only; refused on a cooked install
git diff docs/audits/armory-ref-audit.md            # what changed since the last audit
```

Repair the ref side (never restore a tpac beside its replacement), re-run, commit the report and
the catalogue with the repair. The #599 repair script (`E:\taom-hang-2026-09-15\repoint_refs.py`)
is the shape: byte-exact substring swaps, dry run by default, applied to the live Armory and, at the time, the `lotraom-assets` mirror.

## Performance

About 15 s against 4,497 tpacs (TOC scan only) including the two generator subprocesses; 3 s
with `--no-generators`. The validator's pass adds about 3 s to `validate_moduledata.py`: about
5.9 s without it, about 9 s with it, measured 2026-09-18 once #622 wired it in (the 4.8 s recorded
on 2026-09-15 was the validator without the pass, which never ran then). Inside the commit hook's
45 s inner bound. These figures are from the 1.5.x desktop's loose tree; the WP108 commits record
the laptop's times on its cooked install.

## Changelog

- 2026-11-04: ported to the 1.4.x line (WP108) from `05bd0a730`, with the loose-tree-first package
  list (`30abf550b`) and a cooked-install guard for the catalogue section (new on 1.4.x).
- 2026-09-15: created with #599. Repairs shipped the same day: 32 elven bow refs and 3 Swan
  Knight banner pieces repointed; first report CLEAN with one pre-existing starter-kit drift.

## GitHub Issue

#599. Mechanism first seen as #352 (2026-07-16). The runtime guard is Patch90
([preload-body-guard.md](preload-body-guard.md)), the only protection for an install that already
has a bad pair.
