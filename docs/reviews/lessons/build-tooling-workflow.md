# Lessons — Build, Tooling & Workflow

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Build, Tooling & Workflow lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

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

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

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
run "once per tournament". `FightTournamentGame.GetParticipantCharacters` has four call sites; two
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
