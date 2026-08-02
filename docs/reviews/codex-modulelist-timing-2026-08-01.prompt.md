# Decide one engine-timing question — is the active-module list populated at `OnSubModuleLoad`?

Narrow, decidable investigation. **Bannerlord v1.4.7.** Verify against the INSTALLED DLLs at
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/` — decompile them.
A cache exists at `C:/Users/mikew/.taom-src/v1.4.7/`. Do not answer from memory or from general
Bannerlord knowledge; this needs the actual v1.4.7 call graph.

## The question

`TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules()` returns `List<ModuleInfo>`.

TAOM calls it (by reflection, via `Dependencies/Foundation/IncompatibleModDetector.cs` →
`TryReadActiveModuleIdsViaReflection`) to decide whether a co-op module is present.

**Is that list fully populated by the time a module's `MBSubModuleBase.OnSubModuleLoad()` runs?**

Specifically, trace on v1.4.7:

1. Where does the active-module list get built, and by whom (launcher, `Module.Initialize`,
   `ModuleInfo.LoadWithFullPath`, BLSE/BUTR if they intercept)?
2. What is the exact ordering of: building that list → `Module.LoadSubModules()` → each
   `SubModule` constructor → each `OnSubModuleLoad()` → `OnBeforeInitialModuleScreenSetAsRoot()` →
   `OnGameInitializationFinished()`?
3. Can `GetActiveModules()` return an EMPTY or PARTIAL list during the `OnSubModuleLoad` fan-out?
   If it can, under exactly what conditions?

Answer definitively: **YES it is reliable at `OnSubModuleLoad`**, **NO it is not**, or **it depends on
X** — with the decompiled evidence pasted inline.

## Why it matters

TAOM's `Main/SubModule.cs` → `RegisterUiExtensions` calls `CoopPresence.Refresh()` and then reads
`CoopPresence.IsActive`, during Main's `OnSubModuleLoad`. It uses that to decide whether to skip
registering some UIExtenderEx types (a `[CoopSuppressedUi]` filter) — because a co-op host owns
campaign time, and TAOM's fast-forward widget would otherwise render and do nothing.

Registration is a **one-shot**: a mixin cannot un-inject a widget later, so unlike every other
consumer of this flag (which read live at gameplay time, long after a second `Refresh()` in
`OnGameInitializationFinished`), this decision cannot be corrected afterwards.

If the list is not populated at that moment, the failure is **silent**: `IsActive` returns false, the
ordinary solo registration runs, and nothing logs an error. The code's own comments flag this as
uncertain — `CoopPresence.Refresh()`'s doc says the second probe exists "since ModuleHelper's
active-module list may not be populated at the earlier point." Nobody has established whether that
caution is warranted or superstition.

## Deliverable

1. The verdict, with the decompiled call graph as evidence.
2. If NOT reliable: the earliest lifecycle point on v1.4.7 at which it IS reliable, and whether a
   one-shot UI-registration decision can be made there at all — UIExtenderEx's `Register` must be
   called before `Enable()`, and prefab extensions must be registered before the target movie is
   first built. Say whether `OnBeforeInitialModuleScreenSetAsRoot` is early enough for a `MapBar`
   prefab extension, given the map screen is built much later.
3. If reliable: say so plainly so the redundant `Refresh()` and the surrounding hedging comments can
   be simplified.
4. Note any BLSE / BUTR / `Bannerlord.MBOptionScreen` interception of the module-load path that
   changes the answer in a real player's install versus a vanilla launcher.

Be concise. This is one question; do not review anything else.
