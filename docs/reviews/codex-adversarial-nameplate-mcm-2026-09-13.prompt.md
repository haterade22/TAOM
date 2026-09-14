# Adversarial review: MCM controls for settlement nameplate relation colour and opacity (#596)

You are reviewing the COMMITTED changeset `fe266439` in the TAOM repository (Bannerlord v1.4.8 total-conversion mod, .NET Framework 4.7.2), composed with its parent feature `3a289751` (#591, which you reviewed earlier today; do not re-report #591 findings that were fixed). Try to refute every claim below. Report only defects you can prove from the TAOM source, the installed engine DLLs or the vendored MCM source; state UNVERIFIED where you cannot. Do NOT edit files. Return the full report as your FINAL MESSAGE; do not write any file under docs/reviews/raw yourself (the dispatcher redirects stdout there). Use `git show fe266439` for the exact diff. Ignore any other uncommitted work in the tree.

## Feature in three lines

#591 gave settlement nameplates a relation colour (bar wash, name text, diamond frame) and vanilla's plate transparency through a custom Gauntlet container widget `TaomSettlementPlateWidget` + `SettlementPlatePresenter`, with enemy/allied plates lifted to 0.5 via the Patch38 postfix. #596 puts four controls in MCM (`Map UI / Settlement Nameplates`): a colour toggle, a tint strength (0 to 100%, blends the palette toward identity), a neutral plate opacity (10 to 100, default 35) and a coloured plate opacity (10 to 100, default 50). All are meant to apply live: the Patch38 postfix reads the two opacities through a validated provider on every call, and every plate compares the effective tint against the one it last painted once per frame and repaints on change. MCM v5 raises no event a mod can subscribe to, so polling is the mechanism.

## TAOM ID CHEATSHEET (not exercised by this feature)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, shaghana, abanissa. "rohan" and "dol_guldur" are NOT valid ids.

## READ FIRST

- docs/features/settlement-nameplate-relation.md (Configuration section is #596)
- docs/reviews/rca-settlement-nameplate-relation-2026-09-13.md (both the #591 findings and the "#596 follow-up" table: one fixed doc drift, one disputed performance finding; do not re-report either unless you can show the disposition is wrong)
- .claude/rules/csharp-architecture.md "Config Providers MUST Validate" and "Engine-Float Decision Gates"
- Dependencies/.vendor-source/mcm-v5.11.4.tar.gz (vendored MCM source; read with `tar -xzOf`, do not extract) and the installed `Modules/TAOM.Dependencies/bin/Win64_Shipping_Client/MCMv5.dll`

## Files under review (all in the commit)

C#: Main/Features/SettlementNameplateRelation/INameplateRelationSettingsProvider.cs (new), NameplateRelationSettingsProvider.cs (new), INameplateRelationAlphaService.cs, NameplateRelationAlphaService.cs (now takes the provider; sets the configured opacity by relation instead of raising to a constant), NameplateRelationIoC.cs (provider registration + InitializeWidgetStatics), NameplateRelationPalette.cs (Blend, EffectiveStrength), TaomSettlementPlateWidget.cs (static Settings, per-frame tint compare, palette fields compressed to four entries), Main/Features/TaomSettings.cs (four properties), Main/IoC.cs (end-block call), Main/Features/CoopInterop/CoopSettingsRelevance.cs (four names in Presentation)
Unchanged context: SettlementPlatePresenter.cs, PlateAlphaPolicy.cs, NameplatePaletteEntry.cs, the Patch38 postfix Main/Features/SettlementNameplateFade/Hooks/SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs, the three prefabs under Main/_Module/GUI/Prefabs/Nameplate/
Tests: TAOM.Tests/Features/SettlementNameplateRelation/NameplateRelationSettingsProviderTests.cs (new), NameplateRelationAlphaServiceTests.cs, NameplateRelationPaletteTests.cs, TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs (reflected 229)
Docs: docs/features/settlement-nameplate-relation.md, coop-interop.md, bannerlord-together-compat.md, docs/reference/harmony-patch-registry.md, feature-map.md, CHANGELOG.md

## VANILLA / MCM CODE (decompile and quote; installed binaries are authoritative)

- MCM v5: `MCM.Abstractions.Base.Global.GlobalSettings<T>.Instance`, `BaseSettingsProvider`, `BaseSettingsContainer<T>.GetSettings`, `MCM.Common.PropertyRef.Value`, the settings screen VM (`SettingsVM` or equivalent): ExecuteDone / ExecuteCancel / ExecuteReset / OverrideValues and whether slider edits are previewed into the live instance or held in a copy until Done. `SettingsPropertyVM` handling of `SettingPropertyFloatingInteger` with `ValueFormat "#0"` (storage vs display). The JSON load path (`BaseSettingsJsonConverter.ReadJson` or equivalent) for range checking.
- TaleWorlds.GauntletUI: `WidgetInfo` / `WidgetFactory` property enumeration (does it touch STATIC properties on a widget type? `TaomSettlementPlateWidget.Settings` is static), `EventManager.LateUpdate` dispatch, `Widget.LateUpdate`.
- TaleWorlds.Library.Color.Lerp (extrapolates; the caller clamps).
- SettlementNameplateWidget (v1.4.8): DetermineTargetAlphaValue, UpdateNameplateTransparencyAndBrightness (the text/banner lerp toward 1 and the grid/events SetGlobalAlphaRecursively).

## KNOWN SUSPECTS (CONFIRM or DISPUTE with evidence)

KS1 -- Text alpha anchoring. `PlateAlphaPolicy.TextAlphaFor(plateAlpha)` returns `clamp01(plateAlpha / 0.35)`, anchored on vanilla's neutral minimum. With the new sliders a player can set an opacity BELOW 0.35 (down to 0.10). At neutral opacity 10 the plate settles at 0.10 and the name text at 0.29 even at close range, and under the fade the text now fades before the plate does. The hint says "Opacity of a neutral settlement's plate" and the doc says text stays opaque at close range. Is this a defect (text should stay opaque whenever the plate is at its CONFIGURED resting value) or acceptable? If a defect, propose the anchor: the configured opacity for that relation (needs the widget to know its relation's configured value, which it does through the provider), or min(neutral, coloured) opacity. Give text alpha at close range for opacities 10, 20, 35, 50, 100 for both a neutral and an enemy plate.

KS2 -- MCM apply timing versus the hints. Every hint says "Applies immediately". Determine from the MCM source when `TaomSettings.Instance` actually changes: on each slider move while the screen is open, or only on Done. If only on Done, is "Applies immediately" still truthful from the player's point of view (the map is behind the modal screen either way)? If MCM previews values into the live instance and Cancel restores them, do the plates repaint through the preview and back (harmless) or can a Cancel leave `_appliedTint` mismatched (it cannot: the compare is against the provider's current value each frame; verify)?

KS3 -- Startup window. `NameplateRelationIoC.InitializeWidgetStatics` resolves the provider in `IoC.Configure` (OnSubModuleLoad). The provider caches `TaomSettings.Instance` lazily with `??=`. Confirm MCM has registered the settings object before the first campaign map frame in every launch path (new campaign, load from main menu, load from in-game), and that a saved TAOM.json value is in the instance by then (no first-frame default flash beyond one frame). If MCM registers lazily on first `Instance` access rather than at its own OnSubModuleLoad, does TAOM's first read trigger creation correctly (the `new T().Id` cache-populate branch)?

KS4 -- Static property on a Widget subclass. `TaomSettlementPlateWidget.Settings` is `public static`. Does any engine reflection over widget properties (WidgetInfo constructor, editor attribute scan, `WidgetExtensions`) enumerate static members and choke on an interface-typed static (e.g. trying to build a default value, or an `EditorAttribute` requirement)? Quote the BindingFlags.

KS5 -- Fingerprint and relevance. The four properties are classed Presentation (not simulation-relevant) so the co-op fingerprint ignores them; reflected count moved 225 to 229 and covered stayed 180. Confirm `SettingsFingerprintTests` would fail if a fifth property were added without classification, and that the two docs quoting 243 are the only ones (grep `239`).

KS6 -- The alpha service can now LOWER a target below vanilla (neutral 0.35 to 0.10). With the fade multiplier the far edge is 0 either way. Enumerate every vanilla input (0.35, 0.5, 0.8, 1.0, 0) against opacities at both slider extremes and confirm the postfix never returns NaN, negative, or above 1, and that tracked plates are untouched at all slider values. Also: opacity 100 makes a coloured plate FULLY opaque, which is above vanilla's tracked 0.8; is there any ordering issue where a tracked plate is now DIMMER than an untracked one? State it as a design observation if so.

KS7 -- Hot path cost. Two provider property reads per plate per frame (~863 plates x 60 FPS) plus two per Patch38 call (~3000/sec). Each read: field, null-conditional, `FiniteFloatValidator.IsFiniteInRange` (three compares), one divide. Confirm no allocation and no lock. If `TaomSettings.Instance` is still null (MCM not yet registered), each read calls `GlobalSettings<T>.Instance` (dictionary lookup + `BaseSettingsProvider.Instance?.GetSettings`): does that allocate or lock per call, and how long can that window last?

KS8 -- `SetEntry` and the XML colour attributes. The widget's twelve colour properties now write into four `NameplatePaletteEntry` fields via `SetEntry(ref field, bar, text, frame)`. The prefab loader sets attributes one at a time, so setting `EnemyBarColor` rebuilds the enemy entry with the current text and frame. Confirm the order of attribute application cannot lose a value (each setter reads the other two from the current field) and that the `_paletteDirty` flag ends true after the last attribute so the first paint uses the overridden colours.

## FEATURE-SPECIFIC DEEP ANALYSIS

1. Walk one frame after the player lowers Neutral Plate Opacity from 35 to 20 and returns to the map: postfix returns 0.20 x fade for a neutral plate; item alpha lerps toward it; the presenter mirrors it; `TextAlphaFor(0.20)` = 0.57. Name partially transparent at close range. Compare with what the doc and hint promise.
2. Walk the toggle: off then on within two seconds. Sequence of `_appliedTint` values and paints. Any frame where the bar is white but the text still coloured (paint is atomic per plate: confirm ApplyPalette writes all three in one call).
3. A hand-edited TAOM.json with `NameplateRelationTintStrength: 250` and `NameplateNeutralPlateOpacity: -5`: what each read returns, what MCM's own screen shows for the slider (out-of-range value on a 0..100 slider), and whether MCM clamps on the NEXT save.
4. Two campaigns in one process; MCM Reset to defaults while on the map.
5. Thread safety: the provider's `_settings ??=` from the parallel-update thread (Patch38) and the main thread (widget) at the same time; the four float/bool reads while MCM's main-thread write lands. State what a torn read could produce (nothing: reference and word-size reads).
6. Are the tests honest: `NameplateRelationSettingsProviderTests` can only exercise the null-instance path (MCM absent in tests) plus the static `NormalizePercent`; is the instance path (property reads through a live TaomSettings) covered anywhere, and does it matter?

## CONFIG CROSS-REFERENCE

No ModuleData. Cross-reference the four property names across TaomSettings.cs, CoopSettingsRelevance.cs, the provider, the feature doc table and the CHANGELOG; the slider min/max against the provider constants; the defaults (true, 100, 35, 50) against the provider defaults and the tests.

## FINDINGS OR OBSERVATIONS

Report each finding as: severity (P1 ship-blocking / P2 should fix / P3 nit / observation), file:line, what the code does, what it should do, proof (quoted TAOM and engine/MCM lines). Then the Known Suspects verdicts with numbers. Then anything the two deep reviews (RCA file) missed.

## QUALITY GATES

- Every engine or MCM claim quotes decompiled or vendored source lines.
- No finding on deliberately vanilla behaviour (text opaque at close range at DEFAULT opacities, banner untinted, neutral identity white, tracked plates untouched).
- Do not propose more MCM knobs; the four are the decided set. A better anchor for the text curve is in scope if KS1 is a defect.
- State UNVERIFIED rather than guessing.

## PRIOR REVIEW LESSONS

SUCCESSES: executing the engine's update equations for one frame found the tracked-ring orphan in #591; in-memory probes against the installed widget types found the NaN destination hole; reading the vendored MCM source settled the instance lifecycle in the last deep review.
FAILURES: Codex has flagged vanilla-matching code as bugs, assumed empire=Rohan (it is Dunland), skipped hard sections, and once wrote its report into the transcript file. Do none of these.
