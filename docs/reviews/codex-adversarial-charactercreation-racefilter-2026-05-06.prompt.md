# Codex Adversarial Review -- CharacterCreation Race Filter (Patch9_RaceFilter re-implementation)

You are reviewing a TAOM (Bannerlord 1.3.15 LOTR total-conversion mod) feature that filters the FaceGen race dropdown by the player's selected culture during character creation. The previous attempt (filtering `FaceGen.GetRaceNames()` directly) shipped as a no-op because it broke `FaceGenVM`'s index-based race ID contract. The new attempt patches `FaceGenVM.Refresh(bool)` and rebuilds the race `SelectorVM` while preserving the engine's index contract via a wrapped onChange that translates filtered position to global race index via reflection.

This is your job: find bugs the in-house review missed. Be adversarial, prove things with code references, do not skip hard sections.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar

Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale

NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur". "elf" is a valid race ID and IS present in the live LOTRLOME_Armory monsters.xml and skins.xml.

## READ FIRST

1. docs/features/character-creation.md -- existing feature doc with new "Race Filter" section
2. CHANGELOG.md -- the 2026-05-06 entry (top of file) describes the design and the post-review fixes
3. Main/_Module/ModuleData/charactercreation/cultures.json -- single source of truth for the per-culture races allow-list

## KNOWN SUSPECTS

These are specific hypotheses about likely bugs. CONFIRM or DISPUTE each with code references. If you DISPUTE, explain why (decompile vanilla, point at the actual code path).

1. **`OnPropertyChangedWithValue` reflection target wrong**
   `FaceGenRaceSelectorRebuilder.EnsureFields()` calls `AccessTools.Method(typeof(FaceGenVM), "OnPropertyChangedWithValue", new[] { typeof(object), typeof(string) })`. The actual method on the `ViewModel` base is generic: `protected void OnPropertyChangedWithValue<T>(T value, string propertyName) where T : class`. Verify this resolves to the right method and that `Invoke(faceGenVM, new object[] { newSelector, "RaceSelector" })` actually fires the property-change notification the GUI prefab is bound to. If it silently fails, the dropdown will appear filtered but the visual binding will not update.

2. **`SelectorVM._selectedIndex` mutation while inside the `_onChange` callback**
   The wrapped onChange in `FaceGenRaceSelectorRebuilder.WrapOnChange` saves `s.SelectedIndex`, mutates the private `_selectedIndex` to the global value via reflection, invokes vanilla `OnSelectRace(s)`, then restores `_selectedIndex`. Trace what happens when vanilla `OnSelectRace` calls `Refresh(true)` (line 1728 of FaceGenVM): the inner Refresh rebuilds RaceSelector at line 1925, our Postfix replaces it with another filtered selector. By the time the outer wrapped's `finally` runs, `s` is orphaned -- but the SelectorVM's internal state may have invariants that we have left in an inconsistent state (e.g., `SelectedItem` referring to a stale item, `IsSelected` flags inverted). Verify by tracing the SelectedIndex setter (TaleWorlds.Core decompiled SelectorVM.cs lines 38-62).

3. **`needToSwitchRace` force-switch bypasses SelectorVM lifecycle**
   When `needToSwitchRace` is true, the rebuilder calls `wrapped(newSelector)` directly. This synthesizes the equivalent of "user clicked the first item" but bypasses the public `SelectedIndex` setter (which fires `OnPropertyChangedWithValue("SelectedIndex")` and toggles `SelectedItem.IsSelected`). The wrapped function does not fire those notifications. Could this leave the UI showing the right race but not animating the selection state? Verify against vanilla `SelectorVM<T>.SelectedIndex` setter behavior.

4. **`_inForceSwitch` ThreadStatic guard correctness**
   The guard prevents the recursive Refresh from triggering another force-switch, but only if the recursion happens on the same thread. Bannerlord's GUI is reportedly single-threaded, but verify -- if any Bannerlord async dispatch can move the recursive Refresh to a different thread, the guard would be ineffective and we could infinite-loop. Search for `Task.Run`, `Dispatcher`, or `BeginInvoke` patterns near FaceGenVM/CC code paths in the decompiled vanilla source.

5. **Race-name lookup fallback masks invalid IDs**
   `CharacterCreationContentService.SetPlayerRace` calls `_raceManager.GetRaceNameFromId(faceGenRaceId)`. Per RaceManager.cs lines 126-130, unknown race IDs fall back to "human" with a warning. If `Hero.MainHero.CharacterObject.Race` somehow holds an invalid ID at finalize time, we would resolve it to "human" and then check whether "human" is in the culture's allowed list. For Erebor (allow-list = [dwarf]), "human" is not allowed and we correctly fall back to `Races[0]` = "dwarf". But for Mordor (allow-list = [uruk, orc, human]), an invalid race ID would be silently coerced to "human" and accepted -- even if the player actually picked something else. Verify whether `Hero.CharacterObject.Race` can hold an invalid ID at finalize, and whether this masking is acceptable.

6. **Orphaned SelectorVM event subscriptions / GC pressure**
   On every `Refresh(true)` (which fires on every race change), the patch creates a brand-new `SelectorVM<SelectorItemVM>` and assigns it to `_raceSelector`, leaving the previous one orphaned. The orphan still has its `SelectorItemVM` children holding references back. If anything in the GUI prefab keeps a strong reference to the previous `RaceSelector` (e.g., a captured lambda or a `MBBindingList` listener), we leak. Search for `RaceSelector` bindings in the FaceGen prefab and FaceGenVM gauntlet binding code to verify the property setter properly releases the old reference.

7. **cultures.json ID consistency vs cheatsheet**
   Verify every `culture_id` in `Main/_Module/ModuleData/charactercreation/cultures.json` matches the cheatsheet above. Special attention: `dolguldur` (not `dol_guldur`), `mordor` (not `empire_s` since this file uses CULTURE IDs not kingdom IDs), `gondor`, etc.

8. **`AccessTools.Method` for sealed `Refresh` overload**
   `[HarmonyPatch(typeof(FaceGenVM), "Refresh", new[] { typeof(bool) })]` -- verify the method `Refresh(bool clearProperties)` exists on `FaceGenVM` (not on a base class) in the installed v1.3.15 DLL and is the only single-bool-parameter `Refresh` method. If there is an inherited `Refresh()` (no params) or a `Refresh(int)` overload, the patch attribute might bind to the wrong method.

## FILE LISTS BY CATEGORY

### NEW FILES (the feature):
- Main/Features/CharacterCreation/ICultureRaceFilterService.cs (interface)
- Main/Features/CharacterCreation/CultureRaceFilterService.cs (service: reads cultures.json races[])
- Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs (engine: rebuilds the SelectorVM via reflection, with three pure helper methods extracted for testability)
- Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs (Harmony postfix entry point, 25 lines, lazy-cached IoC.Resolve at boundary)

### MODIFIED FILES:
- Main/Features/CharacterCreation/CharacterCreationIoC.cs (registered new service, removed dead `IOnGetRaceNames` registration)
- Main/Features/CharacterCreation/CharacterCreationContentService.cs (`SetPlayerRace` now honors player's FaceGen race choice when in allowed list -- review-driven fix)
- Main/Adapters/IHeroRosterAdapter.cs + Main/Adapters/HeroRosterAdapter.cs (added `GetHeroRace(string)`)
- Main/_Module/ModuleData/charactercreation/cultures.json (Mordor lost "goblin", Isengard lost "saruman"; added per-culture races filtering)
- CLAUDE.md (Patch9_RaceFilter row updated to name FaceGenVM.Refresh)
- docs/features/character-creation.md (added Race Filter section)

### DELETED FILES (dead code from the failed predecessor):
- Main/Features/CharacterCreation/Hooks/FaceGen_GetRaceNames_Patch.cs (was a no-op)
- Main/Features/CharacterCreation/Hooks/IOnGetRaceNames.cs (empty marker interface)
- Main/Features/CharacterCreation/Hooks/GetRaceNamesHook.cs (empty class)
- TAOM.Tests/Features/CharacterCreation/GetRaceNamesHookTests.cs (asserted nothing useful)

### TESTS:
- TAOM.Tests/Features/CharacterCreation/CultureRaceFilterServiceTests.cs (NEW, 24 tests)
- TAOM.Tests/Features/CharacterCreation/FaceGenRaceSelectorRebuilderTests.cs (NEW, 12 tests for pure helpers + round-trip property)
- TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs (modified: 3 new SetPlayerRace tests for FaceGen-preservation logic)

## REQUIRED SECTIONS

### VANILLA CODE (decompile and paste as code blocks)

For each of these, decompile the installed v1.3.15 DLL via ilspycmd and paste the relevant section as a code block. Do NOT use the v1.4 decompiled folder at E:\Decompiled_Bannerlord\ for signature verification -- it is a different version.

ilspycmd binary: C:\Users\mikew\.dotnet\tools\ilspycmd.exe
DLL paths: E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\

Required vanilla decompiles:
- TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator.FaceGenVM (full type, focus on Refresh, OnSelectRace, _raceSelector, _selectedRace, RaceSelector property, ctor)
- TaleWorlds.Core.ViewModelCollection.Selector.SelectorVM`1 (the generic type -- ilspycmd 9.1 may not resolve generics; if so, use the v1.4 decompiled folder file at E:\Decompiled_Bannerlord\UI\TaleWorlds.Core.ViewModelCollection\TaleWorlds.Core.ViewModelCollection.Selector\SelectorVM.cs as a structural reference and assume same shape unless evidence shows otherwise -- explicitly note the version caveat)
- TaleWorlds.Core.ViewModelCollection.Selector.SelectorItemVM
- TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState (verify CharacterCreationManager property exists and is public-get)
- TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager (verify CharacterCreationContent property)
- TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationContent (verify SelectedCulture property)

### Feature-specific deep analysis

For each Known Suspect 1-8 above, produce:
- VERDICT: CONFIRMED / DISPUTED / UNDETERMINED
- EVIDENCE: code paths, line numbers, decompiled vanilla snippets
- IF CONFIRMED: severity (HIGH/MED/LOW), proposed fix
- IF DISPUTED: why the in-house review's hypothesis was wrong

Additionally analyze:
- The race-change round-trip on a HUMAN-ONLY culture (gondor): allRaces.Length=14, allowed.Count=1, the rebuilder's early-return `if (allowed.Count >= allRaces.Length) return;` is fine, but `if (allowed.Count == 0 || allowed.Count >= allRaces.Length) return;` -- when `allowed.Count == 1` and `allRaces.Length == 14`, neither condition triggers, the filter applies. If the player's `_selectedRace` is already 0 (human), `filteredSelected = 0`, no force-switch needed. If `_selectedRace = 5` (some non-human race), filteredSelected = -1, needToSwitchRace = true. Trace: does the force-switch correctly drive to "human"?
- The Erebor scenario: allowed = [dwarf]. If `_selectedRace = 0` (human), needToSwitchRace = true. The wrapped onChange fires with `newSelector.SelectedIndex = 0` (filtered position 0). globalIdx = globalIndices[0] = (dwarf's global index, say 1). Mutates `s._selectedIndex = 1`, calls vanilla OnSelectRace, which sets `_selectedRace = 1` and calls `UpdateRaceAndGenderBasedResources()` then `Refresh(true)`. Inner Refresh rebuilds RaceSelector. Postfix runs (recursive) but `_inForceSwitch=true` so it does NOT call wrapped again -- BUT it still rebuilds the selector. So we end up with two consecutive selector replacements during a single user click. Is the final state correct? Verify.

### CONFIG CROSS-REFERENCE

Read Main/_Module/ModuleData/charactercreation/cultures.json. For each culture_id verify:
- It matches the cheatsheet (gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, empire, vlandia, sturgia, aserai, shaghana, abanissa, battania, khuzait)
- Each race in `races[]` is a valid race ID present in either Native/skins.xml or LOTRLOME_Armory/skins.xml or LOTRLOME_Armory/monsters.xml
- The Mordor races [uruk, orc, human] match the spec (no goblin)
- The Isengard races [uruk_hai, berserker, human] match the spec (no saruman)
- Elven cultures (mirkwood, lothlorien, rivendell) all have ["elf", "human"]

Live install paths to grep for race IDs:
- E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/monsters.xml
- E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/skins.xml
- E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/ModuleData/skins.xml

### FINDINGS OR OBSERVATIONS

For each finding, provide:
- ID (F1, F2, ...)
- TITLE
- SEVERITY (HIGH/MED/LOW)
- FILE:LINE
- DESCRIPTION (what is wrong)
- EVIDENCE (code snippets from TAOM AND vanilla)
- IMPACT (what breaks for the player)
- PROPOSED FIX

If you find no bugs, list at least 3 OBSERVATIONS (things that work but you want noted).

## QUALITY GATES

This review will be discarded as low-quality if:
- It references the decompiled folder for signature verification (must use ilspycmd on installed DLLs)
- Code blocks are missing for vanilla targets in CONFIRMED findings
- Known Suspects section is skipped
- Config cross-reference is skipped
- Findings are vague ("might have an issue") instead of concrete ("line 47 reads X but vanilla expects Y")

## PRIOR REVIEW LESSONS

SUCCESSES from past reviews on this codebase:
- Config ID cross-reference caught rohan/dol_guldur mismatches
- Vanilla decompilation caught missing gates and mismatched signatures
- Lifecycle tracing caught stale caches
- Reflection-target verification caught field-on-wrong-type bugs

FAILURES from past reviews on this codebase:
- Codex assumed empire=Rohan (it is Dunland) -- USE THE CHEATSHEET
- Codex flagged vanilla-matching code as bugs (decompile first, then judge)
- Codex skipped the hard sections (vanilla decompile, lifecycle tracing) -- do not skip
- Codex was wrong about CC race finalization in past reviews; verify by reading the actual finalize code path

## OUTPUT

Write your review to: docs/reviews/codex-adversarial-charactercreation-racefilter-2026-05-06.md

Format the review with the standard headings: SUMMARY, KNOWN SUSPECTS (with verdicts), FINDINGS (numbered F1+), VANILLA CODE BLOCKS, CONFIG CROSS-REFERENCE, OBSERVATIONS.
