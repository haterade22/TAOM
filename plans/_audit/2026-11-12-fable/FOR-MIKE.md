# FOR-MIKE: 2026-11-12-fable

The run's work is on the trunk since 2026-11-13 (`db1c87d7c`, a fast-forward, after your word to
merge and a passing Fords smoke run of the branch build). The steps and their evidence are in
[PROGRESS.md](PROGRESS.md) "Status" and "Log"; the audit result is in [REPORT.md](REPORT.md); the
choices made while you were away are in [DECISIONS.md](DECISIONS.md). Issues: #1132 (the nine items),
#1133 (messenger arrival decisions), #1134 (Fords allocation), #1135 (closed: the trunk fixed it).

## Waiting on you

Each line has the recommended answer first and where the evidence is.

1. **In-game checks owed for the merged refactors.** None of them changes behaviour by design, and
   the suites prove the logic. One Fords smoke run passed on the merged build (boot, patch
   application, one 120 s match), but it does not reach campaign code: the three LotrIssue templates
   offering a quest (047), character creation with the faction map, where the three CultureStageView
   patches apply (047), the supply order screen with goods and troops (047), a messenger arriving
   (050), a load of a save with a refuge (049), the weapon-usage toggle key in a battle (048).
   Recommended: one campaign session that touches each.
2. **Messenger arrival decisions** (#1133): a prisoner target, a `Released` or `Traveling` target, and
   a player who is a prisoner all lose or misroute a paid messenger. Recommended: they wait. Item 050
   made that a change to one tested function.
   **Decided 2026-11-13:** a prisoner target stays lost; every other case is delivered. Done on
   `fix/messenger-arrival-1133`: a fugitive, released or travelling target and a captive player now
   wait and are delivered. In-game checks owed: `docs/features/messengers.md` items 23 to 27.
   The same branch closes the two limits of that rule: the wait writes one line to the message log,
   and a fugitive or released hero of the Special occupation, whom the engine's daily settle skips,
   is lost and refused at the send. The new key `taom_messenger_waiting` waits for your language pass.
3. **Two "superseded" validators are still live doc steps** (verify-tests-tools.md, TOOLS-01):
   `audit_item_refs.py --show-locations` and `validate_all_troop_refs.py` are named in about 25
   procedure lines. Recommended: keep both until someone proves `validate_moduledata.py` prints the
   same locations.
4. **`/ship` runs `/verify` twice on an unchanged tree** (verify-harness-docs.md, DX-01). Recommended:
   amend `evidence-over-claims.md` to allow citing an earlier run whose recorded tree fingerprint still
   matches, with the NuGet audit run again. Saves about 500 s on a `/ship` with no fix.
5. **The always-on "Also:" list in `output-style.md`** (470 B; DX-03) copies
   `docs/reference/writing-tells.md`. Recommended: keep it; take bytes elsewhere when a rule must grow.
6. **`log-agent.sh` on every SubagentStart writes a log nothing reads** (DX-04). Recommended: delete
   the `SubagentStart` block from `.claude/settings.json` (your file); then the script, its catalog
   row and its test cleanup can go.
7. **Two always-on files disagree on opening issues unasked** (DX-07): `think-before-coding.md` covers
   repo-required issues by the standing word; `CLAUDE.md` says never auto-invoke `/issue`;
   `ship/SKILL.md` says on your word. Recommended: point the CLAUDE.md row at the standing word.
8. **`/ship` can run Codex up to three times on one change** (DX-08), and its line 14 lets a one-line
   C# fix skip review although CLAUDE.md requires `/deep-review` on every C# commit. Recommended: Phase
   2 counts the Codex pass of `/deep-review` Step 0; a one-line fix skips `/ship`, not `/deep-review`.
9. **`improve_ctl.py args review` routes six lenses for any C# change**, while the deep-review skill's
   tier rule launches one or two. This run followed the tier rule (DECISIONS 10). Recommended: make
   the tool route by tier, so the next run does not need a hand edit.
   **Done 2026-11-13** (`86843d0fa`): a review item takes a `tier`.
10. **MEMORY.md is over its 4 KB target** (DX-11): two index lines repeat repo rules. Recommended:
    delete `model-effort-free-choice` and `act-on-recommendations`.
11. **Finished one-off scripts** (TOOLS-02, TOOLS-09): six troop-revamp scripts and 11 `tools/oneoff/`
    scripts have no caller. Recommended: leave them. Four moved one-offs compute their repo root as
    the parent of their folder, which is `tools/` since the move, so they would read or write the
    wrong tree if run: `tools/oneoff/migrate_equipment_type_1_4_3.py:80`,
    `validate_equipment_flags_1_4_3.py:40`, `add_gundabad_clans.py:21`, `add_race_attribute.py:21`.
    Two migration templates still name the first two at their old path; this run did not repoint them
    for that reason.
12. **`docs/community/bannerlordmodding-lt.zip` is a stale copy of the folder beside it** (DOC-09).
    Recommended: delete it if it was a one-time hand-off.
13. **Review evidence has no index** (DOC-05, DOC-06, DOC-07): 462 open questions of the 2026-11-03 doc
    pass are linked from nowhere, `docs/reviews` holds 1,062 files, and
    `lessons/build-tooling-workflow.md` is one 4,994-line file. Recommended: one generated
    `docs/reviews/INDEX.md`, and `##` area groups inside the lessons file. Both need your taxonomy.
14. **A measurement only you can take** (lane-perf, PERF-01): one battle of 500 or more agents with
    `Enable Tick Profiler` on, and 10 minutes on the map with `Enable Map Profiler` on. No log on this
    desktop has per-behaviour or map numbers, so no battle-frame perf plan can be ranked. #1134 (Fords)
    needs the same switch.
15. **Follow-ups the reviews found, all pre-existing and LOW**: a vanilla quick release while TAOM's
    controller holds the player (warg rage) may toggle weapon usage late, not drop it (unverified in
    game; deep-review-048); `CareerChoiceGroupObjectVM.IsLocked` is computed and read by nothing
    (deep-review-048); the mirror enum `MessengerHeroState` could be one `isNotSpawned` flag
    (deep-review-050); `check_orphan_loc_rows.py` exits 1 on the trunk for two co-op rows,
    `taom_coopcompat_settings_differ` and `taom_coopcompat_build_mismatch` (executor 047).
    **2026-11-13:** `IsLocked` is deleted (branch `fix/messenger-arrival-1133`). The enum stays: the
    #1133 rule reads five of its states, so one flag no longer fits. The two co-op rows were a
    false alarm of the gate, which did not scan `CoopCompat/`; it does now and exits 0. The warg
    rage case is confirmed in the code (v1.5.7), not yet seen in game: `HandleQuickRelease`
    (`MissionGauntletMainAgentEquipDropView`) sets `_weaponUsageToggleRequested` on vanilla's
    controller, only vanilla's `ControlTick` reads and clears it, `OnPreMissionTick` skips
    `ControlTick` while `_activated` is false, and `Enable()` leaves the flag set. So a quick
    release during a rage toggles the weapon usage when the rage ends. No code changed. A fix could
    answer false from `IsMainAgentAvailable` while TAOM's controller holds the hero, as Patch140
    does for the Seat (the wheel then stays closed during a rage), or skip
    `OnWeaponUsageToggleRequested` then. Recommended: leave it unless a player reports it.
16. **Commit wording the reviews flagged, LOW** (reword at a squash or leave): the body of
    `cf21b3d33` says the binding gate "fails only when a patch's target is missing" (it also fails a
    target with no body or a throw); `3014400f8` is typed `fix:` and changes only a test and review
    records, so `/release` will list it under fixes. The other "review follow-ups" commits are typed
    `fix:` too and say "No change in game" in their bodies.
17. **Record gaps the last convergence rounds found, LOW**: the 046 RCA has no convergence section
    for two late findings (they are in the deep-review record), and one cell of the 051 RCA table
    still gives the older cause.
18. **`core.hooksPath` is the absolute `e:\repos\TAOM\.githooks`** on this machine, while
    `setup-dev-env.ps1:25` sets the relative `.githooks`. With the absolute value every worktree runs
    the main checkout's copy of the git hooks. Report only.

## Detail

- The custom battle muster VM (`Client/Features/CustomBattles/Muster/TcbVM.cs`) has three commands no
  prefab binds: `ExecuteUseOldScreen`, `ExecuteClosePalette`, `ExecuteCloseIcons`. It landed on
  2026-11-12 in `df8ba8484`, so it may be work in flight (lane-arch, ARCH-08).
- Another session moved the area traps into trap sheets on the trunk during this run
  (`29f863aa4`) and removed the same duplicated CLAUDE.md passage that item 052 removed. The trunk
  merge took the trunk's wording.
- Now that the work is on the trunk: restart open sessions (two skills and two hooks changed). To
  keep something this run deleted, revert its commit: 048's merge `c34192e74` (eight dead members),
  051's first commit `0ba42e002` (three tool scripts).
- The game folder holds the TAOM build of `db1c87d7c`, deployed by `build.ps1` from the run
  worktree for the smoke run. Your next F5 redeploys from your checkout.
- Agents and tokens of the run are in PROGRESS.md "Log", last entry.
