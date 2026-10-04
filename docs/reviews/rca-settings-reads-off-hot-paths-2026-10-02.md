# RCA: plan 031 review findings, MCM settings reads off hot paths (2026-10-02)

## Top line

Plan 031 moved MCM settings reads off TAOM's hot paths: nine settings providers now cache the settings
reference and read through it, four combat services and Patch35 check the cheap condition before the
setting, the Mixed Formations hotkey is parsed only when its string changes, and the English override
prefix looks an id up in place instead of cutting it out with `Substring`. A six-lens review found no
runtime defect, no HIGH and no MEDIUM. The three questions the change rests on (whether MCM can swap
its settings object mid-session, whether the reorders keep every result for NaN and null, whether the
slice compares exactly as `Substring` did) were each answered from the installed MCM and engine
assemblies, by five lenses independently.

Eleven LOW findings were confirmed, all in what surrounds the code rather than its behaviour: an
algorithm left in a Harmony patch class, test-only public surface, a gate test filed in the wrong
folder, two assertions that compared the code with itself, three pieces of wording that described a
contract the code does not have, two "cannot be tested" notes the change itself made false, and two
plan review probes left without a test. All eleven were fixed in the follow-up commit, each new or
tightened test proven against a deliberate mutation.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| C1 | LOW | The slice key's FNV-1a hash and ordinal comparer were nested in `MBTextManager_GetLocalizedText_Patch`, an algorithm in an entry point (ADR-002), and a comment pinned the nested types below `Prefix` to keep the co-op veto scan from naming them as the prefix's owner | Convention | The plan prescribed the nested types; its own review then found the scan collision and fixed it by placement, and the placement comment was accepted as the fix | `IdSlice` is a one-type file with its own tests; lesson in `harmony-il.md` |
| C2 | LOW | `CachedEnumParse` had a public parameterless constructor used only by tests, building a parser that never logs (contradicting its summary), and a public delegate used only by an internal constructor | Simplicity | Written for test convenience | Deleted and narrowed; one-off |
| C3 | LOW | `HotPathSettingsProvidersTests` sat at the `TAOM.Tests/Features` root, where no feature lives; cross-feature gates are in `TAOM.Tests/Infrastructure` | Convention | The plan named the path; nobody checked it against `tests.md` | Moved; one-off |
| C4 | LOW | Two gate-order tests computed `expected` with the reordered code and compared the same call with it, which proves only that the call is deterministic | Test meaningfulness | The plan supplied the assertion as an oracle and it was used as written | Concrete value asserted, proven against a mutation; lesson in `testing-qa.md` |
| C5 | LOW | The rewritten `file-catalogue.md` row limited MCM's precedence over the JSON to the `*Enabled` getters; the five sliders also merge MCM over the JSON | Doc precision | The line was rewritten to fix a stale line range, and the old scope was carried over | Corrected; one-off |
| C6 | LOW | `localization-override.md` updated the test count to 18 but kept the old list of what the tests cover | Doc precision | Count edited, sentence not re-read | Corrected; one-off |
| C7 | LOW | `CachedEnumParse`'s summary, its parameter doc, the test summary, the test name `LogsOneWarningPerDistinctValue`, the mixed-formations doc and the `bf1898c3` body all said one warning per distinct value; the class remembers one last value, so re-entering a bad value after a good one logs again, which the test itself asserted with `Received(2)` | Wording against behaviour | The words were written from the plan's intent; the test was then written from the code, and nobody read the name against its assertions | All wording corrected, test renamed; the commit body is corrected in the follow-up body; lesson in `testing-qa.md` |
| C8 | LOW | `CavalryChargeServiceTests.cs` ended with a note that `SmartCavalryAISettingsProvider` "has no direct unit tests" because MCMv5 is unavailable in the test host; this change added `SmartCavalryAISettingsProviderTests` | Stale claim | The note sits in another feature's test file, outside the diff, and no one searched for it | Deleted; lesson in `testing-qa.md` |
| C9 | LOW | The auto knockdown ratio's floor follows the live neutral ratio; no test changed the neutral ratio through the cached settings, so a floor reading the JSON neutral would have passed | Test gap | The plan listed it as a review probe; the executor's read-through table kept neutral at its default so the auto row would pass its clamp | `AutoRatioFloor_FollowsALiveNeutralEdit_ThroughTheCachedSettings`; one-off |
| C10 | LOW | `CachedEnumParse` resetting the value to `default` on a failure that follows a success was not pinned (the parity test checks the value only on success) | Test gap | Same plan probe, same omission; the caller ignores the value on false, so nothing failed | `TryGet_FailureAfterASuccess_ReturnsDefault`; one-off |
| C11 | LOW | `CombatMechanicsSettingsProviderTests`' summary still said `Instance` "cannot be set in a test" after the class gained the internal constructor tests | Stale claim | The class summary was not re-read when tests were appended | Rewritten; same lesson as C8 |

## Root-cause patterns

**Words that outlive or outrun the code (C5, C6, C7, C8, C11).** Five findings are text that was true
of an earlier state or of the plan's intent. C8 and C11 are the same shape: a change that makes a class
testable leaves behind the notes that said it was not, one in the class's own test file and one in a
neighbouring feature's. C7 is the inverse: the wording was written before the code and kept after the
code chose a simpler contract (one remembered value), and a test name repeated the wording while its
assertion contradicted it.

**Plan text used as an oracle (C1, C3, C4, C9, C10).** The plan supplied paths, nested types,
assertions and a list of review probes. The executor followed them faithfully; where the plan was
weak (a self-comparing assertion, a probe without a test, a nested algorithm with a placement
workaround) the weakness shipped. The `/improve` dispatch rules already say plan text is a draft to
verify against the code; these are cases where it was not re-checked.

## Why each agent missed these

The lenses did not miss them: every finding above came from a lens. What the executor's own checks
missed:

- **The plan review** caught the co-op scan collision (C1) and chose placement over extraction, so the
  ADR-002 question was never asked.
- **The executor's tests** were written to the plan's oracles (C4) and probes list (C9, C10 not
  turned into tests), and its doc edits changed counts and line ranges without re-reading the
  sentences around them (C5, C6).
- **No sweep** looked for "untestable" notes about the providers whose tests the change added (C8,
  C11). A grep for the class names in `TAOM.Tests` would have found both.

Across the lenses: Agent 3 (Efficiency) correctly found nothing in its scope; Agent 2 caught C8 only
because it checked the test host's MCM load to answer the focus question; Agent 5 caught C7 by tracing
the warning from code to doc to commit body.

## Feedback memories to codify

Three lessons, appended to the category files:

- `docs/reviews/lessons/testing-qa.md`: a test's expected value never comes from the code under test,
  and its name states what its assertions pin (C4, C7).
- `docs/reviews/lessons/testing-qa.md`: when a change makes a class testable, delete the notes that
  said it was not (C8, C11).
- `docs/reviews/lessons/harmony-il.md`: a helper type a patch needs lives in its own file; a comment
  pinning where code must sit to satisfy a source scan is the tell (C1).

## Follow-ups

Pre-existing code the review saw, recorded in the deep-review report's FOLLOW-UP list; issue filing
waits on Mike in this run: the hotkey's undefined `InputKey` values, Patch35's catch message, the
CultureDoctrine fallback question (no runtime effect), a NaN `VictimMaxHealth` path,
`NameplateFadeSettingsProvider`'s constructor read, an MCM-bump checklist line, and #706.

## Convergence rounds

| Round | Diff read | Finding | Why missed | Preventive action |
|---|---|---|---|---|
| 1 | `bf1898c3..3dc3739c` | None: the fix diff, the cached settings references (against the installed MCMv5), the reordered gates and the id slice were re-checked clean | n/a | n/a |
