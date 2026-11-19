# Plan 057: ClipBudgetSignature.Find jumps to the next first-byte candidate instead of testing every offset

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat c6a82aa04..HEAD -- Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs Main/Features/MissionPerf/AnimMemory/SignatureScanSession.cs Main/Features/SkeletonBuffer/SkeletonBufferSignature.cs TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureTests.cs`.
> If any of these files changed since this plan was written, compare the "Current state" excerpts
> with the live code; a mismatch is a STOP condition. (Only the first and the last are edited; the
> middle two are the callers whose behaviour this plan must not change.)

## Status

- **Priority**: P3
- **Effort**: S
- **Risk**: MED (a five-line change, but its output is the offset a native patch is written at)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `c6a82aa04`, 2026-11-19
- **Baseline at that commit** (the orchestrator's run, not re-run by the plan writer): build exit 0;
  dotnet `42943 passed, 3 failed, 9 skipped`; failing: `EveryKey_IsSeededInAllTwelveLanguageFiles`,
  `EveryLanguage_DeclaresARowForEveryEnglishKey`, `NoTranslatedString_MixesWritingSystems` (three
  language-file tests, known and unrelated to this plan). Python suite: not needed (this plan touches
  no `tools/` file).
- **Issue**: #784 (perf(tools): a first-byte prefilter in ClipBudgetSignature.Find)

## Why this matters

`ClipBudgetSignature.Find` is TAOM's only byte-pattern scanner. It tests every offset of the 10.6 MB
`.text` section of `TaleWorlds.Native.dll` with an inner loop. The skeleton buffer guard runs up to
six such scans on the main thread before the first main menu, and the animation clip memory probe
runs one at the first mission. The issue's bench of this loop in TAOM's Debug build measured 152 to
173 ms per launch, and 48 to 58 ms with the change this plan makes. Since v2.0.38 (commit
`116e541d1`, 2026-11-13) an address cache answers these scans on a relaunch with an unchanged DLL,
so the full scans now run only on a cache miss: the first launch after a game update, a missing or
damaged cache file, a cached address whose bytes no longer match, and every run of the two
`LiveInstall` test classes that scan the installed DLL. Those are the launches this plan speeds up.
The hits, their order and the `maxHits` stop must stay identical for every input, because the skeleton
guard writes a jump at the offset `Find` returns: a wrong offset is a crash.

## Current state

### Files

- `Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs`: pattern parsing (`Parse`), the scan
  (`Find`, the method this plan changes, lines 31 to 47), rip-relative targets and the clip-site
  resolve. CRLF line endings: edit it with the Edit tool only, never `sed -i`.
- `TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureTests.cs`: 14 `[TestMethod]`s today;
  this plan adds 11. CRLF line endings.
- `Main/Features/MissionPerf/AnimMemory/SignatureScanSession.cs`: the only production caller of
  `Find` (line 89). Read-only for this plan.
- `Main/Features/SkeletonBuffer/SkeletonBufferSignature.cs`: the skeleton guard's patterns and its
  six scans through the session (lines 81, 82, 85, 86, 106, 109). Read-only for this plan.

### The code this plan changes (`ClipBudgetSignature.cs` at `c6a82aa04`)

```csharp
// ClipBudgetSignature.cs:21-29
    /// <summary>Hex bytes separated by spaces; <c>?</c> or <c>??</c> is a wildcard, returned as -1.</summary>
    internal static int[] Parse(string pattern)
    {
        var tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new int[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
            result[i] = tokens[i] == "?" || tokens[i] == "??" ? -1 : Convert.ToInt32(tokens[i], 16);
        return result;
    }

// ClipBudgetSignature.cs:31-47
    /// <summary>Start offsets of <paramref name="pattern"/> in <paramref name="haystack"/>, at most <paramref name="maxHits"/>.</summary>
    internal static List<int> Find(byte[] haystack, int[] pattern, int maxHits)
    {
        var hits = new List<int>();
        for (var i = 0; i <= haystack.Length - pattern.Length; i++)
        {
            var j = 0;
            while (j < pattern.Length && (pattern[j] == -1 || haystack[i + j] == pattern[j]))
                j++;
            if (j < pattern.Length)
                continue;
            hits.Add(i);
            if (hits.Count >= maxHits)
                break;
        }
        return hits;
    }
```

The file already has `using System;` (line 1) and `using System.Collections.Generic;` (line 2).

Behaviour of the base loop that the new code must keep, derived from the excerpt above:

- `-1` in `pattern` is a wildcard; any other value is compared with `haystack[i + j]` as an int, so a
  value outside 0 to 255 never matches.
- Hits are ascending start offsets, and they may overlap (the next offset tested after a hit is
  `i + 1`).
- `hits.Add` runs before the `maxHits` check, so `maxHits` of 0 or below still returns the first hit.
  No caller passes that (the production callers pass 2, the `LiveInstall` test passes 3), but the
  behaviour stays.
- An empty pattern matches at every offset `0..haystack.Length`, up to `maxHits`.
- A haystack shorter than the pattern gives no hits and no exception.

### The callers (blast radius)

`python tools/graphify_taom.py status --brief` at `c6a82aa04` in the run worktree printed
`graphify code graph: MISSING`, so the caller list comes from `git grep -n -w "ClipBudgetSignature"`
and `git grep -n "\.Find("` instead:

- `Main/Features/MissionPerf/AnimMemory/SignatureScanSession.cs:89`, `FindPlain`:
  `return ClipBudgetSignature.Find(text, pattern, maxHits);`. `SignatureScanSession.Find` (line 75)
  calls `FindPlain` on a cache miss and caches a result only when it holds exactly one hit.
- `Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs:62`:
  `var hits = scan.Find("animmem.clip", text, Parse(Pattern), 2);` (the clip probe, through
  `AnimMemoryProbe.cs:92`).
- `Main/Features/SkeletonBuffer/SkeletonBufferSignature.cs:81-109`: `skeleton.watch`,
  `skeleton.guard`, the foreign guard shape (`FindPlain`, skipped when the guard came from the cache),
  `skeleton.pool2`, `skeleton.guard2` and the second foreign shape, each with `maxHits` 2.
- Tests: `ClipBudgetSignatureTests` (direct), `SkeletonBufferInstalledBinaryTests.cs:106-107`
  (direct, `maxHits` 3, on the installed DLL), and through `Resolve`: `SkeletonBufferSignatureTests`,
  `EngineSignatureCacheTests`, `ClipBudgetSignatureInstalledBinaryTests`.

Every pattern in production code starts with a fixed byte (`8B`, `48`, `41`, `E9`, `4C`), so all of
them take the new path; the leading-wildcard path is reached only by tests and future patterns.

### Engine and runtime facts

No TaleWorlds API is involved: `Find` is pure over a `byte[]`. The one framework call is
`System.Array.IndexOf<T>(T[] array, T value, int startIndex, int count)`. Decompiled from the .NET
Framework 4.x `mscorlib.dll` (the test project and the mod both target `net472`, from
`Directory.Build.props:3`):

```csharp
public static int IndexOf<T>(T[] array, T value, int startIndex, int count)
{
    if (array == null) throw new ArgumentNullException("array");
    if (startIndex < 0 || startIndex > array.Length) throw new ArgumentOutOfRangeException("startIndex", ...);
    if (count < 0 || count > array.Length - startIndex) throw new ArgumentOutOfRangeException("count", ...);
    return EqualityComparer<T>.Default.IndexOf(array, value, startIndex, count);
}
```

For `T = byte`, `EqualityComparer<byte>.Default` is `ByteEqualityComparer`, whose `IndexOf` returns -1
for `count == 0` and otherwise calls `Buffer.IndexOfByte` on a pinned pointer. It returns an index
into the whole array (not relative to `startIndex`), or -1. The project does not set
`CheckForOverflowUnderflow` (no hit in any `.props`, `.csproj` or `.targets`), so `(byte)pattern[0]`
is an unchecked cast and never throws.

### Conventions that bind this change

- ADR-002 (thin entry points under 150 lines): no entry point is touched.
- ADR-007 (services take adapters, never sealed TaleWorlds types): `Find` takes `byte[]` and `int[]`
  only; nothing changes.
- ADR-008 (services are unit-testable without the game): the new tests use no engine type, so they
  need no `[TestCategory("RequiresGame")]` and no RefAsm replay.
- `.claude/rules/tests.md`: MSTest, test names `MethodName_StateUnderTest_ExpectedBehavior`, the
  two-edit test (break the behaviour and the test must fail; change only the internals and it must
  pass).
- `.claude/rules/simplicity-criterion.md`: no helper class, no flag, no new file in `Main/`.
- Lesson `docs/reviews/lessons/build-tooling-workflow.md`, "A differential sweep proves only the
  classes in its corpus; seed it from the fix's own mechanism": the differential corpus below is
  seeded from this change's own assumptions (the candidate count bound, a leading wildcard, an empty
  pattern, overlapping hits, haystacks no longer than the pattern).
- Exemplar of a seeded random differential test: `TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs:51-69`
  (`CollectInRadius_RandomPointsAndCentres_MatchesABruteForceSphereScan`, `new Random(15)`).

## Commands you will need

Prefix every dotnet command with the `TEMP="<tmp>" TMP="<tmp>"` your dispatch rules give.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus 11 passed |
| This class | "This class" below | `Passed: 25` after Step 2 (14 before) |
| Callers' tests | "Callers' tests" below | the count Step 1 recorded, 0 failed |
| Installed DLL | "Installed DLL" below | the same passed and skipped counts as Step 1, 0 failed |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 (only if you touch a doc; this plan does not) |

The three filtered commands, exactly (the `|` inside the double quotes is the MSTest filter OR, not a
shell pipe):

```bash
# This class (the name does not match ClipBudgetSignatureInstalledBinaryTests)
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~ClipBudgetSignatureTests"

# Callers' tests
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SkeletonBufferSignatureTests|FullyQualifiedName~EngineSignatureCacheTests"

# Installed DLL (LiveInstall; prints each test's duration)
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~ClipBudgetSignatureInstalledBinaryTests|FullyQualifiedName~SkeletonBufferInstalledBinaryTests" --logger "console;verbosity=normal"
```

Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you modify):

- `Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs` (the `Find` method and its summary
  only)
- `TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureTests.cs`

**Out of scope** (do NOT touch, even though they look related):

- `SignatureScanSession.cs`, `SkeletonBufferSignature.cs`, `EngineSignatureCache.cs`: the cache and
  the patterns stay as they are.
- `Parse`, `RipTarget`, `Resolve` and `SignatureMatch` in `ClipBudgetSignature.cs`.
- Any pattern string. A pattern change would invalidate every user's cache entry and is not this plan.
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, any `*.props`, anything under `tools/` or
  `.claude/`. Nothing here needs them; if you think it does, STOP.
- A timing test or a benchmark harness: a wall-clock assertion is flaky on a shared machine.
- The gates themselves. Never turn a test green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or changing an existing test's expected value. STOP and report instead.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `perf(skeleton-buffer): <version> - Speed up the engine signature scan`, where `<version>`
  is the `<Version value="...">` in `Main/_Module/SubModule.xml` when you commit (`v2.0.40` when this
  plan was written; a hook refuses any other). At most 72 characters.
- The body is the changelog entry, wrapped at 72, for a reader of the release note. Draft (re-check
  every claim against your own run before you use it):

  ```
  The signature scan that finds the skeleton buffer guard's sites and
  the animation clip budget in TaleWorlds.Native.dll tested every byte
  of the 10.6 MB code section. It now jumps straight to the next byte
  that could start a match, using the framework's fast byte search,
  and checks the full pattern only there. The hits and their order
  are unchanged; a test compares the new search with the old loop on
  random inputs and on every shipped pattern.

  The scan runs only when the address cache misses: the first launch
  after a game update, or when the cache file is missing or out of
  date. Issue #784
  measured that scan at 152 to 173 ms per launch before this change
  and 48 to 58 ms after it, in a Debug build.

  Not-tested: in game (a launch with no address cache)
  ```

  Never edit `CHANGELOG.md`. No AI attribution trailer. Write the message to a file in your scratch
  folder and run `git commit -F "<that file>"`. Stage the two in-scope paths explicitly by name.

## Steps

### Step 1: record the base

1. Run the drift check at the top of this plan. Expected: no output (no in-scope file changed).
2. `file Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureTests.cs`.
   Expected: both `with CRLF line terminators`. Keep them CRLF (the Edit tool does).
3. Run the build, the full test command, and the three filtered commands in the table ("This class",
   "Callers' tests", "Installed DLL"). Write into your report: the full suite's totals line and its
   failing test names; the "This class" passed count (expected `Passed: 14`); the "Callers' tests"
   passed count; and, for "Installed DLL", the passed and skipped counts and the duration the normal
   verbosity prints beside `InstalledNativeDll_Patterns_EachMatchOnceAndNeverInTheForeignShape`
   and `InstalledNativeDll_Signature_MatchesOnceWithTargetsInDataAndRdataAndA12MiBBudget`.

**Verify**: the build exits 0; the full suite matches "Baseline at that commit" (42943 passed, the
three named language-file failures, 9 skipped), or the difference is explained in your report; "This
class" shows `Passed: 14`. The "Installed DLL" tests report `Skipped` (Inconclusive) when no
Bannerlord install resolves: record that, it is not a STOP, but then say in the report and the
`Not-tested:` trailer that the installed-DLL check did not run.

### Step 2: pin today's behaviour with characterization and differential tests

Add the tests below to `ClipBudgetSignatureTests` in
`TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureTests.cs`, after
`Find_HaystackShorterThanPattern_ReturnsEmpty` (lines 78 to 84 today) and before
`RipTarget_RealLoadEncoding_IsDABE40`. Add `using System.Collections.Generic;` and
`using TAOM.Features.SkeletonBuffer;` to the usings at the top (today: `System`, `System.Linq`,
`Microsoft.VisualStudio.TestTools.UnitTesting`, `TAOM.Features.MissionPerf.AnimMemory`).

Why no RED here: this plan changes how `Find` searches, not what it returns, so every new test passes
against the base code; that is the point of them. Each test's ability to fail is proven in Step 4,
against named defects of the new code.

```csharp
    [TestMethod]
    public void Find_LeadingWildcard_MatchesAnyFirstByte()
    {
        var hay = new byte[] { 0x01, 0xBB, 0xFF, 0xBB };

        var hits = ClipBudgetSignature.Find(hay, new[] { -1, 0xBB }, 2);

        CollectionAssert.AreEqual(new[] { 0, 2 }, hits);
    }

    [TestMethod]
    public void Find_FirstByteOnlyInTheLastBytes_ReturnsEmpty()
    {
        // 0xAA sits at the last offset, where the two-byte pattern no longer fits.
        var hay = new byte[] { 0x00, 0x00, 0x00, 0xAA };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB }, 2);

        Assert.AreEqual(0, hits.Count);
    }

    [TestMethod]
    public void Find_OverlappingMatches_ReturnsEachStart()
    {
        var hay = new byte[] { 0xAA, 0xAA, 0xAA, 0xAA };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xAA }, 3);

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, hits);
    }

    [TestMethod]
    public void Find_CandidateFailsAfterTheFirstByte_KeepsSearching()
    {
        var hay = new byte[] { 0xAA, 0x00, 0xAA, 0xBB };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB }, 2);

        CollectionAssert.AreEqual(new[] { 2 }, hits);
    }

    [TestMethod]
    public void Find_MaxHitsOne_StopsAtTheFirstHit()
    {
        var hay = new byte[] { 0xAA, 0xBB, 0xAA, 0xBB };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB }, 1);

        CollectionAssert.AreEqual(new[] { 0 }, hits);
    }

    [TestMethod]
    public void Find_MaxHitsZero_ReturnsTheFirstHit()
    {
        // Pins today's behaviour: the hit is added before the maxHits check.
        var hay = new byte[] { 0xAA, 0xBB, 0xAA, 0xBB };

        var hits = ClipBudgetSignature.Find(hay, new[] { 0xAA, 0xBB }, 0);

        CollectionAssert.AreEqual(new[] { 0 }, hits);
    }

    [TestMethod]
    public void Find_EmptyHaystack_ReturnsEmpty()
    {
        Assert.AreEqual(0, ClipBudgetSignature.Find(new byte[0], new[] { 0xAA }, 2).Count);
        Assert.AreEqual(0, ClipBudgetSignature.Find(new byte[0], new[] { -1 }, 2).Count);
    }

    [TestMethod]
    public void Find_EmptyPattern_MatchesEveryOffsetUpToMaxHits()
    {
        var hits = ClipBudgetSignature.Find(new byte[] { 1, 2, 3 }, new int[0], 2);

        CollectionAssert.AreEqual(new[] { 0, 1 }, hits);
    }

    [TestMethod]
    public void Find_HaystackSameLengthAsPattern_MatchesAtZero()
    {
        var hits = ClipBudgetSignature.Find(new byte[] { 0xAA, 0xBB }, new[] { 0xAA, 0xBB }, 2);

        CollectionAssert.AreEqual(new[] { 0 }, hits);
    }

    [TestMethod]
    public void Find_RandomSmallInputs_MatchesTheBaseLoop()
    {
        // A four-byte alphabet and short haystacks make candidates, overlaps and near-end misses common.
        var rng = new Random(784);
        var bytes = new byte[] { 0x00, 0xAA, 0xBB, 0xFE };
        var tokens = new[] { 0x00, 0xAA, 0xBB, 0xFE, -1 };
        for (var n = 0; n < 20000; n++)
        {
            var hay = new byte[rng.Next(0, 25)];
            for (var k = 0; k < hay.Length; k++)
                hay[k] = bytes[rng.Next(bytes.Length)];
            var pattern = new int[rng.Next(0, 6)];
            for (var k = 0; k < pattern.Length; k++)
                pattern[k] = tokens[rng.Next(tokens.Length)];
            var maxHits = rng.Next(0, 4);

            CollectionAssert.AreEqual(BaseFind(hay, pattern, maxHits), ClipBudgetSignature.Find(hay, pattern, maxHits),
                $"case {n}: hay [{BitConverter.ToString(hay)}], pattern [{string.Join(" ", pattern)}], maxHits {maxHits}");
        }
    }

    [TestMethod]
    public void Find_RealPatternsInRandomCode_MatchesTheBaseLoop()
    {
        var rng = new Random(785);
        var patterns = new[]
        {
            ClipBudgetSignature.Pattern, SkeletonBufferSignature.WatchPattern, SkeletonBufferSignature.Pool2Pattern,
            SkeletonBufferSignature.GuardPattern, SkeletonBufferSignature.ForeignGuardPattern,
            SkeletonBufferSignature.Pool2GuardPattern, SkeletonBufferSignature.Pool2ForeignGuardPattern,
        };
        foreach (var text in patterns)
        {
            var pattern = ClipBudgetSignature.Parse(text);
            var code = new byte[64 * 1024];
            rng.NextBytes(code);
            var end = code.Length - pattern.Length;
            Plant(code, 0x100, pattern, rng);
            Plant(code, 0x8000, pattern, rng);
            Plant(code, end, pattern, rng);

            foreach (var maxHits in new[] { 2, 3 })
            {
                var expected = BaseFind(code, pattern, maxHits);
                CollectionAssert.AreEqual(new[] { 0x100, 0x8000, end }.Take(maxHits).ToList(), expected, text);
                CollectionAssert.AreEqual(expected, ClipBudgetSignature.Find(code, pattern, maxHits), text);
            }
        }
    }

    /// <summary>The scan loop as it was before the first-byte jump (commit c6a82aa04), kept as the oracle.</summary>
    private static List<int> BaseFind(byte[] haystack, int[] pattern, int maxHits)
    {
        var hits = new List<int>();
        for (var i = 0; i <= haystack.Length - pattern.Length; i++)
        {
            var j = 0;
            while (j < pattern.Length && (pattern[j] == -1 || haystack[i + j] == pattern[j]))
                j++;
            if (j < pattern.Length)
                continue;
            hits.Add(i);
            if (hits.Count >= maxHits)
                break;
        }
        return hits;
    }

    /// <summary>Writes <paramref name="pattern"/> at <paramref name="at"/>, with a random byte for each wildcard.</summary>
    private static void Plant(byte[] code, int at, int[] pattern, Random rng)
    {
        for (var k = 0; k < pattern.Length; k++)
            code[at + k] = pattern[k] == -1 ? (byte)rng.Next(256) : (byte)pattern[k];
    }
```

Notes for the executor:

- `BaseFind` must stay a verbatim copy of the base `Find` body quoted in "Current state". Compare it
  line by line with `git show c6a82aa04:Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs`
  lines 32 to 47.
- The first `CollectionAssert` in `Find_RealPatternsInRandomCode_MatchesTheBaseLoop` checks that
  the corpus really holds the three planted hits, so the differential cannot pass on an empty corpus.
  An accidental extra copy of a shipped pattern (22 to 50 bytes, at least 18 of them fixed) in 64 KiB
  of random bytes is not a realistic outcome; if this assert fails, STOP and report the pattern and the
  hits rather than changing the seed.
- `Take` and `ToList` come from `System.Linq`, already imported.

**Verify**: the "This class" filtered command shows `Passed: 25`, `Failed: 0` against the base `Find`
(you have not touched `ClipBudgetSignature.cs` yet). Any new test failing here means its oracle
misreads the base: STOP (do not edit the expected value to fit).

### Step 3: implement the first-byte jump

In `Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs`, replace the `Find` summary and body
(lines 31 to 47) with:

```csharp
    /// <summary>
    /// Start offsets of <paramref name="pattern"/> in <paramref name="haystack"/>, ascending, at most <paramref name="maxHits"/>.
    /// When the first token is a fixed byte, the scan jumps to that byte's next occurrence with <c>Array.IndexOf</c> and
    /// checks the whole pattern only there; a leading wildcard or an empty pattern tests every offset.
    /// </summary>
    internal static List<int> Find(byte[] haystack, int[] pattern, int maxHits)
    {
        var hits = new List<int>();
        var last = haystack.Length - pattern.Length; // the last offset where the whole pattern fits
        var jump = pattern.Length > 0 && pattern[0] != -1;
        for (var i = 0; i <= last; i++)
        {
            if (jump)
            {
                i = Array.IndexOf<byte>(haystack, (byte)pattern[0], i, last - i + 1);
                if (i < 0)
                    break;
            }
            var j = 0;
            while (j < pattern.Length && (pattern[j] == -1 || haystack[i + j] == pattern[j]))
                j++;
            if (j < pattern.Length)
                continue;
            hits.Add(i);
            if (hits.Count >= maxHits)
                break;
        }
        return hits;
    }
```

Why each part holds (re-check each against the code you wrote):

- Inside the loop `i <= last`, so `count = last - i + 1` is at least 1, and
  `i + count = last + 1 = haystack.Length - pattern.Length + 1`, which is at most `haystack.Length`
  because `jump` implies `pattern.Length >= 1`. `Array.IndexOf` therefore never throws, and every
  offset it returns lies in `[i, last]`, where the whole pattern fits.
- The inner loop still starts at `j = 0`, so the first byte is re-checked as an int. A first token
  outside 0 to 255 (which `Parse` can produce from a malformed token such as `100`) casts to some
  byte, finds candidates, and the inner loop rejects them all, exactly as the base did.
- `-1` is the only token that may not jump: it means "any byte", and `(byte)-1` would search for
  `0xFF` only.
- `pattern.Length > 0` keeps an empty pattern from reading `pattern[0]`.
- The `continue` and the `i++` after a hit move to `i + 1`, so overlapping hits stay.

**Verify**: the build exits 0; the "This class" filtered command shows `Passed: 25`, `Failed: 0`.

### Step 4: prove each new test can fail (the two-edit test)

Apply each mutant below to `ClipBudgetSignature.cs` with the Edit tool, run the "This class" filtered
command, record the failing test names, then undo the mutant with the Edit tool (the exact inverse
edit) before the next one. Never use git to undo a mutant: `git restore` and `git checkout --` trigger
a confirm hook that stalls the run.

| Mutant | Edit | Must fail at least |
|---|---|---|
| M1, count too large | `last - i + 1` becomes `haystack.Length - i` | `Find_FirstByteOnlyInTheLastBytes_ReturnsEmpty` (IndexOutOfRangeException), `Find_RandomSmallInputs_MatchesTheBaseLoop` |
| M2, jump on a leading wildcard | `var jump = pattern.Length > 0 && pattern[0] != -1;` becomes `var jump = pattern.Length > 0;` | `Find_LeadingWildcard_MatchesAnyFirstByte`, `Find_RandomSmallInputs_MatchesTheBaseLoop` |
| M3, skip past a hit | after `hits.Add(i);` add the line `i += Math.Max(pattern.Length, 1) - 1;` | `Find_OverlappingMatches_ReturnsEachStart`, `Find_RandomSmallInputs_MatchesTheBaseLoop` |
| M4, count one short | `last - i + 1` becomes `last - i` | `Find_MatchAtVeryEnd_IsFound`, `Find_HaystackSameLengthAsPattern_MatchesAtZero`, `Find_RandomSmallInputs_MatchesTheBaseLoop` |
| M5, no empty-pattern guard | `var jump = pattern.Length > 0 && pattern[0] != -1;` becomes `var jump = pattern[0] != -1;` | `Find_EmptyPattern_MatchesEveryOffsetUpToMaxHits` (IndexOutOfRangeException), `Find_RandomSmallInputs_MatchesTheBaseLoop` |

(The writer simulated these mutants' logic in Python, not the C#; your run is the evidence.) Five new
tests have no row of their own; they guard defects this change does not tempt, and running the table
does not have to fail them: `Find_CandidateFailsAfterTheFirstByte_KeepsSearching` (fails if the
`continue` becomes `break`; the simulation also saw it fail under M4),
`Find_MaxHitsOne_StopsAtTheFirstHit` and `Find_MaxHitsZero_ReturnsTheFirstHit` (fail if the `maxHits`
check moves before `hits.Add`), `Find_EmptyHaystack_ReturnsEmpty` (fails if the loop bound stops
guarding a negative `last`), and `Find_RealPatternsInRandomCode_MatchesTheBaseLoop` (pins every shipped
pattern; record whether any mutant fails it, but none has to). Record every failing name for each
mutant, not only the required ones.

**Verify**: for each mutant, the filtered run shows `Failed:` at least the number of names in its row
and every name in its row among the failures; after the last undo, the filtered run shows
`Passed: 25`, `Failed: 0`, and `git diff --stat` shows only the two in-scope files.

### Step 5: full verification

1. Build: exit 0.
2. Full test command: the Step 1 totals plus 11 passed, the same three failing names, the same skipped
   count.
3. "Callers' tests": the passed count Step 1 recorded, 0 failed.
4. "Installed DLL": the passed and skipped counts Step 1 recorded, 0 failed. Record the two durations
   again beside the Step 1 values (information only, no threshold: a slower or equal time is not a
   STOP, report it).
5. `git diff -U0 c6a82aa04 -- Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs | grep '^[+-]' | grep -v '^+++\|^---'`
   shows changed lines only inside the `Find` summary and body (no change to `Parse`, `RipTarget`,
   `Resolve`, `SignatureMatch`, `Pattern` or the constants).
6. `file` on both in-scope files still prints `with CRLF line terminators`.

**Verify**: all six hold.

### Step 6: commit

Stage `Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs` and
`TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureTests.cs` by name, write the message
(Git workflow above) to a file, and `git commit -F "<file>"`.

**Verify**: `git log -1 --format=%s` prints the subject; `git show --stat HEAD` lists exactly the two
files; `git status --porcelain` lists nothing from this plan (untracked run files under `plans/` that
were there before you started may remain).

## Test plan

- New tests, all in `TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureTests.cs` (11):
  leading wildcard; first byte only where the pattern no longer fits (the count bound); overlapping
  hits; a candidate that fails after its first byte; `maxHits` 1 and 0; empty haystack (fixed and
  wildcard first token); empty pattern; haystack the same length as the pattern; a 20,000-case seeded
  differential against the base loop on small inputs; a differential on every shipped pattern planted
  at the start, middle and very end of 64 KiB of random bytes.
- Existing tests that must stay green unchanged: the 14 in `ClipBudgetSignatureTests`,
  `SkeletonBufferSignatureTests`, `EngineSignatureCacheTests`, and the `LiveInstall` classes
  `ClipBudgetSignatureInstalledBinaryTests` and `SkeletonBufferInstalledBinaryTests`, which scan the
  real installed DLL and pin the verified RVAs of the known v1.5.7 build.
- Pattern to model: `TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs:51-69` (seeded
  differential against a brute-force oracle).
- Not testable here: the launch-time saving in game, and a scan of the in-memory `.text` (which can
  differ from the file where another module patched it). Both go in the `Not-tested:` trailer; the
  differential tests make the in-memory scan's result equal to the old one's for any bytes.

## Done criteria

ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] The full test command shows the Step 1 passed count plus 11, the same three failing language-file
      tests and nothing else failing, and the same skipped count.
- [ ] The "This class" filtered command shows `Passed: 25`, `Failed: 0`.
- [ ] Step 4's report lists, for each of M1 to M5, the failing test names, each row's required names
      among them.
- [ ] "Callers' tests" and "Installed DLL" match their Step 1 counts with 0 failed.
- [ ] `git grep -n "Array.IndexOf<byte>" -- Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs`
      prints exactly one line.
- [ ] `git grep -n "private static List<int> BaseFind" -- TAOM.Tests` prints exactly one line, in
      `ClipBudgetSignatureTests.cs`.
- [ ] `git show --stat HEAD` lists only the two in-scope files.
- [ ] Every comment and test oracle this plan supplied was re-checked against the code it describes
      (the summary's claims against the new body, `BaseFind` against `git show c6a82aa04:...`).

## STOP conditions

Stop and report (do not improvise) if:

- The `Find` body in `ClipBudgetSignature.cs` does not match the "Current state" excerpt, or
  `SignatureScanSession.FindPlain` no longer calls `ClipBudgetSignature.Find`.
- Any new test fails against the base code in Step 2 (its oracle is wrong; do not change it to fit).
- A mutant in Step 4 leaves a required test passing: the test does not guard what this plan says.
- Any existing test, in particular a `SkeletonBufferSignatureTests`, `EngineSignatureCacheTests` or
  `InstalledBinaryTests` case, changes result after Step 3. A changed offset there is the crash risk
  this plan exists to avoid.
- `Array.IndexOf<byte>` throws in any run (the count bound is wrong).
- The change seems to need a file outside Scope, a pattern edit, or a cache format change.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: #784 exists (Performance milestone). Close it after review and merge; no in-game label is
  needed unless the reviewer asks for a cache-miss launch check.
- `/deep-review` before merge, risk tier 1 (native signature resolution: the offset feeds a native
  patch install).
- `/localize`: none, no player-facing text.
- Feature doc: none new. `docs/features/mission-perf-heartbeat.md:946` describes the file as "parse,
  scan, rip targets, resolve" and `docs/features/skeleton-buffer-guard.md:171,187` say the search is
  `ClipBudgetSignature`'s; both stay true. No feature-map row changes.

## After merge: the maintainer's actions

None. The next launch after a game update (or with the cache file removed) runs the faster scan; the
`scanMs=` field of the `guard ON` and `[AnimMem] armed` log lines with `scan=scanned` shows the time.

## Maintenance notes

- Every future pattern gets the jump automatically when its first token is a fixed byte. A pattern
  that starts with a common byte (`48`, the REX.W prefix, starts two of today's six skeleton patterns)
  gains less than one that starts with a rare byte; reordering a pattern to start on a rarer byte would
  change its cache key and is a separate decision.
- The review should probe: the `count` bound at `i == last`; the empty-pattern and leading-wildcard
  paths; that `i` returned by `Array.IndexOf` is absolute, not relative to `startIndex`; that `BaseFind`
  is the verbatim base loop.
- Deferred: a multi-byte or rarest-byte prefilter (more speed, more code; not worth it while the cache
  answers most launches), and timing the in-game scan on a cache-miss launch.
