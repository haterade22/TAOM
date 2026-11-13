# Deep review: plan 046, test-suite read caches (2026-11-13)

```
DEEP REVIEW REPORT
===================
Feature: Read the C# corpus once per test run: cache code-root text in RepoPaths, skip Languages in
         two character scans, compile each shipped XSLT once (plan 046, #1132)
Date: 2026-11-13

Scope:   C# test code only: 44 files under TAOM.Tests, 664f19c5b..eb38f4009 on
         improve/046-test-suite-read-caches. No production C#, XML, XSLT, script or harness change.
Blast radius: test infrastructure only. Agent 5 traced the 69 CSharpFiles call sites, the 27 reads
         converted to RepoPaths.Text and the 15 CompiledXslt.Get sites. No shipped type changed.
Waves:   one lens report reached the review lead: Agent 5 (Data flow).

STANDARDS:     NOT SUPPLIED (no report reached the review lead)
COMPATIBILITY: NOT IN SCOPE (no engine-facing code; the shared XslCompiledTransform was checked by
               Agent 5 against the installed Framework64 v4.0.30319 System.Xml and System.Data.SqlXml)
EFFICIENCY:    NOT SUPPLIED
COMPLETENESS:  NOT SUPPLIED
DATA FLOW:     PASS after fixes: 0 gaps, 2 inconsistencies (both LOW, both fixed)
DESIGN:        NOT SUPPLIED (no KEEP proposals to apply)
XML:           NOT IN SCOPE (no ModuleData or XSLT changed)
TOOLING:       NOT IN SCOPE
```

## Details

### Agent 5: Data flow

14 flows traced, 0 gaps, 2 LOW inconsistencies. The flows covered the cached per-root file lists, the
cached text and its keying, stale text, normalisation at every converted read, name binding of the bare
`Text(...)` calls, the loader style kept at each XSLT site, sharing one compiled `XslCompiledTransform`,
the removed `settings` locals, the new `using TAOM.Tests.Core;` lines and the Languages skip.

The three orchestrator questions:

1. **Stale cached text.** No test writes a .cs file under Main, Client, Dependencies or CoopCompat. The
   lens read 559 write-call lines in `TAOM.Tests`. Every target is under `Path.GetTempPath()`,
   `TAOM.Tests/bin` or `EntryPointLineCountBaseline.txt`. The suite has no `Process.Start` and no
   parallel-run setting. The lead's own grep, for a write call that names a root on the same line,
   found none.
2. **Lost normalisation.** Every replaced `File.ReadAllText` was the one-argument overload, and each
   outer step (`StripComments`, the CRLF replace, the whitespace regex, `CommentPattern.Replace`) is
   still applied at its site.
3. **Shared XslCompiledTransform.** The decompiled `Transform` reads only `command` and runs it with a
   fresh `XmlQueryRuntime`. Instance fields are written only on `Load`. The lead confirmed that the 15
   `CompiledXslt.Get` sites never call `Load` or read `OutputSettings` on the shared instance; the only
   `Load` calls are in `CompiledXslt.Compile` (TAOM.Tests/Core/CompiledXslt.cs:38, 44).

The lead also confirmed that no file under `Main/_Module/ModuleData/Languages` holds `<NPCCharacter`
(Grep count 0), so the Languages skip leaves the two character dictionaries unchanged.

| # | Sev | Finding | Verdict | Evidence |
|---|---|---|---|---|
| 13 | LOW | `RepoPathsTests.cs:148` said only the root check kept the bin probe out of the cache; the `bin` segment clause rejects it too, so neither clause had a test of its own | CONFIRMED | `IsCorpusFile` (RepoPaths.cs) rejects the probe on both clauses. With the root clause turned into an `\|\|`, the 19 RepoPaths tests that existed then all passed; only the new test failed (`Failed: 1, Passed: 19, Total: 20`) |
| 14 | LOW | The run-snapshot warning was only on the private `CorpusRoots` field; the public `CSharpFiles`, `ReadSource` and `MentionsOutsideComments` docs and the class summary did not say the results are cached for the four roots | CONFIRMED | RepoPaths.cs class summary and the three member docs, read at `eb38f4009` |

## Action items

None open. Both findings are fixed in the review follow-up commit.

## Improvements (Step 4)

```
APPLIED:     TAOM.Tests/Infrastructure/RepoPathsTests.cs: new Text_TrackedFileOutsideTheCorpusRoots_IsNotCached
             (two reads of a tracked .cs file outside the roots return two string instances); it fails
             against a mutant with the root clause turned into an ||, and every other RepoPaths test
             stays green against that mutant. The bin probe's comment now says both checks reject it.
             TAOM.Tests/Infrastructure/RepoPaths.cs: doc comments only (class summary, CSharpFiles,
             ReadSource, MentionsOutsideComments) name the four cached roots and the per-run snapshot.
             Proof: full suite green before (39257 passed) and after (39258 passed, the one new test).
NOT APPLIED: none. No KEEP or APPLY-scoped proposal was supplied. A probe for the bin clause alone was
             considered and not added: the only .cs files under a root's obj folder are build output
             (Main/obj/Debug/net472/TAOM.AssemblyInfo.cs), so the test would depend on the build
             configuration, and a written probe under Main/bin would put a .cs file in a code root.
             The clause's helper, IsBuildOutput, is already guarded through
             CSharpFiles_Client_ContainsTheSubModuleAndNothingUnderObjOrBin.
FOLLOW-UP:   the other NPCCharacter loaders that plan 046 deferred still parse Languages (same result,
             more time). No issue filed by the lead (role rule 11); the orchestrator owns it under #1132.

VERDICT: READY FOR COMMIT
```

Suite totals: base `664f19c5b` `Passed! - Failed: 0, Passed: 39250, Skipped: 9, Total: 39259` (executor
log); branch head before the review fixes `Passed! - Failed: 0, Passed: 39257, Skipped: 9, Total: 39266`;
after them `Passed! - Failed: 0, Passed: 39258, Skipped: 9, Total: 39267`. All three are game-backed,
non-deploying runs of `dotnet test TAOM.Tests`. Not covered: the RefAsm no-game replay (the new test
touches no engine type), the hook suite and the gate sweep (no hook or validator changed).

RCA: [rca-test-suite-read-caches-2026-11-13.md](rca-test-suite-read-caches-2026-11-13.md). Lesson: the
last entry in [lessons/testing-qa.md](lessons/testing-qa.md).

## Codex review

Codex not run: out of usage, and a paid call needs the maintainer's word. There is no Codex output to
verify, so the Phase 3d table has no rows.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|
| (none) | Codex not run | | | |

- **Confirmed bugs:** none from Codex.
- **False positives:** none.
- **Design questions:** none.
- **Things Codex missed:** not applicable. The lens findings 13 and 14 are in the RCA.

### Codex lessons (pending)

None. No Codex run, so there is no new false-positive pattern and no missed category to record.
