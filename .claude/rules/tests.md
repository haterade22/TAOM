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
`MethodName_StateUnderTest_ExpectedBehavior`

Examples:
- `LoadRegions_ValidJson_ReturnsRegionList`
- `GetWage_Tier10_ReturnsExtendedWage`
- `LoadRegions_MissingFile_ReturnsEmptyAndLogs`

## Framework
- **MSTest** — `[TestClass]`, `[TestMethod]`, `[TestInitialize]`, `[TestCleanup]`
- **NSubstitute** — `Substitute.For<T>()`, `.Returns()`, `.Received()`
- **No Moq** — Project uses NSubstitute exclusively

## Coverage Requirements
| Layer | Required |
|-------|----------|
| Services/Engines | 100% |
| Hooks | 80%+ |
| Entry Points | N/A (thin delegation) |
| Adapters | Via service tests |

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

A `-p:TaomGameRefs=RefAsm` build (`.ai/verification.md`) uses metadata-only reference assemblies,
where every engine method body throws. Tag the class `[TestCategory("RequiresGame")]` when a test executes
engine code (constructing a `Vec2`, `TextObject`, `ExplainedNumber` or `CampaignBehaviorBase`
counts, even through TAOM code), and `[TestCategory("LiveInstall")]` when it reads the live
Armory or the vanilla install. A class tag is the default even when some of its tests would pass
on the stubs; you may tag only the methods that need the game when that returns checks worth
running there (`Patch86HideoutBossFightBindingTests`). `RequiresGame` only removes a test from
the unit step: a `BindingVerification` test that executes engine code, or needs vanilla method
IL or vanilla data, carries `RequiresGameIL` on the method. An untagged test that needs the game
fails in a RefAsm run with a `NullReferenceException` from a `TaleWorlds` frame (a TaleWorlds attribute
constructor run by `GetCustomAttributes` included), a `TypeInitializationException` wrapping one, a
`FileNotFoundException` for a module assembly (`SandBox*`, `StoryMode*`,
`TaleWorlds.MountAndBlade.View`) or a game-bin dependency (`System.Management`, `Steamworks.NET`,
`GalaxyCSharp`, `StbSharp`), or a `ReflectionTypeLoadException` from `Assembly.GetTypes()` on TAOM's
assembly (the test output does not print its `LoaderExceptions`): tag it, never catch the exception.
A RefAsm build points `GameAssemblies` at its stub `refasm-game` folder, so `EnsureLoaded()` returns
true there: an `if (!EnsureLoaded()) Assert.Inconclusive` guard does not keep a test off the stubs.
`RequiresGameIL` removes a test only from the gate, so a test outside `BindingVerification` that
reads vanilla IL or vanilla data carries `RequiresGame`.

## Test Organization
Mirror source structure: `TAOM.Tests/Features/{FeatureName}/{ServiceName}Tests.cs`
