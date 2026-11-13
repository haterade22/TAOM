# Plan review 046, round 1 (cold)

Plan: `plans/046-test-suite-read-caches.md`. Code read from the baseline worktree at `7087fa633`
(`git log -1` there prints `7087fa633`). No earlier review file for 046 existed in this folder.

## Verdict

Two blocking items, both cheap to fix. Everything else checked out: the excerpts match the code, apart
from three small inaccuracies listed below. The TDD order and the mutation proofs work: I traced each
RED and mutation by hand against the code as planned.

## Blocking

1. **Step 11 Verify cannot pass as written (plan lines 936-937).** `git grep -n "new XslCompiledTransform()" -- TAOM.Tests`
   is told to print "exactly one line, in `TAOM.Tests/Core/CompiledXslt.cs`". At Step 11, `CompiledXslt.cs`
   is a new, untracked, unstaged file (it is committed only in Step 12), and `git grep` without
   `--untracked` searches tracked files only. The 15 site lines are gone after the edits, so the command
   prints nothing. A literal executor sees a failed verification and STOPs after two tries. Fix: use
   `git grep -n --untracked "new XslCompiledTransform()" -- TAOM.Tests`, or move the check to after the
   Step 12 commit. The Done criterion (line 980) runs after the commit and is fine.

2. **The Step 1 graphify refresh conflicts with dispatch rules 1 and 4 (plan lines 287-295, 393).**
   The plan says to run `python tools/graphify_taom.py refresh --if-stale`, "in the background if it needs
   a rebuild". From a linked worktree, `out_root()` (tools/graphify_taom.py:70-86) writes the graph to
   `E:\graphify\TAOM-wt-<name>`, which is outside the executor's writable paths. A rebuild allows
   `EXTRACT_TIMEOUT = 1500` s plus `CLUSTER_TIMEOUT = 600` s (lines 58-59), well past the 15-minute
   per-call limit, and only the hook suite may run in the background. The plan itself says the planner's
   refresh was killed after about 10 minutes. Fix: run `python tools/graphify_taom.py status --brief`.
   Only on exit 0 (fresh), run `affected "RepoPaths" --depth 2`. On exit 1 or 2, quote the status line
   and the existing grep count (418, which I re-ran: `git grep -l 'RepoPaths' -- TAOM.Tests | wc -l`
   prints `418`). Never refresh.

## Non-blocking

- **Line counts in Part A (lines 146 and 668).** Both say "27 line edits", but Scope (lines 323-337) lists
  26 lines in 23 files. The 27th is the second read on `CommandTrialWiringTests.cs:125`, which is on the same
  line as the first. Say "26 lines (27 read expressions)" so that an executor who counts the edits does not
  go looking for a missing one.
- **Step 2, probe output (lines 447-456, 494).** The full run at item 3 sets `TAOM_046_OUT`, so
  `Plan046CorpusProbeTests` runs again. That puts six lines in `corpus-probe.txt` and adds one test to the
  full-run total. Say "use the first three lines (the probe-only run)". Also say that the full run in Step 2
  is not judged by its totals: the STOP condition at line 993 ("changes its count") could otherwise fire on
  the extra probe test.
- **Step 2, probe-only run (line 450).** If the variable does not reach the test host, the probe test hits
  its `Assert.Fail`, and that shows before the full run. The `-e` retry note at lines 454-456 covers only
  the full run. Add "if the probe-only run fails with 'set TAOM_046_OUT', retry it with `-e`".
- **Step 5 Verify and the Done criterion (lines 682-684, 977-978).** "`git grep -c "File.ReadAllText" --` over
  the 23 files" does not spell out the 23 paths. Give the full command. I checked the expected result: at
  the base, the 23 files hold 1,1,1,1,1,2,1,4,3,1,2,1,1,1,1,1,1,5,1,2,1,1,1 matching lines. After the 26 edits,
  exactly `NotableServicesWiringTests.cs:3`, `CommandTrialWiringTests.cs:2` and `PatchCategoryApplierTests.cs:4`
  remain, as the plan says.
- **Done criterion "the build command exits 0" (line 974).** No step runs `dotnet build Main/TAOM.csproj ...`.
  Add the build to Steps 6 and 12, or drop the criterion. `dotnet test` builds `Main` anyway.
- **`<scratch>` (lines 389, 449-459, 689, 941).** The plan writes logs and `count_passes.py` to `<scratch>`.
  Dispatch rule 3 puts an agent's files under `<scratch>\<your label>\`. Write "your scratch folder" in
  every one of these places.
- **CompiledXslt key case (line 884).** The tuple key compares the path ordinally (case-sensitive), but
  `.claude/rules/tests.md` says a path key compares with `OrdinalIgnoreCase`. The only effect is a missed
  hit, which compiles one more copy, never wrong output. A comparer, or a comment saying why ordinal is
  safe here, would close the point before deep-review raises it.
- **GATE C scope (lines 779-782).** The gate measures one lords.xslt compile. Most of the repeated
  compiles are spcultures.xslt (6 sites, plus 4 calls in `CulturePartyTemplateTests`). The 300 ms per-compile
  threshold is machine-checkable, so this is acceptable. The commit body should still say that the gate
  priced lords.xslt only.
- **Line 36:** "15 tests compile shipped XSLT". These are 15 sites (lines). Several are helpers that more
  than one test calls.
- **Done criterion for Part B (line 979):** "shows the exclusion in both files" is a judgment. Name the line
  to find: `!f.StartsWith(languages, StringComparison.OrdinalIgnoreCase)`, once per file.

## Excerpt check (against 7087fa633)

Matched exactly:
- `RepoPaths.cs`: 88 lines; usings 1-6; `ReadSource` 50-59; `MentionsOutsideComments` 75-85.
- `RepoPathsTests.cs`: 128 lines, 8 `[TestMethod]` and 1 `[DataTestMethod]` with 6 rows; the cited
  ranges 47-60, 70-75, 98-118 and 120-127.
- All 26 group-1 lines (the read expression at each listed line, plus the reads at
  Notable 152/278/383, CommandTrial 131/438 and PatchCategory 390/432/443/455 that stay as they are).
- The import claims for the bare-`Text` files (`AffectRelativesGateTests`, `PartyMemberSizeLimitCallShapeTests`
  have only `using static`). Every other file has `using TAOM.Tests.Infrastructure;` or sits in that namespace.
  No `Text` member, type or namespace exists (two greps, both empty).
- The 67 `CSharpFiles(` call sites outside `RepoPaths.cs` and their sorting into groups 2 to 4. The
  spot-checks were PartyScreenCommit:33, FeatureModules:194, FiefsNavButton:138, ClientIsolation:138,
  MusicPause:30, SkeletonBuffer:173 (`ReadSource`) and WardensTally:107 (feature-folder filter).
- Part B: both loaders (Hunt 23-44, header over 28-29; FireAndTribute 57, 67-84, one-line header at 70);
  usings `System`, `System.IO`, `System.Linq` present in both; the `<NPCCharacter` grep over Languages prints `0`;
  289 of 457 tracked ModuleData XML files sit under `Languages/`; no other `Languages` folder exists.
- Part C: `git grep -n 'new XslCompiledTransform()' -- TAOM.Tests` gives exactly the 15 lines. Every
  stylesheet expression, line range, call count (CulturePartyTemplate 91/132/194/218, LordTemplateRoster
  74/93/158, Nazgul 64/96) and `settings` reuse matches the plan, and the `settings` line is unused after
  the Load in VanillaWanderer, ShippedClanSide and ShippedCastle. The `LiveInstall` tags sit at the cited
  lines. The `using TAOM.Tests.Core;` claims hold: Garrison and ShippedClanSide lack it, and the other four
  files have it. All 9 Core sites are in `TAOM.Tests.Core`.
- `XsltTransformCache.cs:11-12` and `:20` hold the quoted claims. `CultureDataFixture.cs` is 107 lines, an
  `internal static class` in `TAOM.Tests.Core`.
- Code-root file counts 4,342 / 268 / 22 / 12. The baseline totals agree with `_audit/.../baseline.md`
  (failures=7 is 1 + 3 subtests + 3).

Mismatches:
- Line 52: the excerpt labelled `RepoPaths.cs:20-33` silently skips lines 22-26 (a blank line and the
  `CSharpFiles` doc comment that Step 4 says to keep). There is no ellipsis.
- Line 361: "the 66 other files with their own `Descendants("NPCCharacter")` loader".
  `git grep -l 'Descendants("NPCCharacter")' -- TAOM.Tests | wc -l` prints `42` (40 besides the two in
  scope). `Elements` included gives 48. This is out-of-scope prose, but the number is wrong.
- Lines 146 and 668: "27 lines" (see above).

## Structure checks

- TDD: Part A RED (Step 3) comes before GREEN (Step 4), and Part C RED (Step 9) before GREEN (Step 10). Each
  RED builds the test project, uses a named filter, and names the assertion kind (`Assert.AreSame`), not
  the compiler text. I traced the three Part A mutations and the Part C mutation against the planned code,
  and each fails the named test. For example, the sorted-roots mutation reorders `{Client, Main}`, and
  `return true;` caches the bin probe so it reads "first" again. Part B has no new test. The plan says
  why (the zero-hit grep, and the unchanged totals of the two classes).
- Issue line: "filed by the orchestrator before execution". ADRs named (002, 003-005, 007, 008). No
  protected or single-owner file changes, and the plan says so (lines 355-357). The drift-check paths
  cover every in-scope file. Planned-at is `7087fa633`, and every dotnet command carries both `-p:` flags. No
  worktree path, no branch name, no CHANGELOG step. No em or en dash (checked by codepoint). No secret
  values.
- `count_passes.py`: the class-name extraction (`split("+")[0]`, last dotted segment) handles the
  `+<>c` lambda and iterator frames. The `SAVED` set lists the 23 group-1 and 20 group-2 classes.
- The claim that no test writes a `.cs` file under a code root: my grep for write calls filtered to code-root
  or `.cs` targets returned nothing. A wider scan (StreamWriter, FileStream, CreateDirectory) found only
  temp, config and log writes. This is PARTIAL: the scan was over call shapes, not every path a test could
  build at run time.
