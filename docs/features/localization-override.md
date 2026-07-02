# Localization Override (Vanilla String Patching)

## Overview

Patches `MBTextManager.GetLocalizedText` so that ~120 hardcoded vanilla Bannerlord English strings can be overridden through the TAOM strings XML — including phrases like "The Empire" → "Gondor" or capitalization fixes. These vanilla string IDs aren't reachable through Bannerlord's standard `Languages/` translation pipeline because vanilla short-circuits the localization lookup for English. This feature is the **only intervention point** for fixing typos and replacing baked-in faction phrasing in English text.

## Distinction from `localization.md`

This feature is **not** about TAOM's added translation strings. There are two related but disjoint concerns:

| Doc | Scope |
|---|---|
| [localization.md](localization.md) | TAOM's 1,773 added translation strings shipped in `Languages/<locale>/` for 12 languages. Pure data, no C#. |
| **localization-override.md** (this doc) | A Harmony Prefix on `MBTextManager.GetLocalizedText` that overrides ~120 *vanilla* English string IDs at runtime. C# only. |

If a string already worked through TAOM's translation files, you wouldn't need this feature. If you only had this feature, you couldn't translate to French. They cover different gaps — keep them separate.

## Why This Exists

- **Vanilla behavior:** `MBTextManager.GetLocalizedText("{=SomeID}default text")` short-circuits in English: it returns `"default text"` and never asks `LocalizedTextManager` whether anyone registered an override for `SomeID`. This means a `module_strings.xml` entry like `<string id="..." text="{=SomeID}New Text">` is silently ignored when the game language is English.
- **TAOM requirement:** TAOM needs to overwrite vanilla English phrasing for LOTR immersion (e.g. cultural names that vanilla bakes into C# strings rather than reading from culture XML). About 120 such strings are tracked in `taom_module_strings.xml`.
- **Without this feature:** Every time `MBTextManager.GetLocalizedText` is called with a vanilla `{=ID}` token, the original baked text wins — TAOM overrides for those IDs are dead bytes in the XML.

## Architecture

### Design Challenge

`MBTextManager.GetLocalizedText` is a static method on a TaleWorlds-internal class. The English short-circuit is inside the method body — there is no flag, hook, or override registry to consult. The only way to inject behavior is a Harmony Prefix that runs ahead of the short-circuit.

The override registry has to be populated *before* the patch goes live (otherwise vanilla text shows for the first call), so it's loaded synchronously during `OnSubModuleLoad`.

### Solution Approach

1. **Loader.** [LocalizationOverrideLoader](../../Main/Features/LocalizationOverride/LocalizationOverrideLoader.cs) parses `Main/_Module/ModuleData/taom_module_strings.xml`. For every `<string text="...">` entry whose value starts with `{=ID}`, it extracts the `ID` and the rest-of-text. IDs starting with `taom_` are skipped (those are TAOM's own strings, handled by the normal localization pipeline). IDs `!` and `*` are skipped (they're Bannerlord's "always-translate" / "no-localization" sentinels). Everything else is a candidate vanilla-string override and gets recorded in a `Dictionary<string, string>`.
2. **Registration.** Inside `SubModule.OnSubModuleLoad` ([Main/SubModule.cs:97-111](../../Main/SubModule.cs)), the patch category is applied (`_harmony.PatchCategory("Patch25_LocalizationOverride")`) and then the loader runs. Each parsed override is registered with `MBTextManager_GetLocalizedText_Patch.RegisterOverride(id, text)`.
3. **Patch.** [MBTextManager_GetLocalizedText_Patch](../../Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs) is a HarmonyPrefix. It parses the input text for a `{=ID}` prefix, looks up the ID in the static override dictionary, and if found assigns `__result = overrideText` and returns `false` (skipping vanilla). If no override is registered, returns `true` (let vanilla run).

The override dictionary is process-static. There's no per-save state and no runtime mutation API exposed to other features — it's load-once at module init. `ClearOverrides()` exists but is intended for tests, not gameplay.

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

The patch is gated on Harmony category `Patch25_LocalizationOverride`. To disable feature-wide, comment out the `_harmony.PatchCategory("Patch25_LocalizationOverride")` line in [Main/SubModule.cs:97](../../Main/SubModule.cs).

## Key Files

| File | Purpose |
|---|---|
| [Main/Features/LocalizationOverride/LocalizationOverrideLoader.cs](../../Main/Features/LocalizationOverride/LocalizationOverrideLoader.cs) | XML parser — extracts vanilla-ID overrides from `taom_module_strings.xml` |
| [Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs](../../Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs) | HarmonyPrefix on `MBTextManager.GetLocalizedText` + static override registry |
| [Main/SubModule.cs:97-111](../../Main/SubModule.cs) | Patch category application + loader invocation |
| [Main/_Module/ModuleData/taom_module_strings.xml](../../Main/_Module/ModuleData/taom_module_strings.xml) | The data source (mixed: TAOM-added strings + vanilla overrides, distinguished by ID prefix) |

No services, no IoC, no adapters. Static-only feature.

## Dependencies

- `TaleWorlds.Localization.MBTextManager` (Harmony target)
- `HarmonyLib.AccessTools` (resolves the target method)
- `IPathService` (Core/Infrastructure) — used to compose the path to `taom_module_strings.xml`
- `IModLogger` (Core/Logging) — logs the override count and any load errors

## Tests

- [TAOM.Tests/Features/LocalizationOverride/LocalizationOverrideLoaderTests.cs](../../TAOM.Tests/Features/LocalizationOverride/LocalizationOverrideLoaderTests.cs) — **6 tests**: valid `{=ID}` parse, malformed input, empty XML, sentinel skips (`!`, `*`), `taom_` prefix filtering, multiple-entry parsing.
- [TAOM.Tests/Features/LocalizationOverride/MBTextManager_GetLocalizedText_PatchTests.cs](../../TAOM.Tests/Features/LocalizationOverride/MBTextManager_GetLocalizedText_PatchTests.cs) — **11 tests**: registered ID returns override, unregistered ID falls through, sentinel handling, malformed input, register/clear semantics.

The Harmony patching itself isn't unit-tested — the prefix logic is exercised by calling the static `Prefix(text, ref __result)` directly with crafted inputs.

## How to Add a New Vanilla-String Override

1. Find the vanilla string ID you want to override. The fastest path: enable `[ENABLE_DEBUGGING]` in your engine_config and grep `rgl_log` for `Localization` lines, or open `Modules/Native/ModuleData/Languages/EN/std_module_strings_xml.xml` and find the offending text.
2. Edit [Main/_Module/ModuleData/taom_module_strings.xml](../../Main/_Module/ModuleData/taom_module_strings.xml). Add a new entry:
   ```xml
   <string id="taom_override_<descriptive>" text="{=VanillaIDFromStep1}New Text Here" />
   ```
   Note: the outer `id` attribute (e.g., `taom_override_my_string`) is ignored by the loader for override purposes — only the `{=...}` prefix in the `text` attribute matters. But TaleWorlds' XML reader still requires a unique outer `id`.
3. Restart Bannerlord (the loader runs once at `OnSubModuleLoad`).
4. **Verify in-game.** Confirm the new text appears. If it doesn't, `rgl_log` should have a `[LocalizationOverride] Registered <N> English string overrides` line; if `<N>` didn't increase by one, the loader didn't recognize your entry — most likely because the `{=...}` prefix is malformed or the ID starts with `taom_`.

## How to Disable

Comment out [Main/SubModule.cs:97](../../Main/SubModule.cs):

```csharp
// _harmony.PatchCategory("Patch25_LocalizationOverride");
```

The loader will still run (lines 99-111 are unguarded), but its `RegisterOverride` calls land in a dictionary that nothing reads, since the patch is no longer applied. Vanilla short-circuit wins for every call. No harm, no extra cost.

## Changelog

_No dated entries mapped from the global `CHANGELOG.md` yet — this section is the go-forward home for localization-override changes. See the repository-root `CHANGELOG.md` for full chronological history._

## GitHub Issue

- **Issue:** None — feature predates the mandatory issue-per-feature policy.
- **Status:** Shipping. Stable.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/lotr-issues.md](./lotr-issues.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
