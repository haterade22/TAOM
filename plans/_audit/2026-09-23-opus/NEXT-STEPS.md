# What we do now: the review sprint's branches, in order

State on 2026-09-24 (midday). Nothing below has been merged or pushed. Every change sits on a local
branch `improve/NNN-<slug>` (worktrees under `E:\repos\taom-improve\`); `bannerlord-1.5.x` carries only
the plans commit `7f02fc8d` from this work. Each branch was executed from its plan, then put through
`/deep-review` (lenses as senior reviewers), a Codex adversarial review, a verified fix pass and a
convergence check. The review record for each branch is on the branch at
`docs/reviews/deep-review-NNN-<slug>-2026-09-24.md`.

## 1. First, ten minutes of housekeeping

1. **Move the last C: data to E:.** Close every Claude Code and Codex window, then run
   `pwsh -File E:\repos\taom-improve\move-claude-and-codex-data-to-E.ps1`. It moves `~\.claude\projects`
   (2.4 GB of transcripts) and `~\.codex\sessions` (480 MB) to E: behind junctions and checks file
   counts before deleting anything. Your user `TEMP`, `TMP` and `NUGET_PACKAGES` already point at E:
   (set overnight); old caches you can then clear: `%LOCALAPPDATA%\Temp` (9.5 GB) and
   `%USERPROFILE%\.nuget\packages` (5.7 GB).
2. **Test the slow-desktop suspicion (one launch).** Start the Bannerlord profile with Ctrl+F5 (no
   debugger) or switch `Main/Properties/launchSettings.json` to managed-only debugging, then read
   PatchShield pass 2's time in `Modules/TAOM.Dependencies/diag.log`. About 2.5 s means the mixed
   managed and native debugger has been costing you about 100 s per launch since 2026-06-12.
3. **Push the plans commit** when you are ready (`git push origin bannerlord-1.5.x` pushes `7f02fc8d`
   together with whatever the other session committed on top).

## 2. Merge the reviewed branches, in this order

Merge into `bannerlord-1.5.x` only after the other session has committed its in-flight work (its
uncommitted files include `Main/SubModule.cs`, `Main/IoC.cs` and `CHANGELOG.md`, which several branches
also touch). Every branch adds `## 2026-09-24` CHANGELOG entries and appends to `docs/reviews/REVIEW-LOG.md`
(numbered "Review 132", provisional) and to `docs/reviews/lessons/*.md`: those conflicts are additive,
keep every entry, one date heading, renumber the review-log entries in merge order.

| Order | Branch | What it does | Needs you |
|---|---|---|---|
| 1 | `improve/008-binding-gate-no-silent-skips` | the binding gate fails loudly instead of passing by skipping; the build's game folder is the resolver's fallback | 5 decisions (skip-banner channel, `TreatNoTestsAsError`, a CI `!cancelled()` guard, resolver order) |
| 2 | `improve/010-ci-on-hosted-windows` (built on 008) | C# builds and tests on GitHub-hosted Windows against BUTR reference assemblies; the self-hosted job is gone | its review is finishing; after merge, push and watch the first `csharp.yml` run (expect about 8,183 unit and 338 binding passes) |
| 3 | `improve/009-guarded-patch-category-apply` | all 64 `PatchCategory` calls go through one guard; startup failures show in a persistent inquiry | 3 decisions; review found and fixed one HIGH |
| 4 | `improve/018-composition-root-first-steps` (built on 009) | shared source reader for the wiring tests, the feature-module contract and runner, WandererAllegiance migrated as the pilot | its review is finishing |
| 5 | `improve/006-crash-capture-boot-cost` | the 247-method crash-capture sweep becomes a 6-shim allowlist; the MCM toggles work; four dead finalizers deleted | 8 decisions (allowlist entry 6, an in-game probe, bridge exit and priority, log time floor, whether the toggle stays) |
| 6 | `improve/007-patchshield-skip-callback-shims` | PatchShield no longer re-shields the callback shims at the first game start | none |
| 7 | `improve/012-loading-window-trace-per-frame` | the loading-window trace logs real drops only, not every frame | 1 decision |
| 8 | `improve/014-enlistment-session-scope` | Enlistment's per-session clocks and caches reset on load and on a new campaign | 6 decisions (reset before the co-op authority gate, the `_lossAnnouncedFor` latch, stale records on load, a `SubModule.OnGameEnd` teardown) |
| 9 | `improve/015-warg-tick-costs` | warg trees stop resolving IoC per tick, reuse scan buffers, and skip skeleton wrappers for far or allied targets | its review is finishing |
| 10 | `improve/019-nullable-ratchet` | nullable warnings move from `NoWarn` to `.editorconfig`; Siege graduates to errors (and a real null dereference in the BesiegerCamp patch is fixed) | its review is finishing |
| 11 | `improve/013-bash-hook-prefilter` | Bash hooks skip Python on non-git calls: 256-451 ms per hook down to 60-150 ms | its review is finishing; after merge, one live check that a `git commit` still triggers the gates |

`006`, `009` and `018` all edit `Main/SubModule.cs`; merge them one at a time and rebuild after each.
Player-facing fixes (006, 007, 009, 012, 014, 015) also belong on `bannerlord-1.4.5` per the release
policy; the ports are not done.

## 3. Three plans wait on you (protected files)

`config-protection.sh` blocks agents from these files, so the executors stopped by design.

- **011** (Stop reminders reach the session; force-push guard for `bannerlord-*`): add the two
  PowerShell hook groups to `.claude/settings.json` exactly as the plan's Step 0 shows, then re-run the plan.
  Also add a GitHub ruleset blocking force push and deletion on `bannerlord-*` (Settings, Rules).
- **016** (untrack personal and generated files, pin MCP servers, fix the README): Step 0 moves shared
  settings out of `.claude/settings.local.json` into `.claude/settings.json` before the local file is untracked.
- **017** (dirty-tree build flag, build field in crash bundles, tag-only releases): the MSBuild target goes
  in `Directory.Build.props`; approve that edit (or make it) and re-run the plan.

## 4. Decisions still open from the sprint report

- Crash capture's default: plan 006 keeps it ON and narrows it; turning it OFF is your call.
- The June branches `impl-001`, `impl-002`, `impl-003`, `impl-005`: `plans/README.md` says what each is
  worth (cherry-pick `cfc47206` from 002 for the `MutationParams` NaN guard; 001's SpecialResources leak
  still needs a fresh fix).
- Direction: generate the CHANGELOG at `/release` instead of hand-editing it; amend ADR-007 and the
  interface rule to match the code; LFS and sparse checkouts for repo weight (`REPORT.md`, "Direction").
- The Order of Battle "Auto-Assign is a Phase-1 stub" button: remove it or wire `HeroAutoAssigner`.

## 5. Owed in game after merging

A boot and a first campaign load (timings in `taom_debug` and `diag.log`), a Custom Battle with warg riders
on both sides (015), a siege (019, 009), an enlistment save/load and a second campaign in one process (014),
and the main menu and party screen without per-frame trace lines (012).

## 6. The GitHub issues

One issue per plan (006 to 019, except 010, which is posted as a comment on the existing #421), each with
the problem, analysis, the branch's changes, the test results, the review outcome and the decisions it
needs. Their numbers go in `plans/README.md`'s Status column.
