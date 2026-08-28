# Creating Weapons in Bannerlord — Manual Authoring Workflow (LOTRLOME_Armory)

End-to-end, step-by-step guide for turning an imported weapon mesh into a craftable / equippable
weapon in the **LOTRLOME_Armory** module. This is the **manual 4-file workflow** — author the XML by
hand for full control over interchangeable pieces, flags, and per-piece roles.

> **Two paths — pick one:**
> - **Automated** — [`weapon-xml-pipeline.md`](../features/weapon-xml-pipeline.md) (`tools/build_weapon_xml.py`).
>   Best when each weapon is **self-contained** (one blade + one guard + one hilt + one pommel, meshes
>   named `wm_<theme>_<weapon>_<role>`). Generates all 4 files idempotently from a small manifest.
> - **Manual** (this doc). Best when pieces are **shared / interchangeable** — e.g. 8 spear+polearm heads
>   sharing 3 shafts, or 3 axe blades sharing 2 handles — which the one-weapon-per-entry pipeline can't
>   express without redundant duplicate pieces. This is the workflow used for the Dale set (2026-06).

## The four files (all live in the **game install**, NOT the TAOM repo)

`<game>\Modules\LOTRLOME_Armory\ModuleData\`

| # | File | Format | Role |
|---|------|--------|------|
| 1 | `LOTRLOME_crafting_pieces.xml` | Full XML | Defines every `<CraftingPiece>` (blade/guard/handle/pommel/head/shaft). The **only** file that references mesh + collision (`bo_`) names. |
| 2 | `weapon_descriptions.xslt` | XSLT | Lists each piece under a `WeaponDescription` category so the weapon class accepts it (incl. couch/brace/pike availability). |
| 3 | `crafting_templates.xslt` | XSLT | Lists each piece under a `CraftingTemplate` so the smithing UI offers it. |
| 4 | `LOTRLOME_items\LOTRAOM_weapons.xml` | Full XML | Assembles `<CraftedItem>` presets (multi-piece) and single-piece `<Item>` weapons (bows / shields / javelins). |

A typo in any one of the piece IDs spanning these files means the weapon **silently fails to load** —
no error in the game log. Validate (Step W) before launching.

---

## Step A — Get the assets from the 3D artist

You need an **FBX** plus its **textures**. Establish the mesh-naming convention up front:

```
wm_<culture>_ws_<weapon>_<variant>_<role>      e.g. wm_dale_ws_sword_a01_blade
bo_<same full mesh id>                          e.g. bo_wm_dale_ws_sword_a01_blade   (collision body)
```

- `wm_` = the visible mesh; `bo_` = its physics/collision proxy.
- `<role>` ∈ `blade | guard | handle | pommel | head` (+ bare names for bows/shields).
- **Blades, axe heads, spear/polearm heads, bows** need a matching `bo_` collision twin.
- **Guards, handles, pommels** do **not** have collision meshes (they attach to the blade).

> **`bo_<same full mesh id>` is exact, and getting it wrong HANGS the game — not a cosmetic bug.**
> A `body_name` the engine can't resolve makes `PreloadHelper.WaitForMeshesToBeLoaded` spin the main
> thread forever: no crash, no error log, one CPU core at 100%, mission never loads. LOTRLOME_Armory
> v2.0.8 shipped two refs that broke this exact rule and hung every siege with Dunland troops (#352) —
> `mesh="dunland_caerdh_sword_blade_2h_a"` with `body_name="bo_dunland_caerdh_sword_blade_2h"` (dropped
> the `_a`), and `mesh="wm_harad_spear_a02_head"` with `body_name="bo_wm_harad_spear_a02_blade"` (spears
> use `_head`; `_blade` was copy-pasted from the sword). Both assets shipped correctly — only the refs
> were wrong. **Verify before you ship:** `python tools/validate_mesh_refs.py --scan-bodies` (see
> [mesh-ref-validation.md](../features/mesh-ref-validation.md)). If it flags a body, look for a
> near-match in the packaged names before deleting the item — a missing asset is a typo until proven
> otherwise.

## Step B — Import the FBX + textures into the Bannerlord editor

Importing produces:
- `Assets\weapons\<culture>\<file>_geo.tpac`  (the packed binary the game loads)
- `AssetSources\weapons\<culture>\<file>.fbx`  (the source)

## Step C — Extract the AUTHORITATIVE mesh + collision names from the tpac

Never trust editor thumbnail labels — read the real strings out of the `.tpac`:

```bash
grep -aoE "(bo_)?wm_<culture>_ws_[a-z0-9_]+" \
  "<game>/Modules/LOTRLOME_Armory/Assets/weapons/<culture>/<file>_geo.tpac" | sort -u
```

(`strings` is often unavailable in Git-Bash; `grep -ao` works on the binary. Ignore stray trailing
`y`/`z` chars — they are captured binary bytes, not part of the name.) The `mesh="..."` and
`body_name="..."` you author in Step H **must** match these exactly.

## Step D — Confirm the collision-body (`bo_`) convention

- `body_name` = `bo_` + the exact mesh id. (`wm_dale_ws_sword_a01_blade` → `bo_wm_dale_ws_sword_a01_blade`.)
- Non-blade pieces (guard/handle/pommel) carry **no** `body_name` attribute at all.
- If a head/blade is **missing** its `bo_` twin in the tpac (common for newly-imported polearm heads),
  either (a) temporarily reuse a same-shaped existing `bo_` mesh so it loads, or (b) author the
  predicted `bo_<meshid>` name now and wait for the artist to deliver it. Until the `bo_` exists,
  that piece won't collide correctly in-game.

## Step E — Map the faction to a culture id

LOTR factions map onto vanilla or TAOM-custom culture StringIds (see CLAUDE.md / memory
`kingdom-culture-mapping`). The `culture="Culture.<id>"` on the assembled item (Step K) uses this id.

| LOTR faction | culture id | | LOTR faction | culture id |
|---|---|---|---|---|
| Gondor | `gondor` | | Rohan | `vlandia` |
| Mordor | `mordor` | | Dunland | `empire` |
| Erebor | `erebor` | | Khand | `battania` |
| Rivendell / Lothlórien / Mirkwood | `rivendell` / `lothlorien` / `mirkwood` | | Harad | `aserai` |
| Isengard / Gundabad / Dol Guldur | `isengard` / `gundabad` / `dolguldur` | | Easterlings (Rhûn) | `khuzait` |
| Umbar | `umbar` | | **Dale / Barding** | **`sturgia`** |

## Step F — Decide the piece taxonomy

For each mesh decide its `piece_type` and which weapon category it belongs to:

| `piece_type` (exact, case-sensitive) | Used for |
|---|---|
| `Blade` | sword blades, axe heads, spear/polearm heads — carries `<BladeData>` + damage |
| `Guard` | cross-guards — carries `<StatContributions armor_bonus="…">` |
| `Handle` | grips, hafts, shafts |
| `Pommel` | counterweights |

Bows and shields are **not** crafting pieces — they go straight into File 4 as single-piece `<Item>` (Step K).

## Step G — Collect measurements

- All pieces: `length` (cm).
- **Axe blades only**: also `blade_length` + `blade_width` (a broad axe is wider than it is tall).
- Bows: `weapon_length` (Step K) — **integer only**, see Step L.

Crafting-piece `length`/`blade_length`/`blade_width` are **float** — decimals are fine here
(matches existing armory pieces like `77.3`, `26.72`).

## Step H — File 1: define the crafting pieces (`LOTRLOME_crafting_pieces.xml`)

Append one `<CraftingPiece>` per mesh, before the closing `</CraftingPieces>`. Schema by type:

**Sword blade** (`metal_weapon`, thrust + swing):
```xml
<CraftingPiece id="wm_dale_ws_sword_a01_blade" name="{=aom_wm_dale_ws_sword_a01_blade_name}Dale Sword Blade I"
    tier="4" piece_type="Blade" mesh="wm_dale_ws_sword_a01_blade" length="89.16" weight="1.15">
    <BladeData stack_amount="3" physics_material="metal_weapon" body_name="bo_wm_dale_ws_sword_a01_blade" holster_mesh="">
        <Thrust damage_type="Pierce" damage_factor="2.9" />
        <Swing  damage_type="Cut"    damage_factor="2.8" />
    </BladeData>
    <BuildData piece_offset="20" previous_piece_offset="0" next_piece_offset="0" />
    <Flags><Flag name="CanKnockDown" /></Flags>
    <Materials><Material id="Iron6" count="11" /></Materials>
</CraftingPiece>
```

**Axe head** (note `blade_length`/`blade_width`; swing-only):
```xml
<CraftingPiece id="wm_dale_ws_1h_axe_a03_blade" ... piece_type="Blade" length="18.26" weight="1.2">
    <BladeData stack_amount="3" blade_length="18.26" blade_width="31.09" physics_material="metal_weapon" body_name="bo_wm_dale_ws_1h_axe_a03_blade">
        <Swing damage_type="Cut" damage_factor="3.4" />
    </BladeData>
    <BuildData piece_offset="0" />
    <Flags><Flag name="BonusAgainstShield" /><Flag name="CanDismount" /><Flag name="CanKnockDown" /></Flags>
    <Materials><Material id="Iron4" count="5" /></Materials>
</CraftingPiece>
```

> **The axe head above omits `excluded_item_usage_features` on purpose — do not copy that to a mace
> head.** Axe descriptions (`onehanded:shield:axe`, `twohanded:widegrip:axe`) carry no `thrust` token,
> so there is nothing to remove. `Mace` is `onehanded:block:shield:tipdraw:swing:thrust`, so a
> swing-only mace head MUST carry `excluded_item_usage_features="thrust"` or the crafted weapon gets a
> thrust attack with zero thrust damage. The description decides this, not the weapon's name — TAOM
> shipped 20 such heads (several named "Orc Axe", all authored into `Mace`) before this was caught.
> Rules + token table: [item-usage-features.md](../reference/item-usage-features.md).

**Spear / thrust-only polearm head** (`wood_weapon`, `excluded_item_usage_features="swing"`):
```xml
<CraftingPiece id="wm_dale_ws_spear_a01_blade" ... piece_type="Blade" length="48.81" weight="0.8"
    excluded_item_usage_features="swing">
    <BladeData stack_amount="3" physics_material="wood_weapon" body_name="bo_wm_dale_ws_spear_a01_blade">
        <Thrust damage_type="Pierce" damage_factor="2.6" />
    </BladeData>
    <BuildData piece_offset="0" />
    <Flags><Flag name="CanKnockDown" /><Flag name="CanDismount" /><Flag name="CanHook" /><Flag name="NotStackable" type="ItemFlags" /></Flags>
    <Materials><Material id="Iron4" count="3" /></Materials>
</CraftingPiece>
```
(A halberd head that both cuts and thrusts keeps **both** `<Thrust>` and `<Swing>` and **omits**
`excluded_item_usage_features`.)

**The rule in one line:** exclude the attack the head has no damage element for, but only when the
weapon description carries that token. Never declare damage you then exclude — vanilla ships zero
blades with a `<Thrust>` element and `excluded_item_usage_features="thrust"`, because the item card
would advertise a stat the animation set cannot deliver.

**Guard / Handle / Pommel** (no `body_name`; guard carries the armor bonus):
```xml
<CraftingPiece id="wm_dale_ws_sword_a01_guard" ... piece_type="Guard" length="8.70" weight="0.16">
    <BuildData piece_offset="0" next_piece_offset="2" previous_piece_offset="1" />
    <StatContributions armor_bonus="5" />
    <Materials><Material id="Iron5" count="2" /></Materials>
</CraftingPiece>
<CraftingPiece id="wm_dale_ws_sword_a01_handle" ... piece_type="Handle" length="15.22" weight="0.2">
    <BuildData piece_offset="3" />
    <Materials><Material id="Wood" count="1" /></Materials>
</CraftingPiece>
<CraftingPiece id="wm_dale_ws_sword_a01_pommel" ... piece_type="Pommel" length="4.68" weight="0.1">
    <BuildData piece_offset="-3" />
    <Materials><Material id="Iron4" count="1" /></Materials>
</CraftingPiece>
```

**Field reference**
| Field | Meaning |
|---|---|
| `tier` | 0–6 (Dale baseline 2–4). Higher = better crafted, gates smithy availability. |
| `mesh` | exact tpac mesh name (Step C). Convention: `mesh` == `id`. |
| `body_name` | `bo_` + mesh (blades only). |
| `physics_material` | `metal_weapon` or `wood_weapon`. |
| `excluded_item_usage_features` | Removes tokens from the composed animation-set name (`:`-separated, unioned across all pieces in the weapon). `swing` on a thrust-only head (spears); `thrust` on a swing-only head **whose description carries a `thrust` token** (maces — not axes). Full mechanism + token table: [item-usage-features.md](../reference/item-usage-features.md). |
| `damage_type` | `Pierce` \| `Cut` \| `Blunt`. `damage_factor` ≈ 2.0–3.5 for tier 2–4. |
| `armor_bonus` (Guard) | small bonus, e.g. 4–5. |
| `<Material id>` | `Iron2`–`Iron6`, `Wood`. |

## Step I — File 2: register in `weapon_descriptions.xslt`

Add `<AvailablePiece id="…" />` lines **inside** the `<AvailablePieces>` block of the matching category,
**before** the trailing `<xsl:apply-templates select="@*|node()"/>` (XSLT passthrough must stay last):

```xml
<xsl:template match="WeaponDescription[@id='OneHandedSword']/AvailablePieces">
    <AvailablePieces>
        <AvailablePiece id="wm_dale_ws_sword_a01_blade" />
        ...
        <xsl:apply-templates select="@*|node()"/>
    </AvailablePieces>
</xsl:template>
```

Available `WeaponDescription` categories: `OneHandedSword`, `TwoHandedSword`, `OneHandedAxe`,
`TwoHandedAxe`, `OneHandedPolearm`, `TwoHandedPolearm`, `TwoHandedPolearm_Couchable`,
`TwoHandedPolearm_Bracing`, `TwoHandedPolearm_Pike`, `OneHandedBastardSword`, `Mace`, `TwoHandedMace`,
`Javelin`.

> **Brace + couch-lance:** register the long shafts + thrust-capable heads under
> `TwoHandedPolearm_Couchable` and `TwoHandedPolearm_Bracing` (and the longest shaft under
> `TwoHandedPolearm_Pike`) in addition to `TwoHandedPolearm`. Short spears go in `TwoHandedPolearm` only.

## Step J — File 3: register in `crafting_templates.xslt`

Same pattern, `<UsablePiece piece_id="…" />` inside `<UsablePieces>`. Categories: `OneHandedSword`,
`TwoHandedSword`, `OneHandedAxe`, `TwoHandedAxe`, `TwoHandedPolearm`, `Pike`, `Mace`, `TwoHandedMace`,
`Javelin`.

## Step K — File 4: assemble items (`LOTRLOME_items\LOTRAOM_weapons.xml`)

Append before `</Items>`.

**Crafted preset** (piece order is canonical: Blade → Guard → Handle → Pommel; axes/polearms = Blade → Handle):
```xml
<CraftedItem id="dale_sword_a" name="{=aom_dale_sword_a_name}[Dale] Dale Sword I"
    crafting_template="OneHandedSword" is_merchandise="true" culture="Culture.sturgia">
    <Pieces>
        <Piece id="wm_dale_ws_sword_a01_blade"  Type="Blade"  scale_factor="100" />
        <Piece id="wm_dale_ws_sword_a01_guard"  Type="Guard"  scale_factor="100" />
        <Piece id="wm_dale_ws_sword_a01_handle" Type="Handle" scale_factor="100" />
        <Piece id="wm_dale_ws_sword_a01_pommel" Type="Pommel" scale_factor="100" />
    </Pieces>
</CraftedItem>
```
Polearms add `modifier_group="polearm"`. `crafting_template` ∈ the Step J template ids.

**Single-piece weapon — bow** (no crafting pieces; mirror an existing bow):
```xml
<Item id="dale_longbow_a" name="{=aom_dale_longbow_a_name}[Dale] Dale Longbow"
    body_name="bo_wm_dale_ws_longbow_a01" mesh="wm_dale_ws_longbow_a01"
    is_merchandise="true" culture="Culture.sturgia" weight="1.2" difficulty="80" appearance="0.1"
    Type="Bow" item_holsters="bow_back:bow_back_2:bow_hip:bow_hip_2">
    <ItemComponent>
        <Weapon weapon_class="Bow" ammo_class="Arrow" ammo_limit="1" thrust_speed="70"
            speed_rating="84" missile_speed="84" weapon_length="210" accuracy="96"
            thrust_damage="80" thrust_damage_type="Pierce" item_usage="bow"
            physics_material="wood_weapon" modifier_group="bow">
            <WeaponFlags RangedWeapon="true" HasString="true" StringHeldByHand="true"
                NotUsableWithOneHand="true" TwoHandIdleOnMount="true" AutoReload="true"
                UnloadWhenSheathed="true" />
        </Weapon>
    </ItemComponent>
    <Flags ForceAttachOffHandPrimaryItemBone="true" />
</Item>
```

## Step L — ⚠️ DATATYPE RULE: bows & shields cannot use decimals

Single-piece `<Item>` weapons — **bows, crossbows, shields, javelins, thrown weapons** — have their
numeric `<Weapon>` stats schema-typed as **`unsignedInt`**. **Use whole numbers only.** A decimal throws
a hard load error:

```
The 'weapon_length' attribute is invalid - The value '210.04' is invalid according to its
datatype 'http://www.w3.org/2001/XMLSchema:unsignedInt' - '210.04' is not a valid UInt32 value.
```

So `weapon_length="210"` ✔ , `weapon_length="210.04"` �’✘ . This applies to `weapon_length`,
`thrust_speed`, `speed_rating`, `missile_speed`, `accuracy`, `thrust_damage`, `ammo_limit`, and
shield stats (`body_armor`, `hit_points`, etc.).

**Contrast:** crafting-piece **`length` / `blade_length` / `blade_width` ARE float** — decimals are
fine there (Step G). The integer rule is specific to single-piece `<Item>` weapon stats. *(Verified
on the Dale bows, 2026-06; shields share the same single-piece integer-typed stat schema.)*

## Step M — Localization

Display names use `{=aom_<id>_name}<English fallback>`. The English fallback works in-game
immediately. Full 12-language propagation of the **Armory** module is a separate follow-up (the Armory
loc files live outside the TAOM repo); see [`localize`](../localization/TRANSLATOR_GUIDE.md) for the pipeline.

## Step N — File-handling discipline (CRLF / UTF-8 / idempotency)

- All four files are **CRLF + UTF-8**. Hand-editing with an LF tool silently mismatches anchors —
  prefer a script that reads with `newline=''` (preserves CRLF) or an editor that keeps line endings.
- Never **duplicate** an existing `id` — the engine silently shadows one entry.
- In the XSLTs, always insert **before** `<xsl:apply-templates select="@*|node()"/>`.
- Write `.bak` backups before bulk edits (cheap rollback).

## Step W — Validate (before launching the game)

Parse + cross-check with a short script:
1. **Well-formed** — `xml.etree.ElementTree.parse()` succeeds on all four files (XSLTs are XML too).
2. **Reference integrity** — every `<Piece id>` (File 4) and every `AvailablePiece`/`UsablePiece`
   (Files 2-3) resolves to a `<CraftingPiece id>` defined in File 1.
3. **Mesh existence** — every `mesh=` and `body_name=` resolves to a real string in the tpac (Step C).
   Pending artist `bo_` meshes are the expected exception.
4. **Datatype** — no decimals on single-piece `<Item>` weapon stats (Step L).

## Step X — In-game smoke test (authoritative)

Reviews cannot catch a wrong mesh name or a baked-atlas issue — only the running game can:
- Open the **smithy**: Dale pieces appear under the right weapon types and combine.
- Build a long polearm on the long shaft → confirm it **braces + couch-lances**.
- Fire both bows; equip a shield.
- A weapon rendering as **underwear / invisible** = a missing/misnamed `mesh`.
- Check `rgl_log.txt` for missing-mesh / duplicate-id / datatype warnings.

## Step Z — Rollback

If a launch fails, restore from the `.bak` files written in Step N. The four files are independent —
you can revert just the one that broke while keeping the others.

---

## Quick reference

| Thing | Values |
|---|---|
| `piece_type` | `Blade`, `Guard`, `Handle`, `Pommel` |
| `physics_material` | `metal_weapon`, `wood_weapon` |
| `damage_type` | `Pierce`, `Cut`, `Blunt` |
| Common `<Flag name>` | `CanKnockDown`, `CanDismount`, `CanHook`, `BonusAgainstShield`, `MultiplePenetration`, `CanCrushThrough`, `NotStackable` (`type="ItemFlags"`), `Civilian` (`type="ItemFlags"`), `CanBePickedUpFromCorpse` (`type="ItemFlags"`) |
| `<Material id>` | `Iron2`–`Iron6`, `Wood` |
| Decimals OK | crafting-piece `length`, `blade_length`, `blade_width` (float) |
| Decimals **forbidden** | all single-piece `<Item>` weapon stats — bows, shields, crossbows, javelins, thrown (`unsignedInt`) |

## Visual companion — Bannerlord Weapon Piece Aligner
When a crafted weapon's pieces don't line up (haft clipping the head, weapon sitting wrong in the hand),
preview and tune the `<BuildData>` offsets **before** launching the game with
[tools/BannerlordCraftingTool/](../../tools/BannerlordCraftingTool/README.md) — a standalone WPF app that
loads `crafting_pieces.xml` + your FBX meshes and reproduces the engine's exact piece-positioning
(`WeaponDesign.CalculatePivotDistances`). It's self-contained (no TAOM/Bannerlord build dependency) and
ships as a downloadable Release zip. Export tuned offsets to JSON and patch them back into the source XML.

## Related
- [weapon-xml-pipeline.md](../features/weapon-xml-pipeline.md) — automated alternative for self-contained weapons
- [item-equipment-model.md](../reference/engine/item-equipment-model.md) — engine `ItemObject`/`ItemComponent`/`Equipment` model
- CLAUDE.md "Equipment & Armory" — canonical-folder table per item-ID prefix
- memory `kingdom-culture-mapping` — full LOTR-faction → culture-id table

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/black-numenorean.md](../features/black-numenorean.md)
- [docs/features/weapon-xml-pipeline.md](../features/weapon-xml-pipeline.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/item-usage-features.md](../reference/item-usage-features.md)
- [docs/reviews/rca-crafting-usage-features-2026-07-26.md](../reviews/rca-crafting-usage-features-2026-07-26.md)

<!-- backlinks-end -->
