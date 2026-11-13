# FOR-MIKE: 2026-11-12-fable

Everything this run changed is on the branch `improve/2026-11-12-run` (worktree
`E:\repos\taom-improve\2026-11-12\wt-run`). Nothing is on the trunk and nothing is pushed. The state
of each work item is in [PROGRESS.md](PROGRESS.md); the audit result is in [REPORT.md](REPORT.md).

## Waiting on you

Each line has the recommended answer first and where the evidence is.

1. **Merge the run branch**: yes, after you read the merged commit list in PROGRESS.md. Each work item
   is its own merge or commit, so you can drop one with a revert.
2. **Python suite is red on the trunk** (9 tests, heraldry and kingdom generators; baseline.md):
   someone who owns the Lindon and Ered Luin banner work should say whether the live install or the
   repo lags. Not this run's scope.
3. **Messenger to a prisoner is lost on arrival** (verify-arch.md, ARCH-01): decide whether a prisoner
   target should wait, as the comment at `MessengerCampaignBehavior.cs:304-305` says, or stay lost.
   Plan 050 keeps today's behaviour on purpose. Recommended: wait, in a follow-up fix with its own
   in-game check.
4. **Two "superseded" validators are still live doc steps** (verify-tests-tools.md, TOOLS-01):
   `audit_item_refs.py --show-locations` and `validate_all_troop_refs.py` are named in about 25
   procedure lines. Recommended: keep both until someone proves `validate_moduledata.py` prints the
   same locations, then repoint the docs and delete. Plan 051 deletes only the three scripts with no
   live use.
5. **`/ship` runs `/verify` twice on an unchanged tree** (verify-harness-docs.md, DX-01): reusing the
   first run needs an amendment to `evidence-over-claims.md` ("an earlier run" is not verification).
   Recommended: allow citing an earlier run whose recorded tree fingerprint still matches, with the
   NuGet audit step run again. Saves about 500 s on a `/ship` with no fix.
6. **The always-on "Also:" list in `output-style.md`** (470 B; DX-03): it copies
   `docs/reference/writing-tells.md`, which loads only on demand. Deleting it frees the rules cap (14 B
   left today) but removes eleven tells from every session. Recommended: keep it, and take the bytes
   from somewhere else when a rule needs to grow. Item 052 removed only the two CLAUDE.md duplicates.
7. **`log-agent.sh` on every SubagentStart writes a log nothing reads** (DX-04): about 13,000 process
   starts a week. Recommended: delete the `SubagentStart` block from `.claude/settings.json` (your
   file), then an executor removes the script, its catalog row and its test cleanup.
8. **Two always-on files disagree on opening issues unasked** (DX-07): `think-before-coding.md` says
   repo-required issues are covered by the standing word; `CLAUDE.md` says never auto-invoke `/issue`;
   `ship/SKILL.md:32` says on your word. Recommended: change the CLAUDE.md row to point at the standing
   word. This run filed its issues under the standing word and your "add issues" instruction.
9. **`/ship` can run Codex up to three times on one change** (DX-08), and its line 14 lets a one-line
   C# fix skip review although CLAUDE.md requires `/deep-review` on every C# commit. Recommended: Phase
   2 counts the Codex pass of `/deep-review` Step 0; a one-line fix skips `/ship` but not `/deep-review`.
10. **MEMORY.md is over its 4 KB target** (DX-11): two index lines repeat repo rules
    (`model-effort-free-choice`, `act-on-recommendations`). Recommended: delete those two lines.
11. **Six finished troop-revamp scripts and 11 `tools/oneoff/` scripts with no caller** (TOOLS-02,
    TOOLS-09): move, keep or delete is your call; the catalog keeps the six "for the mechanical-swap
    pattern". Recommended: leave them; the gain is grep noise only.
12. **`docs/community/bannerlordmodding-lt.zip` is a stale copy of the folder beside it** (DOC-09).
    Recommended: delete it if it was a one-time hand-off.
13. **The 462 open questions of the 2026-11-03 doc pass are linked from nowhere** (DOC-05), and
    `docs/reviews` holds 1,062 files with no index (DOC-06), and `lessons/build-tooling-workflow.md` is
    one 4,994-line file (DOC-07). Recommended: one generated `docs/reviews/INDEX.md`; `##` area groups
    inside the lessons file. Both need your taxonomy.
14. **Measurement only you can take** (lane-perf, PERF-01): one battle of 500 or more agents with
    `Enable Tick Profiler` on, and 10 minutes on the map with `Enable Map Profiler` on. No log on this
    desktop has per-behaviour or map numbers, so no battle-frame perf plan can be ranked.
15. **`core.hooksPath` is the absolute `e:\repos\TAOM\.githooks`** on this machine, while
    `setup-dev-env.ps1:25` sets the relative `.githooks`. With the absolute value every worktree runs
    the main checkout's copy of the git hooks. Report only.

## Detail

- The custom battle muster VM (`Client/Features/CustomBattles/Muster/TcbVM.cs`) has three commands no
  prefab binds: `ExecuteUseOldScreen`, `ExecuteClosePalette`, `ExecuteCloseIcons`. It landed on
  2026-11-12 in `df8ba8484`, so it may be work in flight (lane-arch, ARCH-08).
- The Fords match allocates about 92 KB per frame on the main thread, nine times every other battle in
  the November logs (lane-perf, PERF-02). taom-b4 was told on 2026-11-12.
- After a merge of this branch into the trunk: restart open sessions (CLAUDE.md, two skills and two
  hooks change), and run `/security-scan` once on the trunk.
