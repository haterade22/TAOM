# Codex Adversarial Review — CustomBattles Filter+Cap (2026-05-04)

> **Status:** PROMPT — for Codex to fill in. Dispatch via `/codex:adversarial-review --background` from a terminal. When Codex writes its findings into this file, run `/review-codex docs/reviews/codex-adversarial-custombattles-filter-cap-2026-05-04.md`.

---

## Scope

This is a focused review of a SMALL ENHANCEMENT to the existing CustomBattles feature, NOT a full feature review. The original feature was reviewed on 2026-04-05 (see `docs/archive/codex-reviews-2026-04/codex-adversarial-custombattles-2026-04-05.md`). Today's enhancement adds:

- A per-faction commander dropdown filter in the Custom Battle UI (was unfiltered, showing every culture's lords for every faction)
- A 3-commander cap per culture (deterministic, ordered by Id)
- Empty-result diagnostic logging to surface culture-tag misalignment in `rgl_log.txt`

ONLY review the files listed below. Do NOT re-review the original Characters/Factions/Troops patches or the team-fix behavior.

---

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa.

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar.

Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale.

NOTE: "rohan" is NOT a valid ID — Rohan uses "vlandia". "dol_guldur" is NOT valid — use "dolguldur". "empire" means Dunland in TAOM, not Empire.

---

## READ FIRST

- `docs/features/custom-battles.md` (just updated — describes the new filter+cap behavior under "Commander filter+cap (per faction)")
- `Main/_Module/ModuleData/characters/lords.xml` — TAOM lord IDs and their `culture="..."` attribute (specifically the Dunland section: `lord_1_1_*`, `lord_NE7_u`, `lord_NE8_l` should all be `culture="Culture.empire"`)
- The previous Codex review at `docs/archive/codex-reviews-2026-04/codex-adversarial-custombattles-2026-04-05.md` if you need the original feature's design context

---

## Files in scope (review ONLY these)

### Production — modified
- `Main/Features/CustomBattles/CustomBattleService.cs` — added `GetCommanderIdsForFaction(string factionId, int takeMax)` overload with `OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase).Take(takeMax)`
- `Main/Features/CustomBattles/ICustomBattleService.cs` — interface extension
- `Main/Features/CustomBattles/CustomBattlesIoC.cs` — register `ISideCommanderFilter` (Singleton); init two new patches; pass through new dependency
- `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_Constructor_Patch.cs` — extended: cached `MethodInfo` via `AccessTools.Method`, then explicit `callback(initialFaction)` invocation after `FactionSelectionGroup` swap to align initial-paint dropdown with the actually-visible faction
- `Main/SubModule.cs` — passes `IoC.Resolve<ISideCommanderFilter>()` into `CustomBattlesIoC.InitializeHooks(...)`

### Production — new
- `Main/Features/CustomBattles/Hooks/ISideCommanderFilter.cs` — hook interface
- `Main/Features/CustomBattles/Hooks/SideCommanderFilter.cs` — singleton hook impl, `MaxCommandersPerCulture = 3`
- `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_OnCultureSelection_Patch.cs` — postfix on private `OnCultureSelection(BasicCultureObject)` (patched by string name + type array)
- `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_RefreshValues_Patch.cs` — postfix on `RefreshValues()`

### Tests
- `TAOM.Tests/Features/CustomBattles/CustomBattleServiceTests.cs` — added 4 cap tests (cap, deterministic order, fewer-than-cap, zero-cap)
- `TAOM.Tests/Features/CustomBattles/SideCommanderFilterTests.cs` — new (6 tests)

---

## Vanilla targets (decompile to verify)

The installed game is v1.3.15. Use the INSTALLED DLLs only:

- `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/CustomBattle/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.CustomBattle.dll`

Required types:
- `TaleWorlds.MountAndBlade.CustomBattle.CustomBattleSideVM` (constructor, `RefreshValues`, private `OnCultureSelection(BasicCultureObject)`, public `CharacterSelectionGroup`, public `FactionSelectionGroup`)
- `TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem.CustomBattleFactionSelectionVM` (constructor calls `SelectFaction(0)` — verify whether this fires `_onSelectionChanged` or NOT)
- `TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem.FactionItemVM` (`Faction` property, `OnFactionSelected` callback wiring)
- `TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem.CharacterItemVM(BasicCharacterObject)` constructor signature
- `TaleWorlds.Core.ViewModelCollection.Selector.SelectorVM<T>` (`AddItem`, `ItemList`, `SelectedIndex` setter — verify whether `SelectedIndex = 0` re-fires the `_onChange` callback unintentionally)
- `TaleWorlds.Library.MBBindingList<T>` — verify it inherits `Collection<T>` so the cast `(Collection<CharacterItemVM>)(object)ItemList` is sound

Do NOT use `E:\Decompiled_Bannerlord\` — it is v1.4 and signatures differ.

---

## Known Suspects (CONFIRM or DISPUTE each)

1. **Reentrancy / double-rebuild on initial paint.** The `Constructor_Patch.Postfix` now does `callback(initialFaction)` after the FactionSelectionGroup swap. This invokes the patched `OnCultureSelection`, which fires `OnCultureSelection_Patch.Postfix` and rebuilds `CharacterSelectionGroup.ItemList`. Earlier in the constructor, vanilla `RefreshValues()` runs and `RefreshValues_Patch.Postfix` ALSO rebuilds the list (against the still-vanilla `FactionSelectionGroup`). End state is correct but two clear+rebuild cycles fire on initial paint. Is this materially a problem (UI flicker, selection-event loops)? Specifically: does setting `CharacterSelectionGroup.SelectedIndex = 0` inside the postfix re-fire `OnCharacterSelection` and cause a chain reaction?

2. **`SelectedIndex = 0` setter side effect.** In both filter patches (`OnCultureSelection_Patch`, `RefreshValues_Patch`), after rebuilding `ItemList` we set `CharacterSelectionGroup.SelectedIndex = 0`. Decompile vanilla `SelectorVM<T>.SelectedIndex` setter: does it fire the `_onChange` callback (`OnCharacterSelection`)? If yes, we're firing the callback once per filter, every time the user clicks a faction — is that the correct vanilla UX or an unintended side effect?

3. **Empty-list `SelectedIndex` crash.** Both filter patches early-return when `commanders.Count == 0`. But what about cases where the rebuild DOES proceed and produces exactly 1 or 2 items (less than cap)? Setting `SelectedIndex = 0` on an `ItemList` of size 1 — safe? Of size 0 — does the rebuild branch ever produce an empty `ItemList` despite `commanders.Count > 0`? Trace `SideCommanderFilter.ResolveCommandersForCulture` — could any of the 3 IDs resolve to null and silently filter out, leaving an empty list?

4. **`AccessTools.Method` resolution semantics.** `Constructor_Patch.Initialize` now does `_onCultureSelectionMethod = AccessTools.Method(typeof(CustomBattleSideVM), "OnCultureSelection")`. `AccessTools.Method` defaults — does it find private instance methods? Does it match by name only (returning the FIRST match if there are overloads), or does it require an explicit parameter array? `OnCultureSelection` takes `BasicCultureObject` — is there any chance of overload ambiguity? If so, the `BindingFlags`-less call could resolve to the wrong overload silently.

5. **Patch ordering vs `Patch19_CustomBattles` activation.** `SubModule.cs` calls `_harmony.PatchCategory("Patch19_CustomBattles")` at line ~108, before `CustomBattlesIoC.InitializeHooks(...)` at line ~133. The new patches' static `_filter` and `_onCultureSelectionMethod` fields are null until `Initialize`. If the Custom Battle screen could possibly load between line 108 and line 133, the patches would no-op via null-guard. Verify this can NEVER happen at startup (e.g., `OnSubModuleLoad` precedes any UI). Also verify the null-guards exist in BOTH new postfixes and the constructor postfix.

6. **Determinism of `OrderBy(c.Id, OrdinalIgnoreCase).Take(3)`.** Across launches, `MBObjectManager.GetObjectTypeList<BasicCharacterObject>()` enumeration order may differ. The `OrderBy` is applied BEFORE `Take(3)`, so output should be deterministic regardless of input order. Verify there are no other sources of non-determinism (e.g., `Where(IsValidCommander)` evaluation order matters? It shouldn't because OrderBy materializes everything before Take, but confirm).

---

## REQUIRED SECTIONS

### 1. VANILLA CODE

Decompile and paste relevant snippets (5-15 lines each) from the installed v1.3.15 DLL:

- `CustomBattleSideVM` constructor body
- `CustomBattleSideVM.OnCultureSelection(BasicCultureObject)` body
- `CustomBattleSideVM.RefreshValues()` body
- `CustomBattleFactionSelectionVM` constructor (specifically: does `SelectFaction(0)` fire `_onSelectionChanged`?)
- `SelectorVM<T>.SelectedIndex` setter (does it fire `_onChange`?)
- `MBBindingList<T>` declaration (verify `: Collection<T>` inheritance)

### 2. FEATURE-SPECIFIC DEEP ANALYSIS

Trace these scenarios end to end:

**Scenario A — User opens Custom Battle screen, faction[0] is auto-selected:**
1. `CustomBattleSideVM` constructor runs.
2. Vanilla creates `FactionSelectionGroup_v` (vanilla). Does its constructor call `_onSelectionChanged`? If yes, our `OnCultureSelection_Patch.Postfix` fires here against vanilla's FactionSelectionGroup. If no, only `RefreshValues_Patch.Postfix` fires (later in the constructor body).
3. Vanilla creates `CharacterSelectionGroup`.
4. Vanilla calls `RefreshValues()` → `RefreshValues_Patch.Postfix` runs, rebuilds dropdown filtered to vanilla `FactionSelectionGroup_v.SelectedItem.Faction`.
5. Vanilla constructor body ends.
6. `Constructor_Patch.Postfix` runs → replaces FactionSelectionGroup with `TaomFactionSelectionVM` → calls `callback(initialFaction)` → `OnCultureSelection_Patch.Postfix` fires → rebuilds dropdown filtered to TaomFactionSelectionVM's first faction.

CONFIRM the final dropdown state matches the visually-selected faction. CONFIRM no exception path during construction.

**Scenario B — User clicks a different faction button:**
1. `FactionItemVM.OnSelected` → `CustomBattleFactionSelectionVM.OnFactionSelected` → `_onSelectionChanged(faction.Faction)` → `CustomBattleSideVM.OnCultureSelection(culture)` is invoked (Harmony-patched).
2. Vanilla body runs (banner colors, composition group culture).
3. `OnCultureSelection_Patch.Postfix` fires: `_filter.ResolveCommandersForCulture(culture.StringId)` → `_service.GetCommanderIdsForFaction(culture.StringId, 3)`.
4. Service queries `GetCharacterCache().Where(IsValidCommander && CultureId == factionId, OrdinalIgnoreCase).OrderBy(Id).Take(3)`.
5. Hook resolves IDs to `BasicCharacterObject` via `_objectManager.GetBasicCharacter(id)`, filters nulls, returns list.
6. Postfix clears `ItemList`, adds `CharacterItemVM` per resolved char, sets `SelectedIndex = 0`.

CONFIRM the dropdown rebuilds correctly. CONFIRM the user's previously-selected commander is replaced sanely (no orphan reference).

**Scenario C — Faction has zero TAOM lords (data-tag mismatch):**
1. User picks a faction whose `BasicCultureObject.StringId` doesn't match any lord's `culture` attribute in `lords.xml`.
2. `_service.GetCommanderIdsForFaction(...)` returns empty list.
3. Postfix logs `LogWarning` and early-returns.
4. `ItemList` retains whatever was previously there.

QUESTION: Is the "retain previous list" behavior correct, or should the dropdown be cleared (showing empty) so the user knows the faction has no commanders? Argue for/against.

### 3. CONFIG CROSS-REFERENCE

Read `Main/_Module/ModuleData/characters/lords.xml`. For each TAOM kingdom faction the user can pick in Custom Battle (per `CustomBattleService.GetFactionIds()` filter: `CanHaveSettlement && !IsBandit`), confirm at least 3 lords are tagged with the matching `culture="Culture.X"` value. If a faction has fewer than 3 matching lords, list it explicitly — this is a data gap, not a code bug, but the user should know the cap of 3 won't be reached for that faction.

Also: are there any TAOM lords tagged with custom culture IDs (`Culture.dunland`, `Culture.rohan`) instead of the engine vanilla IDs (`Culture.empire`, `Culture.vlandia`)? If yes, those lords will be invisible in Custom Battle for the current code path (which assumes the faction button surfaces vanilla engine IDs).

### 4. FINDINGS OR OBSERVATIONS

For each finding:
- File and line number
- Severity (CRITICAL / HIGH / MEDIUM / LOW)
- What the bug is
- How it manifests at runtime
- Recommended fix

If the section is empty, say "NO ISSUES FOUND" and explicitly list what you verified.

---

## QUALITY GATES

- Vanilla code blocks present for every Harmony patch target — NOT optional
- Every Known Suspect has an explicit CONFIRMED or DISPUTED verdict with reasoning
- Every config ID mentioned is cross-referenced against the actual XML — no "I assume X exists"
- No findings without file paths and line numbers
- "I would have to read more code to verify" is an acceptable answer — do not invent verdicts

---

## Prior review lessons (Codex's track record)

SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches in earlier reviews. Vanilla decompilation caught missing gates on perk-checked methods. Lifecycle tracing caught stale caches.

FAILURES: Codex previously assumed `empire = Rohan` (it is Dunland). Codex flagged vanilla-matching code as bugs because it didn't check the vanilla baseline. Codex skipped the harder analysis sections when they required decompilation. Codex hallucinated method signatures from training data instead of decompiling — DO NOT do that here, all signatures must be confirmed against `Modules/CustomBattle/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.CustomBattle.dll`.

---

## Output

Write your findings INTO THIS FILE, replacing this prompt. Use this structure:

```
# Codex Adversarial Review — CustomBattles Filter+Cap (2026-05-04)

## Summary
[N findings: X critical, Y high, Z medium, W low]

## Vanilla code blocks
[paste decompiled snippets here]

## Known Suspects
[CONFIRMED/DISPUTED for each, with reasoning]

## Findings
[detailed findings with severity, file:line, fix]

## Config cross-reference
[per-faction lord count from lords.xml]

## What I verified vs what I couldn't
[transparent list of completion]
```
