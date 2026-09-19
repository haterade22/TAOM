# Armory Reference Audit

## Overview

One command that answers "did the last art drop break anything the game names?", written to a
committed report so the answer has a history. `tools/audit_armory_refs.py` composes four engines
that already existed and had to be remembered separately: the mesh and collision-body validator,
the generated Armory catalogue and its rename-aware diff, the deleted-mesh impact join
(item to troop, five reference shapes), and the ModuleData item registry. Its report is
`docs/audits/armory-ref-audit.md`; its skill is `/armory-audit`; its trigger is the
`ARMORY ART DRIFT` line `session-start.sh` prints when a `*_geo.tpac` under the live Armory is
newer than the committed catalogue.

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
memory note, with no issue and no fix. The Armory is unversioned in this repo and loads loose
`Assets/**/*.tpac`, so a sync touches nothing the commit hook or CI can see, and the gates had
to be remembered. This feature makes them one command, one report, one startup line, and one
validator error.

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
- **The validator carries the hang class.** `validate_moduledata.py` gained
  `MISSING_COLLISION_BODY` (ERROR) and `MISSING_VISUAL_MESH` (WARNING), emitted from the same
  `validate_mesh_refs` engine, skipped never faked without the install, and a run that found no
  tpacs to scan is itself a finding. The commit hook's `--code` allowlist carries the ERROR;
  `CommitGateCoverageTests` now scans both emitting files so the next code cannot be added without
  its hook line. **Correction, 2026-09-18 (#622):** `main()` never called the pass, so the hook line
  blocked nothing from 2026-09-15 to 2026-09-18. It is wired now, `CommitGateCoverageTests` checks
  that every `*_issues` pass is reached from `main()`, and the MCP tool and `/verify` still do not
  run it (#623).
- **A startup line that fires after a sync.** `session-start.sh` runs one bounded `find` for a
  `*_geo.tpac` newer than the committed catalogue (every catalogue row lives in a `_geo` pack;
  `_tex`, `_mtl` and `_anm` packs are re-saved constantly and never carry a mesh or body) and
  prints `ARMORY ART DRIFT` with the file and the command. Fail-open, never silent: an unresolvable
  install or an overrun prints "UNCHECKED".

### Component Diagram

```
session-start.sh  --"ARMORY ART DRIFT"-->  /armory-audit  -->  tools/audit_armory_refs.py
                                                                    |-- validate_mesh_refs (Tier B + C)
                                                                    |-- generate_armory_catalogue (rows + diff)
                                                                    |-- audit_deleted_mesh_impact (item -> troop join)
                                                                    |-- taom_schema.build_registries (item ids)
                                                                    '-- generate_ranged_ladder_items / generate_starter_kit --verify
                                                                    v
                                                    docs/audits/armory-ref-audit.md  (committed)
validate_moduledata.py  --MISSING_COLLISION_BODY-->  check-moduledata-validation.sh  (MCP, /verify: #623)
```

## Configuration

None. Paths default to the install (`BANNERLORD_GAME_DIR`, then the E: literal), the
`LOTRLOME_Armory` module, and `Main/_Module/ModuleData` as the consumer side; all overridable on
the CLI.

## Key Files

| File | Role |
|------|------|
| `tools/audit_armory_refs.py` | the composer; `flag_breakage`, `join_impact`, `render_report`, `exit_code` are pure |
| `tools/tests/test_audit_armory_refs.py` | seven synthetic tests, no install |
| `docs/audits/armory-ref-audit.md` | the committed report |
| `tools/validate_moduledata.py` | `missing_collision_body_issues`, `BODY_CODE`, `MESH_CODE`, `_loaded_tpacs` |
| `tools/tests/test_validate_moduledata.py` | `MissingCollisionBodyTests`; `CommitGateCoverageTests` scans both emitting files |
| `.claude/hooks/check-moduledata-validation.sh` | `--code MISSING_COLLISION_BODY` |
| `.claude/hooks/session-start.sh` | the `ARMORY ART DRIFT` probe |
| `.claude/skills/armory-audit/SKILL.md` | `/armory-audit [check\|regen]` |
| `docs/reference/armory-catalogue/catalogue.tsv` | the baseline the drift is measured against |

## Dependencies

`tools/validate_mesh_refs.py`, `tools/generate_armory_catalogue.py`,
`tools/audit_deleted_mesh_impact.py`, `tools/taom_schema.py`, `tools/_gamedir.py`. Stdlib only.

## Tests

- `python -m unittest tools/tests/test_audit_armory_refs.py` (7): live-ref flagging beats the
  committed flag; direct and roster-hop consumers both appear; the report names body, item and
  troop on one row and is byte-stable; exit code semantics; generator drift warns.
- `python -m unittest tools.tests.test_validate_moduledata.MissingCollisionBodyTests
  tools.tests.test_validate_moduledata.CommitGateCoverageTests` (5).
- `bash tools/test_hooks.sh` (208) covers the hook's timeout and inner bound.
- Negative proof on real tpacs (2026-09-15): a probe item with `body_name="bo_this_body_does_not_exist"`
  produced exactly one `MISSING_COLLISION_BODY` ERROR naming the body, the item and the file.

## How to run the audit after a sync

```bash
python tools/audit_armory_refs.py --regen-catalogue
git diff docs/audits/armory-ref-audit.md            # what changed since the last audit
```

Repair the ref side (never restore a tpac beside its replacement), re-run, commit the report and
the catalogue with the repair. The #599 repair script (`E:\taom-hang-2026-09-15\repoint_refs.py`)
is the shape: byte-exact substring swaps, dry run by default, applied to both the live Armory and
the `lotraom-assets` mirror.

## Performance

About 15 s against 4,497 tpacs (TOC scan only) including the two generator subprocesses; 3 s
with `--no-generators`. The validator's pass adds about 3 s to `validate_moduledata.py`: about
5.9 s without it, about 9 s with it, measured 2026-09-18 once #622 wired it in (the 4.8 s recorded
on 2026-09-15 was the validator without the pass, which never ran then). Inside the commit hook's
45 s inner bound. The startup probe is
one `find`, under a second, bounded at 4 s.

## Changelog

- 2026-09-15: created with #599. Repairs shipped the same day: 32 elven bow refs and 3 Swan
  Knight banner pieces repointed; first report CLEAN with one pre-existing starter-kit drift.

## GitHub Issue

#599. Mechanism first seen as #352 (2026-07-16). The runtime guard (a prefix on
`PreloadHelper.WaitForMeshesToBeLoaded` that logs and drops unresolvable body names so a player's
mission still loads) is not built; it is the only protection for an install that already has a
bad pair.
