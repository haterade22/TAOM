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
| [008](008-binding-gate-no-silent-skips.md) | Make the binding gate fail loudly instead of passing by skipping | P2 | S | tests | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #652 closed |
| [010](010-ci-on-hosted-windows.md) | Compile and test C# on GitHub-hosted Windows runners against BUTR reference assemblies | P2 | M | dx | 008 | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); first hosted CI run green; #421 open for its Python half |
| [009](009-guarded-patch-category-apply.md) | Apply every Harmony patch category through one guarded helper | P2 | S | bug | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #653 closed (in-game owed); 1.4.5 port owed (D28) |
| [011](011-stop-reminders-and-trunk-guard.md) | Make the four Stop reminders reach Claude and guard the live trunk against force pushes | P2 | S | dx | none | MERGED into `bannerlord-1.5.x` (`cdee2baa`, 2026-09-25); live checks A (Stop reminder reaches the session) and B (PowerShell force push refused) passed headless in the worktree; the guard's remaining parsing gaps moved to plan 027 |
| [012](012-loading-window-trace-per-frame.md) | Stop the loading-window trace walking the stack and flushing a log line every frame | P2 | S | perf | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #655 closed (in-game owed); 1.4.5 port owed (D28) |
| [006](006-crash-capture-boot-cost.md) | Stop the crash-capture sweep costing 30 s of every boot, and delete the four finalizers that can never fire | P2 | S | perf | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #650 closed (in-game owed); 1.4.5 port owed (D28) |
| [007](007-patchshield-skip-callback-shims.md) | Stop PatchShield re-shielding the 247 callback shims at the first game start | P2 | S | perf | none (complements 006) | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #651 closed (in-game owed); 1.4.5 port owed (D28) |
| [014](014-enlistment-session-scope.md) | Reset Enlistment's per-session clocks and caches on load and on a new campaign | P2 | S to M | bug | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #656 closed (in-game owed); 1.4.5 port owed (D28) |
| [016](016-repo-hygiene-pins-readme.md) | Untrack personal and generated files, pin the MCP servers, and correct the README | P2 | S | security | none | MERGED into `bannerlord-1.5.x` (2026-09-25, merge after `a0174c44`); Serena pinned to main `7a296833` (D51), 16 Serena edit tools denied (D58); 1.4.5 port is Mike's (D59); owed: `/permissions` and `/mcp` checks after restart |
| [013](013-bash-hook-prefilter.md) | Let non-git Bash calls skip the Python start-up in every Bash hook | P3 | S | dx | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #661 open until the live harness proof |
| [015](015-warg-tick-costs.md) | Cut the warg behaviour tree's per-tick resolves, allocations and native wrapper churn | P3 | S | perf | none | MERGED into `bannerlord-1.5.x` (`e9b28e2a`, 2026-09-25); #659 closed (in-game owed); 1.4.5 port owed (D28) |
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
| [028](028-mission-tick-profiler.md) | Profile mission frame time and allocation per behaviour and log every hitch | P1 | L | perf | none | REVIEWED (perf run 2026-10-02; `765d3759` on perf/028-mission-tick-profiler after deep review and two convergence rounds; code verified at `7ca09cc2`); awaits the maintainer (FOR-MIKE 16a: the `Mission.OnTick` exclusion) |
| [029](029-perf-runs-parser.md) | Turn taom_debug logs into per-mission perf rows and A/B comparisons | P1 | M | perf / dx | none (shares 028's pinned literals) | REVIEWED (perf run 2026-10-02; `fc141af2` after deep review and two convergence rounds, then the orchestrator's `50028d26` and `43f74556`, records `f92a0bb4`, on perf/029-perf-runs-parser); a convergence pass on `50028d26` owed |
| [030](030-mission-diagnostics-diet.md) | Cut the per-frame and per-hit cost of always-on mission diagnostics, keeping every line | P2 | M | perf | none | REVIEWED (perf run 2026-10-02; `37dab128` on perf/030-mission-diagnostics-diet after deep review and two convergence rounds); awaits the maintainer |
| [031](031-settings-reads-off-hot-paths.md) | Read MCM settings once instead of per blow, per frame and per agent | P2 | L | perf | none | REVIEWED (perf run 2026-10-02; `6d17e69d` on perf/031-settings-reads-off-hot-paths after deep review and a clean convergence round); awaits the maintainer |
| [032](032-worker-thread-formation-patch.md) | Make the worker-thread formation patch and the wield getters cheap and thread-safe | P2 | M | perf / bug | none | REVIEWED (perf run 2026-10-02; records on perf/032-worker-thread-formation-patch after deep review and two convergence rounds, the second clean); awaits the maintainer |
| [033](033-creature-battle-allocations.md) | Cut creature-battle allocations, native wrappers and O(N) scans | P2 | M | perf | none | REVIEWED (perf run 2026-10-02; `06307997` on perf/033-creature-battle-allocations after deep review and two convergence rounds, the hunt commit reverted); awaits the maintainer |
| [034](034-patchshield-per-call-cost.md) | Measure PatchShield's per-call cost and make the no-exception path free if it matters | P2 | M | perf | none | REVIEWED (perf run 2026-10-02; `04894078` on perf/034-patchshield-per-call-cost after deep review and two convergence rounds, Branch B); awaits the maintainer |
| [035](035-release-shader-cache-check.md) | Report shader caches (never refuse, D8) and DLL optimization in the release packager | P3 | S | dx | none | EXECUTED (perf run 2026-10-02; `a2e46e29` on perf/035-release-shader-cache-check, report only), REVIEWED (2026-10-03; records `e403dc7c`) |
| [036](036-anim-memory-probe.md) | Log on-demand animation clip memory against the engine's 12 MiB budget | P1 | M | perf | none | REVIEWED (perf run 2026-10-02; `50df8cbf` on perf/036-anim-memory-probe after deep review and two convergence rounds); awaits the maintainer |
| [037](037-campaign-hot-paths.md) | Cut per-party and per-frame costs on the campaign map | P2 | L | perf | none | REVIEWED (perf run 2026-10-02; `c8c96c09` on perf/037-campaign-hot-paths after deep review and two convergence rounds, Stage D dropped); awaits the maintainer |
| [038](038-battle-equipment-memory-audit.md) | Rank which equipment assets dominate a battle's memory | P2 | M | memory / tools | none | REVIEWED (perf run 2026-10-02; `072d46dc` on perf/038-battle-equipment-memory-audit after deep review and two convergence rounds); awaits the maintainer |
| [039](039-campaign-map-frame-profiler.md) | Attribute campaign-map frame time to TAOM's per-frame map code | P2 | M | perf / diagnostics | 028 | REVIEWED (perf run 2026-10-02; `db777487` on perf/039-campaign-map-frame-profiler after deep review and two convergence rounds, PatchShield option A); awaits the maintainer |
| [040](040-load-time-stamps.md) | Stamp the load-time phases nobody times (LoadXML per id, patch categories) | P2 | S | perf / diagnostics | none | REVIEWED (perf run 2026-10-02; `4e4e65e5` on perf/040-load-time-stamps after deep review and two convergence rounds); awaits the maintainer |
| [041](041-profiler-extensions-and-hitch-probe.md) | Profiler extensions (spawn, script components, clip loading) and an on-by-default hitch probe | P1 | M | perf | 028 | REVIEWED (perf run 2026-10-02; `d7208235` on perf/041-profiler-extensions-and-hitch-probe after deep review and two convergence rounds); awaits the maintainer |
| [042](042-xml-merge-load-time.md) | Cut the 15 to 28 s module-XML merge at every campaign load and custom battle start, byte-identical output | P1 | L | perf / load time | none | REVIEWED (perf run 2026-10-02; `af50ab17` on perf/042-xml-merge-load-time after deep review and two convergence rounds); awaits the maintainer |
| [043](043-offscreen-bone-reads.md) | Measure whether TAOM's own bone reads (warg bites, elephant howdah) see a frozen pose off the screen; force the pose where they do | P2 | S to M | bug | none | TODO (#739; Phase A measures and stops for the maintainer) |

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
