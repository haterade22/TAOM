# Banner-Icon Generation (AI prompt recipe)

How to author new clan/faction banner **sigil icons** for TAOM by generating them in an AI image tool (imagine.art), then packing and wiring them into the engine. The Misty Mountain Orcs (15 clans) are the worked example; the recipe generalizes to any faction.

## Asset spec (the constraints that drive the prompt)

| Thing | Value |
|---|---|
| Source art | `…/TAOM/AssetSources/BannerIcons/taom_banners_<culture>_alpha_01.psd` |
| Compiled sheet | `…/TAOM/AssetSources/GauntletUI/ui_taom_bannericons_N.png` — **4096×4096 RGBA** |
| Grid | **4×4 = 16 cells, each 1024×1024 px**, `texture_index` 0–15 row-major (0–3 top row … 12–15 bottom) |
| Color | **White/grey silhouette on transparency.** The engine recolors per-clan at runtime — **never bake faction color into the art.** |
| Config | `…/TAOM/ModuleData/banner_icons.xml` (`<BannerIconGroup>` → `<Icon id material_name texture_index>`) |
| Group-id convention | Steps by 10 per culture (Gondor 100, Rohan 210, Gundabad 220, … Dale 270/275) |

Because the engine wants flat white-on-transparent silhouettes, the prompt must request **flat 2D vector heraldry, solid white on solid black, no color/shading/3D** — black background keys to alpha cleanly in post.

## Step 1 — Generate the emblems (imagine.art)

**Settings:** aspect ratio **1:1**; highest resolution available (upscale toward 1024+ per emblem); pick the **highest prompt-adherence model** offered (Flux-class / latest Imagine model); guidance/CFG on the higher side. **Keep model + resolution identical across all emblems so line weight matches when assembled.**

> **Recommended: generate one emblem at a time** (below), then assemble the 4×4 grid by hand. A single generation of 16 distinct cells is the hardest grid case for any model — one-by-one gives far better per-emblem fidelity and consistency. The single-sheet prompt is kept further down only as a quick-and-dirty option.

### Misty Mountain Orcs — cell map (16 cells: 15 clans + 1 faction master)

Monochrome only; the rationale column is theming, not pixels.

| Cell | Clan | Emblem (white silhouette) | Rationale |
|---|---|---|---|
| 1 | Bûrzghâsh (T6 overlord) | jagged three-peak mountain under a crude spiked iron crown | paramount ruling house |
| 2 | Krimpâsh (infantry) | two crossed notched orc cleavers | "crushing" heavy infantry |
| 3 | Dushnakh (archers) | crude recurve orc bow + one barbed arrow | ranged-focus |
| 4 | Morgrim (elite) | horned fanged orc skull | chosen veterans |
| 5 | Vargrim | snarling warg / wolf head | "wolf-fierce" skirmisher |
| 6 | Grobûrz | cave-tunnel arch gouged with claw marks | "burrowing/low" |
| 7 | Skarnâk | row of jagged bared fangs | "scar-fang" ridge raiders |
| 8 | Maughâsh | clawed orc hand, palm forward | "dark/clawed hand" |
| 9 | Throgmaw | gaping fanged maw / jawbone | "cruel-jaw" |
| 10 | Lugbúrz | crooked jagged watchtower | "Dark Tower" |
| 11 | Hrakdûr | frost-rimmed skull hung with icicles | "frost/corpse-dark" |
| 12 | Uzgnâsh | single sharp dagger-like mountain peak | "peak-sharp" |
| 13 | Vrakmaw | great long-legged spider | Misty-Mountain spiders |
| 14 | Gashrim | screeching cave bat, wings spread | "ash/cave" dwellers |
| 15 | Morzûk | severed orc head impaled on a spear | "death-haunter" |
| 16 | Faction master | triple-peak mountain range beneath a single baleful slit eye | the Mountain Host |

### Individual-emblem prompts (recommended)

Paste **style prefix + one subject** per generation. Use the **same negative prompt** every time.

**Style prefix:**
> Single heraldic banner emblem, one centered motif, flat solid pure-white silhouette on a pure-black background, medieval coat-of-arms style, crude brutal orcish aesthetic, thick clean bold outlines, stencil-like, high contrast, perfectly centered with even margins, symmetrical, no shading, no gradient, no color, no text, no border, readable at small size. Subject:

**Subjects** (append one; clan/cell mapping in the table above):
1. a jagged three-peaked mountain topped with a crude spiked iron crown
2. two crossed notched orc cleavers
3. a crude recurve orc bow with a single barbed arrow nocked
4. a horned fanged orc skull, front view
5. a snarling warg wolf head, front view, bared fangs
6. a cave-tunnel mouth arch gouged with three deep claw marks
7. a fanged lower jawbone, row of jagged bared fangs
8. a clawed orc hand, palm forward, fingers splayed
9. a gaping fanged open maw, front view
10. a crooked jagged watchtower silhouette
11. a frost-rimmed skull hung with icicles
12. a single sharp dagger-like mountain peak
13. a great long-legged spider, top-down, legs spread
14. a screeching cave bat with wings fully spread, front view
15. a severed orc head impaled on an upright spear
16. a triple-peaked mountain range beneath a single baleful slit eye

**Negative prompt (individual):**
> color, colour, text, letters, words, numbers, watermark, signature, logo, gradient, soft shading, drop shadow, glow, photorealistic, 3d render, perspective, background scenery, clutter, extra objects, multiple emblems, blurry, low contrast, grey background, frame

Then assemble: drop each white-on-black emblem into a 1024×1024 cell on a 4096×4096 canvas (row-major, `texture_index` 0–15), centered, then key to alpha (Step 2).

### Prompt (single 4×4 contact sheet — quick-and-dirty alternative)

> A heraldic banner-sigil contact sheet for a brutal orc faction, arranged as a strict 4×4 grid of 16 equal square cells separated by thin even gutters. Each cell holds ONE emblem, perfectly centered, drawn as a flat solid pure-white silhouette on a pure-black background — medieval coat-of-arms style, crude orcish aesthetic, thick clean outlines, stencil-like, high contrast, no shading, no gradient, no color, no text. Uniform emblem scale and line weight across all 16 cells. Left-to-right, top-to-bottom:
> Row 1: (1) a jagged three-peaked mountain topped with a crude spiked iron crown; (2) two crossed notched orc cleavers; (3) a crude recurve orc bow with one barbed arrow; (4) a horned fanged orc skull.
> Row 2: (5) a snarling warg wolf head; (6) a cave-tunnel arch gouged with claw marks; (7) a row of jagged bared fangs; (8) a clawed orc hand palm-forward.
> Row 3: (9) a gaping fanged maw jawbone; (10) a crooked jagged watchtower; (11) a frost-rimmed skull hung with icicles; (12) a single sharp dagger-like mountain peak.
> Row 4: (13) a great long-legged spider; (14) a screeching cave bat with spread wings; (15) a severed orc head impaled on a spear; (16) a triple-peaked mountain range beneath a single baleful slit eye.
> Flat 2D vector heraldry, symmetrical, centered, readable at small size.

### Negative prompt

> color, colour, text, letters, words, numbers, watermark, signature, logo, gradient, soft shading, drop shadow, glow, photorealistic, 3d render, perspective, background scenery, clutter, extra symbols, overlapping cells, uneven grid, blurry, low contrast, grey background, decorative frame

### Fallback if cells blend

Generate four **2×2 quadrant** sheets (4 cells each) reusing the style block, or generate the 16 emblems **individually**, then assemble the 4×4 in Photoshop. Same end result, far better per-emblem fidelity.

## Step 2 — Pack and wire into the game (after the PNG exists)

1. **Key to alpha** (Photoshop): the art is white-on-black, so use luminance as the alpha (white = opaque sigil). Mirror the layer structure of an existing source such as `taom_banners_abanissa_alpha_01.psd`. Save as `…/AssetSources/BannerIcons/taom_banners_mistymountainorcs_alpha_01.psd`.
2. **Re-grid to exact cells:** AI grids are never pixel-aligned — cut each emblem and re-center it on a clean 4096×4096 canvas at 1024-px boundaries. Export `…/AssetSources/GauntletUI/ui_taom_bannericons_26.png` (sheets 1–25 already exist).
3. **Register the texture/material** as `taom_banners_mistymountainorcs_alpha_01`, the same way the 25 existing sheets are registered. ⚠️ The exact PNG→material/tpac packaging step is **not yet traced in this doc** — confirm against an existing sheet before wiring XML.
4. **Add the icon group** to `…/TAOM/ModuleData/banner_icons.xml` (free id `280`, icon ids `28000`+):
   ```xml
   <BannerIconGroup id="280" name="{=!}TAOM Misty Mountain Orcs Alpha 01" is_pattern="false">
     <Icon id="28000" material_name="taom_banners_mistymountainorcs_alpha_01" texture_index="0" />
     <!-- … through … -->
     <Icon id="28015" material_name="taom_banners_mistymountainorcs_alpha_01" texture_index="15" />
   </BannerIconGroup>
   ```
5. **Point the clans at the new icons:** update each clan's banner key (and/or `…/ModuleData/clan_heraldry/mistymountainorcs.json`) to reference icon ids `28000`–`28014` instead of the shared Gundabad `22000`/`22001`.

## Verify

- New campaign → Encyclopedia → each Misty Mountain Orc clan shows its own sigil (no more shared Gundabad icon).
- Banner editor lists the "TAOM Misty Mountain Orcs Alpha 01" group.
- `python tools/validate_moduledata.py` stays clean after the XML/json edits.

## Reusing for other factions

Swap the cell map for the target faction's clans, change the orcish style words to the faction aesthetic (Gondor → trees/stars/heraldic tree-and-crown; Rohan → horses/sun; Dwarves → anvils/axes/mountains), pick the next free group id (`+10`) and sheet number, and keep everything else identical. The 15 Misty clans currently share one Gundabad-borrowed icon, so this is the first faction to get bespoke per-clan sigils.

## Related

- `docs/features/clan-heraldry.md` — how clan colors are assigned/applied
- `docs/features/banner-injection.md` — banner persistence / banner_key encoding
- `docs/features/gui-sprite-system.md` — general 4096×4096 sprite-atlas pipeline
- `docs/features/new-factions-misty-mountains-lindon.md` — the faction this serves
