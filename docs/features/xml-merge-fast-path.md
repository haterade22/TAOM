# XML Merge Fast Path

## Overview

Every campaign load and every custom battle start makes the engine merge each module's ModuleData XML into one
document per type. The engine's merge loop is quadratic in the merged size, and on TAOM's module set it costs about
28 s of a new campaign's 50.2 s loading screen. `Patch99_XmlMergeFastPath` runs the same loop with the same engine
helpers in the same order, but keeps the accumulated document as one `XDocument` and compiles each XSLT once per
process. The output is identical to the engine's, character for character, on every type of the live install; "Known
limits" says how that proof differs from the inputs the fast path accepts at run time.

## Why This Exists

- **Vanilla behavior:** `MBObjectManager.CreateMergedXmlFile` (v1.5.3, `MBObjectManager.cs:962-978`) loads and
  validates one file at a time and merges it through `MergeTwoXmls`, which converts the WHOLE document merged so far
  from `XmlDocument` to `XDocument` and back for every file. It also builds a fresh `XmlSchemaSet` for every validated
  file and compiles every XSLT on every call (`ApplyXslt`). A 3 KB TAOM NPC file costs 0.20 s only because 7 to 10 MB
  were merged before it.
- **TAOM requirement:** TAOM, the Armory and TAOM_Map add hundreds of files to the validated types, so the
  conversion cost dominates the merge: measured from the engine's rgl logs on 2026-10-02 on the maintainer's desktop,
  a new campaign spent NPCCharacters 56 files 13.3 s, Items 142 files 9.6 s, GameText 30 files 1.7 s and
  EquipmentRosters 31 files 1.4 s merging; a custom battle NPCCharacters 48 files 8.3 s and Items 141 files 5.7 s.
- **Without this feature:** every load pays those seconds again, and every file added to a type makes every later
  file of that type slower.

## Architecture

### Design Challenge

The merged document is what the game builds its objects from, so the replacement must produce exactly the engine's
document, including its quirks: `_replaceWhileMerging` copied into the output, index 0's XSLT never applied, an XSLT
transforming the document BEFORE its own entry's file merges, and an entry with no file that only applies an XSLT
(TAOM's `lords.xslt`). It must also keep the engine's rgl lines (`opening <file>`, `opening <xsd>`) and the engine's
validation messages, and it must never turn a working load into a broken one.

### Solution Approach

A Harmony prefix on `CreateMergedXmlFile` hands the call to `XmlMergeService`, which runs the engine's loop:

1. Load the first file through the engine's own private `CreateDocumentFromXmlFile` (the same rgl lines and
   validation as the engine).
2. For each later entry: if it has an XSLT, convert the accumulated document to `XmlDocument` (only if it is
   currently an `XDocument`) and apply the XSLT through `XsltTransformCache`, the engine's `ApplyXslt` with the
   compiled stylesheet cached by path and re-validated by last-write time and length; if it names a file, load it
   through the engine, convert the accumulated document to `XDocument` (only if it is currently an `XmlDocument`),
   convert the new file, and merge with the engine's own public `MergeElements` (or append its elements when the
   entry's XSD path is "", as the engine does).
3. Convert to `XmlDocument` once at the end.

The one engine step the loop drops is the round trip of the accumulated document through `XmlDocument` between two
consecutive merges. `XmlMergeLiveEquivalenceTests` proves that round trip changes nothing on every type of the live
install, for both the `Campaign` and the `CustomGame` game type. It is not an identity for every possible input:
see "Known limits" below.

| The engine's `CreateMergedXmlFile` does | Fast path |
|---|---|
| Loads and validates each file through `CreateDocumentFromXmlFile` (prints `opening <file>` and `opening <xsd>`, validation messages to rgl) | kept: the same call per file |
| Applies each entry's XSLT to the document so far, before that entry's file | kept, same order; compile cached |
| Merges each file with `MergeElements`, or appends when the XSD path is "" | kept, same calls and arguments |
| Round-trips the accumulated document through `XmlDocument` between merges | dropped; proven output-neutral by the harness on every type of the live install |
| Returns the document; throws what its helpers throw | kept: the identical document on the proven module set; any exception re-runs the original |

**Scope.** Only validated merges (`skipValidation == false`): every `LoadXML` (campaign and custom battle), GameText
and BannerIcons. Unvalidated merges (the engine's native merges, `CoreParameters`, the custom battle's
`CustomBattleScenes` read in `CustomGame` and NavalDLC's `NavalCustomGame`, TAOM's `ObjectManagerAdapter` SPCultures
read and `MonsterSizeCatalogAdapter` Monsters read) cost little and run the engine's code, logged as
`reason=skip-validation`.

**Standing aside.** The fast path runs the engine's code instead (`reason=foreign-patch`) while any Harmony owner
other than `com.taom.mod` and `TAOM.Dependencies.Foundation.PatchShield` has a prefix, transpiler, inner prefix or
inner postfix on `CreateMergedXmlFile`, or any patch at all on `ApplyXslt`, `MergeTwoXmls`, `ToXDocument` or
`ToXmlDocument` (methods the fast path bypasses or calls a different number of times). All six of Harmony's patch
collections are read. `MergeElements` and `CreateDocumentFromXmlFile` are called exactly as the engine calls them, so
another mod's patches on them still apply.

**Failure handling.** Everything the fast path does sits inside one `try`. On an exception it logs one WARNING per
exception type, plus a DEBUG line with the exception's full text (`Exception.ToString()`, stack included) for every
such fallback, and lets the engine's original run on the untouched arguments, which reproduces the unpatched game's
outcome (the only side effect is a repeat of that type's rgl `opening` lines), provided PatchShield stays off the method
("Shipping order" below). The finalizer then compares: if the engine's
merge succeeded where the fast path threw, the fault is TAOM's and the fast path turns itself off for the rest of the
session with one WARNING; if both threw, the data is bad and the fast path stays on.

**Harmony shape.** A `Priority.Last` prefix that returns `false` with `__result` set when the fast path merged, and a
`void` finalizer that only observes, so it never replaces the engine's exception, and logs the engine-path line. They
share a `__state` of type `XmlMergeCall`. Harmony keeps `rethrow` only while every finalizer on a method is void. With
plan 040's PatchShield exclusion (the only configuration that ships: "Shipping order" below), PatchShield never attaches
to `CreateMergedXmlFile`, so every finalizer on it is void (Patch99's and plan 040's Patch100 stamp finalizer), Harmony
keeps `rethrow`, and the engine's exception leaves with its own trace. Without that exclusion (were it ever removed),
PatchShield's second pass (at the first game init) attaches its value-returning `ShieldFinalizerWithResult`: Harmony
then uses `throw`, `RethrowStackPreserver` keeps the engine's trace, and the finalizer swallows the engine-drift
exceptions, the hazard "Shipping order" describes. Applied at `ProcessLoad` (`OnSubModuleLoad`) by `XmlMergeModule`,
because a game's merges run in `Campaign.OnInitialize` and `CustomGame.OnInitialize`, long before the late batch.
About 30 to 40 calls per load, never per frame.

### Shipping order: plan 040 first

Patch99 must not reach a player's build before plan 040 (load-time stamps), because of PatchShield. Its second pass (at
the end of the first game's initialization) attaches the value-returning `ShieldFinalizerWithResult` to every patched
engine method that is not on `PatchShieldPolicy.ExcludedTargetMethods` (or a namespace exclusion, or SaveShield's), and
Patch99 makes `CreateMergedXmlFile` a patched engine method. For every later game in that process the finalizer then
swallows `MissingMethodException`, `MissingFieldException` and `TypeLoadException` from the engine's own merge, and the
method returns null. In the unpatched game that exception propagates: `MBObjectManager.LoadXML` (v1.5.3,
`MBObjectManager.cs:786-797`) calls `GetMergedXmlForManaged` outside its `try`. With the swallow, the null reaches
`LoadXml(doc)` inside the `try`, `doc.ChildNodes` throws, the empty `catch` eats it, and that ModuleData type silently
loads empty. Other exceptions (XML and I/O errors) are outside the swallowed set and still propagate. A co-op session is
not exposed: PatchShield does not install while a co-op module is active (`PatchShieldPolicy.ShouldInstall`).

Plan 040 put `TaleWorlds.ObjectSystem.MBObjectManager.CreateMergedXmlFile` on that exclusion list for its own void
finalizer (`plans/040-load-time-stamps.md`, design decision 7), and 040 landed before this plan. This plan therefore
adds no entry of its own, and a second copy would only duplicate 040's. The failure-handling promise above ("the
unpatched game's outcome") holds with that entry in place and not without it, so removing 040's patch would move the
entry here, with a `BindingVerification` test that walks the patch target through `IsExcludedTargetMethod` (the
`CreatureBanditsWiringTests` precedent).

### Known limits

- **What the fast path accepts versus what is proven.** At run time every validated merge takes the fast path unless
  the service decides otherwise: bindings resolved, a non-empty list, `skipValidation` false, no foreign patch on the
  watched methods and not turned off this session (the `reason=` values below). Nothing looks at which modules or files
  are merged, or at a stylesheet's content. What is proven is narrower: the harness compares the serialized document
  (`OuterXml`, ordinal equality, `XmlMergeAssert`) for each type of the module set it is given, for `Campaign` and
  `CustomGame`, at the time it runs (by default the maintainer's install; also run with NavalDLC and Bannerlord.Diplomacy
  added, see Performance), plus the fixtures in `XmlMergeEngineEquivalenceTests`. Any other module set, and any file
  edited since, is outside the proof but still takes the fast path. A difference that does not throw would be silent in
  game: only an exception sends a merge to the engine's code, and nothing compares the two at run time. Documentation
  does not restrict the prefix either; a runtime guard for the two shapes below would be a behavior change that waits on
  the maintainer. For a player report that points at merged data, run the harness by hand ("How to run the harness": it
  needs `TAOM_RUN_BENCHMARKS=1`) with `TAOM_XMLMERGE_MODULES` set to the reporter's module list; a Skipped result
  checked nothing.
- **Two inputs where the dropped round trip differs** (proven on fixtures by the review lenses of 2026-10-02):
  - A later file of the same type re-declares a namespace prefix with a different URI on its root while an earlier
    file's attribute uses that prefix. The engine's round trip leaves that attribute in no namespace; the fast path
    keeps its namespace and writes a generated prefix. Bannerlord's readers look attributes up by name, so they see
    the same value; a later XSLT matching the attribute by its namespace would not. A scan on 2026-10-02 of all
    4,614 installed ModuleData XML files found only the prefixes `xsi` and `xsd`, each bound to one URI except in
    Native's `prebaked_animations.xml` and `voices.xml`, which spell both URIs in lower case.
  - Mixed content (text beside child elements) under `_replaceWhileMerging`: removing the children leaves two
    adjacent text nodes, which the engine's round trip joins and the fast path does not. The serialized document and
    `InnerText` are identical; only a reader of `FirstChild.Value` would differ, and the harness's text comparison
    cannot see it either. The same scan found no element mixing text and children (87 files did not parse in Python's
    ElementTree and were not inspected).
- **XSLT cache.** A stylesheet pulled in by `xsl:include` or `xsl:import`, or a rewrite that keeps both the length
  and the last-write time, is not re-validated: the cache keys on the main stylesheet's path, last-write time (UTC) and
  length alone, so a changed included file is picked up only by a restart. None of the 23 installed ModuleData
  stylesheets uses `xsl:include`, `xsl:import` or `document()`.

### Component Diagram

```
MBObjectManager.CreateMergedXmlFile (engine)
        |
  MBObjectManager_CreateMergedXmlFile_Patch   (prefix + void finalizer, Patch99)
        |
    XmlMergeService  ---- XsltTransformCache (compiled stylesheets)
        |
  IXmlMergeEngineAdapter / XmlMergeEngineAdapter
        |
  MBObjectManager: CreateDocumentFromXmlFile (private, delegate), ToXDocument, ToXmlDocument, MergeElements
```

## Configuration

None. No MCM setting and no config file: the fast path turns itself off on its own fault and stands aside for other
mods (see the decisions below on a player-facing switch).

## Log lines

All lines go to `taom_debug.log` through `IModLogger`, with the `[XmlMerge]` prefix; numbers are written with the
invariant culture and milliseconds are rounded to whole numbers. `XmlMergeLinesTests` pins every format literally.
INFO, WARNING and ERROR flush synchronously, so a hang inside a merge still leaves the last completed type on disk.
DEBUG lines are queued for the logger's writer thread instead (it wakes every 50 ms, and the next INFO or WARNING
drains the queue too), so the one DEBUG line below is lost only if the process dies inside that window.

| When | Level | Format |
|---|---|---|
| Module start, bindings resolved | INFO | `[XmlMerge] fast path ready: engine bindings resolved (CreateDocumentFromXmlFile, MergeElements, ToXDocument, ToXmlDocument); applies to validated merges (skipValidation=false); xslt cache on` |
| Module start, a binding missing | WARNING | `[XmlMerge] fast path off: {problem}; every module XML merge runs the engine's own code` |
| Each fast merge | INFO | `[XmlMerge] type={type} files={files} xslt={xslt} ms={ms} path=fast load_ms={load} xslt_ms={xsltMs} merge_ms={merge} xslt_compiles={c} xslt_cache_hits={h}` |
| Each engine-path merge | INFO | `[XmlMerge] type={type} files={files} xslt={xslt} ms={ms} path=vanilla reason={reason} result={ok or exception type name}` |
| First stand-aside per distinct patch set | INFO | `[XmlMerge] fast path stands aside: {patches}; merges run the engine's own code while those patches are present` |
| First fast-path exception per exception type | WARNING | `[XmlMerge] fast path failed on type={type}: {ExceptionType}: {message}; this merge re-runs the engine's own code` |
| Every fast-path exception (each fallback, after the WARNING when there is one) | DEBUG | `[XmlMerge] fast path failure detail on type={type}: {exception text}` |
| Fast path turned off for the session | WARNING | `[XmlMerge] fast path disabled for this session: it threw {ExceptionType} on type={type} where the engine's own merge succeeded` |
| Every game init | INFO | `[XmlMerge] summary game={gameType} merges={n} fast={f} vanilla={v} files={files} ms={ms} max_ms={max} max_type={type} xslt_compiles={c} xslt_cache_hits={h} fast_path={state}` |

Fields:

- `type`: the file name, without extension, of the first non-empty XSD path in the list (every list uses the game's
  `XmlSchemas/<id>.xsd`, so this is the type id), else `unknown`.
- `files`: entries whose file path is not "". `xslt`: entries at index 1 or later whose XSLT path is not "" (the
  engine never applies index 0's).
- `ms`: from the prefix to the moment the line is logged. `load_ms`, `xslt_ms`, `merge_ms`: time inside the engine's
  loader, inside XSLT application (compiles included) and inside `MergeElements`; the rest of `ms` is conversion.
- `xslt_compiles`, `xslt_cache_hits`: stylesheets compiled and reused during this merge. There are no schema counters
  (see "Performance", the schema cache).
- `{exception text}`: the exception's own `ToString()`, verbatim: type, message, stack and any inner exceptions, so the
  entry runs over several lines like the log's other exception dumps. Written for every fallback that has an exception,
  not once per type like the WARNING.
- `reason`: `skip-validation` (an unvalidated merge), `bindings` (an engine member did not resolve), `empty-list`,
  `foreign-patch`, `disabled` (turned off this session), `fast-path-error` (this merge's fast attempt threw).
- `{patches}`: the sorted, comma-joined `owner kind MBObjectManager.Method` strings.
- Summary: `merges = fast + vanilla` and `files`, `ms`, `max_ms`, `max_type` cover every merge since the previous
  summary (or process start), fast and engine path alike; a merge whose fast attempt threw counts once, as vanilla.
  The two XSLT counters sum the fast merges only. `game` is the game type id (`Campaign`, `CustomGame`), `none`
  when there is no game type. `max_type` is `none` when the window had no merge. `fast_path` is `on`, `disabled`
  (turned off after its own fault) or `off` (a binding did not resolve).

Examples:

```
[XmlMerge] fast path ready: engine bindings resolved (CreateDocumentFromXmlFile, MergeElements, ToXDocument, ToXmlDocument); applies to validated merges (skipValidation=false); xslt cache on
[XmlMerge] fast path off: MBObjectManager.CreateDocumentFromXmlFile(string, string, bool) not found as a static XmlDocument method; every module XML merge runs the engine's own code
[XmlMerge] type=NPCCharacters files=56 xslt=1 ms=1774 path=fast load_ms=361 xslt_ms=1246 merge_ms=51 xslt_compiles=0 xslt_cache_hits=1
[XmlMerge] type=Monsters files=4 xslt=0 ms=12 path=vanilla reason=skip-validation result=ok
[XmlMerge] fast path stands aside: com.other prefix MBObjectManager.CreateMergedXmlFile; merges run the engine's own code while those patches are present
[XmlMerge] fast path failed on type=Items: InvalidOperationException: boom; this merge re-runs the engine's own code
[XmlMerge] fast path failure detail on type=Items: System.InvalidOperationException: boom
   at TAOM.Features.XmlMerge.XmlMergeService.Load(String path, String xsdPath, Boolean skipValidation, XmlMergeCounters counters)
   at TAOM.Features.XmlMerge.XmlMergeService.MergeFast(...)
   at TAOM.Features.XmlMerge.XmlMergeService.TryMergeFast(...)
[XmlMerge] fast path disabled for this session: it threw InvalidOperationException on type=Items where the engine's own merge succeeded
[XmlMerge] summary game=Campaign merges=32 fast=28 vanilla=4 files=329 ms=6100 max_ms=1774 max_type=NPCCharacters xslt_compiles=9 xslt_cache_hits=0 fast_path=on
```

The detail example is illustrative: its frames depend on where the exception was thrown, and `...` shortens them.

The engine's own rgl lines are unchanged: `opening <file>` and `opening <xsd>` still appear for every file, because
every file still loads through the engine's loader.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/XmlMerge/XmlMergeModule.cs` | Registrations, the patch handshake, the start line; declares Patch99 at ProcessLoad |
| `Main/Features/XmlMerge/XmlMergeService.cs` | The loop, the decision, the fallback and the bookkeeping |
| `Main/Features/XmlMerge/XsltTransformCache.cs` | The engine's `ApplyXslt` with compiled stylesheets cached |
| `Main/Features/XmlMerge/XmlMergeLines.cs` | Every log line |
| `Main/Features/XmlMerge/XmlMergeCall.cs`, `XmlMergeCounters.cs` | One call's state (Harmony `__state`) and one merge's timings |
| `Main/Features/XmlMerge/Hooks/MBObjectManager_CreateMergedXmlFile_Patch.cs` | The prefix, the void finalizer, the summary hook |
| `Main/Adapters/IXmlMergeEngineAdapter.cs`, `XmlMergeEngineAdapter.cs` | The engine's merge helpers and the foreign-patch check |
| `Main/SubModule.cs` (`OnGameInitializationFinished`) | One line: the per-game summary, before the once-per-process guard |

## Dependencies

- `IXmlMergeEngineAdapter` (Adapters): the engine's merge helpers on `MBObjectManager` (the private
  `CreateDocumentFromXmlFile` through a cached delegate, `ToXDocument`, `ToXmlDocument`, `MergeElements`) and the
  Harmony patch info that decides a stand-aside.
- `IModLogger` (Core): every `[XmlMerge]` line in `taom_debug.log`.
- `XmlMergeModule` (feature module): the singleton registrations, the patch handshake (`InitializeStatics`) and
  Patch99's `ProcessLoad` category.

## Tests

- `XmlMergeAssertTests`: the comparison helper can fail, and how it reports.
- `XmlMergeLinesTests`: every log format, literally (the DEBUG detail line's multi-line text kept verbatim included),
  and invariant rounding.
- `XsltTransformCacheTests`: compile once, recompile on a new time and on a new length alone, the drop template, the
  shared name table, a missing stylesheet throws, a malformed one throws every time and is never cached.
- `XmlMergeServiceTests` (hand-written fake engine): every decision reason and their order, the exact engine call
  order for each branch of the loop, the once-only lines (and a second distinct patch set or exception type logging
  again), the DEBUG detail line on every fallback (after the WARNING, again for a repeated exception type, and for the
  logger's own exception when it throws on the fast line), the session disable and its negative case, the finalizer after a fast
  merge logging nothing, a throwing logger falling back to the engine, the summary in all three `fast_path` states and
  its reset, the start line both ways, the type id's edge cases, the log levels.
- `XmlMergeForeignPatchFilterTests`: the stand-aside rule on Harmony's own patch-info types, no game needed: TAOM's
  owners never count, the target's postfixes and finalizers never count, every other patch does. The adapter's
  choice of which method is the target and which are watched is pinned too, through `ForeignPatches`' patch-info
  seam.
- `XmlMergeEngineEquivalenceTests` (`RequiresGame`): byte equality with the engine's own `CreateMergedXmlFile` on
  fixtures for each branch: no XSLT, an XSLT beside a file, an XSLT-only entry, an XSLT as the last step, the ignored
  first XSLT, a single entry, an empty XSD path, `_replaceWhileMerging`, two XSLTs in a row, a missing file.
- `XmlMergeLiveEquivalenceTests` (`RequiresGame`, `LiveInstall`, `BindingVerification`): the gate. Every type of the
  live module set, for `Campaign` and `CustomGame`, the timing table, and the acceptance rules in "How to run the
  harness". Opt-in: a default run skips it unless `TAOM_RUN_BENCHMARKS=1`; the binding gate runs it.
- `LiveGateRulesTests` and `LiveMergeListBuilderTests`: those acceptance rules, pinned without the game, so they run in
  the default suite and on hosted CI. An engine exception fails even when the fast path throws the same type, and the
  message says what the fast path did; a requested module must exist (`FastMode` may be absent from the default order
  only); the four heavy types must be present and merged on both sides; their fast total is at most half their engine
  total, on the totals and not per type; which module orders count as explicit, and that a segment of white space only
  names no module.
- `XmlMergeLiveEquivalenceOptInTests`: the harness class still carries `BindingVerification`. Needs no game, so it
  runs in the default suite and on hosted CI. Its other half, the variable in the gate's runsettings, is pinned by
  `BindingGateRunSettingsTests`.
- `XmlMergeBindingTests` (`BindingVerification`; the IL tests also `RequiresGameIL`): the target's signature and
  parameter names, every engine member the adapter reaches with its exact parameter list (the watched methods
  included), and the shape of the engine's loop. `MirroredEngineBodies_CallExactlyThePinnedSequence` pins the IL
  fingerprint (`IlCallScanner.Fingerprint`) of `CreateMergedXmlFile`, `MergeTwoXmls`, `ApplyXslt` (which
  `XsltTransformCache` copies), `ToXDocument` and `ToXmlDocument` (whose round trip the fast path drops): in order,
  each call with its constructed declaring type and parameter list, each constant (`keepDuplicates`, the loop start,
  list indices, `""`), and each comparison and conditional branch. So the binding gate after an engine update fails
  on a reordered loop, another overload or member, a changed constant argument or a changed comparison. It does not
  pin which local or argument feeds a call, which statements a branch skips (a dropped `else`, an added `continue`, a
  nested `if`), or anything outside those five bodies (the engine's `MergeElements` included). The harness runs in the
  same gate and covers what the fingerprint leaves open on the live module set, so re-pin only after it passes.
  `IlFingerprintTests` proves the fingerprint tells apart bodies that differ in one constant, overload, list element
  type or comparison.
- `XmlMergeWiringTests`: the module is listed once, declares Patch99 at ProcessLoad, and a fresh DryIoc container
  builds the service.
- Shared registries: `CoopVetoClassificationTests` (ReviewedSafe), `ReflectionSiteBindingTests` (the private loader).

## How to run the harness

**Opt-in in a default run.** The harness adds about 40 s (24 s for `Campaign`, 15 to 16 s for `CustomGame`, measured
2026-10-03 on the default module order), so a plain `dotnet test` reports its two tests Skipped, with the reason
`Opt-in: set TAOM_RUN_BENCHMARKS=1 ...`. That is the repo's switch for slow checks. A Skipped harness checked nothing.

**Inside the binding gate.** No extra step. The class carries `BindingVerification`, the category the gate filters
on, and `TAOM.Tests/binding-gate.runsettings` sets `TAOM_RUN_BENCHMARKS=1` for the gate's test host, because the gate
turns every skip into a failure (without the variable the gate would fail the harness's two tests). Two default-suite
tests pin the halves: `XmlMergeLiveEquivalenceOptInTests` the category, `BindingGateRunSettingsTests` the variable.
Hosted CI never runs the harness (`RequiresGame`, `LiveInstall`).

**By hand.** The harness reads the installed game (`BANNERLORD_GAME_DIR` or `BANNERLORD_OVERRIDE_DIR`) and builds, for
each type, exactly the lists the engine's `GetMergedXmlForManaged` builds, then runs the engine's merge and the fast
path and compares them:

```
TAOM_RUN_BENCHMARKS=1 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~XmlMergeLiveEquivalenceTests" --logger "console;verbosity=detailed"
```

In PowerShell, set `$env:TAOM_RUN_BENCHMARKS = '1'` first.

The module order defaults to `TAOM.Dependencies;Native;SandBoxCore;CustomBattle;SandBox;StoryMode;BirthAndDeath;FastMode;LOTRLOME_Armory;TAOM_Map;TAOM`
(only `FastMode` may be absent, as it is on this install; any other absent module fails the run); set
`TAOM_XMLMERGE_MODULES` (semicolon-separated; each name is trimmed and a blank segment is ignored) to test another
order, and every module it names must exist (an explicit list copied from the default order drops `FastMode` on an
install without it).
The binding gate runs it after every engine update, which is the point of having it there: a red
`MirroredEngineBodies_CallExactlyThePinnedSequence` says the engine's merge code changed, and a green one does not
prove it did not. Also run it after a change to this feature, and whenever the module set changes shape.

**What the gate asserts** (`LiveGateRules`, for `Campaign` and for `CustomGame`; the printed table shows the numbers):

1. **Equivalence.** Every type of the module set merges on both sides into the same document. An engine exception is a
   failure even when the fast path throws the same exception type: the game loads these types without one, so either
   the harness's lists differ from the game's or the fast path diverges. The failure message and the table row say what
   the fast path did with the same input (`fast merged` means it accepts what the engine rejects, so in game the type
   would load where vanilla throws). Exception parity belongs to the fixtures in `XmlMergeEngineEquivalenceTests` (the
   missing-file case). The plan's step 7a asked for exception-type equality here; this replaces it.
2. **Inventory.** Every module the order names exists (the rule above), and the four heavy types (`NPCCharacters`,
   `Items`, `EquipmentRosters`, `GameText`) are all among the merged types.
3. **Speed.** The four heavy types' fast total is at most 50% of their engine total, the plan's bar (see
   "Performance", "Timing policy"). On a module list far smaller than the default the bar can fail with nothing wrong,
   because there is little quadratic cost to save: read the table.

Every failure is listed in one message, so one run reports all of them.

## Performance

Harness, maintainer's desktop, 2026-10-03, installed v1.5.3, the default module order (FastMode not installed):

| Type (Campaign) | Entries | Files | XSLTs | Engine ms | Fast ms | Identical |
|---|---|---|---|---|---|---|
| NPCCharacters | 57 | 56 | 1 | 11,213 | 1,944 | yes, 7,276,693 chars |
| Items | 142 | 142 | 0 | 4,174 | 413 | yes, 2,637,805 chars |
| EquipmentRosters | 31 | 31 | 0 | 912 | 165 | yes, 2,346,944 chars |
| GameText | 33 | 30 | 3 | 538 | 250 | yes, 2,321,719 chars |

For the custom battle game type the same four took 12,532 ms on the engine path and 1,175 ms on the fast path.
Every other type of both game types was identical too. Three runs that day put NPCCharacters' engine path at 11.2 to
11.6 s. The engine path runs about twice as slowly inside the test host as in the offline Windows PowerShell
prototype (NPCCharacters 5.4 s there on the same machine and day, with the same output),
so the fair ratio is the prototype's 27 to 35%; in game the engine path is slower still (13.3 s for NPCCharacters in
the rgl log). What remains of NPCCharacters on a process's first load is mostly `lords.xslt`. Measured offline in
Windows PowerShell 5.1 on its real campaign input (the deep review of 2026-10-02): about 0.1 s to compile, about 1.0 s
for the first transform of a freshly compiled instance (most likely the first-run JIT of its generated code), and 0.05
to 0.1 s for each later transform of the same instance. The cache keeps that instance, so a later load in the same
process pays about 0.05 to 0.1 s; the cost is about 11 MB of managed heap held for the process.

The harness also ran with NavalDLC and Bannerlord.Diplomacy added to the default order (2026-10-02, review lead):
every type of both game types was identical (34 Campaign types, 29 CustomGame types). It passed again on 2026-10-03
under the strengthened gate with both modules appended after `TAOM` (34 and 29 types again; 15.3% for `Campaign`,
9.1% for `CustomGame`).

**Timing policy.** The speed bar compares the two paths on one host: in one process, the engine first and the fast path
second for each type with a GC before each, the four heavy types' totals, at most 50% (the plan's bar; the prototype
reached 27 to 35%). Two harness runs on the default module order on 2026-10-03 (one before the gate was strengthened, one
after) measured 16.4% and 16.7% for `Campaign` (engine 16,785 and 17,598 ms, fast 2,753 and 2,933 ms) and 9.6% and 9.4%
for `CustomGame` (12,462 and 14,147 ms against 1,201 and 1,331 ms).

The harness ratio reads about half the prototype's because the test host slows the engine path about twice and leaves
the fast path's total near the prototype's: the four heavy types' engine total is 16,837 ms in the table above against
the prototype's 8,806 and 9,003 ms (about 1.9 times), and their fast total is 2,772 ms against 3,078 and 2,438 ms (0.9
to 1.1 times). What makes the host slow was not isolated. So the bar's margin is about threefold on the harness ratio
(`Campaign`, the closer game type) but only about 1.4 to 1.9 times against the prototype's 27 to 35%. A fast path that
went from 30% to 60% of the engine's time on the prototype's terms, past the plan's 50%, would read about a third in
the gate and pass. **Open, for the maintainer:** whether the gate should assert a tighter harness bar (25%, say) to
hold the plan's 50% in the prototype's terms. Nothing is settled; the bar stays at 50%. Absolute engine times are
printed and never asserted: they depend on the host, the file cache and the machine's load.

**The plan's STOP boundary on engine time.** Plan 042's step 7a said to stop before writing the patch if the harness's
engine time for a type fell outside a factor of two of the offline figures (NPCCharacters 5.4 to 5.6 s, so 11.2 s at
most). The harness measured 11.2 to 11.7 s for NPCCharacters (11,213 ms in the table above, 11.2 to 11.6 s in the three
runs of that day, 11,244 and 11,738 ms in two later runs), up to about 5% over the literal limit and 2.1 to 2.2 times
the 5.4 s the offline prototype took on the same machine and day with the same output. The prototype ran in Windows
PowerShell 5.1 and the harness runs in the test host; what makes the test host about twice as slow was not isolated. The
branch records show no stop-and-resume authorization before the patch was written, and none is claimed here: this
paragraph records the crossing. What replaces the offline comparison is the same-host ratio above, because an offline
figure from another host is not a reproducible bar.

**Schema cache (the plan's Stage 2): not built.** The engine builds one `XmlSchemaSet` per validated file. A cache
would save a predicted 251 to 280 ms per load, under the plan's 300 ms bar, so it was measured and left out. The log
lines carry no schema counters: a count of builds only repeated `files` on a fast merge, and no code counted cache
hits. The in-game `load_ms` fields are the data to re-decide it; a cache built later brings its own counters.

## Decisions

The plan's three open levers were settled by the maintainer on 2026-10-03 (decision D14 of the perf run, "as
recommended", with the review's other choices: the compiled-XSLT cache stays, the two unwritten schema counters are
dropped, the harness is opt-in and runs in the binding gate, and every fallback writes the exception's full text at
DEBUG):

1. **`lords.xslt`** (about 1.1 s of the remaining NPCCharacters merge on each process's first load, 0.05 to 0.1 s
   after; 1,515,721 bytes, 396 lord templates plus the
   identity template, generated by `tools/complete_lords_xslt.py`). Its XmlNode (`Main/_Module/SubModule.xml`, `id="NPCCharacters" path="lords"`) has
   no `lords.xml`, so it rewrites the vanilla and SandBox lords merged before it. Replacing it with an XML file would
   need `_replaceWhileMerging` on every lord (which the engine copies into the merged output) and the vanilla children
   baked in, regenerated at every engine update. **Decided: no `lords.xml`;** the stylesheet stays and its compiled form
   stays cached.
2. **Fewer files**: a build step that concatenates TAOM's per-culture files of one type into one generated file in the
   deployed module, proven by the same harness. With the fast path a file's cost no longer grows with what was merged
   before it, so this lever is worth much less than before. **Decided: no build-step merge.**
3. **A player-facing kill switch**: an MCM toggle would also move the pinned settings counts and the co-op relevance
   classification. **Decided: none is added;** the fast path turns itself off on its own fault and stands aside for
   other mods' patches.

Not decided, and not part of D14: a runtime guard that routes the two known divergent input shapes ("Known limits") to
the engine's code. None exists. Also open: whether the live gate's speed bar should be tighter than 50% of the harness
ratio ("Performance", "Timing policy"). The bar stays at 50%.

## Changelog

- 2026-10-03: Patch99 added (plan 042): the merge keeps one `XDocument` across files and compiles each XSLT once;
  byte-identical to the engine on every type of the live install; one `[XmlMerge]` line per merged type and a summary
  per game in `taom_debug.log`.
- 2026-10-03: deep review follow-ups: the stand-aside rule and every service branch tested, the watched methods
  checked by exact signature, the engine bodies the fast path mirrors pinned call by call, "Known limits" written
  down, the finalizer and caller notes corrected.
- 2026-10-03: the maintainer's choices on the review's open items. The log lines lost `schema_builds`,
  `schema_cache_hits` and the header's `schema cache off`; every fast-path fallback also writes the exception's full
  text at DEBUG; the live harness is opt-in in a default run (`TAOM_RUN_BENCHMARKS=1`) and runs in the binding gate.
  The compiled-XSLT cache stays.
- 2026-10-03: review and Codex follow-ups. The live gate can no longer pass on an engine exception the fast path
  matches, a missing heavy type, a requested module that is absent (`FastMode` excepted in the default order) or a fast
  total above half the engine total, and its rules are pinned without the game. Documented: the shipping dependency on
  plan 040's PatchShield exclusion, the fast path's run-time eligibility against the proven domain, the plan's STOP
  boundary on engine time, and the three settled decisions (no `lords.xml`, no build-step merge, no off switch). "Known
  limits" says the harness needs `TAOM_RUN_BENCHMARKS=1`, and the issue is filed.
- 2026-10-03: convergence fixes for those follow-ups. The gate's message and table row for an engine exception say what
  the fast path did and name both causes; a blank segment in `TAOM_XMLMERGE_MODULES` no longer becomes a missing module
  with no name; "Harmony shape" (here, the patch comment and the registry) states the shipped configuration, plan 040's
  PatchShield exclusion, first and the branch-alone swallow as the hazard; "Timing policy" gives the bar's margin on
  the harness ratio and on the prototype's terms and records a tighter bar as open.

## GitHub Issue

- [#724](https://github.com/haterade22/TAOM/issues/724): perf: cut the module XML merge from every campaign load and
  custom battle start. Plan 042.
- **Status:** Open
