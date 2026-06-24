# Banner-Icon Generation & Wiring (start to finish)

How to create new clan/faction banner **sigil icons** for TAOM with AI (imagine.art), key them, and wire them through **both** banner pipelines so they render in-game. Distilled from the 2026-06 batch that added 9 new sheets (Misty Mountain Orcs ×2, Goblin, Isengard #2, Mordor #2, Gundabad #2, Dol Guldur #2, Dunland #2, and a mixed Rohan/Rhûn/Lindon/Dale sheet).

> **The single most important fact:** there are **two independent pipelines**, both keyed by the same icon ids from `banner_icons.xml`, and **both** must be produced:
> 1. **Banner material** (`taom_banners_*` `.tpac`) — what the banner actually *renders* from (vanilla system).
> 2. **GauntletUI sprite atlas** (`ui_taom_bannericons_*` + `TAOMSpriteData.xml`) — the per-icon UI sprite set, packed by the sprite generator.
> A new icon shows **blank** until the relevant pipeline's binary asset is compiled. Static review can't prove an icon renders — only the running game can.

## The three id layers (all share the icon id)

| Layer | Where | Keyed by |
|---|---|---|
| Definition | `Main/_Module/ModuleData/banner_icons.xml` — `<BannerIconGroup>` → `<Icon id material_name texture_index>` | the **Icon id** (e.g. `28000`) |
| Render material | `taom_banners_<culture>_alpha_NN` (the `material_name`) at cell `texture_index` (0–15) | material_name + texture_index |
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

1. Put the **green-on-black** source at `AssetSources/BannerIcons/taom_banners_<culture>_alpha_NN.psd` (PNG works as import too).
2. In the **Bannerlord Modding Kit** (editor), import the texture and create a banner-icon **material named exactly** `taom_banners_<culture>_alpha_NN`. The editor writes `Assets/BannerIcons/taom_banners_<culture>_alpha_NN_mtl.tpac` + `_tex.tpac`.
3. The `material_name` in `banner_icons.xml` must match that name exactly. **No tpac → blank icon** (no crash).
*(This step is GUI-only; it cannot be done from CLI. Building valid banner `.tpac`s requires the in-engine asset pipeline.)*

## Step 4b — GauntletUI sprite pipeline (the UI atlas) — **sprite generator**

1. Put each **white-on-transparent 1024** emblem at `GUI/SpriteParts/ui_taom_bannericons/<Icon id>.png` (e.g. `28000.png`…`28015.png`). Slice the assembled sheet to get these (cell `k` → `<base+k>.png`).
2. **Run the sprite generation** (the editor's sprite-generation flow — simplest, because it also rebuilds the `_tex.tpac`). It **repacks all** loose source icons into a fresh `AssetSources/GauntletUI/ui_taom_bannericons_*.png` atlas set (the packer chooses how many per sheet — ~9–16; you don't control the layout), rewrites `GUI/TAOMSpriteData.xml` (sprite `<Name>` = Icon id, with assigned `SheetID`/`SheetX`/`SheetY`), and builds `Assets/GauntletUI/ui_taom_bannericons_*_tex.tpac`.
   - The bare `SpriteSheetGenerator.exe` CLI is awkward: its `SingleModule` mode looks for `Config.xml` in the wrong path and silently no-ops, and it never writes the `_tex.tpac`. Use the editor flow.
3. **Texture import settings (per atlas texture).** In the editor's Texture Inspector, each `ui_taom_bannericons_NN` atlas must have **Do Not Generate Mips** ✔ and **Do Not Compress** ✔ checked (Type stays `Albedo (DXT1/DXT5 - RGBA)`) — match the existing sheets. UI needs no mipmaps, and skipping DXT compression keeps the sharp white-sigil alpha clean. New sheets imported without these look soft/artifacted. This is a manual per-texture GUI step on every new atlas (26–41 in the 2026-06 batch).
4. **Do NOT hand-author the atlas sheets.** Pre-packed `ui_taom_bannericons_N.png` you make by hand are ignored — the generator owns those filenames and regenerates them from the source folder.

## Critical gotchas (each cost real time this session)

- **Repo and game-install are separate folders, not linked.** `c:\…\repos\TAOM\Main\_Module\…` vs `E:\…\Modules\TAOM\…`. The repo deploys on `build.ps1`; until then the game install is stale. The sprite generator and the game read the **game install** — sync source icons there before running it.
- **Restart the game after a re-bake.** Repacking moves existing sprites to new atlas rects; a running game holds the old texture but reads the new manifest → moved sprites render from garbage regions. Fully exit and relaunch.
- **White-on-transparent reads blank in image viewers** (white-on-white). Verify content by alpha coverage, not by eye on a white background.
- **`material_name` ≠ `ui_taom_bannericons_N`.** The XML points at the *material* (`taom_banners_*`); the `ui_taom_bannericons` atlas is the separate sprite side. Don't conflate them.
- **Two pipelines, both needed.** Wiring the XML + one pipeline isn't enough.

## Current inventory (2026-06 batch)

| Sheet | material_name (Step 4a) | group | Icon ids / sprite `<id>.png` (Step 4b) |
|---|---|---|---|
| MM Orcs #1 | `taom_banners_mistymountainorcs_alpha_01` | 280 | 28000–28015 |
| MM Orcs #2 | `taom_banners_mistymountainorcs_alpha_02` | 281 | 28100–28115 |
| Goblin | `taom_banners_goblin_alpha_01` | 290 | 29000–29015 |
| Isengard #2 | `taom_banners_isengard_alpha_02` | 231 | 23100–23115 |
| Mordor #2 | `taom_banners_mordor_alpha_02` | 191 | 19100–19115 |
| Gundabad #2 | `taom_banners_gundabad_alpha_02` | 221 | 22100–22115 |
| Dol Guldur #2 | `taom_banners_dolguldur_alpha_02` | 181 | 18100–18115 |
| Dunland #2 | `taom_banners_dunland_alpha_02` | 261 | 26100–26115 |
| Misc (Rohan/Rhûn/Lindon/Dale/Goblin-King/MM-antlers/…) | `taom_banners_misc_alpha_01` | 300 | 30000–30015 |

Status: `banner_icons.xml` wired ✅; green material sources placed in `AssetSources/BannerIcons/` ✅; per-icon sprite sources sliced into `GUI/SpriteParts/ui_taom_bannericons/` ✅. **Remaining:** Step 4a Kit material compile (×9) + Step 4b sprite-generation run + game restart. Green sources / previews / assembled sheets archived under `AssetSources/GauntletUI/_taom_new_banners/`.

## Related

- `docs/features/gui-sprite-system.md` — the sprite-bake pipeline in depth (bake vs render, the generator decompile, the rebake-restart gotcha).
- `docs/features/clan-heraldry.md` — how clan colors are assigned/applied at runtime.
- `.claude/rules/gui-ui.md` — sprite reference + bake rules.
