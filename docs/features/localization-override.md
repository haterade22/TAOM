# Localization Override (Vanilla String Patching)

## Overview

Patches `MBTextManager.GetLocalizedText` so that hardcoded vanilla Bannerlord English strings (313 registered ids today) can be overridden through the TAOM strings XML, including phrases like "The Empire" → "Gondor" or capitalization fixes. These vanilla string IDs aren't reachable through Bannerlord's standard `Languages/` translation pipeline because vanilla short-circuits the localization lookup for English. This feature is the **only intervention point** for fixing typos and replacing baked-in faction phrasing in English text. It applies **only while the game language is English** (#706): in every other language vanilla reads that language's translated row, and the override stays out of its way.

## Distinction from `localization.md`

This feature is **not** about TAOM's added translation strings. There are two related but disjoint concerns:

| Doc | Scope |
|---|---|
| [localization.md](localization.md) | TAOM's 1,773 added translation strings shipped in `Languages/<locale>/` for 12 languages. Pure data, no C#. |
| **localization-override.md** (this doc) | A Harmony Prefix on `MBTextManager.GetLocalizedText` that overrides the registered English string IDs (313 today) at runtime, in English only. C# only. |

If a string already worked through TAOM's translation files, you wouldn't need this feature. If you only had this feature, you couldn't translate to French. They cover different gaps — keep them separate.

## Why This Exists

- **Vanilla behavior:** `MBTextManager.GetLocalizedText("{=SomeID}default text")` short-circuits in English: it returns `"default text"` and never asks `LocalizedTextManager` whether anyone registered an override for `SomeID`. This means a `module_strings.xml` entry like `<string id="..." text="{=SomeID}New Text">` is silently ignored when the game language is English.
- **TAOM requirement:** TAOM needs to overwrite vanilla English phrasing for LOTR immersion (e.g. cultural names that vanilla bakes into C# strings rather than reading from culture XML). The loader registers 313 ids from `taom_module_strings.xml` (181 vanilla-style ids, 90 `aom_*` keys and 42 `TAOM_*` keys, counted 2026-10-03).
- **Without this feature:** Every time `MBTextManager.GetLocalizedText` is called with a vanilla `{=ID}` token, the original baked text wins — TAOM overrides for those IDs are dead bytes in the XML.
- **Why English only (#706):** the short-circuit above is an English-only behavior. In any other language vanilla looks up that language's row, and TAOM ships a translated row for every registered id in all twelve language files. An override applied there would hide the player's own language, so the prefix steps aside unless the active language is English.

## Architecture

### Design Challenge

`MBTextManager.GetLocalizedText` is a static method on a TaleWorlds-internal class. The English short-circuit is inside the method body — there is no flag, hook, or override registry to consult. The only way to inject behavior is a Harmony Prefix that runs ahead of the short-circuit.

The override registry has to be populated *before* the patch goes live (otherwise vanilla text shows for the first call), so it's loaded synchronously during `OnSubModuleLoad`.

### Solution Approach

1. **Loader.** [LocalizationOverrideLoader](../../Main/Features/LocalizationOverride/LocalizationOverrideLoader.cs) parses `Main/_Module/ModuleData/taom_module_strings.xml`. For every `<string text="...">` entry whose value starts with `{=ID}`, it extracts the `ID` and the rest-of-text. IDs starting with `taom_` are skipped (those are TAOM's own strings, handled by the normal localization pipeline). IDs `!` and `*` are skipped (they're Bannerlord's "always-translate" / "no-localization" sentinels). Everything else is a candidate vanilla-string override and gets recorded in a `Dictionary<string, string>`.
2. **Registration.** Inside `SubModule.OnSubModuleLoad` ([Main/SubModule.cs:275-289](../../Main/SubModule.cs)), the patch category is applied (`TryPatchCategory("Patch25_LocalizationOverride")`, the guarded apply: a category that fails to patch is logged and does not stop module load) and then the loader runs. Each parsed override is registered with `MBTextManager_GetLocalizedText_Patch.RegisterOverride(id, text)`.
3. **Patch.** [MBTextManager_GetLocalizedText_Patch](../../Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs) is a HarmonyPrefix. It parses the input text for a `{=ID}` prefix, asks the [OverrideLanguageGate](../../Main/Features/LocalizationOverride/OverrideLanguageGate.cs) whether the text language is English (if not, it returns `true` and vanilla runs), looks up the ID in the static override dictionary (keyed by an [IdSlice](../../Main/Features/LocalizationOverride/IdSlice.cs) of the input, so the lookup allocates nothing), and if found assigns `__result = overrideText` and returns `false` (skipping vanilla). If no override is registered, returns `true` (let vanilla run).

The override dictionary is process-static. There's no per-save state and no runtime mutation API exposed to other features — it's load-once at module init. `ClearOverrides()` exists but is intended for tests, not gameplay.

### English only (#706)

Until #706 the prefix applied in every language. For a non-English player that hid the translated row of every registered id: 313 strings, among them the Abanissa and Shaghana culture names and descriptions, their clan names and their notables, and all of them have a row in each of the twelve language files (checked 2026-10-03). Vanilla skips its dictionary only for English (`MBTextManager.GetLocalizedText`, v1.5.3 `MBTextManager.cs:264-283`): `_activeTextLanguageId == "English"` returns the inline text, and any other language reads `LocalizedTextManager.GetTranslatedText`, falling back to the inline text only when that language has no row. So the table has nothing to fix outside English.

[OverrideLanguageGate](../../Main/Features/LocalizationOverride/OverrideLanguageGate.cs) makes the call, and the prefix asks it after its cheap structure checks and before the dictionary probe:

- **The predicate is vanilla's:** the language id equals `"English"`, ordinal (`English`, `Deutsch`, `Français` and the rest are the ids in the `language_data.xml` files). Any other value, including `null`, turns the overrides off for that lookup.
- **The language is read on every call** through `ITextLocalizerAdapter.ActiveLanguage` (over `MBTextManager.ActiveTextLanguage`), so a change while the game runs takes effect at once. It is not cached and there is no patch on `MBTextManager.ChangeLanguage` to keep a cache current: that would add a second patch to save a string compare that vanilla runs on every lookup too. Measured on 2026-10-03 in a throwaway Debug-build loop, the gate costs about 7 to 9 ns per lookup, and a lookup in another language now ends before the dictionary probe: about 27 ns for the whole prefix, against about 65 ns for an English lookup of a registered id.
- **One INFO line per switch**, written when a lookup first sees the new language and never again for it, into TAOM's own log (`Logs/taom_debug_<timestamp>.log`): `[LocalizationOverride] Text language 'Deutsch' is not English: ...` when the language changes to a non-English one, and `[LocalizationOverride] Text language is English again: the English string overrides apply` when it changes back to English from one. A session that starts in English writes neither, since nothing was off before it. The only state is the last language seen, held in one interlocked field because a text can resolve on any thread; `NoteLanguageChange` is that latch step, internal so a test can call it directly. The logger is resolved from the container inside that rare path, and a logger that cannot be resolved costs one failed attempt, never a failed lookup.
- **Left to vanilla:** a language with no row for an id shows the call site's inline English, as vanilla does for every other string, and no longer gets TAOM's override as a stand-in. None of today's twelve languages is in that state for the 313 ids.

### Component Diagram

```
taom_module_strings.xml
        |
LocalizationOverrideLoader.ParseOverridesFromFile
   filters {=ID} entries, skips taom_*, !, *
        |
   Dictionary<string, string> {ID → overrideText}
        |
   foreach kvp:
     MBTextManager_GetLocalizedText_Patch.RegisterOverride(kvp.Key, kvp.Value)
        |
   _overrides static dictionary
        |
+-------+
|
v
MBTextManager.GetLocalizedText  (HarmonyPrefix)
   parse "{=ID}" prefix
   if OverrideLanguageGate says the text language is not English → return true (vanilla reads that language's row)
   if _overrides.TryGetValue(id) → __result = override; return false
   else → return true (vanilla runs)
```

## Configuration

No dedicated config file. Overrides live alongside TAOM's added strings in [Main/_Module/ModuleData/taom_module_strings.xml](../../Main/_Module/ModuleData/taom_module_strings.xml). The loader auto-discriminates:

| Entry shape | Treated as |
|---|---|
| `<string id="..." text="{=taom_xxx}..." />` | TAOM-added string (handled by the normal pipeline; ignored here) |
| `<string id="..." text="{=Whz5HQX9}..." />` (vanilla-style ID) | Override target — registered for vanilla string ID `Whz5HQX9` |
| `<string id="..." text="plain text without {=...}" />` | Skipped by the loader (no `{=` prefix) |
| `<string text="{=!}..." />` or `{=*}` | Skipped (sentinel) |

The patch is gated on Harmony category `Patch25_LocalizationOverride`. To disable feature-wide, comment out the `TryPatchCategory("Patch25_LocalizationOverride");` line in [Main/SubModule.cs:275](../../Main/SubModule.cs).

## Key Files

| File | Purpose |
|---|---|
| [Main/Features/LocalizationOverride/LocalizationOverrideLoader.cs](../../Main/Features/LocalizationOverride/LocalizationOverrideLoader.cs) | XML parser — extracts vanilla-ID overrides from `taom_module_strings.xml` |
| [Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs](../../Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs) | HarmonyPrefix on `MBTextManager.GetLocalizedText` + static override registry |
| [Main/Features/LocalizationOverride/OverrideLanguageGate.cs](../../Main/Features/LocalizationOverride/OverrideLanguageGate.cs) | Decides whether the text language is English and logs once per switch away from English and once per return to it (#706) |
| [Main/Adapters/TextLocalizerAdapter.cs](../../Main/Adapters/TextLocalizerAdapter.cs) | The shared `ITextLocalizerAdapter`: `ActiveLanguage` reads `MBTextManager.ActiveTextLanguage` |
| [Main/SubModule.cs:275-289](../../Main/SubModule.cs) | Patch category application + loader invocation |
| [Main/_Module/ModuleData/taom_module_strings.xml](../../Main/_Module/ModuleData/taom_module_strings.xml) | The data source (mixed: TAOM-added strings + vanilla overrides, distinguished by ID prefix) |

No IoC registration and no service interface: the feature is static-only, so the patch builds its `OverrideLanguageGate` itself over the shared language adapter, and a test swaps it through `UseLanguageGate`.

## Dependencies

- `TaleWorlds.Localization.MBTextManager` (Harmony target)
- `HarmonyLib.AccessTools` (resolves the target method)
- `IPathService` (Core/Infrastructure) — used to compose the path to `taom_module_strings.xml`
- `IModLogger` (Core/Logging): logs the override count and any load errors; the language gate resolves it from the container only when it writes its line
- `ITextLocalizerAdapter` (Main/Adapters): the active text language, so the gate never touches `MBTextManager` directly (ADR-007)

## Tests

- [TAOM.Tests/Features/LocalizationOverride/LocalizationOverrideLoaderTests.cs](../../TAOM.Tests/Features/LocalizationOverride/LocalizationOverrideLoaderTests.cs) — **6 tests**: valid `{=ID}` parse, malformed input, empty XML, sentinel skips (`!`, `*`), `taom_` prefix filtering, multiple-entry parsing.
- [TAOM.Tests/Features/LocalizationOverride/MBTextManager_GetLocalizedText_PatchTests.cs](../../TAOM.Tests/Features/LocalizationOverride/MBTextManager_GetLocalizedText_PatchTests.cs), **22 tests**: registered ID returns override, unregistered ID falls through, sentinel handling, malformed input, register/clear semantics, in-place id matching (case, an id that is a prefix of another in either direction, the empty id, 500 ids), no `Substring` on the probe path, and the language rule (#706): English returns the override, a non-English language falls through, a language change mid-run is followed in both directions, and the gate the game builds (the real adapter, `RequiresGame`) opens in the engine's default English. [IdSliceTests.cs](../../TAOM.Tests/Features/LocalizationOverride/IdSliceTests.cs) (**5 tests**) pins the slice key itself: equal slices of different strings compare and hash alike.
- [TAOM.Tests/Features/LocalizationOverride/OverrideLanguageGateTests.cs](../../TAOM.Tests/Features/LocalizationOverride/OverrideLanguageGateTests.cs), **19 cases in 15 methods**, over a fake `ITextLocalizerAdapter`: English opens the gate; four shipped non-English ids, an id that differs from `English` only in case, and an empty or null id keep it shut; the language is read on every call; one INFO line per switch away from English (not per lookup, again after a return to English, once for each of two non-English languages, once for an equal id held in another string instance); one INFO line for the return to English, and none for a session that starts in English; and a logger that cannot be resolved costs one attempt and never a failed lookup. The once-per-switch guard is pinned by calling the latch step, `NoteLanguageChange`, directly (the same language twice writes one line, a return to English twice writes one), so that proof does not depend on thread timing. An 8-thread run is kept as a smoke test only: whether threads race in a given run is up to the scheduler, and a build with the guard deleted still passed it (three runs, 2026-10-03).

The Harmony patching itself isn't unit-tested — the prefix logic is exercised by calling the static `Prefix(text, ref __result)` directly with crafted inputs.

## How to Add a New Vanilla-String Override

1. Find the vanilla string ID you want to override. The fastest path: enable `[ENABLE_DEBUGGING]` in your engine_config and grep `rgl_log` for `Localization` lines, or open `Modules/Native/ModuleData/Languages/EN/std_module_strings_xml.xml` and find the offending text.
2. Edit [Main/_Module/ModuleData/taom_module_strings.xml](../../Main/_Module/ModuleData/taom_module_strings.xml). Add a new entry:
   ```xml
   <string id="taom_override_<descriptive>" text="{=VanillaIDFromStep1}New Text Here" />
   ```
   Note: the outer `id` attribute (e.g., `taom_override_my_string`) is ignored by the loader for override purposes — only the `{=...}` prefix in the `text` attribute matters. But TaleWorlds' XML reader still requires a unique outer `id`.
3. Restart Bannerlord (the loader runs once at `OnSubModuleLoad`).
4. **Verify in-game, in English.** Confirm the new text appears (the override never applies in another language; its translations come from the `Languages/<locale>/` rows, see [localization.md](localization.md)). If it doesn't, TAOM's own log (`Logs/taom_debug_<timestamp>.log` under the game's `bin/Win64_Shipping_Client/`, written by `FileLogger`; the line is not in `rgl_log`) should have a `[LocalizationOverride] Registered <N> English string overrides` line; if `<N>` didn't increase by one, the loader didn't recognize your entry, most likely because the `{=...}` prefix is malformed or the ID starts with `taom_`. A session in another language also shows the language gate's `[LocalizationOverride] Text language '<id>' is not English: ...` line in that same file, which is the sign that the override table is being skipped on purpose.

## How to Disable

Comment out the `TryPatchCategory` line at [Main/SubModule.cs:275](../../Main/SubModule.cs):

```csharp
// TryPatchCategory("Patch25_LocalizationOverride");
```

The loader will still run (lines 276-289 do not depend on the category), but its `RegisterOverride` calls land in a dictionary that nothing reads, since the patch is no longer applied. Vanilla short-circuit wins for every call. No harm, no extra cost.

## Changelog

_Most history is in the repository-root `CHANGELOG.md`; this section is the go-forward home for localization-override changes._

- **2026-10-03 (#706):** the overrides apply only while the text language is English. Before, the 313 registered ids showed English in every language and hid their translated rows. `OverrideLanguageGate` added; one INFO line per switch to a non-English language and one when English returns, none for a session that starts in English.

## GitHub Issue

- **Issue:** #706 (English-only overrides). The feature itself predates the mandatory issue-per-feature policy.
- **Status:** Shipping. Stable. #706 is fixed in code; the in-game check owed is German: an Abanissa notable's name and the Abanissa culture name read German.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/lotr-issues.md](./lotr-issues.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/modding/strings-and-localization.md](../modding/strings-and-localization.md)

<!-- backlinks-end -->
