# GUI prefab clones go stale across versions

TAOM ships full `<Prefab>` clones of many vanilla GUI prefabs (party screen, custom-battle, encyclopedia, nameplates, game-menu). A clone overrides the vanilla prefab of the same filename by load order. Vanilla renames widget attributes and re-schemas prefabs between versions; a clone frozen at an older version keeps the obsolete attribute, the engine silently ignores it, and the widget mis-renders or never renders, with no log and no crash. This is the same stale-vs-vanilla failure as the mirrored data XML in [vanilla-data-comparison.md](../../.claude/rules/vanilla-data-comparison.md), applied to UI. The short rule is in [gui-ui.md](../../.claude/rules/gui-ui.md) ("GUI Prefab Clones Go Stale Across Engine Versions").

## Gates

| Gate | What it fails |
|---|---|
| `EveryPrefabAttribute_NamesASettableWidgetProperty` | an attribute that is not an exact-case public property with a public setter on the element's resolved widget type, so a renamed or stale attribute fails |
| `PrefabCloneWidgetReferenceTests` | a clone's reference attributes that point at a widget the clone does not contain |
| `PartyTupleVanillaParityTests` | the `PartyTroopTuple.xml` copy against the installed vanilla file, element for element, excluding TAOM's documented edits (the model for a LiveInstall parity test) |
| `NoTaomPrefab_IsAnUnchangedCopyOfTheVanillaFileItShadows` (#1051) | a copy that already matches the installed file; a stale copy passes it, so the diff below still finds those |

A clone can also stop consuming an engine signal with every attribute still valid. No attribute gate sees that: see the nameplate case in [gui-ui.md](../../.claude/rules/gui-ui.md).

## Verified v1.4.5 attribute changes

The left column is the stale form a clone may still carry. Every row was confirmed against installed vanilla 1.4.5.

| Stale form | v1.4.5 form | Symptom if left stale |
|---|---|---|
| `ImageTypeCode="@ImageTypeCode"` | `TextureProviderName="@TextureProviderName"` | `ImageIdentifierWidget` and `MaskedTextureWidget` thumbnails never resolve a provider, so they show a perpetual loading spinner (0 vanilla `ImageTypeCode`) |
| `LayoutImp.LayoutMethod="..."` | `StackLayout.LayoutMethod="..."` | the layout method is ignored and stacking is not applied (0 vanilla `LayoutImp.LayoutMethod` against 926 `StackLayout.LayoutMethod`). `LayoutImp.HorizontalLayoutMethod` and `LayoutImp.VerticalLayoutMethod` are still valid in 1.4.5: do not touch those. |
| `ScrollYOffset` (on `NavigationAutoScrollWidget`) | `AutoScrollTopOffset` / `AutoScrollBottomOffset` | auto-scroll-to-focused is lost under gamepad or keyboard navigation (0 vanilla `ScrollYOffset` against 137 `AutoScroll*Offset`) |
| `RichTextWidget="...\SelectedTextWidget"` (on `DropdownWidget`) | `TextWidget="...\SelectedTextWidget"` | the dropdown's selected-text reference does not bind (4 of 4 vanilla `DropdownWidget` use `TextWidget=`). The rename is widget-specific, since other widgets legitimately use `RichTextWidget=`: check the widget, not just the attribute. |

**Verify each suspected rename against installed vanilla before treating it as obsolete. Do not trust a list, this one included, and use a discriminating method, never a bare substring grep:** an attribute-name regex (`(^|[^A-Za-z])Attr[[:space:]]*=`) scoped to true vanilla only (`SandBox`, `SandBoxCore`, `Native`, not `Modules/*`, because the install's `Modules/` also holds the deployed `TAOM`, `TAOM_Map` and `LOTRLOME_Armory` modules, whose own possibly stale files inflate a count), plus a decompile of the consuming widget when in doubt. A count claim is checked with a stricter method than the claim used, not a sloppier one (the 2026-05-31 audit got `EaseIn` and the `ScrollYOffset` direction wrong twice this way: [localization-ui.md](../reviews/lessons/localization-ui.md), "TAOM ships full `<Prefab>` clones of vanilla GUI prefabs").

## How to fix a stale clone

1. Run `diff -w --strip-trailing-cr <installed-vanilla> <taom-clone>` and classify each delta as a rename casualty or stale attribute, against an intentional TAOM customization.
2. If the clone has no intentional content, delete it so vanilla loads, from the repo and from the deployed `Modules/TAOM/GUI/Prefabs/` (a deploy never deletes). A clone with TAOM tweaks is re-synced: the installed file verbatim, with only those tweaks re-applied.
3. If it is a TAOM original or a deliberate redesign (the Encyclopedia `EncyclopediaClanListElement` banner swaps, the `SettlementNameplateItem` diamond layout, the `CharacterCreation*Stage` theming), leave the structure alone and fix only the obsolete attribute in place.
4. After a Bannerlord version bump, audit all clones, not just the one you noticed: a rename hits every clone that used the attribute.

Only the running game confirms a prefab renders.
