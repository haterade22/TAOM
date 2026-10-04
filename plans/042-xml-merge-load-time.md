# Plan 042: Merge module XML in one document per type, byte-identical to the engine, so every load drops most of its 15 to 28 s merge

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. This plan's work
> lands on its own branch: never commit it onto a branch that carries another plan's work. The
> orchestrator keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat 0912e1b7..HEAD -- Main/SubModule.cs Main/Composition/FeatureModules.cs Main/Features/XmlMerge Main/Adapters/IXmlMergeEngineAdapter.cs Main/Adapters/XmlMergeEngineAdapter.cs TAOM.Tests/Features/XmlMerge TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs docs/features/xml-merge-fast-path.md docs/reference/feature-map.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/reflection-sites.md docs/reference/engine/object-system-mbobjectmanager.md`
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition. Two kinds of change are expected and are not a
> mismatch: other plans' edits to `Main/SubModule.cs` outside the `OnGameInitializationFinished`
> excerpt below, and other plans appending lines, rows or sections to `FeatureModules.All`, the
> `CoopVetoClassificationTests` registry, `ReflectionSiteBindingTests.cs`, `reflection-sites.md`,
> `harmony-patch-registry.md` or `feature-map.md` (plan 040 appends to the last four). Re-find the
> anchors by their text, not their line numbers.
>
> Also run, before anything else:
> `cat .claude/pinned-game-version.txt` (must print `v1.5.3`), and
> `ls -l "$BANNERLORD_GAME_DIR/bin/Win64_Shipping_Client/TaleWorlds.ObjectSystem.dll"` (size must be
> `61792`). `Directory.Build.props` takes the game folder from `BANNERLORD_OVERRIDE_DIR` or
> `BANNERLORD_GAME_DIR`; if neither is set, the build cannot reference the game either: report it as
> an environment gap (`.claude/rules/environment-failures.md`), do not fix it. A different size is a
> STOP: every engine excerpt below was read from that binary.

## Status

- **Priority**: P1
- **Effort**: L (a feature module with one Harmony prefix and finalizer, one adapter, a service, two
  pure caches, an offline harness that runs the engine's own merge on the live install, fixture
  equivalence tests, binding tests, docs; two commits, the second conditional)
- **Risk**: MED (the patch replaces the engine's merge loop at every campaign load and custom battle
  start for every player. Mitigations, all in this plan: the replacement calls the engine's own
  file loader, element merger and document converters; it is gated by byte-identical output on every
  type of the live install's real module set; it stands aside for any other mod that patches the
  methods it bypasses; any exception re-runs the engine's own merge, and an exception the engine does
  not reproduce turns the fast path off for the session, with a log line)
- **Depends on**: none
- **Category**: perf / load time
- **Planned at**: commit `0912e1b7`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472), measured at `dffdf879` and unchanged at `0912e1b7` (documentation and run records only
  since). Failing: `EveryLanguage_DeclaresARowForEveryEnglishKey` (English keys without rows in the
  other languages; the paid translator run waits on the maintainer). Python suite: not needed, this
  plan touches no `tools/` file (its base is `Ran 2962 tests`, `FAILED (failures=3, skipped=8)`:
  `test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`,
  `test_default_is_on_the_e_drive`). Hosted CI under reference assemblies also fails
  `Patch93_HasTheSevenPatchesInItsCategory` and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`
  (`FileNotFoundException` for `TaleWorlds.MountAndBlade.View`). RefAsm unit step (Commands): not
  measured by the writer; Step 1 records it.
- **Issue**: filed by the orchestrator before execution

## Why this matters

Every campaign load and every custom battle start makes the engine merge each module's ModuleData XML
into one document per type, and the engine's merge loop is quadratic: for every file it re-converts the
whole document merged so far between `XmlDocument` and `XDocument`, recompiles the type's XSD, and
recompiles any XSLT beside the file. Measured from the engine's own rgl logs on 2026-10-02 on the
maintainer's desktop, that merge is about 28 s of a new campaign's 50.2 s loading screen
(NPCCharacters 56 files 13.3 s, Items 142 files 9.6 s, GameText 1.7 s, EquipmentRosters 1.4 s) and
about 14 s of a custom battle's load (NPCCharacters 48 files 8.3 s, Items 141 files 5.7 s); a 3 KB
TAOM NPC file costs 0.20 s only because 7 to 10 MB were merged before it. An offline prototype that
keeps one `XDocument` across the loop and compiles each XSLT once, calling the engine's own helpers,
cut the four heavy types from about 8.8 s to 2.4 to 3.1 s with output identical character for
character. This plan ships that loop as a Harmony prefix, proves byte-identical output on every type
of the live install before the patch exists, logs every merge to `taom_debug.log`, and leaves the two
remaining levers (`lords.xslt`, fewer files) to the maintainer with the numbers.

## Current state

### The engine's merge (installed v1.5.3, `pwsh tools/taom-src.ps1 path TaleWorlds.ObjectSystem.MBObjectManager`)

Line numbers are the decompiled file's (`TaleWorlds.ObjectSystem.MBObjectManager.cs`). Copied from the
writer's own read on 2026-10-02.

`CreateMergedXmlFile` (:962-978), the patch target. `public static`, parameter names `toBeMerged`,
`xsltList`, `skipValidation` (Harmony binds prefix arguments by name):

```csharp
public static XmlDocument CreateMergedXmlFile(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation)
{
    XmlDocument xmlDocument = CreateDocumentFromXmlFile(toBeMerged[0].Item1, toBeMerged[0].Item2, skipValidation);
    for (int i = 1; i < toBeMerged.Count; i++)
    {
        if (xsltList[i] != "")
        {
            xmlDocument = ApplyXslt(xsltList[i], xmlDocument);
        }
        if (toBeMerged[i].Item1 != "")
        {
            XmlDocument xmlDocument2 = CreateDocumentFromXmlFile(toBeMerged[i].Item1, toBeMerged[i].Item2, skipValidation);
            xmlDocument = MergeTwoXmls(xmlDocument, xmlDocument2, toBeMerged[i].Item2, keepDuplicates: false);
        }
    }
    return xmlDocument;
}
```

Facts that follow from it, each one a branch the fast path must keep: `xsltList[0]` is never applied;
an entry's XSLT transforms the document merged so far BEFORE that entry's file is merged; an entry
with file path `""` (an XmlNode that names no file, the `lords.xslt` case) applies its XSLT and merges
nothing; a list of one entry returns the loaded file's document as is; the comparisons are `!= ""`
and `== ""`, not null checks.

`ApplyXslt` (:980-991), public static, compiles the stylesheet on every call:

```csharp
public static XmlDocument ApplyXslt(string xsltPath, XmlDocument baseDocument)
{
    XmlReader input = new XmlNodeReader(baseDocument);
    XmlReader stylesheet = XmlReader.Create(new StreamReader(xsltPath));
    XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
    xslCompiledTransform.Load(stylesheet);
    XmlDocument xmlDocument = new XmlDocument(baseDocument.CreateNavigator().NameTable);
    using XmlWriter xmlWriter = xmlDocument.CreateNavigator().AppendChild();
    xslCompiledTransform.Transform(input, xmlWriter);
    xmlWriter.Close();
    return xmlDocument;
}
```

`MergeTwoXmls` (:993-1006), public static, converts the whole accumulated document both ways per file:

```csharp
public static XmlDocument MergeTwoXmls(XmlDocument xmlDocument1, XmlDocument xmlDocument2, string xsdPath, bool keepDuplicates)
{
    XDocument xDocument = ToXDocument(xmlDocument1);
    XDocument xDocument2 = ToXDocument(xmlDocument2);
    if (keepDuplicates || xsdPath == "")
    {
        xDocument.Root.Add(xDocument2.Root.Elements());
    }
    else
    {
        MergeElements(xDocument.Root, xDocument2.Root, xsdPath);
    }
    return ToXmlDocument(xDocument);
}
```

`ToXDocument` (:1008-1021) and `ToXmlDocument` (:1023-1031), both public static:
`ToXDocument` loads `XDocument.Load(new XmlNodeReader(xmlDocument))` after `MoveToContent()` (and
`Debug.Print`s then rethrows on failure); `ToXmlDocument` builds `new XmlDocument()` and
`Load(xDocument.CreateReader())`.

`MergeElements(XElement element1, XElement element2, string xsdPath)` (:820-871), public static. It
reads `XmlResource.XsdElementDictionary[xsdPath]` (the unique attributes and `AlwaysPreferMerge` per
element path), merges a same-key child recursively, appends a new one, and honours
`_replaceWhileMerging="true"` through `MergeElementAttributes` (:799-818), which removes element1's
attributes and then copies every attribute of element2, the marker itself included.

`CreateDocumentFromXmlFile` (:1339-1355) is `private static XmlDocument CreateDocumentFromXmlFile(string xmlPath, string xsdPath, bool forceSkipValidation = false)`:

```csharp
Debug.Print("opening " + xmlPath);
XmlDocument xmlDocument = new XmlDocument();
StreamReader streamReader = new StreamReader(xmlPath);
string xml = streamReader.ReadToEnd();
if (!forceSkipValidation)
{
    LoadXmlWithValidation(xmlPath, xsdPath, xmlDocument);
}
else
{
    xmlDocument.LoadXml(xml);
}
streamReader.Close();
return xmlDocument;
```

`LoadXmlWithValidation(string xmlPath, string xsdPath, XmlDocument xmlDocument)` (:1057-1125),
private static, builds and compiles a new `XmlSchemaSet` for every file:

```csharp
Debug.Print("opening " + xsdPath);
XmlSchemaSet xmlSchemaSet = new XmlSchemaSet();
XmlTextReader xmlTextReader = null;
try
{
    xmlTextReader = new XmlTextReader(new StreamReader(xsdPath));
    xmlSchemaSet.Add(null, xmlTextReader);
}
catch (FileNotFoundException) { Debug.Print("xsd file of " + xmlPath + " could not be found!", 0, Debug.DebugColor.Red); }
catch (XmlSchemaException ex2)
{
    Debug.Print("XmlSchemaException, line number: " + ex2.LineNumber + ", line position: " + ex2.LinePosition + ", SourceSchemaObject: " + ex2.SourceSchemaObject);
    Debug.Print("xsd file of " + xmlPath + " could not be read! " + ex2.Message, 0, Debug.DebugColor.Red);
}
catch (ArgumentNullException ex3)
{
    Debug.Print("ArgumentNullException, ParamName: " + ex3.ParamName);
    Debug.Print("xsd file of " + xmlPath + " could not be read! " + ex3.Message, 0, Debug.DebugColor.Red);
}
catch (Exception ex4) { Debug.Print("xsd file of " + xmlPath + " could not be read! " + ex4.Message, 0, Debug.DebugColor.Red); }
try
{
    xmlSchemaSet.Compile();
    InjectOptionalAttrToAllComplexTypes(xmlSchemaSet, "boolean", "_replaceWhileMerging");
    foreach (XmlSchema item in xmlSchemaSet.Schemas())
    {
        xmlSchemaSet.Reprocess(item);
    }
    xmlSchemaSet.Compile();
}
catch (Exception ex5) { Debug.Print("Schema overlay failed: " + ex5.Message, 0, Debug.DebugColor.Red); }
XmlReaderSettings xmlReaderSettings = new XmlReaderSettings();
xmlReaderSettings.ValidationType = ValidationType.None;
xmlReaderSettings.Schemas.Add(xmlSchemaSet);
xmlReaderSettings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
xmlReaderSettings.ValidationEventHandler += ValidationEventHandler;
xmlReaderSettings.CloseInput = true;
try
{
    XmlReader xmlReader = XmlReader.Create(new StreamReader(xmlPath), xmlReaderSettings);
    xmlDocument.Load(xmlReader);
    xmlReader.Close();
    XmlReaderSettings xmlReaderSettings2 = new XmlReaderSettings();
    xmlReaderSettings2.ValidationType = ValidationType.Schema;
    xmlReaderSettings2.Schemas.Add(xmlSchemaSet);
    xmlReaderSettings2.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
    xmlReaderSettings2.ValidationEventHandler += ValidationEventHandler;
    xmlReaderSettings2.CloseInput = true;
    xmlReader = XmlReader.Create(new StreamReader(xmlPath), xmlReaderSettings2);
    xmlDocument.Load(xmlReader);
    xmlReader.Close();
}
catch (Exception)
{
    _ = new Uri(xmlDocument.BaseURI).LocalPath;
}
xmlTextReader?.Close();
```

The one-line `FileNotFoundException`, `Exception` and `ex5` catches above are the decompile's bodies
on one line each (the two-line bodies are :1071-1080); read :1057-1125 yourself before Stage 2 if
you get there.
`InjectOptionalAttrToAllComplexTypes(XmlSchemaSet set, string type, string name)` (:1239-1270) and
`ValidationEventHandler(object sender, ValidationEventArgs e)` (:1320-1337) are private static.
`Debug.Print` (TaleWorlds.Library, `Debug.cs:163`) is
`public static void Print(string message, int logLevel = 0, DebugColor color = DebugColor.White, ulong debugFilter = 17592186044416uL)`
and does nothing when `Debug.DebugManager` is null (the test process), so a test cannot observe the
rgl lines.

`GetMergedXmlForManaged(string id, bool skipValidation, bool ignoreGameTypeInclusionCheck = true, string gameType = "")`
(:873-914) builds the two lists from `XmlResource.XmlInformationList` (one record per `<XmlNode>` of
every loaded module's `SubModule.xml`, in module load order, then node order):

```csharp
string xsdPath = ModuleHelper.GetXsdPath(id);
foreach (MbObjectXmlInformation xmlInformation in XmlResource.XmlInformationList)
{
    if (!(xmlInformation.Id == id) || !ModuleHelper.IsModuleActive(xmlInformation.ModuleName) || (!ignoreGameTypeInclusionCheck && xmlInformation.GameTypesIncluded.Count != 0 && !xmlInformation.GameTypesIncluded.Contains(gameType)))
        continue;
    string text = ModuleHelper.GetXsdPathForModules(xmlInformation.ModuleName, xmlInformation.Id);
    if (!File.Exists(text)) text = xsdPath;
    string xmlPath = ModuleHelper.GetXmlPath(xmlInformation.ModuleName, xmlInformation.Name);
    if (File.Exists(xmlPath))
    {
        list.Add(Tuple.Create(ModuleHelper.GetXmlPath(xmlInformation.ModuleName, xmlInformation.Name), text));
        HandleXsltList(ModuleHelper.GetXsltPath(xmlInformation.ModuleName, xmlInformation.Name), ref xsltList);
        continue;
    }
    string text2 = xmlPath.Replace(".xml", "");
    if (Directory.Exists(text2))
    {
        FileInfo[] files = new DirectoryInfo(text2).GetFiles("*.xml");
        foreach (FileInfo fileInfo in files)
        {
            xmlPath = text2 + "/" + fileInfo.Name;
            list.Add(Tuple.Create(xmlPath, text));
            HandleXsltList(xmlPath.Replace(".xml", ".xsl"), ref xsltList);
        }
    }
    else
    {
        list.Add(Tuple.Create("", ""));
        HandleXsltList(ModuleHelper.GetXsltPath(xmlInformation.ModuleName, xmlInformation.Name), ref xsltList);
    }
}
return CreateMergedXmlFile(list, xsltList, skipValidation);
```

`HandleXsltList` (:945-960) adds `xslPath` if that file exists, else `xslPath + "t"` if that exists,
else `""`. Path helpers (`TaleWorlds.ModuleManager.ModuleHelper`, decompiled the same way):
`GetXmlPath(m, n) = GetModuleFullPath(m) + "ModuleData/" + n + ".xml"`,
`GetXsltPath(m, n) = GetModuleFullPath(m) + "ModuleData/" + n + ".xsl"`,
`GetXsdPathForModules(m, id) = GetModuleFullPath(m) + "ModuleData/XmlSchemas/" + id + ".xsd"`,
`GetXsdPath(id) = BasePath.Name + "XmlSchemas/" + id + ".xsd"`. No installed module ships a
`ModuleData/XmlSchemas` folder (writer's `ls`, 2026-10-02), so every list uses the game's
`XmlSchemas/<id>.xsd`, whose file name is the type id.

`XmlResource.GetXmlListAndApply(string moduleName)` (`TaleWorlds.ObjectSystem.XmlResource.cs:258-326`)
fills `XmlInformationList` and calls `ReadXsdFileAndExtractInformation(ModuleHelper.GetXsdPath(id))`
for every XmlNode whose default XSD exists; `public static void ReadXsdFileAndExtractInformation(string xsdFilePath)`
(:116-151) fills `public static Dictionary<string, Dictionary<string, XsdElement>> XsdElementDictionary`
(:78), keyed by the XSD path string exactly as passed. `MergeElements` throws
`KeyNotFoundException` for an XSD path that was never read. The XSD element paths are built by
`GetFullXPathOfElement` (:171-203): `/Root`, `/Root/Child`, and an `xs:unique` or `xs:key` under an
element adds its `@field` names (without the `@`) to the element at `<owner path>/<selector xpath>`.

### Who calls the target, from where, and when

- `GetMergedXmlForManaged(..., skipValidation: false, ...)` from `MBObjectManager.LoadXML(string id, bool isDevelopment, string gameType, bool skipXmlFilterForEditor = false)` (:786-797),
  reached through `MBObjectManagerExtensions.LoadXML(this MBObjectManager, string id, bool skipXmlFilterForEditor = false)`
  (`TaleWorlds.Core`), which passes `Game.Current.GameType.GameTypeStringId` as the game type.
  Campaign: `Campaign.OnInitialize` (`TaleWorlds.CampaignSystem.Campaign.cs:1385-1472`) runs every load
  and then calls `base.GameManager.OnGameInitializationFinished(base.CurrentGame)` at :1471. Custom
  battle: `CustomGame.OnInitialize` (`TaleWorlds.MountAndBlade.CustomBattle.CustomGame.cs:34-57`) runs
  `LoadCustomGameXmls()` (Items, EquipmentRosters, NPCCharacters, SPCultures, all `skipValidation: false`)
  and then `OnGameInitializationFinished` at :56. Also validated: `GameTextManager` (GameText),
  `BannerManager` (BannerIcons).
- `skipValidation: true` callers, which this plan leaves on the engine's own code: `GetMergedXmlForNative`
  (:916-943), `ManagedParameters` (CoreParameters), and TAOM's own
  `Main/Adapters/ObjectManagerAdapter.cs:108-109` (SPCultures) and
  `Main/Adapters/MonsterSizeCatalogAdapter.cs:48-49` (Monsters, every game init).
- Call rate: one call per XML type per load, about 30 to 40 per campaign load. Never per frame, so
  PatchShield's per-call finalizer tax (lessons "PatchShield wraps TAOM's own patch on an engine
  method") does not apply and no `PatchShieldPolicy` exclusion is needed.
- Thread: the callers above run inside game initialisation; nothing proves the thread with a log line,
  so the service takes one lock around its fast path and its caches rather than relying on it.
- The category must be applied in `OnSubModuleLoad` (the composition root's `ApplyPhase.ProcessLoad`):
  the first merges of a game happen long before `OnGameInitializationFinished`, the late batch.

### What was measured (the numbers this plan is judged on)

From the engine's rgl logs (the game's `logs` folder under ProgramData), 2026-10-02, the
maintainer's desktop:

| Load | Window | Merge cost |
|---|---|---|
| New campaign, `rgl_log_32020.txt` | 12:45:00 to 12:45:50 (50.2 s) | 329 files; NPCCharacters 56 files 13.3 s, Items 142 files 9.6 s, GameText 30 files 1.7 s, EquipmentRosters 31 files 1.4 s |
| Custom battle, `rgl_log_56116.txt` | 14:33:30 to 14:33:55 | 274 files; NPCCharacters 48 files 8.3 s, Items 141 files 5.7 s |
| Game start | | 161 files, 0.2 s (unvalidated types; out of scope) |

Offline, same machine, Windows PowerShell 5.1 loading the installed `TaleWorlds.ObjectSystem.dll`
(the orchestrator's prototype; this plan's harness replaces it), "Campaign" game type, module order
`TAOM.Dependencies, Native, SandBoxCore, CustomBattle, SandBox, StoryMode, BirthAndDeath, FastMode, LOTRLOME_Armory, TAOM_Map, TAOM`
(FastMode is not installed and is skipped):

| Type | Entries | XSLTs | Engine `CreateMergedXmlFile`, two runs | Prototype, two runs | Identical |
|---|---|---|---|---|---|
| NPCCharacters | 57 (56 files + the `lords` XSLT-only node) | 1 | 5,432 / 5,647 ms | 2,384 / 1,774 ms (XSLT 1,246 ms, load and validation 361 ms, MergeElements 51 ms) | yes, 7,276,693 chars |
| Items | 142 | 0 | 2,404 / 2,420 ms | 374 / 343 ms | yes, 2,637,805 chars |
| EquipmentRosters | 31 | 0 | 591 / 552 ms | 102 / 101 ms | yes, 2,346,944 chars |
| GameText | 33 | 3 | 379 / 384 ms | 218 / 220 ms | yes, 2,321,719 chars |

`lords.xslt` compiles in 136 to 212 ms; every other TAOM XSLT in 14 ms or less.

### The repo side

- **Composition root** (plan 018): a feature owns its wiring in a `TaomFeatureModule`
  (`Main/Composition/TaomFeatureModule.cs`): `RegisterServices`, `InitializeStatics`, `PatchCategories`
  as `PatchCategoryDecl(category, ApplyPhase)`, `OnPhase`. `ApplyPhase.ProcessLoad` is "OnSubModuleLoad,
  once per process"; `SubModule.cs:656` runs it (`FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);`).
  Exemplar: `Main/Features/CreatureBandits/CreatureBanditsModule.cs` (a ProcessLoad category and an
  `InitializeStatics` that hands a patch class its logger) and
  `Main/Features/TournamentRewards/TournamentRewardsModule.cs`. The list,
  `Main/Composition/FeatureModules.cs:16-24`:

  ```csharp
  internal static readonly TaomFeatureModule[] All =
  {
      new Features.WandererAllegiance.WandererAllegianceModule(),
      new Features.CreatureBandits.CreatureBanditsModule(),
      new Features.ArmourAcquisition.ArmourAcquisitionModule(),
      new Features.RealmBorders.RealmBordersModule(),
      new Features.BattleCorpses.BattleCorpsesModule(),
      new Features.TournamentRewards.TournamentRewardsModule(),
  };
  ```

  `TAOM.Tests/Composition/FeatureModulesTests.cs` checks every module (unique id, each category
  declared once and not also applied in `SubModule.cs`).
- **`Main/SubModule.cs`** (single owner; this plan's one edit is listed in Scope). `OnGameInitializationFinished`,
  :1541-1566 at `0912e1b7`:

  ```csharp
  public override void OnGameInitializationFinished(Game game)
  {
      base.OnGameInitializationFinished(game);

      // [SaveLoad] campaign-launch memory stamp, BEFORE the once-per-process guard below so every
      // game init in the process gets one, not just the first.
      StampSaveLoadPhase(Features.SaveLoadDiagnostics.Domain.SaveLoadPhase.GameInitializationFinished);

      // Mount sizes live on the Monster (taom_body_length, docs/features/monster-size.md). Every game init, before
      // the once-per-process guard: each game reloads its items from XML, and no mission has built a mount yet.
      IoC.Resolve<Features.MonsterSize.IMonsterSizeService>().ApplyMonsterSizes();

      // Armour acquisition (docs/features/armour-acquisition.md): every game init too, for the same reason, and
      // before a new game's workshops cache their items (OnNewGameCreatedPartialFollowUp runs after this hook).
      // Only a campaign has the markets, workshops and loot the gate reaches.
      IoC.Resolve<Features.ArmourAcquisition.IArmourGateService>().ApplyGating(game?.GameType is Campaign);

      // Harmony patches are process-global ...
      if (_gameInitPatchesApplied) return;
      _gameInitPatchesApplied = true;
  ```

- **Patch numbering**: the highest category in code is `Patch96_TournamentRewards`; plan 028 takes
  `Patch97_MissionTickProfiler` and plan 041 `Patch98_HitchProbe`. This plan takes
  `Patch99_XmlMergeFastPath`. `git grep -n "Patch99_" -- Main Dependencies TAOM.Tests docs` printed
  nothing at `0912e1b7`.
- **No existing patch** on `MBObjectManager` merge methods: the only `MBObjectManager` patch is
  `Main/Features/StaleCharacterRepair/Hooks/Patch83_StaleCharacterRepair.cs:33` on `PreAfterLoad`.
- **Logging**: `Main/Core/Logging/IModLogger.cs` (`LogInfo`, `LogDebug`, `LogWarning`, `LogError`).
  `Main/Core/Logging/FileLogger.cs:9-11`: "INFO/WARNING/ERROR therefore drain to disk synchronously on
  the calling thread; DEBUG (the bulk of the volume) stays async." So INFO is crash-durable.
- **Co-op classification**: `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` scans
  `Main/` source for every bool-returning prefix and fails unless its class name is in `Registry`.
  The last `ReviewedSafe` entry at `0912e1b7` (:307-310) is `["Patch96_TournamentJoinChoices"]`,
  followed by a blank line and the `// --- Parked ---...` comment at :312.
- **Binding tests**: `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` auto-discovers every
  `[HarmonyPatch]` class. Private members reached by reflection are listed by hand in
  `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (rows
  `[DataRow(fullName, simpleName, member, kind, sourceSite)]`, last row at :120 is the FactionUI
  `ButtonWidget.HandleClick` one) and in `docs/reference/taleworlds-api-snapshot/reflection-sites.md`
  (a table: Engine type | Member | Kind | Source site | What it drives). Exemplar for a per-patch
  binding test class: `TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs`
  (`GameAssemblies.EnsureLoaded()` in `[ClassInitialize]`, `Assert.Inconclusive` without the game,
  parameter-name assertions). IL call scans: `TAOM.Tests/Migration/IlCallScanner.cs`
  (`ExtractCalledMethods(MethodBase method, byte[] il)`), used by
  `TAOM.Tests/Features/AdvancedCombat/CreatureImpactSoundTests.cs:113`.
- **Test categories** (`.claude/rules/tests.md`): `RequiresGame` when a test executes engine code,
  `LiveInstall` when it reads the live install, `BindingVerification` for binding checks, and
  `RequiresGameIL` on a binding test that needs vanilla method IL. Hosted CI runs the unit step with
  `TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification` and the
  binding gate on reference assemblies with `TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall`.
  `TAOM.Tests/Migration/GameAssemblies.cs` resolves the game folder (`GameAssemblies.GameDir`) from
  `BANNERLORD_OVERRIDE_DIR`, `BANNERLORD_GAME_DIR`, then the build's `TaomGameFolder` metadata. The
  test project references every `TaleWorlds.*.dll` of the game bin with `Private=True`
  (`TAOM.Tests/TAOM.Tests.csproj`), so engine code runs in tests; `InternalsVisibleTo("TAOM.Tests")`
  exists. Main and the tests are SDK-style projects: new `.cs` files compile without a csproj edit.
- **Registry**: `docs/reference/harmony-patch-registry.md`, one `## PatchNN_Name` section per category;
  the last is `## Patch96_TournamentRewards` (:1101), followed by an auto-generated
  `<!-- backlinks-start ... -->` block. Exemplar section: `## Patch90_PreloadBodyGuard` (:1045).
- **Feature map**: `docs/reference/feature-map.md`, one row per feature; `| PreloadBodyGuard | ... |`
  is :103. Feature doc template: `docs/features/TEMPLATE.md`.
- **Engine concept doc**: `docs/reference/engine/object-system-mbobjectmanager.md` has a
  `LoadXML(listName)` section (its heading at :37) and no description of the merge loop.

### Blast radius (`python tools/graphify_taom.py affected "<Type>" --depth 2`, at `0912e1b7`)

The graph was stale only on plan files (11 changed, all under `plans/_audit/`), so the answers stand:

- `Main/SubModule.cs`: `- BehaviorTreeMissionLogic.cs [imports] Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs:L5`
- `IoC` (not edited; for reference): `- .OnSubModuleUnloaded() [calls] Main/SubModule.cs:L2227`, `- .OnSubModuleLoad() [calls] Main/SubModule.cs:L120`
- `CoopVetoClassificationTests`: `No affected nodes found.`
- `FeatureModules` and `ReflectionSiteBindingTests` (both edited by Step 7b): `No affected nodes found.`
  each (re-run on the plan's revision; the graph was then stale only on 20 files under `plans/_audit/`).
- `MBObjectManager` matched only a docstring in `tools/sync_lord_inline_skills.py` ("No affected nodes found."); TAOM's own callers of the merge are the two adapters named above.
- Every other type this plan touches is new.

### Conventions that bind this change

- **ADR-002**: entry points (the Harmony patch, the module) are thin and under 150 lines; logic lives in
  a service.
- **ADR-007**: services never touch TaleWorlds types; engine calls go through an adapter with an
  interface in `Main/Adapters/` (`IXmlMergeEngineAdapter`).
- **ADR-008**: no static TaleWorlds calls in services (`MBObjectManager.*`, `Debug.Print` go through the
  adapter); services 100% covered, hooks 80%, entry points by binding and game tests.
- **ADR-003, ADR-004, ADR-005**: no `#region`, no `[Obsolete]`, no `#if`.
- `.claude/rules/harmony-patches.md` and `docs/reviews/lessons/harmony-il.md` (read these entries
  before Step 7: "When a Prefix returns false, decompile the FULL call chain and replicate every safety
  gate", "Substituting for a vanilla method inherits every one of its responsibilities",
  "'Fall through to vanilla on error' is only safe when vanilla is a safe default at THAT call site",
  "A patch's own try/catch cannot survive a JIT-time member-resolution failure", "Harmony binds
  prefix/postfix parameters by NAME", "Static cache state inside a Harmony patch is untestable",
  "Read every patch collection Harmony exposes", "A value-returning finalizer that hands back its
  exception erases the throw site"): every engine member a patch body reaches needs a binding test;
  parameter names are pinned; a cache with an invalidation policy lives in its own class; an
  observe-only finalizer is `void`.
- `.claude/rules/csharp-architecture.md`: constructor injection, `Reuse.Singleton` services, a service
  gets an interface only when a test fakes it or a second class implements it (the adapter has one;
  the service does not).
- `.claude/rules/tests.md`: `MethodName_StateUnderTest_ExpectedBehavior`, MSTest, NSubstitute or
  hand-written fakes, the categories above.
- Logging (the maintainer's instruction, DECISIONS D6, binding on this plan): every instrument logs a
  one-line configuration header when it starts; every measured merge logs its fields; a summary per game
  gives totals, maxima and counts; anything that disables itself, skips work or falls back logs one line
  naming the reason and the consequence, once; nothing per frame at INFO; the feature doc lists every
  line with its fields and an example, and tests pin each format literally. The engine's own rgl lines
  (`opening <file>`, `opening <xsd>`) must keep appearing exactly as before.

## Design (decided; implement it as written)

**Scope of the fast path.** Only calls with `skipValidation == false` (every `LoadXML`, GameText,
BannerIcons). Unvalidated calls cost little and include the engine's native merges and TAOM's own two
adapters; they run the engine's code and are logged as `path=vanilla reason=skip-validation`.

**The loop.** One lock; the same three engine helpers in the same order; the accumulated document
kept as one `XDocument` between merges and converted to `XmlDocument` only before an XSLT and once at
the end. Draft (the executor re-verifies it with the tests of Steps 5 and 6):

```csharp
internal XmlDocument MergeFast(IReadOnlyList<Tuple<string, string>> toBeMerged, IReadOnlyList<string> xsltList,
                               bool skipValidation, XmlMergeCounters counters)
{
    XmlDocument? xml = Load(toBeMerged[0].Item1, toBeMerged[0].Item2, skipValidation, counters);
    XDocument? merged = null;
    for (int i = 1; i < toBeMerged.Count; i++)
    {
        if (xsltList[i] != "")
        {
            if (merged != null) { xml = _engine.ToXmlDocument(merged); merged = null; }
            xml = _xslt.Apply(xsltList[i], xml!);                        // timed into counters.XsltTicks
        }
        if (toBeMerged[i].Item1 != "")
        {
            XmlDocument next = Load(toBeMerged[i].Item1, toBeMerged[i].Item2, skipValidation, counters);
            if (merged == null) { merged = _engine.ToXDocument(xml!); xml = null; }
            XDocument nextX = _engine.ToXDocument(next);
            if (toBeMerged[i].Item2 == "") merged.Root.Add(nextX.Root.Elements());
            else _engine.MergeElements(merged.Root, nextX.Root, toBeMerged[i].Item2);   // timed into MergeTicks
        }
    }
    return merged != null ? _engine.ToXmlDocument(merged) : xml!;
}
```

`Load` is `_engine.CreateDocumentFromXmlFile(path, xsd, skipValidation)` in Stage 1 (timed into
`counters.LoadTicks`). The order inside a merge step matches the engine: load the next file, convert
the accumulated document, convert the next file, merge. The one thing the engine does that this loop
does not is the `ToXmlDocument` then `ToXDocument` round trip of the accumulated document between two
consecutive merges; the harness proves that round trip changes nothing for every type the game merges.

**XSLT.** `XsltTransformCache.Apply(string xsltPath, XmlDocument baseDocument)` is the engine's
`ApplyXslt` line for line, except that the compiled `XslCompiledTransform` comes from a cache keyed by
the path string and validated by the file's last-write time (UTC) and length, so an XSLT edited between
two loads of one process still recompiles. Compilation reads the stylesheet exactly as the engine
does: `XmlReader.Create(new StreamReader(xsltPath))` with default settings, `Load(reader)` with default
`XsltSettings`, disposing both readers afterwards.

**Standing aside.** The fast path runs the engine's code instead (reason `foreign-patch`) when any
Harmony owner other than `com.taom.mod` and `TAOM.Dependencies.Foundation.PatchShield` has, read from
`Harmony.GetPatchInfo` and all six collections (`Prefixes`, `Postfixes`, `Transpilers`, `Finalizers`,
`InnerPrefixes`, `InnerPostfixes`):
- on `CreateMergedXmlFile`: a prefix, transpiler, inner prefix or inner postfix (a foreign postfix or
  finalizer still runs after this prefix and is harmless);
- on a method the fast path bypasses or calls a different number of times (`ApplyXslt`, `MergeTwoXmls`,
  `ToXDocument`, `ToXmlDocument`; Stage 2 adds `CreateDocumentFromXmlFile` and `LoadXmlWithValidation`):
  any patch at all.
`MergeElements` and (in Stage 1) `CreateDocumentFromXmlFile` are called exactly as the engine calls
them, so foreign patches on them still apply and do not make the fast path stand aside.

**Failure handling.** Everything the fast path does is inside one `try`. On an exception it logs once
per exception type, does not set `__result`, and lets the original run (the prefix returns `true`).
That is safe here: the fast path writes nothing outside its own documents and caches, so the engine
runs on the same arguments as an unpatched game and reproduces the engine's own outcome, including an
exception the bad data would have thrown anyway (the only side effect is a repeat of the rgl `opening`
lines for that type). The finalizer then compares: if the fast path threw and the engine's merge
succeeded, the fault is TAOM's, and the fast path is disabled for the rest of the session with one
WARNING line; if both threw, the data is bad and the fast path stays on.

**Harmony shape.** A `[HarmonyPrefix, HarmonyPriority(Priority.Last)]` that returns `false` with
`__result` set when the fast path merged, and a `void [HarmonyFinalizer]` (observe only, keeps
Harmony's `rethrow`) that logs the engine-path line. They share a `__state` of type `XmlMergeCall`
(precedent for `__state` in a finalizer: `Main/Features/LordPartyTemplates/Hooks/Patch88_SpawnLordPartyScope.cs:36`).
`Priority.Last` so any other prefix runs first; the stand-aside rule already covers a foreign prefix.

**Responsibilities of the skipped original, each kept or dropped on purpose** (lessons: a skipped
original's responsibilities are a list, not a reading):

| The engine's `CreateMergedXmlFile` does | Fast path |
|---|---|
| Loads and validates each file through `CreateDocumentFromXmlFile` (prints `opening <file>` and `opening <xsd>`, validation messages to rgl) | kept: the same call per file (Stage 2: an identical mirror on a cached schema set, same prints) |
| Applies each entry's XSLT to the document so far, before that entry's file | kept, same order; compile cached |
| Merges each file with `MergeElements`, or appends when the XSD path is `""` | kept, same calls and arguments |
| Round-trips the accumulated document through `XmlDocument` between merges | dropped; proven output-neutral by the harness on every type |
| Returns the document; throws what its helpers throw | kept: identical document (gate); any exception re-runs the original |

**Wiring.** A feature module `XmlMergeModule` (`Main/Features/XmlMerge/XmlMergeModule.cs`) registers
the adapter, the caches and the service, hands the patch class the service in `InitializeStatics`
and logs the configuration header there, and declares `Patch99_XmlMergeFastPath` at
`ApplyPhase.ProcessLoad`. One line appended to `FeatureModules.All`. The per-game summary needs a hook
that runs at every game init (the module runner's `OnPhase(GameInit)` runs once per process), so it is
one guarded line in `SubModule.OnGameInitializationFinished`, before the once-per-process guard.

**The log lines** (all through `IModLogger`; formats pinned by `XmlMergeLinesTests`; integers via
`CultureInfo.InvariantCulture`; milliseconds rounded to whole numbers):

| When | Level | Format |
|---|---|---|
| Module start, bindings resolved (header) | INFO | `[XmlMerge] fast path ready: engine bindings resolved (CreateDocumentFromXmlFile, MergeElements, ToXDocument, ToXmlDocument); applies to validated merges (skipValidation=false); xslt cache on; schema cache off` |
| Module start, a binding missing (header) | WARNING | `[XmlMerge] fast path off: {problem}; every module XML merge runs the engine's own code` |
| Each fast merge | INFO | `[XmlMerge] type={type} files={files} xslt={xslt} ms={ms} path=fast load_ms={load} xslt_ms={xsltMs} merge_ms={merge} xslt_compiles={c} xslt_cache_hits={h} schema_builds={sb} schema_cache_hits={sh}` |
| Each engine-path merge | INFO | `[XmlMerge] type={type} files={files} xslt={xslt} ms={ms} path=vanilla reason={reason} result={ok or exception type name}` |
| First stand-aside per distinct patch set | INFO | `[XmlMerge] fast path stands aside: {patches}; merges run the engine's own code while those patches are present` |
| First fast-path exception per exception type | WARNING | `[XmlMerge] fast path failed on type={type}: {ExceptionType}: {message}; this merge re-runs the engine's own code` |
| Fast path turned off for the session | WARNING | `[XmlMerge] fast path disabled for this session: it threw {ExceptionType} on type={type} where the engine's own merge succeeded` |
| Every game init (summary of merges since the previous summary or process start) | INFO | `[XmlMerge] summary game={gameType} merges={n} fast={f} vanilla={v} files={files} ms={ms} max_ms={max} max_type={type} xslt_compiles={c} xslt_cache_hits={h} schema_builds={sb} schema_cache_hits={sh} fast_path={state}` |

Field rules: `state` is `on`, `disabled` (turned off for the session after its own fault) or `off`
(a binding did not resolve); `max_type` is `none` when the window had no merge. `type` is `Path.GetFileNameWithoutExtension` of the first non-empty XSD path in the list
(the type id, since every list uses `XmlSchemas/<id>.xsd`), else `unknown`. `files` counts entries
whose file path is not `""`. `xslt` counts entries at index 1 or later whose XSLT path is not `""` (the
engine never applies index 0). `reason` is one of `skip-validation`, `bindings`, `empty-list`,
`foreign-patch`, `disabled`, `fast-path-error`, `not-initialized` (the last is never logged: without a
service there is no logger). In Stage 1, `schema_builds` equals `files` on a fast merge (the engine
builds the XSD's schema set for every validated file) and `schema_cache_hits` is 0; Stage 2 changes the
numbers, not the format. `{patches}` is the sorted, comma-joined list of `owner kind Type.Method`
strings. Per-call lines are INFO because they are per load, not per frame (about 30 to 40 lines per
load), and a hang inside a merge then leaves the last completed type on disk.

Summary window rules: `merges`, `files`, `ms`, `max_ms` and `max_type` cover every merge the window
saw, fast and engine path alike (`merges = fast + vanilla`; a merge whose fast attempt threw counts
once, as `vanilla`, and its `ms` runs from the prefix to the finalizer); `xslt_compiles`,
`xslt_cache_hits`, `schema_builds` and `schema_cache_hits` sum the fast merges only, since the fast
path cannot see inside an engine-path merge. A merge's `ms` is measured from `XmlMergeCall`'s start
timestamp to the moment its line is logged.

## Step 0: the maintainer's edit

None. This plan edits no protected file (`.claude/settings.json`, `.claude/settings.local.json`,
`Directory.Build.props`, `docs/adrs/*.md`). If any step seems to need one, STOP.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus the new tests |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| One class with its printed table | the same, plus `--logger "console;verbosity=detailed"` | the test's `TestContext.WriteLine` output appears |
| Docs | `python -B tools/lint_docs.py --fail-on-drift` | exit 0 |
| Data | `python -B tools/validate_moduledata.py` | not needed: this plan changes no ModuleData |
| RefAsm unit step (hosted CI's build and unit test, `.github/workflows/csharp.yml`) | `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=`, then `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"` | the failure set Step 1 recorded, no new names |
| RefAsm binding gate (hosted CI's third step), right after the RefAsm unit step | `BANNERLORD_GAME_DIR="$(pwd -W)/TAOM.Tests/bin/Debug/net472/refasm-game" dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall"` | the failure set Step 1 recorded, no new names, no skipped test |

Both MSBuild flags go on build AND test, and prefix every dotnet command with the `TEMP` and `TMP`
your dispatch rules give. Never `./build.ps1`: it deploys into the game install. Run `python` with
`-B`; never `python3`. The RefAsm build overwrites `TAOM.Tests/bin`, so run the plain `Tests` command
(it rebuilds against the installed game) after it, never `--no-build`. If the RefAsm restore cannot
download its reference packages, report the step as not run with the error; that is not a STOP.

## Scope

**In scope** (the only files you create or modify):

- New, `Main/Features/XmlMerge/`: `XmlMergeModule.cs`, `XmlMergeService.cs`, `XmlMergeCall.cs`,
  `XmlMergeCounters.cs`, `XmlMergeLines.cs`, `XsltTransformCache.cs`; Stage 2 only:
  `XmlSchemaSetCache.cs`.
- New, `Main/Features/XmlMerge/Hooks/MBObjectManager_CreateMergedXmlFile_Patch.cs`.
- New, `Main/Adapters/IXmlMergeEngineAdapter.cs`, `Main/Adapters/XmlMergeEngineAdapter.cs`.
- New, `TAOM.Tests/Features/XmlMerge/`: `XmlMergeLinesTests.cs`, `XsltTransformCacheTests.cs`,
  `XmlMergeServiceTests.cs`, `RecordingXmlMergeEngine.cs` (the test fake), `XmlMergeAssert.cs` and
  `XmlMergeAssertTests.cs`, `XmlMergeFixtures.cs`, `XmlMergeEngineEquivalenceTests.cs`,
  `LiveMergeListBuilder.cs`, `XmlMergeLiveEquivalenceTests.cs`, `XmlMergeBindingTests.cs`,
  `XmlMergeWiringTests.cs`; Stage 2 only:
  `XmlSchemaSetCacheTests.cs`.
- `Main/Composition/FeatureModules.cs`: append one line (Step 7).
- `Main/SubModule.cs` (single owner): exactly one edit, in `OnGameInitializationFinished` after the
  `ApplyGating` line and before the `if (_gameInitPatchesApplied) return;` guard (Step 7). Nothing else.
- `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`: one `Registry` entry (Step 7).
- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: one `DataRow` (Stage 2: two more).
- Docs: new `docs/features/xml-merge-fast-path.md`; a row in `docs/reference/feature-map.md`; a
  `## Patch99_XmlMergeFastPath` section in `docs/reference/harmony-patch-registry.md`; a row in
  `docs/reference/taleworlds-api-snapshot/reflection-sites.md` (Stage 2: two more); one paragraph in
  `docs/reference/engine/object-system-mbobjectmanager.md`.

**Out of scope** (do NOT touch, even though they look related):

- `Main/IoC.cs`: single owner; the module registers everything. If a registration seems to need
  `IoC.cs`, STOP and report the exact line.
- `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`, `Directory.Build.props`: no edit is needed.
- Any XML, XSLT or XSD content, in the repo or the live install: this plan changes how files are
  merged, never what they say. `lords.xslt` and `tools/complete_lords_xslt.py` stay as they are.
- The live `LOTRLOME_Armory` and `TAOM_Map` installs, and concatenating any module's files (the
  second lever: a maintainer decision, see Maintenance notes).
- `PatchShieldPolicy` (the target is not hot), any MCM settings class (no new setting; see Maintenance
  notes for the kill-switch question), `CHANGELOG.md`, `plans/README.md`.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an entry to a validator allowlist. STOP and report instead. The one
  registry edit this plan makes to `CoopVetoClassificationTests` is a classification, with its reason.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at `0912e1b7`; a hook refuses
  any other). Stage 1: `perf(xml-merge): <version> - merge module XML in one document per load`.
  Stage 2, if it lands: `perf(xml-merge): <version> - reuse each compiled XSD across a merge`.
- Stage explicit paths only; write the message to a file in your scratch folder and run
  `git commit -F "<file>"`. Never `--no-verify`.
- The body is the changelog entry, wrapped at 72: what changed and why, for a reader of the release
  note, with the harness's measured numbers (before and after per heavy type, the "identical" count)
  and the in-game rgl baseline (a new campaign's 50.2 s screen, 28 s of it merging). Say that
  `taom_debug.log` now has one `[XmlMerge]` line per merged type and a summary per game, and that the
  engine's rgl log keeps its `opening` lines. Never edit `CHANGELOG.md`. No AI attribution trailer.
  Trailers: `Not-tested:` (the in-game load: the Harmony prefix running inside the game, the rgl lines
  it keeps, the per-game summary line; the maintainer's check after merge), and `Research:` (the
  installed v1.5.3 `MBObjectManager` excerpts this plan quotes).

## Steps

### Step 1: drift check, base, and the patch number

Run the drift check and the two checks at the top. Then, before any edit: run the RefAsm unit step and
the RefAsm binding gate and record their totals and failing names; then the full `Tests` command; then
`git grep -n "Patch99_" -- Main Dependencies TAOM.Tests docs`.

**Verify**: `Tests` prints `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`, the one failure
`EveryLanguage_DeclaresARowForEveryEnglishKey`, or the difference is explained by another plan that
landed (name it). The `git grep` prints nothing. The RefAsm totals and failure names are recorded.

### Step 2: the comparison helper, proven able to fail (TDD)

Create `TAOM.Tests/Features/XmlMerge/XmlMergeAssert.cs`: `internal static class XmlMergeAssert` with
`public static void SameDocument(string engineXml, string fastXml, string label)`. It compares with
`string.Equals(engineXml, fastXml, StringComparison.Ordinal)` and, on a difference, calls
`Assert.Fail` with the label, both lengths, the first differing index, and up to 300 characters of
each string starting 120 characters before that index (the prototype's report shape). The message is
`{label}: documents differ; engine length {n}, fast length {m}, first difference at {i}; engine: {context}; fast: {context}`,
where `{i}` is the first index whose characters differ, or the shorter length when one string is a
prefix of the other. Write it first as `=> throw new NotImplementedException();`.

Create `XmlMergeAssertTests.cs` (no category: pure strings, runs on CI):
`SameDocument_IdenticalStrings_Passes`, `SameDocument_OneCharacterDiffers_FailsNamingTheIndex`
(`"<a x=\"1\"/>"` against `"<a x=\"2\"/>"`: catch `AssertFailedException` and assert its message
contains `first difference at 6`), `SameDocument_DifferentLengths_FailsNamingBothLengths`
(`"<a/>"` against `"<a/>x"`: the message contains `engine length 4, fast length 5, first difference at 4`).

**Verify (RED)**: build Main and the tests with the `One test class` command, filter
`FullyQualifiedName~XmlMergeAssertTests`: three tests fail with `NotImplementedException`.
Implement. **Verify (GREEN)**: the same command, three passed.

### Step 3: the log lines (TDD, the contract)

Create `Main/Features/XmlMerge/XmlMergeLines.cs`, `internal static class XmlMergeLines`, one method per
row of the log table in Design, each a stub that throws:

```csharp
internal static string Ready();
internal static string Off(string problem);
internal static string Fast(string type, int files, int xslts, double ms, double loadMs, double xsltMs, double mergeMs,
                            long xsltCompiles, long xsltCacheHits, long schemaBuilds, long schemaCacheHits);
internal static string Vanilla(string type, int files, int xslts, double ms, string reason, string result);
internal static string StandAside(IReadOnlyList<string> patches);
internal static string FastFailed(string type, string exceptionType, string message);
internal static string Disabled(string type, string exceptionType);
internal static string Summary(string gameType, int merges, int fast, int vanilla, int files, double ms, double maxMs,
                               string maxType, long xsltCompiles, long xsltCacheHits, long schemaBuilds,
                               long schemaCacheHits, string fastPathState);
```

Every `double` milliseconds value is written as
`Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)`, every
integer with `CultureInfo.InvariantCulture`.

`XmlMergeLinesTests.cs` (no category): one test per method asserting the exact string for fixed
inputs, for example
`Fast_AllFields_MatchesThePinnedFormat` expects
`[XmlMerge] type=NPCCharacters files=56 xslt=1 ms=1774 path=fast load_ms=361 xslt_ms=1246 merge_ms=51 xslt_compiles=0 xslt_cache_hits=1 schema_builds=56 schema_cache_hits=0`,
`Vanilla_SkipValidation_MatchesThePinnedFormat` expects
`[XmlMerge] type=Monsters files=4 xslt=0 ms=12 path=vanilla reason=skip-validation result=ok`,
`Summary_AllFields_MatchesThePinnedFormat` expects
`[XmlMerge] summary game=Campaign merges=32 fast=28 vanilla=4 files=329 ms=6100 max_ms=1774 max_type=NPCCharacters xslt_compiles=9 xslt_cache_hits=0 schema_builds=320 schema_cache_hits=0 fast_path=on`,
plus `Ready_Default_MatchesThePinnedFormat`, `Off_WithProblem_MatchesThePinnedFormat`,
`StandAside_TwoPatches_SortsAndJoinsThem`, `FastFailed_WithMessage_MatchesThePinnedFormat` (expects
`[XmlMerge] fast path failed on type=Items: InvalidOperationException: boom; this merge re-runs the engine's own code`),
`Disabled_WithType_MatchesThePinnedFormat` (expects
`[XmlMerge] fast path disabled for this session: it threw InvalidOperationException on type=Items where the engine's own merge succeeded`),
and `Fast_FractionalMilliseconds_RoundsUnderAnyCulture` (run under `de-DE` with `1773.6` ms:
`ms=1774`; restore the thread's culture in a `finally`).
Take every literal from the Design table; the table is the contract.

**Verify (RED)**: filter `FullyQualifiedName~XmlMergeLinesTests`: every test fails with
`NotImplementedException`. Implement. **Verify (GREEN)**: all pass.

### Step 4: the XSLT cache (TDD; System.Xml only)

`Main/Features/XmlMerge/XsltTransformCache.cs`: `public sealed class XsltTransformCache` (public with
a public parameterless constructor: it is a constructor parameter of the public `XmlMergeService`,
see Step 5) with `public XmlDocument Apply(string xsltPath, XmlDocument baseDocument)` (the Design's mirror of `ApplyXslt`),
`long Compiles` and `long CacheHits` (read under the same lock that guards the dictionary). Stub first.

`XsltTransformCacheTests.cs` (no category; writes its stylesheets to a fresh folder under
`Path.GetTempPath()` and deletes it in `[TestCleanup]`):
`Apply_SamePathTwice_CompilesOnce` (Compiles 1, CacheHits 1),
`Apply_FileRewrittenWithNewTime_Recompiles` (rewrite the stylesheet, `File.SetLastWriteTimeUtc` one
minute later; Compiles 2),
`Apply_DropTemplate_RemovesTheMatchedElement` (identity plus `<xsl:template match="Thing[@id='b']"/>`),
`Apply_Output_UsesTheInputDocumentsNameTable` (`ReferenceEquals(output.NameTable, input.NameTable)`,
the engine's `new XmlDocument(baseDocument.CreateNavigator().NameTable)`),
`Apply_MissingStylesheet_Throws` (the fallback depends on an exception, not a silent empty result).

**Verify (RED)**: filter `FullyQualifiedName~XsltTransformCacheTests`: all fail with
`NotImplementedException`. Implement. **Verify (GREEN)**: all pass.

### Step 5: the adapter interface, the fake, and the service (TDD)

Create `Main/Adapters/IXmlMergeEngineAdapter.cs`:

```csharp
public interface IXmlMergeEngineAdapter
{
    /// <summary>Null when every engine member the fast path calls resolved; otherwise which did not.</summary>
    string? BindingProblem { get; }
    XmlDocument CreateDocumentFromXmlFile(string xmlPath, string xsdPath, bool forceSkipValidation);
    XDocument ToXDocument(XmlDocument document);
    XmlDocument ToXmlDocument(XDocument document);
    void MergeElements(XElement element1, XElement element2, string xsdPath);
    /// <summary>"owner kind Type.Method" for every patch that makes the fast path unsafe (Design, "Standing aside"); empty when none.</summary>
    IReadOnlyList<string> ForeignPatches();
}
```

Create the service files as stubs. Visibility is load-bearing: the patch class is `public static`
and its `Initialize(XmlMergeService)` and `out XmlMergeCall __state` expose these types, so
`XmlMergeService`, `XmlMergeCall` and `XmlMergeCounters` are `public sealed class`es with public
constructors, like the repo's other module services (`TournamentJoinService.cs:13,20`). Never make a
constructor `internal` to get past CS0051: the tests would still pass through `InternalsVisibleTo`,
but DryIoc's container (`IoC.cs:92`, `new Container()` with default rules) is not expected to build a
type without a public constructor, so the game would fail in `InitializeStatics`; Step 7b's
`XmlMergeWiringTests` resolves the service from a fresh `Container` to catch exactly that.
`XmlMergeCall` (start timestamp from `Stopwatch.GetTimestamp()`,
`Handled`, `VanillaReason`, `FastErrorType`, `Type`, `Files`, `Xslts`), `XmlMergeCounters` (load,
XSLT and merge ticks, XSLT compiles and hits, schema builds and hits), and
`public XmlMergeService(IXmlMergeEngineAdapter engine, XsltTransformCache xslt, IModLogger logger)` with:
`internal string? Decide(bool skipValidation, int entryCount)` (returns the reason, or null for the
fast path; order: `disabled`, `bindings`, `empty-list`, `skip-validation`, `foreign-patch`);
`internal XmlDocument MergeFast(...)` (Design); `public bool TryMergeFast(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation, XmlMergeCall call, out XmlDocument? result)`
(fills `call`, decides, merges under the lock, logs the fast line or the reason line once, never
throws); `public void OnOriginalFinished(XmlMergeCall call, Exception? exception)` (does nothing when
`call.Handled`; else logs the vanilla line and settles a fast-path failure as Design says);
`public void LogConfigurationHeader()`; `public void LogWindowSummary(string? gameType)` (logs and
resets the window; `gameType` null logs `none`); `internal static string TypeOf(IReadOnlyList<Tuple<string, string>> toBeMerged)`
(the `type` field rule in Design).

Create `TAOM.Tests/Features/XmlMerge/RecordingXmlMergeEngine.cs`, a hand-written fake implementing
the interface: documents come from an in-memory map of path to XML text; `ToXDocument` and
`ToXmlDocument` use the engine's own System.Xml code shown in Current state; `MergeElements` appends
`element2`'s child elements to `element1`; every call appends a token to `Calls` (`load a.xml`, `toX`,
`toXml`, `merge`); `BindingProblem` and `ForeignPatches` are settable. Logs go to an NSubstitute
`IModLogger`.

`XmlMergeServiceTests.cs` (no category; no engine type anywhere in the service or the fake):
- Decide, one test per reason and one for the fast path: `Decide_SkipValidation_ReturnsSkipValidation`,
  `Decide_BindingProblem_ReturnsBindings`, `Decide_EmptyList_ReturnsEmptyList`,
  `Decide_ForeignPatch_ReturnsForeignPatch`, `Decide_AfterSessionDisable_ReturnsDisabled`,
  `Decide_AllClear_ReturnsNull`.
- The loop, asserting `Calls` exactly: `MergeFast_ThreeFilesNoXslt_ConvertsTheAccumulatorOnceEachWay`
  (`load a`, `load b`, `toX`(a), `toX`(b), `merge`, `load c`, `toX`(c), `merge`, `toXml`),
  `MergeFast_XsltBesideSecondFile_TransformsBeforeLoadingThatFile`,
  `MergeFast_FirstEntryXslt_IsNeverApplied`,
  `MergeFast_SingleEntry_ReturnsTheLoadedDocumentUnconverted` (`ReferenceEquals` with the loaded one),
  `MergeFast_LastStepIsXslt_ReturnsTheTransformOutputUnconverted`,
  `MergeFast_EmptyXsdPath_AppendsWithoutMergeElements`,
  `MergeFast_EmptyFilePath_AppliesItsXsltAndLoadsNothing`,
  `MergeFast_TwoXsltsInARow_DoNotConvertBetweenThem`.
  (XSLT steps use real stylesheets written to a temp folder, through a real `XsltTransformCache`.)
- Outcomes: `TryMergeFast_AllClear_LogsOneFastLineAndReturnsTheDocument`,
  `TryMergeFast_SkipValidation_ReturnsFalseAndLogsNothingYet` (the line comes from the finalizer),
  `OnOriginalFinished_EnginePath_LogsTheVanillaLineWithTheReason`,
  `TryMergeFast_ForeignPatch_LogsTheStandAsideLineOnlyOnce` (two calls, one stand-aside line),
  `TryMergeFast_AdapterThrows_ReturnsFalseAndLogsTheFailureOncePerType` (two calls, one WARNING),
  `OnOriginalFinished_FastFailedAndEngineSucceeded_DisablesForTheSession` (next `Decide` returns
  `disabled`; one `Disabled` WARNING),
  `OnOriginalFinished_FastFailedAndEngineThrew_StaysEnabled`,
  `LogWindowSummary_AfterMerges_LogsTotalsAndMaximaThenResets` (second call logs zero counts and
  `max_type=none`),
  `LogWindowSummary_EnginePathMerge_CountsInTotalsButNotInCacheCounters` (one fast and one engine-path
  merge: `merges=2 fast=1 vanilla=1`, `files` the sum of both, cache counters the fast merge's only),
  `LogConfigurationHeader_BindingsResolved_LogsReady`, `LogConfigurationHeader_BindingProblem_LogsOff`,
  `TypeOf_FirstNonEmptyXsdPath_IsTheFileNameWithoutExtension` and `TypeOf_NoXsdPath_IsUnknown`.
Assert log LEVELS too: fast, vanilla, stand-aside, header-ready and summary lines are `LogInfo`; the
three failure lines and header-off are `LogWarning`.

**Verify (RED)**: filter `FullyQualifiedName~XmlMergeServiceTests`: every test fails, by
`NotImplementedException` or by an assertion. Implement the service. **Verify (GREEN)**: all pass, then
run the RefAsm unit step: these tests and Steps 2 to 4's run and pass there (they touch no engine type).

### Step 6: the adapter on the real engine, and fixture equivalence (TDD)

Create `Main/Adapters/XmlMergeEngineAdapter.cs`, `public sealed class XmlMergeEngineAdapter : IXmlMergeEngineAdapter`
with a public parameterless constructor, first as a stub whose methods throw
`NotImplementedException` (its constructor does not). Its constructor
resolves `CreateDocumentFromXmlFile` with
`AccessTools.Method(typeof(MBObjectManager), CreateDocumentFromXmlFileName, new[] { typeof(string), typeof(string), typeof(bool) })`
into a cached `Func<string, string, bool, XmlDocument>` through `Delegate.CreateDelegate` (never
`MethodInfo.Invoke`), inside a `try`; on failure `BindingProblem` names the member. Constants for the
binding tests: `internal const string CreateDocumentFromXmlFileName = "CreateDocumentFromXmlFile";`
and `internal static readonly string[] WatchedMethodNames = { "ApplyXslt", "MergeTwoXmls", "ToXDocument", "ToXmlDocument" };`.
The public helpers are direct calls (`MBObjectManager.ToXDocument(document)`, `MBObjectManager.ToXmlDocument(document)`,
`MBObjectManager.MergeElements(element1, element2, xsdPath)`), each inside its own adapter method, so a
missing member fails when that method is compiled, inside the service's `try`. The constructor also
checks those three with `AccessTools.Method(typeof(MBObjectManager), name, <parameter types>)`
(`MergeElements`: `XElement, XElement, string`; `ToXDocument`: `XmlDocument`; `ToXmlDocument`:
`XDocument`) and, when one is null, sets `BindingProblem` to name it, so the header's
`engine bindings resolved (CreateDocumentFromXmlFile, MergeElements, ToXDocument, ToXmlDocument)` is
true when it is logged and a missing helper turns the fast path off before the first merge. `ForeignPatches()`
reads `Harmony.GetPatchInfo` for `CreateMergedXmlFile` and each watched method (resolve the
`MethodInfo`s once in the constructor) across the six collections, skipping the owners
`com.taom.mod` and `TAOM.Dependencies.Foundation.PatchShield`, and returns `owner kind MBObjectManager.Method`
strings by the rules in Design.

`XmlMergeFixtures.cs` writes a fixture set into a fresh temp folder (drafts; re-check each against
`XmlResource.ReadXsdFileAndExtractInformation` before relying on it: after reading `Things.xsd`, the
dictionary for its path must hold `/Things`, `/Things/Thing` with `UniqueAttributes` `["id"]`, and
`/Things/Thing/Part`). `Things.xsd`, written exactly as below (the XML declaration must stay the
file's first line; nothing may precede it):

```xml
<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="Things">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="Thing" minOccurs="0" maxOccurs="unbounded">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="Part" minOccurs="0" maxOccurs="unbounded">
                <xs:complexType>
                  <xs:attribute name="name" type="xs:string" use="optional" />
                  <xs:attribute name="value" type="xs:string" use="optional" />
                </xs:complexType>
              </xs:element>
            </xs:sequence>
            <xs:attribute name="id" type="xs:string" use="required" />
            <xs:attribute name="label" type="xs:string" use="optional" />
          </xs:complexType>
        </xs:element>
      </xs:sequence>
    </xs:complexType>
    <xs:unique name="UniqueThingId">
      <xs:selector xpath="Thing" />
      <xs:field xpath="@id" />
    </xs:unique>
  </xs:element>
</xs:schema>
```

`a.xml` `<Things><Thing id="a" label="first"><Part name="p1" value="1"/></Thing><Thing id="b"/></Things>`;
`b.xml` `<Things xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="Things.xsd"><!-- second file --><Thing id="a" label="second"><Part name="p2"/></Thing><Thing id="c"/></Things>`;
`c.xml` `<Things><Thing id="a" _replaceWhileMerging="true" label="replaced"><Part name="p9"/></Thing></Things>`;
`drop.xslt` (identity plus an empty template for `Thing[@id='b']`); `touch.xslt` (identity plus a
template for `Thing` that copies it and adds `<xsl:attribute name="touched">yes</xsl:attribute>`).

`XmlMergeEngineEquivalenceTests.cs`, class `[TestCategory("RequiresGame")]`: `[ClassInitialize]` writes
the fixtures, calls `XmlResource.ReadXsdFileAndExtractInformation(xsdPath)`, and asserts
`Harmony.GetPatchInfo` of `CreateMergedXmlFile` has no prefix (so the "engine" side is unpatched).
Each test builds the same two lists, runs `MBObjectManager.CreateMergedXmlFile(list, xslt, false)` and
the service's `MergeFast` with the real adapter, and calls `XmlMergeAssert.SameDocument` on the two
`OuterXml` strings:
`NoXslt_ThreeFiles_MatchesTheEngine` ([a, b, c]), `XsltBesideSecondFile_MatchesTheEngine`
([a, b], drop on b), `XsltOnlyEntryInTheMiddle_MatchesTheEngine` ([a, ("",""), b], drop on the middle),
`XsltIsTheLastStep_MatchesTheEngine` ([a, b, ("","")], touch last), `FirstEntryXslt_MatchesTheEngine`
([a, b], drop on index 0), `SingleEntry_MatchesTheEngine` ([a]), `EmptyXsdPath_MatchesTheEngine`
([a, (b, "")]), `ReplaceWhileMerging_MatchesTheEngine` ([a, c]; also assert the output keeps
`_replaceWhileMerging="true"`, the engine's own behaviour), `TwoXsltsInARow_MatchesTheEngine`
([a, ("",""), ("",""), b], drop then touch), `MissingFile_ThrowsTheSameExceptionTypeAsTheEngine`
(both sides throw; compare `GetType()`).

**Verify (RED)**: filter `FullyQualifiedName~XmlMergeEngineEquivalenceTests`: every test fails; the
nine document tests with `NotImplementedException` from the adapter stub, and
`MissingFile_ThrowsTheSameExceptionTypeAsTheEngine` by its type assertion (the engine side throws
`FileNotFoundException`, the stub `NotImplementedException`). Implement the adapter. **Verify (GREEN)**: all pass. A
`SameDocument` failure here is a STOP (Design is wrong somewhere); do not adjust the loop to the
fixture without understanding the difference, and report it.

### Step 7a: the live harness, the gate before any patch exists

`LiveMergeListBuilder.cs` (test helper) builds, for a game type and a module order, exactly the lists
`GetMergedXmlForManaged` builds (Current state), with the game folder from `GameAssemblies.GameDir`
and paths joined the engine's way (`<game>/Modules/<module>/` + `ModuleData/` + name + `.xml`,
folder files as `folder + "/" + fileInfo.Name`, `Replace(".xml", ".xsl")`, the `.xsl` then `.xslt`
probe, the `("", "")` entry for a node with neither file nor folder). A folder's files come from
`new DirectoryInfo(folder).GetFiles("*.xml")` in the order it returns them, never sorted (the
engine's own call). The XSD path of an entry is the module's own
`<game>/Modules/<module>/ModuleData/XmlSchemas/<id>.xsd` when that file exists, else the default
`<game>/XmlSchemas/<id>.xsd` (the engine's `GetXsdPathForModules` probe, `MBObjectManager.cs:884-888`;
no installed module ships one today, but a mod that does must not make the gate compare a list the
game never builds). It reads each module's `SubModule.xml` `Module/Xmls/XmlNode` elements (`XmlName`
`id` and `path`; under `IncludedGameTypes`, the `value` attribute of EVERY element child, whatever its
name, as `XmlResource.cs:291-303` does), keeps a node when its game-type list is empty or contains
the game type, and returns the distinct ids in first-seen order. It also lists every default XSD path
(`<game>/XmlSchemas/<id>.xsd`) that exists, for `ReadXsdFileAndExtractInformation`, the call the
engine makes per XmlNode at startup. Module order: the environment variable
`TAOM_XMLMERGE_MODULES` (semicolon-separated) when set, else
`TAOM.Dependencies;Native;SandBoxCore;CustomBattle;SandBox;StoryMode;BirthAndDeath;FastMode;LOTRLOME_Armory;TAOM_Map;TAOM`,
skipping a module whose folder is absent.

`XmlMergeLiveEquivalenceTests.cs`, class `[TestCategory("RequiresGame")]` and `[TestCategory("LiveInstall")]`,
`Assert.Inconclusive` when `GameAssemblies.EnsureLoaded()` is false or the game folder has no
`Modules/TAOM/SubModule.xml`. Same patch-info check as Step 6. Two tests,
`EveryCampaignType_FastMergeMatchesTheEngine` (`Campaign`) and
`EveryCustomBattleType_FastMergeMatchesTheEngine` (`CustomGame`), sharing one service and one real
adapter (so whichever of the two runs second sees XSLT cache hits, as a second load in one process
does; MSTest does not fix the method order). For
each id: time `MBObjectManager.CreateMergedXmlFile(list, xslt, false)` and then `MergeFast` with a
`Stopwatch`; if the engine throws, assert the fast path throws the same exception type and record it;
else `XmlMergeAssert.SameDocument`. Collect all mismatches and fail once at the end listing them, so
one run reports every type. Print a table through `TestContext.WriteLine`: id, entries, files, XSLTs,
engine ms, fast ms, identical, and the totals for NPCCharacters, Items, EquipmentRosters and GameText.
The engine runs first for each type, so the fast path reads files the OS has just cached; that favours
the fast side slightly and is acceptable for a 50% bar (the prototype reached 27 to 35%).

**Verify**: run the class with the `One class with its printed table` command. Both tests pass, and the
printed table shows, for `Campaign`:
- NPCCharacters 57 entries (56 files, 1 XSLT), Items 142 entries, EquipmentRosters 31 entries,
  GameText 33 entries with 3 XSLTs. A different count is not a STOP by itself: explain it from the live
  `SubModule.xml` files or folders (a node added or removed since 2026-10-02) in your report;
- engine times within a factor of two of the offline figures in Current state (NPCCharacters 5.4 to
  5.6 s, Items 2.4 s, EquipmentRosters 0.55 to 0.59 s, GameText 0.38 s);
- the fast total for those four types at most 50% of their engine total (the prototype reached 27 to 35%).
Quote the table in your report with the machine, and state the in-game rgl figures beside it (the
engine path in game is slower than offline by roughly two to three times; the harness is the
baseline the fix is judged on, not a prediction of the in-game seconds). Then commit nothing yet.

### Step 7b: the patch, the module and the wiring

Read the lessons named in Current state first. The order, each RED quoted in your report before the
line that turns it green:

1. Create `TAOM.Tests/Features/XmlMerge/XmlMergeWiringTests.cs` (no category; exemplar
   `TAOM.Tests/Features/BattleCorpses/BattleCorpsesWiringTests.cs`) with
   `FeatureModules_ListTheXmlMergeModuleOnce` (`FeatureModules.All.OfType<XmlMergeModule>().Count()` is 1),
   `Module_DeclaresTheCategoryAtProcessLoad` (one `PatchCategoryDecl`, `Patch99_XmlMergeFastPath`,
   `ApplyPhase.ProcessLoad`) and `Module_RegistersAServiceTheContainerCanBuild`: a fresh
   `new Container()`, `RegisterInstance(Substitute.For<IModLogger>())`, `new XmlMergeModule().RegisterServices(container)`,
   then `container.RegisterInstance<IXmlMergeEngineAdapter>(new RecordingXmlMergeEngine(), IfAlreadyRegistered.Replace)`
   (exemplar: `FactionUIWiringTests.cs:47`; the real adapter's constructor reflects on the engine) and
   assert `container.Resolve<XmlMergeService>()` is not null and is the same instance twice. Write the
   module (below) and the patch class first so the file compiles, but NOT the `FeatureModules.All`
   line. **Verify (RED)**: filter `FullyQualifiedName~XmlMergeWiringTests`:
   `FeatureModules_ListTheXmlMergeModuleOnce` fails (0, expected 1); the other two pass. If
   `Module_RegistersAServiceTheContainerCanBuild` fails on a constructor, fix the visibility (Step 5),
   never the test.
2. With the patch class in place, filter `FullyQualifiedName~CoopVetoClassificationTests`:
   **Verify (RED)**: `EveryBoolPrefix_HasACoopDisposition` fails and its message lists
   `MBObjectManager_CreateMergedXmlFile_Patch`.
3. Add the `FeatureModules.All` line and the `CoopVetoClassificationTests` registry entry (below);
   both classes pass.
4. The `SubModule.cs` line, the binding tests and the `ReflectionSiteBindingTests` row (below), then
   this step's Verify.

`Main/Features/XmlMerge/Hooks/MBObjectManager_CreateMergedXmlFile_Patch.cs` (under 80 lines), the
target shape:

```csharp
[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.CreateMergedXmlFile))]
[HarmonyPatchCategory(XmlMergeModule.PatchCategory)]
public static class MBObjectManager_CreateMergedXmlFile_Patch
{
    private static XmlMergeService? _service;

    public static void Initialize(XmlMergeService service) => _service = service;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    public static bool Prefix(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation,
                              ref XmlDocument __result, out XmlMergeCall __state)
    {
        __state = new XmlMergeCall();
        try
        {
            var service = _service;
            if (service == null || !service.TryMergeFast(toBeMerged, xsltList, skipValidation, __state, out var merged))
                return true;
            __result = merged!;
            return false;
        }
        catch
        {
            return true;   // TryMergeFast never throws; this guards the call itself
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, XmlMergeCall __state)
    {
        try { _service?.OnOriginalFinished(__state, __exception); }
        catch { /* a log line must never change the engine's outcome */ }
    }

    /// <summary>Called from SubModule.OnGameInitializationFinished at every game init; never throws.</summary>
    public static void LogWindowSummary(string? gameType)
    {
        try { _service?.LogWindowSummary(gameType); }
        catch { /* same */ }
    }
}
```

Its class comment states, in prose, the responsibilities table from Design, the callers and call rate,
why returning `false` is safe (byte-identical gate, stand-aside rule) and why returning `true` on an
exception is safe (the original then runs on untouched arguments; what the engine does there is the
unpatched game's outcome).

`Main/Features/XmlMerge/XmlMergeModule.cs`, `internal sealed class XmlMergeModule : TaomFeatureModule`:
`internal const string PatchCategory = "Patch99_XmlMergeFastPath";`; `Id => "XmlMerge"`;
`RegisterServices` registers `IXmlMergeEngineAdapter` to `XmlMergeEngineAdapter`, `XsltTransformCache`
and `XmlMergeService`, all `Reuse.Singleton`; `InitializeStatics` resolves the service, passes it to
`MBObjectManager_CreateMergedXmlFile_Patch.Initialize` and calls `LogConfigurationHeader()`;
`PatchCategories` returns `new PatchCategoryDecl(PatchCategory, ApplyPhase.ProcessLoad)`. Its class
comment says why ProcessLoad (the merges of a game precede the late batch).

`Main/Composition/FeatureModules.cs`: append after the `TournamentRewardsModule` line
`        new Features.XmlMerge.XmlMergeModule(),`.

`Main/SubModule.cs`, the one edit, inserted after the `ApplyGating(game?.GameType is Campaign);` line
and its blank line, before the `// Harmony patches are process-global` comment:

```csharp
        // [XmlMerge] summary (docs/features/xml-merge-fast-path.md): one line per game for the module-XML merges
        // since the previous one. Every game init, before the once-per-process guard: Campaign.OnInitialize and
        // CustomGame.OnInitialize call this after their XML loads. The hook never throws.
        Features.XmlMerge.Hooks.MBObjectManager_CreateMergedXmlFile_Patch.LogWindowSummary(game?.GameType?.GameTypeStringId);

```

`TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`: add after the
`["Patch96_TournamentJoinChoices"]` entry:

```csharp
        ["MBObjectManager_CreateMergedXmlFile_Patch"] = new(CoopVeto.ReviewedSafe,
            "Patch99: replaces the engine's module-XML merge loop with one proven byte-identical on every " +
            "type of the live install. Each peer merges its own module files during game init, before any " +
            "session state exists, and gets the document the engine would have built; it stands aside for " +
            "foreign patches and re-runs the engine's own merge on any exception."),
```

`XmlMergeBindingTests.cs` (exemplar `Patch80KingdomVoteDeadlockBindingTests`), methods tagged
`BindingVerification`:
`CreateMergedXmlFile_IsPublicStaticWithThePrefixesParameterNames` (`toBeMerged`, `xsltList`,
`skipValidation`; types `List<Tuple<string,string>>`, `List<string>`, `bool`; returns `XmlDocument`),
`CreateDocumentFromXmlFile_ResolvesAsPrivateStaticWithTheAdaptersSignature` (uses the adapter's
constant), `PublicHelpers_ResolveWithTheAdaptersSignatures` (`MergeElements(XElement, XElement, string)`,
`ToXDocument(XmlDocument)`, `ToXmlDocument(XDocument)`), `WatchedMethods_AllResolve` (each name in
`WatchedMethodNames`), `XsdTables_AreThePublicStaticsTheHarnessUses`
(`XmlResource.ReadXsdFileAndExtractInformation(string)`, `XmlResource.XsdElementDictionary`).
Tagged `BindingVerification` and `RequiresGameIL`, through `IlCallScanner.ExtractCalledMethods`:
`CreateMergedXmlFile_CallsExactlyTheThreeHelpers` (the set of `MBObjectManager` methods it calls is
exactly `CreateDocumentFromXmlFile`, `ApplyXslt`, `MergeTwoXmls`) and
`MergeTwoXmls_ConvertsMergesAndConvertsBack` (its `MBObjectManager` calls are exactly `ToXDocument`,
`MergeElements`, `ToXmlDocument`). A red IL test after an engine update means the merge algorithm
changed: the fast path must be re-proven before the patch ships again (the test's message says so).
`Adapter_OnInstalledEngine_HasNoBindingProblem` (`RequiresGame`).

`ReflectionSiteBindingTests.cs`: add after the last row
`[DataRow("TaleWorlds.ObjectSystem.MBObjectManager", "MBObjectManager", "CreateDocumentFromXmlFile", "Method", "XmlMergeEngineAdapter.cs:<line>")]`
with a `// --- XmlMerge (plan 042) ...` comment line naming what a miss degrades (the fast path turns
itself off; every merge runs the engine's code).

**Verify**: build (exit 0). Then run, each with its filter: `XmlMergeBindingTests` (all pass),
`XmlMergeWiringTests` (3 passed), `FeatureModulesTests` (all pass), `CoopVetoClassificationTests`
(all pass), `HarmonyPatchBindingTests` and `ReflectionSiteBindingTests` (all pass). Then
`git grep --untracked -n "Patch99_XmlMergeFastPath" -- Main` prints the module constant and nothing in
`Main/SubModule.cs` (`--untracked` because nothing is committed until Step 9: a plain `git grep`
does not see the new, untracked files).

### Step 8: docs

- `docs/features/xml-merge-fast-path.md` from `docs/features/TEMPLATE.md`: what the merge costs and
  why (Current state's numbers), how the fast path works (the Design, the responsibilities table, the
  stand-aside and failure rules), the harness and how to run it (`One class with its printed table`
  on `XmlMergeLiveEquivalenceTests`; the `TAOM_XMLMERGE_MODULES` override), a **Log lines** section
  listing every line of the Design table with its fields and one example each (D6), the two levers left
  to the maintainer (Maintenance notes), and the GitHub issue line (the orchestrator gives the number;
  leave `#TBD` if it is not filed yet and say so in your report).
- `docs/reference/feature-map.md`: a `| XmlMerge | ... |` row after the PreloadBodyGuard row (Patch99,
  `Main/Features/XmlMerge/`, one sentence, the doc link).
- `docs/reference/harmony-patch-registry.md`: `## Patch99_XmlMergeFastPath` after the Patch96 section
  and before the backlinks block: **Target** line (`MBObjectManager.CreateMergedXmlFile(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation)`,
  prefix that may skip the original at `Priority.Last`, void finalizer), then a paragraph: what it
  replaces and why, the responsibilities, callers and call rate, ProcessLoad through `XmlMergeModule`,
  the stand-aside and fallback rules, the binding and IL tests, the harness as the gate, **ACTIVE**.
- `docs/reference/taleworlds-api-snapshot/reflection-sites.md`: one row for `CreateDocumentFromXmlFile`
  at the end of the "Category B" table (after the `ButtonWidget` | `HandleClick` row, :87 at
  `0912e1b7`), in that table's columns, and a new dated line above the newest `Status (...)` line
  (:89 at `0912e1b7`, newest first), in their shape: "Status (<today's date>): XmlMerge (plan 042)
  adds one row (`MBObjectManager.CreateDocumentFromXmlFile`); it resolves against installed v1.5.3
  (`ReflectionSiteBindingTests` N/N with the binding-gate runsettings).", N being your own run's count.
- `docs/reference/engine/object-system-mbobjectmanager.md`: under the `LoadXML` section, a short
  paragraph "The merge loop (v1.5.3)": `GetMergedXmlForManaged` builds the per-type list, and
  `CreateMergedXmlFile` loads, validates, transforms and merges one file at a time, round-tripping the
  whole document per file (quadratic in the merged size); TAOM's `Patch99_XmlMergeFastPath` keeps one
  `XDocument` instead (link the feature doc).
Every sentence that states an engine fact is re-checked against the excerpts in Current state; no em
or en dash.

**Verify**: `python -B tools/lint_docs.py --fail-on-drift` exits 0.

### Step 9: full verification and the Stage 1 commit

Run the RefAsm unit step and the RefAsm binding gate, then the build and the full `Tests` command
(not `--no-build`), then the live harness class once more and quote its table.

**Verify**: build exit 0; `Tests` prints the baseline failure set plus only new passing tests
(`Failed: 1`, the same name); the RefAsm steps show Step 1's failure names only and no skipped
binding check; the harness passes. To prove the new non-engine tests ran under reference assemblies,
right after the RefAsm build run
`env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~XmlMerge&TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"`:
it prints `Passed! - Failed: 0, Passed: 47` (Step 2's 3, Step 3's 9, Step 4's 5, Step 5's 27,
Step 7b's 3 wiring tests; Stage 2's Step 12 adds `XmlSchemaSetCacheTests`' 3, so 50). Nothing else in
the repo matches `XmlMerge` at `0912e1b7`. A different count names the missing or extra tests in your
report. `git status --porcelain`
lists only Scope files. Commit Stage 1 (Git workflow), quoting the harness table's four heavy types
in the body.

### Step 10: decide Stage 2 (schema cache) by measurement, before writing it

Stage 2 removes the per-file `XmlSchemaSet` build (one parse, two compiles, the injection and a
reprocess per validated file). Measure what that costs before adding about 35 lines that mirror the
engine. In your scratch folder, write a probe script run with Windows PowerShell 5.1
(`powershell.exe -NoProfile -File <probe>.ps1`, the .NET Framework CLR the game uses). Load the engine
the way the run's prototype did (`plans/_audit/2026-10-02-perf/evidence/load/merge_bench.ps1:21-40`,
shown here with the game folder taken from the environment):

```powershell
$bin = Join-Path $env:BANNERLORD_GAME_DIR "bin\Win64_Shipping_Client"
[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $e)
    $n = (New-Object Reflection.AssemblyName $e.Name).Name
    $p = Join-Path $bin "$n.dll"
    if (Test-Path -LiteralPath $p) { return [Reflection.Assembly]::LoadFrom($p) }
    return $null
})
Add-Type -AssemblyName System.Xml.Linq
$null = [Reflection.Assembly]::LoadFrom((Join-Path $bin "TaleWorlds.Library.dll"))
$objAsm = [Reflection.Assembly]::LoadFrom((Join-Path $bin "TaleWorlds.ObjectSystem.dll"))
$mbom = $objAsm.GetType("TaleWorlds.ObjectSystem.MBObjectManager", $true)
$flags = [Reflection.BindingFlags]"Static, Public, NonPublic"
$inject = $mbom.GetMethod("InjectOptionalAttrToAllComplexTypes", $flags)
```

For `XmlSchemas/NPCCharacters.xsd`, `Items.xsd`, `EquipmentRosters.xsd` and
`GameText.xsd`: time 20 builds of the engine's set (new `XmlSchemaSet`, `Add(null, new XmlTextReader(new StreamReader(xsd)))`,
`Compile()`, invoke the private `InjectOptionalAttrToAllComplexTypes(set, "boolean", "_replaceWhileMerging")`,
`Reprocess` each schema, `Compile()`), and 20 creations of the two `XmlReaderSettings` with
`Schemas.Add(cachedSet)`. Predicted saving per type = (build ms minus settings ms) times (validated
files of that type minus 1), using Step 7a's file counts.

**Decision**: if the predicted saving over the four types is at least 300 ms, do Step 11. Otherwise
skip Steps 11 and 12, and report the probe's numbers as the reason (the simplicity criterion: under
300 ms per load does not pay for an engine mirror). Either way the probe is not committed.

### Step 11: Stage 2, the schema cache (TDD; only if Step 10 says so)

- `Main/Features/XmlMerge/XmlSchemaSetCache.cs`: `internal sealed class XmlSchemaSetCache` with
  `XmlSchemaSet? GetOrBuild(string xsdPath, Func<string, XmlSchemaSet?> build)`, keyed by the path
  string and validated by the XSD's last-write time and length; a `null` build (any exception while
  building) is never cached; `Builds` and `CacheHits` counters. Tests first,
  `XmlSchemaSetCacheTests.cs` (no category): `GetOrBuild_SamePathTwice_BuildsOnce`,
  `GetOrBuild_FileRewrittenWithNewTime_Rebuilds`, `GetOrBuild_BuildReturnsNull_IsNotCachedAndRetries`.
- Adapter: `XmlSchemaSet? TryBuildSchemaSet(string xsdPath)` mirrors `LoadXmlWithValidation`'s set
  construction and overlay (Current state) without its `Debug.Print`s, binding the private
  `InjectOptionalAttrToAllComplexTypes` to a cached `Action<XmlSchemaSet, string, string>`; it returns
  `null` if anything throws (that file then takes the engine's own `CreateDocumentFromXmlFile`, which
  prints every message the engine prints). `XmlDocument CreateValidatedDocument(string xmlPath, string xsdPath, XmlSchemaSet set)`
  mirrors `CreateDocumentFromXmlFile` plus the reader half of `LoadXmlWithValidation` line for line:
  `Debug.Print("opening " + xmlPath)`, the `StreamReader` open and `ReadToEnd` (so a missing or locked
  file throws where the engine throws), `Debug.Print("opening " + xsdPath)`, the two settings and loads
  with `Schemas.Add(set)` and the engine's private `ValidationEventHandler` (bound once to a
  `ValidationEventHandler` delegate), the `catch (Exception) { _ = new Uri(xmlDocument.BaseURI).LocalPath; }`,
  and `streamReader.Close()`. `BindingProblem` also covers the two new private members; when they do
  not resolve, Stage 2 stays off and Stage 1 runs unchanged (header says `schema cache off`).
- Service: `Load` uses the cache when validating; `schema_builds` and `schema_cache_hits` come from it;
  the header says `schema cache on`; `WatchedMethodNames` gains `CreateDocumentFromXmlFile` and
  `LoadXmlWithValidation`, and the stand-aside test covers them.
- Fixture equivalence: add `Things2.xsd`, a copy of `Things.xsd` whose `label` attribute carries
  `default="none"`, and `SchemaDefaultAttribute_MatchesTheEngine` over [a, b] with it (a validating
  reader can add schema defaults, so this proves the cached set gives the engine's document); run every
  fixture test again.
- Binding: `ReflectionSiteBindingTests` rows for `InjectOptionalAttrToAllComplexTypes` and
  `ValidationEventHandler`, catalogue rows in `reflection-sites.md`, and an IL test
  `LoadXmlWithValidation_StillBuildsInjectsAndReprocesses` (its calls include `XmlSchemaSet.Compile`,
  `InjectOptionalAttrToAllComplexTypes`, `XmlSchemaSet.Reprocess`).
- Update the pinned header test (`schema cache on`), the docs' log and design sections, and the registry
  section.

**Verify (RED then GREEN)** for each new test class as in earlier steps. Then the live harness: both
tests pass, and the fast total for the four heavy types is lower than Step 9's by at least half the
Step 10 prediction. If it is not, do not commit Stage 2: report both numbers (the edits stay
uncommitted in your worktree for the reviewer; do not revert them with git).

### Step 12: full verification and the Stage 2 commit (only after Step 11)

Same as Step 9, then commit Stage 2 with the before and after numbers in the body.

### Step 13: the report

Report: Step 1's base; every RED and GREEN line; Step 7a's and the final harness tables with the
machine; the Step 10 probe numbers and the decision; the commits; and, for the maintainer, the
`lords.xslt` facts from Maintenance notes with the harness's `xslt_ms` for NPCCharacters (the fast
line's field, read from the harness run or computed there), so the decision has a current number.

## Test plan

- `XmlMergeAssertTests` (no category): the comparison helper can fail.
- `XmlMergeLinesTests` (no category): every log format, literally, and invariant rounding.
- `XsltTransformCacheTests` (no category): compile once, recompile on change, the drop template, the
  shared name table, a missing stylesheet throws.
- `XmlMergeServiceTests` (no category, hand-written fake): one test per decision reason (input x branch),
  the exact call sequence for every loop branch of `CreateMergedXmlFile`, the once-only reason lines,
  the session disable and its negative case, the summary and its reset, the header both ways, the log
  levels.
- `XmlMergeEngineEquivalenceTests` (`RequiresGame`): byte equality with the engine's own
  `CreateMergedXmlFile` on fixtures for each branch: no XSLT, XSLT beside a file, XSLT-only entry,
  XSLT as the last step, the ignored first XSLT, a single entry, an empty XSD path, `_replaceWhileMerging`,
  two XSLTs in a row, a missing file (Stage 2: a schema default attribute).
- `XmlMergeLiveEquivalenceTests` (`RequiresGame`, `LiveInstall`): every type of the live module set, for
  `Campaign` and `CustomGame`; the gate and the baseline timing table.
- `XmlMergeBindingTests` (`BindingVerification`; the IL ones also `RequiresGameIL`): the target's
  signature and parameter names, every engine member the adapter reaches, and the shape of the
  engine's loop.
- `XmlMergeWiringTests` (no category): the module is listed once, declares its category at
  ProcessLoad, and a fresh DryIoc `Container` can build the service (the public-constructor guard).
- Existing suites that must stay green and that this plan feeds: `FeatureModulesTests`,
  `CoopVetoClassificationTests`, `HarmonyPatchBindingTests`, `ReflectionSiteBindingTests`.
- Model after: `TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs` (bindings),
  `TAOM.Tests/Core/LordFamilyTransformTests.cs` (a `LiveInstall` class that skips without the game).
- Not testable here, for the `Not-tested:` trailer: Harmony applying `Patch99_XmlMergeFastPath` and the
  prefix and finalizer running inside the game; the engine's rgl `opening` lines (`Debug.Print` is a
  no-op without the game's `DebugManager`); the per-game summary call from `SubModule`.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] The build command exits 0.
- [ ] The test command shows the baseline failure set (`EveryLanguage_DeclaresARowForEveryEnglishKey`
      only) and every new test passing.
- [ ] `XmlMergeLiveEquivalenceTests` passes for both game types on the live install, its table is in the
      report, and the four heavy types' fast total is at most 50% of their engine total.
- [ ] The RefAsm unit step and binding gate fail only on Step 1's names, with no skipped binding check.
- [ ] `git grep --untracked -n "Patch99_XmlMergeFastPath" -- Main` lists the constant in
      `XmlMergeModule.cs` (prose mentions elsewhere are fine) and no line in `Main/SubModule.cs`
      (`--untracked` so the check also holds in a run that stopped before its commit);
      `git grep -n "XmlMergeModule" -- Main/Composition/FeatureModules.cs`
      prints one line; `git grep -n "LogWindowSummary" -- Main/SubModule.cs` prints one line, above
      `if (_gameInitPatchesApplied) return;`.
- [ ] `git grep -n "MBObjectManager_CreateMergedXmlFile_Patch" -- TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`
      prints one line.
- [ ] `git grep -n "## Patch99_XmlMergeFastPath" -- docs/reference/harmony-patch-registry.md` and
      `git grep -n "xml-merge-fast-path.md" -- docs/reference/feature-map.md` print one line each.
- [ ] `python -B tools/lint_docs.py --fail-on-drift` exits 0.
- [ ] No line this plan ADDS holds U+2013 or U+2014. Existing lines are exempt: at `0912e1b7`,
      `Main/SubModule.cs`, `CoopVetoClassificationTests.cs`, `ReflectionSiteBindingTests.cs`,
      `harmony-patch-registry.md`, `feature-map.md`, `reflection-sites.md` and
      `object-system-mbobjectmanager.md` already hold such dashes, and this plan rewrites none of them.
      Write this script to your scratch folder as `dash_check.py` and run
      `python -B <scratch>/dash_check.py 0912e1b7 <every in-scope path from the drift check>`; it
      scans the `+` lines of `git diff -U0 0912e1b7` (committed and uncommitted) plus every untracked
      in-scope file whole. Quote its empty output.

      ```python
      import subprocess, sys
      sys.stdout.reconfigure(encoding="utf-8")
      base, paths = sys.argv[1], sys.argv[2:]
      DASHES = (chr(0x2013), chr(0x2014))
      diff = subprocess.run(["git", "diff", "-U0", base, "--"] + paths,
                            capture_output=True, encoding="utf-8", check=True).stdout
      current = None
      for line in diff.splitlines():
          if line.startswith("+++ "):
              current = line[4:]
          elif line.startswith("+") and any(d in line for d in DASHES):
              print(f"{current}: {line[1:]}")
      untracked = subprocess.run(["git", "ls-files", "--others", "--exclude-standard", "--"] + paths,
                                 capture_output=True, encoding="utf-8", check=True).stdout.split()
      for path in untracked:
          with open(path, encoding="utf-8") as f:
              for n, text in enumerate(f, 1):
                  if any(d in text for d in DASHES):
                      print(f"{path}:{n}: {text.rstrip()}")
      ```
- [ ] `git status --porcelain` lists only in-scope files; one commit for Stage 1, and one for Stage 2
      only if Step 11 met its bar.
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes.

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts, the pinned version is not
  `v1.5.3`, or `TaleWorlds.ObjectSystem.dll` is not 61,792 bytes.
- `Patch99_` is already used in `Main`, `Dependencies`, `TAOM.Tests` or `docs`: report it, do not
  renumber.
- The harness cannot reproduce the engine's merge for a type: an engine time outside a factor of two of
  the offline figures, a list count you cannot explain from the live `SubModule.xml` files, or an
  engine exception for a type the game loads without one. Stop before Step 7b.
- Any byte difference in Step 6 or Step 7a. Do not write the patch class; report the type, the index
  and both contexts.
- The fast total for the four heavy types is above 50% of the engine total.
- `MergeElements` or the private `CreateDocumentFromXmlFile` cannot be reached without copying engine
  code (a reflection binding that fails, a member that is not static, a signature that differs). Say
  how many engine lines a copy would take (`MergeElements` and `MergeElementAttributes` are about 70;
  `CreateDocumentFromXmlFile` with `LoadXmlWithValidation` about 85, plus 130 for the injection).
- An IL test shows `CreateMergedXmlFile` or `MergeTwoXmls` calling other `MBObjectManager` methods
  than this plan lists: the engine's algorithm changed under the plan.
- The fix seems to need `Main/IoC.cs`, a csproj, `Directory.Build.props`, a settings file or an ADR,
  or any SubModule edit other than the one in Scope.
- A test outside this plan's new ones fails that is not in Step 1's base.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (title along the lines of "Load times: the engine's module-XML merge
  costs 15 to 28 s per load; merge in one document, byte-identical") and give the executor its number
  for the feature doc and the commit body.
- No `/localize`: log lines are not player-facing text and this plan adds no MCM setting.
- After review, `/verify-bindings` to refresh `docs/reference/taleworlds-api-snapshot/patch-targets.md`
  (one new target, `MBObjectManager.CreateMergedXmlFile`).
- Plan 040 (`plans/040-load-time-stamps.md`) already accounts for this plan: its Patch numbering
  note records that 028, 041 and 042 reserve `Patch97` to `Patch99`, and its Status says 042 is judged
  by its stamp (1). Nothing to tell its writer; `[XmlMerge]` lines time the merge part of each
  `LoadXML`, so 040's stamp measures merge plus object creation.
- Add the three maintainer decisions in Maintenance notes to the run's FOR-MIKE list.

## After merge: the maintainer's actions

- Pull, build and deploy as usual, then start one new campaign and one custom battle. In
  `taom_debug.log` expect, at boot, `[XmlMerge] fast path ready: ...` (or `fast path off: <reason>`);
  during each load one `[XmlMerge] type=... path=fast ...` line per validated type plus
  `path=vanilla reason=skip-validation` lines for SPCultures, Monsters and similar unvalidated merges;
  then `[XmlMerge] summary game=Campaign ...` (and `game=CustomGame` for the battle). Any
  `stands aside`, `fast path failed` or `disabled for this session` line is worth a report with the log.
- Compare the loading screen with the 2026-10-02 baseline (new campaign 50.2 s, of which about 28 s
  merging; custom battle about 14 s of merging): the summary's `ms` is the new merge total. The rgl log
  should still show `opening <file>` and `opening <xsd>` for every file, as before.

## Maintenance notes

- **An engine update** can change the merge. `XmlMergeBindingTests`' IL tests go red if
  `CreateMergedXmlFile` or `MergeTwoXmls` call different helpers; then re-run
  `XmlMergeLiveEquivalenceTests` before shipping (the `/engine-bump` flow runs the binding suite).
  The `XsdElementDictionary` contract and the `_replaceWhileMerging` injection are also engine-owned.
- **What review should probe**: the order of calls in the loop against the engine (load next, convert
  accumulated, convert next, merge); that `xsltList[0]` is skipped; the `!= ""` comparisons (a `null`
  entry must fail the way the engine fails, via the fallback); that the finalizer is `void`; that no
  per-frame path logs at INFO; the stand-aside owner filter (TAOM's own id and PatchShield's only); the
  once-only bookkeeping under concurrent calls (the lock); the summary line sitting before the
  once-per-process guard.
- **Decisions this plan does not take (for the maintainer)**:
  1. `lords.xslt` (about 1.2 s of the remaining NPCCharacters merge offline, the transform not the
     compile; 1,515,721 bytes, 396 templates generated by `tools/complete_lords_xslt.py`). It is not a
     plain override: its XmlNode (`Main/_Module/SubModule.xml:120`, `id="NPCCharacters" path="lords"`)
     has no `lords.xml`, so it rewrites the vanilla and SandBox lords merged before it; each template
     sets the lord's attributes afresh (`xsl:copy` drops the originals) and replaces `face`, `skills`,
     `Traits` and `Equipments` while keeping the lord's other children
     (`apply-templates select="node()[not(self::face or self::skills or self::Traits or self::Equipments)]"`).
     Replacing it with an XML file would need `_replaceWhileMerging` on every lord (which the engine
     copies into the merged output, so equality holds only with the marker removed) and the vanilla
     children baked in, regenerated from the XSLT's own output at every engine update. About 1.2 s per
     load against a regeneration step.
  2. The second lever, fewer files: a build step that concatenates TAOM's per-culture files of one type
     into one generated file in the deployed module (sources stay per culture in the repo;
     `SubModule.xml` names the generated file), proven by the same harness. With the fast path, a
     file's cost no longer grows with what was merged before it, so this lever is worth much less than
     before; the live Armory's item files are the maintainer's and unversioned.
  3. A player-facing kill switch. This plan adds none (the fast path turns itself off on its own fault
     and stands aside for other mods); an MCM toggle would also move the pinned settings counts in
     `SettingsFingerprintTests` and the co-op relevance classification.
- **Follow-ups deferred**: the `Debug.Print` lines are not testable offline; if a future change touches
  the mirror in Stage 2, an rgl check in game is the only proof that the `opening` lines are unchanged.
