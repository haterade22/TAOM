# MCM Layout Fix (Patch41)

## Overview

Repairs the MCM (Mod Configuration Menu) options screen, which rendered every mod's settings vertically inverted on Bannerlord v1.4.x — group headers below their members, members in reverse `Order`, groups in reverse `GroupOrder`. A single Harmony Postfix flips the reversed layout attributes in MCM's embedded prefabs at registration time.

**Scope note:** MCM itself is a BUTR dependency provided via TAOM.Dependencies (`Bannerlord.MBOptionScreen*.dll` + `MCM.UI.Adapter.MCMv5.dll` + the `Bannerlord.MCM` NuGet — see [docs/migration/dr3-maintenance.md](../migration/dr3-maintenance.md)). This feature owns only TAOM's layout fix for it, nothing else.

## Why This Exists

- **Vanilla/engine behavior:** Bannerlord v1.4.0 fixed a long-standing engine bug where `LayoutMethod="VerticalBottomToTop"` actually rendered top-to-bottom (`McmLayoutRewriter.cs:10-12`). TAOM's own loose prefabs were mass-flipped for this regression in `ad836d1`, but that pass grepped only `Main/_Module/GUI/Prefabs/` (changelog archive, 2026-05-29 entry).
- **The gap:** MCM v5.11.4's options-screen prefabs (`SettingsView`, `SettingsPropertyGroupView`, `ModOptionsView`, `ModOptionsPageView`) are authored against the old engine and still carry `LayoutImp.LayoutMethod="VerticalBottomToTop"` — but they ship as **embedded resources** inside `Bannerlord.MBOptionScreen.v1.4.x.dll`, not as loose XML TAOM could patch on disk (`McmLayoutRewriter.cs:12-19`).
- **Without this feature:** the entire settings list renders inverted for every mod's MCM page — headers under members, reversed ordering (`McmLayoutRewriter.cs:14-16`).

## Architecture

### Design Challenge

The obvious tool — a UIExtenderEx `[PrefabExtension]` `PrefabExtensionSetAttributePatch` — is a **silent no-op** here. MCM registers its embedded prefabs through `WidgetFactoryManager.CreateAndRegister(name, XmlDocument)`, which builds them via UIExtenderEx's `LoadFromDocument` reverse-patch — a path that bypasses the `ProcessMovie` step where `[PrefabExtension]` patches are applied (`McmLayoutRewriter.cs:18-23`, `Patch41_McmLayoutFix.cs:17-20`). The first attempt (commit `312e4693`, `Mcm/UI/McmSettingsLayoutPrefab.cs`) used exactly that mechanism and was structurally dead; commit `f23434b0` deleted it and replaced it with the Harmony approach ("the earlier PrefabExtension attempt was a structural no-op" — `f23434b0` commit message).

### Solution Approach

Postfix on MCM's actual load path. The `Func<WidgetPrefab>` that `CreateAndRegister` registers closes over the **same** `XmlDocument` reference and parses it lazily at first screen-open, so mutating the document in the Postfix — before that parse — repairs the layout (`Patch41_McmLayoutFix.cs:12-15`).

- **Exact Harmony target:** `Bannerlord.UIExtenderEx.ResourceManager.WidgetFactoryManager.CreateAndRegister(string, XmlDocument)` — Postfix, category `Patch41_McmLayoutFix` (`Patch41_McmLayoutFix.cs:26-28`).
- **What the rewriter changes:** every `LayoutImp.LayoutMethod` or `StackLayout.LayoutMethod` attribute valued `VerticalBottomToTop` becomes `VerticalTopToBottom` (`McmLayoutRewriter.cs:30-39, 69-91`). `StackLayout.LayoutMethod` is handled defensively — MCM's prefabs use the `LayoutImp.` spelling (`McmLayoutRewriter.cs:33-34`).
- **Scoping:** only prefabs named in `McmLayoutPrefabNames` (`SettingsView`, `SettingsPropertyGroupView`, `ModOptionsView_MCM`, `ModOptionsView`, `ModOptionsPageView`) are touched, case-insensitive with an optional `.xml` suffix — verified against the embedded `MCM.UI.GUI.Prefabs.*.xml` resources; other mods' embedded prefabs pass through untouched (`McmLayoutRewriter.cs:41-62`).
- **Safety:** null-safe, idempotent (re-running flips 0), and the Postfix try/catch-swallows everything — a cosmetic layout fix must never break MCM screen registration (`McmLayoutRewriter.cs:26, 67`; `Patch41_McmLayoutFix.cs:43-48`).
- **Apply timing:** registered in `SubModule.OnSubModuleLoad` (`Main/SubModule.cs:144`), NOT the late `OnGameInitializationFinished` batch — MCM's `ResourceInjector.Inject()` runs at `OnBeforeInitialModuleScreenSetAsRoot`, after every module's `OnSubModuleLoad`, so the Postfix must already be attached by then (`Main/SubModule.cs:137-143`, `Patch41_McmLayoutFix.cs:22-24`).

### Component Diagram

```
MCM ResourceInjector.Inject()  (OnBeforeInitialModuleScreenSetAsRoot)
        |
WidgetFactoryManager.CreateAndRegister(name, XmlDocument)   [UIExtenderEx]
        |
Patch41_McmLayoutFix (Harmony Postfix — thin, logs + swallows)
        |
McmLayoutRewriter.FlipMcmLayout(name, doc)   (pure, static, testable)
        |
mutated XmlDocument → parsed lazily at first options-screen open
```

## Configuration

None — no config file and no MCM toggle. The fix is hardcoded always-on: it corrects a rendering defect with no tuning surface, and it self-scopes (a future MCM build that ships already-correct prefabs flips 0 attributes and the feature is inert).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Mcm/McmLayoutRewriter.cs` | Pure XML transform: prefab-name gate + attribute flip; returns flip count |
| `Main/Features/Mcm/Hooks/Patch41_McmLayoutFix.cs` | Thin Harmony Postfix on `WidgetFactoryManager.CreateAndRegister`; delegates to the rewriter |
| `Main/SubModule.cs:137-144` | Applies category `Patch41_McmLayoutFix` in `OnSubModuleLoad` (timing-critical) |

No IoC registration, service interface, or adapter — the rewriter is a static pure function on `System.Xml` types only; the patch resolves `IModLogger` lazily (`Patch41_McmLayoutFix.cs:31-32`).

## Dependencies

- **Bannerlord.UIExtenderEx** — owns the patched `WidgetFactoryManager.CreateAndRegister` and the `LoadFromDocument` reverse-patch that necessitates this approach.
- **MCM v5.11.4** (`Bannerlord.MBOptionScreen.v1.4.x.dll`, via TAOM.Dependencies) — the prefab source being repaired.
- `IModLogger` (Core/Logging) — flip-count info log + failure error log.

## Tests

- `TAOM.Tests/Features/Mcm/McmLayoutRewriterTests.cs` — 17 test cases: flips all 3 reversed ListPanels in a `SettingsPropertyGroupView`-shaped doc; leaves `HorizontalLeftToRight` and already-correct panels untouched; handles the `StackLayout.LayoutMethod` spelling; ignores non-MCM prefab names; accepts a `name.xml` suffix; null doc / null-or-empty name return 0 without throwing; idempotency (second run flips 0); and a 9-row data test pinning the exact scoped name set (incl. case-insensitivity and `SettingsPropertyView` correctly out of scope).
- The Harmony Postfix itself is live-game-only (`Not-tested:` trailer in `f23434b0`); success is visible in the log as `[McmLayoutFix] Flipped N VerticalBottomToTop layout(s) in MCM prefab '<name>'`.

## How to React to an MCM Version Bump

1. If the options screen renders correctly and the `[McmLayoutFix]` log lines disappear, the new MCM likely ships corrected prefabs — the fix is inert by design; consider deleting the feature.
2. If the screen inverts again with **no** `[McmLayoutFix]` log lines, the new MCM probably renamed its embedded prefabs: decompile `Bannerlord.MBOptionScreen.v1.4.x.dll`, check the `MCM.UI.GUI.Prefabs.*.xml` resource names, add the new name(s) to `McmLayoutPrefabNames` (`McmLayoutRewriter.cs:44-52`), and add a matching `[DataRow]` to `IsMcmLayoutPrefab_MatchesOnlyTheScopedSet`.
3. If MCM stops loading through UIExtenderEx's `WidgetFactoryManager`, the Postfix target itself is gone — re-research the load path before re-targeting (see the `Research:` trailer on `f23434b0`).

## Changelog

- 2026-05-30 — `f23434b0` fix(ui): MCM options screen top-to-bottom via Patch41 — Harmony Postfix + pure rewriter + 17 tests; deletes the dead UIExtenderEx attempt. Closes #252.
- 2026-05-30 — `312e4693` fix(ui): first attempt via UIExtenderEx `PrefabExtensionSetAttributePatch` (`Mcm/UI/McmSettingsLayoutPrefab.cs`) — superseded same day; structurally a no-op on MCM's embedded-prefab load path.

## GitHub Issue

- **Issue:** #252 — fix(ui): MCM mod-options screen renders bottom-to-top on v1.4.5
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/balance-levers.md](../modding/balance-levers.md)
- [docs/modding/module-dependencies.md](../modding/module-dependencies.md)
- [docs/modding/module-taom.md](../modding/module-taom.md)

<!-- backlinks-end -->
