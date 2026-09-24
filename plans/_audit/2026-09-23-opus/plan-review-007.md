# Cold review: plan 007 (PatchShield skips the callback shims)

Reviewer: cold read, no prior context. Plan: `plans/007-patchshield-skip-callback-shims.md`.
Template: `.claude/skills/improve/references/plan-template.md` ("Quality bar").
Code compared at `b2e387db` (`git show`); drift check re-run at HEAD `4b5662b2`: empty output.

**Verdict:** not executable by a weak model as written. Two blocking defects, both in the tail
(Step 9 verification and the README/Done criteria). The code half (Steps 1 to 7) is sound: excerpts
match, the TDD order is right, and the expected test counts check out.

## Blocking

**B1. Step 9's dash check cannot pass (plan lines 625-628).** `docs/migration/dr3-maintenance.md:260`
is one very long line that already contains em dashes outside code spans ("... `TypeLoadException`) — <!-- lint-allow-dash -->
the canonical errors", "— the shield finalizer binds", "— see LESSONS-LEARNED", "— both passes —"). <!-- lint-allow-dash -->
Step 9 appends a sentence to that line, so `git diff` shows the whole line as added. The command
`git diff -- docs Dependencies | grep "^+" | grep -nP "[\x{2013}\x{2014}]"` then prints it.
`python tools/lint_docs.py` flags it too: `check_ai_dashes` (`tools/lint_docs.py:1233-1286`) runs `git diff -U0 HEAD`
and scans every changed line number. Line 626 then tells the executor "dash warnings on lines you
wrote mean you used a dash: fix them". The executor either rewrites untouched prose on line 260,
which contradicts lines 283-284, or fails twice and STOPs. **Fix:** make the new sentence its own
line or bullet under line 260, or set the expected result to "exactly one hit, line 260, whose dashes
are all pre-existing".

**B2. The README step and the Done criteria can't all be met as written (lines 6-8, 318, 634-635,
671-674).** Neither the committed `plans/README.md` nor the main tree's modified copy
(`git status`: `M plans/README.md`) has a row for 007, or for 006. The worktree made from `HEAD`
(line 345) also lacks the main tree's uncommitted README edits. Step 10 updates the README after
commit 2, but Done line 672 requires `git status --short` to be empty. No commit for the README is
described. A weak executor will invent a row, edit the main tree's README (uncommitted work by
another session), or fail the clean-status check. **Fix:** say "the orchestrator maintains the
index; do not touch `plans/README.md`" and drop lines 318 and 674. Or give the exact row text, name
the file (worktree copy) and give a third commit with its subject.

## Non-blocking

1. **Baseline failures not named (lines 300-304, 576-578, 666, 693-694).** "an Elk test" is
   ambiguous. `plans/_audit/2026-09-23-opus/baseline.md:19` names them:
   `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
   `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`. Both read the live Armory, which
   another session is editing, so the set can change. Step 1 never runs the full suite, yet Step 7
   says "baseline plus 7". Add a full-suite run to Step 1 and record the failing names there.
2. **Moved comment vs. the no-dash rule (lines 421-422 vs 282-284).** The #331 block moved
   "verbatim" contains an em dash (`PatchShield.cs:57`, "sessions — stack-sampled"). In the diff, <!-- lint-allow-dash -->
   moved lines count as new lines. Say whether they keep their text as moved (exempt) or lose the
   dash. The Step 9 grep runs after commit 1, so it never checks these lines.
3. **STOP grep is too broad (lines 687-689).** `git grep -n "ExcludedTargetNamespacePrefixes" -- .`
   already hits `CHANGELOG.md:21844`, `docs/features/arena.md`, `docs/features/coop-interop.md:413`,
   `docs/reviews/**` and `docs/reviews/lessons/harmony-il.md`. A literal executor may read the
   CHANGELOG hit as "outside the in-scope files and docs" and STOP. Restrict it to `-- '*.cs'`.
4. **Drift check misses premise files (line 11).** It lists `Native2ManagedPatcher.cs` but not
   `Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs` (excerpt at 235-239, STOP at 685) or
   `Main/SubModule.cs` (lines 194 and 203 cited at 212, 331). Add both as read-only premise paths.
5. **The `grep -P` fallback is not a command (lines 627-628).** Under Git Bash with a non-UTF-8 locale,
   `grep -P "\x{2014}"` errors ("code point too large"); it does not just "lack -P". Give the exact
   `python -c` one-liner.
6. **Plan 006 does not exist yet (lines 22, 325, 690, 710).** `plans/006-crash-capture-boot-cost.md`
   is not on disk. The STOP check at 690-692 still works, but the pointer dangles.
7. **The "main menu" fix is partial.** The plan fixes `dr3-maintenance.md:269` and 286. Lines 255
   ("deletes on main-menu reach") and 270 ("at last main-menu reach"), and
   `IncompatibleModDetector.cs:11,87`, still state the wrong timing and now contradict 269. Put them
   in scope or list them as out of scope.
8. **The Step 8 doc comment drops a fact (lines 589-597).** The original (`SubModule.cs:268-270`) and
   `IncompatibleModDetector.cs:88-89` say MarkSessionLaunchSuccessful also snapshots
   `last-good-modlist.txt`. The new comment only mentions deleting the marker.
9. **The premise is slightly overstated (lines 237-239).** `HandleAndSwallow` also returns the
   exception on re-entry (`CrashReportPatchHelper.cs:32`, `_onPatchStack`) and when the service
   throws (line 51). This doesn't change the decision, but "every exception type" is not literal.
10. **Version wording (lines 347-348).** `<Version value="v2.0.30" />` already includes the `v`
    (`Main/_Module/SubModule.xml:6`), so a literal `v<version>` gives "vv2.0.30". The suggested
    subjects (69 and 68 characters) are correct.
11. **"No new findings" has no baseline (lines 294, 625).** State what lint_docs is expected to print
    (for example, the dash section lists nothing, or only the B1 line).
12. **Maintenance note cites the wrong line (line 724).** `TryUnpatchOffendingPatches` is declared at
    `PatchShield.cs:323`; 378-401 is its owner loop. Minor.

## Checklist results

| Check | Result |
|---|---|
| Self-contained | Yes for Steps 1 to 8 (exact code, paths, counts). Gaps: B2, non-blocking 1 and 5. |
| Every step ends in a command | Yes. Step 9's expected result is wrong (B1); Step 10 relies on Done criteria. |
| TDD order | Correct: Step 2 RED (CS0117) before 3, Step 4 RED before 5, 6a RED before 6b. Counts 16, 19, 20 (1 fail), 20, 23 verified: the class has 16 `[TestMethod]`s and no `DataRow`s. |
| Issue-first | Present: "create before implementation lands (orchestrator)" (line 25). |
| ADRs named | ADR-002, 003, 004, 005, 007, 008 and `tests.md` each get a one-line summary (272-284). All exist. |
| Single-owner files | `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj`: out of scope with a STOP condition (322-323, 695-696). |
| STOP conditions | Specific (namespace move, premise invalidated, plan 006 overlap). One too-broad grep (non-blocking 3). |
| Done criteria machine-checkable | Yes, except line 674 (see B2) and the unnamed failing tests (non-blocking 1). |
| Planned-at SHA and drift paths | `b2e387db` filled in. Drift paths cover Scope and add one premise file; see non-blocking 4. |
| Non-deploying commands | Every build and test command has `-p:DisableModuleCopy=true -p:ModuleId=`. The Dependencies post-build copy goes to gitignored `Dependencies/_Module/bin/**` (`.gitignore:48,51`), so `git status` stays clean. |
| Dashes in prose | None in prose. The four hits (lines 82, 107, 130, 193) are inside quoted code excerpts. |
| Secrets | None. |

## Excerpt verification at `b2e387db`

All excerpts match the code:

- `PatchShield.cs:50-84` matches exactly: list at 60-69, `IsExcludedTarget` at 71-84. The exclusion
  check is at 197, `harmony.Patch` at 222-223, the log line at 237 and the `using` block at 1-7 (no
  `System.Diagnostics`). File length 452.
- `PatchShieldPolicy.cs:1-11` header matches (153 lines), and the warning at 126-128 matches.
  Nullable is enabled through `Directory.Build.props:6`; the Dependencies csproj sets
  `LangVersion` preview (line 12).
- `Dependencies/SubModule.cs` (324 lines): pass 1 at 234, pass 2 at 288, doc comment at 267-272,
  label at 276. All match. The `...` in the excerpt hides the `CoopPresence.Refresh` block at 281-286.
- `Native2ManagedPatcher.cs:21-26, 72-80` and the bridge at 119-123 match (the finalizer is on line 121-122).
- `CrashReportPatchHelper.cs:29-53` matches, with the caveat in non-blocking 9.
- `Main/SubModule.cs:194` (`com.taom.mod`) and `:203` (`AttachAll`) match.
- `Main/TAOM.csproj:89` and `TAOM.Tests/TAOM.Tests.csproj:27` match.
- Docs: `dr3-maintenance.md` lines 260, 269, 286 and 291, and `v1.5.2-impact.md:141`, match the
  quoted text.
- Engine facts: the cached v1.5.3 decompile shows `namespace ManagedCallbacks;` in all three shim
  files, `MBSubModuleBase.cs:84`, `MBGameManager.cs:110-114`, `Campaign.cs:1471` and
  `CustomGame.cs:56`. All match.
- Runtime evidence: `diag.log` 35488-35489 and 35500-35501 match. There are 0
  `[PatchShield] swallowed` lines (the log format is at `PatchShield.cs:309`) and 466 `session start`
  lines. `taom_debug_2026-09-23_13-43-40.log:5` reads "attached 247 Finalizer(s)".

Minor citation drift: see non-blocking 12.
