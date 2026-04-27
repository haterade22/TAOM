# Erebor Runes & Motifs — Texture/Decal System

End-to-end pipeline for adding dwarven runes, knot-trims, and heraldic motifs
to Erebor architecture. Two tiers:

1. **Texture variants** — runic versions of base PBR textures, bound to dedicated
   mesh variants. Use for *always-decorated* hero pieces (gate stones, capitals,
   throne-room pillars, treasure-room floor).
2. **Overlay-decal planes** — alpha-keyed quad meshes placed on top of plain
   walls. Use for *composable* placement where each scene varies.

The vanilla `decal_sets.xml` system is **not used** — that's for ephemeral runtime
decals (blood, footsteps), not authoring-time architectural detail.

## Naming Convention

```
t_dw_<category>_<family><n>_d.png            ← plain base texture (existing)
t_dw_<category>_<family><n>_runic_d.png      ← runic variant (Tier 1)
t_dw_<category>_<family><n>_runic_<motif>_d  ← runic variant, named motif

t_dw_decal_<motif>_<family><n>_d.png         ← overlay decal texture (Tier 2)
sm_dw_decal_<motif>_<family><n>.fbx          ← overlay decal mesh (Tier 2)
```

The `_runic` suffix is a *modifier*, not a new family — it composes cleanly
with the A/B/C texture-family rule documented in `design-patterns.md`.

## Pipeline

```
Web UI (Recraft v4 Pro)              ← author one mask
        ↓ download 4K PNG
tools/runes/raw_ai/                  ← gitignored holding pen
        ↓
tools/runes/clean_ai_mask.py         ← threshold, median-filter, downsample
        ↓
tools/runes/masks/{hero,filler}/     ← committed mask library
        ↓
tools/stamp_erebor_runes.py          ← 5-mode stamper (carved / gold / silver / bronze / mithril)
        ↓
…/Scenes/erebor/textures/*_runic_*.png   ← committed PBR triples
```

## Authoring a New Mask

### 1. Generate via Recraft v4 Pro

Use the prompt template in `tools/runes/ai_prompt.txt`:

```
dwarven rune, [MOTIF DESCRIPTION], pure black silhouette on pure white background,
heraldic device, stencil, flat vector style, no shading, no gradient,
hard edges, symmetrical, centered, single device, thick lines,
inspired by Cirth and Angerthas runes
```

**Locked web-UI settings:**

| Setting | Value | Why |
|---------|-------|-----|
| Image size | Square 1:1 | Matches mask format |
| Resolution | 4K | Max source detail; we downsample later |
| Variations | 4 | Triage best of 4 |
| Prompt Enhancer | **OFF** | Adds painterly flourish that breaks stencil intent |

Pick the best variation. Save to `tools/runes/raw_ai/<motif>.png` (gitignored).

### 2. Clean the raw output

```bash
python tools/runes/clean_ai_mask.py tools/runes/raw_ai/<motif>.png tools/runes/masks/hero/<motif>.png
```

This thresholds at 128, median-filters speckle, downsamples to 1024×1024 grayscale PNG.

### 3. (Hero motifs only — optional) Vectorize in Inkscape

For motifs that need to scale crisply across multiple stamp sizes:

1. Open Inkscape → File → Import the cleaned PNG
2. Path → Trace Bitmap → Mode: Brightness cutoff → Threshold: ~0.5 → Update → OK
3. Delete the imported bitmap (keep the traced path)
4. File → Save As → `tools/runes/masks/hero/<motif>.svg`
5. Re-export PNG at 1024×1024 to overwrite the PNG (so the stamper sees the cleaner edges)

Skip this step if Recraft's PNG is already crisp — visual judgement call.

### 4. Stamp the mask onto base PBR textures

**One-shot, hero stamp (centered):**

```bash
python tools/stamp_erebor_runes.py \
    --base t_dw_wall_block_a1 \
    --mask tools/runes/masks/hero/<motif>.png \
    --mode gold \
    --placement centered --scale 0.55
```

**One-shot, trim band (horizontally tiled across middle):**

```bash
python tools/stamp_erebor_runes.py \
    --base t_dw_trim_a1 \
    --mask tools/runes/masks/filler/<trim_motif>.png \
    --mode carved \
    --placement band --band-y-start 0.4 --band-y-end 0.6 --tile
```

Outputs `<base>_runic_d.png`, `<base>_runic_n.png`, `<base>_runic_s.png` next to the base.

**Batch via manifest:**

Edit `tools/runes/manifest.json`, set `skip: false` for the entries you want, then:

```bash
python tools/stamp_erebor_runes.py --manifest tools/runes/manifest.json
```

## Five Blend Modes

| Mode | Treatment | Look | Best for |
|------|-----------|------|----------|
| **carved** | Diffuse darkens (×0.40), normal indents, specular dampens | Engraved stone groove | Weathered architecture, ruin pieces |
| **gold** | Replace with `(212, 175, 55)` + warm edge highlight, high spec | Vibrant yellow inlay | Hero pieces, treasure room, Bandos-Warforge wow |
| **silver** | Replace with `(190, 195, 200)` + neutral highlight, high spec | Bright neutral metal | Restrained heraldic decoration |
| **bronze** | Replace with `(140, 90, 50)` + warm-orange highlight, high spec | Warm copper-orange | Smithy / forge iconography, older hero pieces |
| **mithril** | Replace with `(210, 220, 230)` + near-white highlight, very-high spec | Pale silver-blue, brilliant | Top-tier dwarven feature pieces, throne hall |

Tuning constants live at the top of `tools/stamp_erebor_runes.py`:

- `METALS` dict — `(body, highlight, spec)` RGB tuples per metal mode (gold/silver/bronze/mithril). Edit the tuple, re-run.
- `CARVED_DIFFUSE_MULT`, `CARVED_NORMAL_DEPTH`, `CARVED_SPEC_MULT` — carved-mode behaviour.

The four metal modes share one algorithm — replace base diffuse with metal RGB inside the mask, add a bevel highlight along the edge, push specular high — and differ only in their RGB profile. Adding a new metal (copper, electrum, lead) means appending one entry to `METALS`.

The mirkwood reference at `tools/runes/reference/mirkwood_stone_engraved_*.png` is the calibration target — our `carved` mode should produce comparable groove darkness and normal-depth.

## Mask Format

- **8-bit grayscale PNG**, 1024×1024 (or square multiple of 256)
- **Black (=0)** = stamp lands at full strength
- **White (=255)** = passthrough, base texture preserved
- **Greys** = partial-strength stamp (soft edges)

The stamper scales the mask per-channel because Erebor base textures have
different resolutions per channel (4096 d/n, 2048 s).

## Tier 2: Overlay Decal Planes

For composable placement (a kitbasher decorates each wall differently in scene
editor), author a thin alpha-keyed plane mesh:

1. **Mesh**: `sm_dw_decal_<motif>_<family><n>.fbx` — a flat 3 m × 1 m quad with UV (0..1) covering one face
2. **Texture set**: `t_dw_decal_<motif>_<family><n>_{d,n,s}.png` — RGBA where alpha channel is the mask
3. **Material**: bound by name from the FBX
4. **Placement**: in scene editor, place against the target wall with a small Z-offset along the wall normal (a few cm) to prevent z-fighting
5. **Scene XML**: standard `<game_entity>` with mesh reference

Tier 2 author flow is the same first three pipeline steps (Recraft → clean →
mask) but the mask is then composited as the *alpha channel* of an RGBA texture,
not stamped onto a base PBR. A separate tool entry-point is a likely follow-up.

## Adding a New Motif (Checklist)

- [ ] Add motif description to `tools/runes/ai_prompt.txt` per-motif catalogue
- [ ] Generate via Recraft, download to `tools/runes/raw_ai/<motif>.png`
- [ ] Run `clean_ai_mask.py` → `tools/runes/masks/{hero,filler}/<motif>.png`
- [ ] (Hero only, optional) Vectorize → `tools/runes/masks/hero/<motif>.svg`
- [ ] Add stamper entry to `tools/runes/manifest.json` (or run one-shot)
- [ ] Run `stamp_erebor_runes.py` → committed `_runic_*` textures
- [ ] (Tier 1) Author mesh variant `sm_dw_<thing>_runic_<family><n>.fbx` in Blender, bind to runic texture set
- [ ] (Tier 2) Author overlay plane `sm_dw_decal_<motif>_<family><n>.fbx`
- [ ] Add to a test scene via `build_test_erebor_house.py` or `build_test_erebor_tower.py`
- [ ] Eyeball in Bannerlord scene editor, tune mode constants if needed

## Reference

- `tools/runes/reference/mirkwood_stone_engraved_{d,n,s,h}.png` — vendored from
  `LOTR_Map\AssetSources\mirkwood\Kitbash\textures\` as a calibration target.
  Demonstrates a working engraved-stone PBR set in the same naming style.
- `docs/kitbash/erebor/design-patterns.md` — A/B family-mixing rule (the
  `_runic` suffix composes with this, doesn't replace it).
- `docs/kitbash/erebor/textures.md`, `materials.md`, `meshes.md` — base catalogues.

## Out of Scope

- Vanilla `decal_sets.xml` system — wrong tool (ephemeral runtime decals only)
- Procedural in-shader rune generation — engine shaders are not modifiable
- Animated/glowing runes (Moria-style "speak friend and enter") — possible
  later via emission texture, not in this round
- Lore-correct Cirth/Angerthas glyph semantics — visual read at gameplay
  distance matters more
