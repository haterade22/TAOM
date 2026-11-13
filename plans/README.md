# Implementation Plans

Working backlog, NOT knowledge base (feature docs live in `docs/features/`). Each plan is a
self-contained fix-spec a separate agent or session can execute with only the plan file and the repo.
Issue rule: AGENTS.md Documentation duty.
Each executor: read the plan fully, honour its STOP conditions, and report back; the index is kept by
the orchestrator.

Three generations live here (plans 020 to 045 belong to the runs their rows name):

- **001 to 005**: `/improve` run of 2026-06-12, written against `141b749`, reconciled on 2026-09-23.
- **006 to 019**: `/improve` review sprint `2026-09-23-opus`, written against `b2e387db`. The report,
  baseline metrics, every finding and every verdict are in
  [`_audit/2026-09-23-opus/REPORT.md`](_audit/2026-09-23-opus/REPORT.md).
- **046 to 054**: `/improve` run `2026-11-12-fable`, written against `7087fa633`. Its report, baseline,
  findings, verdicts and decisions are in
  [`_audit/2026-11-12-fable/REPORT.md`](_audit/2026-11-12-fable/REPORT.md). Every change of that run
  is on the branch `improve/2026-11-12-run` until the maintainer merges it.

## Execution order and status

Recommended order, top to bottom. Status values: TODO, IN PROGRESS, EXECUTED, REVIEWED, MERGED, DONE,
BLOCKED (one-line reason), REJECTED (one-line rationale).

| Plan | Title | Priority | Effort | Category | Depends on | Status |
|------|-------|----------|--------|----------|------------|--------|
| [008](008-binding-gate-no-silent-skips.md) | Make the binding gate fail loudly instead of passing by skipping | P2 | S | tests | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #652 closed |
| [010](010-ci-on-hosted-windows.md) | Compile and test C# on GitHub-hosted Windows runners against BUTR reference assemblies | P2 | M | dx | 008 | MERGED `e9b28e2a`, 2026-09-25; workflows removed `c8d69a0dc`, 2026-10-05 |
| [009](009-guarded-patch-category-apply.md) | Apply every Harmony patch category through one guarded helper | P2 | S | bug | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #653 closed (in-game owed) |
| [011](011-stop-reminders-and-trunk-guard.md) | Make the four Stop reminders reach Claude and guard the live trunk against force pushes | P2 | S | dx | none | MERGED into `bannerlord-1.5.x` (`cdee2baa`, 2026-09-25); live checks A (Stop reminder reaches the session) and B (PowerShell force push refused) passed headless in the worktree; the guard's remaining parsing gaps moved to plan 027 |
| [012](012-loading-window-trace-per-frame.md) | Stop the loading-window trace walking the stack and flushing a log line every frame | P2 | S | perf | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #655 closed (in-game owed) |
| [006](006-crash-capture-boot-cost.md) | Stop the crash-capture sweep costing 30 s of every boot, and delete the four finalizers that can never fire | P2 | S | perf | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #650 closed (in-game owed) |
| [007](007-patchshield-skip-callback-shims.md) | Stop PatchShield re-shielding the 247 callback shims at the first game start | P2 | S | perf | none (complements 006) | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #651 closed (in-game owed) |
| [014](014-enlistment-session-scope.md) | Reset Enlistment's per-session clocks and caches on load and on a new campaign | P2 | S to M | bug | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #656 closed (in-game owed) |
| [016](016-repo-hygiene-pins-readme.md) | Untrack personal and generated files, pin the MCP servers, and correct the README | P2 | S | security | none | MERGED into `bannerlord-1.5.x` (2026-09-25, merge after `a0174c44`); Serena pinned to main `7a296833` (D51), 16 Serena edit tools denied (D58); 1.4.5 port is Mike's (D59); owed: `/permissions` and `/mcp` checks after restart |
| [013](013-bash-hook-prefilter.md) | Let non-git Bash calls skip the Python start-up in every Bash hook | P3 | S | dx | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #661 open until the live harness proof |
| [015](015-warg-tick-costs.md) | Cut the warg behaviour tree's per-tick resolves, allocations and native wrapper churn | P3 | S | perf | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #659 closed (in-game owed) |
| [017](017-build-identity-dirty-flag.md) | Stamp a dirty-tree flag into every build and a structured build field into every crash bundle | P3 | S | dx | none | MERGED into `bannerlord-1.5.x` (`6be50e8f`, 2026-09-25); a DLL built from the merge stamps `+6be50e8f...` with no suffix; CI replay green; in-game crash-bundle check owed; 1.4.5 port is Mike's (D59) |
| [018](018-composition-root-first-steps.md) | Start the feature-module composition root: shared source reader, module contract, empty list, one pilot feature | P3 | M | tech-debt | 009 | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #662 closed (in-game owed) |
| [019](019-nullable-ratchet.md) | Stop discarding the nullable warnings and graduate the first folder to errors | P3 | S | tech-debt | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #660 closed |
| [020](020-changelog-at-release.md) | Generate CHANGELOG release sections at /release from commit bodies instead of hand-editing a shared file (decision 18) | P2 | M | dx | merge after the other improve branches | MERGED into `bannerlord-1.5.x` (`55a72915`, 2026-09-25) last, by its recipe (archive-ok); restart running sessions: they still call the two retired hooks |
| [021](021-architecture-rule-amendments.md) | Amend three architecture rules to match the code: virtual boundary seams, when a service gets an interface, when a patch gets a hook (decision 19) | P2 | S | tech-debt | none | MERGED into `bannerlord-1.5.x` (`b177ecbc`, 2026-09-25); review + Codex + three convergence passes; ADR text by the orchestrator under the bypass (D49, D55, D56); O4 open for Mike |
| [022](022-order-of-battle-auto-assign.md) | Wire the Order of Battle Auto-Assign button to HeroAutoAssigner instead of a stub message (decision 21) | P3 | M | feature | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); in-game check and translations owed |
| [025](025-delete-unreachable-scaffolds.md) | Delete two unreachable scaffolds (IEditorSceneAdapter, EditorCacheRebuild caching) and the tests and binding row that keep them alive (decision 21) | P3 | S | tech-debt | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25) |
| [026](026-seam-decision-logic.md) | Move TAOM decision logic out of seven protected-virtual seams into their services: `SupplyOrderService.ChargePlayer` and `RefundConsumption`, `RefugeService.FindNearestHostile` and `ReleasePeacePrisoners`, `WardenService.CompanionsInMainParty` and `MintCompanionFromTroop`, and the fortification search in `CampService` and `RefugeService` (decisions 56, 57) | P3 | M | tech-debt / testability | none (satisfies ADR-007 as amended by 021) | MERGED into `bannerlord-1.5.x` (`aae624bd`, 2026-09-25); 78 new tests, review + Codex + convergence; in-game smokes owed |
| [027](027-powershell-gate-coverage.md) | Make the eight Bash-only PreToolUse gates cover the PowerShell tool, parse PowerShell syntax, and close validate-push's remaining bypass shapes (decision 61) | P2 | M | dx / safety gates | none | MERGED into `bannerlord-1.5.x` (`35bdf96d`, fixes `dad9b169` and `d2fa6d08`, 2026-09-26); five live PowerShell checks passed headless; issue #682 closed. Open by the maintainer's choice: #680 (validate-push timing on 250 KB and larger commands), #681 (Git Bash here-string hang, predates the plan) |
| [023](_audit/2026-09-23-opus/DECISIONS.md) | Record that nothing reloads TAOM in-process, so new patches need no `ResetForUnload` (decision 22, no plan file) | P3 | S | docs | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25) |
| [024](_audit/2026-09-23-opus/DECISIONS.md) | Managed-only default launch profile, mixed debugger as a second profile (decision 23, no plan file) | P2 | S | dx | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); the next Play is the test |
| [002](002-nan-infinity-config-guards.md) | Reject NaN/Infinity in TroopWeight + Career-mutation config loaders | P1 | S | bug | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #663 closed |
| [001](001-cross-campaign-singleton-resets.md) | Reset feature singletons on new-campaign boundary (Career + SpecialResources) | P1 | S to M | bug / save-integrity | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); in-game smoke owed |
| [005](005-security-hygiene.md) | Scrub vendored credential + pin MCP servers + close faction_map subprocess injection | P2 | S | security | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #668 filed and closed (credential part trimmed, D54); vendored archives left as they are (D52), BUTR not told (D53) |
| [003](003-hot-path-resolve-and-grid-caching.md) | Cache hot-path IoC/MCM resolves + fix 2s SpatialGrid staleness | P3 | S each | perf | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #664 closed (in-game slider check owed) |
| [004](004-live-town-scene-crashes.md) | Fix 3 LIVE town-center scene crashes (Isengard / Helm's Deep / Calembel) | P1 | S (remap) / L (author) | data / crash | none | DONE (2026-06-13; re-verified 2026-09-23: `audit_scene_names.py` shows 0 TAOM_Map misses; Calembel still on the `empire_town_h` stopgap; the `.bak_scenes` backup is gone) |
| [028](028-mission-tick-profiler.md) | Profile mission frame time and allocation per behaviour and log every hitch | P1 | L | perf | none | MERGED `6eb2e1b90`, 2026-10-04 |
| [029](029-perf-runs-parser.md) | Turn taom_debug logs into per-mission perf rows and A/B comparisons | P1 | M | perf / dx | none (shares 028's pinned literals) | MERGED `67eff55cf`, 2026-10-04 |
| [030](030-mission-diagnostics-diet.md) | Cut the per-frame and per-hit cost of always-on mission diagnostics, keeping every line | P2 | M | perf | none | MERGED `e0ad0db76`, 2026-10-04 |
| [031](031-settings-reads-off-hot-paths.md) | Read MCM settings once instead of per blow, per frame and per agent | P2 | L | perf | none | MERGED `74e9172d4`, 2026-10-04 |
| [032](032-worker-thread-formation-patch.md) | Make the worker-thread formation patch and the wield getters cheap and thread-safe | P2 | M | perf / bug | none | MERGED `13f34c1cb`, 2026-10-04 |
| [033](033-creature-battle-allocations.md) | Cut creature-battle allocations, native wrappers and O(N) scans | P2 | M | perf | none | MERGED `efe326242`, 2026-10-04 |
| [034](034-patchshield-per-call-cost.md) | Measure PatchShield's per-call cost and make the no-exception path free if it matters | P2 | M | perf | none | MERGED `5ee49d05f`, 2026-10-04 |
| [035](035-release-shader-cache-check.md) | Report shader caches (never refuse, D8) and DLL optimization in the release packager | P3 | S | dx | none | MERGED `e4c2c159a`, 2026-10-04 |
| [036](036-anim-memory-probe.md) | Log on-demand animation clip memory against the engine's 12 MiB budget | P1 | M | perf | none | MERGED `a9f10688f`, 2026-10-04 |
| [037](037-campaign-hot-paths.md) | Cut per-party and per-frame costs on the campaign map | P2 | L | perf | none | MERGED `423d948d8`, 2026-10-04 |
| [038](038-battle-equipment-memory-audit.md) | Rank which equipment assets dominate a battle's memory | P2 | M | memory / tools | none | MERGED `3dac7ab5b`, 2026-10-04 |
| [039](039-campaign-map-frame-profiler.md) | Attribute campaign-map frame time to TAOM's per-frame map code | P2 | M | perf / diagnostics | 028 | MERGED `f6bf0e588`, 2026-10-04 |
| [040](040-load-time-stamps.md) | Stamp the load-time phases nobody times (LoadXML per id, patch categories) | P2 | S | perf / diagnostics | none | MERGED `346f3e093`, 2026-10-04 |
| [041](041-profiler-extensions-and-hitch-probe.md) | Profiler extensions (spawn, script components, clip loading) and an on-by-default hitch probe | P1 | M | perf | 028 | MERGED `daba6f0a0`, 2026-10-04 |
| [042](042-xml-merge-load-time.md) | Cut the 15 to 28 s module-XML merge at every campaign load and custom battle start, byte-identical output | P1 | L | perf / load time | none | MERGED `a27c6b338`, 2026-10-04 |
| [043](043-offscreen-bone-reads.md) | Measure whether TAOM's own bone reads (warg bites, elephant howdah) see a frozen pose off the screen; force the pose where they do | P2 | S to M | bug | none | TODO (#739; Phase A measures and stops for the maintainer) |
| [044](044-nogame-unit-step-green.md) | Make the no-game unit step green: tag or fix the 7 tests that run engine code on BUTR's stubs | P3 | S | tests | none | TODO (#1038 item 14; Step 0, a `/verify` row for the no-game unit step, waits on the maintainer) |
| [046](046-test-suite-read-caches.md) | Read the C# corpus once per test run: cache code-root text in `RepoPaths`, skip `Languages` in two character scans, compile each shipped XSLT once | P2 | M | tests | none | TODO (#1132) |
| [047](047-fold-safe-twins.md) | Fold three behaviour-identical twins: one LotrIssue rules helper, one CultureStageView type probe, one supply row view model | P3 | S to M | tech-debt | none | TODO (#1132) |
| [048](048-delete-dead-members.md) | Delete eight members nothing calls and correct the comments that still describe them | P3 | S | tech-debt | none | TODO (#1132) |
| [049](049-refuge-load-repair-unit.md) | Extract RefugeService's save-load repair into one unit with lazy clock seams | P3 | S | tech-debt | none | TODO (#1132) |
| [050](050-messenger-arrival-classification.md) | Move the messenger arrival verdict out of the largest ADR-002 entry point into MessengerService as one tested classification | P3 | M | tech-debt | none | TODO (#1132; keeps today's behaviour, see #1133) |
| [051](051-tools-dead-scripts-and-test-speed.md) | Tools: delete three dead scripts, correct the catalog's environment section, cut repeated parsing from two tool tests | P3 | S | dx | none | TODO (#1132) |
| [052](_audit/2026-11-12-fable/REPORT.md) | Harness text: remove two duplicated passages from CLAUDE.md, raise the `/verify` discovery floors, run filtered tests per fix in `/review-codex` (no plan file; made by the orchestrator) | P3 | S | dx | none | DONE on `improve/2026-11-12-run` (`e88fa0050`, 2026-11-12; #1132); not on the trunk |
| [053](_audit/2026-11-12-fable/REPORT.md) | Hooks: parse the edited path in bash in the polearm hook; cut session-start lines that repeat the harness's own context (no plan file; made by the orchestrator) | P3 | S | dx | none | DONE on `improve/2026-11-12-run` (`2a913ecdf`, 2026-11-12; #1132); hook suite 1271 passed; not on the trunk |
| [054](_audit/2026-11-12-fable/REPORT.md) | Docs: correct wrong author guidance, add the Realms row to the feature map, add a search hint to the three navigation maps (no plan file; made by the orchestrator) | P3 | S | docs | none | DONE on `improve/2026-11-12-run` (`4a9e001be`, 2026-11-12; #1132); not on the trunk |

Plans 006 and 007 were written as P1 on the assumption that every player pays their boot and load
seconds. A follow-up measured players at about 1 to 3 s (this desktop runs about 30 times slower), so
this index lists them as P2 behind the CI and gate work; see the report's "The slow desktop".

## Dependency notes

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
