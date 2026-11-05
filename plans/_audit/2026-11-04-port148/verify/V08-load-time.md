# V08: campaign load time on v1.4.8 (LOAD-TIME-01, LOAD-TIME-07), adversarial verification

Run 2026-11-04-port148, verifier V08, read-only. Subject: `plans/_audit/2026-11-04-port148/deepdive/load-time.md`
findings LOAD-TIME-01 and LOAD-TIME-07.

Refs used: working tree = c5b84fb41; origin/bannerlord-1.4.5 = 1b00d1acd (the 1.4.x tip); the public build
origin/fix/public-armory-ids-145 (8df90444c); origin/bannerlord-1.5.x = e45f6df2c. Engine: the installed v1.4.8
DLLs (`pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, cache `C:\Users\mikew\.taom-src\v1.4.8\`) and the dump
`C:\Decompiled_Bannerlord\_categories_v1.4.8` (plus `_modules_build` for CustomBattle). No 1.5.x decompile exists on
this laptop, so every 1.5.x engine fact below was checked against the excerpts plan 042 quotes
(`git show origin/bannerlord-1.5.x:plans/042-xml-merge-load-time.md`, written from installed v1.5.3).

Scratch tools written for this check (scratchpad, not committed): `v08_tools/merge_spans.py` (per-type spans from
the rgl `opening` lines), `npc_file_gaps.py`, `dup_ids.py`, `xmlnodes.py`, `il_tokens.py`.

## Verdict

PARTLY-CONFIRMED, high confidence on everything marked verified.

- LOAD-TIME-01: the mechanism, the call sites and the E1/E2 per-type figures reproduce exactly. Corrections: E2's
  later-load windows are 22.6 to 29.7 s (not 22.6 to 24.0 s); E2 Items spans are 6.3 to 7.6 s (not 7.7 to 9.4 s);
  E3/E4's "about 37 s of XML" is a 39.1 s window that also holds non-XML work, so it is an upper bound; the file is
  read three times, not twice; the per-file cost has a floor near 0.18 s but own size still adds (not "whatever its own
  size"); the "+4 line shift" from v1.5.3 starts inside `MergeElements`, which therefore differs between the engines;
  the port needs PatchShield machinery that does not exist on 1.4.x (method-level exclusions arrived on 1.5.x in
  30abf550b, not in plans 007 or 040); 43ca92ee9 carries nothing the port needs; plan 009 is not a prerequisite.
- LOAD-TIME-07: the per-module figures reproduce and joining TAOM's files is semantically safe today, but the
  game-type filter counts are wrong, and "plan 042 leaves it to the maintainer" is stale: on 1.5.x the maintainer
  decided D14 (2026-10-03): no build-step merge. After plan 042 the lever is worth well under 0.5 s per load.

## 1. The engine merge on v1.4.8 against the code plan 042 replaced

### Callers (v1.4.8)

| Caller | Location | Validation |
|---|---|---|
| `MBObjectManager.LoadXML(id, isDevelopment, gameType, ...)` | `MBObjectManager.cs:786-797`; `GetMergedXmlForManaged` at :789 sits OUTSIDE the `try`; :790-796 wraps `LoadXml` in a catch-all that swallows every exception | `skipValidation: false` |
| `MBObjectManagerExtensions.LoadXML` | `_categories_v1.4.8\Core\TaleWorlds.Core\TaleWorlds.Core\MBObjectManagerExtensions.cs:10-17` passes `Game.Current.GameType.GameTypeStringId` | via LoadXML |
| Saved campaign | installed `TaleWorlds.CampaignSystem.Campaign` `OnInitialize` :1366-1453: :1396-1399 `InitializeDefaultCampaignObjects` (body :1466-1494: `LoadBasicFiles`, then `Items`, `EquipmentRosters`, `partyTemplates` at :1471-1473); :1410 `InitializeBasicObjectXmls` (`SPCultures`, `Concepts`, :1460-1464); :1415 `SandBoxManager.OnCampaignStart` into `InitializeSandboxXMLs` (`SandBoxManager.cs:360-382`: `NPCCharacters`; `Heroes`, `Kingdoms`, `Factions` only when not a saved campaign; `WorkshopTypes`, `LocationComplexTemplates`, `Settlements`); :1452 `OnGameInitializationFinished` | via LoadXML |
| Custom battle | `_modules_build\CustomBattle__TaleWorlds.MountAndBlade.CustomBattle.cs:4054-4076` (`LoadCustomGameXmls` :4140-4146: Items, EquipmentRosters, NPCCharacters, SPCultures; then `OnGameInitializationFinished` :4076) | via LoadXML |
| GameText, BannerIcons | `GameTextManager.cs:117`, `BannerManager.cs:198` call `GetMergedXmlForManaged(..., skipValidation: false, ...)` directly | validated |
| Unvalidated | `GetMergedXmlForNative` (`MBObjectManager.cs:920-947`, `soln_*`), `ManagedParameters.cs:27` (CoreParameters), CustomBattle `CustomBattleScenes` (:4136), TAOM's `Main/Adapters/ObjectManagerAdapter.cs:108-109` (SPCultures, origin/bannerlord-1.4.5) | `skipValidation: true` |

The dump file and the installed-DLL decompile of `MBObjectManager` are identical (a CR-stripped `diff` shows only
the ilspycmd version trailer the cache appends). The installed `TaleWorlds.ObjectSystem.dll` is 59,864 bytes;
`.claude/pinned-game-version.txt` reads `v1.4.8`.

### The quadratic step (v1.4.8, `MBObjectManager.cs:966-982` and :997-1010, quoted verbatim)

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

For every file after the first, `MergeTwoXmls` converts the WHOLE document merged so far to `XDocument` (:999) and
back (:1009), and `MergeElements` rebuilds its id dictionary over every child of the accumulated root (:833-845)
before walking the incoming file. Per-file work is therefore proportional to everything merged before it; summed over
the files of a type it is quadratic. Linear extras per file: a fresh `XmlSchemaSet` build with two compiles and the
`_replaceWhileMerging` overlay (`LoadXmlWithValidation` :1061-1129), and three reads of the file (the unused
`ReadToEnd` at :1347-1348 plus two `XmlReader` loads at :1111-1112 and :1120-1121). Each `ApplyXslt` (:984-995)
compiles a new `XslCompiledTransform` and transforms the whole accumulated document; nothing caches anything between
loads.

Player-log evidence that the cost tracks the accumulated document (E1, rgl_log_1744): vanilla SandBox NPC files
cost 0.04 to 0.11 s each early in the loop (L4586-L4604), while TAOM's files cost 0.174 to 0.485 s each
(L4608-L4692, mean 0.281 s over 42 files), with a floor near 0.18 s even for the 3,046-byte
`characters/spider_creature.xml` (0.182 s, L4630). Own size still adds: `characters/lords.xml` (2,732,743 bytes) costs
0.485 s (L4608). Pearson correlation of TAOM file size against gap: 0.44.

### Plan 042's engine facts, each checked on v1.4.8

| Fact (plan 042, v1.5.3 line) | v1.4.8 | Verdict |
|---|---|---|
| `CreateMergedXmlFile` :962-978, params `toBeMerged`, `xsltList`, `skipValidation`; `xsltList[0]` never applied; XSLT before its entry's file; `""` entry applies its XSLT and merges nothing; one-entry list returned as loaded; `!= ""` comparisons | :966-982, same text | CONFIRMED |
| `ApplyXslt` :980-991 compiles per call | :984-995, same text | CONFIRMED |
| `MergeTwoXmls` :993-1006 | :997-1010, same text | CONFIRMED |
| `ToXDocument` :1008-1021, `ToXmlDocument` :1023-1031 | :1012-1025, :1027-1035 | CONFIRMED |
| `MergeElementAttributes` :799-818 (removes, then copies every attribute including the marker) | :799-818, same | CONFIRMED |
| `MergeElements` :820-871 | :820-875, FOUR LINES LONGER | DIFFERS; what differs is UNVERIFIED (no 1.5.x dump). v1.4.8 has a branch at :861-864 that merges an unmatched `_replaceWhileMerging="true"` element into the FIRST element of its name group; it is a candidate. The fast path calls `MergeElements`, it does not replace it, so equivalence is unaffected |
| `CreateDocumentFromXmlFile` :1339-1355, private static, `(string, string, bool forceSkipValidation = false)` | :1343-1359 | CONFIRMED |
| `LoadXmlWithValidation` :1057-1125 | :1061-1129 | CONFIRMED |
| `InjectOptionalAttrToAllComplexTypes` :1239, `ValidationEventHandler` :1320 | :1243, :1324 | CONFIRMED |
| `GetMergedXmlForManaged` :873-914, `HandleXsltList` :945-960 | :877-918, :949-964 | CONFIRMED |
| `ModuleHelper.GetXmlPath`, `GetXsltPath`, `GetXsdPathForModules`, `GetXsdPath` | `_categories_v1.4.8\Platform\TaleWorlds.ModuleManager\TaleWorlds.ModuleManager\ModuleHelper.cs:232-249`, same formulas | CONFIRMED |
| `XmlResource.GetXmlListAndApply` :258-326 (reads every `IncludedGameTypes` child's `value`), `XsdElementDictionary` :78, `ReadXsdFileAndExtractInformation` :116-151, `GetFullXPathOfElement` :171-203 | `XmlResource.cs` is 190 lines on v1.4.8: :149-189, :32, :36, :75; same behaviour for what the harness mirrors | Behaviour CONFIRMED; line numbers differ by 45 to 140, so `XmlResource` itself changed between the engines; re-point every harness comment |
| Custom battle `CustomGame.OnInitialize` loads the four types validated and reads `CustomBattleScenes` unvalidated | `_modules_build` :4054-4146 | CONFIRMED |

IL check (read-only `ilspycmd -il` of the installed DLL; tokens approximated with `il_tokens.py`): the v1.4.8 bodies
of `CreateMergedXmlFile` (31 tokens), `MergeTwoXmls` (14), `ApplyXslt` (14), `ToXDocument` (10) and `ToXmlDocument` (6)
match, in order and with the same constants (`keepDuplicates` 0, loop start 1, `ldstr ""`, `17592186044416`), the pins
in `XmlMergeBindingTests.cs:110-166` at origin/bannerlord-1.5.x. Expect `MirroredEngineBodies_CallExactlyThePinnedSequence`
to pass on v1.4.8 without re-pinning. UNVERIFIED until the test itself runs (no dotnet in this run).

## 2. a27c6b338 and its follow-ups; what it depends on; the 1.4.x port spec

### Commits on 1.5.x

- a27c6b338 (2026-10-04) `perf(xml-merge)`: 46 files, +4738/-2. Main: `Main/Adapters/IXmlMergeEngineAdapter.cs`,
  `XmlMergeEngineAdapter.cs`, `Main/Features/XmlMerge/{XmlMergeModule, XmlMergeService, XsltTransformCache, XmlMergeCall,
  XmlMergeCounters, XmlMergeLines}.cs`, `Hooks/MBObjectManager_CreateMergedXmlFile_Patch.cs`, one line in
  `Main/Composition/FeatureModules.cs`, five lines in `Main/SubModule.cs`. Tests: 17 files under
  `TAOM.Tests/Features/XmlMerge/`, plus `IlCallScanner.cs` (+94, adds `Fingerprint`), new `IlFingerprintTests.cs`,
  `ReflectionSiteBindingTests.cs` (+3), `CoopVetoClassificationTests.cs` (+5), `BindingGateRunSettingsTests.cs` and
  `binding-gate.runsettings` (sets `TAOM_RUN_BENCHMARKS=1`). Docs, lessons, the deep review and RCA, and
  `.claude/skills/engine-bump/SKILL.md` (+5).
- Follow-ups (`git log --full-history` over the XmlMerge code and test paths): only 43ca92ee9, and its XmlMerge change
  is a comment (patch class lines 34-38). Its `PatchShieldPolicy.cs` hunk is comments about plan 034 and the Mission tick
  entries. Docs-only touches: ff1fe1933, cd1fbdb6f, d6b537092. `git diff a27c6b338 origin/bannerlord-1.5.x` over the
  XmlMerge code and tests shows only that comment, so a27c6b338 is the end state. The v1.5.4 bump 9c310bc23 says the
  XML-merge gate passed unchanged.
- The PatchShield exclusion: 346f3e093 (plan 040) appends `TaleWorlds.ObjectSystem.MBObjectManager.LoadXML` and
  `...CreateMergedXmlFile` (plus five `CampaignEventDispatcher` methods) to `PatchShieldPolicy.ExcludedTargetMethods`.
  That list and `IsExcludedTargetMethod` were introduced by 30abf550b (2026-09-26, subject "Add unit tests for various
  tools and functionalities"), on top of 2a3f71b12's `IsExcludedTargetNamespace` (plan 007). The deep-dive names
  346f3e093 and 43ca92ee9; it misses 30abf550b, and 43ca92ee9 adds nothing the port needs.
- No in-game measurement exists on any line: the a27c6b338 body says so, the feature doc still says "not yet measured in
  game", and the only `[XmlMerge]` lines in 1.5.x docs and tools are fixtures (`tools/tests/test_perf_runs.py:1465-1473`).

### Dependencies, and their state on 1.4.x (origin/bannerlord-1.4.5)

| Dependency | 1.4.x | Port action |
|---|---|---|
| `Main/Composition` (`TaomFeatureModule`, `PatchCategoryDecl`, `ApplyPhase.ProcessLoad`, `FeatureModules.All`) | absent | Drop `XmlMergeModule.cs`. Register the three singletons in `Main/IoC.cs` (DryIoc, `Reuse.Singleton`, `IoC.cs:1,81`); in `Main/SubModule.cs` `OnSubModuleLoad` add a guarded block in the Patch83 shape (`SubModule.cs:267-278`): resolve the service, `MBObjectManager_CreateMergedXmlFile_Patch.Initialize(service)`, `service.LogConfigurationHeader()`, `_harmony.PatchCategory("Patch99_XmlMergeFastPath")`, catch and `LogError` |
| Plan 009 `PatchCategoryApplier` / `TryPatchCategory` | absent | NOT needed (a27c6b338 never touches it; the 1.4.x try/catch pattern replaces it). The deep-dive's "plans 040 and 042 build on PatchCategoryApplier, so this goes first" is wrong for 042 |
| Per-game summary call | 1.4.x `OnGameInitializationFinished` (:1309) has no `ApplyMonsterSizes`/`ApplyGating` anchors | Insert `MBObjectManager_CreateMergedXmlFile_Patch.LogWindowSummary(game?.GameType?.GameTypeStringId);` after the `StampSaveLoadPhase(...GameInitializationFinished)` line (:1315) and before `if (_gameInitPatchesApplied) return;` (:1324) |
| PatchShield exclusion of `CreateMergedXmlFile` | `PatchShieldPolicy.cs` (153 lines) has no `ExcludedTargetMethods` and no `IsExcludedTargetMethod`; `PatchShield.cs:60-84` excludes three Gauntlet namespaces only | Add the 30abf550b shape (list plus ordinal `IsExcludedTargetMethod(declaringType, name)`) with one entry, and make `PatchShield.IsExcludedTarget` consult it. If plan 007's port lands first, build on its `PatchShieldPolicy` |
| Settings | plan 042 adds none (D14: no switch) | none; `SettingsFingerprintTests` unaffected (plan 040's MCM toggle would move it, plan 042's does not) |
| `IlCallScanner.Fingerprint`, `IlFingerprintTests` | 1.4.x `IlCallScanner.cs` equals a27c6b338's parent (empty diff), no `Fingerprint`; `IlFingerprintTests.cs` absent | apply the a27c6b338 hunk; add the test file |
| `binding-gate.runsettings`, `BindingGateRunSettingsTests` | both absent; 1.4.x has no CI (ccef172ad) | skip both; the harness runs by hand with `TAOM_RUN_BENCHMARKS=1`, which is new on 1.4.x (no existing user) |
| Test categories `RequiresGame`, `LiveInstall`, `RequiresGameIL` | used by no 1.4.x test | harmless strings; note them in the 1.4.x tests rule if desired |
| `GameAssemblies.GameDir/BinFolder/Diagnostics/EnsureLoaded` | same public surface on both lines | none |
| `CoopVetoClassificationTests` registry, `ReflectionSiteBindingTests` rows, `HarmonyPatchBindingTests` auto-discovery | all exist | add the ReviewedSafe entry and the `CreateDocumentFromXmlFile` row |
| `IModLogger` (Info/Debug/Warning/Error), Lib.Harmony 2.4.2, `System.Xml.Linq` | all present (Harmony 2.4.2 on both lines; XDocument already used in `Main/Features`) | none |
| Patch number | highest on 1.4.x is Patch88; `Patch99_`/`Patch100_` unused | keep `Patch99_XmlMergeFastPath` so crash triage matches across lines |
| Harness default module order | 1.5.x desktop order with `BirthAndDeath;FastMode` | change to the players' order `TAOM.Dependencies;Native;SandBoxCore;CustomBattle;SandBox;StoryMode;LOTRLOME_Armory;TAOM_Map;TAOM;BetterTime` (E1 L29), with BetterTime the one module allowed absent; update `LiveGateRules` and its tests |

### The PatchShield hazard is real on 1.4.x (why the exclusion is not optional)

PatchShield pass 1 runs in TAOM.Dependencies' `OnSubModuleLoad` (`Dependencies/SubModule.cs:234` at
origin/bannerlord-1.4.5), before TAOM applies Patch99, and pass 2 at every `OnGameInitializationFinished` (:288). Pass 2
shields every patched method not declared in a TAOM assembly, not in the three Gauntlet namespaces and not a SaveShield
target (`PatchShield.cs:169-219`); `CreateMergedXmlFile` returns `XmlDocument`, so it gets `ShieldFinalizerWithResult`
(:210-218, :255-258). From the second load of a process, a `MissingMethodException`, `MissingFieldException` or
`TypeLoadException` from the engine's own merge is swallowed (:277-292), the method returns null, and `LoadXML` hands
the null to `LoadXml` inside its catch-all (`MBObjectManager.cs:790-796`): the type loads empty, silently.
`TryUnpatchOffendingPatches` (:301-397) then strips every non-protected owner's prefixes, postfixes and transpilers
on the method; `com.taom.mod` (`Main/SubModule.cs:161`) matches none of the protected prefixes (`PatchShieldPolicy.cs:23-52`;
`IsProtectedOwner` :84-95 is a case-insensitive `StartsWith`, and "TAOM" is not a prefix of "com.taom.mod"), so
Patch99 itself would be removed for the session.

Release-pairing risk (#371): the exclusion lives in TAOM.Dependencies. E3 L2 shows players running a 2026-09-30
TAOM.dll with a 2026-09-14 TAOM.Dependencies.dll (`[BuildStamp] ... MISMATCH`). A port shipped that way would run
Patch99 without its exclusion. Mitigation for the spec: before applying Patch99, call (from its own method, inside a
try/catch, because a stale DLL fails at JIT) `PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.ObjectSystem.MBObjectManager",
"CreateMergedXmlFile")`; if it is missing or false, and PatchShield will install (`PatchShieldPolicy.ShouldInstall`,
`PatchShieldPolicy.cs:151`), do not apply Patch99 and log one WARNING naming the cause.

### Risks for the port

1. Merge semantics. The fast path drops the per-file `XmlDocument` round trip. 1.5.x proved it output-neutral only on
   the module sets it ran, and documents two divergent inputs (`docs/features/xml-merge-fast-path.md:131-142` at
   origin/bannerlord-1.5.x): a namespace prefix rebound by a later file, and adjacent text left by `_replaceWhileMerging`
   in mixed content. TAOM's 1.4.x NPCCharacters and EquipmentRosters roots carry no attributes (all 43 + 28 files);
   the Armory's 131 item files were not scanned here. The live harness on the v1.4.8 install is the gate.
2. XSLT order. Preserved by construction (the fast path walks the engine's own lists). The tip's
   `characters/custom_battle_lords` node must stay before the `lords` XSLT-only node (`Main/_Module/SubModule.xml`
   comment at origin/bannerlord-1.4.5, `CustomBattleLordStubsTests`); the fast path keeps that order. The XSLT cache
   keys on path, last-write time and length; none of the 8 repo stylesheets, the installed TAOM_Map's one or the
   Armory's eight uses `xsl:include`, `xsl:import` or `document()` (grep on this laptop).
3. Modules after TAOM. BetterTime loads last (E1 L29) and contributes no object XML (E1 L549 opens only a language
   file). Its Harmony patch set is UNVERIFIED (not installed on this laptop). The stand-aside rule reads
   `Harmony.GetPatchInfo` at each merge, after every module's `OnSubModuleLoad`, so a foreign patch on
   `CreateMergedXmlFile` (prefix, transpiler, inner) or on `ApplyXslt`, `MergeTwoXmls`, `ToXDocument`, `ToXmlDocument`
   sends merges to the engine. Patches on `MergeElements` and `CreateDocumentFromXmlFile` still apply (called the same
   way). A later module's XML for a validated type merges after TAOM's in both paths.
4. Proof domain and test data. The harness reads the INSTALLED module set (`<game>/Modules/TAOM/...`), not the repo.
   Record the installed TAOM version with the table. The desktop's live TAOM_Map exported after 2026-10-29 carries 1.5.x
   ids (brief2); equivalence still holds on any data, but the timing table is only representative on the players' set.
5. Memory. The compiled-XSLT cache holds about 11 MB of managed heap per process (1.5.x measurement), against
   privMB 9412 to 10826 in E3/E4; negligible.
6. Benefit unmeasured in game on any line. Treat any saving figure as a hypothesis until the in-game check below.

## 3. LOAD-TIME-07: one generated NPCCharacters file

### Figures: CONFIRMED (recomputed with `merge_spans.py`)

| Load | TAOM NPC (42 files, excl. last) | Armory Items (131) | TAOM_Map spnpccharacters + `lords.xslt` | TAOM ER (27) |
|---|---|---|---|---|
| E1 (L4586-L4692) | 11.80 s, mean 0.281 s | 5.35 s | 2.30 s (L4606) | 1.04 s |
| E2 load #1 (L4589 onward) | 11.92 s | 6.16 s | 1.93 s | 1.18 s (deep-dive 1.17) |
| E2 load #4 (L20905 onward) | 10.72 s | 6.12 s | 1.95 s | 1.08 s |

### Is joining semantically safe? Yes today, with conditions

Engine basis: `MergeElements` builds its id dictionary from the accumulated document before walking the incoming file
and never adds to it during the walk (`MBObjectManager.cs:833-874`); managed `LoadXml` deserializes every non-comment
child by id through `GetPresumedObject` (:1387-1395), so a duplicate in the final document is deserialized twice into one
object, not merged. Joining files in node order is therefore equivalent unless an id absent from the pre-TAOM document
appears in two joined files.

Checked (`dup_ids.py`, pre-TAOM documents = the 11 NPC files the engine merges before TAOM in E1, read from this
laptop's install):

| Ref | NPC files / elements | ids in 2+ TAOM files | repeated within a file | overrides of pre-TAOM ids | `_replaceWhileMerging` |
|---|---|---|---|---|---|
| c5b84fb41 (working tree) | 43 / 3,774 | 0 | 0 | 179 | 0 |
| origin/fix/public-armory-ids-145 | 43 / 3,774 | 0 | 0 | 179 | 0 |
| origin/bannerlord-1.4.5 (1b00d1acd) | 44 / 3,795 | 0 | 0 | 200 | 0 |
| EquipmentRosters, c5b84fb41 and tip | 28 / 3,518 | 0 | 0 | 70 | 70, every one matching a pre-TAOM id (so none reaches the "merge into the first element" branch at :861-864) |

All NPC roots are a bare `<NPCCharacters>`, all ER roots a bare `<EquipmentRosters>`. TAOM ships no NPCCharacters XSLT
besides the XSLT-only `lords` node, which precedes every TAOM NPC file. No other module's XSLT runs after TAOM's NPC
entries in the players' list. No C# in `Main` or `Dependencies` reads per-file NPC paths at run time.

Game-type filters: the deep-dive is WRONG. All 44 NPCCharacters nodes on the working tree carry a filter: 42 admit
`Campaign, CampaignStoryMode, CustomGame, EditorGame` and 2 admit only `Campaign, CampaignStoryMode`
(`taom_education_character_templates` L294, `named_companions/named_companions` L783); none is unfiltered. The tip adds a
45th, `CustomGame`-only node (`characters/custom_battle_lords`). EquipmentRosters: 21 four-type and 7 campaign-only (the
deep-dive says 27 unfiltered and 1 campaign-only). Every NPC node admits `Campaign`, so a campaign load merges all 43
files, as the deep-dive says. Adjacent same-filter runs, which a join must keep separate and in order: NPCCharacters
[`lords` XSLT-only] [12 four-type files] [1 campaign-only] [29 four-type] [1 campaign-only], so 4 files instead of 43
(39 fewer whole-document round trips), with the tip's `custom_battle_lords` kept first; EquipmentRosters 5 runs
(1, 2, 19, 5, 1), so 5 files instead of 28.

### What it costs on 1.4.x

- The repo's `SubModule.xml` and per-file sources must not change: 24 test files read the per-file sources and 6 parse
  the repo's `SubModule.xml` (among them `CustomBattleLordStubsTests`, `LordFamilyTransformTests`,
  `LordIdentityConsistencyTests`). Only the deployed copy may name generated files.
- The generated files must not land in `Main/_Module/ModuleData`: `tools/taom_schema.py` (behind
  `validate_moduledata.py`) discovers files with `moduledata.rglob("*.xml")` (:243) and also globs
  `troops/troops_*.xml` and `characters/npcs_*.xml` (:1108-1109), so every id would be read twice.
- `CopyModule` deploys `_Module` verbatim and additively (`Main/TAOM.csproj:126-143`), so the step needs its own MSBuild
  target and output folder, a transform of the deployed `SubModule.xml`, and the same step in `tools/package_release.py`.
- Triage gets worse: rgl `opening` lines and validation messages would name the generated file.

### Does it still help once plan 042 lands? Barely

With the fast path a file's cost no longer includes the round trip. What remains per file is the schema-set build
(1.5.x predicted a whole-load schema cache at 251 to 280 ms across every type, `xml-merge-fast-path.md:402-405`) and
the dictionary rebuild in `MergeElements` (51 ms in total for NPCCharacters in the prototype, plan 042 :343). Joining
after 042 saves well under 0.5 s per load, which is why the 1.5.x maintainer decided D14 (2026-10-03, "as recommended"):
"no lords.xml, build-step merge or off switch" (`plans/_audit/2026-10-02-perf/DECISIONS.md:18` and
`docs/features/xml-merge-fast-path.md:421-423`, origin/bannerlord-1.5.x). It also cannot touch the Armory's 131 item
files (5.35 to 6.68 s per load), which plan 042 covers.

### Recommendation

Port plan 042, not LOAD-TIME-07. Keep LOAD-TIME-07 as the fallback only if the maintainer rejects an engine patch on the
merge, gated as below. Doing both is not worth it. Rough saving of LOAD-TIME-07 alone on the E1/E2 machine: 39 fewer
round trips at the measured 0.24 to 0.28 s per TAOM file, about 9 to 10 s per load, plus about 0.9 s for
EquipmentRosters. That is an estimate, UNVERIFIED.

## 4. Before-figures a plan can quote, and the lines that measure the after

Method: E1/E2 timestamps are milliseconds. Each validated file prints `opening <xml>` (`MBObjectManager.cs:1345`) then
`opening ../../XmlSchemas/<Type>.xsd` (:1063); an XSLT prints nothing, so its cost lands in the gap before the next
file. A type's span runs from its first file to the next type's first file, so it includes that type's object creation
and any non-XML work between two `LoadXML` calls (the Settlements span absorbs `OnGameLoaded`, `LoadBehaviorData` and
`OnGameInitializationFinished`). The window markers: "Initializing saved game end..." (`SandBoxGameManager.cs:98`,
printed just before `Game.Current.DoLoading()`) to `Loading xml file: $BASE/Modules/TAOM_Map/SceneObj/Main_map/atmosphere.xml.`

E1 (rgl_log_1744): LOAD STATISTICS L3881 (19:39:04.134) to `MapScreen::HandleInitialize` L4913: 70.5 s. Window L3940
to L4719: 27.2 s. Spans: GameText 26 files 1.32 s (from L3941), Items 142 files 6.67 s (L4068), EquipmentRosters 31
files 1.23 s (L4352), NPCCharacters 54 files 15.10 s (L4586), Settlements 2 files 1.13 s (L4700). The four heavy types
are 24.32 s of the 27.2 s window; all type spans together 26.30 s.

E2 (rgl_log_22112, same machine, 12 loads; the twelve windows sum to 292.9 s):

| # | LOAD STATISTICS | to map (s) | window (s) | NPC span | Items span | TAOM NPC 42 files |
|---|---|---|---|---|---|---|
| 1 | L3884 | 92.8 (L4938) | 32.8 | 15.08 | 7.58 | 11.92 |
| 2 | L9780 | 88.4 | 29.7 | 13.83 | 7.21 | 10.75 |
| 3 | L12252 | 83.3 | 24.0 | 13.90 | 6.46 | 10.86 |
| 4 | L20189 | 86.1 (L21230) | 23.9 | 13.62 | 6.54 | 10.72 |
| 5 to 11 | L21485 to L30019 | 84.3 to 87.6 | 22.6 to 22.9 | 12.95 to 13.33 | 6.32 to 6.43 | 10.05 to 10.45 |
| 12 | L32294 | 111.3 (L33335) | 23.4 | 13.14 | 6.50 | 10.14 |

E3 and E4 (taom_debug; another machine, 8 cores, E3 L32; one-second log stamps but `[SaveLoad] t=+ms` offsets):
E3 `ObjectsInitialized t=+10172ms` (L30) to `GameLoaded t=+49284ms` (L79) is 39,112 ms; LoadRequested L24 (20:00:34) to
`[MemStation] enter screen='MapScreen'` L154 (20:01:41) is 67 s. E4: L29 +10281 ms to L77 +49336 ms, 39,055 ms; L21
(20:17:16) to L153 (20:18:23), 67 s. `ObjectsInitialized` is the postfix on `LoadResult.AfterInitializeObjects`
(`Main/Features/SaveLoadDiagnostics/Hooks/LoadResult_InitializeCallbacks_Patch.cs:46-52`), after the vanilla Callbacks
phase; `GameLoaded` is `OnGameLoaded` (`Main/SubModule.cs:743-747`). The window holds TAOM's `OnGameStart` (E3 L31-L75,
20:00:46), GameText, game models, the `Default*` constructors and every campaign `LoadXML`; the CulturalFeats postfix
on `InitializeDefaultCampaignObjects` (L77, 20:01:02) splits it 16 s and 21 s after the `OnGameStart` lines. So 39.1 s
is an upper bound on XML work there, not a measurement of it.

After-measure lines:
- rgl (unchanged by the fast path, which loads every file through the engine's `CreateDocumentFromXmlFile`): the same
  `opening` pairs, the same window markers, LOAD STATISTICS and `MapScreen::HandleInitialize`. The TAOM NPC per-file
  gap (0.24 to 0.28 s today) is the sharpest signal. Compare a process's first load with a first load, and later
  loads with later loads: the XSLT cache makes `lords.xslt` about 1 s on the first and 0.05 to 0.1 s after.
- taom_debug.log: `[XmlMerge] fast path ready: ...` at boot (or `fast path off: <reason>`); per validated type
  `[XmlMerge] type=<T> files=N xslt=K ms=M path=fast load_ms=.. xslt_ms=.. merge_ms=.. xslt_compiles=.. xslt_cache_hits=..`;
  `path=vanilla reason=skip-validation` for unvalidated types; per game init `[XmlMerge] summary game=Campaign merges=..
  fast=.. vanilla=.. files=.. ms=.. max_ms=.. max_type=.. ... fast_path=on`; plus the existing `[SaveLoad]`
  ObjectsInitialized to GameLoaded pair. Any `stands aside`, `fast path failed` or `disabled for this session` line is a
  finding.
- Optional: plan 040's `[LoadXml]` lines (merge plus creation split) if 040 is ported; 1.5.x `tools/perf_runs.py`
  (absent on 1.4.x) parses both summaries.

## Corrections to the lens

1. E2 later-load windows are 22.6 to 29.7 s (load #2 is 29.7 s), not 22.6 to 24.0 s; the deep-dive's own 292.9 s sum
   includes the 29.7.
2. E2 Items spans are 6.32 to 7.58 s, not 7.7 to 9.4 s (load #1: L4071 17:22:49.421 to L4355 17:22:57.002).
3. E3/E4: the window is 39.1 s and also holds non-XML work; "about 37 s ... is XML loading" overstates it.
4. `CreateDocumentFromXmlFile` plus `LoadXmlWithValidation` read each file three times and parse it twice, not twice.
5. The per-file cost has a floor near 0.18 s set by the accumulated document; own size still adds (lords.xml 0.485 s).
6. The "+4 lines" shift starts inside `MergeElements` (v1.5.3 :820-871, v1.4.8 :820-875), so that method differs
   between engines (contents UNVERIFIED). The five bodies plan 042 mirrors are identical, by text and by IL.
7. The 1.4.x `PatchShieldPolicy` has no method-level exclusion. The machinery came from 30abf550b; 43ca92ee9 is
   comment-only for this port.
8. Plan 009 is not a prerequisite of plan 042.
9. NPCCharacters filters are 42 four-type plus 2 campaign-only (tip: plus 1 CustomGame-only), not "41 unfiltered";
   EquipmentRosters 21 plus 7, not "27 unfiltered, 1 campaign-only".
10. LOAD-TIME-07's "left to the maintainer" is stale: D14 decided no build-step merge on 1.5.x.

## Open questions

- What `MergeElements` looks like on v1.5.3 (needs a 1.5.x decompile).
- BetterTime's Harmony targets (scan its DLL on a machine that has it, or read a Harmony census from a player log).
- In-game saving on v1.4.8: unmeasured on every line.
- Whether TAOM's own patches should count as protected PatchShield owners (`com.taom.mod` is not; intent UNVERIFIED;
  for the PatchShield lens).

## Recommended skills (for the orchestrator)

`/research` on `MBObjectManager` before the port; `/verify-bindings` after it; `/deep-review` on the branch; the
maintainer's in-game load check.
