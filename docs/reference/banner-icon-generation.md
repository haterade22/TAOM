# Banner-Icon Generation & Wiring (start to finish)

How to create new clan/faction banner **sigil icons** for TAOM with AI (imagine.art), key them, and wire them through **both** banner pipelines so they render in-game. Distilled from the 2026-06 batch that added 9 new sheets (Misty Mountain Orcs ×2, Goblin, Isengard #2, Mordor #2, Gundabad #2, Dol Guldur #2, Dunland #2, and a mixed Rohan/Rhûn/Lindon/Dale sheet).

> **The single most important fact:** there are **two independent pipelines**, both keyed by the same icon ids from `banner_icons.xml`, and **both** must be produced:
> 1. **Banner material** (`taom_banners_*` `.tpac`) — what the banner actually *renders* from (vanilla system).
> 2. **GauntletUI sprite atlas** (`ui_taom_bannericons_*` + `TAOMSpriteData.xml`) — the per-icon UI sprite set, packed by the sprite generator.
> A new icon shows **blank** until the relevant pipeline's binary asset is compiled. Static review can't prove an icon renders — only the running game can.

Heraldry is sober Tolkien style with one bold plain motif; show designs before wiring (Mike, 2026-10-28).

Prompt nano-banana-pro for a solid filled silhouette in "J.R.R. Tolkien's own hand-drawn heraldic devices" or "medieval heraldic charge" style, with the guards "NOT high fantasy, no flourishes, no sparkles, no thin outlines" and a pure-black background. After the first round of Lindon emblems Mike asked for a more realistic look in Tolkien's own style, not high fantasy, and approved the bold swan-wave (Nos Brithombar, now Nos Eldalomril) as the model. Show him a design sheet (raw plus banner mock) and wait for his picks before you key or wire anything.

## The three id layers (all share the icon id)

| Layer | Where | Keyed by |
|---|---|---|
| Definition | `Main/_Module/ModuleData/banner_icons.xml` — `<BannerIconGroup>` → `<Icon id material_name texture_index>` | the **Icon id** (e.g. `28000`) |
| Render material | `taom_banners_NN` (the `material_name`) at cell `texture_index` (0–15) | material_name + texture_index |
| UI sprite | `GUI/SpriteParts/ui_taom_bannericons/<Icon id>.png` → packed atlas; sprite `<Name>` = the Icon id | the **Icon id** |

So one emblem = one Icon id = one `texture_index` cell in its material sheet = one `<id>.png` sprite source.

## Step 1 — Generate emblems (imagine.art MCP)

- **Model: `nano-banana-pro` only** (the free plan rejects `recraft-v4.1`/others). Native output is **1K = 1024×1024**, exactly one banner cell. `aspect_ratio` `1:1`.
- **Flow per image:** `select_organization` once → reuse `org_id`. `generate_image` returns a uuid; **`fetch_image_status` with `sync:true`** both waits for completion *and* frees the queue slot, and returns the `mediaUrl` — then `curl` that URL to disk. (Pull via `curl` directly if the MCP times out; the URL is `https://asset.imagine.art/processed/<uuid>`.)
- **Concurrency cap = 10 in progress.** Fire ≤10, collect them (the `fetch_image_status` call is what decrements the counter — a curl-only download leaves the job "in progress" and the counter sticks), then fire the next wave.
- **Cost:** ~48 credits/image. Check with `get_balance` before big batches.

**Prompt = style prefix + subject.** Match the prefix to the faction's nature:

| Faction type | Style | Prefix gist |
|---|---|---|
| Orcs/goblins/uruks (Mordor, Isengard, Gundabad, Dol Guldur, MM Orcs, Goblin) | rough hand-painted **bone-white war paint** on pure black, THICK & SOLID, grim, `NOT cartoon, NOT a cute creature` | "A crude orcish war emblem hand-painted by orcs in rough thick bone-white war paint on a solid pure-black background…" |
| Men/Elves (Rohan, Dale, Lindon) | clean **elegant/noble heraldry**, smooth confident linework, `NOT war paint, NOT cartoon` | "A noble heraldic emblem in clean bold bone-white on a solid pure-black background…" |
| Easterlings (Rhûn) | **ornate eastern**, symmetrical/decorative, **FLAT 2D** (`NO shading, NO 3D, NO relief` — a 3D-looking medallion won't recolor) | "An ornate eastern heraldic emblem… completely flat graphic…" |
| Dunland | wild beasts in **fierce attacking poses**, rough war-paint (Celtic *knotwork* read as too high-fantasy — rejected) | war-paint prefix + "a [beast] in a dynamic aggressive attacking pose" |

Universal guards in every prompt: `solid pure-black background to all four edges; no cloth/banner/flag/pole/scene/border/frame/text; one large bold centered motif filling most of the frame with clear margins.` nano-banana-pro tends to (a) render a literal *banner on a wall* if you say "banner/coat-of-arms" — don't; (b) add a **white frame** or **shield outline** — the keyer strips edge-connected white, but say "no frame/shield" too; (c) make line-art too thin → low coverage. Push "THICK and SOLID, fills the frame."

## Step 2 — Key + assemble (free, scripted — no credits)

Per emblem (`tools`-style PIL script, see the 2026-06 scratch `assemble_*.py`):
1. Grayscale; `ink = luminance >= 80`.
2. **Drop the frame:** label connected components, remove any touching the image border (kills nano-banana's white frame/banner-edge).
3. **Drop specks:** keep components with `area >= max(600, 0.04 × largest)` (removes spatter flecks that otherwise inflate the bbox and shrink the real sigil).
4. **Solid alpha, not luminance-alpha.** Early attempts set `alpha = luminance` → mid-grey brushwork went semi-transparent and vanished on transparency. Use the kept mask as solid, with a 3px dilation to retain anti-aliased edges.
5. bbox-crop the kept shape, scale to ~900px, center in a 1024 cell.

**Two outputs from the same mask:**
- **White-on-transparent** (`RGB=255,255,255`, `alpha=shape`) — the GauntletUI sprite + the game-format sheet. Matches existing `ui_taom_bannericons_*` (opaque pixels are pure white).
- **Green-on-black** (`R=B=0`, `G=shape`, opaque) — the banner-material source. Matches existing `AssetSources/BannerIcons/*.psd` (the green channel carries the sigil shape; the banner shader recolors from it).

Assemble 16 cells (row-major, `texture_index` 0 = top-left … 15 = bottom-right) into a 4096×4096 sheet for the material source; keep the per-emblem 1024 PNGs for the sprite source.

## Step 3 — `banner_icons.xml` (repo)

Add one `<BannerIconGroup>` per material sheet in `Main/_Module/ModuleData/banner_icons.xml` (before `<BannerColors>`). Conventions:
- **Group id**: faction block × 10; second sheet = `+1` (Mordor a1 `190` → a2 `191`; Gundabad `220`→`221`). New factions: MM Orcs `280`/`281`, Goblin `290`, Misc `300`.
- **Icon id**: unique global block, e.g. a2 sheets use the a1 base `+100` (Mordor a1 `19000–19015` → a2 `19100–19115`); new factions `28000+`, `29000+`, `30000+`.
- 16 `<Icon>` per full sheet, `texture_index` 0–15, all sharing the sheet's `material_name`.
- **Color-ids and group-ids are separate id-spaces** — a `<Color id="280">` does not conflict with `<BannerIconGroup id="280">`.

Validate well-formed XML after editing (`python -c "import xml.etree.ElementTree as ET; ET.parse(path)"`).

## Step 4a — Banner material pipeline (the render) — **Modding Kit**

The flag draws from the **material** (`_mtl.tpac`), which references the **texture** (`_tex.tpac`). You need **both**; they are produced by **two different actions**:

1. **Texture** — put the green-on-black source at `AssetSources/BannerIcons/taom_banners_NN.(psd|png)` (the next free number) and import it. **Importing the source image (PSD *or* PNG) produces ONLY the `_tex.tpac`.** (Verified 2026-06: PSD-vs-PNG makes no difference — both yield texture-only. The earlier "use PSD" theory was wrong.)
   - **Import settings:** keep Type `Albedo (DXT1/DXT5 - RGBA)`, leave **Do Not Compress** clear and tick **Do Not Generate Mips**. Runtime Details must read `4096 x 4096, 1 Mip, DXT5, 16.000 Mb`. The Kit picks the format from the source: an RGB source (no alpha channel) compiles to DXT1, 8 Mb, and the banner material then draws every sigil as speckled dots while the Kit shows the material as a grey "MATERIAL" box. So save each sheet source with an opaque alpha channel (PNG colour type 6, or a 4-channel PSD); the audit reports a 3-channel source as `no_alpha`. The inspector can print `DXT5` beside `8.000 Mb` for an RGB source: trust the size. On 2026-11-14 the repack wrote every sheet as RGB and all of them broke this way (#1272). An uncompressed sheet costs 64 Mb; all 33 sheets were that way until 2026-10-21 (2,112 Mb in total, now 336 Mb). Keep the single mip: with a full chain the banner render reads a smaller mip, and thin line-art sigils such as the Abanissa winged sun fade to a faint outline.
   - **Copy the saved `_tex.tpac` into the repo** (`Main/_Module/Assets/BannerIcons/`) before anyone builds. Every build deploys the repo's copy over the live one but leaves the live `RuntimeDataCache`, so a reverted header meets a recompiled cache: the sheet's sigils render blank, and the editor crashes on its next start while it recompiles (wEditor `TaleWorlds.Native.dll+0x4F626D`, a material with a null shader).
   - **A sheet compiled while the editor starts crashes it** (`rglIntrusive_ptr.h:151 px != nullptr`, right after `Compiled image taom_banners_NN` in the rgl log). The cache is written before the crash, so each restart gets one sheet further: restart until the editor opens, then save each re-imported texture so its `_tex.tpac` stores the new source hash. A reimport with the editor open cannot replace a cache file the editor has loaded ("Unable to write RDC"); close the editor and move that `.rdc` away instead.
   - A tpac with no `.rdc` is skipped whole (lesson: `animation-skeleton.md`). Check with `python tools/check_rdc_entries.py --under <Assets subfolder>`.
   - **A sheet that changes size needs a clean install for players.** On 2026-11-15 the `ui_taom_bannericons_2` sheet grew from 4096x512 to 4096x1024, and the install kept the old `RuntimeDataCache` file. The game crashed at boot in the NVIDIA driver (`nvwgf2umx.dll+0x1681799`) until the two cache files were moved away ([RCA](../reviews/rca-scenario-event-window-2026-11-15.md)). An update copied over the old `Modules/TAOM` can keep that cache, so the release note of a build with such a change tells players to delete `Modules/TAOM` before they install (maintainer, 2026-11-15).
2. **Material** — a **separate Kit step**: create a banner-icon **Material** named exactly `taom_banners_NN`, assign the banner shader, point it at the texture, save → writes `_mtl.tpac`. This is the step that was missing when icons showed in the picker but rendered **blank on the flag**. The existing sheets' shader uses flags `two_sided` + `modulate1` + `alpha_test` (from the `_mtl.tpac` binary).
3. `material_name` in `banner_icons.xml` must match exactly. **Verify BOTH** `_mtl.tpac` **and** `_tex.tpac` exist per material — `_tex` alone = blank flag (the picker may still show it via the GauntletUI sprite).

**Do NOT hand-clone `_mtl.tpac`.** It's a tiny (~368 B) file but carries a material self-GUID (bytes 8–23), a texture-reference GUID (tail 97–112), and an 8-byte trailing checksum (247–254) — editing the name without correctly regenerating the GUIDs/checksum produces a mis-linked or invalid material. Use the Kit.

⚠️ The editor's GauntletUI repack **deletes non-atlas folders/files under `AssetSources/GauntletUI`** — don't stash working files there; keep them in scratch / a sibling folder.
*(This step is GUI-only; it cannot be done from CLI. Building valid banner `.tpac`s requires the in-engine asset pipeline.)*

## Step 4b — GauntletUI sprite pipeline (the UI atlas) — **sprite generator**

1. Put each **white-on-transparent 1024** emblem at `GUI/SpriteParts/ui_taom_bannericons/<Icon id>.png` (e.g. `28000.png`…`28015.png`). Slice the assembled sheet to get these (cell `k` → `<base+k>.png`).
2. **Run the sprite generation** (the editor's sprite-generation flow — simplest, because it also rebuilds the `_tex.tpac`). It **repacks all** loose source icons into a fresh `AssetSources/GauntletUI/ui_taom_bannericons_*.png` atlas set (the packer chooses how many per sheet — ~9–16; you don't control the layout), rewrites `GUI/TAOMSpriteData.xml` (sprite `<Name>` = Icon id, with assigned `SheetID`/`SheetX`/`SheetY`), and builds `Assets/GauntletUI/ui_taom_bannericons_*_tex.tpac`.
   - The bare `SpriteSheetGenerator.exe` CLI is awkward: its `SingleModule` mode looks for `Config.xml` in the wrong path and silently no-ops, and it never writes the `_tex.tpac`. Use the editor flow.
3. **Texture import settings (per atlas texture).** In the editor's Texture Inspector, each `ui_taom_bannericons_NN` atlas must have **Do Not Generate Mips** ✔ and **Do Not Compress** ✔ checked (Type stays `Albedo (DXT1/DXT5 - RGBA)`) — match the existing sheets. UI needs no mipmaps, and skipping DXT compression keeps the sharp white-sigil alpha clean. New sheets imported without these look soft/artifacted. This is a manual per-texture GUI step on every new atlas (26–41 in the 2026-06 batch).
4. **Do NOT hand-author the atlas sheets.** Pre-packed `ui_taom_bannericons_N.png` you make by hand are ignored — the generator owns those filenames and regenerates them from the source folder.
5. **Bring the bake back with `pwsh tools/sync_sprite_bake.ps1`** (`-WhatIf` first), never by copying the files you expect to change. The generator re-saves every sprite category's `_tex.tpac`, not only the banner ones, and a header left behind in the repo goes back over the install at the next build beside a cache compiled for the new one. On 2026-11-14 a hand copy of the 3 banner files left the 3 `ui_custom_fonts_*_tex.tpac` headers stale (#1272). Prune first (`pack_banner_sheets.py --prune`) so a deleted icon's install PNG is not baked back in.

## Critical gotchas (each cost real time this session)

- **Repo and game-install are separate folders, not linked.** `c:\…\repos\TAOM\Main\_Module\…` vs `E:\…\Modules\TAOM\…`. The repo deploys on `build.ps1`; until then the game install is stale. The sprite generator and the game read the **game install** — sync source icons there before running it.
- **Restart the game after a re-bake.** Repacking moves existing sprites to new atlas rects; a running game holds the old texture but reads the new manifest → moved sprites render from garbage regions. Fully exit and relaunch.
- **White-on-transparent reads blank in image viewers** (white-on-white). Verify content by alpha coverage, not by eye on a white background.
- **`material_name` ≠ `ui_taom_bannericons_N`.** The XML points at the *material* (`taom_banners_*`); the `ui_taom_bannericons` atlas is the separate sprite side. Don't conflate them.
- **Two pipelines, both needed.** Wiring the XML + one pipeline isn't enough.

## Sheet names and packing (#1047)

A banner key, a saved banner and a UI sprite name only the icon id, so an icon can move to another
sheet by changing its `<Icon>` row alone, and the order in the in-game picker comes from the
`<BannerIconGroup>` rows, not from the sheets. All TAOM banner materials share one shader (their
`_mtl.tpac` files differ only in GUIDs, name and checksum), so any icons can share a sheet.

Every sheet is named `taom_banners_NN` (two digits) with no culture in the name. On 2026-10-28
`python tools/pack_banner_sheets.py` packed 20 partly used sheets into `taom_banners_packed_alpha_01`
to `_10`. On 2026-11-14 every sheet was renamed to `taom_banners_01` to `_25`, numbered in the order
`banner_icons.xml` first names them, and `taom_banners_dwarves_alpha_02` and
`taom_banners_elven_alpha_01` were merged into `taom_banners_11` (26 sheets became 25). The same day
163 icons were deleted (389 became 235 with the 9 new ids 31022 to 31030) and sheets 16-25 moved into the freed cells (25 sheets became 15; see
the inventory below). Four of those icons were still flown (17006 to 17008 and 10505), because their
banner keys hold negative rotations that a digits-only key parse skips; #1284 repointed both banners, and on 2026-11-15 the four icons came back with their ids on `taom_banners_16` and both keys fly them again.
Before you delete an `<Icon>` row, run `python -m pytest tools/tests/test_banner_icon_ids.py`: its
key parser reads negative tokens, and a deleted icon that any shipped key names fails it.

**Adding emblems.** Draw them on a new sheet (Step 2) named the next free `taom_banners_NN`, add their
`<Icon>` rows (Step 3) and the sprites (Step 4b). The banner editor's atlas builder picks that number
itself. The tool then moves a sheet that uses fewer than 16 cells, whole and largest first, into the
free cells of another partly used sheet. Only when none has room do two or more of them share a new
sheet; a sheet that would sit alone stays put, so a second run is a no-op. It refuses duplicate icon
ids. The dry run lists the free cells.

**Names.** A sheet whose name does not match `taom_banners_NN` is renamed to the first free number
(the audit reports it as `legacy_name`). A sheet that only changes its name is copied byte for byte,
so a PSD keeps its layers. A sheet that receives cells is written as a PNG; when the receiving source
is not a PNG it gets a new number, so the old PSD stays as it was until the prune.

1. `python tools/pack_banner_sheets.py`, read the plan, then `--apply`. It writes the new sources
   (repo and install) and repoints the moved `<Icon>` rows. It refuses while the audit reports a
   missing sheet, an empty or shared cell, or unnamed art, because it copies only the cells a row names.
2. Build each sheet the plan names: in the banner editor, Build game resources for that material, then
   Deploy (one material per build). By hand: Step 4a (re-import a changed sheet; for a new one also
   make its material), save, then copy both `.tpac` into `Main/_Module/Assets/BannerIcons/`.
3. `./build.ps1` with the game and the Kit closed, so the install's `banner_icons.xml` names the
   new sheets.
4. `python tools/pack_banner_sheets.py --prune` lists every sheet that neither `banner_icons.xml`
   names, in the repo (source, `.tpac`) and the install (the same, plus the texture's
   `RuntimeDataCache/<GUID>.rdc`, read from bytes 8 to 23 of its `_tex.tpac`, deleted before the
   texture). It also lists each install sprite PNG in `GUI/SpriteParts/ui_taom_bannericons` that the
   repo does not have and no `banner_icons.xml` names: the editor's sprite generation packs every loose
   PNG it finds, so an icon deleted in the repo comes back into the picker atlas until the install copy
   goes. Prune before you regenerate the sprites. `--prune --apply --backup-dir <dir>` deletes them, each backed up first. It refuses
   while the audit has any other finding and while the game or the Kit runs.

`python tools/pack_banner_sheets.py --audit` is the gate (`tools/tests/test_pack_banner_sheets.py`):
every icon names a sheet that exists and a cell that holds art, no two icons share a cell, no art
cell is unnamed, every icon has a `ui_taom_bannericons` sprite part in `TAOMSpriteData.xml` and its
PNG, every such sprite part or PNG is named by an icon row (`orphan_sprite`, #1274; `KNOWN_ORPHAN_SPRITES`
holds back 22004 until the Kit regeneration removes it, and an entry that is no orphan any more is a
`stale_known_orphan` finding), every material has both packages, each `_mtl.tpac` names its `_tex.tpac` and each `_tex.tpac`
holds the hash of its current source (so a sheet changed after its Kit import is a finding), every
sheet is named `taom_banners_NN`, every sheet source has an alpha channel (`no_alpha`), no sheet file is
unused, and no packing plan would save a sheet. The tool writes every repacked sheet as RGBA with
opaque alpha.

## Current inventory (2026-11-15, after #1047 and #1265)

16 sheets, 244 icons on 2026-11-15 (`grep -c '<Icon ' Main/_Module/ModuleData/banner_icons.xml`). On
2026-11-14 Tom deleted 163 icons meant to be ones no shipped banner key or career screen used (rows,
thumbnails and `banner-ai/credits.json` entries; four were still flown, see above). Their cells became the
placeholder checkerboard, and sheets 16-25 moved into the free cells of 01-15 (06, 07 and 09 did
not change); the free cells are the end of `taom_banners_16`. `banner_icons.xml` is the
authority; the dry run of `pack_banner_sheets.py` lists the free cells. Group names are the
`<BannerIconGroup>` names.

| material_name | Icons | Groups and icon ids |
|---|---|---|
| `taom_banners_01` | 16 | Dúnedain 10000-10001, 10003-10015; Orcish 28110 |
| `taom_banners_02` | 16 | Dúnedain 10016-10027, 10030, 31006; Orcish 22105, 22113 |
| `taom_banners_03` | 16 | Dúnedain 10500, 11000-11004, 11006, 31008-31009; Dwarven 24016, 24019-24020; Dunlending 26102, 26107-26109 |
| `taom_banners_04` | 16 | Adûnaic 12000-12006; Elven 31011-31013; Dwarven 24021-24024; Miscellaneous 31010, 31014 |
| `taom_banners_05` | 16 | Haradrim 13000-13004; Dwarven 24025; Dunlending 26000-26009 |
| `taom_banners_06` | 16 | Haradrim 25000-25015 |
| `taom_banners_07` | 16 | Easterling 14000-14015 |
| `taom_banners_08` | 16 | Elven 15000-15009, 31003, 31005; Northmen 27500, 30001; Dwarven 24029-24030 |
| `taom_banners_09` | 16 | Elven 16000-16009; Northmen 27000-27001; Orcish 23000-23003 |
| `taom_banners_10` | 16 | Elven 17000-17005; Northmen 20000-20006, 30003-30005 |
| `taom_banners_11` | 16 | Elven 31016-31018; Northmen 21006, 30006, 30011-30012, 30014-30015; Dwarven 31022, 31024-31026; Miscellaneous 30008, 31019, 31021 |
| `taom_banners_12` | 16 | Forces of Evil 18000-18003, 22000-22002; Northmen 21007; Dwarven 24501, 24505-24507, 24510, 31007; Miscellaneous 24508-24509 |
| `taom_banners_13` | 16 | Forces of Evil 19001-19015; Orcish 22003 |
| `taom_banners_14` | 16 | Dwarven 24000-24014; Miscellaneous 31015 |
| `taom_banners_15` | 16 | Elven 31035; Northmen 21000-21005, 31031-31032; Dwarven 31023, 31027-31030, 31033-31034 |
| `taom_banners_16` | 4 | Dúnedain 10505; Elven 17006-17008 |

## Related

- `docs/features/gui-sprite-system.md` — the sprite-bake pipeline in depth (bake vs render, the generator decompile, the rebake-restart gotcha).
- `docs/features/clan-heraldry.md`: how clan colours are authored, and why `Patch23_BannerColorPersistence` makes them (not the kingdom colour) drive battlefield armour tint.
- `.claude/rules/gui-ui.md` — sprite reference + bake rules.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/orientation.md](../ai-includes/orientation.md)
- [docs/modding/banners-and-heraldry.md](../modding/banners-and-heraldry.md)
- [docs/modding/cultures.md](../modding/cultures.md)
- [docs/modding/lords-and-heroes.md](../modding/lords-and-heroes.md)
- [docs/modding/recipe-add-a-culture.md](../modding/recipe-add-a-culture.md)

<!-- backlinks-end -->
