---
paths:
  - "**/settlements.xml"
  - "**/sp_battle_scenes.xml"
  - "**/spcultures.xml"
  - "**/taom_spcultures.xml"
  - "**/spclans.xml"
  - "**/spkingdoms.xml"
  - "**/action_sets.xml"
  - "**/*.xslt"
  - "**/GUI/PreFabs/**/*.xml"
  - "**/GUI/Prefabs/**/*.xml"
---

# Compare Against Vanilla Before Modifying Mirrored Data

TAOM ships many XML files that **mirror, extend, or transform vanilla Bannerlord data** (`settlements.xml`, `sp_battle_scenes.xml`, `spcultures`/`spclans`/`spkingdoms`, `action_sets.xml`, the `*.xslt` transforms, party templates, equipment rosters). Vanilla renames, removes, and re-schemas this data between versions. A TAOM reference that was valid in an older version goes **silently stale** after a version bump and crashes the game when that data path is exercised — often far from where the stale value lives (e.g. a battle near a specific map cell, entering a specific town).

**Rule:** before authoring or relying on any value that mirrors/references vanilla, diff it against the **currently installed** vanilla version. Don't trust that "it worked before" — the previous version may have had a name that no longer exists.

**Two failure shapes the stale-name case doesn't cover**, both found on 2026-08-03 and both invisible to a per-value check: the data can be structurally illegal in a way one engine build tolerates and another refuses to boot on (see "`action_sets.xml`: the shape is part of the comparison, and so is the BUILD" below), and it can be perfectly well-formed yet unreachable relative to the rest of the data (see "Settlement entrances: a valid face can still be unreachable").

## What to check, and how

| You're touching… | Compare against | Tool |
|---|---|---|
| `settlements.xml` scene_name refs | on-disk `Modules/*/SceneObj/` folders | `python tools/audit_scene_names.py` |
| `sp_battle_scenes.xml` Scene ids / map_indices | vanilla `SandBox`/`NavalDLC` `sp_battle_scenes.xml` + SceneObj | `python tools/audit_battle_scenes.py` |
| settlement entrance coordinates (`posX/posY`, `gate_posX/gate_posY`) | the navmesh island every OTHER settlement sits on — set-relative, not vanilla | `taom.audit_settlement_entrances` (needs a loaded campaign); see "Settlement entrances" below |
| `action_sets.xml` — structure as much as content | vanilla `Native/ModuleData/action_sets.xml` (the engine field-merges same-id sets across modules) | `python tools/audit_action_set_parity.py` — **exits non-zero** on any root-level `<action>`; see "`action_sets.xml`" below |
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
- **Before committing any `action_sets.xml` edit** — run `audit_action_set_parity.py`. It now fails on a structural defect the client build loads without complaint and the dedicated-server build dies on (2026-08-03).
- **When adding a settlement, or moving one's `posX/posY` or `gate_posX/gate_posY`** — a coordinate can be on-mesh and still unreachable. Only an island comparison finds that, and only in a loaded campaign.
- **When diagnosing a "crash near a specific place" report** — it is almost always a stale data reference (scene, item, troop, culture), not an engine-internals bug. Audit the references first. The non-crashing variant of the same report — **AI parties wedging near a specific place, no log, no crash** — is the navmesh-island case below.

## GUI prefab clones go stale across versions

TAOM ships full `<Prefab>` **clones** of many vanilla GUI prefabs (party screen, custom-battle, encyclopedia, nameplates, game-menu — ~32 of TAOM's 48 prefabs are clones). A clone overrides the vanilla prefab of the same filename by load order. Vanilla **renames widget attributes and re-schemas prefabs between versions**; a clone frozen at an older version keeps the obsolete attribute, the engine silently ignores it, and the widget mis-renders or **never renders** — with no log, no crash. This is the same stale-vs-vanilla failure as the data XML above, applied to UI.

**Verified v1.4.5 attribute changes** (LEFT is the stale form a clone may still carry; every row below was confirmed against installed vanilla 1.4.5 — usage counts in the symptom column):

| Stale form | v1.4.5 form | Symptom if left stale |
|---|---|---|
| `ImageTypeCode="@ImageTypeCode"` | `TextureProviderName="@TextureProviderName"` | `ImageIdentifierWidget`/`MaskedTextureWidget` thumbnail never resolves a provider → **perpetual loading spinner** (0 vanilla `ImageTypeCode`) |
| `LayoutImp.LayoutMethod="…"` | `StackLayout.LayoutMethod="…"` | layout method ignored → stacking not applied (0 vanilla `LayoutImp.LayoutMethod` vs 926 `StackLayout.LayoutMethod`). **`LayoutImp.HorizontalLayoutMethod` / `LayoutImp.VerticalLayoutMethod` are STILL valid in 1.4.5 — do NOT touch those.** |
| `ScrollYOffset` (on `NavigationAutoScrollWidget`) | `AutoScrollTopOffset` / `AutoScrollBottomOffset` | auto-scroll-to-focused lost under gamepad/keyboard nav (0 vanilla `ScrollYOffset` vs 137 `AutoScroll*Offset`) |
| `RichTextWidget="…\SelectedTextWidget"` (on `DropdownWidget`) | `TextWidget="…\SelectedTextWidget"` | dropdown selected-text reference doesn't bind (4/4 vanilla `DropdownWidget` use `TextWidget=`). **Widget-specific** — other widgets legitimately use `RichTextWidget=`, so check the widget, not just the attribute |

> **VERIFY each suspected rename against installed vanilla before treating it as obsolete — do NOT trust a list (this one included), and use a DISCRIMINATING method, never a bare substring grep:** an attribute-NAME regex (`(^|[^A-Za-z])Attr[[:space:]]*=`) **scoped to true vanilla only — `SandBox`/`SandBoxCore`/`Native`, NOT `Modules/*`** (the install's `Modules/` also contains the deployed `TAOM`/`TAOM_Map`/`LOTRLOME_Armory` modules, whose own — possibly stale — files silently inflate a `Modules/*/GUI/Prefabs` count), plus a decompile of the consuming widget when in doubt.
>
> Cautionary tale (2026-05-31): the `EaseIn` row was wrong **twice** — an audit first called it an obsolete "rename causing a regression," then a "correction" claimed *"vanilla uses it 18×"* — **both via substring grep.** The truth (attribute-name regex + decompile of `VisualDefinitionTemplate`): bare `EaseIn=` appears in **0** vanilla files; the "18×" was a substring miscount of `EaseType="EaseInOut"` / `IsEaseInOutEnabled`; `VisualDefinitionTemplate` has **no** `EaseIn` property, so the parser silently ignores `EaseIn="true"` — it is **inert dead markup, not "used by vanilla."** (Leaving it is harmless; it just does nothing.) The `AutoScroll*Offset`↔`ScrollYOffset` direction was also stated inverted at one point (`ScrollYOffset` is the stale one — 0 in vanilla, scoped). This is the `feedback_xml_grep_wrapper_offset` failure (and `feedback_audit_findings_not_always_correct`): when verifying a count claim, use a **stricter** method than the claim you're checking, not a sloppier one — and an internal audit/review is a source of confident false positives, so re-derive its load-bearing numbers yourself.

**How to fix a stale clone:**
1. `diff -w --strip-trailing-cr <vanilla-1.4.5> <taom-clone>` and classify each delta: **rename-casualty / stale-attribute** vs **intentional TAOM customization**.
2. If the clone has **no** intentional content (pure stale copy), **re-sync**: replace it with the vanilla 1.4.5 file verbatim, re-applying only the genuine TAOM tweaks on top.
3. If it's a TAOM-original or a deliberate redesign (e.g. the Encyclopedia `EncyclopediaClanListElement` banner swaps, the `SettlementNameplateItem` diamond layout, the `CharacterCreation*Stage` theming), **leave the structure alone** and only fix the obsolete attribute in place.
4. After a Bannerlord version bump, audit **all** clones, not just the one you noticed — the rename hits every clone that used the attribute. (A `tools/audit_gui_prefab_clones.py` detector is the planned mechanization of step 1.)

## `action_sets.xml`: the shape is part of the comparison, and so is the BUILD

`LOTRLOME_Armory/ModuleData/action_sets.xml` extends Native's — the engine field-merges same-id `<action_set>` nodes across modules — so it is mirrored data in exactly the sense above. It fails differently from the scene-name case: `<action_sets>` accepts only `<action_set>` children, so a violation is a **schema** defect — nothing an id lookup can catch, and nothing a client-side play session can surface.

On 2026-08-03 the file carried 168 `<action>` elements parented directly by `<action_sets>`. Twelve `as_<race>_female_villager_in_aserai_tavern` sets had been authored SELF-CLOSING, which orphaned the 14 female-conversation overrides that belong nested inside each; vanilla's own `as_human_female_villager_in_aserai_tavern` nests exactly those 14 in that order, which is what made the repair mechanical rather than a judgement call. **Build 1.4.7.117484 tolerates the malformed file. Build 117131 — which TaleWorlds' DEDICATED SERVER engine ships — throws `KeyNotFoundException` in `MBObjectManager.MergeElements` at schema path `/action_sets/action` and dies on boot**, which is why server operators had to fall back to the single-player module order (`Alliance.Wargs` before `LOTRLOME_Armory`) to get one started.

**"It loads in game" is evidence about ONE engine build.** The client build's tolerance says nothing about the server build's parser, so a data file that feeds both has to be checked against both — which, for a structural defect, means a validator rather than a play session.

**Before committing an `action_sets.xml` edit:**
1. `python tools/audit_action_set_parity.py` — reports root-level `<action>` elements and **exits non-zero** on them, as well as on humanoid sets missing part of Native's `as_human_warrior` surface.
2. If it reports orphans: `python tools/oneoff/fix_orphaned_tavern_conversation_actions.py --apply`. It is idempotent, and it rewrites the LIVE Armory file and the tracked snapshot `docs/reference/lotrlome-armory-snapshot/action_sets.xml` together — those two must not drift.
3. **The generator is not the suspect.** `tools/generate_race_civilian_action_sets.py` bounds its output with `TAOM-CIVILIAN-COVERAGE:START/END` markers and emits no `_in_aserai_tavern` sets at all; the twelve broken sets sat outside that block and were hand-authored. Grepping for a generator is the obvious first move here and it accuses the wrong component.

## Settlement entrances: a valid face can still be unreachable

`PathFaceRecord.IsValid()` is a **per-face** check, and reachability is not a property of a face — it is a property of a face relative to the rest of the mesh. All three settlement destinations reported wedging AI parties on 2026-08-03 (`town_MM2`'s `gate_posX/gate_posY`, `hideout_desert_7`'s and `castle_village_MM1_2`'s `posX/posY`) are on-mesh, and every coordinate matches the live `TAOM_Map/ModuleData/settlements.xml` exactly. There is nothing wrong with the values themselves. They sit on navmesh **islands** the rest of the map cannot path to, so every AI tick targeting one fails its path query and the engine's only report is a repeating "Path finding target is not valid" assert that names no settlement.

**Compare against the set, not the value.** `PathFaceRecord.FaceIslandIndex` is the engine's own connected-component id — two faces with different indices have no path between them at any cost. `taom.audit_settlement_entrances` (`Main/Features/DevConsole/Cheats/SettlementEntranceCheats.cs`) walks every settlement's entrance (`GatePosition` for towns and castles, else `Position`), takes the main landmass to be the island index the most settlements agree on, flags every disagreement, and emits a replacement coordinate from `IMapScene.GetAccessiblePointNearPosition` at widening radii 1/2/4/8/16/32 — the engine's own navmesh answering, not a guess. Command reference: `docs/features/dev-console.md`.

**Status (2026-08-03): the auditor ships; the corrected coordinates do not exist yet.** Producing them needs one in-game campaign run. When they exist they go into the LIVE `<game>/Modules/TAOM_Map/ModuleData/settlements.xml` — the repo's `Main/_Module/ModuleData/settlements.xml` is a stale shadow, and edits there never reach the game.

## Why this rule exists

2026-05-28 session: "fighting battles near specific places crashes" after the v1.3.15→v1.4.5 bump. Root causes were ALL stale-vs-vanilla references:
- v1.4.5 renamed `<culture>_house_a_interior_house` interior scenes; 61 stale `scene_name` refs across TAOM towns.
- TAOM's `sp_battle_scenes.xml` mapped map indices 158–255 to `battle_terrain_extended`, a scene that doesn't exist on disk.

Full write-up: `docs/reference/scene-reference-audit.md`. Memory: `feedback_scene_name_refs_break_on_version_bump.md`. Sibling research-first rule for code: CLAUDE.md "Research First" (decompile before guessing TaleWorlds behavior) — this rule is its data-side counterpart.

2026-05-31 session: the **GUI-prefab instance** of the same failure — every Party-screen troop thumbnail stuck on the loading spinner because TAOM's stale prefab clones bound the v1.4.5-renamed `ImageTypeCode` instead of `TextureProviderName`. Full write-up: `docs/reviews/rca-party-troop-thumbnail-stale-prefab-clone-2026-05-31.md`. Memory: `feedback_gui_prefab_clones_stale_across_versions.md`. (Prompted the "GUI prefab clones" section + `GUI/PreFabs` globs above.)

2026-08-03 multiplayer field report: two shapes that are not name-staleness at all. `LOTRLOME_Armory`'s `action_sets.xml` carried 168 structurally illegal root-level `<action>` elements — loaded without complaint by the client build, fatal on boot for the dedicated-server build (commit `c9455ec8`). Three settlement entrances sat on unreachable navmesh islands while passing every per-face validity check, visible only because the reporters had written their own pathfinding instrumentation (commit `31405eb1`). Both were reached by comparing against something the file itself cannot show you — vanilla's nesting in the first case, the other settlements' island index in the second. Lessons: `docs/reviews/lessons/data-content-cultures.md`. (Prompted the two sections above + the `**/action_sets.xml` glob.)
