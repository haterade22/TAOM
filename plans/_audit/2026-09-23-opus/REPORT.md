# Review sprint `2026-09-23-opus`: report

Baseline commit `b2e387db` on `bannerlord-1.5.x` (Bannerlord v1.5.3). Advisor run through `/improve`
at effort `deep`, every agent on Opus 5.5, at most four in flight plus one checker. Every number below
was measured this run; the raw evidence is in this folder (see "Where the evidence is").

## Summary

1. No C# is compiled by CI on any branch; hosted runners on BUTR's v1.5.3 packages are proven (plan 010).
2. TAOM Harmony-patches the same 247 engine callback shims twice, behind a toggle that cannot work: about
   1 to 3 s for players, but about 100 s per launch on this desktop, which runs slow (plans 006, 007).
3. The engine-drift gate can print "Passed!" with 335 of 368 checks skipped (plan 008).
4. One drifted Harmony target fails TAOM's boot or loops a failed load: 64 unguarded applies (plan 009).
5. Four harness Stop reminders never reach Claude; the live trunk has no force-push guard (plan 011).
6. June plans: 001 and 002 PARTIAL, 003 and 005 TODO, 004 DONE. June harvest (99): 21 FIXED, 7 STALE,
   70 STILL VALID, none HIGH today.
7. 42 new findings plus two follow-ups; 36 adversarially checked: 35 confirmed, 1 refuted. Duplication is
   low (0.69%); the service-locator lead did not survive measurement (4 of 628 resolves in services).
8. 14 plans written (006 to 019), covering all ten seeds approved earlier.

## Baseline metrics

| Metric | Value |
|---|---|
| Build `Main/TAOM.csproj` (fresh worktree / no-op) | 8 s / 4 s, 0 errors, 2 warnings (BUTR analyzer false positives, settled) |
| Test suite | 10,239 tests: 10,235 passed, 2 failed, 2 ignored; 30 s run, 40 s with build |
| The 2 failures | Elk and Animalia tests reading the live, unversioned Armory another session is editing; HEAD's verdict depends on install state |
| Nullable warnings hidden by `NoWarn` | 2,028 (CS8618 756, CS8603 408, CS8604 331, CS8625 285, CS8600 108, CS8601 78, CS8602 62) |
| `validate_moduledata.py` | 31 s, 0 errors, 1,591 warnings (1,348 are the known armour-ladder backlog) |
| `lint_docs.py --fail-on-drift` | exit 0; 0 dead links; 7 path-scoped rules over their size cap |
| Duplication (jscpd, C#, 70 tokens) | 76 clones, 1,114 of 160,344 lines (0.69%) |
| CI | `build.yml` triggers on `bannerlord-1.4.5` only; 0 self-hosted runners; its non-C# jobs red 82 runs in a row since 2026-09-01 |
| Hooks per Bash call | 13 processes; 219 to 360 ms each on a non-git payload (2.8 s serial) |
| Repository | HEAD tree 1,210 MB (PNG 501, PSD 324, WAV 112); pack 3.27 GiB, 77.9% superseded binaries; no LFS |
| `CHANGELOG.md` | 1.75 MB; touched by 545 of 809 commits since 2026-07-01; 7 of 29 merges had a CHANGELOG conflict hunk |
| Commit subjects since the branches split | 10 of 95 miss the house format |
| Boot and first load (this desktop) | Native2Managed attach 29 to 33 s on 30 of 30 boots; PatchShield pass 2 69.1 to 69.4 s on 67 of 67 first game starts since 2026-09-04 |

Full table and methods: `baseline.md`.

## Reconcile of the June plans

| Plan | Status now | What to do with its branch |
|---|---|---|
| 001 singleton resets | PARTIAL: Career landed (`f4273639`); SpecialResources storage still leaks | `impl-001`: rewrite the SpecialResources half fresh; its test targets a constructor that changed |
| 002 NaN guards | PARTIAL: TroopWeight landed (`bee07b48`); `MutationParams.GetFloat` unguarded | `impl-002`: cherry-pick `cfc47206` only, trunk side on conflicts |
| 003 hot-path resolves | TODO; PERF-03 STALE; Warg part superseded by plan 015 | `impl-003`: `6eb5955c` applies clean; `4962f3ee` is folded into 015 |
| 004 scene crashes | DONE, re-verified (0 TAOM_Map misses; Calembel still on a stopgap) | none |
| 005 security hygiene | TODO; MCP pins folded into plan 016; the credential now sits only in three gitignored tarballs | `impl-005`: cherry-pick `4310aa6e`, `4bc520a1`; drop `1d566be6` |

Evidence: `reconcile.md`, with each claim spot-checked by the orchestrator.

## Top 10 by leverage

Ranked by impact over effort, discounted by confidence and fix risk. Impact is the adversarial checkers'
recalibrated level where it differs from the lane's.

| # | Finding | Why it matters | Effort | Checked | Plan |
|---|---|---|---|---|---|
| 1 | No C# compiled by CI anywhere (TEST-L5-07, seed F1) | every merge is uncompiled by CI; the self-host advice is unsafe on a public repo; hosted CI on BUTR packages is proven by probe | M | 2 lenses | 010 |
| 2 | Binding gate passes by skipping (TEST-L5-01, TEST-L5-02) | the gate every engine bump relies on can be green with 335 of 368 checks skipped; the hosted CI in #1 would be green the same way | S | 2 lenses | 008 |
| 3 | 64 unguarded `PatchCategory` calls (COMP-01) | a renamed engine target fails the boot (13 sites) or aborts the game-init batch, crash guards included, into a load that retries every tick (50 sites) | S | 2 lenses | 009 |
| 4 | Stop reminders never reach Claude (DX-L6-06); live trunk unguarded (DX-L6-03) | the machine backstops for build-before-done, review-before-close and tagging are silent; `bannerlord-1.5.x` carries the release tags and no guard | S | 1 | 011 |
| 5 | Loading-window trace (PERF-L4-02) | a stack walk plus a flushed log line every frame on the menus: 262,763 lines (about 84 MB) in one 35-minute session, shipped inside crash bundles; players pay this | S | 1 | 012 |
| 6 | Native2Managed sweep (PERF-L4-01) and PatchShield re-shield (PERF-FU-PATCHSHIELD-01) of the same 247 callback shims | 494 redundant patches per process behind a dead toggle, 0 captures in 30 logs and 0 shield swallows in 466 sessions; about 1 to 3 s for players (11 player processes measured), about 31 s at boot plus 69 s at the first load on this desktop | S + S | 2 lenses + orchestrator re-measure | 006, 007 |
| 7 | Enlistment session scope (seed F3, CORRECTNESS-07) | after loading an earlier save or starting a new campaign, the dwell anchor defers the exit sweep and the leave-on-arrival offer dies; a new campaign never reaches the reset point at all | S to M | 1 each | 014 |
| 8 | Tracked personal and generated files, unpinned MCP servers, wrong README (F5, F6, F8, DOCS-L6-08) | every clone gets Mike's pre-approvals (including `Bash(node:*)`) and a 37 MB unreferenced pickle; MCP servers run unpinned upstream code each session; the README sends players to the wrong beta | S | 1 each | 016 |
| 9 | Bash hook prefilter (DX-L6-02) | about 82% of Bash calls would skip a Python start-up in every hook | S | 1 | 013 |
| 10 | Warg per-tick costs (F4, PERF-L4-03, PERF-L4-04, June PERF-06/07) | per-tick container resolves, a new list per query, 343 cell lookups per 60 m scan, native wrappers for allies and far targets | S | 1 each | 015 |

Next by leverage: build identity dirty flag (DX-L6-04, plan 017), composition-root first steps
(seed F9, COMP-03, TEST-L5-03, plan 018), nullable ratchet (F10, CORRECTNESS-01, plan 019), NativeSkinFixes
extraction (ARCH-04), dead scaffolds and the player-visible "Phase-1 stub" button (ARCH-02), per-site
suppressed-failure counter (CORRECTNESS-02), singleton behaviors reused across campaigns (COMP-02).

**Ranking note.** The critic asked whether the large boot and load seconds are universal. They are not
(`followup-patch-tax.md`): 11 player processes from at least three installs attach the same 247 patches
in 0 to 1 s, while this desktop takes 29 to 34 s. So #6 is ranked on its player cost; on this desktop it
is still the largest single cost of the development loop.

## Lanes

**Lane 1, composition and wiring** (`lane-1.findings.md`). `SubModule.cs` grew from 758 to 2,148 lines
since June; 38% of feature commits touch a single-owner file. The lane's design (explicit ordered module
list, two-phase registration, each ordering constraint turned into a list-index test, a migration where
`SubModule` only loses lines after step 1) is the most valuable single output of the run and is the basis
of plan 018. Other confirmed findings: 64 bare patch applies (COMP-01), 22 singleton CampaignBehaviors
reused across campaigns (COMP-02, LOW), text-scraping wiring tests that pass on commented-out code
(COMP-03). Refuted by measurement: service-locator creep in services.

**Lane 2, abstraction economy** (`lane-2.findings.md`). Duplication is low; the valuable output is
policy. About 850 lines of unreachable scaffolding plus a player-visible stub button (ARCH-02); a
static `ReflectionHelper` whose 14 engine bindings sit outside the binding gate (ARCH-06); parked
NativeSkinFixes ships 207 KB of never-loaded native code and cannot be re-enabled as written (its
signatures target v1.4.6; ARCH-04); three written rules that disagree with the code or each other
(ARCH-01, ARCH-03, ARCH-05, see Direction). A shared LotrIssue base class was considered: the checker
found the save-break warning overstated, but the fold is still small.

**Lane 3, correctness and resilience** (`lane-3.findings.md`). 2,028 hidden nullable warnings and a
per-folder ratchet (CORRECTNESS-01); 574 `catch (Exception)` plus 476 bare `catch`, 324 of them silent,
and nothing counts them into a crash bundle (CORRECTNESS-02); four crash-capture finalizers on empty
base methods that can never fire (CORRECTNESS-04); session state wired ad hoc (CORRECTNESS-07); Patch88's
analyzer warning settled as a false positive (it binds).

**Lane 4, performance** (`lane-4.findings.md`, `followup-patchshield.md`). The boot and first-load
Harmony costs (#1), the per-frame loading-window trace (#6) and the warg tree (#10). Frame-time numbers
are bounded estimates; the log gaps are measured.

**Lane 5, test effectiveness and CI** (`lane-5.findings.md`). Proved the silent-green path with a probe
(#3); measured the RequiresGame set by running against reference assemblies (100 classes in 92 files,
not the 102 files a `using` proxy gives); designed hosted CI (#2); 47 source-scraping tests with nine
copy-pasted locators (TEST-L5-03); a mutation-testing spike design on AlignmentRecruitment (Stryker.NET
5.0.0 is current, not 4.16.0). Refuted: a pre-Steam binding verdict (BUTR builds its packages from
Steam builds).

**Lane 6, repo, release and harness** (`lane-6.findings.md`). Gates that exist only as Claude hooks miss
IDE, terminal and Codex commits (DX-L6-01; 10 of 95 subjects missed); hook cost (#9); the dead Stop
reminders and the unguarded trunk (#5); build identity: the SDK already stamps the git SHA, the gap is a
dirty-tree flag, a structured crash-bundle field and tag-only release builds (DX-L6-04); repo weight
(DEPS-L6-05); CHANGELOG generation (DOCS-L6-07); README drift (DOCS-L6-08).

## Direction proposals (maintainer's call)

1. **Adopt the feature-module composition root** (Lane 1 design, plan 018 starts it). Cost evidence:
   `SubModule.cs` 758 to 2,148 lines in three months despite the June extraction, 131 commits touching it
   since June, 26 test files pinning its spelling, and every feature forced through one single-owner
   file. It also makes parking a feature one line and turns the ordering folklore into tests.
2. **Generate the CHANGELOG at `/release` instead of hand-editing it** (DOCS-L6-07). Cost evidence:
   1.75 MB, touched by two thirds of all commits, 7 of 29 merges with a CHANGELOG conflict hunk, and a
   recorded data-loss class behind three git-safety rules. It needs an AGENTS.md change ("CHANGELOG.md is
   updated every session"), so it is a policy decision.
3. **Amend three rules to match the code** (ADR challenges; Lane 2, confirmed): record protected-virtual
   boundary seams as an ADR-007 exception with conditions (4 services, 94 members, all tested) instead of
   treating them as violations (ARCH-05); settle the interface-per-service rule as "every adapter has an
   interface; a service gets one only when a test fakes it or a second implementation exists" (ARCH-01;
   its origin is ADR-002:247,291 and the deep-review lens, while `think-before-coding.md` forbids
   single-implementation interfaces); make the patch-to-hook-interface-to-service chain conditional
   (ARCH-03; it describes 11% of 217 patches).
4. **Stop the repo growing and pick a versioning story for what ships** (DEPS-L6-05). 77.9% of the pack is
   superseded binaries; sparse checkout for code worktrees saves about 45% per worktree today; LFS going
   forward for asset sources. It interacts with the standing "the mirror stays local" decision, so it is
   raised, not planned.

## The slow desktop (answered)

Players run fast; the slow mode is local to this desktop, and it slows all of managed start-up (the phase
before TAOM.Dependencies loads takes 7.4 s in slow sessions against 0.5 to 0.8 s in fast ones), not only
Harmony. Most likely cause, UNVERIFIED: `Main/Properties/launchSettings.json` launches the game with
`"debugEngines": "managed-framework,native"` (both TAOM profiles, lines 8 and 17), a change that landed in
`4236e6c9` (2026-06-13) and was in the working tree before the first slow launch on 2026-06-12 14:08.
One launch settles it: start the same profile with Ctrl+F5 (no debugger) or switch it to managed-only
debugging, then read pass 2's time in `Modules/TAOM.Dependencies/diag.log` (about 2.5 s if this is the cause).

## Environment (reported, not changed)

- **C: is full.** 2.7 GB free when found (446 GB volume). The run deleted only its own scratch (about
  4.4 GB) and moved all execution worktrees to E:. The game, the NuGet cache and %TEMP% live on C:.
- **Managed start-up is about 10 to 30 times slower on this desktop since 2026-06-12** (about 186 ms per
  Harmony patch against 5 to 10 ms for players). Likely the mixed managed and native debugger in
  `launchSettings.json` (see "The slow desktop"); one Ctrl+F5 launch confirms or rules it out.
- **`core.hooksPath` points at a folder that does not exist** (`.git/config`:
  `c:\Users\mikew\source\repos\TAOM\.git\hooks`, the old checkout path), so no git hook runs for any
  client; every commit-time rule depends on Claude hooks alone (DX-L6-01).
- CI: 0 self-hosted runners, no `BANNERLORD_GAME_DIR` repository variable, and the non-C# jobs on
  `bannerlord-1.4.5` red 82 runs in a row since 2026-09-01.

## Decisions for Mike

- Crash capture's Native2Managed default: plan 006 keeps it ON and narrows it to an allowlist; turning
  it OFF by default is a posture change for you (two docs rely on it as a safety net).
- The `impl-001`, `impl-002`, `impl-003`, `impl-005` branches: the reconcile table says what each is worth.
- GitHub rulesets on `bannerlord-*` (block force push and deletion): an outward-facing step, written into
  plan 011 for you to do.
- Direction items 2, 3 and 4, and the Order of Battle "Auto-Assign is a Phase-1 stub" button (remove it,
  or wire `HeroAutoAssigner`; ARCH-02).
- Whether any tool reloads TAOM in-process (COMP-05): if not, 14 `ResetForUnload` methods can stop growing.

## Considered and rejected

- Service locator inside services (outside lead: about 134 resolves): 4 of 628 are in services.
- A single Bash hook dispatcher (DX-L6-02 step 2): the per-hook prefilter gets most of the win.
- A pre-Steam binding verdict from BUTR betas (TEST-L5-08): refuted, BUTR builds from Steam builds.
- "Harness diet", extending the validator to TAOM_Map, the `secret-generic` scanner fixture, a
  table-driven `TaomCulturalFeats`, moving `SupplyOrderScreenVM` sums, `TaomSettings.cs` as a god module:
  rejected in the quick pass (reasons in `BRIEF.md`).
- A shared `LotrIssue` base class as the duplication fix: a static helper for the three identical
  methods is enough (ARCH-07).
- Global `MapInconclusiveToFailed`: it would overturn the recorded skip-on-absence decision for every
  test; plan 008 scopes it to the binding gate and CI instead.

## Not audited

From the critic (`critic.md`), measured: 14 of 109 feature folders have no cite in any audit file
(about 4% of the code; the largest are DevConsole, AiPartySize and MapEventGuard); CareerSystem,
CultureDoctrine, BattleLoadDiagnostics, CulturalFeats and CoopInterop got only incidental cites; co-op
correctness, memory footprint and off-thread callbacks got no lane; `tools/` (329 scripts) is almost
unread; the playbook's test-coverage question ("which untested code is dangerous"), game data beyond
the validators, and a fresh direction pass over the 99 open GitHub issues were not done. Six lane findings
never reached a checker (COMP-04, COMP-05, COMP-06, ARCH-08, CORRECTNESS-03, CORRECTNESS-06). 67 of the
70 still-valid June verdicts rest on one triage agent. The critic closed several gaps with quick checks
(security scan, injection sweep, four game-data checkers, localization structure): none changes the ranking.

**Open GitHub issues that overlap findings** (dedupe before filing anything): #491 (CORRECTNESS-01),
#593 (DX-L6-04), #623 (DX-L6-01), #573 and #578 (CORRECTNESS-07, F3), #607 (off-thread), #501 (branches).

## Where the evidence is

`BRIEF.md` (the shared agent brief and the ten seeds), `baseline.md`, `jscpd-digest.md`, `reconcile.md`,
`triage-A.md`, `triage-B.md`, `triage-C.md`, `triage-check.md`, `lane-1.findings.md` to
`lane-6.findings.md`, `verify-*.md` (one file per refuter job), `critic.md`, `followup-patchshield.md`,
`followup-patch-tax.md`, `plan-briefs.json` (what each plan writer was given), `plan-review-NNN.md` (the
cold reviews), `PROGRESS.md` (the run log and workflow ids).
