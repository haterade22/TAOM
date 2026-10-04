# RCA: release packager shader cache and JIT reports deep review (plan 035, 2026-10-02)

## Top line

The three-lens `/deep-review` of plan 035 (`d50bf962..a2e46e29` on
`perf/035-release-shader-cache-check`) found no CRITICAL or HIGH finding, 3 MEDIUM and a tail of
LOW. The change adds two report-only sections to `tools/package_release.py`, and the maintainer's
binding policy is that they never refuse and never change the exit code. The first MEDIUM broke
exactly that promise: a scene folder name outside cp1252 made the scene listing raise
`UnicodeEncodeError` under a piped stdout, so a clean run exited 1 before the copy. The second
was a claim the code could not keep: the docs said the stale-format WARNING covers issue #448,
whose three lagging sacks were module-level `<Module>/Shaders/D3D11` sacks that the report never
reads (and a majority vote among scene sacks could not see a set lagging as a whole either). The
third was coverage: the metadata reader's tests used one fixture shape that
no shipped TAOM assembly has, and 16 of 22 mutations of the reader left every test green. All
were fixed in the follow-up commit; the engine-baseline redesign of the stale check and the scope
of "sacks" in D8 go to Mike.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM | `_shader_cache_report` prints every shipped scene folder name; under a piped cp1252 stdout a name outside the code page raised `UnicodeEncodeError` out of `main`, exit 1 instead of 0 (`package_release.py:654` at `a2e46e29`) | Missing guard on a contract | The contract "never changes the exit code" was read as "adds no `return 2`"; nobody asked what else can end a run. Every name in every install is ASCII, so no run showed it. This is the third cp1252 stdout crash in tools (`rebalance_troops.py`'s delta glyph, the #647 hook extraction) | Stream reconfigure with `backslashreplace` in `main`, a guard around each report; two tests red first; a lesson in `lessons/build-tooling-workflow.md` |
| F2 | MEDIUM | The docs and the WARNING text said the check covers #448, but #448's three lagging sacks (0x0782, against 486 vanilla sacks at 0x0783) were module-level `<Module>/Shaders/D3D11` sacks, which the report never reads; separately, the WARNING compares the shipped scene sacks with one another, so a scene-sack set uniformly behind the engine raises none and a freshly re-saved sack in a stale set is the one flagged | Claim stronger than the mechanism | The majority rule came from the plan's amendment, and the executor wrote the docs from the amendment's intent; nobody replayed #448's own files through the report. The first fix repeated this: it blamed the majority rule for the #448 miss without checking where #448's sacks lived (corrected in convergence round 1) | Wording fixed everywhere it appeared; `test_issue_448s_module_level_sacks_are_not_read` replays #448's layout and `test_a_set_uniformly_behind_the_engine_is_not_flagged` pins the majority limitation; catching #448 needs module-level sacks read (N2) plus an engine baseline (N1), for Mike; a lesson in `lessons/testing-qa.md` |
| F3 | MEDIUM | The reader's only fixture was PE32+ with 2-byte heap and coded indexes and one row per table; the installed `TAOM.dll` (HeapSizes 0x05: #Strings and #Blob indexes 4 bytes, #GUID 2; MemberRefParent and CustomAttributeType 4 bytes through its 20,114 MethodDef rows), the PE32 copy the patreon and public channels ship (15,553 MethodDef rows, 2-byte HasConstant) and the PE32 `TAOM.Dependencies.dll` took branches no test ran | Test fixture narrower than the real input | The fixture was built to be the smallest valid file, and its one validation (System.Reflection.Metadata reads it back) proved it valid, not representative. The reader was correct on real DLLs, so nothing failed to prompt a wider fixture. A repeat of "a parser tested only against the format you invented". The first fix built its wide shapes from a summary rather than the measured header (all three heap widths on one switch, coded indexes widened only through TypeRef and MemberRef), so five mutants aimed at the real shape survived it | Fixture options for PE32, each HeapSizes bit, filler rows and attributes, and table sizes (MethodDef included) that widen each coded index, plus one fixture with the installed `TAOM.dll`'s measured header; each reader test reads a Debug and a Release form; the round-0 mutations and the five convergence mutants all fail a test; a lesson in `lessons/testing-qa.md` naming the earlier one |
| F4 | LOW | Two comments said TAOM does not ship sacks; every channel ships 52 to 53 scene sacks, none ships a module-level sack | Stale claim | The comment paraphrased a remark about one kind of sack as a rule about all of them, without a census | Reworded; the census is in the review report |
| F5 | LOW | The release skill restated `release-process.md` step 9 almost line for line | Duplication across tiers | Both texts were written in the same commit from the same draft | The skill keeps the operator's delta and links the step |
| F6 | LOW | A 139-character line in a paragraph wrapped near 100 | Formatting | A sentence was inserted without reflowing the paragraph | Rewrapped |
| F7 | LOW | `jit_optimizer_disabled` existed only for the tests and dropped the reason they should pin | Dead wrapper | Written first, before D6 added the reason, and kept as a convenience | Deleted; the tests assert `(verdict, reason)` |
| F8 | LOW | The native, missing-file and malformed reasons and the unreadable-sack CLI line were never asserted | Untested branch | Same root as F7: the wrapper hid the reasons from the tests | Tests for each reason, the unreadable-sack line and its summary suffix |
| F9 | LOW | The commit body says the module map describes both reports; it describes the scene listing only | Stale claim | The body was written from the plan's doc list, not from the final diff | Corrected in the follow-up commit body |

## Root-cause pattern

F1, F2 and F4 share one shape: a sentence (a contract, a coverage claim, a policy) was written
from intent and never tested against the input that would falsify it. "Never changes the exit
code" needed one run with a hostile name; "covers #448" needed #448's own files fed to the report;
"TAOM does not ship sacks" needed one count of a release folder. F3 is the testing face of the
same habit: the fixture proved the reader handles the file the test author imagined.

## Why each agent missed these

The lenses found every row above; what follows is why the implementation and the plan did not.

- **The executor** followed the plan and D8 faithfully. Its RED and GREEN runs were real, but the
  RED tests could only fail for the cases the plan listed; none listed an encoding failure, a
  uniformly stale set or a wide assembly.
- **The plan and its amendment** specified the majority rule over scene sacks and named #448 as
  its purpose without checking where #448's sacks lived (module-level, not per scene).
- **Lenses not launched** (Compatibility, Efficiency, Data flow, Design, XML): out of scope for an
  offline Python tool with no engine code. The Data flow lens would likely have asked the
  reverse question for F2 (which sack does the rule flag when only one is fresh?), which the
  tooling lens covered.
- **Codex** was not run for this item.

## Feedback memories to codify

None beyond the three lessons appended for F1, F2 and F3. F1 and F3 repeat patterns already on
file, and their entries name the earlier lessons.

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `a2e46e29..6cc7fbfe` | C1: the F3 fixtures set all three heap widths together and widened coded indexes only through references, so five reader mutants aimed at the installed `TAOM.dll`'s HeapSizes 0x05 shape survived | `893f9428` | The wide shapes were built from a summary of the header, not the measured one | Build a fixture from each real file's measured header and run mutants against it |
| 1 | same | C2: the F2 rewording blamed the majority rule for missing #448, but #448's three sacks were module-level `Shaders/D3D11` sacks the report never reads | `893f9428` | The rewording was written from the code's rule, not from #448's own data | Replay the incident's data as a test before explaining why a check misses it |
| 2 | `6cc7fbfe..893f9428` | R2-1: no fixture took the PE32 copy the patreon and public channels ship (15,553 MethodDef and 14,333 Param rows, so HasConstant 2 bytes beside 4-byte coded indexes); a one-token HasConstant slip passed all 108 tests and read both channels' Debug `TAOM.dll` as ON | the records commit (`test_the_channel_taom_dll_shape`) | Round 1 measured the installed DLL and took it for the shipped one; no channel ships that build | Measure the copies players receive, not the dev install |
| 2 | same | R2-2: `893f9428`'s body says five slips made the Debug `TAOM.dll` read ON; four did, and the fifth (M5) made it read unknown | the records commit's body (a correction line) | The body was written from the mutant count, not each mutant's real read | Copy a mutant's effect from its log line |
| 2 | same | R2-3: the `params` docstring line lost a column of alignment | the records commit | A hand edit of an aligned block | Diff aligned blocks with whitespace shown |

This section and the REVIEW-LOG entry's convergence line were written by the orchestrator after round 2.
