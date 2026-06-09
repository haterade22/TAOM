# Bannerlord Weapon Piece Aligner

A standalone Windows tool for **Mount & Blade II: Bannerlord** modders to visually preview and adjust
weapon **crafting-piece offsets** — without launching the game. It reads your `crafting_pieces.xml`
(and optionally your FBX meshes) and shows a live 2D + 3D preview of how the assembled weapon will look,
using the **exact assembly math from the game engine**.

It depends on nothing but two public NuGet packages — **no Bannerlord install, no mod, and no other
project required to build or run.** It works with vanilla `crafting_pieces.xml` or any mod's.

---

## Download & run (no toolchain needed)

1. Grab the latest **`BannerlordCraftingTool-win-x64.zip`** from the **[Releases page](../../releases)**.
2. Unzip anywhere.
3. Double-click **`BannerlordCraftingTool.exe`**.

The release build is **self-contained** — it bundles its own .NET runtime, so you do **not** need .NET
installed. Windows x64 only.

> Prefer to build it yourself? See [Build from source](#build-from-source) below.

---

## What problem it solves

Bannerlord assembles a crafted weapon from pieces (Blade, Guard, Handle, Pommel) chained along the
weapon's axis. Their positions are controlled by three offset values per piece inside `<BuildData>` in
`crafting_pieces.xml`:

```xml
<BuildData
    piece_offset="-5"
    previous_piece_offset="3"
    next_piece_offset="2" />
```

Tuning these by hand means: edit XML → launch the game → check the crafting bench → repeat. This tool
replaces that loop with a live preview that reproduces the engine's piece-positioning exactly.

---

## How to use

### 1 — Load pieces
- **Pieces XML** → **Browse** to a `crafting_pieces.xml` and **Load**. Examples:
  - Vanilla: `…\Mount & Blade II Bannerlord\Modules\Native\ModuleData\crafting_pieces.xml`
  - A mod: that module's `…\ModuleData\<your>_crafting_pieces.xml`
  - **Add +** merges a second XML into the current set (e.g. your file + a teammate's).
- **Templates XSLT** (optional) → **Browse** to a `crafting_templates.xslt` (or a base
  `crafting_templates.xml`) and **Load**. This filters the piece dropdowns per weapon template. Build
  orders for all vanilla templates are **bundled**, so this step is optional.

### 2 — Pick a template and pieces
- Choose a **Weapon Template** (e.g. `OneHandedSword`, `OneHandedAxe`, `TwoHandedPolearm`) to narrow the
  dropdowns to valid pieces and set the correct assembly order.
- Use the search box above each dropdown to filter by id. Select a Blade / Guard / Handle / Pommel
  (axes/maces have no guard — leave it `(None)`). The preview updates live.

### 3 — Adjust offsets
- Click a piece in the **2D canvas** or the **3D viewport** to make it active (its id shows at the top
  of the **Offset Editor**).
- Nudge `piece_offset`, `previous_piece_offset`, `next_piece_offset` with the **−/+** step buttons or by
  typing a value. Set a per-use `scale_factor (%)` to preview a scaled piece.
- The **XML Output** panel shows the corrected `<BuildData … />` snippet — **Copy** it back into your XML.
- **Export tuned offsets (JSON)** writes only the pieces you changed (original + tuned values) — handy
  for batch-patching your XML or feeding an assistant.

### 4 — 3D preview (optional)
- Switch to the **3D Preview** tab and set the **FBX Folder** (it indexes every `.fbx` in the folder +
  subfolders), **or drag `.fbx` files / a folder onto the viewport**.
- Click any mesh to select that piece. Orbit/zoom with the mouse.

---

## Key concepts

### Assembly order (engine-accurate)
Pieces are placed in the **template's `build_order`**, branching on the sign of each piece's order. The
**Handle is the anchor (build_order 0), at the hand grip (Z=0)**:

```
butt  ←  Pommel(-1)      Handle(0 = grip, Z=0)      Guard(+1) →  Blade(+2)  → tip
            (−Z)                                                  (+Z)
```

- Swords / polearms / pikes / javelins: `Handle 0, Guard +1, Blade +2, Pommel −1`.
- Axes / maces: `Handle 0, Blade +1, Pommel −1` (no guard).

The tool bundles the vanilla build orders and auto-selects the right one. Load a base
`crafting_templates.xml` to override with that file's real `<PieceDatas>` (for custom templates).

### How offsets work
Spacing between two adjacent pieces is dominated by their **half-lengths** (`Length / 2` each — or the
explicit `distance_to_next_piece` / `distance_to_previous_piece`). The three `<BuildData>` offsets are
*subtracted nudges* on top:

| Attribute | Effect |
|---|---|
| `piece_offset` on the **Handle** | **Slides the whole weapon along its axis relative to the grip.** It does NOT move the handle vs the blade — it translates every piece together. The usual "weapon sits too high/low in the hand" fix. |
| `piece_offset` on other pieces | Shifts that piece (and everything beyond it on the same side). |
| `previous_piece_offset` | Tightens / loosens the joint toward the grip-side piece. |
| `next_piece_offset` | Tightens / loosens the joint toward the tip-side piece. |

The tool **faithfully reproduces** `WeaponDesign.CalculatePivotDistances()` + `CalculateWeaponLength()`
from the game engine (`TaleWorlds.Core`, v1.4.5) — including `scale_factor` — so the pivot positions and
the reported **Weapon length** match what the game computes. The grip line (Z=0) stays in frame, so you
see the whole weapon slide when you change the Handle's `piece_offset`.

### XML units
All values are raw XML units (= centimeters in Blender). The game multiplies by `0.01` internally; the
tool stays in raw units so what you see matches what you type into the XML. The **Weapon length** readout
is in those units (≈ cm; the `reach` figure is the rounded `WeaponLength` stat the game stores).

---

## FBX mesh loading

**Two-phase** for speed: an *index* pass opens every `.fbx` with minimal processing and maps node/mesh
names to their file; the *load* pass decodes geometry only for the selected piece's mesh.

**Name matching** is case-insensitive against FBX node names and mesh names. A `.lod0` suffix is handled
(`wm_axe_blade.lod0` is also indexed as `wm_axe_blade`). **Skipped:** `_lod1`–`_lod9`, prefixes `bo_` /
`col_` / `ub_`, and a node named `armature`.

Vertices are transformed to world space down the node chain (applying the exporter's Z-up→Y-up
correction baked into the root). The export origin is used as the piece pivot — no bounding-box centering.

---

## Build from source

Requires the **.NET 8 SDK** (`win-x64`).

```sh
# from this folder (tools/BannerlordCraftingTool)
dotnet build                 # compile
dotnet run                   # build + launch
```

### Make a release build (self-contained, no .NET required to run)

```sh
dotnet publish -c Release -r win-x64 --self-contained true
# output: bin/Release/net8.0-windows/win-x64/publish/
```

Zip the `publish/` folder and that's the distributable — the user unzips and runs
`BannerlordCraftingTool.exe` with no prerequisites. (A folder publish is used rather than single-file
because AssimpNet's native DLL loads most reliably as a loose file beside the exe.)

---

## Project structure

```
BannerlordCraftingTool/
├── BannerlordCraftingTool.csproj   .NET 8 WPF project
├── App.xaml / App.xaml.cs          WPF entry point
├── MainWindow.xaml                 UI layout (dark theme, 3-column)
├── MainWindow.xaml.cs              parsing, assembly math, 2D canvas, 3D view
└── README.md                       this file
```

**Dependencies** (NuGet, restored automatically): `AssimpNet 4.1.0` (FBX loading), `HelixToolkit.Wpf 3.1.2`
(3D viewport). Nothing else.

---

## Features

- **Engine-accurate assembly** — verbatim port of the game's pivot + weapon-length math, build-order
  driven, Handle-anchored, with `scale_factor` support and a live weapon-length / reach readout.
- **2D + 3D preview** — schematic 2D pivot diagram (grip line always in frame) and a real FBX mesh
  viewport; click a piece in either to edit it.
- **Offset editor** — per-piece `piece_offset` / `previous` / `next` + `scale_factor`, with step buttons.
- **Copy / export** — copy a single corrected `<BuildData>` snippet, or export all tuned pieces to JSON.
- **Flexible loading** — merge multiple piece XMLs; browse or **drag-and-drop** FBX files/folders.

---

## Credits & license

Originally built by **KEYforce** for the *Tales from the Age of Men* (TAOM) Bannerlord project; the
engine-fidelity assembly math and dark theme were added in a later pass. It is a general-purpose modding
utility — usable with any Bannerlord crafting data, not tied to any specific mod.

`TaleWorlds.*` types referenced in this document are TaleWorlds Entertainment's; this tool ships none of
their code or assets — it only reproduces the documented assembly arithmetic from your own XML/FBX inputs.
