---
paths:
  - "TAOM.Tests/**"
  - "**/*Tests.cs"
  - "**/*Test.cs"
  - "tools/tests/**"
---

# Testing Rules (TDD Mandatory)

## Workflow: RED -> GREEN -> REFACTOR
1. Write a failing test FIRST (verify RED state)
2. Write minimum production code to pass (GREEN)
3. Refactor while keeping tests green

Judge every test by the two-edit test: break the behaviour (it must fail), change only the internals (it must pass); see `4-completeness.md` item 9 in `.claude/skills/deep-review/lenses/`.

For `tools/tests/**` (Python `unittest`), only this workflow and the gate rule below apply: a test
method must be named `test_*` or it never runs; the naming, framework and layout sections are C#.

A gate (a test that lints or audits the repo) is proven on the states it will meet, not the one it
was written in: a fresh checkout (`git check-attr text eol` on every file its tool reads), an
untracked new file, a deleted one, a non-ASCII path, a folder whose case on disk differs from git's
(so a path key compares with `OrdinalIgnoreCase`), a run from a subfolder, a missing tool, and an
empty input that must not read as a pass (lesson in `docs/reviews/lessons/build-tooling-workflow.md`).

## Naming Convention
`MethodName_StateUnderTest_ExpectedBehavior`. A gate or invariant with no meaningful state may use `Subject_ExpectedBehavior`.

Examples:
- `LoadRegions_ValidJson_ReturnsRegionList`
- `GetWage_Tier10_ReturnsExtendedWage`
- `LoadRegions_MissingFile_ReturnsEmptyAndLogs`

## Framework
- **MSTest** — `[TestClass]`, `[TestMethod]`, `[TestInitialize]`, `[TestCleanup]`
- **NSubstitute** — `Substitute.For<T>()`, `.Returns()`, `.Received()`
- **No Moq** — Project uses NSubstitute exclusively

## Coverage Requirements

Every decision path in a service or engine has a test, judged by the two-edit test. No coverage percentage is measured or claimed.

A test comment or failure message that states an effect (what breaks, what imports, what the engine then does) rests on the line that produces the effect, read at both ends of the data flow. A production comment, a plan or a report is a claim, not that line (`docs/reviews/rca-external-mod-contract-tests-2026-11-13.md`).

A source-text test pins wiring only: this entry point calls that function, with these inputs, in this order. The decision itself goes in a pure type with behaviour tests. Check each pin against the cheapest edit that brings the defect back and keeps the pinned text (`docs/reviews/rca-muster-captain-2026-11-13.md`).

## Skip-Guard Exhaustion (MANDATORY)

When a service method has `if (condition) continue/return` guard clauses, write a test for **every entity state that should be skipped**, not just the obvious ones.

**Why:** Review #23 found a HIGH bug where `EnsureCompanionsPlaced()` had guards for dead heroes, disabled entries, and already-placed companions -- but missed the recruited-and-traveling state. The most important negative case (companion in player's party) was untested.

**Rule:** For any method that iterates entities and conditionally skips:
1. List every possible entity state (use the state matrix from `csharp-architecture.md`)
2. Write one test per skip condition
3. The test name must identify the specific state: `Method_RecruitedCompanion_SkipsPlacement`
4. Prioritize the most common real-world states first -- a companion traveling with the player is more common than a dead companion

**Pattern to apply:**
```
// For each guard clause:  if (!X) continue;
// Write: Method_XisFalse_Skips()
// AND:   Method_XisTrue_Proceeds()
```

## Testing data that reaches the game through a transform

When the thing you need to pin is not a value in a file but a value the ENGINE will compute from that
file, assert on the transform's OUTPUT, not on its source markup. Reading the markup cannot see an
exclusion filter, and it cannot see an attribute that is not there.

**The sentinel-stub pattern.** Build a synthetic input document in the test where every field you care
about holds a unique `SENTINEL_*` value, run the real transform over it with `XslCompiledTransform`
(the same class `MBObjectManager.ApplyXslt` uses), then read the output. Three failure states, all
distinguishable:

| Output | Means |
|---|---|
| field absent | the transform dropped it |
| value still `SENTINEL_*` | the transform never bound it, so the real run inherits vanilla's value |
| value present but not one of ours | bound to the wrong thing |

Use a synthetic stub rather than the installed vanilla file so the test is deterministic and needs no
game install. Pair it with a **whitelist** of ids the project authors rather than a blacklist of
vanilla prefixes: a whitelist needs no exemption for intentional sharing and does not go stale on an
engine bump.

Worked example, pinning culture party-template bindings across `spcultures.xslt`:
`TAOM.Tests/Core/CulturePartyTemplateTests.cs`. The same technique applies to `spclans.xslt` and
`spkingdoms.xslt`, neither of which currently has output coverage; `lords.xslt` has it for the Nine's
race, age, face age and kit (`TAOM.Tests/Features/NazgulFamily/NazgulRaceDataTests.cs`, #644), lord
families with `heroes.xslt` (`LordFamilyTransformTests`) and the vanilla wanderer retag
(`VanillaWandererTemplateTransformTests`, #758).

## Test categories (builds without the game)

Tag a test `RequiresGame` when it executes engine code, even through TAOM code (`Vec2`, `TextObject`, `ExplainedNumber`, a `CampaignBehaviorBase` all count); `LiveInstall` when it reads the Armory or the vanilla install; `RequiresGameIL` on a `BindingVerification` method that needs vanilla IL or data. A class tag is the default; you may tag only the methods that need the game when that returns checks worth running on the stubs (`Patch86HideoutBossFightBindingTests`). A test outside `BindingVerification` that reads vanilla IL or data carries `RequiresGame`. Tag it, never catch the exception. `EnsureLoaded()` is true on stubs, so it is no guard. Symptoms: [.ai/verification.md](../../.ai/verification.md). The build and filter commands are there too (`-p:TaomGameRefs=RefAsm`).

## Test Organization
Mirror source structure: `TAOM.Tests/Features/{FeatureName}/{ServiceName}Tests.cs`
