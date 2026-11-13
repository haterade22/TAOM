# Plan 046: The test suite reads the C# corpus once, skips Languages in two character scans, and compiles each shipped XSLT once

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: from your worktree,
> `git diff --stat 7087fa633..HEAD -- TAOM.Tests/Infrastructure/RepoPaths.cs TAOM.Tests/Infrastructure/RepoPathsTests.cs TAOM.Tests/Adapters TAOM.Tests/Composition TAOM.Tests/Core TAOM.Tests/Features/CulturalFeats TAOM.Tests/Features/LinesOfDescent TAOM.Tests/Features/NotableServices TAOM.Tests/Features/SeatOfSeeing TAOM.Tests/Features/SpecialResources TAOM.Tests/Features/SwornAnew TAOM.Tests/Features/Warg TAOM.Tests/Features/EyeOfSauron TAOM.Tests/Features/FireAndTribute TAOM.Tests/Features/CultureConversion TAOM.Tests/Features/CustomBattles TAOM.Tests/Features/Diplomacy TAOM.Tests/Features/Execution TAOM.Tests/Features/KeepWardens TAOM.Tests/Features/NazgulFamily TAOM.Tests/Infrastructure`.
> Any output means an in-scope area changed since this plan was written: compare every "Current
> state" excerpt you rely on with the live file; a mismatch is a STOP condition.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: LOW
- **Depends on**: none
- **Category**: tests
- **Planned at**: commit `7087fa633`, 2026-11-12
- **Baseline at that commit**: dotnet `Passed!  - Failed:     0, Passed: 39250, Skipped:     9, Total: 39259, Duration: 1 m 52 s`
  (game-backed desktop run), failing: none. Python suite: `Ran 8578 tests`, `FAILED (failures=7, errors=2, skipped=13)`,
  all heraldry and kingdom-generator data checks that fail on the trunk (`test_make_faction_emblems.ShippedDataTests`
  2 errors, `test_clan_heraldry_specs.ShippedSpecsMirrorTheLiveFiles.test_applying_every_spec_is_a_no_op`,
  `test_generator_regions_in_sync.GeneratorRegionsInSyncTests.test_every_owned_region_matches_a_fresh_run`,
  3 `test_lindon_clan_expansion.LindonClanExpansionDataTests` tests); this plan does not touch `tools/`, so you do not run it.
  Build: 11 warnings, 0 errors.
- **Issue**: filed by the orchestrator before execution.

## Why this matters

`TAOM.Tests` (one net472 MSTest assembly, no parallelisation) re-reads the same files and recompiles the
same stylesheets many times per run. About 45 to 50 source gates each read every `.cs` file under `Main`
and `Client` (4,610 files, about 17.6 million characters) from disk; two character loaders parse 31 MB of
translation XML that holds no character; and 15 call sites (some of them helpers that several tests
call) compile shipped XSLT, including the 1.16 MB `lords.xslt`, once per test or per helper call. Every `/verify`, `/ship` and executor pass pays this. The
plan removes the repeats with the same coverage, in three independent parts, each its own commit. Parts
A and C are speculative wins until measured, so each starts with a measurement and a gate: if the gate
fails you commit nothing for that part and report NOT WORTH IT with the numbers. That outcome is a
success, not a failure.

## Current state

All excerpts are from `7087fa633`.

### Part A: `TAOM.Tests/Infrastructure/RepoPaths.cs` (88 lines)

The shared locator every source gate uses. `CSharpFiles` walks the tree on every call, and both readers
call `File.ReadAllText` with no cache:

```csharp
// RepoPaths.cs:20-33
    /// <summary>The compiled code roots a source-scanning gate reads: core and the client submodule (ADR-013).</summary>
    public static readonly string[] CodeRoots = { "Main", "Client" };

    /// <summary>
    /// Every .cs file under the given repo roots (the compiled code roots when none are named), skipping
    /// any path with an obj or bin segment below the repo root.
    /// </summary>
    public static IEnumerable<string> CSharpFiles(params string[] roots)
    {
        var repoDir = RepoPath();
        return (roots.Length == 0 ? CodeRoots : roots)
            .SelectMany(root => Directory.EnumerateFiles(RepoPath(root), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Substring(repoDir.Length).Split('\\', '/').Any(seg => seg == "obj" || seg == "bin"));
    }

// RepoPaths.cs:50-59
    public static string ReadSource(string relativePath, bool stripComments = false)
    {
        var path = RepoPath(relativePath.Split('/', '\\'));
        if (!File.Exists(path))
            Assert.Fail($"Source file not found: {path}. RepoPaths resolves from its own compile-time path "
                + "([CallerFilePath]); a <PathMap> on TAOM.Tests would break that.");

        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        return stripComments ? StripComments(text) : text;
    }

// RepoPaths.cs:75-85
    public static bool MentionsOutsideComments(string path, string needle, bool wholeWord = false)
    {
        var text = File.ReadAllText(path);
        if (text.IndexOf(needle, System.StringComparison.Ordinal) < 0)
            return false;

        var stripped = StripComments(text);
        return wholeWord
            ? Regex.IsMatch(stripped, @"\b" + Regex.Escape(needle) + @"\b")
            : stripped.Contains(needle);
    }
```

The file's usings (lines 1-6) are `System.Collections.Generic`, `System.IO`, `System.Linq`,
`System.Runtime.CompilerServices`, `System.Text.RegularExpressions`, `Microsoft.VisualStudio.TestTools.UnitTesting`.

**Its tests**, `TAOM.Tests/Infrastructure/RepoPathsTests.cs` (128 lines, 8 `[TestMethod]`s and one
`[DataTestMethod]` with 6 DataRows), include three that a careless cache breaks:

- `:47-60` `ReadSource_NormalisesAnExplicitCrlfFile_ToLf` writes a probe under `TAOM.Tests/bin/`.
- `:98-118` `MentionsOutsideComments_ReadsCodeNotComments` rewrites **the same path**
  `TAOM.Tests/bin/repopaths-mentions-probe.txt` with different content in each of its 6 DataRows.
- `:70-75` expects `AssertFailedException` from `ReadSource("Main/NoSuchFile_Plan018.cs")`, and `:120-127`
  expects `FileNotFoundException` from `MentionsOutsideComments` on a missing `TAOM.Tests/bin` path.

So the cache holds only `.cs` files under the four code roots, and every other path reads the disk.

**Code-root sizes** (`git ls-files "<root>/*.cs" | wc -l`, then the bytes): `Main` 4,342 files, 16,164,690
bytes; `Client` 268, 1,455,486; `Dependencies` 22, 183,832; `CoopCompat` 12, 32,623.

**No test writes a `.cs` file under a code root.** `git grep -nE '(WriteAllText|WriteAllLines|WriteAllBytes|AppendAllText|File\.Copy|File\.Move|File\.Delete)\(' -- TAOM.Tests`
filtered for `RepoPath("Main"|"Client"|"Dependencies"|"CoopCompat"` or a `.cs"` literal returned nothing,
and no write call names a `.cs` literal. Scope: the tracked `.cs` files under `TAOM.Tests`.

**Call sites.** `git grep -n 'CSharpFiles(' -- TAOM.Tests` gives 67 calls outside `RepoPaths.cs`. They fall
into four groups.

1. **Callers that read the text themselves with `File.ReadAllText` over a whole root.** The cache inside
   `RepoPaths` cannot help these unless the read goes through it. This plan converts them (Step 5); the
   list is in Scope with each line.
2. **Callers that read through `MentionsOutsideComments` or `ReadSource`.** They gain with no edit:
   `Adapters/PartyScreenCommitTests.cs:33`, `Composition/FeatureModulesTests.cs:194`,
   `Features/ArmyTargeting/HoldTheWalls/HoldTheWallsWiringTests.cs:82`, `Features/Diplomacy/WarOfTheRingWiringTests.cs:45`,
   `Features/EyeOfSauron/EyeOfSauronWiringTests.cs:71`, `Features/FarWatch/FarWatchWiringTests.cs:100`,
   `Features/FiefManagement/FiefsNavButtonWiringTests.cs:138`, `Features/FieldCamp/CampActivitiesWiringTests.cs:99`,
   `Features/FireAndTribute/FireAndTributeWiringTests.cs:83`, `Features/FromTheAshes/FromTheAshesWiringTests.cs:69`,
   `Features/HeroRace/RacePersistenceWiringTests.cs:40`, `Features/HiddenHosts/HiddenHostsWiringTests.cs:181`,
   `Features/NamedCompanions/Oaths/OathsOfServiceWiringTests.cs:77`, `Features/WarChronicle/WarChronicleWiringTests.cs:45`,
   `Features/WarChronicle/Events/WarChronicleEventsWiringTests.cs:48`, `Features/WarPledge/WarPledgeWiringTests.cs:79`,
   `Features/WatchfulHolds/WatchfulHoldsWiringTests.cs:80`, `Infrastructure/ClientIsolationGateTests.cs:138`,
   `Infrastructure/MusicPauseGateTests.cs:30`, `Features/SkeletonBuffer/SkeletonBufferWiringTests.cs:173` (`ReadSource`).
   Also `MapPopupGateTests.cs:29` and `MainPartyNullCheckScanTests.cs:119`, whose second read is converted.
3. **Do not edit.** The two `File.ReadAllLines` callers,
   `Infrastructure/Localization/LocalizationKeyConsistencyTests.cs:265` and
   `Infrastructure/Localization/UnregisteredLocalizationKeyBaselineTests.cs:113` (a text cache does not
   replace a line read as is), and every call site in the skip-list folders, which belong to other work
   in flight: `TAOM.Tests/Features/CoopInterop`, `LongDefeat`, `FordsOfWar`, `HeroDefence`, `FieldBallista`
   (`CoopPresenceReadGateTests.cs:43`, `CoopVetoClassificationTests.cs:494` and `:601`,
   `ResetForUnloadSweepTests.cs:35` and `:81`, `SinglePlayerGateScanTests.cs:27`, `FordsDomainPurityTests.cs:68`
   and `:73`, `LongDefeatDomainPurityTests.cs:43`, `LongDefeatAdapterRulesTests.cs:107`,
   `LongDefeatBlowOwnerTests.cs:52`, `LongDefeatSallyWiringTests.cs:59`). They still get the cached file list.
4. **Callers that filter the list to one feature folder before reading.** They read a handful of files
   and stay as they are: `Features/MarshalsWord/MarshalMarkCaptureAndShotTableTests.cs:552`,
   `Features/MarshalsWord/TaomMarshalHotKeyCategoryTests.cs:144`, `Features/SeatOfSeeing/TaomSeatHotKeyCategoryTests.cs:143`,
   `Features/WardensTally/WardensTallyWiringTests.cs:107`, and `RepoPathsTests.cs` itself.

**Decision: convert group 1 to a new `RepoPaths.Text(path)`.** Groups 2 and 1 are about half of the
whole-tree passes each, so a cache that only `ReadSource` and `MentionsOutsideComments` see saves about
half of what the measurement in Step 2 counts. The conversion is a mechanical replace of the read
expression on 26 lines in 23 files (27 read expressions: `CommandTrialWiringTests.cs:125` holds two); it
changes no assertion.

**Name lookup.** Files that import `RepoPaths` only through `using static TAOM.Tests.Infrastructure.RepoPaths;`
and sit outside the `TAOM.Tests.Infrastructure` namespace cannot write `RepoPaths.Text`; they write bare
`Text(...)`. That applies to `Adapters/AffectRelativesGateTests.cs` and
`Features/CulturalFeats/PartyMemberSizeLimitCallShapeTests.cs`. Every other converted file either has
`using TAOM.Tests.Infrastructure;` or is in that namespace, and writes `RepoPaths.Text(...)`. No namespace
named `Text` exists in the repo (`git grep -nE '^namespace [A-Za-z.]*\bText\b' -- '*.cs'` returned nothing),
and none of the 23 files declares a member named `Text`.

### Part B: two character loaders

`TAOM.Tests/Features/EyeOfSauron/HuntTroopListsDataTests.cs:23-44` (and line for line the same loader in
`TAOM.Tests/Features/FireAndTribute/FireAndTributeDataTests.cs:57`, `:67-84`):

```csharp
    private static readonly Lazy<Dictionary<string, XElement>> Characters = new(LoadCharacters);

    private static Dictionary<string, XElement> LoadCharacters()
    {
        var byId = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData"), "*.xml",
                     SearchOption.AllDirectories))
        {
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (System.Xml.XmlException) { continue; }
            foreach (var el in doc.Descendants("NPCCharacter"))
            {
                ...
            }
        }
        Assert.IsTrue(byId.Count > 1000, $"only {byId.Count} NPCCharacters parsed; the scan is broken");
        return byId;
    }
```

FireAndTribute's `foreach` is one line: `foreach (var file in Directory.EnumerateFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData"), "*.xml", SearchOption.AllDirectories))` (`:70`).
Both files already have `using System;`, `using System.IO;`, `using System.Linq;`.

Facts (run at `7087fa633`): `git grep -l '<NPCCharacter' -- Main/_Module/ModuleData/Languages | wc -l` prints `0`;
289 tracked XML files are under `Main/_Module/ModuleData/Languages/` (31,374,231 bytes) out of 457 under
ModuleData; no tracked path has a `Languages` folder anywhere else under ModuleData. The class comment of
`HuntTroopListsDataTests` (`:14-15`) says what the loader is for: "Every host troop must exist as an
NPCCharacter in Main/_Module/ModuleData". Each loader already sits in a static `Lazy`, so each class
parses the tree once per run.

`Features/NamedCompanions/NamedCompanionShippedDataTests.cs` also scans ModuleData but returns before
reading anything today. Leave it.

### Part C: 15 sites that compile XSLT

`git grep -n 'new XslCompiledTransform()' -- TAOM.Tests` gives exactly 15 lines. Two loader styles:

- **Style A** (`Load(string)`, framework defaults for the reader, `XsltSettings` and resolver), 9 sites:

  | Site | Lines | Stylesheet | Called |
  |---|---|---|---|
  | `TAOM.Tests/Core/CommentStringTagsTests.cs` | 80-81 | `XsltPath()` (comment_strings.xslt) | once |
  | `TAOM.Tests/Core/CultureExecutionerTests.cs` | 97-98 | `Path.Combine(ModuleData(), "spcultures.xslt")` | once |
  | `TAOM.Tests/Core/CulturePartyTemplateTests.cs` | 339-340 | `Path.Combine(CultureDataFixture.ModuleDataPath(), "spcultures.xslt")` | 4 calls (`:91`, `:132`, `:194`, `:218`) |
  | `TAOM.Tests/Core/LordTemplateRosterTests.cs` | 38-40 | `Path.Combine(ModuleData(), "lord_template_rosters.xslt")`, inside `LoadTransform()` | 3 calls (`:74`, `:93`, `:158`) |
  | `TAOM.Tests/Core/NotableTemplateGenderTests.cs` | 157-158 | `Path.Combine(ModuleData(), "spcultures.xslt")` | once |
  | `TAOM.Tests/Core/RenamedCultureWandererListTests.cs` | 33-34 | `Path.Combine(moduleData, "spcultures.xslt")` | once |
  | `TAOM.Tests/Features/CultureConversion/GarrisonCultureCoverageTests.cs` | 247-248 | `xsltPath` (spcultures.xslt), inside `if (File.Exists(xsltPath))` | |
  | `TAOM.Tests/Features/CustomBattles/CustomBattleLordStubsTests.cs` | 64-65 | `Path.Combine(ModuleData(), "lords.xslt")` | once |
  | `TAOM.Tests/Features/NazgulFamily/NazgulRaceDataTests.cs` | 45-46 | `Path.Combine(CultureDataFixture.ModuleDataPath(), "lords.xslt")` | 2 calls (`:64`, `:96`) |

  The shape, from `CulturePartyTemplateTests.cs:339-340`:
  ```csharp
          var transform = new XslCompiledTransform();
          transform.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "spcultures.xslt"));
  ```

- **Style B** (`XmlReader` with `DtdProcessing.Ignore` and `XmlResolver = null`, `XsltSettings(false, false)`,
  `null` resolver), 6 sites:

  | Site | Lines | Stylesheet | `settings` used again after the Load? |
  |---|---|---|---|
  | `TAOM.Tests/Core/CustomBattleTroopFilterTests.cs` | 25-28 | `RepoPaths.RepoPath(Sheet.Split('/'))` | yes, `:30` |
  | `TAOM.Tests/Core/LordFamilyTransformTests.cs` | 69-72 | parameter `stylesheet` | yes, `:74` |
  | `TAOM.Tests/Core/VanillaWandererTemplateTransformTests.cs` | 37-40 | `LordsXslt` | no |
  | `TAOM.Tests/Features/Diplomacy/ShippedKingdomDataTests.cs` | 121-125 | `Path.Combine(CultureDataFixture.ModuleDataPath(), "spkingdoms.xslt")` | yes, `:128` |
  | `TAOM.Tests/Features/Execution/ShippedClanSideCoverageTests.cs` | 116-119 | parameter `stylesheet` | no |
  | `TAOM.Tests/Features/KeepWardens/ShippedCastleOwnerCultureTests.cs` | 36-39 | parameter `stylesheet` | no |

  The shape, from `LordFamilyTransformTests.cs:69-72`:
  ```csharp
          var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
          var xslt = new XslCompiledTransform();
          using (var sheet = XmlReader.Create(stylesheet, settings))
              xslt.Load(sheet, new XsltSettings(false, false), null);
  ```

The two styles compile the same program for every shipped stylesheet today only because none uses
`xsl:import`, `xsl:include`, `document()`, a DOCTYPE or a script. A later `xsl:include` would resolve under
A and fail under B, so the cache keys on (full path, style) and copies each style's own `Load` call: no
site changes loader.

Several style B classes are tagged `[TestCategory("LiveInstall")]` (they read the vanilla install):
`LordFamilyTransformTests.cs:30` (class), `ShippedClanSideCoverageTests.cs:35` (class),
`ShippedCastleOwnerCultureTests.cs:22` (class), `VanillaWandererTemplateTransformTests.cs:72`,
`ShippedKingdomDataTests.cs:107`. They run only on a machine with the game, so the proof is the
game-backed full suite on the desktop.

**Where the helper goes.** `TAOM.Tests/Core/CultureDataFixture.cs` (107 lines, `internal static class
CultureDataFixture` in namespace `TAOM.Tests.Core`) holds culture-data lists and `ModuleDataPath()`; a
stylesheet cache is a different job, so it goes in a new file beside it, `TAOM.Tests/Core/CompiledXslt.cs`,
same namespace, `internal`.

**Using lines.** Sites in namespace `TAOM.Tests.Core` need nothing. `CustomBattleLordStubsTests.cs:8`,
`ShippedKingdomDataTests.cs:10`, `ShippedCastleOwnerCultureTests.cs:9`, `NazgulRaceDataTests.cs:7` already
have `using TAOM.Tests.Core;`. `GarrisonCultureCoverageTests.cs` (namespace `TAOM.Tests.Features.CultureConversion`)
and `ShippedClanSideCoverageTests.cs` (namespace `TAOM.Tests.Features.Execution`) do not, and get one.

**A lead for the gate.** The production cache `Main/Features/XmlMerge/XsltTransformCache.cs:11-12` says
lords.xslt "takes 136 to 212 ms to compile", and `:20` says "a reused instance also skips the first-run
JIT of its templates, about 1 s for lords.xslt". That is why Step 8 measures the Load and the first
Transform together: a shared instance avoids both. It is a claim in a comment, not your evidence; your
own Stopwatch numbers decide.

### Conventions and binding rules

- **ADR-008** (testability): services stay testable without the game. This plan adds no service and no
  engine call; the new tests touch no TaleWorlds type, so the RefAsm unit step is not needed.
- **ADR-002** (entry points under 150 lines) and **ADR-007** (adapters) do not apply: test code only.
- **ADR-003, 004, 005**: no `#region`, no `[Obsolete]`, no `#if`. `BannedConstructsGateTests` scans
  `TAOM.Tests` too, so the new files must hold none.
- `.claude/rules/tests.md`: names are `MethodName_StateUnderTest_ExpectedBehavior`; MSTest and NSubstitute
  only; a path key compares with `OrdinalIgnoreCase`; judge each test by the two-edit test (break the
  behaviour and it fails, change only internals and it passes).
- `.claude/rules/simplicity-criterion.md`: a tiny win plus added complexity is rejected. That is why
  Parts A and C are gated on a measured time.
- Lessons that apply: `docs/reviews/lessons/testing-qa.md` "A caching refactor's test puts a live object
  behind the cache and edits it afterwards" (the bin probe test in Step 3 does that for the uncached
  paths) and `docs/reviews/lessons/build-tooling-workflow.md` "A performance stage carries a measured
  cost" (the gates).
- Test code only: nothing under `Main`, `Client`, `Dependencies` or `CoopCompat` changes.

### Blast radius

`RepoPaths` is the only changed type with callers outside its own file. `python tools/graphify_taom.py
affected "RepoPaths" --depth 2` could not run when this plan was written (the graph for the planning
worktree was missing, and a refresh into scratch was killed in clustering after about 10 minutes). By
grep: `git grep -l 'RepoPaths' -- TAOM.Tests | wc -l` prints `418`; nothing under `Main`, `Client` or
`TAOM.CoopCompat.Tests` names it. Every public signature stays the same and one public method is added,
so callers recompile unchanged.

In Step 1, check the graph with `timeout 120 python -B tools/graphify_taom.py status --brief`. Only when
it exits 0 (fresh), run `timeout 300 python -B tools/graphify_taom.py affected "RepoPaths" --depth 2` and
paste its output into your report. On any other exit (1 stale, 2 missing, 3 environment), quote the
status line and the grep count `418` above, and go on. **Never run `refresh`**, whatever the status
output suggests: from a linked worktree it writes a graph outside your writable paths
(`tools/graphify_taom.py:70-86`), and a rebuild may take up to `EXTRACT_TIMEOUT = 1500` s plus
`CLUSTER_TIMEOUT = 600` s (`:58-59`), past the 15-minute call limit. In a linked worktree at
`7087fa633`, `status --brief` printed `graphify code graph: MISSING (no taom-stamp.json beside the graph
(built outside the wrapper?))` and exited 2.

## Commands you will need

Run every command from your worktree. Prefix every dotnet command with the TEMP and TMP your dispatch
rules give, quoted. `<myscratch>` in this plan is your own scratch folder, `<scratch>\<your label>` from
your dispatch rules (create it first); every log, probe output and script goes there.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `timeout 900 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the base totals plus the new tests |
| One class | the same, plus `--filter "FullyQualifiedName~RepoPathsTests"` | the named tests run; a filter matching nothing proves nothing |
| Tests a change reaches | `python tools/graphify_taom.py tests "<Type>"` (needs the graph, which was missing at planning; optional here, since every filter this plan needs is spelled out) | prints a `dotnet test --filter` line |

The test command builds `TAOM.Tests`, which builds `Main` and `Client`, so it also proves the test code
compiles. `python tools/validate_moduledata.py` and `python tools/lint_docs.py --fail-on-drift` are not
needed: this plan changes no data and no doc. Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you modify):

Part A:
- `TAOM.Tests/Infrastructure/RepoPaths.cs`
- `TAOM.Tests/Infrastructure/RepoPathsTests.cs`
- The 23 group-1 files, each on exactly the listed lines (replace the read expression `File.ReadAllText(x)`
  or `System.IO.File.ReadAllText(x)` with `RepoPaths.Text(x)`, or with bare `Text(x)` where marked
  *bare*; a method group `File.ReadAllText` becomes `RepoPaths.Text`):
  - `TAOM.Tests/Adapters/`: `AffectRelativesGateTests.cs:24` (*bare*), `MapPopupGateTests.cs:30`
  - `TAOM.Tests/Composition/`: `FeatureModulesTests.cs:88`, `SubModuleMissionViewWiringTests.cs:24`
  - `TAOM.Tests/Core/`: `MainPartyNullCheckScanTests.cs:120`
  - `TAOM.Tests/Features/`: `CulturalFeats/PartyMemberSizeLimitCallShapeTests.cs:33` and `:50` (*bare*),
    `LinesOfDescent/FamilyTreeWiringTests.cs:56`, `NotableServices/NotableServicesWiringTests.cs:87` (only
    this line; `:152`, `:278`, `:383` read other files and stay),
    `SeatOfSeeing/CommandTrialWiringTests.cs:125` (both reads on the line; `:131` and `:438` stay),
    `SeatOfSeeing/SeatPanelWiringTests.cs:168`, `SeatOfSeeing/SeatTimeRiseWiringTests.cs:64` and `:124`,
    `SpecialResources/SpecialResourceEarningServiceWiringTests.cs:23`, `SwornAnew/SwornAnewWiringTests.cs:193`,
    `Warg/WargRageFordsTests.cs:42`
  - `TAOM.Tests/Infrastructure/`: `BannedConstructsGateTests.cs:169`, `ClientIsolationGateTests.cs:48`,
    `EntryPointLineCountGateTests.cs:237` (method group: `.Select(RepoPaths.Text)`),
    `PatchCategoryApplierTests.cs:376` (only this line; `:390`, `:432`, `:443`, `:455` stay),
    `PhotoModeEscapeMenuGateTests.cs:29`, `PrefabExtensionBrushTests.cs:164` and `:233`,
    `PrefabSpriteTests.cs:186`, `ServiceLocatorGateTests.cs:41`, `SilentCatchRatchetTests.cs:42`

Part B:
- `TAOM.Tests/Features/EyeOfSauron/HuntTroopListsDataTests.cs`
- `TAOM.Tests/Features/FireAndTribute/FireAndTributeDataTests.cs`

Part C:
- `TAOM.Tests/Core/CompiledXslt.cs` (new)
- `TAOM.Tests/Core/CompiledXsltTests.cs` (new)
- The 15 files in the two Part C tables, on the listed lines, plus one `using TAOM.Tests.Core;` line in
  `GarrisonCultureCoverageTests.cs` and `ShippedClanSideCoverageTests.cs`

Temporary, never committed: `TAOM.Tests/Infrastructure/Plan046CorpusProbeTests.cs` and
`TAOM.Tests/Core/Plan046XsltProbeTests.cs`, plus the temporary logging lines in `RepoPaths.cs` (Step 2).

**Out of scope** (do NOT touch, even though they look related):
- Anything under `Main`, `Client`, `Dependencies`, `CoopCompat`, including `Main/Features/XmlMerge/XsltTransformCache.cs`
  (a production type with its own engine-matching loader; tests do not reuse it).
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` (single-owner) and the protected files
  `.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props`, `docs/adrs/*.md`.
  This plan needs none of them.
- Every file under `TAOM.Tests/Features/CoopInterop`, `LongDefeat`, `FordsOfWar`, `HeroDefence`, `FieldBallista`.
- `LocalizationKeyConsistencyTests.cs` and `UnregisteredLocalizationKeyBaselineTests.cs` (the `ReadAllLines` callers).
- The group-4 filtered callers, `Infrastructure/Localization/RegisteredDefaultRoundTripTests.cs` (its own
  `*.*` walk), `NamedCompanionShippedDataTests.cs`, and the 40 other files with their own
  `Descendants("NPCCharacter")` loader (`git grep -l 'Descendants("NPCCharacter")' -- TAOM.Tests | wc -l`
  prints `42` at `7087fa633`, the two Part B files included). A shared character loader is a separate refactor.
- `CHANGELOG.md`, `plans/README.md`, `.claude/rules/tests.md`.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist entry. STOP and report instead.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.38` at planning; a hook
  refuses any other). Suggested subjects:
  - Part A: `test(tests): <version> - Read the C# corpus once per test run`
  - Part B: `test(tests): <version> - Skip Languages in the two NPCCharacter scans`
  - Part C: `test(tests): <version> - Compile each shipped stylesheet once per run`
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72, with your measured numbers (the gate figures and the suite Duration before and after). No AI
  attribution trailer. Write it to a file in your scratch folder and run `git commit -F "<file>"`. Stage
  explicit paths only, never `-A`, `-u` or `.`. Never `--no-verify`.
- One commit per part that passes its gate. A part whose gate fails gets no commit.

## Steps

### Step 1: record the base

Run the drift check above. Then run the full suite once before any edit:

```
TEMP="<tmp>" TMP="<tmp>" timeout 900 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > "<myscratch>/step1-base.log" 2>&1
```

Write the totals line (with its `Duration`) and any failing test names into your report. Run the
graph status check from "Current state", Blast radius, and do what it says there (`affected` on exit 0;
otherwise quote the status line and the grep count). Never `refresh`.

**Verify**: the totals line reads `Failed: 0, Passed: 39250, Skipped: 9, Total: 39259` (Duration will
differ), or the difference is explained by commits between `7087fa633` and your base. A base failure in
any class this plan edits is a STOP condition.

### Step 2: Part A measurement and gate (no production change)

1. Write `TAOM.Tests/Infrastructure/Plan046CorpusProbeTests.cs`:

   ```csharp
   using System;
   using System.Collections.Generic;
   using System.Diagnostics;
   using System.IO;
   using System.Linq;
   using Microsoft.VisualStudio.TestTools.UnitTesting;

   namespace TAOM.Tests.Infrastructure;

   [TestClass]
   public class Plan046CorpusProbeTests
   {
       [TestMethod]
       public void Probe_CorpusPassTime()
       {
           var outDir = Environment.GetEnvironmentVariable("TAOM_046_OUT");
           if (string.IsNullOrEmpty(outDir)) Assert.Fail("set TAOM_046_OUT to your scratch folder");
           var lines = new List<string>();
           for (var pass = 1; pass <= 3; pass++)
           {
               var sw = Stopwatch.StartNew();
               var files = 0;
               long chars = 0;
               foreach (var text in RepoPaths.CSharpFiles().Select(File.ReadAllText)) { files++; chars += text.Length; }
               lines.Add($"pass {pass}: {files} files, {chars} chars, {sw.ElapsedMilliseconds} ms");
           }
           File.AppendAllLines(Path.Combine(outDir!, "corpus-probe.txt"), lines);
       }
   }
   ```

2. Add a temporary pass counter as the first statement of `RepoPaths.CSharpFiles` (before `var repoDir`):

   ```csharp
           var plan046Out = System.Environment.GetEnvironmentVariable("TAOM_046_OUT");
           if (!string.IsNullOrEmpty(plan046Out))
           {
               var caller = new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod();
               File.AppendAllText(Path.Combine(plan046Out, "passes.log"),
                   "CSharpFiles|" + string.Join(",", roots) + "|" + caller?.DeclaringType?.FullName + "|" + caller?.Name + "\n");
           }
   ```

3. Run the probe alone, then the full suite with the counter on (your scratch folder as a Windows path):

   ```
   TAOM_046_OUT="<myscratch>" TEMP="<tmp>" TMP="<tmp>" timeout 900 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~Plan046CorpusProbeTests"
   TAOM_046_OUT="<myscratch>" TEMP="<tmp>" TMP="<tmp>" timeout 900 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > "<myscratch>/step2-full.log" 2>&1
   ```

   If the probe-only run fails with `set TAOM_046_OUT to your scratch folder`, or `passes.log` is
   missing or empty after the full run, the variable did not reach the test host: retry that run once
   with `-e TAOM_046_OUT="<myscratch>"` added to its `dotnet test` command line (use `-e` on both runs
   from then on). If it still fails or stays empty, STOP.

   The full run here also runs `Plan046CorpusProbeTests` again, so `corpus-probe.txt` ends with six
   lines and the full-run total is one above Step 1's. This run exists only to fill `passes.log`: it
   is not judged by its totals, and the "changes its count" STOP condition does not apply to it. A
   test that fails in it is still a STOP.

4. Count the passes the cache saves. Write this script to `<myscratch>/count_passes.py` and run
   `python -B "<myscratch>/count_passes.py" "<myscratch>/passes.log"`:

   ```python
   import sys

   SAVED = {
       # group 1, converted in Step 5
       "AffectRelativesGateTests", "MapPopupGateTests", "FeatureModulesTests", "SubModuleMissionViewWiringTests",
       "MainPartyNullCheckScanTests", "PartyMemberSizeLimitCallShapeTests", "FamilyTreeWiringTests",
       "NotableServicesWiringTests", "CommandTrialWiringTests", "SeatPanelWiringTests", "SeatTimeRiseWiringTests",
       "SpecialResourceEarningServiceWiringTests", "SwornAnewWiringTests", "WargRageFordsTests",
       "BannedConstructsGateTests", "ClientIsolationGateTests", "EntryPointLineCountGateTests",
       "PatchCategoryApplierTests", "PhotoModeEscapeMenuGateTests", "PrefabExtensionBrushTests",
       "PrefabSpriteTests", "ServiceLocatorGateTests", "SilentCatchRatchetTests",
       # group 2, through MentionsOutsideComments or ReadSource
       "PartyScreenCommitTests", "HoldTheWallsWiringTests", "WarOfTheRingWiringTests", "EyeOfSauronWiringTests",
       "FarWatchWiringTests", "FiefsNavButtonWiringTests", "CampActivitiesWiringTests", "FireAndTributeWiringTests",
       "FromTheAshesWiringTests", "RacePersistenceWiringTests", "HiddenHostsWiringTests", "OathsOfServiceWiringTests",
       "WarChronicleWiringTests", "WarChronicleEventsWiringTests", "WarPledgeWiringTests", "WatchfulHoldsWiringTests",
       "MusicPauseGateTests", "SkeletonBufferWiringTests",
   }
   total = saved = 0
   for line in open(sys.argv[1], encoding="utf-8"):
       parts = line.rstrip("\n").split("|")
       if len(parts) < 4:
           continue
       total += 1
       roots, owner = parts[1], parts[2]
       if roots and "Main" not in roots.split(","):
           continue  # a Client-only pass is under a tenth of the corpus
       if owner.split("+")[0].rsplit(".", 1)[-1] in SAVED:
           saved += 1
   print("calls=%d saved_passes=%d" % (total, saved))
   ```

5. **GATE A**: let `warm` be the mean of passes 2 and 3 in the first three lines of `corpus-probe.txt`
   (the probe-only run), in seconds, and `N` the `saved_passes` count. Continue to Step 3 only if `(N - 1) * warm >= 5.0`.
   Write `N`, the three pass times, and the product into your report either way.

6. Remove the temporary code now, whatever the gate says: delete `Plan046CorpusProbeTests.cs` (with
   `rm`, it is untracked) and remove the counter block from `RepoPaths.cs` with Edit.

**Verify**: `git status --porcelain` prints nothing, and `git grep -n "TAOM_046_OUT" -- TAOM.Tests` prints nothing.
If GATE A failed, skip Steps 3 to 6, record "Part A: NOT WORTH IT" with the numbers, and go to Step 7.

### Step 3: Part A RED: the cache tests

0. Run `--filter "FullyQualifiedName~RepoPathsTests"` once before any edit and record its `Total:`
   (14 expected if each DataRow counts as one result; use the number you see).
1. Add a stub to `RepoPaths.cs`, after `ReadSource`, so the new tests compile:
   `public static string Text(string path) => File.ReadAllText(path);`
2. Append these five tests to `RepoPathsTests` (the file already has `using System.IO;` and `using System.Linq;`):

   ```csharp
       [TestMethod]
       public void Text_CodeRootFile_ReturnsOneCachedInstance()
       {
           var path = RepoPaths.RepoPath("Main", "SubModule.cs");

           var first = RepoPaths.Text(path);

           Assert.AreEqual(File.ReadAllText(path), first);
           Assert.AreSame(first, RepoPaths.Text(path), "a code-root .cs file is read once per test run");
       }

       [TestMethod]
       public void Text_EitherSeparator_ReturnsTheSameCachedInstance()
       {
           var path = RepoPaths.RepoPath("Client", "ClientSubModule.cs");

           Assert.AreSame(RepoPaths.Text(path), RepoPaths.Text(path.Replace('\\', '/')));
       }

       // A .cs name under bin: only the root check keeps it out of the cache.
       [TestMethod]
       public void Text_FileOutsideTheCodeRoots_IsReadFreshEveryCall()
       {
           var probe = RepoPaths.RepoPath("TAOM.Tests", "bin", "repopaths-text-probe.cs");
           Directory.CreateDirectory(Path.GetDirectoryName(probe)!);
           try
           {
               File.WriteAllText(probe, "first");
               Assert.AreEqual("first", RepoPaths.Text(probe));
               File.WriteAllText(probe, "second");
               Assert.AreEqual("second", RepoPaths.Text(probe), "only the four code roots are cached");
           }
           finally
           {
               File.Delete(probe);
           }
       }

       [TestMethod]
       public void CSharpFiles_KeepsTheCallersRootOrderAndTheDirectoryWalkOrder()
       {
           var repoDir = RepoPaths.RepoPath();
           foreach (var roots in new[] { new[] { "Main", "Client" }, new[] { "Client", "Main" } })
           {
               var walked = roots
                   .SelectMany(r => Directory.EnumerateFiles(RepoPaths.RepoPath(r), "*.cs", SearchOption.AllDirectories))
                   .Where(f => !f.Substring(repoDir.Length).Split('\\', '/').Any(s => s == "obj" || s == "bin"))
                   .ToList();

               CollectionAssert.AreEqual(walked, RepoPaths.CSharpFiles(roots).ToList(), string.Join(",", roots) + ": first call");
               CollectionAssert.AreEqual(walked, RepoPaths.CSharpFiles(roots).ToList(), string.Join(",", roots) + ": second call");
           }
       }

       [TestMethod]
       public void MentionsOutsideComments_MissingCodeRootFile_Throws()
       {
           var missing = RepoPaths.RepoPath("Main", "NoSuchFile_Plan046.cs");

           Assert.ThrowsException<FileNotFoundException>(() => RepoPaths.MentionsOutsideComments(missing, "X"));
           Assert.ThrowsException<FileNotFoundException>(() => RepoPaths.MentionsOutsideComments(missing, "X"),
               "a failed read must cache nothing");
       }
   ```

3. Run `--filter "FullyQualifiedName~RepoPathsTests"` (with the TEMP and TMP prefix).

**Verify**: the build succeeds and the run reports a `Total:` of the item 0 count plus 5 (19 if it was
14) with exactly two failures: `Text_CodeRootFile_ReturnsOneCachedInstance` and `Text_EitherSeparator_ReturnsTheSameCachedInstance`,
each an `Assert.AreSame` failure. Quote both. Any other failure is a STOP condition.

### Step 4: Part A GREEN: the cache in `RepoPaths`

Replace the stub and change `CSharpFiles`, `ReadSource` and `MentionsOutsideComments` to this shape (add
`using System;` and `using System.Collections.Concurrent;` to the usings; keep `RepoPath`, `StripComments`,
`ThisFile`, `CommentPattern` and `CodeRoots` as they are):

```csharp
    /// <summary>
    /// The roots whose .cs text a run reads once and keeps. No test writes a .cs file under them; a test that
    /// starts to must read that file with File.ReadAllText, not through <see cref="Text"/>.
    /// </summary>
    private static readonly string[] CorpusRoots = { "Main", "Client", "Dependencies", "CoopCompat" };

    // One list per root, in Directory.EnumerateFiles order, so a caller's root order and the walk order both survive.
    private static readonly ConcurrentDictionary<string, string[]> CorpusFiles = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> CorpusText = new(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> CSharpFiles(params string[] roots) =>
        (roots.Length == 0 ? CodeRoots : roots).SelectMany(root =>
            Array.IndexOf(CorpusRoots, root) >= 0
                ? CorpusFiles.GetOrAdd(root, r => WalkCSharpFiles(r).ToArray())
                : WalkCSharpFiles(root));

    /// <summary>
    /// The raw text of a file, decoded as File.ReadAllText decodes it. A .cs file under Main, Client,
    /// Dependencies or CoopCompat (outside obj and bin) is read once per test run and the same string is
    /// returned after; every other path reads the disk on each call. A missing file throws
    /// FileNotFoundException and caches nothing.
    /// </summary>
    public static string Text(string path)
    {
        if (CorpusText.TryGetValue(path, out var cached))
            return cached;

        var full = Path.GetFullPath(path);
        return IsCorpusFile(full) ? CorpusText.GetOrAdd(full, p => File.ReadAllText(p)) : File.ReadAllText(path);
    }

    private static IEnumerable<string> WalkCSharpFiles(string root)
    {
        var repoDir = RepoPath();
        return Directory.EnumerateFiles(RepoPath(root), "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f.Substring(repoDir.Length)));
    }

    private static bool IsBuildOutput(string repoRelative) =>
        repoRelative.Split('\\', '/').Any(seg => seg == "obj" || seg == "bin");

    private static bool IsCorpusFile(string fullPath)
    {
        var repoDir = RepoPath();
        return fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            && fullPath.StartsWith(repoDir, StringComparison.OrdinalIgnoreCase)
            && !IsBuildOutput(fullPath.Substring(repoDir.Length))
            && CorpusRoots.Any(root => fullPath.StartsWith(RepoPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }
```

In `ReadSource`, change only `File.ReadAllText(path)` to `Text(path)` (the `File.Exists` check and its
`Assert.Fail` stay first). In `MentionsOutsideComments`, change only `File.ReadAllText(path)` to `Text(path)`.
Extend the class summary by one sentence: "Source text under the code roots is read once per test run; see
<see cref="Text"/>." Keep the existing `CSharpFiles` doc comment.

Run `--filter "FullyQualifiedName~RepoPathsTests"`: all 19 pass. Then prove the three guard tests that
the stub did not fail, one at a time, with Edit (never git): make the change, run the filter, see the
named test fail, quote it, undo the change, re-run green.

| Temporary change | Must fail |
|---|---|
| insert `return true;` as the first statement of `IsCorpusFile` (an unreachable-code warning is expected) | `Text_FileOutsideTheCodeRoots_IsReadFreshEveryCall` |
| in `CSharpFiles`, `(roots.Length == 0 ? CodeRoots : roots)` becomes `(roots.Length == 0 ? CodeRoots : roots).OrderBy(r => r, StringComparer.Ordinal)` | `CSharpFiles_KeepsTheCallersRootOrderAndTheDirectoryWalkOrder` |
| in `Text`, the corpus branch becomes `(File.Exists(full) ? CorpusText.GetOrAdd(full, p => File.ReadAllText(p)) : string.Empty)` | `MentionsOutsideComments_MissingCodeRootFile_Throws` |

**Verify**: each mutation run shows its named test failing (other failures in that run are fine); after
each undo, `--filter "FullyQualifiedName~RepoPathsTests"` reports `Failed: 0` and the Step 3 total.

### Step 5: Part A: convert the group-1 call sites

1. Before editing, run the filter for the 23 classes and record its totals:

   ```
   --filter "FullyQualifiedName~AffectRelativesGateTests|FullyQualifiedName~MapPopupGateTests|FullyQualifiedName~FeatureModulesTests|FullyQualifiedName~SubModuleMissionViewWiringTests|FullyQualifiedName~MainPartyNullCheckScanTests|FullyQualifiedName~PartyMemberSizeLimitCallShapeTests|FullyQualifiedName~FamilyTreeWiringTests|FullyQualifiedName~NotableServicesWiringTests|FullyQualifiedName~CommandTrialWiringTests|FullyQualifiedName~SeatPanelWiringTests|FullyQualifiedName~SeatTimeRiseWiringTests|FullyQualifiedName~SpecialResourceEarningServiceWiringTests|FullyQualifiedName~SwornAnewWiringTests|FullyQualifiedName~WargRageFordsTests|FullyQualifiedName~BannedConstructsGateTests|FullyQualifiedName~ClientIsolationGateTests|FullyQualifiedName~EntryPointLineCountGateTests|FullyQualifiedName~PatchCategoryApplierTests|FullyQualifiedName~PhotoModeEscapeMenuGateTests|FullyQualifiedName~PrefabExtensionBrushTests|FullyQualifiedName~PrefabSpriteTests|FullyQualifiedName~ServiceLocatorGateTests|FullyQualifiedName~SilentCatchRatchetTests"
   ```

2. Make the 26 line edits listed in Scope (27 read expressions, two of them on
   `CommandTrialWiringTests.cs:125`), each with Edit. Examples of the exact form:
   - `MapPopupGateTests.cs:30`: `.Where(p => EncounterTest.IsMatch(RepoPaths.StripComments(File.ReadAllText(p))))` becomes
     `.Where(p => EncounterTest.IsMatch(RepoPaths.StripComments(RepoPaths.Text(p))))`
   - `AffectRelativesGateTests.cs:24` (*bare*): `.Select(f => (Path: f, Text: File.ReadAllText(f)))` becomes
     `.Select(f => (Path: f, Text: Text(f)))`
   - `SeatTimeRiseWiringTests.cs:64`: `RepoPaths.StripComments(System.IO.File.ReadAllText(f))` becomes `RepoPaths.StripComments(RepoPaths.Text(f))`
   - `EntryPointLineCountGateTests.cs:237`: `RepoPaths.CSharpFiles().Select(File.ReadAllText).ToList()` becomes `RepoPaths.CSharpFiles().Select(RepoPaths.Text).ToList()`
   - `CommandTrialWiringTests.cs:125`: both `File.ReadAllText(f)` on the line become `RepoPaths.Text(f)`.

   Change nothing else on those lines or in those files. If a bare `Text(` fails to compile, write
   `TAOM.Tests.Infrastructure.RepoPaths.Text(` on that line instead.

3. Re-run the same filter.

**Verify**: the filter totals equal the ones from item 1 (same Passed, Failed 0). This command (the
"Part A count", reused in Done criteria):

```
git grep -c "File.ReadAllText" -- TAOM.Tests/Adapters/AffectRelativesGateTests.cs TAOM.Tests/Adapters/MapPopupGateTests.cs TAOM.Tests/Composition/FeatureModulesTests.cs TAOM.Tests/Composition/SubModuleMissionViewWiringTests.cs TAOM.Tests/Core/MainPartyNullCheckScanTests.cs TAOM.Tests/Features/CulturalFeats/PartyMemberSizeLimitCallShapeTests.cs TAOM.Tests/Features/LinesOfDescent/FamilyTreeWiringTests.cs TAOM.Tests/Features/NotableServices/NotableServicesWiringTests.cs TAOM.Tests/Features/SeatOfSeeing/CommandTrialWiringTests.cs TAOM.Tests/Features/SeatOfSeeing/SeatPanelWiringTests.cs TAOM.Tests/Features/SeatOfSeeing/SeatTimeRiseWiringTests.cs TAOM.Tests/Features/SpecialResources/SpecialResourceEarningServiceWiringTests.cs TAOM.Tests/Features/SwornAnew/SwornAnewWiringTests.cs TAOM.Tests/Features/Warg/WargRageFordsTests.cs TAOM.Tests/Infrastructure/BannedConstructsGateTests.cs TAOM.Tests/Infrastructure/ClientIsolationGateTests.cs TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs TAOM.Tests/Infrastructure/PhotoModeEscapeMenuGateTests.cs TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs TAOM.Tests/Infrastructure/PrefabSpriteTests.cs TAOM.Tests/Infrastructure/ServiceLocatorGateTests.cs TAOM.Tests/Infrastructure/SilentCatchRatchetTests.cs
```

prints exactly three lines: `TAOM.Tests/Features/NotableServices/NotableServicesWiringTests.cs:3`,
`TAOM.Tests/Features/SeatOfSeeing/CommandTrialWiringTests.cs:2`, `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs:4`.
(At `7087fa633` it prints 23 lines, those three reading 4, 3 and 5.)
`git diff --stat` lists only `RepoPaths.cs`, `RepoPathsTests.cs` and the 23 files.

### Step 6: Part A: full suite and commit

Run the full suite to `<myscratch>/step6-full.log`.

**Verify**: `Failed: 0`, `Total:` equal to the Step 1 total plus 5, `Skipped:` equal to Step 1's. Write the
Duration beside Step 1's in your report (other agents may share the machine, so note it as one run each).
Then stage the 25 files by explicit path and commit with the Part A subject. The body says the corpus is
read once, that only `.cs` files under the four code roots are cached, and gives the gate numbers and both
Durations. Trailer: `Not-tested: none; test code only`.

### Step 7: Part B: skip Languages in the two character loaders

1. **GATE B**: `git grep -l "<NPCCharacter" -- Main/_Module/ModuleData/Languages | wc -l` prints `0`. If it
   prints anything else, STOP: the folder holds characters and the exclusion would drop them.
2. Run `--filter "FullyQualifiedName~HuntTroopListsDataTests|FullyQualifiedName~FireAndTributeDataTests"`
   and record its totals and Duration.
3. In both `LoadCharacters` methods, replace the `foreach` header with this shape (keep the loop body):

   ```csharp
           // Languages holds string tables, never an NPCCharacter, so the scan skips it.
           var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
           var languages = Path.Combine(moduleData, "Languages") + Path.DirectorySeparatorChar;
           foreach (var file in Directory.EnumerateFiles(moduleData, "*.xml", SearchOption.AllDirectories)
                        .Where(f => !f.StartsWith(languages, StringComparison.OrdinalIgnoreCase)))
   ```

   The exclusion is by the path prefix `ModuleData\Languages\`, so a file elsewhere whose name contains
   the word stays in. Add nothing else; do not share the loader between the two files.
4. Re-run the filter from item 2.

**Verify**: the same totals as item 2 with `Failed: 0` (record the new Duration). Then run the full suite:
`Failed: 0`, total unchanged from the last full run. Commit the two files with the Part B subject; the body
says why the folder is skipped and that the character set is unchanged (the zero-hit grep). Trailer:
`Not-tested: none; test code only`.

### Step 8: Part C measurement and gate

1. Write `TAOM.Tests/Core/Plan046XsltProbeTests.cs`:

   ```csharp
   using System;
   using System.Collections.Generic;
   using System.Diagnostics;
   using System.IO;
   using System.Linq;
   using System.Xml.Linq;
   using System.Xml.Xsl;
   using Microsoft.VisualStudio.TestTools.UnitTesting;

   namespace TAOM.Tests.Core;

   [TestClass]
   public class Plan046XsltProbeTests
   {
       [TestMethod]
       public void Probe_LordsXsltCompileCost()
       {
           var outDir = Environment.GetEnvironmentVariable("TAOM_046_OUT");
           if (string.IsNullOrEmpty(outDir)) Assert.Fail("set TAOM_046_OUT to your scratch folder");
           var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "lords.xslt");
           var lines = new List<string>();
           for (var round = 1; round <= 3; round++)
           {
               var sw = Stopwatch.StartNew();
               var transform = new XslCompiledTransform();
               transform.Load(path);
               var load = sw.ElapsedMilliseconds;
               sw.Restart();
               Run(transform);
               var first = sw.ElapsedMilliseconds;
               sw.Restart();
               Run(transform);
               var second = sw.ElapsedMilliseconds;
               lines.Add($"round {round}: load={load} ms, firstTransform={first} ms, secondTransform={second} ms, saved={load + first - second} ms");
           }
           File.AppendAllLines(Path.Combine(outDir!, "xslt-probe.txt"), lines);
       }

       // NazgulRaceDataTests.cs:48-54, the sentinel stub of the Nine.
       private static void Run(XslCompiledTransform transform)
       {
           var stub = new XElement("NPCCharacters",
               new[] { "lord_1_15", "lord_1_155", "lord_1_16", "lord_1_28", "lord_1_38", "lord_1_48", "lord_1_48_1", "lord_1_48_2", "lord_1_48_3" }
                   .Select(id => new XElement("NPCCharacter", new XAttribute("id", id), new XAttribute("race", "SENTINEL_RACE"))));
           var output = new XDocument();
           using (var writer = output.CreateWriter())
               transform.Transform(new XDocument(stub).CreateReader(), null, writer);
       }
   }
   ```

2. Run it alone: `TAOM_046_OUT="<myscratch>" ... --filter "FullyQualifiedName~Plan046XsltProbeTests"`.
3. **GATE C**: the cost of one compile is what a shared instance avoids: `saved` (the Load plus the first
   Transform, minus a Transform on an already used instance). Take the mean `saved` of rounds 2 and 3
   (round 1 also pays the framework's own one-time JIT). Continue only if it is at least 300 ms. Write
   all three rounds, including `load` alone, into your report either way.
4. Delete `Plan046XsltProbeTests.cs` with `rm`.

**Verify**: `git status --porcelain` prints nothing. If GATE C failed, skip Steps 9 to 12, record
"Part C: NOT WORTH IT" with the numbers, and go to Step 13.

### Step 9: Part C RED: the cache tests

1. Write `TAOM.Tests/Core/CompiledXslt.cs` with the final shape below, except that `Get` is the stub
   `public static XslCompiledTransform Get(string stylesheetPath, XsltLoadStyle style) => Compile(Path.GetFullPath(stylesheetPath), style);`
   (no `Cache` field yet).
2. Write `TAOM.Tests/Core/CompiledXsltTests.cs`:

   ```csharp
   using System;
   using System.IO;
   using Microsoft.VisualStudio.TestTools.UnitTesting;

   namespace TAOM.Tests.Core;

   [TestClass]
   public class CompiledXsltTests
   {
       private const string Sheet =
           "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
           + "<xsl:template match=\"/\"><out/></xsl:template></xsl:stylesheet>";

       private static string WriteSheet()
       {
           var dir = Path.Combine(Path.GetTempPath(), "taom-compiledxslt-" + Guid.NewGuid().ToString("N"));
           Directory.CreateDirectory(dir);
           var path = Path.Combine(dir, "probe.xslt");
           File.WriteAllText(path, Sheet);
           return path;
       }

       [TestMethod]
       public void Get_SamePathAndStyle_ReturnsOneCompiledInstance()
       {
           var path = WriteSheet();
           try
           {
               Assert.AreSame(CompiledXslt.Get(path, XsltLoadStyle.FrameworkDefaults), CompiledXslt.Get(path, XsltLoadStyle.FrameworkDefaults));
               Assert.AreSame(CompiledXslt.Get(path, XsltLoadStyle.IgnoreDtdNoResolver), CompiledXslt.Get(path, XsltLoadStyle.IgnoreDtdNoResolver));
           }
           finally
           {
               Directory.Delete(Path.GetDirectoryName(path)!, true);
           }
       }

       [TestMethod]
       public void Get_SamePathOtherStyle_CompilesItsOwnInstance()
       {
           var path = WriteSheet();
           try
           {
               Assert.AreNotSame(CompiledXslt.Get(path, XsltLoadStyle.FrameworkDefaults), CompiledXslt.Get(path, XsltLoadStyle.IgnoreDtdNoResolver));
           }
           finally
           {
               Directory.Delete(Path.GetDirectoryName(path)!, true);
           }
       }
   }
   ```

3. Run `--filter "FullyQualifiedName~CompiledXsltTests"`.

**Verify**: `Total: 2`, exactly one failure, `Get_SamePathAndStyle_ReturnsOneCompiledInstance`, an
`Assert.AreSame` failure. Quote it.

### Step 10: Part C GREEN

Give `CompiledXslt.cs` its final shape:

```csharp
using System.Collections.Concurrent;
using System.IO;
using System.Xml;
using System.Xml.Xsl;

namespace TAOM.Tests.Core;

/// <summary>How a test compiles a stylesheet. Each value is one loader the tests used before the cache, copied as it was.</summary>
internal enum XsltLoadStyle
{
    /// <summary><c>XslCompiledTransform.Load(string)</c>: the framework's default reader, settings and resolver.</summary>
    FrameworkDefaults,

    /// <summary>An <c>XmlReader</c> that ignores a DTD and resolves nothing, <c>XsltSettings(false, false)</c>, no resolver.</summary>
    IgnoreDtdNoResolver,
}

/// <summary>
/// One compiled stylesheet per (full path, load style) for the whole test run, so a test that runs a shipped
/// XSLT does not compile it again. A compiled XslCompiledTransform is safe to share for Transform. A
/// stylesheet that fails to compile throws on every call and is never cached. Not re-validated: a test
/// that edits a stylesheet during the run must compile it itself.
/// </summary>
internal static class CompiledXslt
{
    // The path half compares ordinally on purpose: the same file spelled in another case only compiles
    // one more copy, never a wrong one.
    private static readonly ConcurrentDictionary<(string Path, XsltLoadStyle Style), XslCompiledTransform> Cache = new();

    public static XslCompiledTransform Get(string stylesheetPath, XsltLoadStyle style) =>
        Cache.GetOrAdd((Path.GetFullPath(stylesheetPath), style), key => Compile(key.Path, key.Style));

    private static XslCompiledTransform Compile(string path, XsltLoadStyle style)
    {
        var xslt = new XslCompiledTransform();
        if (style == XsltLoadStyle.FrameworkDefaults)
        {
            xslt.Load(path);
            return xslt;
        }

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using (var sheet = XmlReader.Create(path, settings))
            xslt.Load(sheet, new XsltSettings(false, false), null);
        return xslt;
    }
}
```

Run `--filter "FullyQualifiedName~CompiledXsltTests"`: both pass. Then prove the second test with Edit:
change the key in `Get` to `(Path.GetFullPath(stylesheetPath), XsltLoadStyle.FrameworkDefaults)`, run the
filter, see `Get_SamePathOtherStyle_CompilesItsOwnInstance` fail, quote it, undo, re-run green.

**Verify**: after the undo, the filter reports `Failed: 0`, `Total: 2`.

### Step 11: Part C: convert the 15 sites

1. Before editing, run the filter for the 15 classes and record the totals:

   ```
   --filter "FullyQualifiedName~CommentStringTagsTests|FullyQualifiedName~CultureExecutionerTests|FullyQualifiedName~CulturePartyTemplateTests|FullyQualifiedName~CustomBattleTroopFilterTests|FullyQualifiedName~LordFamilyTransformTests|FullyQualifiedName~LordTemplateRosterTests|FullyQualifiedName~NotableTemplateGenderTests|FullyQualifiedName~RenamedCultureWandererListTests|FullyQualifiedName~VanillaWandererTemplateTransformTests|FullyQualifiedName~GarrisonCultureCoverageTests|FullyQualifiedName~CustomBattleLordStubsTests|FullyQualifiedName~ShippedKingdomDataTests|FullyQualifiedName~ShippedClanSideCoverageTests|FullyQualifiedName~ShippedCastleOwnerCultureTests|FullyQualifiedName~NazgulRaceDataTests"
   ```

2. **Style A** (9 sites): replace the two lines `var transform = new XslCompiledTransform();` and
   `transform.Load(<expr>);` with `var transform = CompiledXslt.Get(<expr>, XsltLoadStyle.FrameworkDefaults);`,
   keeping `<expr>` exactly as written. In `LordTemplateRosterTests.cs:38-40`, the three lines become
   `return CompiledXslt.Get(Path.Combine(ModuleData(), "lord_template_rosters.xslt"), XsltLoadStyle.FrameworkDefaults);`.
3. **Style B** (6 sites): replace the `new XslCompiledTransform()` line and the two-line `using (var sheet = ...) xslt.Load(...)`
   with `var xslt = CompiledXslt.Get(<the path expression from XmlReader.Create>, XsltLoadStyle.IgnoreDtdNoResolver);`.
   Keep the `var settings = ...` line where the table says it is used again (CustomBattleTroopFilter,
   LordFamilyTransform, ShippedKingdomData); delete it where it is not (VanillaWandererTemplateTransform,
   ShippedClanSideCoverage, ShippedCastleOwnerCulture). Example, `ShippedKingdomDataTests.cs:122-125` becomes
   `var xslt = CompiledXslt.Get(Path.Combine(CultureDataFixture.ModuleDataPath(), "spkingdoms.xslt"), XsltLoadStyle.IgnoreDtdNoResolver);`.
4. Add `using TAOM.Tests.Core;` in alphabetical place to `GarrisonCultureCoverageTests.cs` and
   `ShippedClanSideCoverageTests.cs`. If that makes a name ambiguous, remove it again and qualify the two
   names as `TAOM.Tests.Core.CompiledXslt` and `TAOM.Tests.Core.XsltLoadStyle` on that line instead.
   Leave the other usings as they are.
5. Re-run the filter from item 1.

**Verify**: the same totals as item 1, `Failed: 0`. `git grep -n --untracked "new XslCompiledTransform()" -- TAOM.Tests`
prints exactly one line, in `TAOM.Tests/Core/CompiledXslt.cs`. The `--untracked` is required: that file is
new and not yet staged, and a plain `git grep` searches tracked files only, so without it the command
prints nothing.

### Step 12: Part C: full suite and commit

Run the full suite to `<myscratch>/step12-full.log`.

**Verify**: `Failed: 0`; `Total:` equal to the Step 1 total plus 5 if Part A landed, plus 2; `Skipped:` as
in Step 1. Commit `CompiledXslt.cs`, `CompiledXsltTests.cs` and the 15 site files by explicit path with
the Part C subject. The body names the two loader styles, says no site changed loader, and gives the
GATE C numbers and the Duration. It also says that GATE C priced one `lords.xslt` compile only, while
most repeated compiles are of `spcultures.xslt` (five style A sites, one of them a helper called four times). Trailer: `Not-tested: none; test code only`.

### Step 13: build check and report

Run the build command (with the TEMP and TMP prefix):
`dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`.

**Verify**: exit 0 and `0 Error(s)` (11 warnings at `7087fa633`; this plan touches nothing `Main` compiles,
so the warning count should not change). Then report, per part: the gate numbers, LANDED (commit hash) or NOT WORTH IT, the full-suite totals line from
Step 1 and from your last full run with both Durations, the Part B filter Durations before and after, and
the mutation-proof failures you quoted.

## Test plan

- `TAOM.Tests/Infrastructure/RepoPathsTests.cs`, 5 new tests: a code-root file returns one cached instance
  (RED with the stub); either separator reaches the same entry (RED with the stub); a `.cs` under
  `TAOM.Tests/bin` is read fresh after a rewrite (proved by the `IsCorpusFile` mutation); `CSharpFiles`
  keeps the caller's root order and the walk order on the first and the cached call (proved by the
  sorted-roots mutation); a missing code-root file throws `FileNotFoundException` twice (proved by the
  empty-string mutation). The existing 14 results keep guarding the uncached paths and the two
  missing-file failures.
- `TAOM.Tests/Core/CompiledXsltTests.cs`, 2 new tests: same path and style returns one instance (RED
  with the stub); the other style compiles its own instance (proved by the path-only key mutation).
- Pattern: model the probe-file tests on `RepoPathsTests.ReadSource_NormalisesAnExplicitCrlfFile_ToLf`.
- Part B adds no test: the zero-hit grep is its evidence, and the two classes' unchanged totals are the
  regression check.
- What cannot be tested: nothing engine-bound is touched.

## Done criteria

Machine-checkable. ALL must hold, for each part that passed its gate:

- [ ] The build command in Step 13 exits 0 with `0 Error(s)`.
- [ ] The last full run reads `Failed: 0`, and its `Total:` is the Step 1 total plus 5 (Part A landed)
      plus 2 (Part C landed), with Step 1's `Skipped:`.
- [ ] Part A: the "Part A count" command from Step 5 Verify prints only
      `TAOM.Tests/Features/NotableServices/NotableServicesWiringTests.cs:3`,
      `TAOM.Tests/Features/SeatOfSeeing/CommandTrialWiringTests.cs:2`,
      `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs:4`.
- [ ] Part B: `git grep -c "f.StartsWith(languages, StringComparison.OrdinalIgnoreCase)" -- TAOM.Tests/Features/EyeOfSauron/HuntTroopListsDataTests.cs TAOM.Tests/Features/FireAndTribute/FireAndTributeDataTests.cs`
      prints both paths, each with `:1`.
- [ ] Part C: `git grep -n "new XslCompiledTransform()" -- TAOM.Tests` prints one line, in `TAOM.Tests/Core/CompiledXslt.cs`.
- [ ] `git diff --stat <your base>..HEAD -- Main Client Dependencies CoopCompat TAOM.Tests/Features/CoopInterop TAOM.Tests/Features/LongDefeat TAOM.Tests/Features/FordsOfWar TAOM.Tests/Features/HeroDefence TAOM.Tests/Features/FieldBallista TAOM.Tests/Infrastructure/Localization`
      prints nothing.
- [ ] `git grep -n "TAOM_046_OUT" -- TAOM.Tests` prints nothing, `git ls-files TAOM.Tests | grep -c "Plan046.*ProbeTests"`
      prints `0`, and `git status --porcelain` prints nothing.
- [ ] Every doc comment and code comment this plan supplied was re-checked against the code it describes.

## STOP conditions

Stop and report (do not improvise) if:

- The code at a "Current state" location does not match its excerpt, or a listed line no longer holds
  the read it names.
- A test outside the new ones fails or changes its count after any step. Never adjust such a test.
- GATE B's grep returns a file.
- `passes.log` stays empty after the `-e` retry (Step 2).
- A mutation in Step 4 or Step 10 does not make its named test fail: the test does not guard what it claims.
- A converted call site turns out to read a file outside the four code roots that a test writes during
  the run, or you find any test that writes a `.cs` file under `Main`, `Client`, `Dependencies` or `CoopCompat`.
- A fix seems to need a file under `Main`, `Client`, `Dependencies`, `CoopCompat`, a skip-list folder,
  one of the two `ReadAllLines` callers, `IoC.cs`, `SubModule.cs`, the csproj or a protected file.
- An XSLT site fails only after it shares a compiled instance (the assumption "a compiled
  XslCompiledTransform behaves the same on every Transform" is false for that sheet).
- A step's verification fails twice after a reasonable fix.

A failed GATE A or GATE C is not a STOP: skip that part as the step says and continue.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (test-suite cost; no player-facing change).
- No `/localize`, no feature doc and no feature-map row: test infrastructure only.
- After merge, consider one line in `.claude/rules/tests.md` (a paths rule, not protected): "Read a code-root
  `.cs` file with `RepoPaths.Text`; compile a shipped stylesheet with `CompiledXslt.Get` and the loader style
  the test needs." That steers new tests onto the caches; it is outside this plan's scope.

## After merge: the maintainer's actions

None. No hook, setting or tracked-file removal.

## Maintenance notes

- A future test that writes a `.cs` file under a code root during the run and reads it back through
  `RepoPaths.Text`, `ReadSource` or `MentionsOutsideComments` would see stale text. The `CorpusRoots` doc
  comment says so; a reviewer should probe any new write under `Main` or `Client` in tests.
- The skip-list folders still call `File.ReadAllText` themselves; when their work lands, the same
  mechanical conversion can follow.
- A shipped stylesheet that starts to use `xsl:include` keeps working under style A and fails under
  style B, exactly as today, because the cache keys on the style.
- Review should probe: `Text` normalising forward slashes and case (the `OrdinalIgnoreCase` key and the
  `GetFullPath` fallback), the `bin` and `obj` exclusion in `IsCorpusFile`, and that each style B site kept
  its `settings` line only where it is still used.
- Deferred: a shared `NPCCharacter` loader for the 40 other files that carry their own, and an IL call index
  (a separate audit finding that needs its own measurement first).
