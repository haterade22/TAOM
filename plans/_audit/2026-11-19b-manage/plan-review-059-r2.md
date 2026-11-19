# Cold review: plan 059, round 2

Plan: `plans/059-validate-moduledata-mcp-parity.md` (1256 lines), read against `db5680c8a` in the run
worktree. Earlier review: `plan-review-059.md` (round 1, 2 blocking, 12 non-blocking).

Verdict: **0 blocking, 7 non-blocking.** Both round-1 blocking items are fixed, and so are all 12
round-1 non-blocking items. The planned code gives the commit gate the same verdict as the base on
every row I ran except two. In those two rows the gate now allows a commit the base denied. Both
loosenings are deliberate and the plan names them (non-blocking 1 and 2). No row fails open on a
parse slip or a crash.

## How this was checked

My own simulation, in the scratch folder `review-059-r2` (not in the repo):

- `setup_sim.py` copies the worktree's `tools/*` files and its `schemas/`, `data/` and `tests/` into
  `sim_base/` and `sim/`. It also copies the codes doc. `build_sim.py` applies the plan's Steps 2, 3, 4
  (the codes-doc row included), 6.4, 8, 9 and 10 to `sim/`. I filled the elided "verbatim" parts from
  `main()` :832-843, :870-871 and :898-918. `make_s4.py` builds `sim_s4/`, which is the tree at Step 5
  (Steps 2 to 4 applied, Steps 9 and 10 not yet).
- Unit files, base sim against planned sim:
  - `test_validate_mesh_refs.py`: "Ran 69 ... OK" against "Ran 73 ... OK".
  - `test_taom_query.py`: "Ran 15 ... OK" both times.
  - `test_taom_mcp_server.py`: "Ran 6 ... OK" (3.8 s) against "Ran 7 ... OK" (11.1 s).
  - `test_validate_moduledata.py`: "Ran 446" against "Ran 452", and the failure sets are identical
    (13 errors, 2 failures). Every one of them is a sim artifact, because no `Main/` sits beside the copy.
  - With the hook copied in, `-k CommitGateCoverageTests` gives "Ran 7 ... OK" against "Ran 8 ... OK".
    `test_every_emitted_code_has_a_row_in_the_codes_doc` passes once the Step 4 row is in place.
  - New tests in total: 6 + 4 + 1 = 11, which matches Step 11.2.
- Step 5.1 as written (`cli_compare.py`, full CLI on the repo ModuleData):
  - Base against Step-4 tree, then Step-4 tree against final tree: rc 0/0. "Sorted stderr equal" both
    times. 4 added and 4 removed in both comparisons. Each of those 4 is a warning whose message embeds
    the sim folder's own path (artifact). stdout differs only by those same 4 messages.
  - With `--game-modules` absent: rc 2/2, `added 0`, `removed 0`, stdout identical, sorted stderr equal.
  - So the plan's new sorted-stderr check can pass.
- Step 5.2 probe (`mcp_probe.py`, planned code, live install):
  - 0 characters on stdout, 13.3 s, `_read_stripped` currsize 0, 0 `PARTIAL_RUN` notices.
  - `added 0`, `removed 0` against the planned CLI's JSON.
  - This confirms the Maintenance note at plan lines 1252-1255.
- The shipped hook, unchanged by the plan, driven by `hook_sim.py`. It ran in a scratch git sandbox
  through Git Bash (`shutil.which`), against the base and the planned validator, with Bash and
  PowerShell payloads:

  | Case | Bash base / planned | PowerShell base / planned |
  |---|---|---|
  | clean live install | `{}` / `{}` (21.3 s / 15.4 s) | `{}` / `{}` |
  | troop with `Item.zz_no_such_item` | deny / deny | deny / deny |
  | item with `body_name="bo_zz_review_missing"` | deny (BODY) / deny (BODY) | deny / deny |
  | item with `body_name="bo_backyard_stairs"` (a body only `TAOM_Map` ships) | **deny / `{}`** | **deny / `{}`** |
  | the missing body, in `ModuleData/Languages/zz_review_body.xml` | **deny / `{}`** | **deny / `{}`** |
  | `--game-modules` absent (exit 2) | `{}` / `{}` | `{}` / `{}` |
  | `run_all_passes` (base: `schema_invalid_issues`) raises | allow + CRASHED / allow + CRASHED | same |

  Empty but existing Modules folder:
  - The hook harness hit its own 180 s limit on the base run, so I ran that case through the CLI with
    the hook's exact `--code` list instead (`empty_mods.py`).
  - Base and planned both return rc 1 in about 14 s, with the same nine ERROR codes, so the hook denies
    both.
  - Planned reports 82 per-ref `MISSING_COLLISION_BODY` findings where base reports the single "no
    *.tpac found" finding. Round-1 non-blocking item 7 predicted this, and Maintenance notes now
    record it.
  - The harness stall happened on the base validator, so it is not the plan's. I did not investigate
    its cause (UNVERIFIED).
- Step 7 (`hook_7m_new.sh`): the three new 7m cases ran against the shipped hook under both payloads.
  - Exit 0 → `{}`, exit 2 → `{}`, plain exit 3 → allow + CRASHED.
  - Each of the three mutation strings matches the hook exactly once, and each mutant gives the
    non-expected answer: an UNCHECKED context for exits 0 and 2, `{}` for exit 3.
- Pack sets (`find_map_only_body.py`):
  - Base module set: 5828 packs, 0 unparsed, 8279 bodies.
  - `TAOM_Map`: 2085 packs, 0 unparsed, 1092 bodies, all 1092 absent from the base set.
  - TAOM: 80 packs, 0 bodies, whether read from the install or from the repo module.

## Round-1 items

- Blocking 1 (codes-doc row needed before Step 4's Verify): fixed. The row is now in Step 4
  (lines 704-710), Verify names the failure it prevents (743-745), and my sim passes with it.
- Blocking 2 (Step 2 grep matched two lines): fixed (lines 478-481). The planned file has one
  `^if __name__ == "__main__":` line, at its end.
- All 12 round-1 non-blocking items are addressed:
  - `/verify` half: lines 186-190. Verified: `.claude/skills/verify/SKILL.md:100` runs
    `python tools/data_gates.py`, and `data_gates.py:143` runs the CLI. `f1675c058` and `69d2e8c0a` are
    both dated 2026-11-03, and `69d2e8c0a` is the first commit that adds `data_gates` to the skill.
  - `armory-ref-audit.md` :84-86 now in scope, with its grep: lines 386, 797-805, 1179. At base that grep
    prints line 85, as stated.
  - `shutil.which("bash")`: line 860.
  - Line range :845-848: line 948.
  - Em dash and CLI wording kept verbatim: lines 619-622 and 1242-1245.
  - SandBox and the installed TAOM in the Step 10 check: lines 1099-1102.
  - Guard note: lines 1246-1251.
  - Read cache: lines 1252-1255.
  - `<Modules>`: line 433.
  - Step 10.1 commands with counts: lines 1052-1058.
  - `(warn)` and the anchor row: line 707.
  - stderr on exit 3: lines 179-181.

## Blocking

None.

## Non-blocking

1. **Step 10 deliberately loosens the commit gate for bodies only `TAOM_Map` ships (plan lines
   1019-1020, 1068, 1128-1130).**
   - Simulated with `bo_backyard_stairs`: base denies, planned allows, under both tool names. 1092 bodies
     qualify.
   - #623 item 5 asks for this, and the plan says it. Its basis is that "the engine resolves a mesh or a
     body from every loaded module". That claim rests on the `tpac_paths_for_modules` docstring (the
     engine logs `Loading packages ...` for `TAOM_Map` and TAOM), not on a quoted engine line.
   - `Main/_Module/SubModule.xml:21-25` does not declare `TAOM_Map` as a dependency. The live
     `TAOM_Map/SubModule.xml:8-13` does not declare TAOM either. So if TAOM runs without `TAOM_Map`, an
     item whose body only `TAOM_Map` ships would hang preload, and after Step 10 the gate would pass it.
   - Recommendation: add this precondition to Maintenance notes and to the Step 10 commit body, so the
     reviewer and the maintainer can weigh it.
2. **Step 9.3's `Languages/` skip loosens the gate for a ref under `ModuleData/Languages/` (plan lines
   976-992).**
   - Simulated: a missing `body_name` in `Languages/zz_review_body.xml` is denied by base and allowed by
     planned, under both tool names.
   - Harmless as long as no `SubModule.xml` `XmlNode` loads items from `Languages/`. Step 1's STOP guard
     (lines 451-452) proves only that no such ref exists today.
   - Recommendation: one Maintenance-notes line saying that the skip assumes no XmlNode path points into
     `Languages/`.
3. **"`git status --porcelain` is empty" (plan lines 826, 1112, 1134, 1184) may never hold.**
   - It cannot hold in a worktree that carries untracked orchestrator files. This run's worktree has
     `?? plans/059-...md`, `?? plans/_audit/...` and others.
   - A literal executor in such a worktree fails Step 6's Verify and STOPs.
   - Fix: `git status --porcelain --untracked-files=no` prints nothing, or "lists no path in Scope".
4. **Step 3's RED run states no expected count (plan lines 591-594).**
   - The first command should print `Ran 5 tests`: two parity tests, the renamed reachability test, the
     new call test, and the schema test. The server command should print `Ran 1 test`.
   - As written, a partial `-k` match passes the "not 0" check.
5. **The new `armory-ref-audit.md` sentence says "`/verify` runs the full CLI through
   `tools/data_gates.py`" (plan line 804). That is conditional.**
   - The `moduledata` gate runs only when a changed path matches its globs (`tools/data_gates.py:70-78`,
     :135-143).
   - Suggest "`/verify` runs the full CLI through `tools/data_gates.py` whenever the tree changes
     ModuleData, Armory, `TAOM_Map` or validator files", which is the wording the plan already uses at
     line 188.
6. **Step 7's mutation proof and its 7m cases send only the Bash payload (plan lines 833-849, 859).** My
   run shows the same answers under the PowerShell payload, so this is optional. A PowerShell row would
   pin `taom_hook_command`'s translation for these exits as well.
7. **Test cost.** `test_taom_mcp_server.py` goes from 3.8 s to 11.1 s in my sim, because
   `test_validate_tool_returns_shape` now runs every install-backed pass on the live install. Worth one
   line in Maintenance notes beside the MCP's 13 to 16 s per call (lines 1218-1220).

## Excerpt mismatches

None. Re-checked at `db5680c8a`:

- `taom_query.py`: :36-39, :45, :124-137 (137 lines).
- `taom_mcp_server.py`: :43-65, :52-59, :73-79.
- `validate_moduledata.py`:
  - :308-317, :320-326, :341-342, :354-367;
  - :529-530, :586-616 (`is_dir` at :606);
  - :802-945 with each sub-range, :948-963.
- `validate_mesh_refs.py`:
  - :97-100, :214-215, the three `_lineno` calls, :426, :543;
  - :772-773, :836-851 (the comment that is replaced starts at :845), :1137-1140, :1183.
- `_lineno` is also defined at `taom_schema.py:202` and `audit_deleted_mesh_impact.py:200`.
- `extract_refs` callers: `audit_armory_refs.py:352-354`, `audit_deleted_mesh_impact.py:781`,
  `audit_fbx_lods.py:330`, `generate_armory_catalogue.py:456`.
- `DEFAULT_TPAC_MODULES` readers: `audit_deleted_mesh_impact.py:805`, `audit_gender_variation_flags.py:159`.
- Other `tools/` files:
  - `audit_armory_refs.py:355`;
  - `taom_schema.py:1710`;
  - `data_gates.py:143`.
- `test_validate_moduledata.py`:
  - :3168-3169 (22 classes follow; 5346 lines);
  - :4052, :5053-5066, :4775-4784.
- `test_validate_mesh_refs.py:76-80`.
- `test_taom_query.py:156-170`.
- `test_hooks.sh` 7m at :3982-4054, with the cases at the cited lines.
- The hook at :18-23, :112-121, :166-182, :185-191.
- Docs and other files:
  - `armory-ref-audit.md:83-86` and :104;
  - `.claude/skills/armory-audit/SKILL.md:79`;
  - `moduledata-validator-codes.md:63` (`SCHEMA_INVALID`);
  - `mesh-ref-validation.md:149`.
- Counts: 85 tracked `*.tpac` under `Main/_Module`.
- Stale-claim grep at base: the four hits the plan names (`validate_moduledata.py:342`, :530,
  `test_validate_moduledata.py:4052`, `armory-ref-audit.md:104`) plus `armory-ref-audit.md:85-86`. The
  remaining `#623` mentions are in dated Changelog entries, `docs/changelog-archive/` and
  `docs/reviews/`, which the plan correctly leaves alone.

## Checklist items

- Executability: everything a weak executor needs is inline; the gaps are non-blocking 3 and 4.
- Exact expected results: every step has one.
- TDD: each implementation step follows a RED step. Step 2's RED is in the log, and Step 7's RED comes
  from mutation. No C#, so no binding ADR applies (line 347).
- Issue #623 is named, with the maintainer's approval. Step 0 is "None" and no protected file changes.
  Single-owner files are out of scope.
- STOP conditions are specific to this plan. Planned-at `db5680c8a`. The drift-check paths (line 8) cover
  Scope plus the hook, which the plan reads but does not edit.
- dotnet commands carry `-p:DisableModuleCopy=true -p:ModuleId=`. No worktree path or branch name, and
  no CHANGELOG step.
- Prose: the only em dashes are inside the quoted current code at lines 248 and 252. No secret values
  and no local absolute paths.

## Not verified

- The dotnet baseline (43028 passed, 3 failed, 9 skipped), the full Python suite total (10549) and the
  hook suite's base count: this role does not run them.
- `lint_docs.py --fail-on-drift` after the doc additions. The plan's STOP covers a budget overrun.
- Why the hook harness stalled on the base validator with an empty Modules folder.
