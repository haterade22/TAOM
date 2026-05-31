---
paths:
  - "**/settlements.xml"
  - "**/sp_battle_scenes.xml"
  - "**/spcultures.xml"
  - "**/taom_spcultures.xml"
  - "**/spclans.xml"
  - "**/spkingdoms.xml"
  - "**/*.xslt"
  - "**/GUI/PreFabs/**/*.xml"
  - "**/GUI/Prefabs/**/*.xml"
---

# Compare Against Vanilla Before Modifying Mirrored Data

TAOM ships many XML files that **mirror, extend, or transform vanilla Bannerlord data** (`settlements.xml`, `sp_battle_scenes.xml`, `spcultures`/`spclans`/`spkingdoms`, the `*.xslt` transforms, party templates, equipment rosters). Vanilla renames, removes, and re-schemas this data between versions. A TAOM reference that was valid in an older version goes **silently stale** after a version bump and crashes the game when that data path is exercised — often far from where the stale value lives (e.g. a battle near a specific map cell, entering a specific town).

**Rule:** before authoring or relying on any value that mirrors/references vanilla, diff it against the **currently installed** vanilla version. Don't trust that "it worked before" — the previous version may have had a name that no longer exists.

## What to check, and how

| You're touching… | Compare against | Tool |
|---|---|---|
| `settlements.xml` scene_name refs | on-disk `Modules/*/SceneObj/` folders | `python tools/audit_scene_names.py` |
| `sp_battle_scenes.xml` Scene ids / map_indices | vanilla `SandBox`/`NavalDLC` `sp_battle_scenes.xml` + SceneObj | `python tools/audit_battle_scenes.py` |
| any `scene_name=` that no longer resolves | — | `python tools/remap_stale_scene_names.py --dry-run` |
| TaleWorlds API signatures | installed DLLs | `pwsh tools/taom-src.ps1 path <Type>` (NOT the decompiled dump) |
| culture/clan/kingdom IDs | vanilla `SandBoxCore` XML | grep + `xml-data.md` ID table |
| XSLT passthrough attributes | vanilla source the XSLT transforms | `/xslt-check`, `feedback_xslt_passthrough_unintended_inheritance.md` |
| GUI prefab **clones** (full `<Prefab>` copies of a vanilla prefab) | vanilla `Modules/{SandBox,SandBoxCore,Native}/GUI/Prefabs/<same-name>.xml` | `diff -w --strip-trailing-cr <vanilla> <taom>`; see "GUI prefab clones" below |

**Matching is case-insensitive.** Windows resolves `HART_ISENGARD` vs `HART_isengard`; an exact-case check produces false positives. The scene audit tools already lower-case both sides.

## When this fires

- **After ANY Bannerlord version bump** — run `audit_scene_names.py` + `audit_battle_scenes.py` as part of the post-bump validation (see `docs/migration/v1.4.x-changes.md`). v1.4.5 renamed the house-interior scenes and TAOM's `sp_battle_scenes.xml` referenced a non-existent `battle_terrain_extended`; both crashed battles/visits until repointed (2026-05-28).
- **When editing any of the `paths:` files above** — re-run the relevant audit before committing.
- **Before editing — or after a version bump touching — any TAOM GUI prefab that is a CLONE of a vanilla prefab** — diff it against installed vanilla first (see "GUI prefab clones" below). v1.4.5 silently broke every troop thumbnail this way (2026-05-31).
- **When diagnosing a "crash near a specific place" report** — it is almost always a stale data reference (scene, item, troop, culture), not an engine-internals bug. Audit the references first.

## GUI prefab clones go stale across versions

TAOM ships full `<Prefab>` **clones** of many vanilla GUI prefabs (party screen, custom-battle, encyclopedia, nameplates, game-menu — ~32 of TAOM's 48 prefabs are clones). A clone overrides the vanilla prefab of the same filename by load order. Vanilla **renames widget attributes and re-schemas prefabs between versions**; a clone frozen at an older version keeps the obsolete attribute, the engine silently ignores it, and the widget mis-renders or **never renders** — with no log, no crash. This is the same stale-vs-vanilla failure as the data XML above, applied to UI.

**Verified v1.4.5 attribute changes** (LEFT is the stale form a clone may still carry; every row below was confirmed against installed vanilla 1.4.5 — usage counts in the symptom column):

| Stale form | v1.4.5 form | Symptom if left stale |
|---|---|---|
| `ImageTypeCode="@ImageTypeCode"` | `TextureProviderName="@TextureProviderName"` | `ImageIdentifierWidget`/`MaskedTextureWidget` thumbnail never resolves a provider → **perpetual loading spinner** (0 vanilla `ImageTypeCode`) |
| `LayoutImp.LayoutMethod="…"` | `StackLayout.LayoutMethod="…"` | layout method ignored → stacking not applied (0 vanilla `LayoutImp.LayoutMethod` vs 926 `StackLayout.LayoutMethod`). **`LayoutImp.HorizontalLayoutMethod` / `LayoutImp.VerticalLayoutMethod` are STILL valid in 1.4.5 — do NOT touch those.** |
| `ScrollYOffset` (on `NavigationAutoScrollWidget`) | `AutoScrollTopOffset` / `AutoScrollBottomOffset` | auto-scroll-to-focused lost under gamepad/keyboard nav (0 vanilla `ScrollYOffset` vs 137 `AutoScroll*Offset`) |
| `RichTextWidget="…\SelectedTextWidget"` (on `DropdownWidget`) | `TextWidget="…\SelectedTextWidget"` | dropdown selected-text reference doesn't bind (4/4 vanilla `DropdownWidget` use `TextWidget=`). **Widget-specific** — other widgets legitimately use `RichTextWidget=`, so check the widget, not just the attribute |

> **VERIFY each suspected rename against installed vanilla before treating it as obsolete — do NOT trust a list (this one included).** Grep vanilla for the exact attribute on the exact widget; if vanilla still uses it, it is not drift. On 2026-05-31 an audit flagged two FALSE "renames": `EaseIn="true"` is **not** obsolete (vanilla 1.4.5 uses it 18×, alongside `EaseType`), and the `AutoScroll*Offset` ↔ `ScrollYOffset` direction was **inverted** (`ScrollYOffset` is the stale one). Both were caught only by checking vanilla — the same evidence-over-claims discipline this rule exists to enforce.

**How to fix a stale clone:**
1. `diff -w --strip-trailing-cr <vanilla-1.4.5> <taom-clone>` and classify each delta: **rename-casualty / stale-attribute** vs **intentional TAOM customization**.
2. If the clone has **no** intentional content (pure stale copy), **re-sync**: replace it with the vanilla 1.4.5 file verbatim, re-applying only the genuine TAOM tweaks on top.
3. If it's a TAOM-original or a deliberate redesign (e.g. the Encyclopedia `EncyclopediaClanListElement` banner swaps, the `SettlementNameplateItem` diamond layout, the `CharacterCreation*Stage` theming), **leave the structure alone** and only fix the obsolete attribute in place.
4. After a Bannerlord version bump, audit **all** clones, not just the one you noticed — the rename hits every clone that used the attribute. (A `tools/audit_gui_prefab_clones.py` detector is the planned mechanization of step 1.)

## Why this rule exists

2026-05-28 session: "fighting battles near specific places crashes" after the v1.3.15→v1.4.5 bump. Root causes were ALL stale-vs-vanilla references:
- v1.4.5 renamed `<culture>_house_a_interior_house` interior scenes; 61 stale `scene_name` refs across TAOM towns.
- TAOM's `sp_battle_scenes.xml` mapped map indices 158–255 to `battle_terrain_extended`, a scene that doesn't exist on disk.

Full write-up: `docs/reference/scene-reference-audit.md`. Memory: `feedback_scene_name_refs_break_on_version_bump.md`. Sibling research-first rule for code: CLAUDE.md "Research First" (decompile before guessing TaleWorlds behavior) — this rule is its data-side counterpart.

2026-05-31 session: the **GUI-prefab instance** of the same failure — every Party-screen troop thumbnail stuck on the loading spinner because TAOM's stale prefab clones bound the v1.4.5-renamed `ImageTypeCode` instead of `TextureProviderName`. Full write-up: `docs/reviews/rca-party-troop-thumbnail-stale-prefab-clone-2026-05-31.md`. Memory: `feedback_gui_prefab_clones_stale_across_versions.md`. (Prompted the "GUI prefab clones" section + `GUI/PreFabs` globs above.)
