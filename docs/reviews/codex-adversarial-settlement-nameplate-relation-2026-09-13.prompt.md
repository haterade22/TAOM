# Adversarial review: settlement nameplate relation colour + alpha mirroring (#591)

You are reviewing an uncommitted TAOM changeset (Bannerlord v1.4.8 total-conversion mod, .NET Framework 4.7.2). Try to refute every claim below. Report only defects you can prove from the TAOM source or the installed engine; state UNVERIFIED where you cannot. Do NOT edit files. Return the full report as your FINAL MESSAGE; do not write any file under docs/reviews/raw yourself (the dispatcher redirects stdout there).

## Feature in two lines

Campaign-map settlement nameplates (town/castle/village) now show the player's relation to the owner: a custom Gauntlet widget `TaomSettlementPlateWidget` replaces the plain `SettlementNameplateLayout` container in the three prefab clones, binds `RelationType="@Relation"` a second time, paints the parchment bar / name text / diamond frame from a palette, and every late update mirrors the vanilla item widget's `AlphaFactor` / `ColorFactor` onto the bar and frame (which `Widget.Render` never propagates to children), driving text and banner alpha from the plate alpha. The existing Patch38 postfix on `SettlementNameplateWidget.DetermineTargetAlphaValue` now applies a relation floor (enemy / allied raised from 0.35 to 0.5) before the distance fade multiplier.

## TAOM ID CHEATSHEET (not exercised by this feature; included for completeness)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa. "rohan" and "dol_guldur" are NOT valid ids.

## READ FIRST

- docs/features/settlement-nameplate-relation.md (design, palette, alpha rules, in-game checklist)
- docs/features/settlement-nameplate-fade.md (Patch38, the fade this feature composes with)
- docs/reviews/rca-settlement-nameplate-relation-2026-09-13.md (the deep-review findings already fixed; do not re-report them unless the fix is wrong)
- .claude/rules/gui-ui.md (custom widget and prefab clone rules)

## Files under review

C# (new): Main/Features/SettlementNameplateRelation/TaomSettlementPlateWidget.cs, SettlementPlatePresenter.cs, NameplateRelationPalette.cs, NameplatePaletteEntry.cs, PlateAlphaPolicy.cs, INameplateRelationAlphaService.cs, NameplateRelationAlphaService.cs, NameplateRelationIoC.cs
C# (modified): Main/Features/SettlementNameplateFade/Hooks/SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs, Main/IoC.cs (registration line), Main/SubModule.cs (Initialize call near line 1563)
Prefabs (modified): Main/_Module/GUI/Prefabs/Nameplate/SettlementNameplateItemLarge.xml, SettlementNameplateItemMedium.xml, SettlementNameplateItemSmall.xml (the `TaomSettlementPlateWidget` element at line 89 / 88 / 89)
Tests (new): TAOM.Tests/Features/SettlementNameplateRelation/NameplateRelationPaletteTests.cs, PlateAlphaPolicyTests.cs, NameplateRelationAlphaServiceTests.cs, NameplateRelationPrefabTests.cs, NameplateRelationBindingTests.cs

## VANILLA CODE (decompile and paste the relevant lines; signatures from the INSTALLED DLLs are authoritative)

Use `pwsh tools/taom-src.ps1 path <Full.Type.Name>` (cache under ~/.taom-src/v1.4.8) or the ilspy MCP. The dump under E:\Decompiled_Bannerlord\ is v1.4.8 too but treat it as browsing only.

- TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate.SettlementNameplateWidget: RelationType setter, SetNameplateRelationType, OnParallelUpdate, UpdateNameplateTransparencyAndBrightness, DetermineTargetAlphaValue, DetermineTargetColorFactor, UpdateTutorialState.
- TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate.SettlementNameplateItemWidget (whole class).
- TaleWorlds.GauntletUI.BaseTypes.Widget: Render, OnRender, LateUpdate / OnLateUpdate, IsVisible / IsHidden setters, SetState and UpdateChildrenStates, FindChild overloads, Color / AlphaFactor / ColorFactor.
- TaleWorlds.GauntletUI.BaseTypes.BrushWidget: Brush and ReadOnlyBrush getters; TaleWorlds.GauntletUI.BaseTypes.TextWidget and MaskedTextureWidget inheritance chain.
- TaleWorlds.GauntletUI.EventManager: Update, ParallelUpdate, LateUpdate, OnWidgetConnectedToRoot (which containers a widget is registered in and whether visibility gates the LateUpdate call).
- TaleWorlds.GauntletUI.UIContext / TaleWorlds.ScreenSystem.ScreenManager / GauntletLayer: the order of Update, LateUpdate and Render within one frame.
- TaleWorlds.GauntletUI.Data.GauntletView (or the binding classes it uses): what happens on initial data-source bind for an `@Property` whose current VM value equals the widget's default, and what a widget-side OnPropertyChanged does (write-back into the VM through SetPropertyValue?).
- TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttributeFromString(Aux): Widget-typed and Color-typed attribute handling; WidgetTemplate instantiation order (children created before attributes are applied?).
- SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM: Relation property and setter guard, RefreshRelationStatus, RefreshBindValues, IsVisible(cameraPosition); SettlementNameplatesVM.Initialize / Update / the event handlers that call RefreshRelationStatus.

## KNOWN SUSPECTS (CONFIRM or DISPUTE each with evidence)

KS1 -- Initial relation push. `TaomSettlementPlateWidget.RelationType` defaults to -1 and the VM's `Relation` defaults to 0 with a change-guarded setter. If the initial data-source bind does NOT push an unchanged 0 into the widget, the widget stays at -1 forever for neutral plates. The palette maps -1 to neutral, so the claim is that this is harmless; confirm from the GauntletView bind path whether the initial value is pushed at all, and whether any later Refresh re-pushes.

KS2 -- Frame ordering of the text alpha fight. Vanilla's OnParallelUpdate lerps the name text and banner `Brush.GlobalAlphaFactor` toward 1 every frame; the widget's OnLateUpdate overwrites them with `PlateAlphaPolicy.TextAlphaFor(itemAlpha)`. The claim is Update -> ParallelUpdate -> LateUpdate -> Render within one frame, so the late-update value is what renders and there is no flicker. Confirm the order from ScreenManager / GauntletLayer / UIContext, including whether Render can run between ParallelUpdate and LateUpdate.

KS3 -- Widget-to-VM write-back. The widget calls `OnPropertyChanged(value, "RelationType")` in its setter, and both the root SettlementNameplateWidget and the plate bind `@Relation`. Does a widget-side OnPropertyChanged for a bound property write back into `SettlementNameplateVM.Relation` (public setter) through GauntletView? If so, can the two bindings ping-pong, or can the widget's -1 default ever be written into the VM?

KS4 -- Hidden plates. When IsVisibleOnMap goes false, vanilla lerps the item alpha to 0 and then sets the root `IsVisible = false`. Confirm that (a) the plate widget's OnLateUpdate still runs (enrolment by override, not by visibility) and stays cheap because `PlateAlphaPolicy.TryTakeChange` sees an unchanged pair, and (b) nothing in the hidden path leaves the bar at a stale non-zero alpha when the plate becomes visible again.

KS5 -- Patch38 composition. The postfix now does: early return on `__result <= 0f`; `__result = Adjust(__result, RelationType, IsTracked)`; `__result *= fade`. With the fade disabled the multiplier is 1. Confirm `DetermineTargetAlphaValue` returns 1f for tracked-and-outside-window, that Adjust leaves that alone (isTracked), and that Harmony passes `__result` by ref for a private instance method returning float so both writes land. Also: is `IsTracked` the same flag `DetermineTargetAlphaValue` reads (no separate `_isTracked` field that lags the property)?

KS6 -- Path attributes vs construction order. The plate's Widget-typed attributes (`BarBackgroundWidget="BarContainer\SettlementBarBackgroundWidget"` etc.) are resolved by `FindChild(BindingPath)` from the plate element. Confirm that WidgetTemplate creates the whole subtree before applying attributes, so the paths resolve on the first pass and the Id fallback in `ResolveReferences` never runs on a correct prefab. Also confirm a null result on a miss (not a throw).

KS7 -- SetState cascade. The capsule `ButtonWidget` above the plate changes state on hover / press. Confirm `UpdateChildrenStates` defaults false and is not set in the prefabs, so the text widget never leaves its Default style (TAOM's three nameplate text brushes define only Default). Also confirm the tutorial `SetState("Disabled")` on the root does not cascade.

KS8 -- Colour string format. Every default in `NameplateRelationPalette` is `#RRGGBBAA` because `Color.ConvertStringToColor` reads `Substring(7, 2)`. Confirm from the installed TaleWorlds.Library that a 7-character value throws, and whether the prefab loader catches it (blank movie) or crashes.

## FEATURE-SPECIFIC DEEP ANALYSIS

1. Trace one frame for an ENEMY plate inside the window, untracked, fade disabled: vanilla target 0.35 -> Adjust -> 0.5 -> item alpha lerps to 0.5 -> plate widget mirrors 0.5 onto bar and frame -> TextAlphaFor(0.5) = 1 -> text opaque. State what renders for bar, frame, text, banner, and whether that matches the feature doc.
2. Same for a NEUTRAL plate in the fade band with the fade multiplier at 0.5: target 0.35 x 0.5 = 0.175 -> TextAlphaFor = 0.5. Does the whole plate fade together?
3. Declare war mid-session: SettlementNameplatesVM.OnWarDeclared -> RefreshRelationStatus -> next RefreshBindValues -> Relation changes 0 -> 2 -> both bindings push -> the plate widget's setter marks dirty -> next OnLateUpdate repaints. Any path where the VM refreshes relation without the widget seeing it (e.g. a nameplate VM created after the event, a plate re-created by the manager widget's item template)?
4. Settlement changes owner to the player's own clan: relation 1. Confirm the per-settlement VM (not the manager) is the one refreshed, and that bound villages are refreshed too (SettlementNameplatesVM.OnSettlementOwnerChanged).
5. New campaign in the same process after a previous one: any static state in the feature? (The patch holds two singleton service refs only.) Any per-widget state that survives a UI context change?
6. Thread safety: OnParallelUpdate runs on worker threads and writes item.AlphaFactor / ColorFactor; OnLateUpdate reads them on the main thread after the parallel phase. Confirm no concurrent read/write within the same phase.
7. Performance: ~863 plates, OnLateUpdate every frame regardless of visibility. Count the work per call after references resolve (null checks, two float reads, two compares, two brush reads) and confirm no allocation. Check `ReadOnlyBrush` does not clone. Check `FindChild(id, true)` allocates (GetAllChildrenRecursive) and is bounded by MaxResolveAttempts = 120 per widget.
8. The prefab tests parse the XML with `XDocument` and mirror `FindChild` (direct Children by Id per segment). Would they fail on a renamed Id, a moved element, a second `TaomSettlementPlateWidget`, or a 7-character colour attribute? Is anything about the prefab that the widget depends on NOT pinned (e.g. the plate must be a descendant of `SettlementNameplateItemWidget` within 4 levels)?
9. NaN: for every gate on an engine float (item.AlphaFactor, item.ColorFactor, __result, DistanceToCamera), state what happens on NaN and whether any owned value is computed from it.

## CONFIG CROSS-REFERENCE

No ModuleData config. Cross-reference the 12 default colour strings in NameplateRelationPalette against the `#RRGGBBAA` rule, and the Id strings in `TaomSettlementPlateWidget.ResolveReferences` against the three prefab files.

## FINDINGS OR OBSERVATIONS

Report each finding as: severity (P1 ship-blocking / P2 should fix / P3 nit / observation), file:line, what the code does, what it should do, proof (quoted TAOM and engine lines). Then the Known Suspects verdicts. Then anything the deep review (RCA file above) missed.

## QUALITY GATES

- Every claim about engine behaviour quotes decompiled lines from the installed v1.4.8 DLLs.
- No finding on code that matches vanilla behaviour on purpose (the doc says which behaviours are deliberately vanilla: text stays opaque at close range, banner heraldry untinted, neutral identity white).
- No proposal to add an MCM toggle (decided against) or to change the palette values (starting points for the artist).
- State UNVERIFIED rather than guessing.

## PRIOR REVIEW LESSONS

SUCCESSES: vanilla decompilation caught missing gates and cross-party propagation; lifecycle tracing caught stale caches; executing the engine's update equations caught a scroll-momentum survivor last review.
FAILURES: Codex has assumed empire=Rohan (it is Dunland), flagged vanilla-matching code as bugs, skipped hard sections, and once wrote its report into the transcript file. Do none of these.
