# Codex re-review: the fixes for the follow-ups review, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls, and run no game. Read
`AGENTS.md` and `.ai/review-reference.md` first. Scope is this worktree's committed diff `git diff 35aa37439..HEAD`
on branch `chore/follow-ups`. These commits answer your first pass on `9edc3a88b..35aa37439`
(`docs/reviews/raw/codex-prereview-follow-ups-2026-11-04.md`) and six Claude reports. Engine source for the installed
build is `C:\Users\mikew\.taom-src\v1.5.6-202611041234\`. Do not write to the game install, and do not read or write
`E:\LOTRAOM_Releases`. The red test `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is trunk's state.

Answer with evidence:

1. **Warg (`Main/Features/Warg/`).** Trace the new close path: `CleanIfEnemyDied.RageIsOver` and its two subclasses
   `CloseOpenRage` and `RestoreDismountedRider`, `GiveFormationBack`, `ValidStoredRider`, the store in
   `PrepareOnFirstCall`, and `OnWargDied`. Does your finding 1 (wrong rider), 2 (no close with no enemy near) and 3
   (a later warg death) still hold? Find any order of events that leaves a rider with no formation, a wrong formation or
   scripted movement on, or that stops the tree. Does the Sequence that replaced the "no enemy close" Selector
   change what the "has rider" selector does when its decorator is false, or when the sleep ends? Does the
   stranded clock in `WargRageLog` hide a real stranded rider or fail check 12 on a good run?
2. **Pregnancy.** Is every input's result now what `9edc3a88b`'s model gave, including a clanless hero and a negative chance?
3. **Pair check and validator.** Your findings 5, 6 and 7 and the MCP cache clear. Can `check_release_pair.py` still pass a
   channel that disagrees, or fail a good one?
4. **Hook.** Your finding 8: re-run your probes against `.claude/hooks/block-no-verify.sh`. Over-blocking on the raw path is
   intended; a missed refusal is not. Also the `context_budget_scan.sh` walker and fallback.
5. **Tools.** Your findings 9 and 10, then `rebuild_translation_files.py`'s new flags (can a run write without `--apply`, or write
   a module it did not name?), `run_fords_autorun.dump_crash`, `audit_mount_parity`'s section G.
6. **Gates.** Your findings 11 and 12, and the new direction of the visual definition gate.
7. **Data.** The 14 more rosters removed from Lindon and Blue Craig: is any read? Are `rosters_kept` and the data consistent?

Report each finding with severity, file and line, impact, and the proving code or a reproduction. Say PASS for what you
checked and found sound.
