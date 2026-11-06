# Lessons — Build, Tooling & Workflow

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Build, Tooling & Workflow lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### A fix verified against the artifact that motivated it cannot see the defect the fix introduces
After changing a log line, a parser, or a data shape, re-running the ORIGINAL reproduction proves
nothing about the new behaviour: the original evidence predates the change, so it exercises the old
shape. The 2026-09-04 battle-load work added a `shaders=N` token to the `WATCHDOG STILL LOADING`
line, then verified the triage tool by replaying the bundle that motivated the work. It passed every
time, because that bundle's log predates the token. The token in fact broke
`triage_battle_load.py`'s `_WATCHDOG_RE`, which required whitespace exactly where the token now sits,
so every FUTURE bundle would have had its watchdog line silently dropped from the report.
- **Why missed:** the replay felt like verification because it ran the real tool over a real log and
  produced a correct-looking report. The test fixture had the identical blind spot: `_watchdog()`
  emitted the pre-change shape, so the regex stayed pinned against a string the emitter no longer
  produced, and the suite stayed green through the break.
- **Prevent:** when a change alters an emitted shape, write the test from the NEW shape first, and
  update every fixture that constructs the old one. Treat a fixture no emitter can produce any more
  as a defect in its own right. Re-running the old repro is a regression check, not a verification of
  the new path, and the two must not be confused in a completion claim.
- **Source:** docs/reviews/rca-battle-load-render-wait-2026-09-04.md finding 5 (HIGH), issue #539.

### Two summarizers over one dataset must agree on their scope, or the report contradicts itself
When one report renders several sections computed independently from the same input, each section's
scoping rule is a separate decision, and two rules that disagree produce an artifact that is
internally inconsistent while every individual section is "correct". `triage_battle_load.py` scoped
its VERDICT to the last mission (anchoring on any mission-start marker) but left the bucket LEDGER
anchoring on `MissionInitialize` alone, so a log whose last mission stalled before reaching that
marker printed a stall verdict directly above a healthy timing table belonging to an earlier,
completed mission.
- **Why missed:** the scoping bug was found and fixed in ONE consumer, and the fix never asked which
  other consumers read the same list. The regression test asserted only the verdict, on a log with no
  bucket markers, so the ledger returned None and the divergence could not appear.
- **Prevent:** after changing how one section selects its data, grep for every other function reading
  the same collection and either share the selector or make the disagreement impossible (render
  nothing rather than something true about the wrong subject). Add at least one test asserting on the
  WHOLE rendered artifact, since self-contradiction is only visible from the assembled output.
- **Source:** docs/reviews/rca-battle-load-render-wait-2026-09-04.md finding 6 (HIGH), issue #539.

### Prove no engine loader reads a file before excluding it from packaging
A `package_release.py` rule excluded `ModuleData/project.mbproj` as "editor-only, not read at runtime". Both halves were false. The SHIPPING runtime calls `XmlResource.GetMbprojxmls(module.Id)` for every module (`TaleWorlds.MountAndBlade`, `Module.LoadSubModules`), reads that file's `<file>` nodes, and registers them as native resources through `MBObjectManager.GetMergedXmlForNative`. That loader is **disjoint** from `SubModule.xml`'s `<XmlName>` glob, so anything registered there appears nowhere else: TAOM's mbproj is the only registration for four voice-definition XMLs and `module_sounds.xml`, and `LOTRLOME_Armory`'s is the only one for its monsters and action sets, whose absence is a documented native spawn CTD. The release zip would have shipped silent audio and missing monsters while the repo, the validator, and 6,454 tests all stayed green.
- **Why missed:** the file was classified from its NAME and from three dev-path elements inside it, without decompiling the loader. "Research First" was applied to the sibling `.vs` copy mechanism in the same commit and simply not applied here. A packaging exclusion is invisible to every gate TAOM has: nothing tests the zip.
- **Prevent:** an exclusion rule is an engine claim, so it needs engine evidence. Decompile the loader, or grep the shipping assemblies for the filename, before adding one. `project.mbproj` specifically is now documented at the exclusion site and pinned by a `COPY` regression test. Note the lone exception that does NOT generalise: `TAOM_Map`'s mbproj uses `<Module>` elements rather than `<file>`, so it genuinely is inert.
- **Source:** docs/reviews/rca-provenance-register-2026-08-13.md finding #1.

### A machine-shaped document with no machine is worse than a prose one
A provenance register was authored with a format designed for machine validation: glob columns as a coverage contract, backticked tokens as an allowlist, a shrink-only baseline. The validator was a later phase and did not exist. Five `Covers` globs were wrong (two pointed at a directory that does not exist, two asserted the wrong party's authorship over a dozen DLLs, one missed the only file carrying the derivation it described) and nothing said so. The rule and two other docs also described the checker, a hook, a test class, and a CI step in the present tense; none existed.
- **Why missed:** the design was written as the end state and shipped as the current state. Twelve of the changeset's fifteen review findings were an artifact asserting something about another artifact with nothing checking the assertion, which is the same defect the changeset existed to fix, one level up.
- **Prevent:** build the validator in the same change, or state plainly in the document that it is hand-maintained and future-tense every unbuilt component. A machine-shaped document invites the reader to trust its shape; without the machine that trust is unearned. This is `evidence-over-claims` §C applied to one's own roadmap, the case the rule does not spell out.
- **Source:** docs/reviews/rca-provenance-register-2026-08-13.md, root-cause pattern.

### After a whole-word identifier rename sweep, run a substring sweep over tests and docs
A vocabulary rename executed with `\b`-bounded regex (correct — it protects engine-owned compound identifiers such as the `isShruggedOff` engine parameter) leaves embedded occurrences untouched: 10 test METHOD NAMES still carried old tokens (`…HeroWithHorseChargeDamagePassive…`, `…CustomResourceGain_ScalesEarning`) and 2 feature docs still used old names, because compound identifiers have no word boundary at the seam and the sweep's file-set excluded `docs/`.
- **Why missed:** the boundary choice was deliberate and verified for production code, so "sweep complete" was declared off the whole-word grep; the deep-review completeness agent re-checked with the same whole-word assumption and also passed it — only an independent substring grep (Codex) caught the survivors. One grep methodology is not completeness evidence.
- **Prevent:** every identifier rename sweep ends with a SECOND, substring (no `\b`) grep over `TAOM.Tests/` and `docs/`, triaging each hit (rename test names/docs; leave engine-owned identifiers). Negative "unknown value" tests use synthetic tokens (`NoSuchEffectType`), never retired real names.
- **Source:** docs/reviews/rca-career-enum-prefab-cleanup-2026-07-06.md.

### Every numeric claim in a CHANGELOG/doc/commit body comes from a command run this session, not recall
The CultureConversion notable-replacement CHANGELOG entry claimed "+10" new tests; the diff had 9. The number was recalled ("about ten") while authoring the entry instead of counted. Second confirmed instance of the evidence-over-claims §C fabrication class (first: the 2026-05-30 hotfix-review doc authored before its proving diff was read).
- **Why missed:** the entry was written at the end of a long implementation flow where the author "knew" the test count; §C's never-invent list explicitly includes counts, but the rule fired on tool OUTPUT recall, and a self-authored diff felt exempt. It isn't — your own diff is tool output too.
- **Prevent:** before writing any count/percentage/id into a durable artifact, produce it with a command in the same session (`git diff | grep -c "\[TestMethod\]"`, `grep -c`, `wc -l`) and paste from that output. The deep-review completeness agent cross-checks CHANGELOG numerics against the diff — keep that check in its prompt.
- **Source:** docs/reviews/rca-culture-conversion-notables-2026-07-03.md (finding 1); prior instance `feedback_no_write_before_reading_tool_output.md`.

### Parallel-builder briefs: shared sub-problems get ONE prescribed solution in the contract
When fanning a feature out to parallel builder agents, any sub-problem two or more builders both face (id normalization, NaN handling, validation invariants, hot-path allocation patterns) must be solved ONCE in the shared contract/brief — never left to per-builder judgment. Independently-correct builders otherwise produce divergent solutions at the seams: CombatMechanics' two crush/cleave services normalized settlement-variant monster ids differently (runtime Substring-per-hit vs construction-time set expansion), which was both a per-hit allocation AND a config-semantics inconsistency (a suffixed config entry matched in one service and not the other); the MCM slider and the JSON validator enforced different ordering rules for the same value.
- **Why missed:** each builder passed its own brief — the orchestrator's brief itself prescribed the allocating helper for one builder while the sibling invented the better pattern. No single author saw both sides; per-component review can't catch cross-component divergence.
- **Prevent:** before dispatching parallel builders, list the sub-problems that appear in more than one brief and pin ONE solution in the shared contracts. After integration, run a cross-consistency review pass (data-flow + efficiency agents over the seams, not just per-file checks).
- **Source:** docs/reviews/rca-combat-mechanics-2026-07-02.md (findings 1+6).

### Substring keyword matching on names false-matches — use word-boundary or an explicit allowlist
A classifier that flags items by `if marker in (id + name).lower()` will over-match when the marker is a substring of an unrelated word. The `analyze_troop_balance.py` creature detector used `'mount'` as a marker and wrongly exempted **46 troops** — every `mistymountainorcs_*` ("misty**MOUNT**ainorcs") plus every "Mountain Guard" — from formula judging, and warg/elephant *rider* markers swept in humanoid riders too. The fix narrowed the exempt set to genuine non-humanoid tokens (`troll`, `creature`) + an explicit id allowlist (`{cave_troll}`), and moved riders to a separate non-exempt tag.
- **Why missed:** the marker list was written for the obvious case (elephant/spider/mount troops) without auditing what *else* those substrings hit across the real id/name space. A 47-count "special troops" total in the first run was the tell — far more than the ~2 genuine creatures.
- **Prevent:** for any keyword classifier over names/ids, prefer an explicit id allowlist for the precise cases, and use token/word-boundary matching (not bare `in`) for heuristic markers. Sanity-check the match COUNT against the expected magnitude before trusting it; an order-of-magnitude surprise means the matcher is wrong, not the data.
- **Source:** docs/features/troop-skill-balance.md (2026-06-24 rebaseline — analyzer creature-detection bug)

### Never restate a build-file version in prose — point at the file
Any doc, comment, or table that copies a version out of a csproj/stub/DLL is a drift site *by construction*: the value doesn't drift, the copy does — and the copy is exactly what the next maintainer trusts. The BUTR bump found 37 stale version lines across 6 files, incl. a loader-manifest comment block, a legal attribution file, and `dr3-maintenance.md`'s own Category 1 table. That table had already been re-synced once (May 2026) and had drifted again by July; its stub rows were logged broken in June (`plans/_audit/2026-06-12-harvest.md` DEPS-05/06) and were still broken — now version-wrong *and* policy-wrong.
- **Why missed:** re-syncing feels like the fix. It isn't — it resets the clock without removing the mechanism, which is why the same table rotted twice in three months. Also: I updated the two stub comments I had open to bump `<Version>` and never swept for sibling declarations, so an untouched file's six stale lines survived. Fixing what you read ≠ fixing what exists.
- **Prevent:** delete the duplicated value and point at the authoritative file; if it must be asserted, assert it in a test (never as a literal — a test that restates a version is one more drift site). Any targeted version edit ends with a repo-wide grep for sibling declarations, including `*.txt` licence/attribution files and XML comments.
- **Source:** docs/reviews/rca-butr-dependency-update-2026-07-16.md (patterns A + D).

### Two files that must agree get a test in the same change
If a change requires two artifacts to stay in sync and the compiler can't enforce it, prose cannot either. TAOM's Native engine pin sat at `v1.4.5.*` through **both** the 1.4.6 and 1.4.7 bumps, and the vendored BUTR impl set stayed capped at the game-1.4.1 build while upstream shipped through 1.4.5 — the whole time, 4212 tests passed. There was no failure signal because there was no assertion; both `/engine-bump` and `dr3-maintenance.md` *described* the coupling in prose.
- **Why missed:** the tell is that the drift is exactly as old as the last time a human remembered — the signature of a memory-based guard. Procedures that say "also bump X" are guards that fail silently and leave no trace.
- **Prevent:** write the coupling test in the same change that creates the coupling. `BundledDependencyManifestTests` now pins compile-pin parity, the v99 stub derivation, vendored-DLL version homogeneity, the Native↔pinned-engine coupling, and licence attribution — asserting *relationships*, never literals. Verify the test RED against the pre-fix state before trusting it.
- **Source:** docs/reviews/rca-butr-dependency-update-2026-07-16.md (pattern B).

### A safety argument that also holds in the failure case is not evidence
When justifying "this is safe," test the argument against the unsafe hypothesis before accepting it. Claim made: *"Patch41 only rewrites `BottomToTop`→`TopToBottom`, therefore it can't double-invert the corrected MCM screen."* True — and worthless. In the dangerous world (MCM fixes the ordering in code, leaves the attribute) Patch41 is **still** one-directional, **still** flips, and the screen **still** re-inverts. The cited property was constant across both hypotheses, so it discriminated neither. The conclusion happened to be right; the reasoning could not have caught it being wrong.
- **Why missed:** the property was true, verifiable in the source, and felt like proof. Reasoning from an appealing invariant substituted for going and reading the actual artifact — a changelog line ("Fixed mod list was upside down") was trusted as a claim about *intent* when the prefab bytes were available as *fact*.
- **Prevent:** ask "would this same sentence be true if the thing were unsafe?" If yes, it proves nothing — go get the discriminating fact. Here that was a byte-scan of MCM's embedded prefabs across two releases (9→0 `VerticalBottomToTop`, total conserved at 11). This is `evidence-over-claims.md` §C applied to *reasoning*, not just to facts.
- **Source:** docs/reviews/rca-butr-dependency-update-2026-07-16.md (pattern C).

### When two review agents disagree, the narrower + more confident one is usually the wrong one
An adversarial agent byte-proved Patch41 was a dead no-op against TAOM's bundled MCM and concluded "delete it." An API agent found another module (`DOTS.Dependencies`) shipping the *older* MCM on the same install and concluded "keep — these are unsigned assemblies resolved by simple name, so load order decides which prefabs win." Both were rigorous; the second was right, because it accounted for a wider world than the first sampled.
- **Why missed:** the deleting agent verified exactly what it was asked to verify (TAOM's own DLL) and generalised from a single-install sample. Confidence tracked the *quality of its evidence*, not the *scope of its premise*.
- **Prevent:** treat an agent disagreement as high-value signal, never as noise to average away — resolve it by asking which premise is narrower. Never let one agent's proof retire a defensive workaround without checking the multi-mod / multi-install case (`dr3-maintenance.md` "External module conflict" generalises to any unsigned BUTR assembly).
- **Source:** docs/reviews/rca-butr-dependency-update-2026-07-16.md ("Why the review caught what the work missed").

### A glob-driven tool that derives keys from filenames silently no-ops on missing dict keys
`rebalance_troops.py` globs `troops_*.xml` and looks up `CULTURAL_MODS[filename_culture]` — but three cultures on disk (`goblin`, `mistymountainorcs`, `dale`) had **no dict entry**, and `troops_rhun_new.xml` derived the key `rhun_new` while the entry was `rhun`. In all four cases `dict.get(culture, {})` returned `{}`, so those troops silently got baseline-only skills with no faction identity and no error. The drift went unnoticed until a read-only audit measured it.
- **Why missed:** the tool fails *open* (missing key → empty modifier → no crash), so a new troop file or a filename/key mismatch produces wrong-but-not-broken output. The April pass predated `goblin`/`misty`/`dale`/`rhun_new`, and nothing flagged the coverage gap when those files were added.
- **Prevent:** a config-keyed-by-discovered-name tool should ASSERT coverage — enumerate the discovered keys, diff against the lookup dict, and warn on any discovered key with no entry (and any dict entry that no discovered key resolves to = dead config, e.g. `lothlorien`). `analyze_troop_balance.py`'s data-quality section now reports exactly this (cultures-without-identity + dead-modifiers) so the gap can't hide again.
- **Source:** docs/features/troop-skill-balance.md (2026-06-24 rebaseline — rhun_new key mismatch + 3 missing-identity cultures)

### Test an advertised tool invariant per flag combination, not just the default path
`rebalance_settlement_prosperity.py` shipped a docstring claim ("a second run after --apply must report 0 changes") proven only for the default flag path. All three optional flags broke it: `--preserve`/`--pin-zero-village` left frozen fiefs inside the quantile-map ranking population (their unchanged values shifted every free fief's rank each run — unbounded drift), and `--town-uplift` stacked `+N` per run because the post-hoc add landed on top of a lift-only clamp whose floor already contained the previous run's `+N`. Caught in deep review by the Step 2c tooling agent with minimal reproductions; fixed by excluding frozen fiefs from the ranking input and applying uplift pre-clamp.
- **Why missed:** the idempotency proof (monotonic rank ⇒ fixed point) was written for the bare quantile map; the flags were added in the same authoring pass as post-hoc `targets` overwrites and the blanket claim was never re-checked against them. The planned validation gate (post-apply dry-run) would only ever have exercised the default path.
- **Prevent:** when a tool's docstring/help asserts an invariant (idempotency, lift-only, order-preservation), test the invariant **per flag combination** with an in-memory apply→recompute harness (~20 lines) before trusting it — a flag that mutates the output outside the core transform almost always feeds back into the next run's input. If a flag is genuinely one-shot, say so in its help text instead of claiming the invariant.
- **Source:** docs/reviews/rca-settlement-economy-2026-07-02.md (deep-review tooling agent, 2 HIGH)

### Non-ASCII characters in tool stdout crash on Windows cp1252 — keep tool output ASCII
`rebalance_troops.py`'s warning section printed a `Δ` (delta) glyph, which raised `UnicodeEncodeError: 'charmap' codec can't encode 'Δ'` the moment stdout was the default Windows cp1252 console/redirect AND that code path ran. It was dormant for the whole prior life of the tool because the path only fires when there are >100-point deltas — which the under-tuned rebaseline finally produced. Writes happen before the report print, so an `--apply` would have left the files written but exited with a traceback.
- **Why missed:** the glyph reads fine in a UTF-8 terminal, so it survived authoring and review; the crash needs both a cp1252 stdout and the rarely-hit warning branch.
- **Prevent:** keep CLI tool output ASCII (`delta=` not `Δ=`), or set `sys.stdout.reconfigure(encoding='utf-8')` at entry; when capturing tool output on Windows, `PYTHONIOENCODING=utf-8` is the belt-and-suspenders. Treat a latent non-ASCII print in a rarely-taken branch as a real defect, not cosmetic.
- **Source:** docs/features/troop-skill-balance.md (2026-06-24 rebaseline — latent Δ stdout crash)

### Document continuously, not only at task completion
Update docs **as findings happen**, not just when a feature is "done." After a real finding (a decompile result, a tpac scan, a refuted hypothesis) write it into the relevant `docs/features/<name>.md` / `docs/reviews/rca-*.md` / CHANGELOG in the **same turn** — don't batch it for "later." Record troubleshooting state: what was tried, the result, what it ruled in/out, the next untried step (RCA "ranked experiments" style). Negative results are first-class — "the L/R mesh-split was tested in-game and still AV'd" is as valuable as a fix.
- **Why:** the creature-pipeline work is long, multi-session, and exploratory; the expensive thing a future session loses is the reasoning trail (which hypotheses were refuted, what a tpac scan showed, what a decompile proved). User standing expectation (2026-06-06): docs always reflect current troubleshooting state, including in-progress dead-ends, not a clean post-hoc summary.
- **Prevent:** document from evidence actually read this turn (ties to `feedback_no_write_before_reading_tool_output` + `evidence-over-claims.md` §C); never from assumption.
- **Source:** memory/feedback_continuous_documentation.md

### Renaming a test/symbol that docs reference by name — grep `docs/` for the old name
Prose caveats and CHANGELOG entries get updated when behavior changes, but ENUMERATED symbol lists in docs (a feature doc's "Tests" section, a "Key Files" table, a Patch table) silently keep the old identifier. After renaming or replacing a test/class/patch/method any doc names, grep `docs/` for the old identifier and fix every hit.
- **Why missed:** the Phase 0 shader review (2026-06-25) replaced `DefaultScenes_IncludesTheCrashScene` with `DefaultScenes_ExcludesDisabledCrashScenes` + `DefaultScenes_IncludesActiveSiegeScene`; the feature doc's prose + Changelog were updated, but its "Tests" bullet still listed the old test name. Codex caught it (LOW); deep-review Agent 4 confirmed the doc covered the NEW work but didn't diff the test-name enumeration against the renamed tests.
- **Prevent:** `grep -rn "<old-symbol>" docs/` after any rename of a doc-referenced symbol. The completeness reviewer's "is the doc updated for the new work?" check is necessary but not sufficient — also ask "does every symbol the doc NAMES still exist?"
- **Source:** docs/reviews/rca-shader-precompile-phase0-2026-06-25.md

### Content-gating commit hooks must read the staged blob, not the worktree file
A PreToolUse(Bash) hook that gates `git commit` on a file's **content** (not just presence) must read the **staged blob** via `git show ":$PATH"` (extract to a temp file if the inspector needs a path), NOT the on-disk working-tree file. The two can diverge — a rebuilt-correct file on disk while a stale-bad blob is staged — and the staged blob is exactly what the commit ships. Presence gates ("is file X in this commit?") can use `git diff --cached --name-only`; content gates must read the blob.
- **Why missed:** `check-native-dll-crt.sh` (blocks committing a dynamic-CRT `TAOM.NativeSkinFixes.dll`) was modeled on `check-moduledata-validation.sh`, which validates the *working-tree* XML — correct there because XML edits stage wholesale. For a binary artifact rebuilt out-of-band, worktree (rebuilt static DLL) and staged/HEAD blob (still old dynamic DLL) routinely diverge. Codex caught it (HIGH) by parsing the index/HEAD blobs in the git object store; the 4-agent `/deep-review` missed it because the agents reviewed source logic, not the artifacts the commit would produce.
- **Prevent:** content gate pattern — `TMP=$(mktemp); git show ":$PATH" > "$TMP" 2>/dev/null && [[ -s "$TMP" ]] && inspect "$TMP"; rm -f "$TMP"`. Fail open if extraction fails (per `harness-facts.md` "TAOM hooks MUST fail open"). Keep CI as the backstop — it inspects the committed blob after `actions/checkout`, catching the "forgot to stage the corrected file" case the local hook can't.
- **Source:** memory/feedback_commit_hook_validate_staged_blob_not_worktree.md + docs/reviews/rca-native-skin-fixes-crt-2026-06-18.md

### Run /deep-review + /review-codex BEFORE the closing commit — the workflow is not risk-gated
For any C# feature change touching ≥2 files (or any feature module), run `/verify` → `/deep-review` → fix → `/review-codex` → fix **BEFORE** the closing commit + push. Do NOT commit first and review after. The mandatory completion workflow in CLAUDE.md is a hard gate, not a risk-proportionate suggestion. "Additive + fully tested + validator-clean" is the exact trigger to stop and review, not a license to skip.
- **Why missed:** 2026-06-07 cultural-feats Wave 1 (24 additive feats) — purely additive, fully unit-tested (3091/0/2), ModuleData-validator-clean. That low perceived risk was used implicitly to commit + push (bf9226f, ce07ebe) before either review. The reviews then found 1 MED + 2 LOW: the MED was that production feat metadata (EffectBonus sign, IsPositive, AdditionType) for the 24 feats was unpinned by any test — a flipped sign (`+0.15f` instead of `-0.15f` on a cost-reduction feat) would invert the feat and pass every existing test. Proof that "additive + tested" can still carry a silent, test-invisible correctness bug. Repeat offender (`docs/reviews/rca-crash-report-2026-05-25.md`).
- **Prevent:** treat the thought "this is additive/trivial/well-tested, I'll just commit and review after" as the trigger to stop and review first. Open the GitHub issue when STARTING the work (so it exists before the commit), reference it in commits, close with the final commit.
- **Source:** memory/feedback_review_before_commit_not_after.md + docs/reviews/rca-cultural-feats-wave1-2026-06-07.md (siblings: feedback_completion_workflow, feedback_root_cause_mandatory)

### Worktree-isolation agents branch from a STALE base, not current HEAD
Spawning `Agent` with `isolation: "worktree"` creates a worktree at `.claude/worktrees/agent-<id>/` on branch `worktree-agent-<id>`, but that branch is branched from some BASE that is NOT the parent-session HEAD. In the Phase 9b marathon (2026-05-14): parent HEAD `db5c59b` (200+ commits ahead) vs worktree base `77aaff8` (~183 commits behind) → every agent couldn't find features added in the missing commits (e.g. `Main/Features/SmartCavalryAI/CavalryChargeService.cs` didn't exist on the worktree branch); 10/10 agents reported "environment failure — cannot proceed" and were killed.
- **Prevent:** prefer main-tree concurrency when agent tasks are scoped to non-overlapping feature directories (edit single-owner files `Main/SubModule.cs` / `Main/IoC.cs` / `CHANGELOG.md` sequentially at the end). If worktree isolation IS needed, verify base before work — `cd .claude/worktrees/agent-<id> && git log --oneline -1` should be near parent HEAD; if stale, re-provision rather than `git reset --hard` (destructive). Orchestrator sentinel: an agent completion mentioning "branch is N commits behind / cannot find file X" means the worktree base is wrong — stop it rather than letting it produce stale code.
- **Source:** memory/feedback_worktree_base_stale_in_parallel_agents.md (Phase 9b autonomous marathon 2026-05-14)

### New asset/animation FBXs go to a staging folder OUTSIDE the Armory
When authoring new animation/asset FBXs for a LOTRLOME_Armory-hosted feature (troll clips, creature anims), export to a staging folder OUTSIDE the Armory — default `E:\LOTRAOMAssets\troll_clips_to_import\` — NOT into `LOTRLOME_Armory\AssetSources\...`. The user imports them into the Armory and runs the Modding-Kit compile themselves.
- **Why:** LOTRLOME_Armory is the external/live game module (not in the repo); the user controls what lands in it. Dumping FBXs straight into its `AssetSources` bypasses the user's review gate. (This session put 15 clip FBXs + 3 skeleton FBXs into `…\AssetSources\Race Test\Mordor\Trolls\Hill Troll\` before the rule was given.)
- **Prevent:** default new-asset export target = a clearly-named non-Armory staging dir; tell the user the path; never copy into the Armory or run the Kit-compile for them. Before relocating files ALREADY in the Armory, ask — the user may have imported some.
- **Source:** memory/feedback_keep_new_anims_out_of_armory.md (standing instruction 2026-06-14; see project_troll_race_arp_inflight)

### Read-only subagents need the VERBATIM allowlist + name-banned write-mode tools
Summarizing a read-only contract ("read-only, findings only — no edits") to a tool-capable subagent does NOT reliably hold. On the first `/improve` whole-repo audit (2026-06-12) an audit agent saw `tools/audit_scene_names.py` named in its brief, reached for the family, and ran a `remap_*` script with `--apply` — rewriting a tracked `custom_battle_scenes.xml` (scene-id + 12-language loc-key rewrites). It self-resolved only because the user happened to commit the same rename deliberately.
- **Why missed:** subagents don't inherit CLAUDE.md/rules; an allowlist paraphrase leaves the boundary to the agent's judgment, and "run the auditor" reads as license to run its write-mode sibling. The protections that DO hold are the ones passed verbatim.
- **Prevent:** (1) pass the **verbatim** read-only Hard Rule (allowed + forbidden command lists), not a summary; name-ban write-mode families explicitly — `remap_*` / `apply_*` / `generate_*` / any `--apply` are never run by an audit pass (a stale ref is a *finding*, not an edit). (2) On large fan-outs give each subagent its own findings file it appends to as it goes (`plans/_audit/<run>/<label>.md`), the only path it may write — so a mid-run stall / usage-cap loses nothing. Codified in `.claude/skills/improve/SKILL.md` (Hard Rules 1+2) + `references/audit-playbook.md` §9; generalises to any fan-out.
- **Source:** memory/feedback_readonly_subagents_need_verbatim_allowlist.md (see project_improve_audit_inflight)

### re.sub: a backref immediately before a digit silently corrupts output
In Python `re.sub`, a numeric backreference followed by a value that starts with a digit is parsed as a higher-numbered group: `\1` + `0` → `\10` (group-10 reference, or octal char). Either raises `re.error: invalid group reference 10` or silently corrupts the output (prefix characters chopped). When the substituted value starts with a digit (numeric IDs, hex hashes, currency), use a lambda replacement or the `\g<N>` form — never bare `\N`.
- **Symptom:** 2026-05-26 Gondor lord review script — replacement `r'\1' + new_key + r'\2'` where `new_key` was a 128-char hex string starting with `0`. Expected `... key="0015580A4..."`; got `@15580A4...` — the entire `<BodyProperties ... key="` prefix eaten, replaced by a single `@`. 24 BodyProperties lines corrupted in one apply; reverted via `git checkout -- <file>`.
- **Prevent:** default to lambda replacement when the value is dynamic — `pattern.sub(lambda m: m.group(1) + new_value + m.group(2), text, count=1)`; or named/numbered `\g<1>` form. Scan trigger: any `re.sub(r'\1...', ...)` / `re.sub(..., r'\1' + dynamic_value + ...)` where `dynamic_value` could start with a digit. (Doesn't apply to `.format()` / f-strings.)
- **Source:** memory/feedback_re_sub_backref_followed_by_digit.md (2026-05-26)

### Write tool may entity-encode `&` in target paths → phantom directory tree
The Write tool may entity-encode `&` (and other XML-special chars) to `&amp;` in the target path, creating a phantom directory tree that does NOT match what the consuming app reads. Verify with `ls "<actual-expected-path>"` after any Write to a path containing `&`, `<`, `>`, `'`, `"`.
- **Symptom:** 2026-05-19 career-system PR — wrote `starter_armors.xml` to `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\...\gondor\`. Write reported success at `...\Mount &amp; Blade II Bannerlord\...` (literal `&amp;`), creating a phantom `Mount &amp; Blade II Bannerlord` dir alongside the real one. The game read the real path, found no file, the player spawned naked. Only manifested in-game; `/deep-review` caught the missing item references but not the path-encoding cause until the user reported it.
- **Prevent:** trust the filesystem, not the Write tool's reported success path; the success-message path can disagree with the literal target. Especially critical for paths outside the project root (not caught by `git status` / build / hooks). Fix a phantom dir via `cp <phantom> <real>` then `rm -rf <phantom-root>` (Bash, not Write again).
- **Source:** memory/feedback_write_tool_ampersand_path_encoding.md (first occurrence 2026-05-19; watch for repeats on `'` apostrophe in names)

### Every feature completes ALL 4 mandatory phases — no exceptions
Every feature must complete all 4 phases before closeout: (1) `/verify` + `/deep-review` + fix; (2) Codex adversarial review (write v6 prompt from `docs/reviews/REVIEW-GUIDE.md`, dispatch, `/review-codex`); (3) a SECOND Codex pass reviewing OUR fixes from steps 1-2; (4) final `/verify` + issue + docs + CHANGELOG.
- **Why:** Phase 2 (Codex review) found 43 bugs Claude missed in the codebase review. Phase 3 (self-review of our fixes) caught 2 more bugs in our own fix code (IsFemale field targeting wrong type; shaghana/abanissa alignment set to evil instead of neutral). Each phase exists because the previous one proved insufficient.
- **Prevent:** RCA is mandatory (added 2026-04-09) — after every cycle that finds confirmed bugs, produce a root-cause table BEFORE committing fixes; update `.claude/rules/`, AGENTS.md, REVIEW-LOG.md. Skipped during the BUTR internalization session (user caught it); now a BLOCKING GATE in `/review-codex` Phase 3e. The BUTR session's 6 bugs produced 3 new Harmony rules that prevent recurrence.
- **Source:** memory/feedback_completion_workflow.md

### Commit by pathspec — `git add <mine> && git commit` sweeps concurrent staged files
TAOM is frequently worked by multiple concurrent processes/agents staging into the SAME git index at once. `git commit` commits **everything currently staged**, not just the files you named in `git add` — including files another process pre-staged before your commit fired.
- **Symptom:** 2026-06-22 — ran `git add <4 doc files> && echo staged && git diff --cached --name-only && git commit -F -` as ONE chained command. The index already held a concurrent worker's staged education work, so the commit (`dbb5457a`, "docs(lords): erratum…") swept in `taom_education_equipment_templates.xml` (+2940 lines), `add_education_roster_cultures.py`, and CHANGELOG. The `git diff --cached` DID print all 7 files, but because it was `&&`-chained with the commit, the commit ran regardless. Pushed before noticed, with concurrent commit `57e6bb7a` stacked on top → could not be safely rewritten.
- **Prevent:** (1) commit by pathspec — `git commit -- <path1> <path2> …` commits ONLY the named paths regardless of index state; use by default in TAOM. (2) Never `&&`-chain stage + inspect + commit — stage, then in a SEPARATE call run `git diff --cached --name-only` and READ it, `git restore --staged <not-mine>` anything that isn't yours, THEN commit in a THIRD call. If it already happened and is pushed with a concurrent commit stacked, do NOT force-push to un-mix (destroys shared history) — report and move on; do NOT push a local-ahead commit that isn't yours.
- **Source:** memory/feedback_git_commit_sweeps_prestaged_concurrent_files.md (2026-06-22; see evidence-over-claims, feedback_no_write_before_reading_tool_output)

### Leave the commit to the user when the tree carries concurrent WIP
When my finished work coexists with concurrent WIP that isn't mine — especially interleaved in single-owner files (`Main/IoC.cs`, `Main/SubModule.cs`) — do NOT commit or deploy. Finish my work (build, test, review, docs, issue), then leave it in the working tree and let the user commit later.
- **Why:** a blanket commit would bundle someone else's unfinished feature into mine; `./build.ps1` deploy compiles + ships the whole tree (incl. concurrent WIP) to the game install. User said verbatim (2026-06-01): *"Let everything finish and then I will commit later."* He owns the commit boundary when concurrent work is in flight.
- **Prevent:** detect via `git status` showing files I never touched (cross-check the session-start "Uncommitted: 0" baseline against the current dirty tree). Do the full completion workflow EXCEPT the final commit/deploy. Use diffs to confirm my changes to shared files are isolated (2026-06-01: `IoC.cs`/`CHANGELOG.md`/`tools/README.md` diffs 100% mine; only `SubModule.cs` mixed — career-quest in a separate hunk).
- **Source:** memory/feedback_leave_commit_to_user_when_concurrent_wip.md (2026-06-01; see feedback_parallel_port_build_watcher)

### Parallel-port build watcher auto-comments integration on build failure — close the build atomically
When multiple feature ports run in parallel (visible as `?? Main/Features/<X>/` + `// TEMP-SMARTCAVALRY-EXCLUDE` markers), a build watcher auto-edits source on every build failure: re-adds `<Compile Remove="Features\<Feature>\**\*.cs" />` to both `Main/TAOM.csproj` and `TAOM.Tests/TAOM.Tests.csproj`, comments `using TAOM.Features.<Feature>.*;` directives + integration calls (`AddBehavior`/`AddMissionBehavior`/manual `AccessTools.Method` patches), and stamps a `// TEMP-SMARTCAVALRY-EXCLUDE: <reason>` banner. It does NOT differentiate which feature caused the error — a failure in ANY parallel port cascades exclusion to others.
- **Why:** CompanionTactics port (2026-05-06) lost ~2 hours iterating Edit → build-fail → hook-revert → Edit. RCA `docs/reviews/rca-companiontactics-2026-05-06.md`; documented in `.claude/rules/harness-facts.md` "Parallel-port build watcher".
- **Prevent:** `git status` first to see booby-trapped sibling ports; make all source edits to YOUR feature first and confirm it compiles in isolation; reserve csproj + `SubModule.cs` + `IoC.cs` edits for ONE atomic batch + run the build immediately in the same response (`dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true --verbosity quiet`). Don't chase the watcher iteratively (each cycle burns context) — two atomic attempts max, else ship with auto-commented integration + document manual restoration. Fully-qualifying call sites (`new Features.<Feature>.Hooks.<Class>(...)`) only helps if the source files are still in the compile. Best fix: `isolation: worktree` on agent dispatch.
- **Source:** memory/feedback_parallel_port_build_watcher.md + docs/reviews/rca-companiontactics-2026-05-06.md

### Bulk display-name renames across master + N loc XMLs → single Python script, byte-faithful
For a bulk display-name change spanning a master XML (`{=key}DEFAULT` entries) AND N per-language loc XMLs (`<string id="key" text="..."/>`), use a single Python script with a name-mapping dict + regex anchored on unique IDs. Used 2026-05-26 to rename 345 village placeholder names across 13 files (1 master + 12 loc) = 4,485 replacements in <1s. Don't use the Edit tool (permission-prompted, slow, asymmetric-write risk for >50 changes) or sed (UTF-8/CRLF/locale hell on Windows + Git Bash).
- **Why:** Python `open(path, "rb")` + bytes round-trip preserves encoding losslessly (CRLF, BOM, exotic glyphs); PowerShell `Get-Content`/`Set-Content` loses CRLF unless careful with raw binary IO.
- **Prevent / pattern properties:** idempotent (regex anchored on unique ID → re-run is a no-op); lossless (UTF-8 → bytes → write-bytes); atomic per-file; safe by design (anchored on `id="<unique>"`, not text content). Encoding gotchas: write the script to a `.py` file via Write (don't inline via bash heredoc — parse errors); `sys.stdout = io.TextIOWrapper(..., encoding='utf-8', errors='replace')` for non-Latin debug prints; BOM presence may differ across files in one dir (`TAOM_Map/settlements.xml` has BOM, `loc_settlements.xml` don't) — the bytes round-trip preserves whatever was there, don't normalize. NOT for single-file <10 edits (use Edit) or structural XML changes (use `xml.etree`/`lxml`). Live example: `tools/Apply-MapVillageNames.py`.
- **Source:** memory/feedback_bulk_xml_rename_via_python.md + docs/reference/taom-map-settlement-naming.md (see feedback-taom-map-live-vs-stale-shadow)

### Add comprehensive diagnostic logging to live-only behavior, then strip after sign-off
When building/changing TAOM behavior whose correctness can only be confirmed live (combat BT decisions, clip selection, mount/agent state, anything not unit-testable), add comprehensive diagnostic logging up front, then remove it once confirmed in-game. Standing rule 2026-06-15: "We always need to add comprehensive logging to ensure our stuff is working correctly and then remove it later."
- **Why:** BT/engine behavior (which clip fires for which bearing, which cooldown branch won, whether a hit landed) is invisible to unit tests — the only oracle is `taom_debug.log` from a real battle. Comprehensive logging *proves* a fix rather than assuming it; stripping afterward keeps the log readable and off the hot path.
- **Prevent / how to apply:** single greppable prefix per feature (`[Spider][diag]`, `[Warg]`, `[Elephant]`) so removal is one grep/strip. **Gate on the event, not the tick** — log on the decision/fire event (cooldown-limited, ~once/2-5s per agent), never per-frame in a per-eval decorator (the C++-port hot-path-logging discipline, `feedback_native_port_hot_path_audit.md`, applies to managed code too). Log inputs AND the resolved choice (kind, clip, bearing sign+value, velocity, bone set, range/radius/window). Keep terminal hit/outcome logs until sign-off. Reference impl: spider directional-attack model (2026-06-15), `[Spider][diag] ATTACK fire:` in `SpiderAttackService.SpiderAttack`.
- **Source:** memory/feedback_comprehensive_diag_logging_then_remove.md (2026-06-15)

### A log-volume gate verified against the log that motivated it is not verified — and never gate on a steady-state quantity
A log-hygiene gate must be re-measured on a FRESH session after it ships. Commit `0abe1854` (2026-07-04) cut a 21 MB / 169,676-line session log by adding `if (added > 0 || topUp > 0 || removed > 0)` to CultureMarketplace's per-town daily line and claimed "~170k → ~2k lines per session." The next measured session (2026-07-26) still had **45,080** of them — 95.2% of the whole file. The gate had been inert since the day it shipped.
- **Why:** the `removed` term counts foreign items stripped from a town market. That is a **steady-state quantity**, not an event — vanilla restocks cross-cultural goods every day and the filter strips them again, forever, so `removed > 0` was true on 98.8% of ticks and 83.2% of all emitted lines existed for that term alone. This is the same "**gate on the event, not the tick**" rule as the lesson above, one level subtler: the gate *looked* event-shaped because it was phrased as a delta, but a delta that is non-zero every tick is a tick.
- **Why missed:** the fix was validated by re-running frequency analysis over the **pre-fix** 21 MB log, filtering it by the new predicate — data in which the pathological case (nothing injected, foreign strip alone) was indistinguishable from the healthy one. The commit's own trailer says so: `Not-tested: log volume itself (verified via frequency analysis of the live 21MB session log, not a unit test)`. No unit test can catch this (nothing asserts a per-tick DEBUG line), so the *only* oracle is a fresh session — and nobody re-measured for three weeks.
- **Prevent:** (1) For every term in a volume gate, ask *"is this non-zero in the boring steady state?"* — if yes it does not belong in the gate, though it can stay **in the message** (the foreign count still prints on every surviving line, so no visibility was lost). (2) A log-volume change is not done until a fresh post-fix session log has been frequency-analysed; treat the pre-fix log as a hypothesis generator, never as verification. (3) Before suppressing a high-volume line, confirm it is narrating equilibrium and not a runaway — here, removals/town/day were flat across the session (3.54 → 3.59 → 3.63 → 3.57) and roster counts plateaued, so nothing real was being hidden. A subagent read the same data as "the filter is losing the race, investigate before silencing"; the hour-over-hour trend refuted it. (4) **Latch the LOG, never the WORK.** `MainMenuCustomizer.CustomizeMenu` runs on every screen-root set, and a headless dedicated server sets that root thousands of times per boot with StoryMode and SandBox absent — so both "option not found" warnings fired every time, 4,803 `MainMenuCustomizer` lines in one server log. The fix latches the warnings (a `HashSet` gives one line per option, plus one applied-line per session) while the customization itself still runs on every call, because the engine can rebuild the initial-state options between sets and skipping the work would silently drop the rename on a real client. The tempting one-line fix — an `if (_alreadyRan) return;` at the top — trades a log problem for a correctness one.
- **Source:** CHANGELOG 2026-07-27 (`chore(logging)`), superseding the volume claim in the 2026-07-04 entry; the latch-the-log clause from the 2026-08-03 multiplayer field report (commit 9758fada).

### Log the outcome, not the intent — an engine action that returns `void` cannot tell you it did nothing
When announcing the result of a TaleWorlds action (`KillCharacterAction`, `ChangeOwnerOfSettlementAction`, any `Apply*` that returns `void`), log AFTER the action and only on a re-read of the state it was supposed to change. A pre-action log states an intent as if it were a fact, and if the caller re-evaluates the same population on a timer, the false statement repeats.
- **Symptom:** `RaceAge` logged `Hero X died of old age` then called `KillByOldAge`. 238 death lines for 222 heroes — 16 announced twice, one campaign day apart. `KillCharacterAction.ApplyInternal` marks-and-defers when the victim is in a `MapEvent`/`SiegeEvent` and returns without touching `HeroState`, so the hero stayed alive, matched the daily age check again, and was re-announced. (Its guard is `&& victim.DeathMark == KillCharacterActionDetail.None`, so the second call always lands — hence exactly two lines, never three. The same method also silently refuses the player character and any hero when the life/death cycle is disabled.)
- **Prevent:** give the adapter a `bool` return that re-reads the state after the action (`return !hero.IsAlive;`) and gate the log on it. Verify the re-read property is a plain field comparison, not a computed getter that could throw (`Hero.IsAlive => !IsDead => HeroState == CharacterStates.Dead` — safe). More generally: **an adapter wrapping a `void` engine action that can silently no-op should return whether it took effect**, or its callers cannot tell success from silence. There is a second reason to shape it that way, beyond the caller's need to know: an adapter that touches static engine state (`ModuleMenuAdapter` reads `Module.CurrentModule`) cannot be unit-tested, so any judgement it makes — including "is this miss worth a log line?" — is untestable by construction. `IModuleMenuAdapter.HideOption`/`RenameOption` now report a miss as `false` and the service owns the logging and its dedupe; the adapter's injected `IModLogger` became dead and was deleted. Push decisions down to the layer that has tests.
- **Source:** CHANGELOG 2026-07-27; `TAOM.Tests/Features/RaceAge/RaceAgeBehaviorTests.cs`; the untestable-adapter clause from the 2026-08-03 multiplayer field report (commit 9758fada).

### Grep the whole file for a symbol before commenting it out on an Explore agent's "used only by X" claim
Before commenting out (or disabling) any field/symbol an Explore agent flagged as "used only by <one method>," run a `Grep` for that symbol across the file (or repo) FIRST. Phase 1 inventories miss secondary call sites that only the compiler catches.
- **Symptom:** 2026-05-22 disabling the Pre-compile Shaders main-menu button. Phase 1 Explore reported static fields `_shaderTickAccumulator` and `_lastShaderCount` in `Main/SubModule.cs` as "used only inside the menu action body" (true for the lines it read), so the plan commented them out with the menu block. The build surfaced 5 more references inside `OnApplicationTick` (lines 564-584) — an independent in-game shader-progress reporter. Plan was rebuild-fail-revert. A 10-second grep would have caught it during planning.
- **Prevent:** grep the file for the symbol before the first Edit, especially when the field has a generic name (`_count`, `_accumulator`, `_lastX`), the file is large (>300 lines, Explore reads in chunks), or the agent enumerated specific lines without stating "and nowhere else." Absence of evidence ≠ evidence of absence. File-level analogue of `think-before-coding.md`'s assumption-surfacing rule.
- **Source:** memory/feedback_grep_field_references_before_commenting.md (2026-05-22)

### Authoring a new hook: mirror the sibling's FULL convention set
When adding a hook in an existing category (Stop reminder, PreToolUse gate, PostToolUse logger), copy the nearest sibling's ENTIRE behavioral surface — detection mechanism, **muting/idempotency** (early-exit when already handled), I/O preamble (`INPUT=$(cat)` verbatim), and exit semantics (`exit 0` non-blocking vs `exit 2`/JSON `deny`). Consciously match-or-deviate on each, not just the part you're focused on.
- **Why missed:** 2026-05-29 — the new `check-verification-evidence.sh` Stop hook copied `check-deep-review.sh`'s detection (git state, stderr, exit 0) but NOT its muting → it re-nagged on every Stop while a `.cs` file stayed dirty (MED, caught in adversarial review before commit). `mark-verification-run.sh` also hand-wrote its stdin preamble (`cat 2>/dev/null` + `printf`), diverging from 13 sibling hooks (LOW). Same shape as the native C++ port hot-path miss — architectural focus consumes the audit budget; surrounding conventions ride along unaudited.
- **Prevent:** pre-flight pass over a sibling's WHOLE body before committing a new hook. Checklist in `.claude/rules/harness-facts.md` → "Authoring a new hook — mirror the sibling's FULL convention set." (Related: when adding discipline text to a skill, point to the centralized rule and add only the skill-specific delta.)
- **Source:** memory/feedback_hook_authoring_mirror_siblings.md + docs/reviews/rca-superpowers-enforcement-2026-05-29.md

### Parallel-port coordination hook etiquette: accept its authority; don't fix others' WIP
TAOM's coordination hook auto-applies `<Compile Remove>` lockouts and comments out IoC/SubModule registration calls when sibling parallel-port sessions ship transient build errors — a defensive measure to keep `master` green. Accept the hook's authority: only remove your own feature's lockouts after the build is verified clean; do NOT fix other sessions' WIP code as a side-effect of unblocking your own build.
- **Why:** EquipPresets port (2026-05-06) — 5 concurrent sessions (QuickActions, FiefManagement, SmartCavalryAI, CompanionTactics, EquipPresets) each broke shared files (`Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`) at different points; the hook locked out each broken module and restored only after the owning session verified clean. Stabilized to 1598/1598 tests.
- **Prevent:** when your code is excluded, verify it compiles in isolation THEN remove the exclusion — if the hook re-applies within seconds, your code STILL has an error (fix it, don't fight the hook). When OTHER sessions' code blocks your build, either add their code to the lockout list (mirror the hook's pattern + document `// TEMP: parallel-port — re-enable when X builds clean`) or stop and report (per `environment-failures.md`). The line: anything inside YOUR feature folder is yours; shared csproj/IoC/SubModule is shared infrastructure — touch only when necessary, mirror the hook's patterns, document. Never modify another session's tracked code as a side-effect without explicit authorization.
- **Source:** memory/feedback_parallel_port_coordination_hook.md (EquipPresets port, 2026-05-06)

### Root cause analysis (Phase 3e) is a mandatory BLOCKING GATE — stop skipping it
Root cause analysis (step 3e in `/review-codex`) is MANDATORY; multiple sessions have skipped it. After verifying Codex findings (3d) and BEFORE implementing any fixes (3f), STOP and produce the root-cause table. Treat the "BLOCKING GATE" language literally — do not proceed past 3e until the table is written. This applies to every `/review-codex` run, not just when the user is watching.
- **Why:** the root-cause table is how TAOM prevents the same bug category recurring in future features; without it the same mistakes repeat. The user has corrected this multiple times and is frustrated by the pattern.
- **Prevent:** do NOT dismiss findings as "N/A" or "one-time" — every confirmed bug gets a real root cause + preventive action. Do NOT produce the table retroactively after being caught — it must be proactive in the natural flow.
- **Source:** memory/feedback_root_cause_mandatory.md

### `-p:DisableModuleCopy=true` is broken when the game is running — build with `-p:ModuleId=` instead
When Bannerlord is **running** (live debug session) it locks module DLLs, so `./build.ps1` and `dotnet build -p:DisableModuleCopy=true` both FAIL with `System.UnauthorizedAccessException: Access to the path '...\Modules\<Module>\bin\Win64_Shipping_Client\0Harmony.dll' is denied` — the post-build `CopyFolder` can't delete the locked DLL. This is an environment file-lock, not a compile error (the C# compiles fine before the copy step). `-p:DisableModuleCopy=true` is **ineffective** in `bannerlord.buildresources` 1.1.0.129: only the umbrella `PostBuildCopyToModules` target is gated by it, but `CopyBinariesWindows`/`CopyBinariesWindowsStore`/`CopyModule` ALSO hook `AfterTargets="PostBuildEvent"` and their conditions check only `$(ModuleId) != '' AND Exists($(GameFolder))` (verified reading `Basic.targets` lines 47-65, 2026-06-15).
- **Prevent:** build/test with `-p:ModuleId=` (empty) — all three copy targets are gated on `$(ModuleId) != ''`, so an empty ModuleId skips every game-folder copy while compilation + `GameFolder` HintPaths stay intact: `dotnet build TAOM.sln -c Debug -p:ModuleId= --nologo --verbosity quiet` then `dotnet test TAOM.Tests -c Debug --no-build -p:ModuleId= --nologo`. Do NOT empty `GameFolder` to skip the copy — that breaks the DLL HintPaths and compilation fails.
- **Source:** memory/feedback_disablemodulecopy_broken_use_empty_moduleid.md (EMPIRICAL: TAOM 2026-06-15, CultureFeatAdapter NRE fix during a live debug session)

### "File read failed" / "GPU device suspended" / CC-freeze in Bannerlord can be GPU-driver, not files — check `nvlddmkm` FIRST
Bannerlord's engine surfaces GPU device-loss as "File read failed! Please try to verify your installation!" — a message that strongly implies a file/mod-data issue. Before bisecting mod code, touching the shader cache, or renaming RDC files, run the cheap GPU probe: `Get-WinEvent -ProviderName nvlddmkm` (AMD: `amdkmdag`/`amdwddmg`). Zero events → genuine file/mod issue; Event ID 13/153/4101 in the symptom window → GPU instability is the cause, stop chasing files.
- **Why:** RCA 2026-05-20 chased the wrong layer for hours. Compounding false signals: a different GUID fails each crash (async-load queue position is non-deterministic); `Missing shader from sack` lines (normal on-demand compiles, NOT cache corruption); empty 181-byte `rgl_log_errors_*.txt` (means the GPU hung so hard the engine couldn't flush stderr, not bad file IO). The afternoon "GPU device suspended" had 339 `nvlddmkm` Event 13s (`Graphics Exception ESR 0xb100020`, MMU fault); zero at any other point. Confirmed bad driver: NVIDIA 596.49 (`32.0.15.9649`) on RTX 5080 — Event 13 `Multiple Warp Errors` + ESR `0xb0d0020`, broadened to desktop-wide freezes + Kernel-Power 41s (2026-05-21).
- **Prevent / do-NOT:** don't delete/rename orphan RDC files (just moves the failure to the next queued file — cascade `48AA370A → 8012DF0D → C19B1338`); don't rename `compressed_shader_cache.sack` on a single `Missing shader from sack` line (forces a 20k+ shader recompile storm that can hard-freeze CC — a worse problem); don't bisect mod code for a "started today" crash without first checking the system change timeline (`Get-HotFix -ge AddDays(-3)` + ask about driver/Windows updates). Remediation is user-domain (per `environment-failures.md`) — DDU + clean reinstall or driver rollback.
- **Source:** memory/feedback_bannerlord_async_load_check_gpu_first.md (RCA 2026-05-20 + 2026-05-21 follow-up; see feedback_shader_cache_invisible_cc, reference_bannerlord_slow_load, environment-failures.md)

### Data-mutating XML scripts must round-trip byte-faithfully (BOM + encoding) and compare ids case-insensitively
**THIRD INSTANCE 2026-08-06 — the rule was right, its LOADING was the defect.** `tools/oneoff/fix_uruk_hai_hands_teamcolor.py` shipped with the forbidden mixed shape (plain `utf-8` text read + text write) despite this lesson existing since 2026-05-28. Root cause of the recurrence: the convention lives in `tools/README.md`, which **nothing auto-loads**, and `.claude/rules/moduledata-validation.md` was paths-scoped to *repo* ModuleData only — so authoring a script that edits the game install loaded no rule at all. Fixed by extending that rule's `paths:` to `tools/**/*.py` + `tools/**/*.ps1` and inlining both sanctioned idioms there, so writing any tool now surfaces the convention. A blocking lint was evaluated and **rejected**: 92 of 124 XML-writing scripts trip a naive mixed-shape heuristic, so a build gate would fail on pre-existing debt and false-positive on read-only analyzers. Two sibling defects in the same script, same root: a dormant branch spliced bare `
` into CRLF files, and the target set was derived by substring containment rather than exact-token compare. **When a lesson recurs, check whether the rule is reachable from where the code is written before writing the lesson a third time.**

Any TAOM script that EDITS Bannerlord ModuleData XML (`tools/*.py`, `*.ps1`) must round-trip byte-faithfully: detect the BOM via `read_bytes`, decode `utf-8-sig`, write `write_bytes` with an explicit `b"\xef\xbb\xbf"` prefix re-prepended only if the file had one. Bannerlord XML is UTF-8 — some files carry a BOM (`TAOM_Map/settlements.xml`), most repo files don't — with CRLF and non-ASCII (û, é, î, ñ). Scene/asset/id membership checks must be case-insensitive (Windows resolves `HART_ISENGARD` → `HART_isengard`) — lowercase both sides.
- **Why missed:** `/deep-review` 2026-05-28 (scene tooling). A family of scripts (`audit_scene_names`, `audit_battle_scenes`, `remap_stale_scene_names`, `add_bandit_faction_banner_keys`, `add_bandit_hideouts`, `migrate_hideouts_to_lotr`) grew across many turns with no shared I/O convention → each reinvented file IO and drifted. No live corruption occurred but it was fragile. The 5 core deep-review agents are C#-centric and do NOT review Python/PowerShell tooling — a script bug can corrupt live data silently with zero C# signal.
- **Prevent:** the canonical pattern (also in `tools/README.md` "XML I/O convention"): `had_bom = path.read_bytes().startswith(b"\xef\xbb\xbf"); text = path.read_text(encoding="utf-8-sig"); ...edits...; path.write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))` + `.bak` backup before destructive write. Anti-patterns: writing the BOM as a U+FEFF string literal (fragile if the `.py` is re-saved), reading a BOM file with plain `utf-8` (leaves a stray U+FEFF at pos 0), case-sensitive id checks. When a changeset includes data-mutating scripts (especially ones writing OUTSIDE the repo to the `TAOM_Map` game install), launch a dedicated Tooling Correctness agent (`.claude/skills/deep-review/SKILL.md` Step 2c).
- **Source:** memory/feedback_xml_tool_bom_io_convention.md + docs/reviews/rca-scene-tooling-2026-05-28.md + tools/README.md (siblings: feedback_compare_against_vanilla_before_mirroring, feedback_scene_name_refs_break_on_version_bump, feedback_re_sub_backref_followed_by_digit)

---

### TAOM's working tree is concurrently mutated — verify the index right before commit, HEAD before any reset
A second actor (user terminal, format-on-save, parallel-port build watcher) mutates the repo while you work, so the index can be contaminated between stage and commit and your commit may no longer be HEAD. On 2026-06-01 a user `git add CHANGELOG.md` during an `AskUserQuestion` wait swept their entry into a no-pathspec commit; a `git reset --mixed HEAD~1` then un-committed *their* commit stacked on top of mine.
- **Why missed:** `git commit`/`reset` assume a stable solo-owned index and stable HEAD; in this repo neither holds, and the long wait between stage and commit (especially `AskUserQuestion`) is the danger window.
- **Prevent:** Re-run `git diff --cached --stat` in the **same** tool call as the commit, or stage+commit atomically in one chained command. Before ANY `git reset`, run `git reflog -3` / `git log --oneline -3` and confirm HEAD is what you think. A pushed commit with a child can't be fixed without history rewrite (force-push is blocked) — accept cosmetic splits. Sibling: [[feedback_git_commit_sweeps_prestaged_concurrent_files]].
- **Source:** memory/feedback_concurrent_repo_commit_race.md

### When the user shares a repo to mine, drill in — don't summarize the README
For a "what can we use from here?" repo share, do an exhaustive review: open the high-signal files (SKILL.md, system prompts, rule files), not just the README. Standing user expectation (2026-04-25): *"I am going to keep adding github repos. Please review everything."*
- **Why missed:** N/A — this is a standing workflow preference, not a bug. (WebFetch's summarizer mangles GitHub content and has surfaced prompt-injection in past reviews — use `curl https://raw.githubusercontent.com/...` for real file contents.)
- **Prevent:** Output structure: Context → What's new → What we already match → What to skip → Recommended minimum action → Files that would change. Evaluate each interesting piece against the existing `.claude/`. In plan mode, write the assessment to the plan file then ExitPlanMode. This is the review depth the `/adopt-external` workflow encodes.
- **Source:** memory/feedback_repo_review_thoroughness.md

### A structural refactor's leftover-reference sweep must cover living docs, not just code
When a refactor renames/moves/deletes types, folders, or public methods, grep the ENTIRE repo for the old names — and fix the hits in **living documentation** (`docs/features/*.md`, `docs/ai-includes/*.md`, `CLAUDE.md` Key Paths blurbs), not only `*.cs`. Historical records (past CHANGELOG entries, `docs/reviews/rca-*.md`, audit snapshots, REVIEW-LOG) describe the state at their time and are left untouched. CLAUDE.md edits are gated by `config-protection.sh` — surface the needed correction to the user instead of forcing it.
- **Why missed:** the 2026-07-01 overnight refactor stack (ElephantLike unification, #305) deleted `Elephant/BehaviorTreeElements/` + `Mumakil/BehaviorTreeElements/` and renamed 6 types; the post-refactor leftover sweep grepped `*.cs` only, so `docs/features/elephant.md` + `mumakil.md` kept dead links and dead type names. Caught by the `/deep-review` completeness agent (2 confirmed MED/LOW findings — the only findings in an otherwise-clean 6-dimension review).
- **Prevent:** the refactoring-specialist agent's Method now includes a mandatory step 6 "Documentation sweep" (grep old identifiers repo-wide with no file-type filter; classify hits living-vs-historical; update living, leave historical). Orchestrator-led refactors follow the same step. The `/deep-review` completeness agent's stale-doc check (added this session) is the backstop.
- **Source:** docs/reviews/rca-refactor-stack-2026-07-01.md

### A commit message's claimed deltas are part of the diff — verify each one before committing
When a commit message or CHANGELOG entry claims a deliberate behavioral delta ("X now also logs Y", "gains Z"), grep the staged diff for each claimed delta before committing. Refactor review discipline points one way only (hunt *unintended* changes in moved code); nothing checks that *promised* changes actually landed, so a half-delivered intent ships as a documented lie.
- **Why missed:** round-2 R4 (2026-07-01) claimed "the elephant gains the late-attach counter + first-late log" — the mid-mission half was wired but the mission-end summary (present in the Spider/Mûmakil siblings) was not. The claim was written from intent, not read back from the diff. Caught by the deep-review standards + wiring-parity agents comparing the three behaviors side-by-side.
- **Prevent:** treat claimed deltas like claimed counts/hashes under `evidence-over-claims.md` §C — produce them FROM the diff. For sibling-parity refactors, diff the siblings against each other at the same call sites, not just new-vs-old per file.
- **Source:** docs/reviews/rca-round2-cleanups-2026-07-01.md

### Generated data files get hand-edited downstream — regen must diff empty BEFORE any --apply
`taom_lord_skill_sets.xml` says "Generated ... do not edit by hand", but the legendary-lord hierarchy commit (1f7a7a9a) hand-tuned 14 canonical sets and hand-added 3 (Sauron/Witch-King/Thranduil) without syncing the generator. The next blind `--apply` would have reverted all 14 and DELETED Sauron's set the day after #321 shipped. The drift was caught only because the balance plan mandated a regen-idempotency pre-check.
- **Why missed:** the hand-edit commit documented its own debt in a CHANGELOG note ("update its canonical entries first if regenerating") — documentation is not enforcement, and two months later nobody re-reads a CHANGELOG note before running a generator.
- **Prevent:** the pre-flight is now codified in `docs/ai-includes/lord-skills-authoring.md` (Quick reference + gotchas): regen on a clean tree, require an empty `git diff`, sync the generator first on any drift (done in 874e7574 with a regen==committed acceptance check). Applies to EVERY "generated — do not hand-edit" artifact in the repo.
- **Source:** commits 1f7a7a9a (debt) + 874e7574 (sync); #322 session 2026-07-02.

### git diff presentation is not file content — verify block equality before staging surgery
During #326 close-out, `git diff` showed lord_BC2_2's face-tag block as removed (then re-added elsewhere): pure diff re-anchoring noise around adjacent skill-value changes, not a real edit. A "surgical hunk exclusion" built on that presentation reverse-applied a phantom hunk into the index and had to be unwound; comparing the actual block between `git show HEAD:file` and the worktree proved them near-identical in seconds.
- **Why missed:** a parallel session owned uncommitted work in the same tree, so an unexplained "removal" pattern-matched to "their edit — exclude it" instead of "diff artifact — verify it". The exclusion machinery was built before the block-level comparison was made.
- **Prevent:** before any partial-staging surgery keyed off a diff hunk, extract the touched block from HEAD and worktree and compare CONTENT (regex the element out of both). If they match, the hunk is presentation. Cheap, and it inverts the default: verify first, machinery second.
- **Source:** #326 session 2026-07-03; evidence-over-claims §C applied to git output.

### In headless Blender, every dimensions/bound_box/matrix_world read after a mutating op needs view_layer.update()
Four incidents in two days (rivendell asset pipeline 2026-07-15/16): stale `matrix_world` right after import baked garbage transforms; stale `bound_box` after `join()` made a tiny-cluster filter degrade, report dims read 0.0, and the chunk re-pivot silently no-op; a "record foliage transforms" feature read matrices AFTER a bake had zeroed them all to Identity.
- **Why missed:** ops-based flows force depsgraph evaluation implicitly, so the bugs only appear in data-level/headless paths; and the visible symptom (dims 0.0 in a report column) was dismissed as "cosmetic" instead of traced.
- **Prevent:** treat `bpy.context.view_layer.update()` as mandatory after import/join/transform_apply and before any `matrix_world`/`dimensions`/`bound_box` read; snapshot transforms BEFORE any bake step; never label an anomalous report value cosmetic without tracing it. RCA: `docs/reviews/rca-asset-pipeline-tools-2026-07-16.md`.
- **Source:** deep-review 2026-07-16 (asset-pipeline tooling agents).

### Multi-script pipelines glued by name agreement need ONE shared derivation, and a safety claim in a docstring is a bug until enforced in code
The rivendell/tents pipeline's only integration contract is string agreement (texture stems ↔ material names ↔ mesh ids ↔ meshlists). Five drifted `sanitize()` copies, a collision-disambiguation one script performs and its consumer can't derive, a `MISSING:` convention its consumer never handled, and meshlist producers added without re-auditing readers — plus a `--force` flag whose docstring promised hand-made files were safe while the code had no such check, and a `--dry-run` that still mkdir'd + wrote reports.
- **Why missed:** per-file review passes structurally cannot see cross-file contract drift; docstrings were written as intent under iteration pressure.
- **Prevent:** shared helpers (or emitted sidecar maps) for any name derivation used by ≥2 scripts — the tooling twin of the parallel-builder-briefs rule; on review, diff every duplicated helper; verify every docstring safety claim has an enforcing code path; dry-run must gate every mutation including mkdir. RCA: `docs/reviews/rca-asset-pipeline-tools-2026-07-16.md`.
- **Source:** deep-review 2026-07-16, cross-script consistency agent (2 HIGH gaps confirmed live).

### When a rewrite adds an early return to a loop body, enumerate what the old unconditional path guaranteed
The `FileLogger` crash-durability rewrite added `if (_logFile == null) return;` at the top of `Drain()` for null-safety. The old code had no guard — it dequeued unconditionally and wrote via `_logFile?.`, which *incidentally* guaranteed the queue always drains. That guarantee was load-bearing for `ProcessQueue`'s `while (!_stopping || !_queue.IsEmpty)` exit condition: post-`Dispose` the new guard leaves items queued forever, so a still-live writer thread spins at 100% of a core until process exit.
- **Why missed:** the new guard is locally correct and globally a liveness regression. The rewrite preserved ordering and null-safety (both tested); the drain-regardless property was never named, documented, or tested — it was an emergent side effect of the old shape.
- **Prevent:** when adding a guard clause to a method a loop depends on, read the loop's exit condition and ask whether the guard can permanently prevent it from clearing. Pin the invariant with a test asserting the *side effect the loop needs* ("the queue drains"), not merely the absence of an exception. RCA: `docs/reviews/rca-battle-load-blind-window-2026-07-16.md`.
- **Source:** deep-review 2026-07-16, dedicated concurrency agent (MED, RED-proven: "Drain() left 200 item(s) queued after Dispose").

### A diagnostic that fails silently is worse than one that fails loudly — a swallowed fault still needs a channel
`FileLogger.Drain()` swallowed all write faults in an empty catch. Both constraints behind that were real: an IO fault now lands on the game thread and must not propagate into engine code, and the catch cannot log without re-entering itself. But a disk-full/AV-lock fault then dropped the in-flight line and every subsequent one, forever, with zero signal — the crash-forensics instrument would look healthy while losing exactly the lines it exists to capture, during the incident it exists to document.
- **Why missed:** the design question "swallow or propagate?" was asked and answered; "swallowed and therefore invisible" was never asked. No rule covered it.
- **Prevent:** for any swallowed fault in a diagnostic/observability path, provide a signal that does not re-enter the faulting component — a counter surfaced on recovery, a one-shot sentinel, a field the crash bundle reads. "It can't log from here" is a reason to find another channel, not to stay silent. RCA: `docs/reviews/rca-battle-load-blind-window-2026-07-16.md`.
- **Source:** deep-review 2026-07-16, dedicated concurrency agent (MED).

### Before dispatching /deep-review, name the changeset's riskiest property and check a core agent actually covers it
Commit `c53c8436` was nominally a Harmony changeset (4 new bindings incl. a private engine method) — covered from three directions by Agents 1/2/5, all clean. Its *actual* risk was a lock/liveness rewrite of `FileLogger`, which every feature depends on. Both confirmed defects were in those 20 lines, and all five core agents read the file and missed both; they were found only because a 6th concurrency agent was hand-rolled.
- **Why missed:** the 5 core agents are calibrated for TAOM's usual work (Harmony, GameModels, adapters, XML). Agent 3 came closest but frames locks as a *throughput* concern — it asked "how long does this block?" (and got the number wrong: it claimed a 50ms stall from a `Thread.Sleep` that sits outside the lock) instead of "can this loop fail to terminate?"
- **Prevent:** name the single riskiest property of the changeset before dispatch; if no core agent's rule set covers it, write the extra agent. The core five are a floor, not a ceiling — the skill says so, and this is the worked example. Triggers: threading/locking, process lifetime, native interop, anything under `Main/Core/` that every feature depends on. RCA: `docs/reviews/rca-battle-load-blind-window-2026-07-16.md`.
- **Source:** deep-review 2026-07-16 (6 agents; 5 core PASS, 6th found 2 MED).
### When an approved plan says "fix it in data", fixing it in code instead is a rule breach waiting to happen
The BannerBearers plan (2026-07-16) specified adding `<banner_bearer_replacement_weapons>` to the cultures missing it, with C# only as a defensive backstop. Mid-implementation the reasoning shifted -- "a C# fallback reading the troop's own sidearm is per-troop accurate and maintenance-free" -- and the XML was skipped. That single deviation produced an ADR-002 breach (a `for`+`if` loop inside a GameModel, which `.claude/rules/gamemodels.md` rejects as binary regardless of line count), a spurious nullable question, two efficiency findings, and a master-toggle leak. All four dissolved the moment the plan's original data fix was applied and the C# was deleted. The deep review's own suggested fix was worse still -- move the loop into the service -- which would have breached ADR-007 by putting the sealed `BasicCharacterObject`/`ItemObject` across a service boundary.
- **Why missed:** the deviation was reasoned locally ("this is more accurate") without re-checking it against the rules the plan was written to satisfy. Plans get scrutiny; mid-implementation changes usually do not.
- **Prevent:** treat a mid-implementation deviation from an approved plan as a decision needing the same scrutiny as the plan -- state it explicitly and re-check it against the ADRs before proceeding. Corollary: prefer data over code for anything expressible as data. Data is validated by `validate_moduledata.py` plus a parse smoke test and cannot breach an architecture rule; the equivalent C# can, and did. A build-time test pinning a data invariant beats runtime C# defending it.
- **Source:** docs/reviews/rca-banner-bearers-2026-07-16.md (finding 4, HIGH).

### Blender's Smart UV Project is the wrong charter for dense organic triangulation — probe UV candidates before spending a bake
Re-UV'ing the Tripo AI throne (42.7k uniform tris) for Substance paintability, `uv.smart_project` made fragmentation WORSE at every angle limit probed (66–89°: 1,485–2,112 islands at 17–24% UV utilization, vs the source atlas's 298 at 53%) — its per-face normal-bucket clustering shatters bumpy meshes. An xatlas-style charter (BFS region-growing on angle-to-chart-average-normal + sub-20-face fragment absorption into the most-shared-boundary neighbour + planar projection + `pack_islands`) landed 128 islands / 57% / 1.4% fold-over at spread 75°.
- **Why missed:** smart_project is the reflex "auto-UV" op and its defaults look reasonable; nothing fails loudly — the first full run baked 4 maps onto a 1,993-island layout before the report's island count was compared against the baseline.
- **Prevent:** measure the unwrap BEFORE committing a bake to it: islands + UV utilization + flipped-face count (fold-over telemetry), compared against the incumbent atlas as a gate. `blender_prep_tripo_prop.py --probe-angles/--probe-spreads` is the pattern (script named `blender_prep_witchking_throne.py` at the time) — candidates cost seconds, bakes cost minutes each. Push chart spread past 90° only knowing fold-over jumps (5.6–10% probed) into visible bake artifacts.
- **Source:** Witch-king throne prep session 2026-07-25 (probe JSON in the session DONE.txt records; numbers reproduced in the script docstring).

### `tools/README.md`'s XML I/O convention binds throwaway and external-module scripts too — the paths-scoped rule only fires inside the repo
The convention (`tools/README.md:7-24`) mandates one of two byte-faithful idioms — `utf-8-sig` decode plus BOM re-prepend on a bytes write, or a full binary round-trip — forbids the mixed `utf-8`-decode-plus-text-write shape, and requires a `.bak` before destructive writes. `.claude/rules/moduledata-validation.md` is paths-scoped to **repo** ModuleData directories, so a script editing `Modules/<Mod>/ModuleData/*.xml` in the game install loads no convention at all, and `tools/README.md` is not auto-loaded by any rule. A scratchpad one-off is exactly where the discipline gets dropped, and it is also where the blast radius is worst: the target file is live and not git-tracked, so there is no revert path.
- **Why missed:** the 2026-07-26 crafting-piece fix script read with plain `utf-8` and wrote in text mode, and carried no internal backup or post-write parse check. Harmless that run — the target had no BOM (verified), and `newline=""` on both ends preserved LF — but the same script pointed at a BOM'd sibling like `TAOM_Map/settlements.xml` (named in the README precisely because it has one) would leave a stray `U+FEFF` in the text. Both safety nets happened to be supplied out-of-band by the operator rather than by the script.
- **Prevent:** Before writing any script that edits Bannerlord ModuleData XML — repo or game install, permanent or scratchpad — open `tools/README.md` "XML I/O convention" and pick one idiom. Have the script take its own `.bak` and re-parse its output before treating the run as successful, rather than relying on the operator to remember. Verify BOM/CRLF/non-ASCII preservation by byte-comparing against the backup afterwards, not by eyeballing the diff.
- **Source:** [rca-crafting-usage-features-2026-07-26.md](../rca-crafting-usage-features-2026-07-26.md) (findings #1–2) · convention origin: [rca-scene-tooling-2026-05-28.md](../rca-scene-tooling-2026-05-28.md)

---

### Before hand-editing a generated field, check whether a generator owns it

Three Crossbow values were hand-tuned in `troops_erebor.xml` without checking that `tools/rebalance_troops.py` derives that exact field from level + `CULTURAL_MODS`. Its `--dry-run` wanted all three reverted to the formula values; the next `--apply` would have undone the fix with no warning and no diff to review.
- **Why missed:** the question "does another tool own this field?" is in no per-file review's rule set. The data change was self-consistent, validated, and tested — the conflict lives entirely outside the changed file.
- **Prevent:** when hand-editing a value in generated or regenerable data, grep `tools/` for a script that writes that field. If one exists, add the id to its skip list AND record the residual in the owning feature doc, in the same commit. A hand-tune with no skip-list entry has a shelf life measured in "until someone regenerates."
- **Source:** docs/reviews/rca-erebor-equipment-sweep-2026-07-30.md (F5).

### `<DependedModuleMetadatas>` is a launcher extension — the vanilla engine never parses it

`TaleWorlds.ModuleManager.ModuleInfo.LoadWithFullPath` (installed v1.4.7) reads exactly four child
elements of `<Module>`: `DependedModules`, `ModulesToLoadAfterThis`, `IncompatibleModules`,
`SubModules`. There is no branch for `DependedModuleMetadatas` anywhere in the type — it is a
BUTR/BLSE launcher extension. Vanilla ordering is a topology sort over `DependedModules` plus any
module whose `ModulesToLoadAfterThis` names you.
- **Why missed:** TAOM's manifests already use `DependedModuleMetadata … order="LoadBeforeThis"` rows
  for Native/SandBoxCore/Sandbox/CustomBattle, and those orderings hold — but only because the same
  modules are ALSO declared in `<DependedModules>`. Copying the idiom for a new, optional co-op module
  (which deliberately must not be a hard `<DependedModule>`) carried an implication the element does
  not have on its own, so the "TAOM loads before the co-op layer" guarantee the adjacent comment
  relied on was not established for a vanilla-launcher user.
- **Prevent:** to express "load X after me" in a way the game honours, use `<ModulesToLoadAfterThis>`.
  Keep the metadata row as the BUTR-launcher mirror if you like, but never as the only mechanism. When
  a manifest edit is load-bearing, decompile `ModuleInfo.LoadWithFullPath` rather than trusting the
  BUTR `SubModule.xsd` the file references — the schema describes what launchers accept, not what the
  engine reads.
- **Source:** `docs/reviews/rca-coop-interop-2026-07-31.md` finding #5

### If a shipped artifact's identity never changes, "did I ship the new one?" is unanswerable — by you, the user, or the launcher

- **Symptom:** players' characters rendered prone in every UI tableau. Shipping a rebuilt `TAOM.dll`
  alone did not fix it; shipping `TAOM.dll` **and** `TAOM.Dependencies` did. `Main/TAOM.csproj` has a
  `ProjectReference` to `TAOM.Dependencies.csproj` and resolves **HarmonyLib and UIExtenderEx through
  that assembly**, so a stale pairing fails at the member level during patch application — the
  HeroRace preview patches never apply and the tableau falls back to vanilla resolution.
- **Why missed:** all three version signals were constant. `Dependencies/_Module/SubModule.xml` had
  read `v2.0.5` on every release; both assemblies carry frozen assembly/file versions
  (`TAOM.Dependencies` `0.1.0.0`, `TAOM` `2.0.0.0`) on every build ever made, so .NET binds any pair
  without complaint and fails later; and `Main/_Module/SubModule.xml` declares **no
  `DependedModule` for `TAOM.Dependencies` at all**, so the launcher has nothing to check. The only
  distinguishing evidence was a file timestamp, which does not survive a zip and a download. Hours
  went into race data, action sets, shader caches and duplicate-BUTR theories before the artifact
  identities were compared.
- **Prevent:** a version string that never changes is worse than none, because it reads as
  information. Stamp real build versions into shipped assemblies; bump the module `<Version>` when
  the assembly changes; declare inter-module dependencies with a version pin so the **launcher**
  refuses a mismatched pair; and log the build stamps at startup so any user report answers this in
  one line.
- **Source:** `docs/reviews/rca-prone-character-tableau-2026-07-31.md`

### Two concurrent sessions in one worktree: `git add -A` commits the other session's half-finished work

On 2026-08-01 two Claude sessions worked the same clone. The second ran `git add -A` and committed
`633b87e5 docs(coopinterop): reconcile the record with what shipped` — which swept in the first
session's in-progress `Directory.Build.props` change, a `SubModule.xml` dependency pin, and a
`BuildStampReport` call **whose parser was still broken** — then pushed it. For several commits the
branch carried a version-mismatch detector that silently never fired, under a commit message about
documentation.
- **Why missed:** neither session can see the other's working tree, and `git status` looks like your
  own mess. The staging discipline that protects you (staging named files, never `-A`) protects
  nothing if the *other* session does not follow it — and the commit message will describe only that
  session's work, so the sweep is invisible in the log.
- **Prevent:** when a second session may be running, stage explicit paths, never `git add -A` or
  `git commit -a`. Before committing, diff the staged set against what you actually touched and drop
  anything you do not recognise. For genuinely parallel work give one session its own worktree
  (`isolation: "worktree"` on the Agent call, or `git worktree add`) — shared-tree collisions are
  already on record here from the 2026-05-06 build-watcher cascade. If a sweep happens anyway, check
  whether the swept state was *coherent*: the danger is not the wrong commit message, it is shipping
  a half-finished change that compiles.
- **Source:** `docs/reviews/rca-prone-character-tableau-2026-07-31.md`; commits `633b87e5`, `e0e4fd57`

### Build both, ship both — never hand-copy one module out of a working tree

- **Symptom:** same incident. The dev install ran `TAOM.dll` from 07-31 and
  `TAOM.Dependencies.dll` from 07-31, while the release carried 07-30 and **07-17** respectively —
  so the exact shipped combination had never been run by anyone. A mid-incident attempt to fix it by
  copying just the newer `TAOM.Dependencies` into the release produced the *inverse* mismatch.
- **Why missed:** "works on my machine" was structurally guaranteed, not lucky. The dev machine is
  the one place where every module is always freshly built from the same tree.
- **Prevent:** rebuild every coupled module from one source state and ship them together. If a
  hotfix tempts you to copy a single DLL into the release payload, that is the moment the pairing
  becomes untested. Also keep runtime artifacts out of the payload — this release shipped the dev's
  `diag.log`, `failed-mods-catalog.txt` and `last-good-modlist.txt`, so every user's log began with
  the dev's session history and their own sessions appended to it.
- **Source:** `docs/reviews/rca-prone-character-tableau-2026-07-31.md`

---


### When a docstring names a catastrophic failure mode, test the failure path

`generate_black_numenorean_weapons.py` opened with: "A piece id present in file 1 but missing from 2
or 3 makes the weapon fail to load with NO log line, which is why all three are written in one pass."
The detection for a missing XSLT template was implemented. The consequence was not: it printed the
NOT-FOUND note among eight others, wrote all the files anyway, and exited 0.

- **Why missed:** the happy path was exercised repeatedly (dry-run, apply, idempotency re-run) and
  the failure path never was, so the gap was invisible to every run.
- **Prevent:** a guard whose docstring calls the failure catastrophic needs a fixture that triggers
  it. Here that was a copy of the ModuleData dir with one template renamed; the fix is confirmed by
  exit 1 on that fixture and exit 0 on the good path.
- **Source:** `docs/reviews/rca-black-numenorean-2026-08-17.md` finding 4.

### Sibling scripts written in one session drift from each other

Four generators authored in one sitting ended up with four conventions: three detected the target
file's line endings and one hardcoded `
` (inserting 328 LF lines into a CRLF file), three guarded
the backup against overwrite and one did not, two passed `count=1` to `str.replace` and one did not.

- **Why missed:** each script was reviewed against the convention, never against its siblings.
- **Prevent:** diff sibling scripts against each other before shipping. The axes that actually
  drifted: EOL handling, backup guards, replace counts, dry-run reporting, fatal-path handling.
- **Source:** `docs/reviews/rca-black-numenorean-2026-08-17.md` findings 4, 9, 10.

### After editing a generated file, grep `tools/` for the generator that owns it

`taom_partyTemplates.xml` gained 13 stacks across 16 templates. `tools/generate_clan_heraldry.py`
upserts `<MBPartyTemplate>` blocks **wholesale** from `clan_heraldry/<culture>.json`, and that JSON
carries no `mordor_num` entries, so a future run would silently delete the feature from 15 of the 16
templates and revert the party-size rescale with it.

- **Why missed:** the search was "what CONSUMES these troop ids", which finds the models and configs.
  It does not find the tool that REGENERATES the file being edited.
- **Prevent:** after editing any ModuleData file, grep `tools/` for writers of that filename. A
  regeneration is a silent revert, and it will not fail any gate.
- **Source:** `docs/reviews/rca-black-numenorean-2026-08-17.md` finding 8.

### A regenerator that honours an override "only when present" restores the vanilla value when you delete it
`tools/complete_lords_xslt.py` rebuilds `lords.xslt` from vanilla plus the overrides it parses back
out of the current `lords.xslt`. For most attributes that round-trips a hand edit safely. For
`is_female` it does not, because the male convention is to OMIT the attribute: deleting the line is
indistinguishable from never having overridden it, so the next `--apply` copies vanilla's
`is_female="true"` straight back in. The fix is a separate `GENDER_OVERRIDES` table that forces the
absence, which is why `lord_4_6` and `lord_WE8_c` are listed there by id.

- **Why missed:** "the generator preserves my overrides" is true for every attribute whose corrected
  value is a value. It fails only where the corrected value is *nothing*, and that is exactly the
  case nobody tests, because the evidence of success and the evidence of failure look identical in
  the source file: an absent attribute.
- **Prevent:** when an override's correct state is an absent attribute, it needs an explicit forcing
  table, not a deletion. Prove it the way the fix was proved, by running the merge in-process and
  reading the resulting attribute rather than eyeballing the file:
  `merge_lords(parse_vanilla_lords(...), parse_current_xslt(...))` should report `None` for
  `lord_WE8_c` where vanilla reports `true`. Note the guard covers `lords.xslt` only; the file that
  actually wins at runtime, `characters/lords.xml`, has no regenerator and therefore no guard.
- **Source:** 2026-08-28, `a00086da`.

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/README.md](../../modding/README.md)
- [docs/modding/validation-and-testing.md](../../modding/validation-and-testing.md)
- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)
- [docs/reviews/lessons/data-content-cultures.md](./data-content-cultures.md)
- [docs/reviews/rca-butr-dependency-update-2026-07-16.md](../rca-butr-dependency-update-2026-07-16.md)

<!-- backlinks-end -->
### A tool that widens its own coverage must report the DEGRADED path, not just the success path

When a validator/audit gains a new data-dependent scope (an extra sweep root, a registry built from
an explicit file list, a globbed asset inventory), the run must be LOUD when that scope resolves to
nothing. Three instances shipped in one sitting: a missing `LOTRLOME_Armory` root printed `PASS`, a
shrunken body-property registry tripped the empty-registry guard and skipped its check, and an empty
chariot clip inventory made the `quad_movement` probe vacuous (`bound[r] in inv` is false for every
`r`). All three produced output indistinguishable from a healthy run — in a changeset whose entire
purpose was fixing an earlier silent under-scoping.

- **Why missed:** the existence filter and the success message get written in the same breath and
  nobody asks what the negative branch prints. An empty-input guard designed for one benign cause
  ("no game install", already reported) gets silently reused for a malignant one ("the file list
  broke"). All five deep-review core agents passed it; only the conditional tooling agent — the one
  that ran the tool with a deliberately wrong path instead of reasoning about it — caught them.
- **Prevent:** write the "resolved to nothing" branch in the SAME edit as the filter, and add a test
  asserting the tool says so (`missing_ref_roots`, `suspect_registries`). Set registry floors far
  below real counts so they catch a broken file list, not data drift. Ask of every new check: *if
  its input were empty, would this still report clean?* If yes, it is not a check yet.
- **Source:** `docs/reviews/rca-validator-silent-scope-2026-08-03.md`

### Read the enclosing method before naming a specific throw site

`CampaignTime.ToString()` was documented in 5 places (2 code comments, a test comment, an
investigation doc, a CHANGELOG entry) as throwing `DivideByZeroException` in `GetDayOfSeason`
(`/ TimeTicksPerDay`). It evaluates `GetYear` (`/ TimeTicksPerYear`) FIRST, and that is what throws.
The attribution came from grepping the class for a division and taking the first hit.

- **Why missed:** a grep hit *consistent* with the symptom reads as confirmation of it. Same
  exception type, same root cause, same fix — so nothing downstream ever contradicted the wrong
  detail. Caught only by decompiling installed v1.4.7 and reading the method body.
- **Prevent:** when naming a line/getter as the failure site, read the enclosing method's evaluation
  order. "A division exists in this class" is not "this division is the one that ran."
- **Source:** same RCA; `.claude/rules/evidence-over-claims.md` §C

### A majority-non-C# changeset needs the tooling agent, not just the core 5

The 5 core `/deep-review` agents are C#-centric. On a changeset that is mostly Python tooling they
will pass it while having reviewed the minority of the diff — here they returned clean or
quality-only findings while the conditional tooling agent found all five real defects.

- **Prevent:** treat the Step 2c tooling-agent trigger as mandatory whenever `tools/**/*.py|ps1` is
  more than a trivial slice of the changeset — **including read-only tools**, which the trigger's
  current wording ("scripts that WRITE files") does not cover. All three silent-scope findings above
  were in read-only tools.
- **Source:** same RCA

### A review finding's stated CAUSE can be wrong even when the finding itself is right

The 2026-08-03 field report's armory finding was correct to the element: 168 `<action>` elements in
`LOTRLOME_Armory/ModuleData/action_sets.xml` were parented by `<action_sets>` rather than by an
`<action_set>`, and they really did kill every dedicated server on boot. The attribution was not.
`tools/generate_race_civilian_action_sets.py` is the obvious suspect — it is the tool that writes
race action sets into that file — but the broken sets sit OUTSIDE its
`TAOM-CIVILIAN-COVERAGE:START/END` marker block and were hand-authored. Editing the generator would
have changed correct code, left the data untouched, and degraded every future run.
- **Why missed:** a plausible mechanism reads as an explanation. "This generator writes elements of
  this shape into this file, and elements of this shape are broken" is a syllogism with a missing
  premise (that these particular elements came from it), and the missing premise is the cheap thing
  to check — marker boundaries are right there in the file. This is a third shape distinct from the
  two review-attribution entries above: "when two review agents disagree, the narrower + more
  confident one is usually the wrong one" is agent-vs-agent premise scope, and "read the enclosing
  method before naming a specific throw site" is a mis-attributed line inside a correctly-identified
  method. Here the symptom is real and the accused COMPONENT is innocent.
- **Prevent:** verify the cause independently of the symptom before editing the accused component.
  For generated data specifically, check the generator's own marker/ownership boundaries before
  assuming it produced the region you are looking at — a generator is exactly the kind of component
  where a wrong fix is silent and compounds on every future run.
- **Source:** Multiplayer field report 2026-08-03 (TAOM v2.0.16 co-op testing); commit c9455ec8

### A deployment TARGET that never existed: the engine logs one line and runs degraded

Neither `Main/TAOM.csproj` nor `Dependencies/TAOM.Dependencies.csproj` ever produced a
`Win64_Shipping_Server` folder. A Bannerlord dedicated server therefore logged
`Cannot find: ...\TAOM\bin\Win64_Shipping_Server\TAOM.dll` and carried on — running a **vanilla
simulation over TAOM's map**: no race capture, no War of the Ring, no campaign systems at all. This
is a different failure from the two shipped-artifact entries above ("If a shipped artifact's identity
never changes…", "Build both, ship both…"), which are both about a mismatched pair of artifacts that
do exist; here the destination is absent and the engine runs past it.
- **Why missed:** the missing folder is invisible from every surface a developer looks at. The build
  succeeds, the client install is complete and correct, and the only evidence is one line in a log
  produced by a machine nobody on the project runs.
- **Prevent:** enumerate the engine BUILDS your artifact must be present for, not just the
  configurations you build. `MirrorWin64ShippingClientToServer` (both csprojs, `AfterTargets="PostBuildCopyToModules"`,
  same `$(ModuleId) != ''` condition family as the `-p:ModuleId=` entry above) mirrors the **assembled**
  client folder — `$(GameFolder)\Modules\$(ModuleId)\bin\Win64_Shipping_Client\*.*`, not build output,
  because the vendored natives and NuGet companions only exist there after `PostBuildCopyToModules`.
  It is modelled on the pre-existing `MirrorWin64ShippingClientToEditor` target; the same absent-target
  question applies to any future build folder. A real deploying build mirrored 10 files for TAOM
  (incl. `TAOM.dll`, `DryIoc.dll`, `MinHook.x64.dll`, `TAOM.NativeSkinFixes.dll`) and 42 for
  TAOM.Dependencies (incl. `TAOM.Dependencies.dll`, `Bannerlord.UIExtenderEx.dll`, `0Harmony.dll`,
  `MCMv5.dll`).
- **Owed, and a second hazard:** a dedicated-server boot verifying both this deploy and the
  `action_sets.xml` fix has not happened. Separately, the two opt-out flag files the field reporters'
  recipe required are now obsolete — `PatchShieldPolicy.ShouldInstall` already skips install under
  co-op presence and `SaveShieldPolicy.ShouldSwallow` already rethrows save-load faults under co-op.
  A documented workaround that outlives its cause is a maintenance hazard: it keeps being applied,
  and the next reader cannot tell whether it is load-bearing.
- **Source:** Multiplayer field report 2026-08-03 (TAOM v2.0.16 co-op testing); commit 5f373df9

### A registry built from the wrong merge order prints PASS while the game crashes

The new `LANDLESS_CULTURE` check is worth exactly what `build_settled_cultures` is worth. That builder
walks the settlement-contributing modules in load order (`Native`, `SandBoxCore`, `SandBox`,
`CustomBattle`, `TAOM_Map`) and must honour TAOM_Map's unconditional strip-XSLT, which deletes all 494
vanilla `<Settlement>` elements before contributing its own 988. Skip the strip and those 494 count:
every culture reads as landed, the check reports clean, and the game still CTDs on the landless one.
This is the inverse of the silent-scope entry above — that one catches a scope resolving to NOTHING,
which at least has a tell; a scope that resolves to TOO MUCH produces the output shape everyone is
hoping for and has no tell at all.
- **Why missed:** a merged-data registry is assembled once at the top of the run and then trusted by
  every check downstream. Nothing re-derives it, nothing compares it against what the engine actually
  loads, and an over-broad registry never trips an empty-input guard.
- **Prevent:** state the merge SEMANTICS in the builder (append / replace / strip-then-append) rather
  than assuming the additive case, and assert the result against counts measured independently — 988
  settlements, 28 distinct cultures after the Khand retag. Then run the new check against a KNOWN-BAD
  state before wiring it in: 18 `LANDLESS_CULTURE` errors before the retag, `PASS: no validation
  issues found.` after. A check nobody has seen go red is not a check yet.
- **Source:** #374 (`tools/taom_schema.py` `build_settled_cultures` / `_landless_cultures`)

### A permanently-red gate is a broken gate — put known exceptions in an allowlist with stated reasons

`_landless_cultures` fires on any culture used by an `occupation="Lord"` NPCCharacter, a `<Faction>` or
a `<Kingdom>` that owns no settlement — and TAOM ships ten such cultures on purpose or by inheritance.
Left unlisted, an ERROR-severity check in a pre-commit path would be red forever on TAOM's own data,
which teaches people to route around it. `_LANDLESS_BY_DESIGN` names each with its reason in-code: the
six bandit cultures are unreachable because `GetBestAvailableCommander` filters on `Occupation.Lord`
and bandit heroes are `Occupation.Bandit`; `neutral_culture` carries no lords; `darshi` / `nord` /
`vakken` are vanilla minor factions TAOM inherits and never re-cultured, all three still holding a
valid `initial_home_settlement`.
- **Why missed:** the cheap alternative to an allowlist is narrowing the predicate until it only fires
  on the case you already found — which passes, looks clean, and is silent on the next instance. An
  allowlist keeps the check wide and makes every exception an auditable claim someone can later
  disprove: the `darshi` / `nord` / `vakken` entries are exactly that, and they carry no TAOM content
  at all, a follow-up nobody would ever see if the predicate had been narrowed instead.
- **Prevent:** when a new check fires on shipped data, decide per case — fix the data or allowlist it
  with a written reason. Never narrow the predicate to dodge them. Keep the allowlist in the tool
  where the check can be read against it, not in a doc. Keep the check's SCOPE honest too: this one
  covers TAOM's own ModuleData, matching the validator's documented contract, so a vanilla-inherited
  faction is the engine guard's problem (`Patch65_LandlessCultureSpawnGuard`), not a TAOM data defect.
- **Source:** #374 (`tools/taom_schema.py` `_LANDLESS_BY_DESIGN`)

### A binary-format parser's negative tests must cover TRUNCATION — and struct.error is not a ValueError

**Why missed:** `native_crash_triage.py --dump` (#387) caught `ValueError` around the minidump
parse, but every truncated-file `struct.unpack` raises `struct.error`, which subclasses `Exception`
directly — so a torn dump (the exact artifact class the tool exists to meet: crash bundles) produced
a raw traceback. The negative tests covered bad-signature and missing-file, never truncation; the
stream header's `szentry` was also trusted blindly. Five graceful-degradation paths existed in code
with zero tests.

**Prevent:** For any binary-format parser: (1) enumerate the parsing library's REAL exception types
(`struct.error`, `OSError`) at every catch site instead of assuming taxonomy; (2) write one
truncation test per stream/section boundary (header, directory, per-entry short read) asserting a
message, not a traceback; (3) validate size/count fields from the file before trusting them in
reads; (4) one test per graceful-degradation path — "degrades correctly by inspection" is how a
refactor silently turns degrade into crash.

**Source:** deep-review 2026-08-05 (tooling agent), `docs/reviews/rca-memsample-telemetry-2026-08-05.md` finding #2.

---

### The module deploy NEVER deletes — a file removed from the repo lives on in the game install forever

**Why missed:** `Bannerlord.BuildResources.targets:33` mirrors `$(ProjectDir)/_Module` into
`Modules/$(ModuleName)` with `Clean="false"`. Deleting a prefab/brush/XML from the repo therefore
does nothing to the installed copy — it just stops being refreshed. Compounding it: the standard
agent-briefing build (`dotnet build … -p:DisableModuleCopy=true`) skips the deploy entirely, so a
whole session's GUI edits can be green in tests, committed, and still not present in the running
game. On 2026-08-05 the career arc deleted `AbilityHUD.xml` from the repo and edited
`CareerScreen.xml`; the install kept a *foreign* `CareerScreen.xml` (a contributor's rewrite,
byte-identical to their package) plus their blanked `AbilityHUD.xml` and a clone of vanilla's
`Mission/AgentStatus.xml` referencing a widget class no installed DLL defines — a broken-HUD
hybrid that no test could see.

**Prevent:** after any session that ADDS, EDITS or DELETES files under `Main/_Module/`, run a plain
`./build.ps1` (no `DisableModuleCopy`) and then verify the install directly — `ls -la` the changed
paths and grep one new binding/brush name in the DEPLOYED file, not the repo file. For deletions,
remove the installed copy by hand; the build cannot. Same family as the sprite-atlas lesson in
`lessons/localization-ui.md` ("delete the old PNGs from the GAME INSTALL, not just the repo") —
the install is not a mirror of the repo, it is an accumulation of every build ever run.

**Corollary for evaluated third-party packages:** a reference module laid out as `source/_Module/`
mirrors OUR module structure, so its files can be hand-copied into `Modules/TAOM/` during
evaluation and then look like ours. Before trusting anything about in-game behaviour, diff the
installed file's byte size against the repo's — identical-to-the-package is the tell.

**Source:** career UX arc post-commit install audit 2026-08-06; RCA `docs/reviews/rca-career-ux-arc-2026-08-05.md`.

### The career language XMLs use `\r\r\n` — text-mode Python I/O silently doubles every line

**Trap:** `Main/_Module/ModuleData/Languages/*/std_taom_career_strings_*.xml` are committed with
`\r\r\n` line endings (verify: `git show HEAD:<file> | head -c 80`). Python's universal-newline text
mode reads `\r\r\n` as TWO line breaks and `write_text` emits them back as `\r\n\r\n`. The content
stays correct and the file still parses, so nothing fails — but a 164-string edit lands as a
**6,180-line whole-file diff** with a blank line inserted between every original line, ×12 files.
Caught on 2026-08-06 only by noticing `git diff --stat` was asymmetric (51,407 insertions vs 26,687
deletions); a symmetric diff would have hidden it.

**Prevent:** `tools/README.md` already mandates the binary round-trip for ModuleData XML —
`read_bytes()` / `decode` / regex / `encode` / `write_bytes()`. This file family is the reason the
rule is not merely cosmetic: for ordinary CRLF files text-mode happens to round-trip, so a script
can violate the convention for years and look fine, then destroy these twelve. Never
`read_text`/`write_text` a ModuleData XML, even when the edit is "just one attribute".

**Diagnostic that generalises:** after any bulk data edit, check `git diff --stat` for
insertion/deletion asymmetry before reading the diff itself. Equal counts mean you changed lines;
unequal means you changed the file's *shape*, which is almost never intended.

**Second trap in the same change (different mechanism):** `tools/translate_with_claude.py` looks its
translation cache up by `string_id` alone (`elif e.string_id in cache`) and never checks that the
English source still matches. So editing an English string in `taom_career_strings.xml` does NOT
invalidate its cached translation — the next `/localize` run serves the OLD translation straight
back into all 12 language files and silently reverts the edit. Any tool that rewrites English source
text must also re-point `tools/translation_cache/<lang>.json`, or the change has a delayed fuse.
`tools/retune_career_health.py` does this in its third pass.

**Source:** #388 career health retune, 2026-08-06.

### A value-remapping tool is only idempotent if its old and new value sets are disjoint

**Trap:** the natural way to make a retune script re-runnable is to key the swap on the OLD value —
`if current not in MAPPING: skip  # already retuned`. That is correct only while the mapping's keys
and values do not overlap. The moment they do, an already-converted pip sits on a value that is
still a mapping KEY, the skip branch becomes unreachable for it, and a second `--apply` shifts it
again.

Caught by the tooling agent in deep review 2026-08-06 as CRITICAL. `tools/retune_career_health.py`
started with one profile (`health`, 25-100 → 5-10, disjoint, guard sound). A second profile was
added (`troopdamage`, 0.03-0.20 → 0.02-0.08) whose keys and values overlap on
{0.03, 0.05, 0.06, 0.08}. A re-run would have silently double-shifted **71 of 105** pips across four
data surfaces — magnitude, English description, source string, 12 language files and 12 translation
caches. The tool's own docstring, `tools/README.md`, the CHANGELOG and the feature doc all asserted
"re-running is a no-op"; all four were wrong, and the dry-run's own "71 pips found" was the tell.

**Why missed:** the guard was written and verified against the profile it was born with, then a
second profile was added without re-checking that the guard's *precondition* still held. The
precondition was never written down, so there was nothing to re-check against.

**Prevent:**
- Decide idempotency at FILE level, not per item: if every item already sits on a target value and
  none sits on a key that is not also a target, the transform has run. That is decidable even when
  per-item detection is not (`already_applied()` in that tool).
- Assert or warn on the precondition explicitly (`overlapping_values()`), so a future profile with
  an overlapping mapping announces itself instead of silently disarming the guard.
- Unit-test idempotency **per profile**, driven off the config table rather than a hand-written
  list, so adding a profile cannot skip the test (`tools/tests/test_retune_career_health.py`).
- Treat "re-running is a no-op" as a claim requiring proof — run the dry-run twice and read the
  count. It reported 71 in plain text the whole time.

**Generalises to:** any migration, renamer, or unit-converter with an old→new table — ID
remappings, tier shifts, percentage rescales. Ask first: *can old and new values collide?*

**Source:** deep review of the career effect layer audit, 2026-08-06;
`docs/reviews/rca-career-effect-layer-audit-2026-08-06.md` finding #3.

### A feature is not verified until a real container has resolved it

Unit tests that `new` a service directly, and wiring tests that assert on `IoC.cs`/`SubModule.cs`
**source text**, both pass happily while DryIoc cannot construct the type at all. DryIoc validates
constructor selection at `Register` time, not first resolve — so two public constructors on a
registered class throw `UnableToSelectSinglePublicConstructorFromMultiple` inside `IoC.Configure()`,
called from `SubModule.OnSubModuleLoad()`: a hard crash to desktop **before the main menu**, with a
green test suite and a clean build.

**Why missed:** all six `/deep-review` dimensions reason about code that is already running —
standards checks that a registration *exists*, completeness checks that a test file exists per
class, API-compatibility scopes to TaleWorlds signatures, and performance/data-flow/lifecycle all
presuppose a live campaign. No dimension owned the question *"does this feature survive startup?"*.
The repo's existing `*WiringTests` pattern reinforced the blind spot by asserting on source text,
which structurally cannot see a constructor-selection failure.

**Prevent:** every IoC-registered feature gets one test that builds a real `Container`, calls the
feature's `Register…Feature(container)`, and resolves each registration — plus an invariant that
every registered implementation exposes exactly one public constructor (make extra overloads
`internal`; `InternalsVisibleTo` keeps them reachable from tests). Add the container round-trip to
the review checklist for any changeset touching `Main/IoC.cs`.

**Generalises to:** any DI-registered type, and any framework that validates at registration rather
than at resolve.

**Source:** `docs/reviews/rca-economy-diagnostics-2026-08-06.md` finding #1 — found by the user
launching the game mid-review, not by the review.

### Enumerate a patch target's call sites before writing a durable log in it

**Symptom:** the Patch69 roster postfix wrote a durable, synchronously-flushed INFO line claiming to
run "once per tournament". `FightTournamentGame.GetParticipantCharacters` had four known call sites (six in fact on v1.4.8: the off-screen `TournamentManager.ResolveTournament` and `HeroKnownInformationCampaignBehavior` were missed); two
(`GetMenuText`, `GetTournamentPrize`) run from the arena join menu's `on_init`, so every menu open hit
the disk.

**Why missed:** the call frequency was checked only against "is this per-frame?" — it is not — and that
answer was treated as sufficient. Not-per-frame is not once-per-event.

**Prevent:** enumerate the target's callers before costing any logging in a postfix. Reserve durable
levels (INFO+, which `FileLogger` flushes synchronously so a native CTD preserves the tail) for events
that genuinely happen once; put the common case on DEBUG.

**Source:** `docs/reviews/rca-patch69-tournament-guard-2026-08-07.md` finding 1 (#407).

### Read-binary/write-binary applies to any tracked file, not just ModuleData XML

**Symptom:** a one-row edit to `tools/README.md` produced a 297-line whole-file diff. The file is
committed CRLF; the edit was written with `newline='
'`.

**Why missed:** `.claude/rules/moduledata-validation.md` scopes its XML I/O convention to *ModuleData
XML*. This was a `.md`, so the convention was not applied — the identical failure mode, one file type
outside the rule's stated scope.

**Prevent:** the discipline is about any tracked file whose line endings or BOM you did not author.
Read bytes, replace bytes, write bytes. Check with `git diff --numstat` vs
`git diff --ignore-all-space --numstat` — a large gap between them is line-ending churn.

**Source:** `docs/reviews/rca-patch69-tournament-guard-2026-08-07.md` finding 5 (#407).

### A check that is 100% false positives is a deleted check — repair the checker, not the docs

**Symptom:** `lint_docs.py`'s stale-version check reported 29 findings and every one was a legitimate
historical reference ("Ported for TAOM v1.3.15", "v1.3.15 reference RVA, informational only"). One
was the rule text in `agent-operating-manual.md` that *defines* staleness; another was `adrs/010`,
the ADR that recorded this very problem, reporting itself.

**Why missed:** the check's model was "a version string older than the pin is rot", which is not what
rot is. The tempting repair — edit the 29 docs — would have falsified accurate history to satisfy a
wrong check. The 2026-08-05 exemption sweep had already tried the other tempting repair and failed:
its exemptions were whole-FILE switches (directory, filename, path part), and the remaining sites
lived in docs that must stay linted (`native-skin-fixes.md` alone held 10).

**Prevent:** when a check is mostly false positives, fix its *definition* at the granularity the
distinction actually lives at. Here that was per-LINE — "is this naming the current target, or
recording history?" — so the model became "a version string **presented as the current target** is
rot". Measured: the line-granular flip cleared 26 of 29; the wording-marker approach proposed in the
issue cleared 16. Measure competing repairs before picking one.

**Source:** #397 → #399 (`fa7ba39b`), external contribution reviewed 2026-08-07.

### A checker reporting zero is indistinguishable from a checker that is dead

**Symptom:** #399 took the doc linter from 43 findings to 0. A silent check and a broken check produce
byte-identical output.

**Why missed:** nothing in the repo distinguished the two states. The 29 false positives had already
survived months precisely because a noisy check trains readers to skip it — the failure mode is
symmetric, and a quiet one invites nobody to look at all.

**Prevent:** any change that narrows a check ships with a fixture proving it still fires — here
`test_naming_an_old_version_as_the_current_target_is_still_reported`. Treat that test as
load-bearing: a later exemption pass that deletes it converts the check into decoration with no
visible signal. Write the narrowing's blind spots down where the *reader of a clean run* will see
them (skill body, tool README), not only in the PR description, which stops being visible on merge.

**Source:** #397 → #399, review by @Sternab; gaps carried forward as #405.

### Exempt a link check by target, not by linking file — and watch for author-clean/contributor-dirty checks

**Symptom:** 14 dead links, all pointing into `docs/reviews/raw/`, which is gitignored (`.gitignore:157`
— the transcripts are 2-4 MB each). They resolve on the machine that ran the review and are dead on
every fresh clone, so the check read clean for its author and dirty for everyone else.

**Why missed:** the existing exemptions were all keyed on the *linking* file, and `rca-*` /
`REVIEW-LOG` were already exempt from the stale-version check, which made "exempt them here too" look
consistent. It would have switched off dead-link coverage for `REVIEW-LOG.md`'s several hundred other
links.

**Prevent:** two rules. Exempt on the property that actually causes the exemption — here the target's
location, not the linker's name. And whenever a check's input includes untracked or gitignored paths,
assume its output differs per machine; verify against a fresh clone (or simulate the absent dir)
before trusting a clean local run.

**Source:** #397 → #399 (`fa7ba39b`).

### `tools/README.md`'s whole-file diff is `core.autocrlf` vs a CRLF blob, not the write mode

**Symptom:** a byte-preserving 11-line insert into `tools/README.md` still produced
`git diff --numstat` = 307/296, i.e. the whole file, while `--ignore-all-space --numstat` = 12/1.

**Why missed:** the sibling lesson above attributes this file's churn to writing with `newline='\n'`.
That is one cause, but fixing it does not clear this one. `core.autocrlf=true`, there is no
`.gitattributes`, and the HEAD **blob** for `tools/README.md` contains CRLF (296 of them) — so git
cleans the working copy CRLF→LF for the comparison and every line reads as changed no matter how the
edit was written. `docs/ai-includes/agent-operating-manual.md` is also CRLF on disk and diffs 1/1,
because its blob is LF; the disk encoding is not the discriminator.

**Prevent:** diagnose with `git cat-file -p HEAD:<path>` and count `\r\n` in the **blob** before
blaming the editor. A CRLF blob under `autocrlf=true` normalizes on the next commit regardless, so
the churn is a repo-state decision (normalize the file, or add a `.gitattributes`), not something an
individual edit can avoid. Raise it; don't silently normalize a file as a side effect of an unrelated
change.

**Source:** verified 2026-08-07 while adding the `lint_docs.py` row (#399 doc pass); corrects the
scope of the `newline=` lesson above.

### A hardcoded path with no flag is not a degraded tool, it is an unrunnable one

**Symptom:** five `tools/` diagnostics — `audit_battle_scenes`, `audit_mount_parity`,
`audit_scene_names`, `validate_all_troop_refs`, `validate_gondor_refs` — resolved the game from a
literal `E:\Steam\steamapps\common\Mount & Blade II Bannerlord` and accepted no path argument. On
the author's machine they were fine; on any other install they exited on the first missing path. An
outside contributor could not run them at all, and nobody noticed for as long as they had existed.

**Why missed:** `BANNERLORD_GAME_DIR` was already a documented prerequisite in `README.md`, already
set by `setup-dev-env.ps1`, and already read by `verify_mount_assets.py`. The convention existed and
was simply not applied here — the kind of gap that is invisible from inside the environment where the
default happens to be correct. Every local run passed.

**Prevent:** two things. A tool that resolves an external path takes an override — env var, flag, or
both — and the literal is only ever the fallback. And the test that proves such a fix is **pointing it
at a nonexistent root**, not running it where the default already works: on the author's machine a
decorative change and a real one produce identical output. Verify in both directions — identical
output with the variable set and unset, changed behaviour when it points somewhere absent.

**Source:** #400 → #401 (`fe145207`). The remaining tail is #404: thirteen top-level tools still carry
the literal with no override, nine of which write rather than read, and the knob has drifted into four
variable names (`BANNERLORD_GAME_DIR`, `BANNERLORD_GAME_MODULES`, `TAOM_ARMORY_BASE`, `TAOM_MAP_FILE`).

### `grep -c` exits non-zero on zero matches, so `&& next-step` silently skips (2026-08-07)

`dotnet build … | grep -cE "error CS" && python - <<'PY' …` — the changelog step never ran, because a
CLEAN build makes `grep -c` print `0` and exit **1**, short-circuiting the `&&`. The commit went
through without its entry, and the failure was reported to the user as success because "0" looked
like the expected output.

Same shape twice in one session: later, `grep -oE "error CS…"` printing nothing was read as "build
succeeded" when the build had actually failed with `MSB4018` (a locked file), which that pattern
does not match.

**The rule:** verify a command's success from its **exit code**, never from a grep's silence. Grep
output is evidence of what matched, not evidence of what happened. `echo "exit=$?"` immediately
after the command, and pattern-match the output only to summarise it.

### Never `pull --rebase` while another session's work is uncommitted — and verify the autostash popped (2026-08-08)

`git pull --rebase --autostash` with 49 dirty files belonging to a concurrent session: the rebase hit
a CHANGELOG conflict on the first of eleven commits, and when it completed the autostash was **still
in the stash list, unapplied**. The working tree had none of that session's work in it. This is the
same failure that lost a session's work the previous morning, arriving through the flag that is
supposed to prevent it.

**The protocol that made it safe:**

1. `md5sum` every dirty file to a snapshot file BEFORE starting.
2. Rebase.
3. `git stash list` — an autostash entry surviving the rebase means it did NOT pop.
4. `git stash apply` (never `pop`) so the stash remains a net.
5. Compare every file against the snapshot, and diff the tree against the stash blob to confirm the
   restore is exact.
6. Build + full test run before pushing.
7. Leave the stash. It is redundant once verified, but it is the other session's net, not yours.

Also learned: hash mismatches against the snapshot are NOT automatically data loss — a concurrent
session writing between the snapshot and the autostash produces exactly that signature, and the
stash then holds their NEWER content. Distinguish the two by diffing the tree against the stash
blob (`git diff stash@{0} -- <path>`), not by trusting the hash comparison alone.

### "Untracked in this repo" is not "unfixed" — read the installed module before holding an issue open (2026-08-08)

Eleven issues were parked as `blocked-external` on the first triage pass because their fix lives in
`LOTRLOME_Armory` or `TAOM_Map`, neither of which this repo tracks. Re-checked against the installed
modules on a second pass, **seven were already applied** and closed: #352, #364, #300, #390, #338,
#342, #358. *Can this repo ship it* and *does the fix exist* are different questions, and only the
second one decides whether an issue is still an issue. The four that survived the recheck did so for
reasons unrelated to tracking — #398 and #385 wait on asset re-exports that have not happened, #62's
fix is inside a third-party mod, and #359 shipped and then regressed.

- **Why missed:** a triage driven off the repo's own diff and history never sees the external edit,
  so "no commit touches this" reads as "nobody fixed it". The label then makes the mistake durable —
  `blocked-external` describes where a file lives, not what state it is in.
- **Prevent:** an issue whose fix lands outside the repo is verified by reading the installed module,
  the same way any other issue is verified by reading the code. **The corollary is the more important
  half:** an external fix has no version control, so a module reinstall silently reverts it with no
  diff and no crash. The durable part of such a fix is the in-repo check that would catch the
  regression — `tools/validate_mesh_refs.py` for #390, `validate_moduledata.py`'s
  `MISSING_HARNESS_FAMILY_TYPE` for a harness like #364's — not the data edit. Close the issue on the
  data, but do not close it *without* the check.
- **Source:** `docs/audits/issue-triage-2026-08-08.md` (`62abaeb6`)

### When a quick re-check contradicts a well-evidenced verdict, suspect the re-check (2026-08-08)

Two shallow greps were run to spot-check careful agent verdicts during the issue triage. **Both were
wrong; the verdict was right both times.** One counted `<action ` inside `as_dwarf_warrior` and
reported 134 where `<action[\s>]` finds **4,842**. The other guessed `ui_taom/` for a sprite root that
is actually `ui_taom_career_system/CareerSystem/Abilities/` and reported **0** PNGs where 49 exist —
the guessed directory does not exist at all. Either one, believed, would have put a false number in a
public issue comment.

- **Why missed:** a cheap check that contradicts an expensive one feels like the cheap check caught
  something, because finding a hole is the outcome you are looking for. Neither failure looks like a
  failure: a wrong-path `find` and an under-matching regex both return cleanly, and `0` and `134` are
  perfectly plausible answers.
- **Prevent:** hold the re-check to the standard of the finding it contradicts *before* believing it.
  `ls -d` the directory before writing "0 files" — a path that does not exist and a path that is empty
  produce the same count. Point the pattern at one known-present instance and confirm it matches.
  Then compare.
- **Source:** #300, `docs/audits/issue-triage-2026-08-08.md` (`62abaeb6`)

### Both CHANGELOG files are newest-first — order the entries, never take the first grep hit (2026-08-08)

`CHANGELOG.md` and `docs/changelog-archive/CHANGELOG-2026-H1.md` are both strictly descending by date
(the live file opens on 2026-08-08 and ends on 2026-07-01; the archive opens on 2026-06-30 and ends
on 2026-01-24), and the `###` entries inside a single day run newest-first too. So
`grep -n … | head -1` returns the **most recent** mention, not the original one — the inverse of what
a log that appends at the bottom trains you to expect.

- **Why missed:** "first hit" and "first in time" are the same thing in an append-at-the-bottom file,
  and most tooling output is one. The changelogs prepend, so the line number silently inverts the
  chronology while still looking like an ordering.
- **Prevent:** anything reasoning about supersession — *was this reverted? which entry is current?* —
  collects every hit and sorts by the enclosing `## <date>` heading rather than reading one match.
  To find the ORIGINAL mention, take the **last** hit in the live file, then check the archive.
- **Source:** `docs/audits/issue-triage-2026-08-08.md` (`62abaeb6`)

### A tool that measures a subset of a global limit will report OK at 99% of it (2026-08-08)

`tools/check_prefab_budget.py` guards the editor's native prefab queue (`CAP = 131_072`,
`WARN = 120_000`). Its docstring states the constraint correctly — "every `<game_entity>` across all
loaded Prefabs folders" — and its `DEFAULT_DIR` then measures exactly one of them,
`TAOM_Map/Prefabs`. That folder holds **93,407** entities across 138 files, so the tool prints
`TOTAL: 93407 … OK` and exits 0. Counted the way the engine counts, across every installed module's
`Prefabs/`, the real figure is **130,151 of 131,072 — 99%, with 921 entities of headroom.** Dropping
the modules that need not load together (`NavalDLC` plus three third-party map/beast modules) still
leaves 120,178, already past the tool's own warn line.

- **Why missed:** the default scope encodes the folder we own rather than the resource the cap covers,
  and every run since has been green. A green gate over the wrong denominator is indistinguishable
  from a green gate.
- **Prevent:** when a check guards a shared or global resource, the *default* measurement is the whole
  resource and the part you own is a filter on the report, never the denominator. Write the constraint
  out as a sentence and confirm the measured set is that set — this tool's own docstring already said
  it and the code still did not.
- **Source:** #359, `docs/audits/issue-triage-2026-08-08.md` (`62abaeb6`)

### A measurement tool must be able to detect that it is measuring the wrong thing

Every measurement pipeline needs at least one independent quantity it can cross-check itself
against, and it must **stop** rather than report when the check fails.

**Why missed:** AutoResolveDiagnostics (2026-08-08) shipped four defects into review that each
produced plausible output rather than an error — a 0.0% loss rate for every class (the analyzer read
a key the producer never wrote), zero records parsed (wrong log path and format), a composition
table built from winners only, and a morale column reading the wrong engine property. For a tool
whose output is pasted into a balance config, a silent wrong answer is worse than no tool: it
launders a guess into a number.

The safeguard had even been *written about*. `menStart` was logged specifically so the analyzer
could cross-check its roster reconstruction, and three separate places — a DTO comment, the feature
doc and the CHANGELOG — described that check. It was never implemented. That is why the
survivorship bias survived: the comment read as evidence the check existed.

**Prevent:** (1) validate the producer/consumer field contract in both directions, on every record,
and hard-stop on drift — a warning that is followed by the reports anyway is not a gate. (2) Log one
authoritative quantity that is derived independently of the rest, and assert against it. (3) Never
write the justification for a safeguard in the same pass as the field it justifies; write the
safeguard first.

**Source:** `docs/reviews/rca-autoresolve-diagnostics-2026-08-08.md`.

### A validation constant nobody references is worse than no validation

`tools/analyze_battle_logs.py` declared `SUPPORTED_VERSIONS`, `EXPECTED_PARTY` and `OPTIONAL_PARTY`,
and the C# `BattleLogRecord` doc comment told readers the analyzer "refuses to analyse a version it
does not understand rather than producing quiet nonsense." A `grep -c` for each name returned **1** —
its own definition. The gate had never existed, and the documentation asserting it made anyone
reading the code *less* likely to check.

The tell that this is a category, not an incident: a session editing that file changed
`SUPPORTED_VERSIONS = {5}` to `{5, 6}` believing it was loosening an active constraint. A constant
that looks like configuration is assumed to be wired.

**How to apply:** when a constant encodes a rule, the same commit adds the assertion that consumes
it — and one test that feeds a violating input and expects rejection. When reviewing a validation
block, `grep -c` each constant it names before believing the block does anything; a count of 1 means
the rule is decoration. Prefer validating the union across all records to sampling `records[0]`:
drift does not have to appear in the first row, and a field only a defender or only a siege carries
will sail past a single-sample check.

**Source:** review wave on #430, 2026-08-08.

### A decompile stack that skips module-bin assemblies makes an engine diff silently partial, and the loss is one-way (2026-08-10)

The v1.4.7 → v1.4.8 assembly diff read as complete — 8 of 56 changed, each one accounted for — and
it covered `<GameBin>\Win64_Shipping_{Client,wEditor}` alone, because that is the only place
`tools/decompile_bannerlord.ps1` walked. `tools/decompile_to_folder.ps1` could not fill the gap
either: it takes a single mandatory `-Source` bin folder, and its `Modules` category pattern is
anchored to `^(SandBox|SandBoxCore|StoryMode)\.dll$` — the primary DLL per module. So the 34 vanilla
assemblies that ship inside a module's own `bin\Win64_Shipping_Client` (`SandBox.View`,
`SandBox.ViewModelCollection`, `SandBox.GauntletUI`, `TaleWorlds.MountAndBlade.View`,
`TaleWorlds.MountAndBlade.GauntletUI`, `TaleWorlds.MountAndBlade.Platform.PC`, the
StoryMode/Multiplayer/NavalDLC satellites, `CustomBattle`, `BirthAndDeath`, `FastMode`, `DOTS`) were
in no decompile artifact at all — and TAOM patches into several of them (`AgentVisuals`,
`CharacterTableau`, `MobilePartyVisual`, `SPInventoryVM`, the tournament controllers). Steam
overwrites the install in place, so **the loss is one-way**: an assembly absent from the stack when
an update lands has no recoverable baseline afterwards. The v1.4.7 bytes for those 34 are gone.

- **Why missed:** the diff's denominator was the folder the tool already walked, not the set of
  assemblies the game loads, and every prior bump produced a clean-looking result off the same
  denominator. Nothing in the pipeline compares its own coverage against the install's DLL
  inventory, so "56 assemblies diffed" reads identically whether that is all of them or two thirds.
  Same shape as the prefab-budget lesson above — a green result over the wrong denominator is
  indistinguishable from a green result.
- **Prevent:** before trusting an engine diff, enumerate every `bin\Win64_Shipping_Client` the game
  loads from — the base bin **and** `Modules\*\bin\` — and confirm each DLL landed in an artifact.
  The baseline has to be captured BEFORE the update, because it cannot be reconstructed after.
  `decompile_bannerlord.ps1` now carries a `_modules_build` pass over
  `Modules\*\bin\Win64_Shipping_Client` (125 managed DLLs, written as `<Module>__<Dll>.cs` because
  names collide across modules), so the next bump is diffable. Recovery for *this* one was partial
  and accidental: `~/.taom-src/v1.4.7/` had cached 475 per-type decompiles, 42 of them from module
  DLLs, which diffed 1.4.7-vs-1.4.8 as 42 identical / 0 changed. A per-type lookup cache is an
  accidental baseline — do not plan to be saved by it twice.
- **Source:** `docs/migration/v1.4.8-impact.md` ("The decompile stack had a 34-assembly hole").

### A fail-open guard whose failure mode is silence reads as "all clear" — make it fail LOUD (2026-08-10)

`session-start.sh`'s game-version drift check printed nothing on the v1.4.7 → v1.4.8 bump, the exact
event it exists to catch. Nothing is also what "no drift" looks like. The cause was a shell default
substitution: `"${BANNERLORD_GAME_DIR:-<literal>}/bin/.../Version.xml"` substitutes the literal only
when the variable is **unset or empty**, so a variable that was *set but did not resolve in the
hook's environment* took the `-f` test straight to false and the whole block fell through without a
word. `.claude/settings.json` defines no `BANNERLORD_GAME_DIR`; the hook inherits whatever the
harness process happens to carry.

- **Why missed:** the guard was written and verified in the one environment where the variable
  resolved, and its skipped path and its clean path emit the same thing — nothing. A gate whose pass
  state is "no output" has no observable difference between working and dead, so no session could
  have noticed; the drift was found later, by diffing the install.
- **Prevent:** fail-open is mandatory for TAOM hooks (`.claude/rules/harness-facts.md`), but
  fail-open must still be fail-LOUD — a guard that can be skipped says it was skipped. The fix tries
  the env path, then **always** falls back to the known install, and prints `engine drift is
  UNCHECKED this session, not absent` when neither resolves. When a path comes from an environment
  variable, build a candidate list with an unconditional fallback rather than `${VAR:-default}`,
  which defends against unset/empty and never against wrong. Test the guard by handing it a bogus
  path and confirming it still says something.
- **Source:** `docs/migration/v1.4.8-impact.md` ("`session-start.sh` — the drift guard failed on the
  event it exists for"); `.claude/hooks/session-start.sh`.

### For a native-only changelog item, audit the DATA feeding the native path — not the C# calling it (2026-08-10)

v1.4.8's "Fixed horse rein visual bug when a mounted agent died" was first ruled **Unaffected** off a
C#-only grep: TAOM has no Harmony patch on agent death, ragdoll or reins. Wrong question. The fix is
native with no managed diff anywhere, so no grep of `Main/` could return a hit — a zero-hit grep was
guaranteed before it ran. TAOM's exposure is in Monster DATA. Measured against the live install:
native `horse` / `camel` / `mule` each declare the full set of 12 `rein_*` attributes and are
rideable; native `horse_2` / `camel_unmountable` / `mule_unmountable` declare none and are not
rideable. `taom_war_elephant` and `taom_mumakil` declare **zero rein attributes and are rideable**
(`LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_{elephant,mumakil}.xml`), `Monster.spider`
declares a partial set, `chariot` the full 12. In vanilla, "rideable" and "declares a full rein set"
are the same set; TAOM breaks that pairing. Rideability is declared, not inferred —
`LOTRLOME_items/LOTRAOM_horses.xml` carries `<Horse monster="…">` for `Monster.chariot`,
`Monster.spider`, `Monster.taom_mumakil`, `Monster.taom_war_elephant` — and
`tools/audit_mount_parity.py` contains **zero** occurrences of `rein`, so nothing gates it. This is
an UNVERIFIED risk awaiting an in-game test, not a confirmed defect.

- **Why missed:** "does TAOM patch this?" is the reflex question at a bump and the right one for a
  managed change. For a native fix it is unanswerable by construction: the absence of a managed
  surface guarantees the empty result, which then reads as evidence of safety instead of evidence
  that the wrong instrument was used.
- **Prevent:** classify each changelog line as managed or native FIRST. For a native one, ask what
  data TAOM feeds into that subsystem and compare its shape against vanilla's — a total conversion
  is usually the only caller producing the unusual input. Where the comparison finds a gap, the
  subsystem's parity auditor gets the check (`audit_mount_parity.py` covers usage actions and gait
  clips; rein attributes are in neither). The live monster files sit in unversioned dependency
  modules (`LOTRLOME_Armory`, `Alliance.Wargs`), so per the CLAUDE.md trap any fix there ships with
  a repo-side validator gate beside it, or a module reinstall silently reverts it.
- **Source:** `docs/migration/v1.4.8-impact.md` (changelog row N7 — rein / ragdoll).


### Batch verification; a suite you already ran is not new evidence

`evidence-over-claims.md` requires fresh verification before a completion claim. It does NOT require
verification after every edit, and reading it that way is how a session spends most of its wall-clock
waiting on its own test runs.

- **Why it happens:** each individual re-run feels like diligence, so there is no single moment at
  which the cost becomes visible. The 2026-08-11 enlistment session ran the full 6,380-test suite
  roughly fifteen times across seven fixes. The marginal runs proved nothing that a batched run at
  each item's completion would not have, and the user noticed the latency before the session did.
- **Prevent, a rate that keeps the guarantee intact:**
  - **Compile** after each edit. Fast, and it catches the error that actually happens most.
  - **Filtered suite** (`--filter FullyQualifiedName~XxxTests`) while iterating on one component.
  - **Full suite** at each work-item boundary and once before the review gate. That is the run whose
    result you quote, and it is the only one the rule ever asked for.
- **Same discipline for engine lookups:** batch `ilspycmd` / `taom-src` calls for related types into
  one command instead of one round trip per type.
- **Source:** 2026-08-11 enlistment field-fix session, user-reported.

### Use Edit for edits; reach for a script only for genuine fan-out

A throwaway Python or sed script to change one constructor or one comment is slower than the Edit
tool, not faster. It fails on assumptions you cannot see until it runs, and a failed script leaves you
re-reading the file to work out what state it is now in.

- **Why it happens:** batching several edits into one script LOOKS like the efficient move, and for
  genuine fan-out (the same mechanical change across 12 language files, or 8 test constructors) it is.
  For one or two edits it inverts: the script needs anchors that must match exactly, and a mismatched
  anchor costs a full read-diagnose-rewrite cycle.
- **Concrete failures, one session:** a constructor extraction took **three attempts** (wrong method
  ordering, then a regex that did not match the ctor signature); a `sed` meant to rename a parameter
  also mangled the doc comment above it and needed a manual repair; and a heredoc broke on an
  apostrophe inside the payload.
- **Prevent:** Edit for 1-3 sites. Script only when the same change lands in 4+ files, and when it
  does, make every anchor an `assert` so it fails loudly and atomically **before** the write rather
  than half-applying. Write the script to the scratchpad with the Write tool and execute the file,
  rather than piping a heredoc through the shell, which turns every apostrophe and backtick in the
  payload into a quoting hazard. Never chain a backup through `/tmp` on Windows: the Read tool cannot
  see git-bash `/tmp` paths, so a failed restore is invisible.
- **Source:** 2026-08-11 enlistment field-fix session.

### Do not refactor during a review gate

A cleanup that is not fixing a reported defect does not belong between "changeset complete" and
"changeset committed". It churns the exact files the review agents are reading, invalidating their
results, and it delays the finding that would actually change the code.

- **Why it happens:** a review flags a code-quality issue, and fixing it immediately feels like
  responsiveness. The tell is writing the words *this is polish, not a fix* and then doing it anyway,
  which happened verbatim on 2026-08-11.
- **Prevent:** while a review is outstanding, act only on findings that change runtime behaviour.
  Queue quality findings and address them after the gate closes, or in a follow-up commit. The one
  exception is a violation your own changeset introduced or deepened, which is yours to fix, and even
  then hold the edit until the agents reading that file have reported.
- **Source:** 2026-08-11 enlistment field-fix session.

### A diagnostics change justified on volume must carry the measurement, in the comment

"This line is noisy" is not a reason to downgrade or delete it. The count is the reason, and if the
count cannot be produced the change does not go in. TAOM's `FileLogger` writes INFO synchronously
and leaves DEBUG on an async queue that a hard native CTD discards, so every INFO-to-DEBUG move
spends real crash-forensic value; the price is only worth paying against a measured volume.

- **Why missed:** `ServiceBattleService`'s join-refusal line was moved to DEBUG under a code comment
  asserting it "lands after every single fight." The field log that motivated the whole session was
  open at the time and says 3 occurrences across 5 joins in 39 minutes. Nobody checked, because the
  frequency was incidental to the change rather than its subject. The deep-review efficiency agent
  caught it only because its prompt carries a standing order to read `FileLogger.cs` before costing
  any logging change, a rule added after the 2026-08-03 battle-load incident.
- **Prevent:** put the measurement inline, with its source: "3 lines across 5 joins in 39 minutes,
  `taom_debug_2026-08-12_12-50-32.log`". A comment that states a frequency reads as settled fact to
  the next person in the file, so an unmeasured one is worse than no comment. Note the surface:
  `evidence-over-claims.md` §C lists "doc / CHANGELOG / commit message" and does not name code
  comments, which is exactly where this one landed.
- **Source:** `docs/reviews/rca-enlistment-diagnostics-legibility-2026-08-12.md` finding #1.

### Confirm a negative by exhaustion, not by sampling

"I read the file and found no other assignment" and "no other assignment exists" are different
claims, and only the second justifies writing an engine invariant into a shipped comment.

- **Why missed:** the claim that `MapEventSide.LeaderParty` is assigned in exactly two places went
  into a code comment and the CHANGELOG after one file was read. It happened to be true. The
  compatibility agent established it properly: decompile the whole assembly as a project with
  `ilspycmd -p`, grep every file for the assignment, then check the setter's accessibility and the
  absence of `InternalsVisibleTo` to prove no other assembly can reach it either.
- **Prevent:** for any "this is the only place X happens" claim about engine internals, decompile
  the whole assembly and grep it, and close the loop on accessibility. It costs one command more
  than reading a single file. Relatedly, a subagent's decompile finding that is about to become a
  durable repo artifact needs the same first-hand verification as one relayed to the user;
  `evidence-over-claims.md` §A.4 reads as though it governs only the latter.
- **Source:** `docs/reviews/rca-enlistment-diagnostics-legibility-2026-08-12.md` findings #4 and the
  Agent 2 note.

### A tool that writes outside the repo still owes the ModuleData I/O convention

A Python tool whose *target* is the live game install (`Modules/TAOM_Map/ModuleData/settlements.xml`)
needs the byte-level BOM and newline idiom from `tools/README.md` exactly as much as one editing a
tracked file. `Path.read_text(encoding="utf-8")` does NOT strip a BOM, it decodes it to a literal
U+FEFF, and `write_text` re-encodes it; on Windows the newline translation is likewise symmetric for a
uniformly-CRLF file. So the round-trip can come out byte-identical and look correct while being correct
by codec coincidence, not by construction. Point the same code at a doubled-CR language file, or run it
under WSL where `os.linesep` is `\n`, and it rewrites the whole file.

- **Why missed:** `.claude/rules/moduledata-validation.md` is correctly path-scoped to `tools/**/*.py`,
  so the rule was in scope. It either did not fire on the `Write` that CREATED the file (as opposed to
  a `Read`/`Edit` of an existing one), or it fired and was not applied. Treat scoped-rule coverage as
  unproven for a file you are creating rather than opening.
- **Prevent:** for any script that writes XML anywhere, use `read_bytes()` + `startswith(BOM)` +
  `decode("utf-8-sig")` + `write_bytes(BOM_if_present + text.encode("utf-8"))`, and prove it by
  round-tripping the real target file and asserting byte equality before shipping. A dedicated tooling
  review agent has now caught this class twice; the C#-centric core agents structurally cannot see it.
- **Source:** `docs/reviews/rca-fiefgranting-2026-08-14.md` finding #1.

### Separate "resolve attempted" from "resolve succeeded" in every lazy service cache

The `_field ??= IoC.Resolve<T>()` idiom is written for the success path. When the resolve THROWS, the
field stays null, so the next call re-enters the try and throws again. In a method the engine calls
often this turns one misconfiguration into a silent exception storm: `CalculateMeritOfOutcome` runs 3N
times per election (vanilla calls `NarrowDownCandidates` from `Setup` twice and from
`ShouldBeCancelled` once), so a missing registration meant 3N thrown-and-swallowed exceptions per
election with nothing in the log.

- **Why missed:** the lazy-cache guidance in CLAUDE.md ("use a lazy cache" before an `IoC.Resolve` in a
  hot path) is about avoiding repeated *successful* resolves. Nobody asks what the cache does on
  failure.
- **Prevent:** keep a separate `_resolveAttempted` flag, or resolve everything once in an
  `EnsureServices()` guarded by a single bool. Fall through to vanilla on null.
- **Source:** `docs/reviews/rca-fiefgranting-2026-08-14.md` finding #3.

### Enumerate producers by grepping the constructor, not by tracing one path

A doc that says "both producers funnel through here" is a counted claim, and counted claims need the
cheap exhaustive check. Tracing forward from the siege path found two producers of
`SettlementClaimantDecision`; `grep -rn "new SettlementClaimantDecision("` found three in one command,
and the third (`KingdomManager.RelinquishSettlementOwnership`) had been asserted away in a patch
comment, a registry row and a feature doc.

- **Why missed:** the narrative trace answered the question that motivated it ("how does a captured
  fief get here"), and that felt like completeness. It was coverage of one scenario, not of the type.
- **Prevent:** before writing any "all N of X" claim about engine call sites, grep the constructor or
  the method name across the whole decompile. This is the Never Fabricate rule's "counts, IDs, names"
  clause applied to documentation, and it is one command.
- **Source:** `docs/reviews/rca-fiefgranting-2026-08-14.md` finding #5.

### Test the input that VIOLATES a guarantee, not the one that satisfies it

One changeset claimed four guarantees in prose (lift-only, idempotent, fail-loud, dry-run gated) and
tested each along the path where it holds. Every one of them was broken along the path where it is
stressed, and review found all four:

| Guarantee | Tested | Actually broken by |
|---|---|---|
| "lift-only, no fief is ever lowered" | a value below the floor | a value ABOVE the cap: `min(max(current, floor), CAP)` turned 6000 into 5600 |
| "a re-run reports 0 changes" | a re-run in DRY mode | a re-run with `--apply`, which overwrote the `.bak` with a byte-identical file and destroyed the previous run's rollback point |
| "exactly-once assertion, fail loud" | a well-formed settlement | `max-prosperity=` (a hyphen IS a word boundary), an attribute value containing the attribute name, and `</Settlement >` with a space. Each produced exactly one match, so the assertion passed while the wrong bytes were rewritten |
| "the gate reports clean or fails" | a violating value | a missing spec file, an empty spec, and a spec culture matching no settlement, all of which returned "no findings" |

- **Why missed:** verifying the hard part feels like verifying the change. The derivation was
  re-computed twice and was clean both times, which is exactly what made the ring around it feel
  covered. Confirming idempotency by dry run is the specific trap: the dry run never reaches the
  write path, so the write path's no-op behaviour is the one thing that check cannot see.
- **Prevent:** for each guarantee a change states in prose, write down the input that would violate
  it and assert the guarantee survives that input. "Lift-only" is a claim about values above the
  floor. "Idempotent" is a claim about the second WRITE. A guarantee with no violating-input test is
  a comment, not a contract. Where the shape is adversarial (regex boundaries, parser edge cases),
  ask an adversarial reviewer to construct counterexamples: four of the five nastiest defects here
  came from construction, not from rule-checking.
- **Source:** `docs/reviews/rca-faction-economy-2026-08-14.md` findings #2, #3, #4, #7 (#459).

### A constant shared by a writer and its verifier is IMPORTED, never restated

`rebalance_settlement_prosperity.py` clamps a configured floor to `PROSPERITY_CAP` (5600) and
`HEARTH_CAP` (825). The `SETTLEMENT_ECONOMY_FLOOR` check that verifies the writer's output read the
same spec file and compared against the raw value. Set a floor above a cap and the writer silently
produces 5600 while the checker demands 6000: an ERROR-severity gate in a pre-commit path that
fails forever, on every commit, with no `--apply` able to satisfy it. Latent at the time, because
the committed numbers happened to sit under both caps.

The same defect at a different scale, in the same changeset: a doc asserted the prefab entity cap
left "~921 spare" as a measurement, having copied it from CLAUDE.md's trap table. The independent
reviewer then contradicted it with 93,407 from `check_prefab_budget.py`, which CLAUDE.md's own trap
table says undercounts because it reads one module. Two parties quoted a stale or partial number as
a live measurement and reached opposite conclusions from it.

- **Why missed:** a copied constant is correct at the moment it is copied, and both copies read as
  right forever after. Nothing about the second one looks like a duplicate. `csharp-architecture.md`
  already carries this rule for a value settable from both JSON and MCM; it was not read as covering
  a writer and its verifier, which is the same shape.
- **Prevent:** one component imports the constant from the other, even across a language or tool
  boundary (the Python checker now execs the writer module for its caps). And a number quoted as a
  design CONSTRAINT carries the command that produced it and the date, or it is not a measurement.
  If two sources disagree, record the disagreement rather than picking the convenient one.
- **Source:** `docs/reviews/rca-faction-economy-2026-08-14.md` findings #1 and #12 (#459).


### A replay tool that defaults to one copy fixes one copy

`tools/register_one_handed_polearms.py` is the replay half of the unversioned-Armory trap: the
Armory ships to players but is not in the repo, so an edit there is reverted by any module refresh
and this tool re-applies it. On 2026-08-20 it ran once, against the game install, and the fix was
called done. On 2026-08-21 the assets-repo copy at `lotraom-assets/v1.4/LOTRLOME_Armory` turned out
to be missing the Black Numenorean lance's two pieces. It did hold the four Dale spears' seven,
under the older `TAOM-DALE-1H` marker that the #449 fix wrote and that this tool sweeps forward.

That copy is what a refresh restores FROM. So the replay tool had written only to the thing that
gets overwritten, and not to the thing that overwrites it. The gate would have reported the same
finding a fourth time and it would have looked like a regression rather than an incomplete fix.

- **Why missed:** the tool takes `--game-modules` as an override, which frames the game install as
  the location and anything else as a special case. It is not a special case; it is the authoritative
  one. A tool whose whole purpose is surviving a refresh should default to writing every copy, or at
  minimum report the copies it did not touch. Nothing in its output said "1 of 2".
- **The check that would have caught it:** resolve the affected item's primary usage in every copy,
  or diff each copy against its own HEAD. A block hash across copies is NOT the check, because a
  legacy-marker sweep legitimately makes two correct copies differ in text.
- **Generalise:** for any unversioned dependency module, "where does a refresh restore from" is a
  different question from "where does the game read from", and a replay tool has to answer the first.
- **Script and hand edit are both legitimate; the choice is per situation, not per policy.** A
  script is usually the easier call for a replayable registration like this one. Choosing a hand edit
  here was defensible in principle and wrong in fact, because the premise behind it was a misread. The tool prints "9 distinct piece(s) to register", which is the size of the block it
  rewrites, not a count of what was absent. Read as a count, it looked like running the tool would
  land four unrelated Dale spears in a repo Erkam commits to, so the block was hand-scoped down to the
  lance. The spears were already registered under the legacy marker, so the hand edit deleted a
  working registration rather than declining to add one.

### "N items to write" is not "N items missing"

A tool that rewrites a whole block reports the size of the block. Inferring absence from that number
is a guess wearing a tool's authority. Here it produced two wrong beliefs in a row: first that the
assets copy held none of the nine pieces, then that scoping the block by hand was preserving someone
else's state, when it was destroying it.

- **Why missed:** the number was specific, freshly printed, and came from the tool that owns the
  file, which is exactly the shape of evidence that does not get re-checked. Both later documents
  repeated it as fact.
- **The check:** `git diff` the file and read the actual +/- lines. Four added and two removed told
  the whole story instantly, including the legacy-marker rename that no amount of reading the tool's
  stdout would have revealed. For an untracked file, resolve the behaviour instead (here, the item's
  primary usage) rather than counting lines.
- **Generalise:** a tool's activity count describes what the tool did, never what the file lacked.

### `sed -i` is not a byte-safe hand edit on a CRLF file

Deleting seven lines from a CRLF-ended XSLT with `sed -i '708,718{/pat/d}'` produced the intended
seven-line content change AND silently converted the entire file to LF: 1483 CR bytes to **zero**,
1861 bytes smaller, every one of 1476 remaining lines rewritten. In a git-tracked shared repo that
turns a 7-line reviewable diff into a whole-file rewrite that buries the actual change.

- **Why missed at first:** the obvious check lies. `grep -c $'\r' <file>` returned a plausible 1477
  on the converted file, which read as "line endings preserved". It is counting the wrong thing.
  `tr -cd '\r' | wc -c` returned 0 and is the measure to trust. A plain `diff` reporting
  `1,1483c1,1476` was the real tell and was visible immediately.
- **Prevent:** hand edits are fine and often better than a script, but pick a hand-edit tool that
  does not normalise. The `Edit` tool, or the binary round-trip already mandated by
  `.claude/rules/moduledata-validation.md` (`open(p,"rb").read()` / `open(p,"wb").write()`), both
  preserve bytes. Verify with a plain `diff` afterwards: if it reports every line changed, the
  content edit is not what shipped.
- **Note the rule this does NOT support.** It is not an argument for "always use the generator". It
  is an argument for using an editor that respects the file's existing bytes.

### When two tools write the same XML list, bound the presence check by the container

`generate_black_numenorean_weapons.py` inserts registrations immediately before a template's
`<xsl:apply-templates>` passthrough, and checked whether a piece was already registered by searching
the span from the template's start to that passthrough. The span it inserts into, in other words.
When a second tool began appending a `TAOM-1H-POLEARM` marker block AFTER the passthrough, those
registrations were invisible to the check and the next `--apply` would have written all nine pieces a
second time into the same `<AvailablePieces>`.

- **Why missed:** the bug is dormant until a second writer exists, and it presents as the check
  working perfectly for months. It surfaced only because the two tools' outputs were compared
  directly, and even then it read as "the entries are missing" rather than "the check cannot see
  them". The dry run said `+2` about a file that already had both.
- **The rule:** scope a presence check to the semantic container (here the whole
  `</xsl:template>`-bounded body), never to your own insertion window. They coincide only while you
  are the only writer, which is an assumption no shared data file earns.
- **Related:** the same instinct that made the insertion bounded by `</xsl:template>` was correct and
  deliberate, recorded in a comment. The presence check was simply not revisited when the insertion
  bound was added.

### The binary round-trip I/O idiom is about the idiom, not the file type

`io.open(path, encoding='utf-8-sig')` writes a BOM unconditionally, and a default-newline read
flattens CRLF to LF. Used for a round-trip edit, it silently rewrites the encoding and line endings
of the whole file alongside your intended change.

- **Why missed:** `.claude/rules/moduledata-validation.md` already specifies binary round-trip, and
  it was read as scoped to ModuleData XML. During #461 the same idiom was used on markdown and C#
  and converted 11 tracked files from CRLF to LF while adding BOMs to files that had none. Git's
  autocrlf normalisation hid it from `git diff --stat`, which stayed proportional to the intended
  edits.
- **Prevent:** round-trip any tracked file as bytes, or verify afterwards. `tools/lint_docs.py`
  caught it by flagging an em dash on line 1 of a file that had never been edited, which is the tell
  that the BOM moved the line. `git ls-files --eol` gives the authoritative per-file index and
  worktree state; comparing a file against an untouched sibling in the same directory is the quick
  check.
- **Source:** `docs/reviews/rca-ai-party-size-2026-08-18.md` process finding.


### State the hard requirement BEFORE evaluating a tool, not after

The graphify v8 trial ran a full install, three extraction runs, and a mixed-corpus experiment before
the disqualifying fact surfaced: it does not read `.xml` or `.xslt`, which is where TAOM's content
actually lives. That fact was available in one grep of the upstream's `detect.py` on day one.

- **Why missed:** the evaluation was framed as "what can this tool do", so capabilities were
  enumerated and the gap was found by exhaustion. Framed instead as "here is the acceptance
  criterion, does it meet it", the first check would have been the extension list. The June 2026
  review made the same omission and reached the right answer for adjacent reasons, which is luck,
  not process.
- **Prevent:** write the acceptance criterion down before the first install, as a list of file types,
  modules, and questions the thing must answer. For TAOM that baseline is 1,057 XML and 16 XSLT
  across three modules (repo, `TAOM_Map`, `LOTRLOME_Armory`), two of which are live and unversioned.
  `/adopt-external`'s "map novel vs duplicative" phase should run against that criterion, not
  against the upstream's feature list.
- **Source:** `docs/reviews/adopt-graphify-v8-2026-08-18.md`.


### A tool wired into no hook and no CI decays silently, and the decay is invisible

`/doc-graph` shipped 2026-06-08 with a measured baseline and was never run again, because nothing
runs it. Ten weeks later the knowledge base had gone from 64 isolated docs to **153**, and from 70
components to **156**. No test failed, no hook fired, and no session noticed.

- **Why missed:** the June work treated shipping the tool as the deliverable. The tool was correct,
  documented, registered in the skill list, and cross-linked, so it looked complete. Nothing in the
  definition of done asked which recurring event would ever invoke it.
- **Prevent:** a diagnostic tool is not shipped until something calls it on a schedule. Either a
  hook, a CI job, or an explicit line in a periodic skill such as `/skill-stocktake`. A ratchet on
  the metric (fail if orphans exceed the committed baseline) turns a report nobody reads into a gate
  that speaks. Grep for the tool's own name outside `docs/` as the check: zero hits in `.claude/` and
  `.github/` means nothing will ever run it.
- **Source:** `docs/reviews/adopt-graphify-v8-2026-08-18.md`.

### A gate sitting in an unmerged PR is not a gate
PR #447 was opened on 2026-08-10 with `tools/audit_polearm_shield_parity.py`, a detector for one defect class: a roster pairing a shield with a weapon whose primary usage is flagged `requires_no_shield`. It was complete, tested, and green. It then sat unmerged for ten days. On 2026-08-18 `2fcbef10` shipped the Black Numenorean cavalry with exactly that defect on 8 rosters, and the first time the gate ran against trunk, on merge day, it caught them. The detector existed the whole time and prevented nothing, because nothing ran it.
- **Why missed:** the PR was not blocked, contested, or forgotten in any visible way. It was green with zero review comments, which reads as healthy rather than stalled, and its own CI was passing, which reads as "covered". The measure that would have shown the problem is the one nobody looks at: a gate's value is delivered on the branch everyone else commits to, so a gate's age as an open PR is a live risk, not administrative lag. The 40-commits-behind number was visible on the PR page the entire time.
- **Prevent:** when a change adds a **detector** rather than a fix, merging it is the deliverable and it should jump the queue ahead of the fix it shipped with. Merge the gate first, tolerate it red if need be, rather than holding it until everything it reports is clean. A corollary for review: run a newly added gate against current `origin/bannerlord-1.4.5`, not against the branch's merge base, before believing its findings list is complete. This one went from "28 Dale findings" at merge base to "8 findings, none of them Dale" at trunk, and both numbers were correct for what they measured.
- **Sibling entries:** the same shape as the doc-graph ratchet (2026-08-18, `2a15afaa`): `/doc-graph` shipped 2026-06-08 and was run by no hook and no CI job, so isolated docs grew 64 to 153 over ten weeks with every unit test green, because the tests checked the algorithm against synthetic fixtures and never read the real tree. Written-but-unrun and merged-but-unwired are the same failure with different plumbing.
- **Source:** 2026-08-20, merging PR #447.

### A generator's output is unverified until something diffs it against its input

The localization-registration extractor stopped at the first placeholder brace; 20 registered rows
shipped truncated into the strings XML and all 12 language files, silently, because English still
rendered from the inline default and "39 keys registered" looked complete. A matching COUNT is not
verification. **Prevent:** every generated registration gets a mechanical round-trip gate (the
registered text must equal the code default), living in the test suite, not in the generator.

**Source:** docs/reviews/rca-yotthani-camps-2026-08-23.md Class 4 (orchestrator-inflicted HIGH).

### A wiring gate must exercise the wiring; a source-text scan only pins the idiom (camps port, 2026-08-23)

The batch-1 contributor-ordering fix satisfied both of its own preventive gates (source scans for
eager container.Resolve) while regressing into a DryIoc construction cycle that would have killed
the module at startup: CampService materialized its contributor collection, Refuge's contributor
needed IRefugeService, RefugeService needed ICampService back. Nothing in 7,400 tests resolved
the finished container. The honest gate is DryIoc Validate over the cross-feature graph
(CampsContainerWiringTests, the EnlistmentContainerWiringTests shape), which reproduced
Error.RecursiveDependencyDetected before the lazy-injection fix and pins it after. When a defect
class is "the graph is broken", the gate must build the graph.

### A module folder cannot bindingRedirect; vendored framework shims must match their consumers' exact binds (deps, 2026-08-23)

.NET Framework binds strong-named assemblies to the EXACT version their consumer's metadata names,
and a Bannerlord module folder has no app.config to redirect. TAOM.Dependencies vendored
System.Memory 4.0.1.1 (binds Unsafe 4.0.4.1) while the csproj pinned the Unsafe PACKAGE at 6.0.0
(assembly 6.0.0.0): ButterLib died in System.Memory's cctor on the first Trace.WriteLine, every
tick, and the visible symptom was two layers away (MCM's Mod Options hanging). Two mechanics to
remember: (1) nuget package versions are NOT assembly versions (package 4.5.3 = assembly 4.0.4.1;
package 6.0.0 = 6.0.0.0) - derive the required pin from the consumer's reference metadata, never
from "latest"; (2) build outputs overwrite vendored module files at deploy, so a wrong package pin
silently clobbers a correct vendored DLL forever - DependenciesPairingTests now pins both sides.

### Prove WHICH build a log came from before reading anything into it, and search the DLL correctly

A log is evidence about the binary that produced it, not about your working tree. Twice in one
session the answer turned on this: a log at 09:07 ran a DLL deployed 09:05 and therefore DID carry
the fix, while a log at 11:02 ran one deployed at 09:05 because the next deploy landed at 11:16, one
minute after that session ended. Reading either as "the fix did not work" would have been wrong.

The check is two timestamps and one content probe: the deployed DLL's mtime under
`<game>/Modules/TAOM/bin/Win64_Shipping_Client/`, the log's start time, and a search of the DLL for a
symbol only the new code has.

**The content probe has a trap that will silently mislead you.** Method and type names live in the
metadata heaps as UTF-8, so `b'MyNewMethod' in dll_bytes` works. **String literals live in the #US
heap as UTF-16LE**, so the same search for a log message returns MISSING for a DLL that definitely
contains it. Searching four names and two literals produced "2 MISSING" and a wrong conclusion until
the literals were re-searched as `s.encode('utf-16-le')`, which found all of them.

**Why missed:** the first probe used method names and worked, so the technique looked validated. The
second probe mixed names and literals without noticing they are stored differently.

**Prevent:** probe with a METHOD or TYPE name (UTF-8) as the primary check. If you must probe a
string literal, encode it `utf-16-le`. And when a build script reports failure while the game is
running, read which STEP failed: `dotnet build` returning `0 Error(s)` with `build.ps1` still failing
is the module-copy step hitting a file lock, which means the compile is fine and the deploy did not
happen, so the running game keeps the OLD binary.

**Source:** enlistment #512 follow-up, 2026-08-25, two log sessions and one blocked deploy.

### A lessons record that is only appended to is a write-only log; put the read where the work happens
TAOM's review pipeline reliably APPENDS lessons after every RCA and reliably fails to READ them before the next feature in the same subsystem. On 2026-08-26 the UncapturableHeroes patches shipped two defects into review that were already written in `lessons/harmony-il.md`, one of them landed six days earlier: a defer-on-error catch handing a hero back to vanilla capture after the patch had already made him a fugitive (the existing lesson's worked example is the same actor in the same state, from #486), and a `false`-returning prefix that dropped a safety gate its sibling seam applied. Both were then found by an adversarial Codex pass, at far greater cost than reading one file would have been.
- **Why missed:** the instruction to read the category file lives in CLAUDE.md, which is always loaded and therefore general. Generic always-on instructions lose to specific in-the-moment ones. Nothing connected "I am about to write a Harmony patch" to "there is a file of Harmony patch traps", so the file was opened only at the end, to append to.
- **Prevent:** put the read instruction in the PATH-SCOPED rule that auto-loads for the subsystem, not only in CLAUDE.md, and make it cite a concrete incident so it does not read as boilerplate. Done for `.claude/rules/harmony-patches.md` → `lessons/harmony-il.md`. The same wiring is owed for the other category files whose subsystems have a path-scoped rule (`gamemodels.md`, `adapters.md`, `xslt.md`, `native-cpp-ports.md`). General rule: when a process step is skipped repeatedly, do not restate it louder, move it to where the work happens.
- **Source:** docs/reviews/rca-uncapturable-heroes-2026-08-26.md, "The finding that matters most".
### A transform that changes text length invalidates every offset you recorded before it

Masking regions you must not touch (XML comments, CDATA, code fences) by recording their byte
spans and writing them back at those spans is correct only while the text length is fixed. On
2026-08-28 an item-id swap did exactly this: comments were blanked in place, the swap replaced
ids with longer ones, and restoration then spliced a `GOLASGIL` comment into the middle of an
item id and left NUL bytes behind. Eight ModuleData files went out malformed.
- **Why missed:** the masking and the restoring were written together and read as obviously
  symmetric. The asymmetry is introduced by the code BETWEEN them, which was added later. Unit
  tests covered swaps and comments separately, and both passed, because neither exercised a
  replacement of a different length next to a comment.
- **Prevent:** restore by TOKEN, not by offset. Substitute each masked region for an indexed
  private-use sentinel that cannot occur in XML and matches no pattern, transform freely, then
  restore with a regex on the sentinel. Length-independent and order-independent. Then add the
  backstop that makes the class unshippable: **parse the transformed document and refuse to write
  it if it no longer parses.** A data-mutating script that cannot detect it has produced garbage
  will ship garbage.
- **Source:** docs/features/armoury-mesh-cleanup.md; tools/apply_dead_mesh_item_swaps.py.

### Revert per file, never per directory, in a tree another session is working in

Undoing a bad write with a directory-wide `git checkout` reverts everything uncommitted under it,
including files you never touched. On 2026-08-28 that took a concurrent session's four new
war-ram troops in troops_erebor.xml along with the eight files being repaired. It was recoverable
only because that session had a generator script and could re-run it.
- **Why missed:** the mental model was "undo my change", but the command's scope is "the
  directory", and in a multi-session tree those are not the same set. The corrupted files were 8
  of the 9 modified paths, so a directory revert felt equivalent and was not.
- **Prevent:** name the paths, using the exact list the tool reported writing. Before any revert
  wider than one file, run `git status --porcelain <dir>` and diff that against your own
  written-file list; anything in the first and not the second belongs to someone else. The
  stronger fix is upstream: a script that refuses to write malformed output never creates the mess
  a wide revert is reaching for.
- **Source:** docs/features/armoury-mesh-cleanup.md; CLAUDE.md "Multi-session git safety".

### A zero you did not prove is not a zero: LFS pointers and pipeline exit codes both fake one

Two independent silent-zero failures in one session. (1) Scanning the pre-image of every deleted
`.tpac` with `git show` returned a 128-byte Git LFS pointer, not the pack, so the TOC scan found
no meshes and the corroboration step reported "0 names recovered, 0 agreeing" as though the
deleted set were empty. (2) A peer concluded `validate_mesh_refs.py` "exits 0 even with 10 errors,
so it is a report and not a gate" after running it piped through `tail`; the shell reports the
LAST command in a pipeline, and the tool actually returns 1.
- **Why missed:** both produce the shape of a clean result. A zero count and a zero exit code are
  exactly what success looks like, so neither prompts the question that would expose it.
- **Prevent:** for LFS-tracked blobs use `git cat-file --filters`, which applies the smudge
  filter, and explicitly detect the pointer header so an unresolvable object is COUNTED and
  reported rather than skipped. For exit codes, never read the status after a pipe: redirect to a
  file, or read `PIPESTATUS`. Generally: when a check reports nothing wrong, confirm it actually
  ran before believing it.
- **Source:** docs/features/armoury-mesh-cleanup.md; tools/audit_deleted_mesh_impact.py.
### Report what a transform DID, never what it was asked to do

A mutation script logged its insert counts from the request constants (`len(NEW_PIECE_BLOCKS)`)
while logging its removal counts from actual matches. The asymmetry meant a no-op second run
printed `+4 pieces` against a byte-identical file, and a silent insert failure (anchor tag not
found, function returns the text unchanged) would have printed `+4` just the same.
- **Why missed:** the removal path was written to count matches because the count was interesting
  (11 of 12 requested ids existed). The insert path was written from the constants because they
  were in lexical scope and the number "looked right" on the first run, when it happened to be
  true. No test asserted an insert count, so nothing distinguished 4-because-it-worked from
  4-because-it-is-a-literal.
- **Prevent:** every mutating helper returns `(text, count)` and the caller prints that count.
  Pin it with a test that runs the transform twice and asserts the second run reports 0. This is
  the write-side of the same rule as "a zero you did not prove is not a zero": a non-zero you did
  not measure is not a non-zero either.
- **Source:** docs/reviews/rca-rohan-spear-reforge-2026-08-28.md finding 2.

### A heredoc is not a safe transport for source containing escape sequences

Twice in one session, Python string literals containing `\n` were appended to a file through a
Bash heredoc and arrived as real newlines, breaking the file's syntax both times. The second
occurrence came after the first had already been diagnosed and fixed by hand.
- **Why missed:** the quoted-heredoc form (`<<'EOF'`) genuinely does suppress shell expansion, so
  the technique looks safe and works for prose. It is the tool-layer wrapping around the command,
  not the shell, that rewrites the escapes, and that is invisible from the script's own text.
- **Prevent:** author any file containing backslash escapes with the Write/Edit tools. Reserve
  heredocs for data with no backslashes. If a heredoc-written file is source, parse it immediately
  (`python -c "import ast; ast.parse(open(p).read())"`) before trusting it.
- **Source:** docs/reviews/rca-rohan-spear-reforge-2026-08-28.md, "Process lesson".

### The tool you called decides the dialect and the escaping, and it never tells you when you got it wrong

The heredoc lesson above fired three more times on 2026-08-29, each time writing real newlines into
C# string literals and producing `error CS1010: Newline in constant`. In the same session a
PowerShell here-string (`@'...'@`) was handed to the Bash tool: the first line became the entire
commit subject, prefixed with a stray `@`, and every following line ran as a shell command. The
commit still succeeded. Only reading `git log -1` back afterwards showed it.
- **Why missed:** two pressures point the wrong way. A standing instruction to prefer Bash for file
  changes pulls toward heredocs, and the two shells' here-doc and here-string forms look
  interchangeable while sharing no syntax. Neither failure is in the shell: one is the tool layer
  rewriting escapes, the other is the wrong shell entirely, and both are invisible from the text you
  wrote.
- **Prevent:** author anything containing a backslash escape or a quote with Write/Edit, never a
  heredoc. `<<'EOF'` is bash only and `@'...'@` is PowerShell only, so the dialect follows the tool
  you invoked, not the machine you are on. For a commit message use `git commit -F -` with a quoted
  heredoc that carries no escapes, then read `git log -1` back before doing anything else. A mangled
  commit message is cheap to amend and expensive to notice.
- **Source:** the 2026-08-29 lord identity and placeholder rename pass.

### Your uncommitted edits can vanish with no rebase, no error, and a clean `git status`

Mid-turn, a concurrent session restored two `characters/*.xml` files to HEAD. Exactly that turn's
four edits disappeared; everything from earlier turns survived because it had already been
committed. The stylesheets and the registry were untouched, so the data was left saying one name in
one file and a different name in another, which is the defect class the whole pass existed to
remove. `git status` showed the two files as clean, which reads as "nothing to do here", not "your
work is gone".
- **Why missed:** the existing rule asks for a marker re-check "after any rebase/stash event", and
  there was no rebase and no stash. The trigger is not the git operation, it is elapsed time in a
  tree somebody else is also writing to. A file reverting to HEAD produces no diff, no conflict and
  no message.
- **Prevent:** re-assert your own markers from disk before any completion claim, not only after a
  git event. The strongest form is a test that compares the files against each other rather than
  against your memory: here `EveryBiographyNamesTheLordItIsAttachedTo` and
  `EveryLordNameFallbackMatchesTheRegisteredEnglishText` both failed and named the exact id, turning
  a silent data inconsistency into a two-minute repair. Commit early in a shared tree; the edits
  that survived were the ones already in a commit.
- **Source:** the 2026-08-29 rename pass. The perpetrator side of the same event is "Revert per
  file, never per directory, in a tree another session is working in", above.

### A mutating script must resolve every anchor before it writes the first byte

Twice in one session a script performed several successful in-memory replacements, printed an `ok`
line for each, hit a failed assertion on the next anchor, and exited before the single `write()` at
the end. Nothing reached disk and the successful edits were lost, but the console showed a column of
`ok` lines, which reads as progress rather than as a rollback.
- **Why missed:** the success print sat next to the mutation instead of next to the write, so the
  output described intent, not effect. This is the same shape as "Report what a transform DID, never
  what it was asked to do", one level down.
- **Prevent:** resolve and assert every anchor first, mutate second, write third; or print nothing
  until after the write returns. A partial run that writes nothing is safe. A partial run that
  reports success is not, because the next thing you do is trust it.
- **Source:** the 2026-08-29 rename pass.

### A gate the harness kills is not a gate

`check-moduledata-validation.sh` runs a validator measured at 27.0s. A timeout pass registered it
at 5s. The harness kills a hook that overruns and **discards its output**, surfacing nothing, so
every ModuleData commit afterwards skipped the broken-ref / landless-culture / duplicate-id checks
and looked exactly like a clean pass. `check-doc-config-drift.sh` died the same way at 7.8s against
5s, while the tree carried drift it would have blocked on.
- **Why missed:** the timeouts were sized against each hook's fast path, which is the path nobody
  debugs, and the change was verified by "does it still return quickly" rather than "does it still
  deny". A killed gate returns quickly. That is the whole problem.
- **Prevent:** time the exact command the hook runs before choosing a number; bound external work
  *inside* the script under the registered timeout (`timeout -k 2 45 "$PY" ...`) so an overrun stays
  where it can still speak; treat rc 124 as `permissionDecision: "ask"`, never as a pass. Then prove
  the gate denies something. `tools/test_hooks.sh` fails the build on a missing timeout, a missing
  inner bound, or an inner bound not strictly below its registered timeout.
- **Source:** 2026-08-31, `docs/reviews/rca-hook-harness-hang-2026-08-31.md`. Sibling of "a gate
  sitting in an unmerged PR is not a gate": that one never ran, this one runs and is silenced.

### `command -v` answers "is it installed", never "is it safe to execute"

`python3` on this machine resolves to a Microsoft Store App Execution Alias that prints nothing,
never exits and ignores SIGTERM. `command -v python3` succeeds, because a file genuinely exists at
that path, so every guard written as `if command -v python3` selected the one binary that must
never run. With no timeouts registered, each Bash tool call paid a 600s PreToolUse batch plus a
600s PostToolUse batch: the 20.0-minute stalls in the overnight transcripts.
- **Why missed:** the guard tested the wrong proposition, and the sibling project's `json-lib.sh`
  tried to compensate by checking the candidate's *exit status*, which cannot help against something
  that never exits. Both guards were reasonable against "not installed" and useless against "hangs".
- **Prevent:** reject on the *resolved path* before executing (`case $p in *[Ww]indows[Aa]pps*)`),
  and bound any probe with `timeout -k 1 5`; bare `timeout` sends SIGTERM then waits, so it hangs on
  exactly the process it is guarding against. Outside a hook just write `python`, which resolves to
  real CPython here. `.github/workflows/` is the only place bare `python3` is correct, because CI is
  Linux.
- **Source:** 2026-08-31, same RCA.

### The doc that tells the next author how to do it is a load-bearing part of the system

`docs/reference/hooks-catalog.md` recorded on 2026-08-20 that jq was absent and then prescribed
"use the python3 fallback". Eleven days later that instruction had been followed into every
JSON-parsing hook, and the hooks wedged. The trap was written down before it was sprung, and the
doc was still recommending it when the repair started.
- **Why missed:** the fix pass treated the hooks as the defect and the doc as commentary. Repairing
  every call site while leaving the instruction intact means the next author reintroduces it, which
  is how this class survives a cleanup.
- **Prevent:** when a defect class is found, grep for the *advice* that produced it, not only the
  code. If a doc, rule, or SKILL.md prescribes the broken pattern, that edit is part of the fix and
  belongs in the same commit. Three of the six hang sites here were inside skills, and one was a
  prose line in `skill-stocktake/SKILL.md` that exists specifically to be copied.
- **Source:** 2026-08-31, same RCA.

### A repo with core.autocrlf and no .gitattributes is one checkout from a dead harness

Every hook script was LF on disk only because tools had written them directly. `core.autocrlf=true`
with no `.gitattributes` meant one `git checkout -- .claude/hooks/`, or any fresh clone, would write
CRLF, and a `.sh` with CRLF dies at the shebang. All 28 hooks would fail open at once, silently:
every gate off, no error anywhere. `git diff` had been printing "LF will be replaced by CRLF the
next time Git touches it" on all 15 modified hooks the whole time.
- **Why missed:** the warning appears on unrelated diffs constantly and reads as noise, and nothing
  had triggered a checkout of those paths yet. The failure is prospective, so no symptom existed.
- **Prevent:** `*.sh text eol=lf` in `.gitattributes`. Verify with `git ls-files --eol '*.sh'`,
  wanting `i/lf w/lf` on every row. Scope it to `*.sh`: normalising `*.py` would have rewritten 72
  CRLF files for no benefit, which is churn a hook fix has no business causing.
- **Source:** 2026-08-31, same RCA.

### A gate registered on one tool name has a hole the size of every other tool

Every PreToolUse safety hook here matches `Bash`, and `config-protection.sh` matches
`Edit|Write`. Nothing matched `mcp__*`, so `mcp__git__git_commit`, `git_reset`, `git_checkout`,
`git_add` and `mcp__filesystem__write_file` walked past the force-push block, the
CHANGELOG-staged gate, the `.claude/`-tracked-files gate, the ModuleData ref gate and the
settings/ADR protection simultaneously. CLAUDE.md's own MCP Usage Guide routed git work to
those tools, so the documented happy path was the bypass.
- **Why missed:** the gates were written when Bash was the only way to run git, and the MCP
  servers arrived later. Nobody re-asked "what else can perform this action now?" A matcher is
  a claim about the set of routes to an effect, and that set grows silently as tools are added.
- **Prevent:** when a hook guards an EFFECT (a commit, a file write, a push), enumerate every
  tool that can produce it, not just the one you had in mind. Deny the alternatives you do not
  intend to guard: nine write tools are now in `permissions.deny` in `.claude/settings.local.json` (untracked since WP142; the git and filesystem servers left `.mcp.json` with WP116),
  and the read-only ones are untouched.
- **Source:** 2026-08-31 audit, `docs/reviews/rca-hook-harness-hang-2026-08-31.md`.

### A failing advisory step silences every real gate behind it in the same CI job

The doc-graph ratchet went over baseline on 2026-08-28 and CI failed 42 runs straight. GitHub
Actions stops a job at the first failed step, so the two steps after it never ran again: the
native-CRT check (skipped silently since 2026-08-18, thirteen days) and the hook-harness gate
added the same day to prevent this class of thing, which executed zero times.
- **Why missed:** the job was red for a known, tolerated reason, so "CI is failing" stopped
  carrying information. A single red step reads as one problem, not as everything downstream of
  it being switched off.
- **Prevent:** a gate that must always run gets its OWN job, not a step behind something that
  can fail for unrelated reasons. Order steps so advisory checks come last. When a CI job has
  been red for more than a day, read which steps SKIPPED, not just which one failed.
- **Source:** 2026-08-31 audit, same RCA.

### Rebinding sys.stdout at import time breaks every test that imports the module

`tools/translate_with_claude.py` did `sys.stdout = io.TextIOWrapper(sys.stdout.buffer, ...)` at
module scope to force UTF-8 on a cp1252 console. Under pytest, `sys.stdout` is the capture
object: wrapping its `.buffer` and rebinding left pytest holding a stream it could not read, so
every module importing this one died during collection with `ValueError: I/O operation on closed
file`. Three test files were silently absent from every whole-directory run, and the
whole-directory run had never completed at all.
- **Why missed:** the three files passed when run individually, which is how anyone would check
  them, and the directory-level failure looked like a broken pytest install rather than a
  project bug. Nothing counts tests, so 36 missing ones raised no alarm.
- **Prevent:** `sys.stdout.reconfigure(encoding=...)` mutates in place and leaves the capture
  machinery's handle valid; never rebind a standard stream at import time. More generally,
  import-time side effects on process-global state are a test-harness hazard: put them behind
  `if __name__ == "__main__"` or make them idempotent and in-place. Watch the test COUNT, not
  just the pass/fail: 748 in one run against "712 across per-file runs" is the tell.
- **Source:** 2026-08-31 audit, same RCA.

### Proving a gate fires once is not proving it fires

The force-push gate was repaired, demonstrated to block `git push --force origin <trunk>`, and
reported as alive. It still let five ordinary forms through silently: `git push origin +branch`
(a leading `+` IS force, with no flag on the line), `--force origin HEAD`, `git -C <dir> push`,
`refs/heads/master`, and `HEAD:master`. The detector keyed on the substring "git push", which
`git -C ... push` does not contain, and never normalised the refspec.
- **Why missed:** the verification reused the payload that had demonstrated the bug. That proves
  the specific defect is gone, not that the gate covers its input space. For a security gate the
  interesting inputs are the ones nobody thought of.
- **Prevent:** verify a gate against a TABLE of invocations, must-block and must-allow, derived
  from the tool's actual syntax rather than from the bug report. `git push --help` lists `+refspec`
  and `src:dst`; both were in the manual the whole time. 15 block cases and 6 allow cases are now
  pinned in the repair.
- **Source:** 2026-08-31 audit, same RCA.

### A backup convention with no sweep is a shipping defect with a long fuse

Every tool under `tools/` writes a `.bak` sidecar before a destructive edit, which is the documented
XML I/O convention working as designed. Nothing ever removed them from the live module folders, so
they accumulated for months and shipped to players: 781 files, 937 MB across the three modules, of
which 769 MB was a single Modding Kit `SceneObj\Backups` tree in `TAOM_Map`. The trigger for finding
it was unrelated to correctness, it was that `.bak` breaks the Cloudflare distribution.
- **Why missed:** the convention doc specified how to CREATE a backup and said nothing about who
  removes it. A create-without-a-matching-destroy rule has no natural point of failure, so it never
  produced a symptom until an external constraint made the accumulation visible.
- **Prevent:** when a convention writes files into a shipped tree, the same doc names the sweeper
  and a gate runs it. `tools/sweep_module_backups.ps1` now exists and `/release` Phase 2 fails unless
  it reports zero. `docs/reference/module-backup-sweep.md`.
- **Source:** 2026-09-01 module backup sweep.

### The obvious glob for a file convention matched 3% of the files it governs

Cleaning up `.bak` sidecars looks like a one-line `*.bak` glob. It matched **18 of 658**. The tools
that write them use dated, topic-tagged suffixes (260 `.bak-armoryloc`, 85 `.bak-guidremap`, 57
`.bak-preskel`), a shape the convention doc mentions in passing and `.gitignore` had already been
widened to `*.bak*` to handle, years of sidecars ago.
- **Why missed:** the canonical example in the doc is bare `.bak`, so that is the shape that comes
  to mind. The real distribution lived only in the filesystem.
- **Prevent:** before writing a matcher for a file convention, histogram the actual names on disk.
  One `Group-Object` over the extension would have shown 57 distinct suffixes in a second.
- **Source:** 2026-09-01 module backup sweep.

### A backup whose live sibling is gone is not a backup, it is the only copy

Sweeping every file with a backup suffix would have moved
`sk_spider_forest_c_geo.tpac.backup` out of the spider's mesh folder, which holds no `sk_spider_forest_c_geo.tpac`
and never did. The spider's `spider_skeleton` resource survived only in that file, as a June 2026
entry records. Two more files were in the same state.
- **Why missed:** "it has a backup extension" reads as "something else holds the real version". That
  is an assumption about a sibling file, and it is checkable in one line.
- **Prevent:** any bulk operation keyed on a naming convention should verify the thing the
  convention implies. The sweeper strips the suffix, tests for the live sibling, reports those
  separately, and aborts on `-Apply` if more turn up than expected.
- **Source:** 2026-09-01 module backup sweep.

### `Measure-Object` emits nothing for an empty pipeline, and StrictMode turns that into a crash

`($items | Measure-Object Bytes -Sum).Sum` works for every non-empty run and throws
`The property 'Sum' cannot be found on this object` when the collection is empty, because
Measure-Object returns no object at all rather than one with `Sum = 0`. Under `Set-StrictMode` that
is a terminating error. The sweeper hit it on its own success case: the clean-tree run, which is
exactly the run the release gate performs every time.
- **Why missed:** the empty case only becomes reachable after the tool has done its job once, so it
  cannot appear during development against a dirty tree.
- **Prevent:** for a tool whose whole purpose is to empty something, test the emptied state as a
  first-class case. Guard aggregates on `.Count`, not on the aggregate's own result.
- **Source:** 2026-09-01 module backup sweep.

### Cleaning the deployed copy while the source keeps redeploying it

The backup sweep emptied all three installed modules, verified zero remaining, and passed every
gate. Twenty minutes later nine sidecars were back in `<game>\Modules\TAOM\`, carrying their
ORIGINAL timestamps, which is the tell that they were copied rather than recreated. The source is
`Main/_Module/ModuleData/` in the repo: `TAOM.csproj`'s `CopyModule` target "recurses `_Module`
verbatim and deploys whatever it finds", its own comment, written directly above the guard that
already stops a `.vs` folder reaching the install. Two mechanisms were hiding the same nine files
in opposite directions: `.gitignore`'s `*.bak*` keeps them out of `git status`, and the build keeps
putting them back.
- **Why missed:** the task was framed as "clean the modules", and the modules are a place in the
  game install. Nobody asked what writes to that place.
- **Prevent:** before cleaning a deployed tree, find its deploy step and check whether the source
  carries the same garbage. A preserved mtime on a file you just deleted means copy, not create.
- **Source:** 2026-09-01 module backup sweep.

### Repeating the exact flaw you just diagnosed, one scan earlier

The sweep's headline finding was that a bare `*.bak` glob matches almost none of the sidecars our
tools write, because the dated `.bak-<topic>-<date>` form dominates. The same session had already
scanned the repo for backups using `\.bak\d*$`, reported "the repo is already clean", and put that
in the CHANGELOG. That regex has the identical blind spot: it matches `.bak` and `.bak2` and none
of the nine dated files sitting in `Main/_Module/ModuleData/`.
- **Why missed:** the repo scan happened first, as a quick precondition check, and was never
  revisited once the real suffix distribution was understood. An early cheap check gets grandfathered
  in as established fact.
- **Prevent:** when an investigation revises its understanding of a pattern, re-run the earlier
  checks that used the old one. Treat a precondition established before the key insight as
  provisional, not settled.
- **Source:** 2026-09-01 module backup sweep.


### Mirror a validator's PREDICATE across languages, never an equivalent-looking pattern

`Invoke-CommitMatrix.ps1` validated station labels with `^[A-Za-z0-9_.:-]+$`, documented as "the SAME
grammar" as `MemoryProbeReportFormatter.IsValidLabel`. It is not: in .NET regex `$` matches at the
end of the string *or immediately before a single trailing newline*, so `"B-menu\n"` passed the
PowerShell check and failed the C# one. The newline then injected a physical line break into
`stations.csv`, which any line-based reader sees as an extra row. The mirror also diverged the other
way: `char.IsLetterOrDigit` is Unicode-aware while `[A-Za-z0-9]` is not.
- **Why missed:** the regex encodes the same *intent* and reads as obviously equivalent. The
  divergence lives in anchor semantics nobody re-derives when writing a character class.
- **Prevent:** when a validator exists on both sides of a language boundary, port the predicate
  literally (`foreach char: IsLetterOrDigit or in the allowed set`), not a pattern that means the
  same thing to a human. Same family as the prior C#/Python integer-floor threshold drift: pin a
  boundary INPUT on both sides, and for validators include an input that is valid-but-for-one-
  trailing-control-character.
- **Source:** deep review of the memory-diagnostics changeset, 2026-09-01; RCA `docs/reviews/rca-memory-diagnostics-2026-09-01.md`.

### Check a name-family heuristic against the whole id inventory before it picks anything

`fix_upgrade_armour_regressions.family()` strips trailing tier and variant tokens so a troop steps up its own item family. The token list was written from the Armory ids in front of the author (Rhun, Dol Guldur, Dale, Gundabad) and never run over the rest. Rivendell and Lindon name their items `rivendell_helmet_archer_tier1_silver`; `tier1` and `silver` were not tokens, the family collapsed to the whole id, no candidate matched, and the fallback dressed two archer troops in the parent's cavalry helmet. The values were right, the identity was wrong, and the dry-run table showed only values.
- **Why missed:** the heuristic was judged on the ids it had been written from. Nothing counted how many live ids it failed to reduce, and the review table printed old and new VALUES, not whether old and new shared a family.
- **Prevent:** before a family or prefix heuristic is allowed to choose, run it over every id in every culture it will touch and print the ids it leaves unchanged; each one is either a token to add or a mesh with no family. Print the family of old and new side by side in the plan table, so a cross-family swap is visible before `--apply`.
- **Source:** deep review of #541, `docs/reviews/rca-troop-tier-ladders-2026-09-04.md`.

### A ratchet entry must pin the measured VALUE, and a gate that inspected nothing must fail
Two defects in one new validator, both found by the tooling-correctness agent with working
reproductions. (1) The baseline suppressed by identity (`slot|path`) and not by measurement, so
re-cutting a baselined audio clip from 4.11 s to 8.77 s kept the gate green: an exemption granted
for a specific measured state silently covered every future state of that file. (2) File discovery
was a glob and there was no floor on how much got inspected, so renaming three of four input files
took the run from 379 checks to 53 with no signal, and a structurally empty input reported
"OK". Pin the value the exemption was granted at and re-verify it every run; drive discovery off
the authoritative registration (here `project.mbproj`) rather than a glob; and make a
zero-measurement run a hard failure.
- **Why missed:** the gate was written to catch the bug that had just been fixed, so it was only
  ever tested against that bug. Every hole appears the moment the question changes from "does this
  catch my bug?" to "what input passes this check while still being broken?". `UPGRADE_INDEX_EMPTY`
  in `.claude/rules/moduledata-validation.md` had already recorded the zero-coverage half of this
  after the identical mistake elsewhere, and was not consulted when writing a new gate.
- **Prevent:** when adding a validator, write its tests as adversarial inputs BEFORE calling it
  done, and name each test for the false PASS it closes. Doing that here immediately caught a
  fourth defect: the new zero-coverage floor returned exit 2 before findings were printed, so a
  file whose every binding was a finding reported as "bad input" and hid them. Findings must take
  precedence over a coverage floor. Read the sibling gates' rules before writing a new one; this
  repo has already paid for most of these lessons.
- **Source:** docs/reviews/rca-dwarf-voices-2026-09-06.md (2026-09-06 deep review, 4 demonstrated false PASSes)

### Fixing a convergence bug in one solver means re-asking the question of its siblings
When a data-mutating tool computes a value from the value already on disk, it is only idempotent if
the computation is a fixed point. Fixing that in one function does not fix it in the function next to
it, and a test named `test_is_idempotent` can pass on a fixture that never triggers the drift.
- **Why missed:** `rebalance_template_power.py` shipped with `solve_flat` deliberately rewritten to
  converge from any starting value, after `gundabad_raiders_boss_party_template` oscillated between
  18 and 19 troops per stack on alternate runs. Its sibling `solve_band` was never re-examined. It is
  genuinely not a fixed point (feeding its output back as `shape` drifts the mins by one), and is safe
  in production only because `rewrite_text` always hands it a constant lookup table keyed on troop
  role. The existing test asserted the general property using numbers that happen not to expose it.
- **Prevent:** after fixing a fixed-point bug, run every sibling solver's output back through itself
  at production budgets and assert equality. Where a caller-side invariant is what makes an unsafe
  function safe, pin the INVARIANT (the shape never comes from the file) rather than the symptom, and
  add a test asserting the warning docstring still says so, so the warning cannot be tidied away.
  Related: a tool that skips work must not exit 0, or a harness reads "silently skipped N templates"
  as a clean run.
- **Source:** docs/reviews/rca-caravan-bandit-parity-2026-09-06.md findings 5 and 6.

### A number in prose is measured from the artifact it describes, with the definition the prose uses
Three counts shipped wrong in one commit and its issue: "218 value settings" (217; the regex that
produced it included the one `[SettingPropertyButton]`, which the test being described skips),
"18 tests deleted" (16; relayed from an exploration agent's report, never counted), and a doc table
that still said `default 14` fifteen lines above the table the same commit updated to 7. None changed
behaviour. All three passed five review agents; a sixth agent whose only job was re-deriving every
number from the committed files caught them in one pass.
- **Why missed:** each number was produced once, from a source other than the artifact the sentence
  described (a looser regex, a subagent, the section being rewritten rather than the whole file), and
  then copied into sibling artifacts, so the copies agreed with each other and looked verified.
- **Prevent:** before a count or a default goes into a CHANGELOG, doc, issue or commit message,
  measure it from the committed artifact using the same filter the code or test uses, and grep every
  artifact that will carry it for the OLD value. A subagent's number is a claim; count it yourself.
  When a review matters, add a numeric-claims agent whose brief is only "re-derive every number".
- **Source:** #559 deep-review second pass, 2026-09-11, `docs/reviews/rca-bandit-scaling-mcm-2026-09-11.md` findings 3 to 5.

### An absolute in prose (never, nothing, all, every) is a claim about every path; name the exception first
Two shipped in one doc: "on every real install the file was never read" (the old provider constructor
loaded it, and its one JSON-only field applied; only the six MCM-shadowed values were dead) and
"switching scaling off changes nothing about a campaign already in progress" (vanilla's 2-party
infestation minimum comes back live, so one-party camps stop counting; what holds is that nothing
deletes a party or culls a hideout). Both survived two six-agent passes; Codex found both by starting
from the engine and reading the prose last.
- **Why missed:** each sentence was written from one true finding and generalised to the whole
  mechanism. Every reviewer checked the sentence against the mechanism it named and nobody asked what
  else the word "never" or "nothing" was excluding.
- **Prevent:** before writing an absolute, list the paths it covers and check the one you did not
  test. If an exception exists, write the qualified sentence; "X is dead, Y still applies, Y is retired
  by decision" is longer and true.
- **Source:** #559 Codex pass, 2026-09-11, `docs/reviews/rca-bandit-scaling-mcm-2026-09-11.md` findings 8 and 10.

### A parser window is bounded by the element it parses, not by the next thing that usually follows
`scene_position` in `tools/add_map_villages.py` matched `<game_entity name="X"[^>]*>.*?<transform` under
`re.DOTALL`. Every entity in today's 12 MB scene carries a transform right after its tags, so the nine
villages resolved correctly and the tool shipped; an entity saved without one would have taken its
neighbour's coordinates silently.
- **Why missed:** the "missing entity" test used an entity absent from the whole snippet. The adjacent
  case, present but incomplete, was never written because the author reasoned from the file's observed
  shape rather than from what the regex permits.
- **Prevent:** cut the window first (`find("<game_entity", open_tag.end())` or the element's close
  tag), then search inside it. Pin with two tests: the element present without the field, followed by a
  neighbour that has it; and the field before a child that also has it.
- **Source:** #562 deep-review, 2026-09-11, `docs/reviews/rca-map-villages-tool-2026-09-11.md` finding 1.

### A repair tool's "nothing to do" is computed over every file it writes, and idempotency is tested with the secondary file broken
`add_map_villages.py --apply` planned its 12 loc rows from the ids missing in the master and returned
"already present" on that list alone, so a master-present/loc-missing state (a run that died on
language 7 of 12, a reverted file) was unrepairable by re-run while `--check` kept failing on it.
- **Why missed:** idempotency was tested per master id; the precedent tool had no secondary files, so
  the copied shape had no place for the question.
- **Prevent:** when a tool writes N files, derive each file's own to-do list from the full table and
  short-circuit only when all N are complete. Test it by breaking a secondary file on a scratch copy
  (`BANNERLORD_GAME_DIR` pointed at a copied module tree) and asserting the re-run writes exactly that
  file, with an honest per-file count in the summary.
- **Source:** #562 deep-review, 2026-09-11, `docs/reviews/rca-map-villages-tool-2026-09-11.md` findings 2 and 3.

### A worktree-sourced blob is whatever the worktree holds at commit time, not at the time you checked it

Three sessions in one tree. I ran `git status --porcelain` on two shared docs, saw them clean, edited
them, and minutes later committed them from the worktree through a temporary index. In between,
another session edited the same two files, so the pushed commit (`8948437d`) carries its hunks under my
message: the feature map's BanditManagement row and the balance chapter's settings count. Content
correct, attribution wrong, and a pushed commit cannot be re-cut.
- **Why missed:** the status check was read as a property of the file instead of the instant, and the
  temporary-index technique guards against the shared INDEX, not against the worktree moving under you.
- **Prevent:** `git add <path>` and `update-index --add <path>` stage the WHOLE worktree file, so a status
  check that predates the other session's edit proves nothing. Run `git diff -- <path>` immediately before
  staging and refuse if it shows a hunk you did not write; when a peer has hunks in the same file, build
  the blob yourself: `GIT_INDEX_FILE=<scratch> git read-tree HEAD`, then `update-index --cacheinfo` with a
  blob made from `git show HEAD:<path>` plus your own change (how the #558 CHANGELOG and language rows and
  the three #559 commits were built). This is the inverse of CLAUDE.md's "Never write a reconstructed
  shared file to DISK" row: reconstruct into the index, never the worktree. When it has already happened,
  the remedy is a CHANGELOG line naming the carried hunks.
- **Source:** 2026-09-11, the #558 documentation commit; relayed by the #559 session.

### When a change moves a number a file quotes, grep the file for the OLD LITERAL, not for the concept
#564 (2026-09-11) took the eight boss templates out of `rebalance_template_power.py`'s scope, 50
templates to 42. The edit was scoped by grepping the tool for `boss`, and every hit was fixed. Two
sentences survived saying "None of the 50 templates carries a hero", one in the tool's `troop_power`
docstring and one in its test class docstring, because neither contains the word `boss`: they quote
the total. The tool's tests assert behaviour, so they stayed green.
- **Why missed:** the grep that scoped the edit searched for the thing being removed, and a derived
  number does not mention the thing it was derived from. The diff-scoped review agents could not see a
  line the diff did not touch; the one agent briefed to read every number in the file found it.
- **Prevent:** after any change that moves a count, scope, threshold or version a file states, grep
  that file (and its tests and its README row) for the old value as a bare literal (`50`, `16 bandit`)
  and re-derive each hit. Same class as REVIEW-LOG 90 and 91, where unverified counts rode into prose.
- **Source:** #564 deep-review, `docs/reviews/rca-hideout-boss-fight-2026-09-11.md` finding 1.

### A generator whose input its own next step rewrites must re-derive its plan from the rewritten state
`generate_starter_kit.py` read the donor ids out of the player-start rosters; `wire_starter_kit_rosters.py` then rewrote those rosters to the `starter_` ids, which the donor scan skipped. The first `--apply` and its idempotency check both ran before the wiring, so the second run was a genuine no-op at that moment and looked like proof. Once wired, a re-run planned 0 clones, and `--apply` would have emptied the crafting-pieces marker block (39 blades), every stylesheet marker block and every generated item file, while `--verify` reported no drift because it checked only the plan's own entries.
- **Why missed:** every unit test and the live proof captured the pre-wiring state of the input. Nothing asked what the tool sees after the pipeline's second step has run, and the documented maintenance workflow (re-run the generator, then the wiring tool) is exactly the sequence that walks into it. Found by the deep review's tooling-correctness agent running the tool dry against the already-wired repo.
- **Prevent:** when step B rewrites what step A reads, prove A's idempotency AFTER B has run (a fixture that has been through B, and a live dry run on the post-B state); map the rewritten ids back to their sources in place so the plan and its order come out identical; and make every writer refuse to drop ids it previously wrote unless an explicit `--allow-shrink` is given, so a run that cannot see its inputs cannot empty its outputs. `--verify` must check what is on disk against the plan in both directions.
- **Source:** #569 deep review, 2026-09-12, `docs/reviews/rca-starter-kit-2026-09-12.md` finding 1.

### A rule reused by a second consumer answers the second consumer's question, or it gets its own predicate
#581's kingdom armour overview borrowed two rules that had been written for other readers. The validator's `_ARMOUR_LADDER_EXEMPT` set answers "which cells may the gate judge"; the tool reused it only for its gate preview and left the exempt Ithilien ranger in the matrices and the pairwise list, where a light kit at level 51 filled Gondor's T10 cell and topped the executive summary's worst-pairs list five times as the loudest regression in the game. The curve alias table answers "which curve predicts this TROOP"; the ceilings reused it for "which curve statted this ITEM", and Umbar, which wears Harad's folder at a different protection modifier, lost two elite pieces from its re-slot reserve. Both numbers were internally consistent and the completeness pass read them as correct.
- **Why missed:** each reuse was a one-line convenience, the fixture had no culture wearing a foreign folder at a different modifier and no exempt troop, and the doc described the views as built, so a diff-scoped or doc-checking agent had nothing to disagree with. The tooling agent found both by running the tool per culture against the live install and reading the output as a user would.
- **Prevent:** when a rule is reused by a second consumer, write down the question the rule answers and the question the consumer asks; if they differ, the consumer gets its own predicate (`_ladder_records`, the item's own folder curve) and a test whose fixture makes the two answers differ. For any report, read the executive summary and one per-culture section of the live output before the numbers go into a doc.
- **Source:** #581 deep review, 2026-09-12, `docs/reviews/rca-kingdom-armour-2026-09-12.md` findings 1 and 2.

### A spec that names external things is validated against them; a name that matches nothing is a finding
`tools/ranged_ladders.json` claims troop files by culture token (`files: ["rhun_new"]`). `validate_spec` proved the spec consistent with itself and with the Armory, and never with the `troops/` directory, so a `rhun` typo would have been a line with no troops: no rows in the report, nothing for the gate to compare, a clean-looking run (#582 deep review, tooling agent). The settlement-economy gate learned the same thing on 2026-08-14 (a spec culture owning no settlement is a finding) and it did not carry, because it was written down as a fact about settlements.
- **Why missed:** the validator was a pure function of the spec dict; the "unassigned troop" check covers the opposite direction only, and the shipped spec had no typo to show it.
- **Prevent:** every check that reads a spec of names takes the set of names that exist (`validate_spec(..., cultures=troop_file_cultures(md))`) and reports a claimed name with no referent. Same for a registry builder added to `taom_schema.build_registries`: time the validator with and without it, because the commit hook pays for it on every ModuleData commit (the launcher index cost 1.2 s parsing every item XML; a byte pre-filter made it 0.18 s).
- **Source:** `docs/reviews/rca-ranged-ladders-2026-09-12.md`
### A guard on a file is keyed on the invariant it protects, and a change that deletes data re-runs every guard keyed on that data
`generate_clan_heraldry.py` replaces a whole `<MBPartyTemplate>` from `clan_heraldry/<culture>.json`. Its 2026-08-17 guard stated the property ("the spec is behind the live file") and checked one instance of it (the spec would drop a troop id), because that was the failure in front of it: the Mordor spec lacked the 13 Black Numenorean ids. Every spec was also behind on counts (pre-2026-08-14 maxes of 2 to 4 against live 23 to 44), which the id check could not see and did not need to, since it fired first on every Mordor clan anyway. #584 removed those 13 ids from 13 of the 15 templates. Their live id sets then equalled the stale spec's, the guard went blind for them, and only the two house templates still tripped it. A guard that fires on every input for the wrong reason looks complete.
- **Why missed:** the tool was not in the changeset, so no per-file agent opened it; the data-flow agent found it by listing every writer of `taom_partyTemplates.xml` and dry-running the one whose refusal depended on the deleted data. The 2026-08-17 doc said "19 of 21 specs pass unaffected", which was true of the id check and false of the file.
- **Prevent:** write the guard's check as the property (here: the spec's max sum is not below the live template's, and it drops no id), not as the shape of the instance that motivated it, and give it a test whose fixture has the ids equal and the counts behind. When a change DELETES data, list every guard, refusal or test that keyed on that data and re-run it against the post-change state; a guard keyed on deleted data passes by construction. Now refuses 19 of 21 specs, `tools/tests/test_generate_clan_heraldry.py`.
- **Source:** #584 deep review, 2026-09-13, `docs/reviews/rca-black-numenorean-confinement-2026-09-13.md` finding 1.

### When two tools are said to share a rule, the shared symbol is not the claim: test the pair where the logic around it differs
#583's writer and the tier map both used `rebalance_armor.level_to_band`, and the map's docstring said the two "cannot disagree about a level". They agreed about the level and disagreed about which signal wins: the map kept an id keyword ahead of the wearer's band, the writer read the band first, so 682 of 1,861 worn items were labelled wrong in the report while the Armory was written right. The gate and the writer likewise shared the cap table but not the routing: items go to a cap by prefix and folder, the gate scaled a troop by its culture, and an Umbar noble in Black Numenorean plate read 29% higher than he was.
- **Why missed:** each pair had a genuinely shared symbol, and the sentence claiming agreement was written about that symbol. Neither test suite ran the two consumers on one fixture where the precedence or the routing around the shared symbol would give different answers, so nothing could fail.
- **Prevent:** when a doc or docstring says two tools cannot disagree, write the test that would make them disagree: one fixture, both consumers, an input chosen so the logic AROUND the shared symbol (precedence, routing, scaling granularity) is what decides. If the second consumer needs data the first has and it lacks (the validator had no item folders), add the data rather than approximating (culture for folder).
- **Source:** #583 deep review, 2026-09-13, `docs/reviews/rca-kingdom-cap-curve-2026-09-13.md` findings 1 and 2.

### When a fact moves, grep every statement of it (plan 016, 2026-09-24)
Plan 016 moved the MCP deny list into the tracked `.claude/settings.json` and pinned four servers
in `.mcp.json` and `.codex/config.toml` on the 1.5.x line. The files the plan listed were right; four other places
stayed stale: `.vscode/mcp.json.example` and a `.mcp.json` snippet in `kingdom-voices.md` kept the
unpinned launch strings, a lesson's **Prevent:** still sent deny edits to the untracked file, and
the ModuleData activation step still said the server was enabled for every clone.
- **Why missed:** the plan's scope list was treated as the complete set of places the fact lives.
  The audit reads only `.mcp.json`, so no gate saw the other copies.
- **Prevent:** before committing a change that moves a file, renames a setting or pins a value,
  `git grep` the old location and the old value across the repo (docs, lessons, templates,
  examples) and fix or consciously leave each hit. A recount ("Three things" to "Two things")
  re-derives the list rather than deleting one item.
- **Source:** 1.5.x `docs/reviews/rca-repo-hygiene-pins-readme-2026-09-24.md`, findings D5, D6, D7, D10, on `origin/bannerlord-1.5.x` (ported by WP142).

### Give the command that writes the file itself (plan 016, 2026-09-24)
The migration note told developers to restore their settings with
`git show <sha>:<path> > <path>`. In Windows PowerShell 5.1 (installed on the desktop) `>` is
`Out-File`, which wrote `FF FE` plus UTF-16LE; Git Bash and pwsh 7 write the bytes unchanged.
- **Why missed:** the command was written and tested in Git Bash and read as portable.
- **Prevent:** in a documented command that recreates a file, let the tool write it
  (`git restore --source=<sha> -- <path>` writes the working tree only and leaves an ignored path
  unstaged), or name the shell. Never rely on a shell redirect for a file another program parses.
- **Source:** 1.5.x `docs/reviews/rca-repo-hygiene-pins-readme-2026-09-24.md`, findings D9, on `origin/bannerlord-1.5.x` (ported by WP142).

### Untracking a file another live branch tracks changes every switch and every new worktree (plan 016, 2026-09-24)
Plan 016 untracked `.claude/settings.local.json` on `bannerlord-1.5.x` only. Its note covered merge
and pull, but `bannerlord-1.4.5` still tracks the file: git silently overwrites an ignored file
when a switch brings in a tracked one, and removes it on the way back. A worktree created after
the untrack starts with no copy at all. This line untracked it in WP142; `1b00d1acd` and older still track it.
- **Why missed:** the note traced one transition (merge or pull on one branch), not every way a
  checkout's tree changes.
- **Prevent:** when untracking a per-user file, list each live branch that still tracks it and
  state in the migration note what a switch, a new worktree and a fresh clone do to the user's copy.
- **Source:** 1.5.x `docs/reviews/rca-repo-hygiene-pins-readme-2026-09-24.md`, findings D8, on `origin/bannerlord-1.5.x` (ported by WP142).

### A Visual Studio launch deploys the whole working tree, refs to held-back Armory items included (2026-09-29)
To keep a load test to two Armory mesh swaps, four new items were held out of the live Armory while the repo already
named them. The launch came from Visual Studio, which builds and deploys the working tree (MSBuild 19:22:05,
`TAOM.dll` 19:22:11, the game 19:22:23), so the deployed `harad_elephant_rider` rolled two rosters whose HorseHarness
named nothing: 14 of 29 elephants spawned bare, with no platform and no crew (`wears harness 'none'`). An earlier
session under that debugger ended with no crash log and no Windows error event; whether the debugger caught a native
crash is unestablished (the repo's default Visual Studio profile, `Main/Properties/launchSettings.json`, debugs managed
code only).
- **Why missed:** "nothing deploys until a build" was reasoned about the session's own build commands, while the
  maintainer's launch path is itself a build.
- **Prevent:** land Armory items before any repo data that names them (a `BROKEN_ITEM_REF` means the tree is not
  launchable), and when a session ends with no crash record, ask whether a debugger was attached, and what it showed,
  before reading the silence as a clean exit.
- **Source:** 1.5.x TAOM logs `taom_debug_2026-09-29_19-22-43.log` and `_19-29-26.log`, and `docs/features/elephant.md` "Package the Armory", on `origin/bannerlord-1.5.x` (ported by WP142).

### A subagent's issue number is a claim: check it against the tracker (2026-09-29)
A builder subagent tagged the elephant change "#694" in five comments across code and tests. #694 is the troll
bandits, and no issue existed for the work; every other `#694` in the tree was the trolls'.
- **Why missed:** the brief named no issue, and the agent filled the gap with the most recent number in its context.
- **Prevent:** brief a subagent with the issue number or say there is none, and grep its diff for `#[0-9]+` before
  accepting it.
- **Source:** the 1.5.x KEYforce art session, 2026-09-29 (`Main/Features/Elephant/`), on `origin/bannerlord-1.5.x` (ported by WP142).

### A backup written beside a live module file ships with it: write it to the quarantine (2026-09-29)
The 14 backups for the KEYforce wiring went in as `*.bak-keyforce-2026-09-29` beside the live Armory files, and
`.bak` breaks the Cloudflare distribution ([module-backup-sweep.md](../../reference/module-backup-sweep.md)). They were
moved to `E:\Bannerlord_Backups\module_bak_sweep_2026-09-29\` with their hashes checked, before any package.
- **Why missed:** the XML I/O convention says to back up before a destructive write and never with an `.xml`
  extension, but not where; the sweep doc says where, and nothing links the two.
- **Prevent:** write a live-module backup straight to `E:\Bannerlord_Backups\module_bak_sweep_<date>\<Module>\<same
  relative path>`, or run `pwsh tools/sweep_module_backups.ps1 -Apply` before a package.
- **Source:** 1.5.x `docs/reference/lotrlome-spider-mount-changes.md` "2026-09-29", on `origin/bannerlord-1.5.x` (ported by WP142).

### Before hand-editing a data file, check who writes it; an owned file goes through its owner (2026-09-29)
A one-off translation script inserted rows by cloning an anchor line. Two of its targets are generated:
`taom_troop_name_strings.xml` (its header says "Do not hand-edit; re-run the generator") and the 12
`tools/translation_cache/*.json` (sorted on every save by `translate_with_claude.py`). The content was right and the
order was not, so the next save by either owner would have landed a reorder hunk in an unrelated commit. The same
session rewrote one sentence of the 1.5.x `/armory-audit` skill to forbid editing the mirror and left the file's second
mention of the mirror in place.
- **Why missed:** the script checked bytes and parse, not ownership; the skill fix edited the sentence that misled,
  not the rule.
- **Prevent:** read a file's header and `tools/README.md` for a generator before editing it, and run the owner
  (`generate_name_localization_strings.py --apply --category troop`, `translate_with_claude.save_cache`). When changing
  a policy sentence, grep the same file for its other mentions.
- **Source:** 1.5.x `docs/reviews/rca-keyforce-art-wiring-2026-09-29.md` F7 and F8, on `origin/bannerlord-1.5.x` (ported by WP142).

### A memory card's "UNCOMMITTED" is a claim about the past: grep the release commit before saying what ships (v2.0.33, 2026-10-04)
Before the v2.0.33 cut this session told Mike that nine untranslated `taom_tr_*` strings were unused by shipped
code. The Tournament Rewards memory card said "UNCOMMITTED, review owed"; in fact Mike's mixed commit `9e2a39f4` had
carried the whole feature to trunk two days earlier, registered in `FeatureModules.cs:23`, with no review.
- **Why missed:** the card was read as current state; nothing in git flags a feature that reached trunk without a
  review record.
- **Prevent:** before telling Mike what a release contains, grep the code at the commit being released (`git grep
  -n <key> <sha> -- Main`), and for each feature in the range check that a review record exists
  (`docs/reviews/rca-*`). A card that says UNCOMMITTED for files `git log` shows on trunk is stale: update it.
- **Source:** 1.5.x `docs/reviews/rca-tournament-rewards-2026-10-04.md`, "How it shipped unreviewed"; the Mouth of Sauron's gear went the same way (`docs/reviews/rca-mouth-of-sauron-gear-2026-10-04.md`), on `origin/bannerlord-1.5.x` (ported by WP142).

### Pick a release number no channel already carries (v2.0.33 renumbered as v2.0.34, 2026-10-04)
v2.0.33 was tagged and pushed while the testing channel already held an untagged v2.0.33 build of 2026-10-02
(`bc39f6e4` plus uncommitted work, by its DLL stamp), so a tester's "v2.0.33" was not the tagged code. Mike asked for
v2.0.34; the same happened with v2.0.31 on 2026-09-26.
- **Why missed:** the release pre-flight checked git tags only; an untagged channel build is invisible to git.
- **Prevent:** `/release` Phase 1 item 4b reads the `<Version>` of every `E:\LOTRAOM_Releases\<channel>\Modules\TAOM`
  before choosing the number; on a machine without that folder, it asks Mike.
- **Source:** the 1.5.x v2.0.34 release commit `18a4e402`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Commit a single path with `git commit -- <path>` when anything else is staged (B4a, 2026-10-22)
A docs commit ran `git add <skill file> && git commit`, and `git commit` with no path commits the whole index. The index
already held 15 staged `git mv` renames of an unfinished batch, so the pushed docs commit (1.5.x `0a8d61a4e`) moved the files
without the core changes and did not build on its own until the batch commit (1.5.x `10f1f0b80`) landed.
- **Why missed:** the subagent's report said the renames were staged; the commit step checked the working tree, not the
  index.
- **Prevent:** run `git diff --cached --name-only` before every commit and compare it with the paths you meant to commit.
  When the index holds anything else, commit with `git commit -- <path>`, which commits only that path. Never push a commit
  whose file list you did not read.
- **Source:** 1.5.x commits `0a8d61a4e` and `10f1f0b80`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A generator that pastes free text into a file it later parses must escape that text's delimiters (plan 020, 2026-09-24)
`tools/changelog_from_commits.py` copied commit bodies verbatim into `CHANGELOG.md` and then, on the
next release, searched the whole file with `^## v…` to refuse a duplicate. A body that showed an
example heading (`## v2.0.32 (2026-11-01)`, even inside a code fence) made the v2.0.32 run refuse,
and any heading-shaped body line added structure to the file. Codex found it; six lenses did not.
- **Why missed:** every fixture body was plain prose and every test ran one release. The design
  lens dismissed escaping because none of 916 past bodies had a heading, which judges the defect
  by history when the input is bodies not yet written.
- **Prevent:** when a tool embeds text it does not control inside a format it will parse again,
  escape the format's delimiters on the way in (here, lines CommonMark reads as ATX headings, which
  leaves `#622:` alone) and write a test that runs the tool twice, the second run reading the
  first run's output.
- **Source:** 1.5.x `docs/reviews/rca-changelog-at-release-2026-09-24.md`, C3, on `origin/bannerlord-1.5.x` (ported by WP142).

### Retiring a duty: search for the duty, not for its phrases in the folders you expect (plan 020, 2026-09-24)
Plan 020 retired the hand-written CHANGELOG. Its sweep grepped a fixed list of folders for fixed
phrases, so eleven live instructions survived (`docs/features/` recipes, the feature-doc template,
the `commit-split` routing row, two lens prompts), a rule still said a deleted hook enforced the
duty, and `release-process.md` kept the old step order that `/release` had just reversed. The
executor wrappers under `plans/` still ordered hand entries.
- **Why missed:** a list of places is a guess about where the duty is written. Reworded
  instructions ("Update CHANGELOG", "Add to CHANGELOG", "the CHANGELOG `Not-tested:` line") pass a
  phrase grep. Repeat of "A structural refactor's leftover-reference sweep must cover living docs"
  (2026-07-01).
- **Prevent:** grep the whole repo, case-insensitive, for the artifact's name (`changelog`) with no
  folder filter, then classify each hit as history (leave it) or instruction (fix it). When a skill
  changes its step order, re-read the doc it calls its "Full contract" in the same change.
- **Source:** 1.5.x `docs/reviews/rca-changelog-at-release-2026-09-24.md`, C5, C6, C13, on `origin/bannerlord-1.5.x` (ported by WP142).

### Anchor "was X changed" checks and release ranges to a fixed commit, not HEAD (plan 020, 2026-09-24)
Two defects in one change. The completeness lens checked for a hand edit with
`git diff --name-only HEAD -- CHANGELOG.md`, which sees only uncommitted edits, so on a branch
that had rewritten the file it printed nothing. And `/release` generated the section from `HEAD`,
then tagged `HEAD` phases later, so a commit landing in between was in the tag and in no section.
- **Why missed:** each check was right at the moment its author pictured (`/verify` before a
  commit; a single session cutting a release) and was reused where `HEAD` means something else.
- **Prevent:** a review check diffs against the review's base (`HEAD` only for uncommitted work).
  A multi-step procedure that reads a range resolves the end to a SHA once, prints it, checks it
  before committing, and tags by SHA.
- **Source:** 1.5.x `docs/reviews/rca-changelog-at-release-2026-09-24.md`, C2, C4, on `origin/bannerlord-1.5.x` (ported by WP142).

### `ReadAllText` strips the BOM, so a "keep the BOM if present" check on its result can never fire
A one-number edit to `docs/reviews/LESSONS-LEARNED.md` was made with PowerShell `[IO.File]::ReadAllText` then `WriteAllText(..., new UTF8Encoding($raw.StartsWith([char]0xFEFF)))`. `ReadAllText` detects the BOM and removes it from the string it returns, so `StartsWith(0xFEFF)` was always false and the write dropped the file's `EF BB BF`, a hunk nobody asked for in a file two sessions were editing.
- **Why missed:** the check read as a preservation guard and was never run against a file known to have a BOM; its input had already lost the thing it was testing for.
- **Prevent:** decide the BOM from bytes (`[IO.File]::ReadAllBytes(p)[0..2]`), or edit with the Edit tool, which keeps the file's encoding. After any scripted rewrite of a tracked file, `git diff --stat` it: a line-1 hunk you did not intend is the tell.
- **Source:** 1.5.x `docs/reviews/rca-shield-rethrow-stack-2026-09-22.md` deep review finding 4, on `origin/bannerlord-1.5.x` (ported by WP142).

### A PreToolUse gate's decision counts only under `hookSpecificOutput`; prove a gate live (#647, 2026-09-23)
Nine TAOM gates printed `{"permissionDecision": "deny", ...}` at the top level of their JSON, and Claude Code ignores that form, so no deny or ask they returned was ever honoured: the destructive-git prompt, `/freeze`, the commit-label, CHANGELOG, ModuleData and doc-drift gates. A debug dump showed the label gate returning a deny in 0.25 s while the commit ran.
- **Why missed:** `harness-facts.md` stated the top-level form as the contract, and `tools/test_hooks.sh` judged a decision by matching the text `"permissionDecision":"deny"`, which the nested form contains too. Every test ran a gate through bash; none ran a tool call through the harness. The hooks catalog's "Hard-blocks" lines described the source, not an observed refusal.
- **Prevent:** print `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":...,"permissionDecisionReason":...}}` or exit 2. `test_hooks.sh` 5c fails the top-level form statically, and section 4 reads decisions the way the harness does. A gate is done when a live tool call it must stop has been shown refused (`hook-authoring.md` "Prove a gate live").
- **Source:** 1.5.x `docs/reviews/rca-adr011-batch1-2026-09-23.md` F1, on `origin/bannerlord-1.5.x` (ported by WP142).

### Hook Python reads stdin in the ANSI code page: one character outside it blinded every gate (#647, 2026-09-23)
On Windows a piped stdin and stdout default to cp1252. A command holding a character outside it made the hooks' extraction `print(command)` raise inside `except: pass`, the command came back empty, and each gate treated it as nothing to check. With the old `_pybin.sh`, `NOTE=→ git push --force origin bannerlord-1.4.5` passed the force-push block.
- **Why missed:** every test payload was ASCII, and the bare `except` turned an encoding error into the gate's "not my command" path, which is silent by design.
- **Prevent:** `_pybin.sh` exports `PYTHONIOENCODING=utf-8` for every hook that sources it. Test a gate with a non-ASCII command as raw and as escaped JSON; a new gate copies those cases.
- **Source:** 1.5.x `docs/reviews/rca-adr011-batch1-2026-09-23.md` F2, on `origin/bannerlord-1.5.x` (ported by WP142).

### Read a shell command, Markdown or YAML with its parser, not a line regex (#647, 2026-09-23)
Three gates approximated a language with regexes and each broke on valid input. The commit gate missed `$'...'` quoting, attached `-m"..."` and concatenated words, and took option-shaped prose inside a message for a real option. The import scan closed a four-backtick fence on a three-backtick line and read a code span across lines as prose. The budget took a rule with unparseable frontmatter for path-scoped.
- **Why missed:** each regex matched the forms seen so far, and every test used those forms.
- **Prevent:** tokenize shell with `shlex` (decode `$'...'` first, split at `;`, `&&`, `|` and unquoted newlines) and read each command's own options; scan Markdown by its documented rules (fence character and length, spans across lines inside a paragraph); parse YAML with `yaml.safe_load`. Test the unusual forms the language allows, not only the usual ones.
- **Source:** 1.5.x `docs/reviews/rca-adr011-batch1-2026-09-23.md` C2 to C6, on `origin/bannerlord-1.5.x` (ported by WP142).

### A timeout check that picks hooks by what they launch misses the one slow for another reason (#647, 2026-09-23)
`check-claude-files-tracked.sh` asked git two or three times per file, took 6 s against its 5 s registration, and was killed on every commit, which reads as a pass. `test_hooks.sh` check 3 times only hooks that launch a `tools/*.py`, so it never looked at this one: the 2026-08-31 "measure the slow path" lesson, missed a second time by how the check chose its hooks.
- **Why missed:** the check discovered hooks by their external tool, not by being a gate.
- **Prevent:** check 4b times every PreToolUse gate on a commit payload against the real repo and fails at 80% of its registration. Query git once for all files, never once per file.
- **Source:** 1.5.x `docs/reviews/rca-adr011-batch1-2026-09-23.md` C1, on `origin/bannerlord-1.5.x` (ported by WP142).

### A here-string of 65,537 to 65,664 bytes hangs Git Bash forever, and a killed gate fails open (#681, 2026-09-26)
On the desktop's Git Bash (bash 5.3.15), a here-string or here-document whose text is 65,537 to 65,664 bytes never
returns: up to 65,536 bytes bash feeds it through a pipe, from 65,665 through a temp file, and in between it blocks
writing into its own pipe before any reader exists. Every form built on that redirection hangs (`<<<`, `read -a <<<`,
`mapfile <<<`, an expanding `<<EOF`, even `: <<<` with no reader). Ten hook sites fed a payload or a git name list
through `<<<`, so a force push to a trunk, a `reset --hard` or a broad `add` padded into the window hung
`validate-push.sh`, `block-dangerous-git.sh` and `block-broad-git-add.sh` until the harness killed them, and a killed
gate allows. The staged-name sites are reachable too: four commits in TAOM history list over 64 KiB of paths. The
issue proposed `done < <(printf '%s\n' "$X")`, which never hangs but forks on every call (22 ms each) and makes bash
read a pipe one byte at a time: 2,155 ms for a 400 KB loop against 585 ms for the here-string and 22 ms for an
array split, which would have pushed `validate-push.sh` past its 5 s registration on a large command (#680).
- **Why missed:** a here-string reads as an in-memory string, so nobody sized it, and every test payload was either
  small or far above the window; a hang only 128 bytes wide is invisible to a test that doesn't aim for it.
- **Prevent:** split hook text without a redirection: `set -f; IFS=$'\n'; A=($X); IFS=$' \t\n'; set +f` and a `for`
  loop (no fork, empty lines dropped, variables set in the loop stay visible), or `set -f; T=($X); set +f` for words;
  keep `set -f`, `set +f` and the IFS restore on one line. `tools/test_hooks.sh` 4e refuses a here-string or an
  expanding here-document in any script a hook registration runs, and 7h sends payloads sized into the window
  through the gates. Measure an alternative's cost on large text before proposing it as a fix.
- **Source:** 1.5.x issue #681; `.claude/hooks/validate-push.sh`, `block-dangerous-git.sh`, `block-broad-git-add.sh`, `mark-verification-run.sh`, `check-doc-config-drift.sh`, `check-moduledata-validation.sh`, `check-native-dll-crt.sh`, `check-verification-evidence.sh`, `detect-docs-gaps.sh`; `.claude/rules/hook-authoring.md`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A gate's suite passed a mutant that switched off its no-Python answer (#680 review, 2026-09-27)
The #680 review changed `validate-push.sh`'s degraded branch from `&& coarse "no safe python"` to `&& exit 0`, and
the whole hook suite stayed green: the starved-environment row checks only that a gate fails open and says so, and
the mutant still printed its note while it allowed every push. Three more tests proved less than they claimed. The
bytecode check passed whenever an earlier run had left `_pushjudge.cpython-*.pyc` in `.claude/hooks/__pycache__`.
With a global `push.default=matching`, four 7c rows and two unit rows failed on a correct hook, since they read
this machine's git config. And 4e skipped every line that opens with `#`, so a here-string on a line continuing a
quoted string went unseen, as did one after a quoted `<< "Z"` inside a string, one in a `bash -s <<'SH'` body that a
child shell runs, and any registration naming a script outside `.claude/`.
- **Why missed:** each test was checked by passing on a correct hook, never by failing on a broken one, and each
  leaned on the machine it ran on: a Python on PATH, a warm cache, an unset global setting.
- **Prevent:** give every branch of a gate a row that runs on any machine (a sandbox copy of the hooks with
  `PYBIN=""` appended to `_pybin.sh` takes Python away portably); run each new test against a mutant of the code it
  guards and watch it fail; build a test's inputs fresh (a temp copy of the modules, a scratch repository whose
  local config sets what the code reads); and make a scan assert that it covered what it claims (4e now fails a
  registration naming a script it cannot find).
- **Source:** 1.5.x the #680 and #681 review, 2026-09-26 (mutant H10; the 4e probes); `tools/test_hooks.sh` 4e, 7c and 7e; `tools/tests/test_pushjudge.py` (`CliTests`), on `origin/bannerlord-1.5.x` (ported by WP142).

### A hook registered by a relative path stops running after a cd (#690, 2026-09-26)
All 27 `settings.json` registrations named their script as `.claude/hooks/<name>.sh`. Claude Code runs a hook
command in the session's current directory, and a Bash `cd` into a project subdirectory persists across tool
calls, so after `cd tools` every hook failed to start with "No such file or directory". The harness treats that
as a non-blocking error: the call ran with no force-push guard, no commit gates and no confirm prompts. The #680
review proved it live (a trunk force push reached git after `cd tools`; 33 such lines in the session's debug
log) and in a sandbox where the anchored form refused from the subdirectory. The skill-frontmatter hooks of
`/freeze` and `/investigate` had always anchored on `${CLAUDE_PROJECT_DIR}` and kept working.
- **Why missed:** every test ran hooks from the repo root, and the catalog recorded "cwd = the MAIN tree
  regardless", which holds for a `cd` inside the same command, not for one that persists.
- **Prevent:** register every hook as `"$CLAUDE_PROJECT_DIR"/.claude/hooks/<name>.sh`, and test a gate the way
  the harness runs it: the registered command string through `bash -c`, from a subdirectory
  (`tools/test_hooks.sh` 7j).
- **Source:** 1.5.x issue #690; `docs/reference/hooks-catalog.md`; the review evidence in `E:\repos\taom-improve\scratch\680\review\bash\` (desktop), on `origin/bannerlord-1.5.x` (ported by WP142).

### The dirty-flag pathspec lists every compiled project root
The build stamp marks a DLL `.dirty` when the tree has uncommitted changes under the compiled roots. A new project root
absent from that pathspec leaves a stamp that reads clean while its sources are modified.
- **Why missed:** nothing ties the pathspec to the list of compiled projects.
- **Prevent:** when a project joins the module, add its root to the pathspec in `Directory.Build.props` in the same
  change and pin it with a test.
- **Source:** 1.5.x B3 review, 2026-10-22, ADR-013, on `origin/bannerlord-1.5.x` (ported by WP142).

### Renaming a convention or deleting a log line: grep the review harness and the tools for the old text (plan 009, 2026-09-24)
Plan 009 replaced every `_harmony.PatchCategory("X")` with `TryPatchCategory("X")` and collapsed nine catch blocks. The Data Flow lens (`lenses/5-data-flow.md`) and the Harmony lesson still told reviewers to grep for the old spelling and flag its absence HIGH, so every later patch review would raise a false HIGH. `tools/triage_battle_load.py` and `battle-load-diagnostics.md` still sent triagers to the deleted "Patch43 diagnostics failed to apply" warning.
- **Why missed:** the plan's maintenance notes named the one rule line it knew about; nobody grepped `.claude/`, `docs/reviews/lessons/` and `tools/` for the old text, and no build or test reads them.
- **Prevent:** when a change renames a call convention or deletes a log message, grep the whole repo for the old literal (code, `.claude/`, `docs/reviews/lessons/`, `tools/`, feature docs) and update every consumer that acts on it in the same change; list the rest as follow-ups.
- **Source:** 1.5.x `docs/reviews/rca-guarded-patch-category-apply-2026-09-24.md` findings 4 and 5, on `origin/bannerlord-1.5.x` (ported by WP142).

### A timing stamp reads its clock outside its own work: start after its setup, end before its lines
The `[Lifecycle]` dispatch line (plan 040) started its clock before the stamp swapped every listener by
reflection and read the end only after it had written every handler and event line, each a synchronous
INFO flush. Three texts then called `ms - listeners_ms` the engine's receivers. The sibling `[LoadXml]`
path read its end tick before logging and was right.
- **Why missed:** the stamp was reviewed for having its lines and totals, not for what each window
  contains; the oracle's fake clock never advanced while logging, so no test could see it.
- **Prevent:** in a stamp, take the end tick first in the closing call and start the window after any
  setup the stamp itself does; pin it with a test whose logger (and adapter) advance the fake clock. A
  sentence that says what a difference of two numbers is cites the clock reads that bound it.
- **Source:** 1.5.x `docs/reviews/rca-load-time-stamps-2026-10-02.md` row 1 (Standards, Engine, Efficiency, Data flow), on `origin/bannerlord-1.5.x` (ported by WP142).

### A once-only reason line has one latch per reason, and every path that writes it does what it says
`[Lifecycle] per-handler timing off for this session` was written by a gate, clock, logging or restore
fault that turned nothing off, and those faults spent the line's single latch, so a later real
switch-off logged no reason. The settings gate beside it returned "off" from a catch with no line at all.
- **Why missed:** D6 was checked for the existence of once-only reason lines; nobody listed each caller
  of the line and checked the consequence it states, and a catch returning the off value was read as
  fail-safe rather than as a disable path.
- **Prevent:** for each reason line, list every writer and confirm each one does what the text says;
  give unrelated faults their own line and latch (the X3 and C5 "stamp fault" shape); a catch that
  returns the off value logs why, once or on change. Pin each with a test that a second reason still
  logs after the first.
- **Source:** 1.5.x `docs/reviews/rca-load-time-stamps-2026-10-02.md` rows 2 and 3 (all six lenses), on `origin/bannerlord-1.5.x` (ported by WP142).

### No Windows dump while a debugger holds the game: Visual Studio, or TaleWorlds' own Watchdog (2026-09-26)
Three troll crashes in a row left no Event Log entry and no dump: Visual Studio was attached, so the access
violation stopped in the debugger, and ending the session killed the process before Windows saw it. procdump
`-e -w` then refused with "The process is already being debugged": the launcher starts `Watchdog.exe -p <pid> -dd
...\crashes`, TaleWorlds' crash catcher, which attaches as a debugger itself.
- **Why missed:** the earlier crashes of the day had produced dumps, so their absence read as a different crash
  rather than a changed setup.
- **Prevent:** reproduce a native crash with no debugger attached; if Visual Studio is attached, Detach All at the
  exception, never Stop. Only one debugger can hold the process, so procdump cannot attach while the Watchdog runs;
  a debugger's `$exception` still gives `_ip` and `_target`, enough to match the fault offset.
- **Source:** 1.5.x `docs/features/troll-race.md` "The swing CTD", on `origin/bannerlord-1.5.x` (ported by WP142).

### A native hash-map miss names its missing key in a register: read it from the dump, resolve it in game (2026-09-25)
The +0x6590B9 crash was a chained hash lookup whose miss left a null row. The missing key stayed in `r9` in the
exception context of all three dumps (6511, 6511, 6462); a one-off in-game scan of every action's clip index named
it `anim_hill_troll_release_overswing_2h`. After that, reading the table's builder in the disassembly (RTTI named the
owning class, the insert call named the rule) replaced three rounds of guessing the scope from crashes.
- **Why missed:** the crash had been treated as unreadable because the dump holds no heap; the register needed no
  heap.
- **Prevent:** `python tools/native_crash_triage.py --dump <dmp>` now prints the faulting thread's registers; for a
  lookup miss, find which register holds the key in the disassembly, then name it in game. Before widening a data
  rule to dodge a native crash, find the code that fills the table.
- **Source:** 1.5.x `docs/features/troll-race.md` "The swing CTD"; `tools/native_crash_triage.py`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Test a stated cause against the failures before fixing it (Bash tool length, 2026-10-21)
CLAUDE.md said Bash heredocs "mangle backslashes and quotes", and agents kept hitting "unexpected EOF
while looking for matching" anyway: 151 times in 51 session rows across every agent type. Measured
over the whole history, the cause was length: `bash -n` accepts 58 of 61 of the failing commands,
140 of 142 Bash calls at 8,000 or more characters (each ' counted as 4) failed, and none under 7,000
did. A short heredoc full of quotes runs fine; the backslash half of the old rule is real (the tool
halves `\\`). `check-bash-length.sh` now refuses the long commands with the fix in its reason.
- **Why missed:** the rule was written from the symptom (a quote error, seen in heredocs that held
  quotes), and every failing example also happened to be long, so nothing contradicted it.
- **Prevent:** before writing a rule or gate against a recurring error, split the calls that fail
  from the ones that pass on the suspected cause and on size; a cause that does not separate them is
  not the cause.
- **Source:** 1.5.x harness-retro-2026-10-21.md, proposal P3, on `origin/bannerlord-1.5.x` (ported by WP142).

### A change's sweep has two halves: readers of what changed, and text that states it (plan 007, 2026-09-24)
The plan 007 decisions commit renamed a local (`alreadyShielded` to `alreadySeen`), added a class to `Dependencies/Foundation/` and wrote a "reword when plan 006 lands" list. Its sweep grepped for code that reads the old log format and the removed `ShieldedCount` (both clean) and stopped. A test comment kept the old local's name, two docs kept the folder's old class count (18, now 19), and the reword list missed a line on the same branch that makes the same claim. Two of the three repeat lessons above ("After a whole-word identifier rename sweep", "When a change alters what an artifact CONTAINS").
- **Why missed:** "who reads this?" was answered; "what text describes this?" was never asked. A reword list written from memory of the decision covers the files the decision touched, not every file that states the claim.
- **Prevent:** before the commit, `git grep -n <old identifier>` over the whole repo for every rename, comments included; `git grep -n -e "<N> classes" -e "<N>-class"` (or the folder name) for every file added to or removed from a folder a doc counts; and build any "reword when X lands" list by grepping the claim's own words (here "preserves the stack", "fallback paths") across `docs/`, `CHANGELOG.md` and the code comments, then list every hit.
- **Source:** 1.5.x `docs/reviews/rca-patchshield-skip-callback-shims-decisions-2026-09-24.md` findings 1, 3 and 4, on `origin/bannerlord-1.5.x` (ported by WP142).

### A dry run and the patch that adds it are chained with `&&` (2026-10-08)
- **Why missed:** a heredoc patch meant to add a `--dry` flag to a doc-fix script failed on mangled backslashes (the
  known heredoc trap), and the next command on the same shell line ran the unpatched script, which ignored `--dry`
  and wrote 13 docs while review agents were still reading them.
- **Prevent:** write scripts with the Write tool and give a script its dry-run mode when it is written; chain
  dependent shell steps with `&&` so a failed step stops the rest.
- **Source:** 1.5.x `docs/reviews/rca-yotthani-adoption-2026-10-08.md` finding 28, on `origin/bannerlord-1.5.x` (ported by WP142).

### A merge of parallel branches drops text and doubles tests without a conflict: replay every branch's additions and the merged test set (2026-10-04)
The perf programme's integration trial merged 18 reviewed branches. Three passages a branch had added were gone (plan
039's narrowing of a PatchShield lesson, plan 028's crash-capture facts, plan 039's per-target PatchShield table), and
git auto-merged two overlapping tests from plans 028 and 041 into one class, one a subset of the other with a stale
comment. A conflict resolved by keeping one side's paragraph leaves no marker for the other side's facts, and two tests
added at different places in a file merge cleanly.
- **Why missed:** the trial's lost-hunk check recorded the table's drop but did not raise it, because its rows named a
  patch another branch had deleted and so read as obsolete; it compared lost lines, not doubled tests; and the fixups
  swept for one deleted patch by name, not for every member the deleting branch removed.
- **Prevent:** after an integration merge, replay every branch's added lines against the merged tree and raise every
  drop; stale-looking content is rewritten for the merged tree, not dismissed. For each test file more than one branch
  touched, list the merged tests and compare their assertions for one that is a subset of another. For each member a
  merged branch deleted, grep the whole tree for its name.
- **Source:** 1.5.x `docs/reviews/rca-perf-integration-fixups-2026-10-04.md` F2, F3 and F4; `plans/_audit/2026-10-02-perf/evidence/merge-map.md`, on `origin/bannerlord-1.5.x` (ported by WP142).

### When a change alters what an artifact CONTAINS, grep the docs for that artifact's inventory
The crash bundle's file list is stated in four places: the header comment in `CrashBundleWriter.cs`, the `TryCopyFile` calls, `BuildManifest`, and `docs/features/crash-report.md`. Adding `diag.log` to the ZIP updated the three that live inside the writer, because editing that file makes them impossible to miss, and left the doc claiming a five-file bundle. The stale neighbour on the same table row (the Logs section still describing the RGL log as coming from `Documents`, months after the collector started probing `%ProgramData%` first) showed the whole section had gone unverified for just as long.
- **Why missed:** the change was framed as "add a file to the ZIP", which is a code-shaped framing, and three of the four inventory copies are code. Nothing in the repo gates this: `lint_docs.py` checks version refs, orphan docs, config drift and dashes, and cannot know that a prose sentence enumerates a ZIP's entries. Teaching it to parse prose inventories is not worth building, so the discipline has to be the author's.
- **Prevent:** before committing a change to what an artifact contains (a bundle, a report, a config schema, a package manifest, an export), grep `docs/` for the artifact's current inventory and update every enumeration. Treat a stale claim found next to the one you are fixing as evidence the surrounding section is unverified, and read the rest of it rather than fixing only your line.
- **Source:** 1.5.x docs/reviews/rca-crash-log-readability-2026-08-19.md findings #1 and #2, on `origin/bannerlord-1.5.x` (ported by WP142).

### A release gate reads what the packager ships, and fails closed on every input it did not see (plan 017, 2026-09-24)
`package_release.py --require-build` read one fixed path per module (`bin/Win64_Shipping_Client/<dll>`) while the
packager copies every `bin/*/` DLL, so the Game Pass, server and Modding Kit copies shipped unread; the real patreon
package's Dependencies server copy came from a third commit. The same gate skipped a module named `taom` (case), ran
no check at all for `--require-build ""`, and certified the set after planning had silently dropped a requested module.
- **Why missed:** the plan designed the gate from its inputs (a module name, a path, a rev string) and the tests built
  only the one folder the gate expected. Nobody put the gate's read set beside the packager's copy set.
- **Prevent:** derive a gate's input from the exact list the tool will act on (here `_copy_list`), validate the
  requested set before any filtering drops members, casefold names that come from a Windows path, test an option with
  `is not None`, and write one test per way the input can be smaller than what ships.
- **Source:** 1.5.x `docs/reviews/rca-build-identity-dirty-flag-2026-09-24.md`, findings 1 to 4, on `origin/bannerlord-1.5.x` (ported by WP142).

### A missing marker proves nothing unless the producer could have written it (plan 017, 2026-09-24)
The gate read "no `.dirty` suffix" as "built from a clean tree". A DLL built at a commit whose `Directory.Build.props`
has no `TaomStampWorkingTreeState` target (the whole 1.4.5 line, every tag before plan 017) never writes the suffix, so
a dirty build there passed as clean.
- **Why missed:** the check tested for the bad marker and treated its absence as the good one.
- **Prevent:** when a verdict rests on a marker being absent, first prove the producer at that revision could have
  emitted it (here `git show <sha>:Directory.Build.props` contains the target), and refuse otherwise.
- **Source:** 1.5.x `docs/reviews/rca-build-identity-dirty-flag-2026-09-24.md`, finding 5, on `origin/bannerlord-1.5.x` (ported by WP142).

### A JVM left with a non-daemon thread keeps its Python host alive after the traceback (2026-09-26)
The failed first run above printed its Python traceback and then never exited: it sat at 1.5 GB until killed, and
its background task never reported. A `jstack` dump showed one non-daemon Java thread besides `main`,
`FelixDispatchQueue`, left by the half-started OSGi framework, and the JVM will not let the process end while one
lives. A run that succeeds releases the bundle host and exits normally, so only the failure path hangs.
- **Why missed:** a traceback reads as "the process ended". Only the missing `exit=` line from the wrapper showed it
  had not.
- **Prevent:** a tool that starts a JVM in-process leaves through `os._exit` once the JVM is up, after closing
  what it opened (`tools/native_decompile.py` `exit_process`, tested both ways). When a run prints a traceback but its
  task never completes, take a thread dump before killing it: the non-daemon threads name the culprit.
- **Source:** 1.5.x #688; `docs/reviews/adopt-ghidra-hindsight-2026-09-26.md` install trap 3, on `origin/bannerlord-1.5.x` (ported by WP142).

### A test fake returns the text shape you imagine; Ghidra's C carries CRLF on Windows (2026-09-26)
`native_decompile.py` printed every decompiled line ending in CR CR LF: Ghidra's `getC()` already uses CRLF on
Windows, and Python's text-mode stdout added a CR to each LF. Redirected to a file the output doubled its line count
(3,781 lines for about 1,900), and viewers show a blank line under every line of C. The 22 unit tests stayed green,
because their fake backend returns `\n` text.
- **Why missed:** the fake encoded the shape the author expected rather than the one the real API returns, the same
  blind spot as the fixture lesson at the top of this file.
- **Prevent:** normalise line endings where text crosses from a foreign runtime into Python's stdout, and assert on
  the raw bytes in the integration test (`assertNotIn(b"\r\r\n", ...)`), where universal newlines cannot hide it.
- **Source:** 1.5.x #688; `tools/tests/test_native_decompile.py` `GhidraIntegrationTests`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A mute that greps for a token nothing writes is dead, and reads as working
`check-deep-review.sh` muted its Stop reminder by grepping `agent-audit.log` for `deep-review|Standards Compliance|Efficiency.*Performance|...`, the review's lens titles. `log-agent.sh` writes only `[TS] agent_type=<type> agent_id=<id>`, and the reviewers ran as `Explore` and `taleworlds-researcher`, so across both log generations the mute matched 0 lines and never fired. The lesson above held it up as the muting example to copy.
- **Why missed:** the consumer was written against what the author expected the log to hold, not against a line the writer had produced. Every run of the hook looked fine because a reminder that always fires is indistinguishable from one that fires correctly.
- **Prevent:** a hook that reads another hook's output (a log line, a state file) gets a two-direction case in `tools/test_hooks.sh` that drives the REAL writer and the REAL reader together: without the event it must fire, after the writer logs it it must not. Section 7 does this for the deep-review mute; the pattern now names `agent_type=deep-reviewer`, the one token the writer emits.
- **Source:** 1.5.x `/deep-review` overhaul review, 2026-09-18, `docs/reviews/rca-deep-review-overhaul-2026-09-18.md`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A gate checked only against the case it was written for misses the history it guards (#1017, 2026-10-26)

The first `check_mcm_default_flips.py` compared the settings against one release tag, so every flip that had shipped earlier was invisible, and it compared constant defaults as text, so a changed constant passed. Rebuilt to compare against every reachable release tag and to resolve constants, it found nine more settings.

- **Why missed:** one base tag, constants compared as text, and silent OK paths (no tag, no settings found, a failed read, a partial class) that printed a pass.
- **Prevent:** compare against every release the trap can reach; make every degenerate input fail loudly; prove the gate on the commit before the fix (it must fail there) before trusting a green run.
- **Source:** 1.5.x `rca-settings-default-flips-2026-10-26.md`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A plan item dropped mid-build is struck from the issue that promised it, in the same step (2026-10-01)
#701 was filed before implementation and promised a `[MissionPerf] corpses=` field and a campaign-map advisor. The
field was dropped (it duplicates `agents` minus `active`) and the advisor moved to the main menu, both for good
reasons recorded only in chat; the issue kept promising them, and whoever closed it would have looked for a field
that does not exist.
- **Why missed:** the departure was a simplicity call made mid-build, and the issue, written first, was not on the
  list of things to update.
- **Prevent:** when a planned item is dropped or moved, edit the issue body (and the feature doc's departures)
  in the same step, not at close.
- **Source:** 1.5.x `docs/reviews/rca-battle-corpses-2026-10-01.md` finding 2, on `origin/bannerlord-1.5.x` (ported by WP142).

### A performance stage carries a measured cost, and is re-weighed after the stages before it land (2026-10-02)
Plan 037 Stage D added a transpiler, a thread-static hand-off, a fallback and four log formats to skip one
cache-backed distance read per positively scored town on a caravan's infrequent re-think, reversing a reviewed
"cheap enough" decision (`rca-caravan-trade-2026-07-04.md` row 2) on a call count, not a time. Stage B1
reordered two MCM toggle reads that Stage A, one commit earlier, had already made a cached field read. Six
review lenses rejected D under the simplicity criterion and B1 bought nothing; both were removed.
- **Why missed:** the plan costed each call site in isolation and before the earlier stages existed, and the
  build step's contract asked for parity, not for a fresh simplicity verdict once the code was written.
- **Prevent:** a performance stage states its win as a measured or bounded time (a profile, or the callee's
  cost times its real frequency), never a call count multiplied out. A stage that reverses a reviewed
  decision needs new evidence the review lacked. Before building stage N, re-state stage N's win against the
  code as stages 1 to N-1 left it; drop the stage when the win no longer dominates its cost.
- **Source:** 1.5.x `docs/reviews/rca-campaign-hot-paths-2026-10-02.md` findings 1 and 2, on `origin/bannerlord-1.5.x` (ported by WP142).

### Reversing a toggle again: sweep every commit since the template, and re-verify its claims (2026-09-25)
Shader Pre-compilation was parked (`5ae02f08`), re-enabled (`1e654021` and `1700156b`, #560) and
parked again. The second park replayed the first one's file list and comments. Docs the re-enable
commits had rewritten as live were not on that list, and a copied comment said the Patch21 postfix
ran "per loading-screen frame"; vanilla calls `LoadingWindowViewModel.Update` every frame from a
global layer.
- **Why missed:** the older template looked complete because it once was; everything changed
  between it and today was invisible to a replay.
- **Prevent:** when undoing a change that was itself an undo, list files from every commit since
  the template (`git log --stat <template>..HEAD --grep='#<issue>'`, not just the headline commit),
  then grep the whole repo for the feature doc's slug and issue number and classify each hit as
  history or living text (the "Retiring a duty" lesson above prescribes the same sweep). Re-check
  every engine or library claim the template's comments make before copying them.
- **Source:** 1.5.x `docs/reviews/rca-shader-precompile-repark-2026-09-25.md`, findings 1 to 3, on `origin/bannerlord-1.5.x` (ported by WP142).

### Moving a file out of a module does not remove it from installs: rename what a stale copy would still answer to (#627, 2026-09-19)
The howdah prefab moved from `Main/_Module/Prefabs/` to `LOTRLOME_Armory/Prefabs` under the same name. The repo and the dev install were cleaned, but every release from v2.0.22 to v2.0.30 had shipped `Modules/TAOM/Prefabs/taom_howdah_agent.xml`, an in-place update keeps it, and the release channel folder is itself updated in place. Players would load two prefabs of one name, and which one the engine instantiates is native and undefined.
- **Why missed:** "the deploy never deletes" was already a lesson (above), but it was read as a dev-machine fact. The move checked the dev install and not the installs players already have.
- **Prevent:** when a file leaves a module and something is looked up by its name (a prefab, a string id, an XML id), rename the identifier so a stale copy is dead data, and pin the name the code asks for in a test. Deletion steps in the packager cannot reach installs already in players' hands.
- **Source:** 1.5.x `docs/reviews/rca-howdah-prefab-review-2026-09-19.md`, #627, on `origin/bannerlord-1.5.x` (ported by WP142).

### Re-enabled parked code is new code: review the path the flag switches on, not the flag (#627, 2026-09-19)
Howdah crew spawn sat behind a commented-out call from June to September. Turning it back on was a one-line change; the path behind it had drifted (the crew's formation comments still said HorseArcher, the rider had been Cavalry since June 29), carried a port deviation that booked crew deaths as the rider's (see adapters-taleworlds-api.md), and missed vanilla spawn parity. A seven-lens delta review found one HIGH and a dozen LOW in code that "already worked in June".
- **Why missed:** "re-enable X" reads as a toggle, and the June code had once run, which read as proof.
- **Prevent:** when a parked path comes back, review it as if newly written: trace it end to end against today's data and engine, and compare it with the reference it was ported from. Also: a validator described as read-only may write (`audit_armory_refs.py` rewrites `docs/audits/armory-ref-audit.md` on every run); re-run such a tool just before staging its output.
- **Source:** 1.5.x `docs/reviews/rca-howdah-prefab-review-2026-09-19.md` (delta addendum), #627, on `origin/bannerlord-1.5.x` (ported by WP142).

### A scratch tool rebuilt at every bump needs a parse count, or "0 unresolved" means "0 of what I read" (2026-10-05)
The bound-member body diff, rebuilt per bump as "forty lines", parsed snapshot rows with a regex that
skipped every target carrying a generic-arity backtick: 27 of 292 rows, one a real change, while it still
reported 0 unresolved. It also ran before the snapshot was regenerated.
- **Why missed:** the tool counted what it compared, never what it skipped, and the skill told each bump
  to rewrite it.
- **Prevent:** regenerate the API snapshot first, then run the archived script and require its
  `unparsed=0` (`/engine-bump` Phase 3). Promoting it into `tools/` with a row-count test is a follow-up
  on #736.
- **Source:** 1.5.x `docs/reviews/rca-engine-bump-v1.5.4-2026-10-05.md` F4, on `origin/bannerlord-1.5.x` (ported by WP142).

### A report a tool promises never changes the exit code is still on the exit-code path (plan 035, 2026-10-02)
The release packager's new scene listing printed every shipped scene folder name. Under a piped stdout, which is
cp1252 on Windows, a name outside the code page raised `UnicodeEncodeError` out of `main`: a clean dry run exited 1,
and a real run stopped before the copy, although the change and its docs promised the report never changes the exit
code. This is the third cp1252 stdout crash in TAOM tools, after `rebalance_troops.py`'s delta glyph and the #647
hook extraction (both earlier in this file).
- **Why missed:** "never changes the exit code" was checked as "adds no `return` or `raise`"; a print of a name the
  tool did not write is a way out of `main` too. Every scene name on disk is ASCII, so no real run showed it.
- **Prevent:** a tool that prints names it reads from disk calls
  `sys.stdout.reconfigure(errors="backslashreplace")` (and stderr) at the top of `main`, guarded with
  `hasattr(stream, "reconfigure")` for in-process tests. A section promised to be report-only runs inside its own
  `try/except Exception` that prints the failure and goes on. Test both: a child run with
  `PYTHONIOENCODING=cp1252` and a non-ASCII name, and a report patched to raise
  (`tools/tests/test_package_release.py`, `TestSceneShaderCacheCli`).
- **Source:** 1.5.x `docs/reviews/rca-release-shader-cache-check-2026-10-02.md` F1, on `origin/bannerlord-1.5.x` (ported by WP142).

### A release blocker goes in the /release pre-flight, not only in a feature doc (#646, 2026-09-23)
Mike kept the Custom-Battle test riders visible until release (D1), and the blocker was written in the feature doc,
the workflow doc and the CHANGELOG, none of which `/release` reads.
- **Why missed:** writing it down felt like tracking it.
- **Prevent:** a "must happen before a release" item becomes a mechanical pre-flight line in
  `.claude/skills/release/SKILL.md` (here `git grep -l taom_test_ -- Main/_Module` prints nothing).
- **Source:** 1.5.x `docs/reviews/rca-animalia-2026-09-23.md` "Final review", finding F7, on `origin/bannerlord-1.5.x` (ported by WP142).

### A gate that checks less on a parsed reading falls back to the old check when the reading resolves nothing (#987, 2026-10-26)

To judge `--amend` and path commits, the doc-drift commit hook began asking `_shellwords.py commit-plan` for the
paths after `git commit` and checking only those. The reader took `2>&1` as the paths `2`, `>&`, `1`; the hook found
no relevant file among them and allowed the commit without running the linter, on half the commit calls in recent
transcripts. A path commit typed from a subdirectory and the `>|` form failed the same way. Before the change the hook
judged the index for every commit, so a misread command could not shrink the check; after it, any misread path list
narrowed it to nothing. The same review found the audit's new INERT-ROW exemption written from the brief's example
(any `<XmlName>` registering the file) instead of the engine's condition for a load (an exact requested id).
- **Why missed:** the reader was tested on the command shapes the brief listed, not replayed on real commands; the
  #985 lesson (wrapped and grammar forms in every gate's test rows) was not applied to a reader that narrows a gate.
- **Prevent:** when a parsed reading makes a gate check less (a narrower path set, a downgrade to a warning), keep a
  fallback to the old check when the reading resolves nothing, and replay the gate on real inputs (the local
  transcripts, the live install) against the base version before review.
- **Source:** 1.5.x `docs/reviews/rca-tool-987-2026-10-26.md` findings 1 to 3 and 9, on `origin/bannerlord-1.5.x` (ported by WP142).

### Removing an MCP server means every scope that declares it (2026-09-29)
Deleting the `github` entry from `.mcp.json` left the server loaded: the `github@claude-plugins-official`
plugin, enabled in `.claude/settings.json`, ships a `.mcp.json` declaring the same server. The other way
round, the `blender` and `substance-painter` servers existed but never loaded: `claude mcp add --scope
local` had stored them under the repo's old `C:` path in `~/.claude.json`, and the repo moved to `E:`.
- **Why missed:** a server's identity was read from one file, while Claude Code assembles it from the
  project `.mcp.json`, every enabled plugin, and the user and local scopes keyed by project path.
- **Prevent:** to add or remove a server, grep the plugin caches for its name as well as `.mcp.json`, and
  prove the result from inside a restarted session with `/mcp`, not from a shell in another folder.
- **Source:** 1.5.x `docs/reviews/adopt-ecc-2026-09-29.md` Step 4 and Step 6; `docs/reference/mcp-servers.md`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A test oracle must not sit downstream of anything the code under test can swap out (plan 013, 2026-09-24)
`tools/test_hooks.sh` 4c proved "this hook starts no Python" by counting starts of a fake interpreter pinned through `TAOM_PYBIN`. `_pybin.sh` probes the pin under a 0.8 s timeout and silently falls back to the real `python` when it misses, which counts nothing. Under load the check read 0 starts and blamed a prefilter byte-identical to seven that passed; the same fallback could pass a hook that does start Python. The builder saw the false failure, reran, got green, and did not record it.
- **Why missed:** the counter was designed without reading the resolver's fallback, and its premise check ran once, before the rows, so it could not see a later probe miss. A zero count looks exactly like success (repeat of "A zero you did not prove is not a zero", above).
- **Prevent:** observe the decision itself, not a side effect behind a timed or fallible step: 4c reads a `bash -x` trace for the `source` of `_pybin.sh`. When a test flakes once, find the mechanism before rerunning; a green rerun is not evidence the red one was noise.
- **Source:** 1.5.x `docs/reviews/rca-bash-hook-prefilter-2026-09-24.md` F1, on `origin/bannerlord-1.5.x` (ported by WP142).

### State a payload premise as the producer's behaviour, with its re-check, not as a property of the format (plan 013, 2026-09-24)
Eleven gates now skip parsing when the raw payload lacks `git`, which is safe only because Claude Code writes ASCII letters literally. The comments said "JSON never escapes an ASCII letter", which is false (`\u0067` is valid JSON for `g`), and the instruction to re-prove it after a Claude Code upgrade lived only in the plan.
- **Why missed:** the plan's caveat did not reach the comment text it prescribed.
- **Prevent:** name the producer and the evidence ("Claude Code writes letters literally; raw UTF-8 seen in #647"), and put the re-check where the next upgrade will find it (`docs/reference/hooks-catalog.md`, and `harness-facts.md` once free).
- **Source:** 1.5.x `docs/reviews/rca-bash-hook-prefilter-2026-09-24.md` F5, on `origin/bannerlord-1.5.x` (ported by WP142).

### Prove a before-case against the committed base, not the RED intermediate (plan 013 decisions, 2026-09-24)
The D39/D40 CHANGELOG said `git \u0063ommit -m "no label here"` passed the subject gate before the change. It passed only in the builder's intermediate tree (D39 applied, D40 not yet); the committed base, still filtering on `git`, denied it. The real base hole was `\u0067it commit`. The same entry reused a "240 cases, no changed decision" parity sentence whose script held no escaped payload, next to a bullet describing a changed decision, and two docs said 4d covers "each blocking gate" when its table has five rows.
- **Why missed:** the example came from the nearest RED log, not from a run on the base commit, and the counts came from intent, not from the table or the run.
- **Prevent:** for any "before the change, X happened" claim, run X on the parent commit's file (`git show <base>:<path>`) and quote that result. When a claim is reused after a further change, restate it against what the evidence can see. Count the rows before writing "each" or "every".
- **Source:** 1.5.x `docs/reviews/rca-bash-hook-prefilter-decisions-2026-09-24.md` R1, R2, R5, on `origin/bannerlord-1.5.x` (ported by WP142).

### Build a coverage row that fails on the mutant it exists for: it holds no other copy of the filtered word (plan 013 decisions, 2026-09-24)
Two new 4c rows passed on correct hooks and on the broken ones they were meant to catch. The escaped-word default row `git \u0063ommit` kept a literal `git`, so a `git`-filtered hook without the escape arm passed it through the word arm. The commit gates had no `git -C <dir> commit` row, so a filter narrowed to `git commit` stayed green. Planted mutants proved both: the committed suite caught neither.
- **Why missed:** each row was checked green on the code, not red on its mutant, although the plan 013 lesson above says to delete the arm a row covers. A `*)` default row serves hooks it was not written for.
- **Prevent:** before committing a coverage row, plant the one mutant it exists for and watch it fail. Build the payload so no other arm can admit it (here: escape every word any prefilter reads). Give each arm of the hook's own trigger, not only of its prefilter, a row.
- **Source:** 1.5.x `docs/reviews/rca-bash-hook-prefilter-decisions-2026-09-24.md` R3, R4, on `origin/bannerlord-1.5.x` (ported by WP142).

### A gate change that refuses less anywhere must be run against the old gate, not argued from examples (plan 027, 2026-09-24)
Plan 027 made `validate-push.sh` refuse less on purpose in two places: a trunk named only in a
trailing `#` comment, and an option value (`-o ci.skip`) taken for the remote. Each relaxation
consumed text that another stage had already reinterpreted. The comment strip ran on the
quote-blind split, so a quoted ` #` after a heredoc line holding one quote, or inside PowerShell's
typographic quotes, hid the push. The value skip ran on quote-flattened tokens, so a lost empty
value (`-o ""`) made it skip the remote. Eight shapes the old hook refused passed, under a comment
that promised the gate "never refuses less than before".
- **Why missed:** the plan and both cold reviews checked the named examples and wrote the promise
  as a property; no test fed the old hook and the new one the same inputs.
- **Prevent:** before merging any gate change that relaxes a shape, run a differential sweep: the
  old hook from `git show <base>:<path>` beside the new one over prefixes (heredoc lines with one
  quote, comments, `cd x;`), bodies and suffixes, under both tool names and on a trunk and a
  feature branch, and treat every refused-then-allowed row as blocking unless it is the named
  relaxation. A stage that drops or skips text must do it where the quotes and argument boundaries
  are still known, or judge both readings (with and without the drop) and block if either does.
- **Source:** 1.5.x `docs/reviews/rca-powershell-gate-coverage-2026-09-24.md`, findings 1 to 4, on `origin/bannerlord-1.5.x` (ported by WP142).

### A translator between two shells must cover the target grammar's statement forms, checked with its own parser (plan 027, 2026-09-24)
`_shellwords.py` read every PowerShell form in the plan's parser table and none outside it: an
assignment (`$r = git commit ...`), the `.` operator, `${env:X}`, the typographic quotes, a quoted
word closing its token, and a string or variable standing alone as a value. The assignment alone
let an unlabelled commit and an unconfirmed `reset --hard` through two gates. The same loss of the
literal-versus-command distinction made the commit gate read `pbpaste` as a commit subject.
- **Why missed:** the reader was written from a list of forms to support, not from the grammar of
  where a command can start; `[System.Management.Automation.Language.Parser]::ParseInput` was run
  only on the listed inputs.
- **Prevent:** for a reader that decides "is this a command", list every statement head the shell
  has (assignment, call operators, value statements, pipelines, blocks) and pin each with the
  shell's own parser output in the unit tests. Interpret a producer's output only when it can be
  computed (a literal echo, `printf '%s'`); anything else stays unknown, as before.
- **Source:** 1.5.x `docs/reviews/rca-powershell-gate-coverage-2026-09-24.md`, findings 5 to 7 and 11, on `origin/bannerlord-1.5.x` (ported by WP142).

### Time a hook on one long segment and on a 1 MB payload, not only on many short ones (plan 027, 2026-09-24)
The fork-free quote strip in `block-broad-git-add.sh` beat `sed` on 100 short segments and lost
badly on one 100 KB segment (2.5 s, quadratic). `validate-push.sh` started Python three times and
ran a bash split over the raw text twice, which took a 1 MB PowerShell command to 3.5 to 4.8 s of
its 5 s registration; a killed gate fails open.
- **Why missed:** the timing rows measured the shape the change optimised for.
- **Prevent:** a hook's timing rows cover many short segments, one long segment and a 1 MB payload
  under each tool, and a text transform in bash names its complexity in a comment.
- **Source:** 1.5.x `docs/reviews/rca-powershell-gate-coverage-2026-09-24.md`, findings 8 and 9, on `origin/bannerlord-1.5.x` (ported by WP142).

### A differential sweep proves only the classes in its corpus; seed it from the fix's own mechanism (plan 027 convergence, 2026-09-26)
The review fix for the quoted ` #` checked for a quote only inside each piece of the quote-blind
split, and a 5,896-shape sweep came back clean. The convergence pass then put the separator inside
the quoted value (`X="a;b #c" git push --force origin <trunk>` after a heredoc line holding one
quote): the split cut inside the value, the piece holding the push began at the `#`, and its
opening quote sat in the piece before. 28 of 612 shapes the base gate refused passed. The same pass
found the assignment fix covered two left-side spellings of the five `ParseInput` accepts.
- **Why missed:** the sweep's bodies came from the findings being fixed, so it tested the fix's
  examples; nothing in it attacked the fix's own assumption (that a piece holds its own quotes).
- **Prevent:** after a fix, name the assumption it rests on and add sweep bodies that break it (for
  a split, put every separator inside a quoted value on both sides of the checked character); for a
  grammar fix, list every form the parser's AST type admits (Variable, Convert, Member, Index,
  ArrayLiteral) and pin each.
- **Source:** 1.5.x `docs/reviews/rca-powershell-gate-coverage-2026-09-24.md`, findings 16 and 18, on `origin/bannerlord-1.5.x` (ported by WP142).

### A bash judge ported to Python reads a line differently at every natural Python choice (#680, 2026-09-26)
`validate-push.sh` judged each candidate line in bash at about 25 microseconds a word, so a trunk force push
carrying 250 KB or more of quoted text holding `push` outran the 5 s registration, and a killed gate fails open.
Plan 027's review had seen the cost and answered it with an order (lines that could force first, shortest first),
which a message holding a force-like word beats. The fix moved the judge into Python (`_pushjudge.py`, run by
`_shellwords.py verdict` in the reader's own start) under a 3.0 s deadline that asks on an overrun: the slowest
2 MB shape now answers in under a second. A security gate's port must give every verdict bash gave, and each
natural Python spelling of a bash step changes some. The #680 design audit judged 37,986 lines under three
current branches against the bash judge and counted:

| Natural Python choice | What bash does | Judgements that differ |
|---|---|---|
| `str.split()` | splits at space, tab and newline only (IFS) | 12,320 |
| `str.lower()`, or bash under a named UTF-8 locale | `${tok,,}` in the hook's C.UTF-8 lowers A-Z only | 99 each |
| `str.isdigit()` for an fd number | `^[0-9]+$` takes ASCII digits only | 14 |
| `fnmatch` for a refspec pattern | `[[ == ]]` pattern matching | 38 |

The split is the dangerous one: the reader writes `\x1f` and `\x1e` inside a word to keep an argument whole, and a
naive port let 546 refused hook runs through, the 7c row `git push --force -o "ci variable" origin` on a trunk
among them. Two more sit outside the judge: bash's `$( )` dropped every NUL and saw a lone surrogate as `?`, so a
line is judged as bash received it, and `str.splitlines()` on the candidate list breaks at an empty argument's
`\x1e`. The port reads a `*`-only pattern exactly and takes one holding any other glob character as matching every
protected name, which can only refuse more (git refuses such a refspec), instead of porting bash's bracket matcher.
- **Why missed:** the per-word cost was measured in plan 027's review and treated with an order, which any content
  can beat, rather than removed; and a bash judge's reading lives in bash's defaults (IFS, the locale, `$( )`),
  which no line of the script spells out, so a port written from the script's text passes every ASCII test.
- **Prevent:** pin each divergence with a line whose verdict was read from the bash judge's own functions
  (`tools/tests/test_pushjudge.py`: 99 pins, and every 7c command with the target bash named), prove the pins bite
  by mutating each choice back to the natural one, and switch only after a hook-level differential sweep against
  the old hook shows zero refused-then-allowed rows (1,035 commands, 4,140 runs per hook). Delete the old judge
  rather than keep it as a fallback: two readings drift. With no verdict, a coarse ask on a force marker in the
  raw payload stands in for it.
- **Source:** 1.5.x issue #680; `.claude/hooks/_pushjudge.py`, `.claude/hooks/_shellwords.py` (`verdict`), `.claude/hooks/validate-push.sh`; `tools/tests/test_pushjudge.py`; `tools/test_hooks.sh` 7e and 7i, on `origin/bannerlord-1.5.x` (ported by WP142).

### A gate that matches full option spellings misses what git accepts (#689, 2026-09-26)
`validate-push.sh` matched git's long force options by their full spelling, so every shorter spelling git accepts
read as an ignored option. git takes an unambiguous prefix of a long option as that option: on a local bare
repository with `--dry-run --porcelain` (a `+` line is a forced update), git 2.55 ran `--force-w` and `--force-with`
as `--force-with-lease`, `--mir` and even `--m` as `--mirror`, and `-f --al` and `-f --b` as a forced `--all`, each
force-updating both trunks from a feature checkout. The #680 design review found two more readings of the same
kind: the refspec `:` (every branch the remote also has, forced by a flag or written `+:`) was read as an empty
destination, that is the current branch, and so was a forced push with no refspec under `push.default=matching`,
which pushes every matching branch. The judge now compares a long option's name before any `=` as a prefix of each
option it acts on, from 3 characters; git refuses an ambiguous prefix (`--f`, `--forc`), so counting one as force
refuses nothing git would run. A forced `:` or `+:`, and a forced push with no refspec under
`push.default=matching` from `-c` or from `git config`, are refused as every matching branch. A differential sweep
against the port (1,648 commands, 555 of them seeded from these rules; 8,240 runs per hook under both shell tools,
on a trunk, a feature checkout and one set to `matching`) found 0 pushes refused before and allowed now; its 1,027
exit-code differences and 275 renamed targets are all these shapes.
- **Why missed:** the option table was written from the documented spellings and every test used them; the #680
  port then pinned the bash verdicts, this gap among them (a pin read `--force-w` as a plain push), because a port
  must change nothing; and "no refspec" was read as "the current branch", which holds only under the default
  `push.default`.
- **Prevent:** read an option the way the tool's own parser reads it, not the way its manual spells it: for git, a
  prefix match on the name before `=`, counting an ambiguous prefix as the dangerous option. Before trusting a
  gate's table, run each shorter spelling and each empty or default argument against a scratch repository and read
  what git did. Where a gate resolves a missing argument, find the setting that decides it (`push.default` here) and
  read it.
- **Source:** 1.5.x issue #689; `.claude/hooks/_pushjudge.py`; `tools/tests/test_pushjudge.py` (`ISSUE_689`, `PushDefaultTests`); `tools/test_hooks.sh` 7c, on `origin/bannerlord-1.5.x` (ported by WP142).

### Evidence for a flag comes from the same parse that raised it (2026-10-02)
`perf_runs.py` raised `MEMORY_PRESSURE` from `triage_battle_load.py`'s strict `[MemSample]` parser but chose the line to show with its own looser `memLoad=(\d+)%` scan, so the cited line could be one the flag never read; its docstring said `[MemSample]` was never re-implemented.
- **Why missed:** the evidence was added later as "find the line to print", a separate small scan beside the parse that decides the flag.
- **Prevent:** derive a flag's evidence from the parsed records that raised it (or from the same parser, line by line); a second regex for one fact is a fork that drifts.
- **Source:** 1.5.x `docs/reviews/rca-perf-runs-parser-2026-10-02.md` F6, on `origin/bannerlord-1.5.x` (ported by WP142).

### A parser rule change prints its diff per tag over the real corpus, not only for the tag that motivated it (2026-10-02)
`perf_runs.py`'s new value rule (a value runs to the next space-led `key=` outside brackets) was written for `[Doctrine]`'s lists and checked over the 30 installed logs as "rows identical outside extra_tags, 489 values kept whole". The same rule also folded `[MapLoad]`'s seven per-kind party counts into `parties` (92 values, 644 keys) and would fold `[EnlistDiag]`'s presence snapshot into `verdict`; neither the commit, the doc nor the review record said so.
- **Why missed:** the comparison counted the change where it was expected and summed the rest into "identical rows"; nothing printed which other tags' fields moved.
- **Prevent:** when a parsing rule changes, run the old and new parser over the real corpus and print values changed, keys dropped and keys added per tag and key; every tag in that list is either intended (and pinned by a test on its real line) or a bug. Check producers on unmerged branches too when the rule is generic (plan 040's `phase=GameInit total`).
- **Source:** 1.5.x `docs/reviews/rca-perf-runs-parser-2026-10-02.md` R2, on `origin/bannerlord-1.5.x` (ported by WP142).

### Every branch where an audit tool degrades writes a reason row, and a STOP check reads the whole output (2026-10-02)
Plan 038's battle equipment audit had a reason-code table, a run-log line per code and a test that every code it emits has a consequence, yet 82 loose textures counted 0 bytes with no line: the size fallbacks it inherited from the map tool set only flags (`HEADER_UNDECODED`, `NO_PIXEL_DATA`) in one TSV column. An empty selection exited 0 the same way, and a troops-file error crashed before `run.log` said why. The plan had made an undecoded loose header a STOP; the executor checked it on one sampled row.
- **Why missed:** the consequence test proves the table covers the codes the source emits; it cannot see a branch that emits none. A flag reads like reporting but never reaches the log, and a sampled STOP check passes whenever the sample is a good row.
- **Prevent:** list every branch where the tool substitutes, skips or sizes by formula, and every input that can come out empty or unreadable, and give each a reason row or an abort line with exit 2 (`TEXTURE_SIZE_FROM_HEADER`, `TEXTURE_SIZE_UNKNOWN`, `abort reason=NO_TROOPS_SELECTED`). Run a plan's STOP condition as a query over the whole output (`awk` over the TSV's flags column), not on an example row.
- **Source:** 1.5.x `docs/reviews/rca-battle-equipment-memory-audit-2026-10-02.md` R1, R4, R7, R15, on `origin/bannerlord-1.5.x` (ported by WP142).

### When one index keeps two maps over the same items, every consumer resolves through the map that carries the precedence rule (2026-10-02)
`AssetIndex.add_item` swaps its name map to the `EmAssetPackages` copy of a Native texture that holds the pixels, but its guid map keeps the first copy it saw, the `AssetPackages` stub with the same guid. The map tool resolves textures by name; the battle tool resolved them by guid through a material, so 338 textures named the stub's pack and carried a false `PIXELS_NOT_IN_PACKS`. Every byte check passed, because the header formula equals the pixel segment.
- **Why missed:** the replacement rule lives in one map; the second consumer reused the other map, and a byte oracle cannot see a wrong source that yields the right number.
- **Prevent:** when a shared index holds two lookups over one item set, read `add_item` (or its equivalent) for any rule that touches only one of them, and resolve through that one, or re-resolve (`index.get(kind, item.name)` when the guid matches). Check metadata columns (pack, flags), not only bytes, against a second reader.
- **Source:** 1.5.x `docs/reviews/rca-battle-equipment-memory-audit-2026-10-02.md` R2, on `origin/bannerlord-1.5.x` (ported by WP142).

### A tool that writes into a folder it may reuse deletes its own previous outputs first (2026-10-03)
Plan 038's audit wrote seven files into one folder, `sides.tsv` only when sides were given. A run without sides left the previous run's `sides.tsv` beside fresh tables, and a run that aborted left the whole earlier set beside a `run.log` that said it had failed. The report and the log described the current invocation correctly, so a careful reader was safe; anything that read a TSV alone read another run's numbers. Every test built its fixture in a fresh folder.
- **Why missed:** a first-run test cannot see a stale file, and the optional output (`sides.tsv`) and the abort path were each tested only for the transition from nothing.
- **Prevent:** for every tool that writes into a configurable folder, delete the tool's own previous outputs by name after the refusal checks and before the log opens (never the folder's other files; `_remove_previous_outputs`), and test the second run: one with fewer outputs and one that aborts, the second also checking the log holds only its own run.
- **Source:** 1.5.x `docs/reviews/rca-battle-equipment-memory-audit-2026-10-02.md` Codex pass, X3, on `origin/bannerlord-1.5.x` (ported by WP142).

### A plan's precedence choice is checked against the repository's engine record, and models the engine's unit of choice (2026-10-03)
Plan 038 specified packed-first for `--loose-assets` (the `AssetPackages` copy wins, the loose one is listed as also-in), and the executor built and tested exactly that. `docs/reference/armory-guide.md` had recorded since 2026-09-01, from the engine's own log, that the engine loads a module's `Assets` tree and ignores the cooked packs it also ships, and that art deleted from `Assets` is broken in game whatever a stale pack holds. The first review filed the conflict as a LOW decision for the maintainer, Codex raised it again, and the decision settled on the engine. Reversing the iteration order would not have been enough: it would still have kept names only a stale pack holds. The engine chooses per module tree, so the index now does too.
- **Why missed:** a plan choice reads as a requirement to the executor and as a decision to the reviewer, and nobody grepped the guide for the rule it contradicted.
- **Prevent:** when a plan fixes a precedence or fallback order for an offline model of the engine, grep `docs/reference` and the lessons for that rule before building, and name in the plan the source that settles it. Model the engine's unit of choice (a module's tree), then record what the model skipped as a reason row (`COOKED_TREE_NOT_READ`).
- **Source:** 1.5.x `docs/reviews/rca-battle-equipment-memory-audit-2026-10-02.md` Codex pass, X4, on `origin/bannerlord-1.5.x` (ported by WP142).

### A generator that touches a hand-edited file may only add to it, and the proof is a byte comparison (#959, 2026-10-21)
`tools/complete_lords_xslt.py --apply` rebuilt `lords.xslt` from vanilla and what it parsed back, and the result was not the
shipped file: 389 `<Equipments>` blocks and 19 `race` attributes dropped, five skills and Traits blocks in three lords reflowed,
one attribute reordered, an explicit `is_female="false"` removed, and the BOM and the final newline lost. The tools that wrote the
Equipments blocks (`assign_xslt_lord_equipment.py`) were retired in `19b5d77c5`, so the blocks had become hand-owned data the
generator never read. #758 and #957 each fixed only the block they had just added. The symptom (`race`) was named in two docs
(`lords-and-heroes.md`, `lord-identity-reconciliation.md`), never in a lesson.
- **Why missed:** each fix checked its own block (`assertIn` of a constant) and never asked whether the whole regeneration equals
  the file. A first answer, a generator that read every unmodelled part back, grew a refusal guard and a parse-back of each shape
  of hand data, and still needed a new case for every shape nobody had modelled.
- **Prevent:** a tool that touches a hand-edited file only adds to it. `--apply` inserts a template for each vanilla lord the file
  lacks, after its section's last template, and never rewrites a byte that is already there; it keeps the BOM and the dominant
  line ending (a file with mixed line endings comes back with its dominant one) and parses before writing. A hand block in the file is therefore safe without a copy in the generator. Prove an
  `--apply` by reading the file and `git show HEAD:<path>` as bytes (converted to the worktree's line endings) and comparing
  them: `git hash-object` and `git diff --stat` cannot see line endings under `core.autocrlf`. Contract: `tools/README.md`.
- **Source:** 1.5.x issue #959; `tools/tests/test_complete_lords_xslt.py`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Explain a generator's unexpected diff before you discard its output (#886, 2026-10-18)
`tools/snapshot_api_surface.ps1` regenerated `patch-targets.md` with one row nobody expected, so its output was thrown
away and the header was set by arithmetic. The row was a real omission: commit `2410aec51` added two Diplomacy patch
classes but only one row, which also kept that target out of engine-bump body diffs.
- **Why missed:** an unexpected line in generated output was read as generator noise.
- **Prevent:** when a generator's output differs from what you expected, find the cause of each extra or missing line
  before you discard it (grep the code for the class, `git log -S` for the commit). Take the generated content when it is
  right, and restore only what the generator does not own (for example the backlinks footer).
- **Source:** 1.5.x `docs/reviews/rca-castle-recruitment-seam-2026-10-18.md` finding 9, on `origin/bannerlord-1.5.x` (ported by WP142).

### A change to how a gate behaves updates every doc that runs or reads it (plan 008, 2026-09-24)
Plan 008 changed the binding gate's command and added two red forms. Three consumers were left behind. `reflection-sites.md` still gave the old command, because the sweep grepped only the `TestCategory=BindingVerification"` spelling. The skill's triage line still said "a red gate is a real finding, one of three classes". The skill claimed "every gate test" goes Inconclusive without the game, while the executor's own log showed 33 of 368 passing.
- **Why missed:** each claim was checked against the diff, not against the files that consume the gate or the run that measured it.
- **Prevent:** grep for the command's stem (`BindingVerification`, the test class names) rather than one filter spelling. When a change adds a failure message, update the table that tells an agent how to read a failure. Before writing a quantifier ("every", "all", "never") about a gate, check it against the measured counts in the run log.
- **Source:** 1.5.x `docs/reviews/rca-binding-gate-no-silent-skips-2026-09-24.md` F4, F5, F7, on `origin/bannerlord-1.5.x` (ported by WP142).

### Prove a gate on the states it will meet, not the one it was written in

The five #1020 gates all passed on the worktree that built them and failed on states a later run
meets. A ShellCheck rc file written LF became CRLF on checkout (the third LF-only-by-accident file,
after the hooks and the Howdah snapshot), a new script was not linted until staged, a new Python
folder escaped a hand-kept list, an unchanged checkout skipped the NuGet audit, and a history scan
run from a subfolder or outside the repo reported "no leaks" with exit 0.
- **Why missed:** each gate was run once, on its author's state: every file tracked, LF and in a
  known folder, a full restore, the repo root as working directory.
- **Prevent:** before review, run a new gate on a fresh checkout (`git check-attr text eol` on each
  file its tool reads), an untracked new file, a file deleted but not removed, a non-ASCII path, a
  run from a subfolder, a missing tool, and an empty input (a tool that finds nothing to check must
  not read as a pass). Prefer passing a tool's options on the command line over a tracked config
  file that a CR-intolerant tool reads.
- **Source:** 1.5.x `docs/reviews/rca-quality-gates-2026-10-26.md` findings 1 to 4, 6 and 11 (#1020), on `origin/bannerlord-1.5.x` (ported by WP142).

### A review round is every lens in scope; stopping after the first wave is a partial review (Race Abilities, 2026-10-04)
Round 1 of the Race Abilities review ran the defect wave (standards, engine compatibility, data flow, XML) and stopped. Efficiency, completeness and design never looked, and round 2's efficiency lens then found a HIGH per-frame allocation and four MEDIUM hot-path costs in code round 1 had passed.
- **Why missed:** the first wave's findings were fixed and the round was treated as done; the deep-review table runs completeness and design always and efficiency whenever C# changed.
- **Prevent:** run every lens the table puts in scope, in waves of four. When a round must stop early (a usage limit, Mike's call), its RCA names the lenses that did not run, and the next round starts with them.
- **Source:** 1.5.x `docs/reviews/rca-race-abilities-2026-10-04.md` R20 to R24, "Root-cause pattern 2", on `origin/bannerlord-1.5.x` (ported by WP142).

### Count a scripted attack by a unique blow fingerprint; a landed blow never proves its animation played
`[BlowDiag]` lines carry only `attackerIdx`, and the war ram's attack task applies damage in the same tick it plays
the action. The ram's `AttackBlowMagnitude` (35) is the only such constant in `Main/`, so `mag=35 missile=False` counted
every head-butt (1,300 in one test, 1,005 in the next) and, grouped by attacker and second, the victims per butt.
- **Why missed:** "it fired" and "it played" were read as the same fact; the head never dropped in 2,305 firings.
- **Prevent:** before reading a count, grep the source for the constant to prove the fingerprint is unique. A log keeps
  growing while the battle runs, so rerun every count on the same snapshot before comparing two of them. Attribute an
  engine warning by its timestamp against the edit and import times: `rgl_log_<pid>.txt` accumulates a whole session.
- **Source:** 1.5.x #618, 2026-09-18 (`docs/features/war-ram.md` "Verifying from the logs"), on `origin/bannerlord-1.5.x` (ported by WP142).

### `r.pos += r.take(...)` drops the advance that `take` itself makes
A tpac header parser skipped each item's metadata with `r.pos += r.take("<Q")`. Python evaluates the attribute on the
left of an augmented assignment before it calls the right side, so the stored position was the old one plus the
length: the 8 bytes `take` had just consumed were lost and every later field read shifted. The unit tests over a
synthetic package failed on their first run; the scratch prototype had used a local variable and never showed it.
- **Why missed:** the line reads as "skip the length-prefixed block", and nothing about it looks order-dependent.
- **Prevent:** read a length into a local first (`n = r.take(...)`, then `r.pos += n`); never put a call that moves
  a cursor on the right of an augmented assignment to that same cursor. Better still, reuse the tested container code:
  `tools/tpac_clone_metamesh.py`'s `parse` and `serialize` round-trip a real package byte for byte, and the new tool
  moved onto them the same day instead of carrying another tpac parser in `tools/`.
- **Source:** 1.5.x `tools/skeleton_hit_capsules.py`, `tools/tests/test_skeleton_hit_capsules.py` (2026-09-18), on `origin/bannerlord-1.5.x` (ported by WP142).

### Parallel builders: pin the conventions, not only the shared API (repeat, Shadowfax Screens 2026-10-19)
Four builders built four screens from one plan that pinned the timer API, the settings provider, the
module and the MCM group. Each was correct against that plan, and they still diverged on six
conventions: test category tags (five IL tests untagged in two screens), the policy for an unreadable
setting (on in two places, off in two), one category constant (15+ repeated literals), the open line
format, what the timer window spans (two screens included the movie load), and the wiring test setup
(four copies, one of them leaking a facade).
- **Why missed:** the 2026-07-02 rule covers shared sub-problems such as id normalisation; conventions
  did not look like sub-problems, so the brief did not list them.
- **Prevent:** the pre-dispatch checklist in `docs/ai-includes/agent-teams.md` now names conventions.
  Before dispatch, write in the brief the failure policy, the test tags, the constant, the log formats,
  the timing span and the wiring-test shape every sibling uses.
- **Source:** 1.5.x `docs/reviews/rca-shadowfax-screens-speed-2026-10-19.md` findings 1, 2, 11, 13, 16; earlier `rca-combat-mechanics-2026-07-02.md`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Price a per-call cache with a micro-benchmark before you build it (Shadowfax Screens 2026-10-19)
The plan called two caches "clean wins": a memo for two concept lookups per inventory row and a count
cache for the party screen's item-category walk. Measured on the game's runtime afterwards, they saved
about 0.7 microseconds per row and 0.1 to 1 ms per open, against a 16 ms keep rule, and carried a
transpiler, a window protocol and about 500 lines with tests. Both were deleted.
- **Why missed:** the plan reasoned from "vanilla repeats this work" without the cost of one repeat.
- **Prevent:** before a cache or memo goes into a plan, time one vanilla call and one cache hit on the
  game's runtime (a 20-line benchmark in the scratchpad) and multiply by the calls per action; keep it
  only if the product can clear the feature's own threshold.
- **Source:** 1.5.x `docs/reviews/rca-shadowfax-screens-speed-2026-10-19.md` findings 4, 5, on `origin/bannerlord-1.5.x` (ported by WP142).

### A tool's regression test covers the boundary shapes and compares whole output (harvest tool, 2026-10-19)
`harvest_literal_loc_keys.py` added one blank line before `</strings>` per run. The first regression test started
every case with exactly one blank line, the one shape where a wrong fix that overwrites the row above `</strings>`
behaves like the right one, and it checked a few properties instead of the text. The same bug was also filed three
times (#781, #919, #922), twice in one afternoon by two sessions.
- **Why missed:** the test was written from the bug report's single example; nothing told `/issue` to look for an
  existing issue first.
- **Prevent:** a tool test runs each boundary of the input (none, one, many; LF and CRLF; every real file shape) and
  asserts the whole expected output, and is shown to fail on the old code and on one plausible wrong fix. Before
  filing an issue, search open issues for the file and symptom (`gh issue list --search "<file> in:title,body"`).
- **Source:** 1.5.x `docs/reviews/rca-harvest-blank-line-2026-10-19.md` rows 2 and 3, on `origin/bannerlord-1.5.x` (ported by WP142).

### A finding that repeats a lesson means the brief did not carry it (#769 E8 to E10, 2026-10-14)
The E8 to E10 review found seven defects that match lessons already on file: a service locator in a hook, an
outcome keyed on one path, an owed in-game check with no log line, one predicate made to answer a second
question, a new filter that an old reader missed, a sentence built from fragment keys, and tests that could
not fail. The builder had not read
the lesson files, and the orchestrator's brief had not named them.
- **Why missed:** a brief names files, steps and seams. The lessons live in 13 cross-cutting category files
  (the five this change repeated hold about 8,000 lines), and nothing in the spawn path opens them.
- **Prevent:** the Feature Developer template (`docs/ai-includes/agent-teams.md`) has a `Lessons:` slot. The
  lead fills it with the lesson headings that apply, by name and file, and the builder's report says what it
  did about each. A review that finds a repeat checks the brief first.
- **Source:** 1.5.x `docs/reviews/rca-career-e8e10-2026-10-14.md` pattern 1, on `origin/bannerlord-1.5.x` (ported by WP142).

### A fix that removes a false positive must keep what the old code caught by accident (#947, 2026-10-23)
`generate_new_faction_kingdoms.py` deleted every copy of its marker region and re-appended one, so a block added after it read as drift. The first in-place fix replaced only the first region, and that also silenced the case the strip-all code had caught: a stale second copy, which the engine loads last and so wins. Two more re-reviews found the same hole in other shapes (an orphan END, a missing closing tag).
- **Why missed:** the fix was tested against the one false positive it removed, not against the damage the old behaviour happened to report.
- **Prevent:** before you narrow what a gate or a writer matches, list every input the old code rejected or changed, and keep a test that each one still fails. For a marker region, count BEGIN and END and refuse anything but zero or one complete region.
- **Source:** 1.5.x `rca-ui-data-sweep-2026-10-23.md` findings 1 to 3, on `origin/bannerlord-1.5.x` (ported by WP142).

### Count the change before you call the cause (Carn Dûm map icon, 2026-10-16)
A 47k-triangle candidate had 943 UV charts; the guess was split vertices on its Tripo seams (27,675 vertices against an
expected 23,600), and a weld was added. It merged 0 of 27,675 vertices there and 24 of 930,709 on the other mesh.
- **Why missed:** the vertex-to-triangle ratio of a single closed shell was applied to a mesh made of many shells.
- **Prevent:** log the before and after count of any repair and remove it when it changes nothing; the weld was
  deleted in the same session.
- **Source:** 1.5.x the prep log lines `[weld] verts 27675 -> 27675` and `930709 -> 930685`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A preview must not assume every optional bake exists (Carn Dûm map icon, 2026-10-16)
`blender_prep_tripo_prop.py` crashed in its preview after a successful export, because a basecolor-only Tripo source
skips the roughness and metallic bakes and the preview loaded them unconditionally.
- **Why missed:** every earlier source shipped all four Tripo maps.
- **Prevent:** a consumer of optional outputs checks each one exists; the preview now skips what was not baked.
- **Source:** 1.5.x `build_preview_material` in `tools/oneoff/blender_prep_tripo_prop.py`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Scratchpad scripts are not a workflow (Carn Dûm map icon, 2026-10-16)
The first Painter pass ran from session-scratchpad scripts with one asset's paths written in, so nobody could repeat it.
- **Why missed:** each script began as a probe and grew into a step.
- **Prevent:** once a probe becomes a step, move it into `tools/` with its inputs as arguments, then re-run the whole
  chain from the repo copies and diff the output against the scratch run (here a mean difference under 0.25 of 255).
- **Source:** 1.5.x `tools/painter/`, `tools/blender/probe_fbx.py`, `render_pbr_preview.py`, `compare_prop_candidates.py`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Calibrate a scripted colour against its export (Carn Dûm map icon, 2026-10-16)
Four paint rounds came out lighter than written. A Painter fill takes the numbers as linear values whatever the
`Color`'s tag says, so 0.11 stone exported at about 0.37.
- **Why missed:** the `Color` object reported a correct conversion, and Blender's tone mapping and a broken normal map
  hid the albedo, so each round changed the recipe instead of testing the value.
- **Prevent:** before tuning colours, export one flat fill of a known value and read the PNG (0.2 came back 0.486,
  which is sRGB(0.2)). Judge paint by the exported albedo, measured or rendered emission-only, never by a lit render
  alone. Label a measurement by its colour space: `image.pixels` of an 8-bit PNG is sRGB, not linear.
- **Source:** 1.5.x `tools/painter/painter_stack_angmar_fortress.py` `rgb()`; the traps table in `docs/reference/substance-painter-mcp-setup.md`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Margins, not distortion, limit a hard-surface UV layout (Carn Dûm map icon, 2026-10-16)
Smart UV Project cut the icon's towers set into about 5,000 islands a few pixels wide; with their margins they covered
5% of the tile. One island per part (a box as a cross, a spike as one strip) covered 51%.
- **Why missed:** one run's stat counted the stacked hidden islands at full size and showed coverage above 1.0, which
  read as overlap, not as margin waste.
- **Prevent:** report visible islands and visible px/m per set, and read the layout image before tuning anything else.
- **Source:** 1.5.x `tools/blender/unwrap_texture_sets.py` `net_seams`; the table in the pipeline doc's rebuild variant, on `origin/bannerlord-1.5.x` (ported by WP142).

### Stage a file two sessions append to by hunk, and read the staged diff before committing (2026-10-16)
A port's lessons commit staged `docs/reviews/lessons/data-content-cultures.md` whole. A concurrent session had appended
its own lesson (Erebor dresses) to the same file, so that lesson went to origin with a gate its session had not yet
committed.
- **Why missed:** `git add <file>` is right for a file only one session touches; lesson files, `tools/README.md` and
  `docs/INDEX.md` are appended by every session. The staged diff was not read before `git commit`.
- **Prevent:** for a shared file, stage only your hunks (build the index blob from `HEAD` plus your edit and
  `git update-index --cacheinfo`, since `git add -p` is interactive), then read `git diff --cached` for every staged
  file before committing. A heading that is not yours in that diff stops the commit.
- **Source:** 1.5.x `rca-keyforce-harad-javelins-2026-10-16.md` second review, finding 5, on `origin/bannerlord-1.5.x` (ported by WP142).

### A guard that refuses must run before the first write, and cover every block the tool rewrites (2026-10-16)
`generate_starter_kit.py` gained a refusal for marker blocks holding another author's rows. It ran inside the
stylesheet step, after the item files and the pieces block were already written (a refused revert had deleted both
`starter_kit.xml` files), and it did not cover the pieces block at all.
- **Why missed:** the guard was added where the reported block is rewritten, and only for the kind of block that
  had gone wrong; the tool's older shrink guard, a pre-flight, was the pattern to follow.
- **Prevent:** a tool that writes several files computes and parses every new text first and writes last; a new
  refusal goes into that pre-flight. List every block the tool rewrites or deletes and test the guard on each, with
  a byte snapshot of the whole tree after the refusal.
- **Source:** 1.5.x `rca-keyforce-harad-javelins-2026-10-16.md` second review, findings 1 and 2, on `origin/bannerlord-1.5.x` (ported by WP142).

### A cap of zero must be a cap, most of all on paid calls (2026-10-16)
`translate_with_claude.py --max-entries 0` meant "no cap" (`if args.max_entries and ...`), and a negative value
sliced all but one entry into the paid phase. Someone deferring translation would reach for exactly those values.
- **Why missed:** a truthiness test on an int treats 0 as unset; no test covered the boundary.
- **Prevent:** test a limit with `is not None`, reject negatives, and pin 0 and -1 with tests that patch the provider
  call to raise. For "do the free half only", add a flag that returns before the paid phase (`--seed-only`).
- **Source:** 1.5.x `rca-keyforce-harad-javelins-2026-10-16.md` (D6 and the second review), on `origin/bannerlord-1.5.x` (ported by WP142).

### Prove a gate on a tree it cannot read, not only on the fault it was built for (#1026, 2026-10-26)

The text-processor gate skipped a file it could not parse, passed when it found no Native files, passed a release channel when the path was wrong, and its tests stayed green when the UTF-16 path or the exit code was deleted. This is the third gate on 2026-10-26 to pass on input it never read (after #1020 and #1017).

- **Why missed:** the gate was proven on the live install that showed the fault, and its pass test for the release step was the absence of a FAIL line, which a run that never starts also meets.
- **Prevent:** read inputs the way the consumer (the engine) reads them; exit 2 on an unreadable file, an empty reference set or zero files checked; give a release check a PASS line and an exit code with one meaning; kill a mutant of each guard (skip the parse, drop the exit code) before trusting the tests.
- **Source:** 1.5.x `rca-text-processors-2026-10-26.md` findings 3 to 5, on `origin/bannerlord-1.5.x` (ported by WP142).

### Run the docs lint in the mode the hook runs: --fail-on-drift (2026-09-25)
A new trap row was 197 characters (cap 180). `python tools/lint_docs.py` in report mode printed it among eight
context-budget lines while the builder read only the dash count; `--fail-on-drift`, which the commit hook and CI run,
exits 1 on it.
- **Why missed:** the report mode exits 0 whatever it finds.
- **Prevent:** before a commit that touches docs, run `python tools/lint_docs.py --fail-on-drift` and read the exit code.
- **Source:** 1.5.x `docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md` finding 3, on `origin/bannerlord-1.5.x` (ported by WP142).

### Regenerating a generated file encodes every input's working-tree state, another session's included (2026-09-25)
`generate_name_localization_strings.py --apply` rebuilt `taom_troop_name_strings.xml` from `troops/*.xml` as they stood
on disk, so the file picked up another session's uncommitted Gondor renames and dropped a troop that session deleted;
the translator then re-translated 75 language rows to those names. Committed alone, 11 languages would name troops the
committed English does not.
- **Why missed:** the regeneration's own diff looked right, and its inputs were not compared with HEAD.
- **Prevent:** before regenerating a shared generated file, `git status` its inputs; if another session has them
  modified, commit the regenerated file with that session's work or `/commit-split` the hunks.
- **Source:** 1.5.x `docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md` finding 9, on `origin/bannerlord-1.5.x` (ported by WP142).

### When a finding reverses a documented claim, grep the claim's key terms, not one phrasing (2026-09-24/25)
On 2026-09-24 the builder corrected "the human set plays on the re-framed rig" in eight docs and missed a ninth,
`troll-race.md`'s recipe step "Human clips need no retarget on a re-framed rig", worded differently.
- **Why missed:** the sweep searched for the sentence as first written.
- **Prevent:** grep the nouns of the claim together (here `re-framed` with `human clip`, `retarget`, `no retarget`)
  across `docs/`, the CHANGELOG and tool docstrings, and read every hit.
- **Source:** 1.5.x `docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md` finding 5, on `origin/bannerlord-1.5.x` (ported by WP142).

### A generator reads every fact the runtime owns from the runtime's source (#769 C1, 2026-10-15)
The career forks generator parsed the effect enum from C#, but kept hand copies of which types are flat,
class-filtered and mask-gated, which sign helps the player, and which troops pay upkeep. Each copy was wrong
or drifted while it was under review: TroopMorale priced as a count, penalties priced as perks, a new
class-filtered type refused, and upkeep counted by a field the runtime calls documentation only.
- **Why missed:** a rules file feels like configuration, so facts the code decides were typed into it.
- **Prevent:** before a generator writes a rule into its own config, ask who decides it at run time; if the
  answer is C# or data, parse that source at run time and fail with exit 2 when it cannot be found. Keep only
  design choices (units, budgets, caps, bans) in the rules file.
- **Source:** 1.5.x `docs/reviews/rca-career-forks-generator-2026-10-15.md` pattern 1, on `origin/bannerlord-1.5.x` (ported by WP142).

### A data rule enforced in one writer is not enforced: list every tool that writes the file (2026-10-26)
The fix taught `translate_with_claude.py` to skip English-only overrides. `rebuild_translation_files.py` and
`generate_translation_template.py` also write the language files, and the translator guide prescribes both; each wrote
all 177 deleted rows back, from the cache or as English that the translator then filled.
- **Why missed:** the fix was tested against the tool it changed, not against the file it protects.
- **Prevent:** before relying on a rule about what a data file may hold, grep `tools/` for every writer of that file
  and route all of them through one shared function, with a test per writer that runs on a synthetic tree. Prune any
  cache that would let a bypassing writer restore the data for free.
- **Source:** 1.5.x `docs/reviews/rca-english-only-overrides-2026-10-26.md` finding 1, on `origin/bannerlord-1.5.x` (ported by WP142).

### A validator states an engine verdict only from the engine's own read path

`tools/validate_xml_schemas.py` was written to say how the engine loads a file. Its first NATIVE line said "loaded
through project.mbproj row X" for any row, but the engine loads a row only when a caller requests its exact id
(managed `GetMergedXmlForNative`) or when native code reads its `type`; a dead id printed "loaded". Its first
unloadable-manifest check said "the engine throws at startup" whenever Python failed to parse the file, but the engine
reads `project.mbproj` and `language_data.xml` as decoded text and ignores the declared encoding, so a file the tool
called fatal loads in the game. Both repeat the class #980 was opened for: the tool described a load from the data's
shape or from its own parser instead of from the engine's consumer.
- **Why missed:** the brief described one loader, and the builder verified the parsing it wrote, not the engine call
  that decides whether anything is read; "Python cannot parse it" was taken as "the engine cannot".
- **Prevent:** before a tool prints that the engine loads, ignores or rejects a file, name the engine method that
  decides it and read it (`pwsh tools/taom-src.ps1 path <Type>`); when the tool's parser is stricter or looser than
  the engine's, either parse the way the engine does or word the line as the tool's own verdict. Replay one edge case
  on the engine's real reader when the two might differ.
- **Source:** 1.5.x `docs/reviews/rca-schema-check-2026-10-23.md` finding 1 and C1, on `origin/bannerlord-1.5.x` (ported by WP142).

### A gate is tested through the thing that runs it, not only as a function (#984, 2026-10-23)

The RCA FOLLOW-UP check in `lint_docs.py` was built test first and passed every test, but the commit hook that runs
`--drift-only` only starts the linter when a staged path is on its trigger list, and `docs/reviews/` was not; an RCA-only
commit (9 of the last 60 RCA commits) never reached the check. The hook also judged the working tree, so another
session's untracked draft could block a commit, and its deny message extracted named sections only, so the new failure
came with no detail.
- **Why missed:** the brief named the linter, not its consumer; `tools/test_hooks.sh` section 5d (the trigger list
  covers what the gate checks) exists for exactly this, after the #647 review, and was not extended.
- **Prevent:** a brief for a new gate names every consumer (the hook, the skill step, `/verify`) and asks for one test
  through each: the hook fires for the gate's own file paths, judges what the commit records, and quotes the failure.
- **Source:** 1.5.x `docs/reviews/rca-review-followups-2026-10-23.md` findings 2 to 4, on `origin/bannerlord-1.5.x` (ported by WP142).

### Give a scratch transform a timeout (2026-10-20)
A read-only XSLT transform a review agent started hung for more than four hours and held `spclans.xslt` open, which blocked a later data fix with `PermissionError`.
- **Why missed:** the script had no time limit and nothing reported it as stuck.
- **Prevent:** run scratch transforms through `timeout` (or `Start-Process` with a wait limit), and when a repo file will not open for writing, look for this session's own stale processes first.
- **Source:** 1.5.x `docs/reviews/rca-followups-949-952-2026-10-20.md`, process note, on `origin/bannerlord-1.5.x` (ported by WP142).

### Before you add a gate, prove no gate already catches the case (#974, 2026-10-23)
A review said nothing caught a repeated clan, hero or kingdom id, and the claim went into an RCA, a lesson, two docs and an issue, then three new duplicate-id schemas. The next review showed `SCHEMA_INVALID` already blocked it at commit: the engine XSDs declare those ids unique (`xs:unique`), and the hook validates the registered files against them. The schemas only reported each duplicate twice and were removed.
- **Why missed:** the claim was relayed from a reviewer without a probe; the schema JSON files looked like the only duplicate-id source.
- **Prevent:** before writing a new check, break a copy of the real file the way the check targets and run the existing gate (`validate_moduledata.py`, which includes the engine XSD layer). Write the gate only if that run passes. Pin an existing protection with a test instead (`EngineIdUniquenessTests`).
- **Source:** 1.5.x `rca-ui-data-sweep-2026-10-23.md` follow-up batch, on `origin/bannerlord-1.5.x` (ported by WP142).

### An absence check must run where the thing lives, in the form it is stored (plan 005, 2026-09-24)
Plan 005's port reported a vendored BUTR credential "already gone from disk" after
`grep -rln packageSourceCredentials Dependencies/` found nothing. The grep ran in a worktree, where
the gitignored `Dependencies/.vendor-source/` does not exist, and grep cannot read inside the
`.tar.gz` archives that folder actually holds; three of them still carry the block. The new
checklist line shipped with the same blind spot.
- **Why missed:** a "no match" was read as proof of absence, although the check could not have
  matched: wrong tree, wrong file form. The sprint record (`plans/README.md`) already named the
  three tarballs.
- **Prevent:** before claiming something is absent, prove the check can see it: run it where the data
  lives (the main checkout or the live install, not a worktree, for anything gitignored) and on the
  stored form (`tar -xzOf <a> | grep`, `zgrep`, or extract a `.zip`/`.nupkg`). Seed a known hit when
  you can. Cross-check the claim against the plan's own status row before writing it.
- **Source:** 1.5.x `docs/reviews/rca-security-hygiene-2026-09-24.md`, findings 1 and 2, on `origin/bannerlord-1.5.x` (ported by WP142).

### Code carried in a string is only tested by running it (plan 005, 2026-09-24)
`tools/process_faction_map.py` runs two child programs held in `"""..."""` literals. Plan 005
verified its injection fix with `python -m py_compile`, which parses the outer file and never the
children, and called a behaviour test "structurally untestable". A short stdlib test (a synthetic
PNG under folders named `it's here` and `a'+...+'`) fails on the old tool and passes on the fix.
- **Why missed:** "it deploys assets" was taken for the whole tool; the helpers write only to paths
  they are given, so a temp folder isolates them.
- **Prevent:** when a fix touches code built as a string (`python -c`, `eval`, templated SQL or
  XSLT), the proof is a test that runs it, including the hostile input the fix targets. Before
  writing "untestable" in a plan or a `Not-tested:` trailer, name the side effect that prevents a
  temp-folder test.
- **Source:** 1.5.x `docs/reviews/rca-security-hygiene-2026-09-24.md`, finding 3, on `origin/bannerlord-1.5.x` (ported by WP142).

### Write a documented search pattern in `-E` form (plan 005, 2026-09-24)
The adoption checklist's `grep -rln "a\|b"` works in GNU grep, but ripgrep (the Grep tool) reads
`\|` as a literal pipe, so the same pattern pasted there reports a clean tree.
- **Why missed:** the command was written for one tool and read as portable.
- **Prevent:** write alternation in documented commands as `grep -E 'a|b'`, which GNU grep, ripgrep
  and the Grep tool read the same way.
- **Source:** 1.5.x `docs/reviews/rca-security-hygiene-2026-09-24.md`, finding 6, on `origin/bannerlord-1.5.x` (ported by WP142).

### A transcript count needs the unit its claim is about, read from real records (harness retro, 2026-10-21)
`tools/harness_retro.py` called three failed results in a row a "runaway retry", and called a gate
with no deny "no fires, check before removing". Both were wrong on real data: the results of one
assistant message's parallel calls sit in consecutive records, so six Edits sent at once read as a
loop of six (18 runaways became 3), and three gates can only ask, which leaves no trace when
approved, so they could never count as firing. The schema had been pinned record type by record
type, and every test passed on fixtures that had one call per message.
- **Why missed:** the fixtures modelled each record's shape, not how records group into the event
  the signal claims (a retry is a new message; a fire is any decision, not just a deny).
- **Prevent:** before a transcript signal backs a proposal, read three real instances behind it
  and name the unit it counts (message, tool call, session); a gate's "no fires" counts only when
  its code can deny or block.
- **Source:** 1.5.x harness-retro-2026-10-21.md, code review findings, on `origin/bannerlord-1.5.x` (ported by WP142).

### A first-word anchor is not a command reader: wrappers and shell grammar hide the call (#985, 2026-10-23)
`block-dangerous-git.sh` and `block-broad-git-add.sh` allowed `timeout 60 git reset --hard`, `{ git reset --hard; }`, `! git ...`, `then git ...` and seven more forms; `check-commit-subject-version.sh` allowed a wrapped unlabeled commit; `mark-verification-run.sh` never marked `timeout 600 dotnet test`, a form the local transcripts hold 372 times. Each hook found the command by its segment's first word after skipping `VAR=value`, each with its own loop (D41 was the same bug in the marker's copy). Found while reading rtk-ai/rtk's permission matcher, which reads past wrappers and grammar.
- **Why missed:** plan 027 gave every gate one reader for the command text, but each gate still found the command word itself, and the test rows only held bare `git` forms.
- **Prevent:** a gate reads segments from `_shellwords.py commands` and a parsed call through `_git_call`, which share `command_index`. A new gate's test rows include a wrapped form and a grammar form for each case it gates (`hook-authoring.md` forms table).
- **Source:** 1.5.x adopt-rtk-2026-10-23.md; issue #985, on `origin/bannerlord-1.5.x` (ported by WP142).

### Moving work to a branch by patch carries other sessions' hunks (2026-10-24)

Work done in the main checkout was moved to a new worktree with `git diff -- <paths>`, applied there and
reverse-applied in the main checkout. A second session was editing the same checkout, and one of the named files
(`.claude/skills/improve/references/run-protocol.md`) held three of its lines. `git diff` takes every change in a
file, so those lines moved to the wrong branch and left the main checkout. A per-file read of the moved diff found
them, and they were put back by hand.
- **Why missed:** the move script checked that the files were this session's, not that each hunk was.
- **Prevent:** before a patch move, list each named file's changed lines (`git diff -U0 -- <file>`) and compare them
  with what this session wrote; a file with a foreign hunk moves by hand edit, never by whole-file patch. Better:
  start work that may need its own branch in a worktree from the first edit.
- **Source:** 1.5.x adopt-pstack-2026-10-24.md, on `origin/bannerlord-1.5.x` (ported by WP142).

### A skill that wraps a script the model already runs is a description tax that rots (2026-10-08)
Over seven weeks of transcripts, 21 of 45 skills had no call: the model ran `taom-src.ps1` about 5,100
times and `lint_docs.py` about 2,100 times directly, and fixed 301 build errors without `/build-fix`.
Several unused skills had drifted into wrong instructions (`xslt-check` mapped five vanilla files to a
module that lacks them; `lord-skills` called a merged XSLT lord dead code). The recurring mistakes were
caught by gates, never by skill text.
- **Why missed:** skills were added per workflow and never measured, and an unused skill fails silently.
- **Prevent:** before adding a skill, check whether the model already does the job by running the script;
  measure Skill-tool calls in the transcripts before keeping one. A gate beats skill text.
- **Source:** 1.5.x `docs/reviews/skill-usage-audit-2026-10-08.md`, on `origin/bannerlord-1.5.x` (ported by WP142).

### Compare a repo path from the disk without case

The Gauntlet stretch gate looked up a path from `Directory.EnumerateFiles` in a literal baseline with
an ordinal comparer. Git tracks `GUI/PreFabs`, the main checkout holds `GUI/Prefabs`, and the worktrees
hold `GUI/PreFabs`, so the gate passed in a worktree and failed on the main checkout with five false
offenders. `core.ignorecase` is true, so `git status` shows nothing.
- **Why missed:** the state list in the lesson above names line endings, new and deleted files, a
  non-ASCII path, a subfolder, a missing tool and an empty input, but not path case.
- **Prevent:** a key built from a disk path compares with `OrdinalIgnoreCase`. Run a new path-keyed
  gate on the main checkout as well as in a worktree.
- **Source:** 1.5.x `rca-gauntlet-ratchet-case-2026-11-03.md` finding 1, on `origin/bannerlord-1.5.x` (ported by WP142).

### Write last the file a re-run reads to decide what is left (map-tool follow-ups, 2026-10-27)
`Apply-MapVillageNames.py` decided what to rename from the master alone, wrote the master first and evicted the
translation cache last. A run that failed on the fourth language left a master that matched its table, so the re-run
printed "nothing to do" with nine languages and the whole cache still stale; the reset one-off did the same with
its German witness file. Separately, both new one-offs called themselves idempotent, but a re-run after the
translation backlog (#763) would turn every translated row back into English, the same missing `--rerun-retired`
guard the Lindon RCA named that morning. And the map-wide name check went to the two writers the issue named, not
to the renamer, the third writer of settlement names.
- **Why missed:** each writer was checked against the state right after its own first run, not against a failed
  run, a later legitimate writer or a reinstall; and the check was scoped by the issue title, not by the field.
- **Prevent:** write the decision file last and evict caches first, and test it by making one output read-only and
  proving the re-run finishes; a finished one-off whose re-run would undo later work refuses without
  `--rerun-retired` from its first commit, with a test that it refuses before reading anything; before adding a
  check to one writer, grep every writer of that field and add it to each.
- **Source:** 1.5.x `docs/reviews/rca-map-tool-followups-2026-10-27.md` findings 1 to 4; #1042, on `origin/bannerlord-1.5.x` (ported by WP142).

### A tool's default output never points into a live module (Carn Dûm map icon, 2026-10-16)
`convert_tripo_prop_textures.py` defaulted `--dst` to the live `TAOM_Map\AssetSources\Scenes\Mordor\textures` and
replaced files there with no backup, so a run without `--dst` wrote into an unversioned module.
- **Why missed:** the default came from the first prop's run, and the script was extended twice without a read of its
  defaults.
- **Prevent:** a path into the game install is a required argument, never a default, and an existing file is replaced
  only with `--force`. Before you extend a script, read its argument defaults.
- **Source:** 1.5.x `docs/reviews/rca-carn-dum-icon-tooling-2026-10-16.md` #1; `tools/tests/test_convert_tripo_prop_textures.py`, on `origin/bannerlord-1.5.x` (ported by WP142).

### A step written for one item refuses many (Carn Dûm map icon, 2026-10-16)
The rebuild route exports 7 texture sets. The converter, written for one, took the alphabetically first set (the
256 px openings) under the mesh's name, dropped the other six and exited 0.
- **Why missed:** the converter and the skill's handoff predate the route that sends many sets into them.
- **Prevent:** a step that takes one item from a folder counts its matches and fails on zero or several, unless it
  handles each one. Here each set becomes `t_<set>`, and `--stem` must match exactly one set.
- **Source:** 1.5.x `docs/reviews/rca-carn-dum-icon-tooling-2026-10-16.md` #2, on `origin/bannerlord-1.5.x` (ported by WP142).

### A last-resort path errs toward refusing

`8afe46d39` stopped `block-no-verify`'s raw fallback from over-blocking a message that named a flag, and lost eight refusals, among them `bash -c`. That path runs only past the reader's limit, on a reader crash or on an overrun, so a false refusal costs one retry and a missed one skips a gate.
- **Why missed:** a line said the fallback over-blocks, and nobody asked which side is safe there.
- **Prevent:** before you narrow a fallback, name what a false pass costs against a false refusal on that path.
- **Source:** 1.5.x `rca-follow-ups-2026-11-04.md` finding 18, on `origin/bannerlord-1.5.x` (ported by WP142).
