# Codex Adversarial Review — CustomBattles NRE fix + diagnostic + LOW fix-loop (2026-05-04)

> **Status:** PROMPT — Codex fills in below. When done, run `/review-codex docs/reviews/codex-adversarial-custombattles-nre-diagnostic-2026-05-04.md`.

## Scope

Focused review of TWO commits that landed today after the filter+cap fix (commit `0cf50ed`, reviewed earlier as Codex Review 30):

- `a9e0bba` — NRE fix (Prefix guard on `OnCharacterSelection`) + rebuilder refactor (use vanilla `SelectorVM<T>.Refresh`) + Phase 2A equipment-slot diagnostic (TEMP)
- `25415b1` — Deep-review LOW fix-loop: log + null-guard if `_onChangeField` reflection fails; log re-entry hint when diagnostic state persists across XML edits; null-safe Equipment slot reads

This is a focused review of the SECOND change to CustomBattles in this session. The original feature (custom battle TAOM factions/commanders/troops) was reviewed at length in `docs/reviews/codex-adversarial-custombattles-2026-04-05.md`; the filter+cap was reviewed at `docs/reviews/codex-adversarial-custombattles-filter-cap-2026-05-04.md`. Do NOT re-review unrelated CustomBattles surface.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar.

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar.

Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale.

NOTE: "rohan" is NOT a valid ID — Rohan uses "vlandia". "dol_guldur" is NOT valid — use "dolguldur". "empire" means Dunland in TAOM.

## READ FIRST

- `docs/features/custom-battles.md` — describes the 9-patch architecture
- `docs/reviews/codex-adversarial-custombattles-filter-cap-2026-05-04.md` — prior review for context
- `docs/reviews/codex-adversarial-custombattles-2026-04-05.md` — even earlier original-feature review
- The two commits in scope: `git show a9e0bba` and `git show 25415b1`

## Files in scope (review ONLY these)

### Production
- `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_OnCharacterSelection_Patch.cs` (NEW in `a9e0bba`) — defensive Prefix on private `CustomBattleSideVM.OnCharacterSelection(SelectorVM<CharacterItemVM>)`. Returns `false` (skip vanilla body) when `selector?.SelectedItem == null`.
- `Main/Features/CustomBattles/Hooks/CommanderSelectorRebuilder.cs` (MODIFIED in `a9e0bba`, then again in `25415b1`) — refactored to use vanilla `SelectorVM<T>.Refresh(items, 0, existingOnChange)`. Reads `_onChange` via `AccessTools.Field` cached at `Initialize()`. `25415b1` adds a logger and a guard that bails if `_onChangeField` is null (instead of passing null to Refresh and severing wiring).
- `Main/Features/CustomBattles/Hooks/SideCommanderFilter.cs` (MODIFIED in `a9e0bba`, then again in `25415b1`) — added a TEMP one-shot equipment-slot diagnostic that logs each commander's slot resolution to `rgl_log.txt` once per culture-switch. `25415b1` wraps the per-commander read in try/catch with a null guard on `c.Equipment`, and adds a one-time hint log when re-entering an already-diagnosed culture (so testers know to relaunch Bannerlord to re-capture).
- `Main/Features/CustomBattles/CustomBattlesIoC.cs` (MODIFIED in `25415b1`) — single-line change: `CommanderSelectorRebuilder.Initialize(logger)` (was zero-arg).

### Out of scope
Everything else in `Main/Features/CustomBattles/` was reviewed in prior Codex passes and not touched by these commits.

## Vanilla targets (decompile against installed v1.3.15 — NOT `E:\Decompiled_Bannerlord\` which is v1.4)

- `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/CustomBattle/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.CustomBattle.dll` -- `CustomBattleSideVM`
- `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.ViewModelCollection.dll` -- `SelectorVM<T>` (specifically `Refresh(IEnumerable<T>, int, Action<SelectorVM<T>>)` overload, `_onChange` private field, `SelectedItem` and `SelectedIndex` properties)
- `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.dll` -- `Equipment` indexer, `EquipmentIndex` enum, `EquipmentElement.Item`, `BasicCharacterObject.Equipment`

## Known Suspects (CONFIRM or DISPUTE each)

1. **Prefix `bool` return semantics.** The new Prefix returns `bool` to skip the vanilla body when `SelectedItem` is null. Harmony interprets `false` as "skip original AND skip postfix-modifies-result". Verify: are there ANY postfixes on `CustomBattleSideVM.OnCharacterSelection` (vanilla mods, BUTR, MCM, or other TAOM patches) that we'd skip silently? Grep TAOM source. If any exist, the Prefix should return `true` after manually doing the null-skip work, not `false`.

2. **`Refresh` overload resolution ambiguity.** TAOM's call is `selector.Refresh(items, 0, existingOnChange)` where `items` is `IEnumerable<CharacterItemVM>`. There are three Refresh overloads on `SelectorVM<T>`: `IEnumerable<string>`, `IEnumerable<TextObject>`, and `IEnumerable<T>`. C# overload resolution should pick the third (T = `CharacterItemVM`), but verify the compiled IL targets the correct overload. (Specifically: would a future TaleWorlds DLL update change the overload set in a way that ambiguates this call?)

3. **`existingOnChange` round-trip preserves identity.** The rebuilder reads `_onChange` from the selector via reflection, then passes it to `Refresh(items, 0, existingOnChange)`. Vanilla Refresh assigns `_onChange = onChange` (overwrite). So we read the same delegate we write back. Verify the cast `(Action<SelectorVM<CharacterItemVM>>)_onChangeField.GetValue(selector)` is correct — specifically: is the runtime field type EXACTLY `Action<SelectorVM<CharacterItemVM>>` or is it `Action<SelectorVM<T>>` (open generic)? If the runtime field is the open generic, the cast may throw at runtime in some scenarios.

4. **Diagnostic try/catch swallows real bugs.** `25415b1` wraps the per-commander equipment-slot read in try/catch. The exception path logs a `LogWarning` with `ex.Message` only — no stack trace. If the diagnostic crashes for an unrelated reason (e.g., MBObjectManager mid-load state), we lose diagnostic value AND mask a real exception. Should the catch log `ex.ToString()` or rethrow?

5. **Diagnostic null-guard on `c?.Equipment`.** The `?.` traverses to Equipment. If `c` is non-null but `Equipment` getter throws (rather than returns null), the catch handles it. If Equipment returns a default `MBEquipmentRoster.EmptyEquipment` (which is the documented behavior when `_equipmentRoster` is null), the indexer still works and we'd see all slots = INVALID. Both outcomes are useful diagnostic data — verify neither path crashes.

6. **`_resetHintLogged` is a single bool, not per-culture.** The hint fires ONCE for the entire process lifetime, on the first re-entry into ANY already-diagnosed culture. After that, subsequent re-entries are silent. Is that intent? Or should each culture get its own hint? (Argument for current behavior: avoids log spam. Argument against: if the user only re-enters faction X repeatedly, they get no signal that culture Y was also diagnosed.)

7. **Initialize ordering.** `CommanderSelectorRebuilder.Initialize(logger)` is called from `CustomBattlesIoC.InitializeHooks(...)`, which runs at line 133 of `Main/SubModule.cs` AFTER `_harmony.PatchCategory("Patch19_CustomBattles")` at line 108. The new Prefix patch has no static state so the gap is harmless for it. The new logger field on `CommanderSelectorRebuilder` IS static state read by `Apply` — but `Apply` is only called from the rebuilt RefreshValues/OnCultureSelection postfixes, which only fire when the user opens Custom Battle (well after `OnSubModuleLoad` finishes). Verify nothing in the new commits could trigger `Apply` between PatchCategory and InitializeHooks.

## REQUIRED SECTIONS

### 1. VANILLA CODE

Decompile and paste 5-15 line snippets from the installed v1.3.15 DLL:

- `CustomBattleSideVM.OnCharacterSelection(SelectorVM<CharacterItemVM>)` body (private)
- `SelectorVM<T>.Refresh(IEnumerable<T>, int, Action<SelectorVM<T>>)` body (the third overload)
- `SelectorVM<T>.SelectedIndex` setter (already covered in prior reviews — link, don't re-paste, unless changed)
- `BasicCharacterObject.Equipment` getter and `Equipment[EquipmentIndex]` indexer

If any signature differs from what TAOM assumes, flag it as INCOMPATIBLE.

### 2. SCENARIO TRACES

**Scenario A — User clicks faction X for the first time, X has 2 commanders:**
1. `OnCultureSelection_Patch.Postfix` calls `_filter.ResolveCommandersForCulture("empire_w")` → service returns 2 commander IDs → adapter resolves to 2 BasicCharacterObjects → diagnostic logs 2 lines to rgl_log.txt → returns 2 commanders.
2. `CommanderSelectorRebuilder.Apply(selector, [c1, c2])` runs:
   - `selector != null && commanders.Count == 2 > 0` → enters body
   - `_onChangeField != null` (Initialize ran successfully)
   - `existingOnChange` = the bound `OnCharacterSelection` delegate
   - `items` = `[CharacterItemVM(c1), CharacterItemVM(c2)]`
   - `selector.Refresh(items, 0, existingOnChange)` → vanilla Refresh:
     - `ItemList.Clear()`
     - `_selectedIndex = -1`
     - foreach add → ItemList = [vm1, vm2]
     - `HasSingleItem = false`
     - `_onChange = existingOnChange` (no-op, same delegate)
     - `SelectedIndex = 0` → setter: `0 != -1`, so `_selectedIndex = 0`, `SelectedItem = ItemList[0] = vm1`, `_onChange.Invoke(this)` → `OnCharacterSelection`
3. New Prefix on OnCharacterSelection: `selector?.SelectedItem != null` → `selector.SelectedItem == vm1`, non-null → returns `true` → vanilla body runs, `SelectedCharacter = vm1.Character`.

CONFIRM the visible commander matches `vm1.Character`. CONFIRM no exception path.

**Scenario B — User clicks faction X again (same faction, second click):**
1. `OnCultureSelection_Patch.Postfix` calls `ResolveCommandersForCulture("empire_w")` → service returns same 2 IDs → adapter resolves to same 2 BasicCharacterObjects → diagnostic CHECK: `_diagnosedCultures.Contains("empire_w")` is true (logged on first call) → if `!_resetHintLogged`, logs the relaunch hint and sets `_resetHintLogged = true` → returns 2 commanders.
2. Same Apply path as Scenario A. CharacterSelectionGroup.ItemList is cleared and refilled with the SAME 2 commanders (idempotent). SelectedIndex transitions from 0 (first click) to -1 (Refresh internal) to 0 (Refresh's set), so the setter actually fires this time. OnCharacterSelection runs with non-null SelectedItem.

CONFIRM no UI flicker beyond the rebuild itself. CONFIRM the relaunch hint fires exactly once across the whole session.

**Scenario C — User clicks a faction that has zero TAOM lords (data mismatch):**
1. `ResolveCommandersForCulture("badculture")` → service returns empty list → adapter resolves to empty list → diagnostic skips (foreach over empty list is a no-op) → returns empty.
2. `OnCultureSelection_Patch.Postfix` checks `commanders.Count == 0` → logs LogWarning → returns early WITHOUT calling Apply. Selector retains whatever was there.

CONFIRM the existing dropdown stays valid (not cleared to empty) and the user can still pick a previously-selected commander.

**Scenario D — Future Bannerlord update renames `_onChange` to `_onChangeCallback`:**
1. At Initialize, `AccessTools.Field` returns null. `_logger.LogError(...)` fires with the diagnostic message.
2. User opens Custom Battle, clicks a faction. `OnCultureSelection_Patch.Postfix` calls Apply.
3. Apply checks `_onChangeField == null` → fires LogError → returns WITHOUT calling Refresh.
4. CharacterSelectionGroup retains whatever vanilla put there; no rebuild, no filter, no cap. NRE Prefix still active.

CONFIRM this is the intended graceful-degrade behavior. The dropdown shows the unfiltered list (degraded UX) but the game does NOT crash.

### 3. CONFIG CROSS-REFERENCE

Not applicable to this commit (no XML/JSON changes). Code-only.

### 4. FINDINGS OR OBSERVATIONS

For each finding:
- File and line number
- Severity (CRITICAL / HIGH / MEDIUM / LOW)
- What the bug is
- Recommended fix

If no findings, say "NO ISSUES FOUND" and explicitly list what you verified.

## QUALITY GATES

- Vanilla code blocks present for every Harmony patch target — NOT optional
- Every Known Suspect has an explicit CONFIRMED or DISPUTED verdict with reasoning
- No findings without file paths and line numbers
- "I would have to read more code to verify" is an acceptable answer — do not invent verdicts

## Prior review lessons (Codex's track record)

SUCCESSES: Decompiling property setters to find no-op early-return guards (CustomBattles filter+cap review 30 — caught the `SelectorVM<T>.SelectedIndex` setter `if (value != _selectedIndex)` short-circuit). Tracing tick-rate vs wall-clock semantics on user-visible timers (Career cooldown review 31). IEEE-754 special-value enumeration for user-facing float validation (Career cooldown review 31).

FAILURES: Codex previously assumed `empire = Rohan` (it is Dunland). Codex flagged vanilla-matching code as bugs because it didn't check the vanilla baseline. Codex hallucinated method signatures from training data — DO NOT do that here, verify all signatures against the installed DLLs.

## Output

Write your findings INTO THIS FILE, replacing this prompt. Use this structure:

```
# Codex Adversarial Review -- CustomBattles NRE + Diagnostic + LOW fix-loop (2026-05-04)

## Summary
[N findings: X critical, Y high, Z medium, W low]

## Vanilla code blocks
[paste decompiled snippets here]

## Known Suspects
[CONFIRMED / DISPUTED with reasoning, one section per suspect]

## Findings
[detailed findings with severity, file:line, fix]

## What I verified vs what I couldn't
[transparent list]
```
