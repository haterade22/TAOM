# Implementation Plans

Working backlog, NOT knowledge base (feature docs live in `docs/features/`). Each plan is a
self-contained fix-spec a separate agent or session can execute with only the plan file and the repo.
Per AGENTS.md a GitHub issue must exist before any plan's implementation lands on a trunk branch.
Each executor: read the plan fully, honour its STOP conditions, and report back; the index is kept by
the orchestrator.

Two generations live here:

- **001 to 005**: `/improve` run of 2026-06-12, written against `141b749`, reconciled on 2026-09-23.
- **006 to 019**: `/improve` review sprint `2026-09-23-opus`, written against `b2e387db`. The report,
  baseline metrics, every finding and every verdict are in
  [`_audit/2026-09-23-opus/REPORT.md`](_audit/2026-09-23-opus/REPORT.md).

## Execution order and status

Recommended order, top to bottom. Status values: TODO | IN PROGRESS | DONE | BLOCKED (one-line reason) |
REJECTED (one-line rationale). "Branch" is where an overnight executor put the work (never merged,
never pushed; see "Overnight execution" below).

| Plan | Title | Priority | Effort | Category | Depends on | Status |
|------|-------|----------|--------|----------|------------|--------|
| [008](008-binding-gate-no-silent-skips.md) | Make the binding gate fail loudly instead of passing by skipping | P2 | S | tests | none | DONE on branch `improve/008-binding-gate-no-silent-skips` (`37306bca`, #652): reviewed; D7-D9 applied (hook change reverted, `TreatNoTestsAsError`) |
| [010](010-ci-on-hosted-windows.md) | Compile and test C# on GitHub-hosted Windows runners against BUTR reference assemblies | P2 | M | dx | 008 | DONE on branch `improve/010-ci-on-hosted-windows` (`c139bc50`, on 008; #421, comment posted): D44/D45 applied, D46 measured and reverted; second review running in batch 3; replay on the merged tree before the first push |
| [009](009-guarded-patch-category-apply.md) | Apply every Harmony patch category through one guarded helper | P2 | S | bug | none | DONE on branch `improve/009-guarded-patch-category-apply` (`b6cb6ff5`, #653): reviewed; D10 (class-by-class category index) and D11 (localized notice, 12 languages) applied; second review owed |
| [011](011-stop-reminders-and-trunk-guard.md) | Make the four Stop reminders reach Claude and guard the live trunk against force pushes | P2 | S | dx | none | TODO, unblocked (D1; #654): executes on 013's tip with D29 (no ruleset), D30 (two trunks), D38 (multi-line push gate), D41 (verification marker), D42 (delete two dead hooks) |
| [012](012-loading-window-trace-per-frame.md) | Stop the loading-window trace walking the stack and flushing a log line every frame | P2 | S | perf | none | DONE on branch `improve/012-loading-window-trace-per-frame` (`a4492fa8`, #655): deep review + Codex + convergence |
| [006](006-crash-capture-boot-cost.md) | Stop the crash-capture sweep costing 30 s of every boot, and delete the four finalizers that can never fire | P2 | S | perf | none | IN PROGRESS on branch `improve/006-crash-capture-boot-cost` (`42624b95`, #650): reviewed; decisions D2-D6, D24, D26 applied or recorded; D43/D47 (off-thread mark, then 10 combat callbacks) being applied; fresh review owed |
| [007](007-patchshield-skip-callback-shims.md) | Stop PatchShield re-shielding the 247 callback shims at the first game start | P2 | S | perf | none (complements 006) | DONE on branch `improve/007-patchshield-skip-callback-shims` (`31a31f16`, #651): deep review + Codex + convergence; D27 follow-up (log label, seen/attached counts) queued |
| [014](014-enlistment-session-scope.md) | Reset Enlistment's per-session clocks and caches on load and on a new campaign | P2 | S to M | bug | none | DONE on branch `improve/014-enlistment-session-scope` (`aac0e05b`, #656): D12-D16 applied; second review READY FOR COMMIT (18 + 4 fixed); in-game checks owed |
| [016](016-repo-hygiene-pins-readme.md) | Untrack personal and generated files, pin the MCP servers, and correct the README | P2 | S | security | none | TODO, unblocked (D1; #657) |
| [013](013-bash-hook-prefilter.md) | Let non-git Bash calls skip the Python start-up in every Bash hook | P3 | S | dx | none | DONE on branch `improve/013-bash-hook-prefilter` (`7aa658e3`, #661): D39/D40 applied (hook suite 382/0, re-run by the orchestrator); second review running in batch 3 |
| [015](015-warg-tick-costs.md) | Cut the warg behaviour tree's per-tick resolves, allocations and native wrapper churn | P3 | S | perf | none | DONE on branch `improve/015-warg-tick-costs` (`516380d2`, #659): D31-D33 applied; second review READY FOR COMMIT (14 + 1 fixed); in-game warg battle owed |
| [017](017-build-identity-dirty-flag.md) | Stamp a dirty-tree flag into every build and a structured build field into every crash bundle | P3 | S | dx | none | PARTIAL on branch `improve/017-build-identity-dirty-flag` (`743ce2e1`, #658): commit A (build stamp in crash bundles) done; stopped at Step 5 (protected `Directory.Build.props`, see FOR-MIKE) |
| [018](018-composition-root-first-steps.md) | Start the feature-module composition root: shared source reader, module contract, empty list, one pilot feature | P3 | M | tech-debt | 009 | DONE on branch `improve/018-composition-root-first-steps` (`71fd2169`, on 009's `4c728dac`; #662): deep review + Codex + convergence; D48 recorded; merge after 009's new tip |
| [019](019-nullable-ratchet.md) | Stop discarding the nullable warnings and graduate the first folder to errors | P3 | S | tech-debt | none | DONE on branch `improve/019-nullable-ratchet` (`272ac1f0`, #660): D34-D36 applied; second review READY FOR COMMIT (11 + 4 fixed) |
| [023](_audit/2026-09-23-opus/DECISIONS.md) | Record that nothing reloads TAOM in-process, so new patches need no `ResetForUnload` (decision 22, no plan file) | P3 | S | docs | none | DONE on branch `improve/023-no-inprocess-reload` (`636b50ab`): one bullet in `harmony-patches.md` + CHANGELOG; docs only, no review gate |
| [024](_audit/2026-09-23-opus/DECISIONS.md) | Managed-only default launch profile, mixed debugger as a second profile (decision 23, no plan file) | P2 | S | dx | none | DONE on branch `improve/024-managed-debug-profile` (`37ff41e4`): config only; the next Play is the test (`diag.log` shield pass should fall from about 69 s) |
| [002](002-nan-infinity-config-guards.md) | Reject NaN/Infinity in TroopWeight + Career-mutation config loaders | P1 | S | bug | none | PORTED on branch `improve/002-nan-guards` (`78889a85`, 2026-09-24): the MutationParams half of `cfc47206` (TroopWeight half already on trunk in `bee07b48`); RED/GREEN proven; deep review + Codex owed |
| [001](001-cross-campaign-singleton-resets.md) | Reset feature singletons on new-campaign boundary (Career + SpecialResources) | P1 | S to M | bug / save-integrity | none | PORTED on branch `improve/001-specialresources-reset` (`4263535a`, 2026-09-24): SpecialResources storage reset on a new campaign and on a load without the key (5 tests, RED first; suite 10318/0 failed); Career half already on trunk (`f4273639`); deep review + Codex owed |
| [005](005-security-hygiene.md) | Scrub vendored credential + pin MCP servers + close faction_map subprocess injection | P2 | S | security | none | PORTED on branch `improve/005-security-hygiene` (`6b34fd00`, 2026-09-24): `4310aa6e` + `4bc520a1`; credential already gone from disk; MCP pins moved to 016; deep review + Codex owed |
| [003](003-hot-path-resolve-and-grid-caching.md) | Cache hot-path IoC/MCM resolves + fix 2s SpatialGrid staleness | P3 | S each | perf | none | PORTED on branch `improve/003-battlebalance-settings-cache` (`7feca96b`, 2026-09-24): `6eb5955c` plus an IL rule test; warg half superseded by 015; deep review + Codex owed |
| [004](004-live-town-scene-crashes.md) | Fix 3 LIVE town-center scene crashes (Isengard / Helm's Deep / Calembel) | P1 | S (remap) / L (author) | data / crash | none | DONE (2026-06-13; re-verified 2026-09-23: `audit_scene_names.py` shows 0 TAOM_Map misses; Calembel still on the `empire_town_h` stopgap; the `.bak_scenes` backup is gone) |

Plans 006 and 007 were written as P1 on the assumption that every player pays their boot and load
seconds. A follow-up measured players at about 1 to 3 s (this desktop runs about 30 times slower), so
this index lists them as P2 behind the CI and gate work; see the report's "The slow desktop".

## Dependency notes

- **010 after 008:** the CI binding step uses 008's `binding-gate.runsettings` and its
  `GameAssemblies` fallback to the build's game folder.
- **018 after 009:** the module runner reuses 009's guarded `TryPatch` helper.
- **006 and 007 touch the same 247 methods** from two sides (the crash-capture sweep, PatchShield's pass
  2). Either can land first; together they remove both redundant patch passes.
- **006, 009 and 018 all edit `Main/SubModule.cs`**, a single-owner file that another session also has
  uncommitted edits in. Land them one at a time and rebase each over the other session's work.
- **016 supersedes plan 005's item B**; **015 supersedes plan 003's Warg item (PERF-07)**.

## Non-interactive default (recorded)

The sprint prompt said to take `/improve`'s non-interactive default wherever it would wait for a
selection. Selection used: the top six by leverage, plus every P1 correctness or security item (none
survived as P1 after adversarial checking), plus the ten quick-pass seeds Mike approved earlier in the
session (F1 to F10), grouped so that tiny siblings share a plan (016 carries F5, F6 and F8). Every plan
went through a writer, a fresh cold reviewer and a reviser; the cold reviews are in
`_audit/2026-09-23-opus/plan-review-NNN.md`.

## Overnight execution

Mike asked (2026-09-23 evening) to continue through the night and run `/deep-review` and
`/review-codex` on the changes. Plans are executed one branch each, `improve/NNN-<slug>`, in worktrees
under `E:\repos\taom-improve\`, and each branch then goes through a deep review (lenses as
`deep-reviewer`), a Codex adversarial review, a verified fix pass and a convergence check. Nothing is
merged or pushed; behaviour-changing review proposals and product decisions are left for Mike. The
live log is `_audit/2026-09-23-opus/PROGRESS.md`; the Status column above is updated as branches finish.

## Findings considered and rejected

- Service locator inside services (an outside skim estimated about 134 resolves): measured 4 of 628.
- A single Bash hook dispatcher: the per-hook prefilter in 013 gets most of the win for less.
- A pre-Steam binding verdict from BUTR betas: refuted, BUTR builds its packages from Steam builds.
- A global `MapInconclusiveToFailed`: it would overturn the recorded skip-on-absence decision; 008
  scopes it to the binding gate and CI.
- A shared `LotrIssue` base class: a static helper for the three identical methods is enough.
- From the quick pass: a "harness diet" (always-on text is about 34 KB after ADR-011), extending the
  validator to TAOM_Map (done in #462), the scanner's `secret-generic` test fixture, a table-driven
  `TaomCulturalFeats`, moving `SupplyOrderScreenVM` sums, `TaomSettings.cs` as a god module.
- June harvest: 21 findings FIXED and 7 STALE on re-triage (`_audit/2026-09-23-opus/triage-*.md`,
  `triage-check.md`); the 70 still-valid ones stay as backlog there.

## Provenance

The June run's first attempt (117 agents) stalled and one agent broke its read-only brief; the skill
was hardened in `141b7494`. The September sprint ran every agent on Opus 5.5, at most four in flight
plus one checker, each with its own findings file, and verified every finding it ranked before writing
a plan. Its full record: `_audit/2026-09-23-opus/`.
