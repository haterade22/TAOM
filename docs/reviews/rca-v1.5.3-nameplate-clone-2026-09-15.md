# RCA: the v1.5.3 nameplate clones (a prefab that shadows vanilla inherits nothing)

**Date:** 2026-09-15 · **Branch:** `bannerlord-1.5.x` · **Scope:** the v1.5.3 engine bump, one day
after v1.5.2; every compile, binding and suite gate was green before the finding.

## Top line

Bannerlord v1.5.3 added ship-banner widgets to the two party nameplate prefabs and to the widget
classes behind them. `PartyNameplateWidget.OnUpdate` calls
`ShipBannerContainerWidget.SetGlobalAlphaRecursively(0f)` on its first frame, and
`UpdateNameplatesVisibility` sets `ShipBannerContainerWidget.IsVisible` and reads
`ShipBannerWidget.ReadOnlyBrush` every frame after, none of it null-guarded. TAOM ships clones of
`PartyNameplateItem.xml` and `PartyPlayerNameplateItem.xml`, re-based on the v1.5.2 files the day
before, and a clone replaces the vanilla file outright: the engine never sees the new attributes, the
properties stay null, and every party nameplate throws the moment the campaign map comes up. The
same failure shipped on v1.5.0 with `BloodFeudIconWidget` (`65bc90a2`), and the clone even carried a
comment saying so. What was missing was not knowledge; it was a gate.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | HIGH | Both nameplate clones lacked `ShipBannerWidget`, `ShipBannerContainerWidget` and `ShipBannerFrameWidget` (plus the `Ship.Banner.Size` constant and the container block they point at); the v1.5.3 widget dereferences two of them unguarded. | Data drift under an engine bump | The three prefab gates checked element types (`PrefabElementTypeBindingTests`), extension XPaths (`PrefabExtensionBindingTests`) and the snapshot; none compared a clone against the vanilla file it hides. The v1.5.0 lesson was written into a comment on the clone, not into a test. | Both clones re-based on the v1.5.3 files with TAOM's edits re-applied; `PrefabCloneWidgetReferenceTests` (BindingVerification) pairs every TAOM prefab with the vanilla prefab of the same basename and requires every Widget-typed property attribute vanilla declares to survive in the clone. |
| 2 | LOW | `PartyTroopManagerPopUp.xml` had dropped `TertiaryInputKeyVisualParent` at some earlier re-base. `PartyManageTroopPopupWidget` guards it with an early return, so nothing threw; the popup's input-key hints had silently never shown. | Same class, guarded branch | No gate; the symptom is a missing hint on a gamepad-oriented overlay nobody exercised. | Found by the new gate's first run; the attribute and the vanilla block spliced in. |
| 3 | LOW | `check_handbook_attributes.py` reported eighteen FABRICATIONs on `ModuleInfo.LoadWithFullPath` against the v1.5.3 tree because the engine now reads `Id`/`Name`/`Version` and the dependency rows through `GetRequiredValueAttribute(node, "<Element>", path)` and `GetRequiredAttribute(node, "<Attr>", what, path)`, and the checker only knew direct `SelectSingleNode`/`Attributes[]` literals. | Checker idiom drift | The checker follows private helpers, but the literal lives at the call site and the helper reads through a parameter. | Two call-site idioms added with their own unit test; default dump root moved to `_categories_v1.5.3`. |

## The lesson worth keeping

**A prefab clone inherits nothing.** Every other TAOM surface that layers on vanilla gets the
engine's additions for free: an additive GameModel override calls `base`, an XSLT sees the merged
document, a Harmony postfix runs after the new body. A cloned prefab is the exception. It is a
full replacement of a file the engine keeps evolving, and the widget class behind it is written
against the vanilla file, so every property the vanilla prefab binds and the clone does not is a
null the engine will eventually dereference. The rule is therefore not "re-base the clones on each
bump" (that was done, the day before) but "pin the clone to the vanilla file it hides", which is
what the new gate does: it fails the moment vanilla adds a widget reference the clone lacks.

The precise form of the check matters. Comparing every attribute would flag TAOM's own design
choices (a font size, a hidden arrow button, a ring icon in place of a portrait). Comparing only
attributes whose name is a Widget-typed property on the element's widget class catches exactly the
null-dereference class and nothing else, and reflection over the installed assemblies supplies
that list without a hand-maintained table.
