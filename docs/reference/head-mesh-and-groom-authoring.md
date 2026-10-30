# Head meshes built without the Kit, and groom that follows them

> **Engine:** Bannerlord v1.5.3 for every [Certain] tag. **Source:** yotthani's FaceLearner mod, read in the private
> repository `yotthani/bannerlord` (`bn faces/`: `FaceLearner/Core`, `FaceLearner.HeadExtract`,
> `docs/phenotype-races`, commit `8e040ab`) on 2026-09-30. Its notes are dated 2026-07-02 to 2026-08-10, on the
> 1.4 line of the game. Nothing on this page is FaceLearner code: each fact is restated in TAOM's words and tagged.
> Review: [adopt-yotthani-bannerlord-2026-09-30.md](../reviews/adopt-yotthani-bannerlord-2026-09-30.md); provenance:
> the "Yotthani bannerlord repository" row in [provenance-register.md](provenance-register.md).
>
> **Tags:** **[Certain]** read in the v1.5.3 decompile cache (`~/.taom-src/v1.5.3/`), `TaleWorlds.Engine.dll` or
> Native's ModuleData on 2026-09-30. **[yotthani]** measured by yotthani in game or on a package; TAOM has not
> reproduced it. **UNVERIFIED** nobody has established it.
>
> **Second pass, 2026-10-29:** the sections below also restate yotthani's later `bn faces/` work (commits to
> `6265f4a2`, 2026-09-24 to 2026-10-20, 1.5.3 to 1.5.5 per the mod's own version gate in
> `FaceLearner/Core/NativeHeadHook.cs@6265f4a2:1038-1050`). Those facts name their path@6265f4a2 and lines in the
> sentence. A tag that names v1.5.6 was checked by TAOM on 2026-10-29 against the installed game (Native
> `skins.xml`, the v1.5.6 decompile cache, a Ghidra decompile of `TaleWorlds.Native.dll`); every other upstream
> fact is **[yotthani]** and its engine version is the one the source states.

**Why TAOM cares.** TAOM builds race heads in the Modding Kit, and
[race-face-and-hand-morphs.md](race-face-and-hand-morphs.md) owns that process. FaceLearner built heads with no Kit
at all: it cloned vanilla's `head_male_a`, reshaped it, and wrote the package with a patched TpacTool. To make that
work it had to learn what a head package must hold, how the face channels divide up, why a head's neck shows a seam,
what the head material needs, and how beards and eyebrows follow a face whose shape is baked in. Those answers apply
to any custom head, Kit-built or not.

## 1. The 101 face channels and the deform keys

- **The keys** [Certain, Native `skins.xml` on 1.5.3]: every human skin has **63** `deform_key`s whose
  `key_time_point` values are 1 to 63, one channel each. 1 to 58 are sliders **except four** that carry
  `group_id="-1"` (no slider in the face editor): 46 `head_scaling`, 51 `hide_ears`, 57 `old_face` and 58 `kid_face`
  [Certain, v1.5.6 Native `skins.xml`, parsed with ElementTree on 2026-10-29: all four are `group_id="-1"` in all ten
  skins; `FaceGenVM.cs:1835-1838` on v1.5.5, the nearest cached version, adds a key to a tab only when `GroupId > -1
  && GroupId < 7`]. Some skins pin `old_face` at 0 to 0 (kid and toddler skins on v1.5.6), and `kid_face` varies with
  maturity. 59 is `eyebump`, pinned at 1 to 1; 60 `weight`, 61 `build`, 62 `height` and 63 `age` carry no range and
  the engine drives them. Ten or eleven keys per skin are **inverted** (`key_min` above `key_max`: `eyebrow_depth`,
  `eye_position`, `nose_angle`, `kid_face` and others), so normalise a slider as `(w - key_min) / (key_max - key_min)`,
  which a plain min and max would mirror. No key names a channel above 63. FaceLearner's notes count 64 keys, from an
  older build; its list of engine keys also omits 60, `weight`.
- **Channel 51, `hide_ears`, is engine-driven** [Certain for the attributes, v1.5.6 Native `skins.xml:478-486`:
  `key_min="0" key_max="1.5" group_id="-1" deforms_hair="1.0" auto_activate_when_ears_are_hidden="true"`; [Likely]
  for the link, TAOM's reading of the attribute name against `Equipment.EarsAreHidden`, which on v1.5.6
  (`TaleWorlds.Core.Equipment.cs:109-118`) is true for hair cover types Type2, Type3 and All and false for None,
  Type1 and Type4]. yotthani (`yotthani/bannerlord`, `bn faces/FaceLearner.HeadExtract/Program.cs@6265f4a2:270-277`,
  a comment dated 2026-10-02, with a render probe on its `aragmv` head) found that a head with frame 51 zeroed
  kept its ears standing ("blieben unsere Ohren stehen") under helmets that leave the head visible, and that "Form + 1,5 x Frame 51" took the
  widest ear |x| from 92.8 mm to 72.0 mm against vanilla's 72.2 mm [yotthani, measured on a render probe, no engine
  version stated]. **TAOM risk, not reproduced:** `tools/blender/add_face_morph_channels.py` writes 101 zero channels
  for a part that has none, so a race head built that way keeps its ears under a helmet that hides them. Keep frame 51
  when a tool zeroes slider channels, as for 59 and up. `tools/oneoff/tune_face_slider_reach.py` scales every ranged
  key (`ranged_keys`, lines 73-84), engine-driven ones included; whether that matters is a design call.
- **Channels 64 to 100 are facial animation** [yotthani]: blinking, speech and expression. Zeroing them froze the
  eyelids while the eyeballs kept moving (they are bones, not morphs). A tool that zeroes a head's slider channels
  must keep 51 and 59 and up. This answers part of the open question in race-face-and-hand-morphs.md about how the keys
  map onto the 101 channels: `key_time_point` is the channel index.
- **Setting a face from code** [yotthani, from a character-creation patch]:
  - `BodyProperties` carries neither the race nor the gender, so `GetParamsFromKey` cannot restore them. Packing a
    key against the wrong race uses the wrong deform-key layout and the character builder throws a
    NullReferenceException in the `AgentVisuals` constructor.
  - Set the age before packing: the editable key set and channel 63 depend on it.
  - `ProduceNumericKeyWithParams` yields only the static half (the packed key); attach age, weight and build again,
    or the character arrives generic and young.
  - In the face editor, `UpdateFace` re-derives `CurrentBodyProperties` from the view model's live
    `FaceGenerationParams`, so write both, or the next refresh reverts the face.
  - `FaceGenVM` lives in `TaleWorlds.MountAndBlade.ViewModelCollection`, which is not loaded at SubModule `PatchAll`
    time: a patch on it must be applied late, or Harmony throws during module load.
  - **Age, weight and build sit beside the key; height multiplier, hair, beard and hair colour are inside it**
    [yotthani, `bn faces/FaceLearner/Core/HeadPresets.cs@6265f4a2:8-22`, a doc comment, code reading plus the
    author's play; no engine version stated]. Race and gender must be set before packing, and so must the age, because
    the slider set depends on it. `FaceGenVM.SetBodyProperties` takes age, weight and build "nur, wenn deren Regler
    sichtbar sind (im Einzelspieler ohne facegen.show_debug nicht)" (same file, lines 95-96), so the source sets
    `BodyGen.CurrentBodyProperties` first. A packing that fails returns an empty key; the source never passes it on
    because the character build would then crash deep in AgentVisuals ("der stuerzt tief in AgentVisuals ab", line 81). It clamps hair and beard indices to the
    race's list length, noting that TAOM elves have only "kein Bart" in theirs (lines 19-20).
  - **Patching in OnSubModuleLoad is suspected of laying the creator's figure on the ground** [yotthani, unconfirmed,
    `bn faces/FaceLearner/SubModule.cs@6265f4a2:143-148`, a comment dated 2026-10-12; "in-game check pending" per the
    commit message of `c288efa7`]. The author's Harmony patches in `OnSubModuleLoad` made the runtime compile
    `BodyGeneratorView.AddCharacterEntity` and `MBAgentVisuals.AddSkinMeshes`; the comment says static initialisers
    "koennen" run before the engine has read `action_types.xml`, which would leave `ActionIndexCache` fields unresolved (TAOM's reading: at -1, the value `GetActionCodeWithName` returns for a name it does not know, [bannerlord-animation-system-map.md](bannerlord-animation-system-map.md) section 7). The
    author moved the patching to `OnBeforeInitialModuleScreenSetAsRoot`. TAOM's reading: if character creation ever
    shows the figure on the ground, check what patches run at module load before suspecting the face.

## 2. A head package built without the Kit

FaceLearner's `buildpheno` recipe [yotthani; loaded and rendered in game on 2026-07-03]:

1. Clone `head_male_a` from `Native/AssetPackages/core_game.tpac`.
2. Move `EditData.Positions`, **all 101 morph frames** and the render `VertexStream` by the same displacement field.
3. Recompute the tangent basis (section 3), give the metamesh, its meshes and data segments **fresh GUIDs**, rename,
   and save one package holding mesh, material and textures.
4. Wire it: a `skins.xml` race with `skeleton="human_skeleton"`, deform keys cloned from Native (same ids), and
   `face_meta_mesh` naming the new head; a Monster with `base_monster="human"`; `skins.xml` registered in
   `project.mbproj` as `type="skin"`.

It loaded from FaceLearner's own `AssetPackages/`, a module with no loose `Assets/` tree. TAOM's module loads its
loose tree instead, so hand-built packages are unproven in TAOM; see
[tpac-static-prop-authoring.md](tpac-static-prop-authoring.md) "Known limitation".

The traps, each measured by yotthani:

- **Reused GUIDs crash.** Keeping `head_male_a`'s GUIDs collides with the native asset: the GPU morph mapping
  resolves an ambiguous pointer and faults. `skins.xml` resolves heads by name, so new GUIDs are safe.
- **A missing `face_meta_mesh` crashes hard** in `gpu_morph_mapping`, with no fallback: every registered skin needs a
  loadable head.
- **A copied head with no edit data on its LODs crashes the groom bind** [yotthani, 1.5.3 offsets,
  `bn faces/FaceLearner/Core/NativeHeadHook.cs@6265f4a2:952-962`, a comment about the "dritter Spieltest" of
  2026-10-02, crash location "aus dem Log"]. The crash site was `Native+0x71370 <- 68d30 <- 56f360`, a read of
  `0x10`. The comment's reading: 56f360 loads the edit data of every mesh in the head metamesh, and for a
  `MetaMesh.GetCopy` copy 68d30 takes the base mesh's edit data "OHNE zu pruefen, ob das Basis-Mesh welche hat", while
  "die LOD-Meshes unseres Pakets haben keine". It is FaceLearner's own package that the comment describes; whether a
  Kit head can reach it is UNVERIFIED. A native crash with that chain names a copied head metamesh whose LODs have no
  edit data.
- **Upstream TpacTool's `Save` corrupts meshes**: a 4-byte prefix per vertex channel (+56 bytes a mesh) that the
  native GPU morph pass does not expect. [tools/README.md](../../tools/README.md) already warns about it.
- **`MarkLongLive()` before any edit.** TpacTool's `ExternalLoader` holds the data through a `WeakReference`; if the
  collector runs between the edit and the save, the save rereads the original and the edit vanishes without a trace
  (a deployed head's eyes sat 0.00 mm from vanilla's after a build that logged moving them).
- **The engine deforms `EditData`, not the render stream.** Edit both, and both bone sets. `head_male_a` has 1,389
  render and 1,269 edit vertices, so pair them by position, not index.
- **A metamesh holds sub-parts besides its LODs** (`head_male_a.1`, `.2`, `beards_c_g.1`): process every part, not
  the first. Keep LOD1 to LOD5: a head without them gives the LOD dither-fade nothing to blend to (flicker and a
  semi-transparent veil).
- **Never round-trip through OBJ**: it duplicates vertices at UV and normal seams (2,810 to 8,383 on `beards_c_j`)
  and loses the mapping to UVs and skinning. Move raw binary.
- **TpacTool's structs format with the current culture** (a German system writes `0,47`); force the invariant
  culture. It cannot decode BC7 and reports the same format as BC3 or DXT5.

## 3. Shading and the neck seam

A reshaped head showed shading seams, then a hairline crack at the neck that let the background through. Every
cause below was proven in game (2026-08-10) [yotthani]:

- **Recompute the basis after deforming**, or the shader lights the old orientation. The QTangent convention (rows
  N, T, B, the w sign for handedness) is in [tpac-static-prop-authoring.md](tpac-static-prop-authoring.md).
- **Weld normals across seams, and only normals.** Vanilla's head is unwelded islands (positions doubled for separate
  UV islands) but stores averaged normals: 110 coincident groups, 102 with an identical normal. FaceLearner's own had
  272 groups, 1 identical, 40 degrees mean spread: a lighting step along every seam. Average the normals (area
  weighted); leave tangents, which follow the UV islands, and never weld positions.
- **The neck rim's normals must be vanilla's.** The body joins there with its own averaged normals: 16 of 66 rim
  points were up to 34 degrees off.
- **No T-junctions on the rim.** The head's neck had 95 rim points against the body's 66; the extra 29 lay on the
  body's edges (0.001 mm off), and the rasteriser rounds the long edge and the split one differently: a hairline
  crack. Moving them does nothing; collapse each onto its neighbour that has a body partner.
- **No `skinning_precise` on the head material.** The body runs `[bumpmap, skinning]`; a head with `skinning_precise`
  skins the shared rim at a higher precision, a sub-pixel offset on the common edge. It is an evaluation switch, not a
  vertex format: removing it keeps the mesh intact and closed the seam.
- **Skin weights from the nearest triangle, never the nearest vertex.** The globally nearest vertex crosses the jaw
  and neck bone boundary, and the vertex is dragged when posed (a fold invisible in Blender, which runs no skeleton).
  Take the bones of the hit triangle, mix their weights barycentrically, and keep a vertex that sits on a corner
  exactly as it is. Do the same for the `EditData` bone set, or the engine deforms the old one.
- **Fill `Uv2`** (vanilla's convention in the same package: equal to `Uv1`). With `Uv2` all zero and the material's
  `AreamapAmount` at 0.65, every vertex reads one corner texel of the area map: an even darkening that no measurement
  of the diffuse texture can see. **Beards are the exception** (section 6): their `Uv2` is constant.
- **A dense head can show saw-toothed lip and lid edges from the half-float base** [yotthani, in game from a user
  video of 2026-10-02, `bn faces/FaceLearner.HeadExtract/Program.cs@6265f4a2:3317-3326`; no engine version stated].
  The comment says the engine computes the head base from `CompressedPositions` (Half4), that half precision steps
  "0,98 mm" at z 1.6 m (y 0.12 mm, x 0.06 mm), and that on vanilla's coarse mesh this goes unnoticed but on a dense
  mesh (point spacing about 1 mm) every point of a nearly horizontal edge (lip line at z 1.608, lid rim) "springt
  zufaellig auf die Stufe darueber oder darunter". Its fix places the base on the half grid by rounding, not
  truncating (truncation left a mean 0.49 mm bias that went into the groom), and carries the true shape in the
  always-on frame 59 (section 6). TAOM has not seen this on a Kit head; a cheap check is to round the exported vertex
  positions to half precision and look.
- **The collar rim must stay still in every morph frame, not only at rest** [yotthani, "gemessen" on packages on 2026-10-08 (`fixkragenmorph`, `Program.cs@6265f4a2:2190-2196`); a comment of 2026-10-02 (`:3261-3267`) says the head baked before Kopf als Morph "war im Spiel nahtfrei" and that the pinned points restore it; no in-game result for `fixkragenmorph` itself is stated; no engine version stated].
  The comment measured that `body_male_a` has no morphs while the head moves its collar points in frames 67 to 71,
  75, 76, 82 to 85, 99 and 100 (vanilla left up to 1.34 mm, right 0.13 mm; its own heads left 2.82 mm, right
  1.06 mm), and that whenever such a frame is active a gap of one pixel opens between head and body. Its
  `fixkragenmorph` command pins the collar points in every frame. In the Kopf-als-Morph layout (section 6) frame 59
  must carry nothing at a point whose base already equals the shape: the half-float rounding remainder, "gemessen am
  Kragenrand 0,49..0,60 mm", alone opened a dashed line. Open rim points below z 1.56 are always pinned (`:3303-3307`, a comment dated 07.10.).
- **A closed mouth on a dense head gets "sewn shut"** [yotthani, in game and measured, `Program.cs@6265f4a2:95-103`
  and `:58-64`, both 2026-10-02; no engine version stated]. The comment says speech and expression are "Morph-Frames
  ab 64" and that each point's deltas are "aus den 6 naechsten Vanilla-Edit-Punkten gemischt". A closed mouth puts both
  lips on the contact line, so both get the same blend: at frame 71 (jaw open) it measured the upper-lip zone at
  -1.9 mm, the lower at -13.7 mm and the contact line of both lips at -6.4 mm. The fix splits the lip loop into an
  upper and a lower chain and lets a lip point inherit only from its own side. The teeth sub-mesh must sit on the
  middle quarter of the lip contact line, not the band centre (1.3 mm off). Only informational for TAOM: its heads come
  from artist FBX files.

## 4. Head material and textures

[yotthani, 2026-08-07 to 2026-08-10]

- **Match vanilla's head material structurally**: shader `8c88213c` with seven texture slots (0, 1, 2, 3, 4, 10, 11)
  and vanilla's coefficients (`head_male_a`: specular 1.22, gloss 0.94). A shader that expects seven inputs and finds
  four unbound samples undefined data (seen as a violet cast). The LOD material `head_male_a.LOD` runs shader
  `61469ddb`, which ignores slot 0 (the face compositor supplies the skin); shader `328d3572` (the working custom
  dwarf head's) draws slot 0.
- **Texture GUIDs are global**: a material may name a texture in another package, as vanilla's do.
- **Replacing a texture's pixels**: keep the item and its GUID (the material points at the GUID, so swapping the file
  strands it); the pixel block is compressed inside the package, so it cannot be byte-patched; the new data must match
  the old length exactly; and **write every mip level** (12 for 2,048 pixels), or the near view shows the new texture
  and the far view the old one, switching as the camera moves.
- **The head material's slots 10 and 11 are the tattoo and scar overlay** [yotthani, Ghidra reading of 56d5c0 on 1.5.3 offsets, `bn faces/FaceLearner/Core/NativeHeadHook.cs@6265f4a2:599-605` and `:609`, a comment dated 2026-10-04]. The comment says the face build lays the tattoo
  material's textures over the face as slots 10 and 11 as soon as the tattoo list is non-empty and the chosen entry
  has a material; vanilla human entry 0 has no name and no material, so nothing shows. "TAOMs Saruman-Rasse hat GENAU
  EINEN Eintrag mit name=""", which got a material and "liegt als schwarze Schicht auf unserem Kopf", adding "(TAOMs eigenes Kopfmaterial zeigt die Slots nicht)": TAOM's own head material does not show the slots. **[Certain]
  for TAOM's side**: the Armory snapshot's `saruman` `man` skin lists exactly one tattoo entry with an empty name
  (`docs/reference/lotrlome-armory-snapshot/skins.xml`, parsed 2026-10-29). The source's workaround is to treat a
  one-entry list as empty in its own copy of the skin record (`:609`, `tattoos == 1 ? 0 : tattoos`); the same author ruled the tattoo list out on the same day: `KopfMessung.cs@6265f4a2:11-13` lists the "Tattoo-Liste" among four guesses, "alle im Spiel widerlegt" (`HeadFit.cs:67-68` says the same), and the measured cause is the head mesh colour (next bullet). So the one-entry list is a refuted guess for Saruman's black head, not its cause. Scars are tattoo entries (the engine tests the name for `scar`, see
  [race-face-and-hand-morphs.md](race-face-and-hand-morphs.md)). Upstream also added Harad war paint as 8 male and 7 female `tattoo_material` entries (the comment calls the male ones clones of `tattoo_male_a_mat` and names no source for the female ones), under `group_id="8" zero_probability="0"`; its XML
  comment says "only B = black, R+B = palette colour, G+B = white" (`bn faces/FaceLearner/ModuleData/skins.xml@6265f4a2:108-110`
  and `:198-199`, dated 2026-10-20; the channel code is the author's probe, not checked against the engine).
- **A foreign race can draw a vanilla-shader head near-black** [yotthani, measured in game on 2026-10-04,
  `bn faces/FaceLearner/Core/KopfMessung.cs@6265f4a2:10-27` and `HeadFit.cs:67-73`; no engine version stated]. The
  measurement: on TAOM's `saruman` race "setzt die Engine die Farbe der Kopf-Meshes auf FF050202 (fast schwarz)", and
  as a human with the same key `FFC8BB93`, with material, shader and textures equal; the comment's reading is that
  TAOM's own materials of that race "werten die Farbe offenbar nicht aus" while the vanilla face shader does.
  **[Certain] for TAOM's side** (Armory snapshot `skins.xml`, parsed 2026-10-29): the `saruman` `man` skin has 26
  skin-colour stops against Native's 24, and pins `eyebump` (key 59) at `key_min="0" key_max="0"` (snapshot line
  188099) where Native and the other skins have 1 and 1. The colour `FF050202` itself is yotthani's measurement. If a
  vanilla-shader head is ever put on that race, expect a dark head.
- **A normal map flipped vertically needs its green channel inverted**, or every bump points down.
- **A hair texture loses thin strands under mipping and bilinear filtering** [yotthani, in game ("Oberkopf aus der
  Distanz kahl"), `Program.cs@6265f4a2:1112-1131` and `tools/groom/haartex_packen.py@6265f4a2:1-9`, 2026-10-06; no
  engine version stated]. The source says vanilla's hair material runs `alpha_test` with threshold 0.761, that thin
  strands no longer reach it once bilinear filtering averages them, and that the crown looked bald from a distance
  although the hair lay 4 mm above the scalp. Its packer writes BC1 with 1-bit alpha (the three-colour mode where
  index 3 is transparent, "fuer ein alpha_test-Material (Haar) reicht das") and adjusts the cut-off per mip level so
  the covered area stays equal; it can also lower `AlphaTest` on a cloned material (`CLONEMAT_ALPHATEST`). TAOM's
  props use the same machinery at 0.314 ([tpac-static-prop-authoring.md](tpac-static-prop-authoring.md) item 8).
  Cloning a vanilla material keeps its shader, flags and coefficients and takes a new GUID and name.
- **EditData UV is stored v-flipped relative to the render stream in vanilla heads** [yotthani, commit `7eb82e9a`
  body: "Vanilla speichert EditData.Uv gespiegelt zum Render-Stream, buildmicahead nicht", tattoos and scars standing
  upside down on its heads "im Spiel per Probe belegt"; the commit title calls the fix (VANUV, `EditData.Uv = (u, 1-v)`)
  a "Test an aragmv", so the fix is untested in that commit; no engine version stated]. This does not contradict
  [tpac-static-prop-authoring.md](tpac-static-prop-authoring.md) item 6, which is about the render stream. Only a
  hand-built head package meets it, and tattoos and scars (the overlay slots 10 and 11) are the visible tell.
- **A non-tiled texture** has `UnknownUlong2` 0 and uses its embedded pixels; a made-up non-zero value is read as a
  Granite tile id, and the failed lookup crashes.

## 5. Eyes on a custom head

[yotthani, 2026-07-30 and 07-31]

- **Find each socket from its open rim loop** (edges with one face), never from a fixed height window: FaceLearner's
  sockets sat 25 mm lower than vanilla's, and the fixed window found the brow instead. Vanilla's `head_male_a` socket
  is at x ±0.0379, y 0.1312, z 1.6825 (radius 7.83 mm), and its eyeball centre sits 3.9 mm inward and 1.9 mm back from
  the socket centre; that relation carries to other heads.
- **Place each eye on its own socket** (real sockets are asymmetric) and **scale an eyeball only uniformly**: scaling
  x about the origin to close the eyes up made every iris elliptical.
- **Eye colour is the mesh colour of the `eye_mat` mesh** [yotthani, measured in game on a vanilla head ("Regler
  durchgeschoben, 60 Zeilen"), `bn faces/FaceLearner/Core/KopfMessung.cs@6265f4a2:69-73`, dated 2026-10-05; no engine
  version stated]. The source says the engine stores the eye colour as the colour of the eye mesh (material `eye_mat`),
  "exakt der Verlaufswert aus skins.xml", and that its own heads' eye meshes got the skin colour instead ("Augenfarben
  sind noch alle gleich"); setting the colour of all `eye_mat` meshes, LODs included, after the build worked (a red
  control on Legolas' eyes showed in game, `:85`). Its interpolation, `t = offset * (count - 1)` over 20 stops, is its
  own code (the 20-stop table at `:62-66`, the `* 19f` at `:81`), not read from the engine. TAOM's reading: this fits the offset being a 0 to 1 position, so an
  appended stop moves every existing position's colour, as [race-face-and-hand-morphs.md](race-face-and-hand-morphs.md)
  "Eye colour" already says.
- TAOM's own gate for an eye that must move with its socket is `tools/check_eye_follow.py`
  ([race-face-and-hand-morphs.md](race-face-and-hand-morphs.md) "The process").

## 6. Groom on a head whose shape is baked in

Vanilla's beards, eyebrows and hair carry no morph channels and follow the face through the engine's head-to-upper-
mesh mapping ([race-face-and-hand-morphs.md](race-face-and-hand-morphs.md) "Hair, beards and eyebrows"). A head that
bakes its shape into the geometry, with its slider channels zeroed, gives that mapping nothing to follow, so the groom
stays at vanilla's neutral place: brows float above the skin and the beard misses the mouth [yotthani].

There are two ways out. The first needs no hook.

**Option A: "Kopf als Morph" (the head's base shape in the geometry, the real shape in the always-on frame 59)**
[yotthani, in game, `bn faces/FaceLearner.HeadExtract/Program.cs@6265f4a2:3250-3258`, a code comment dated 2026-10-02
and marked "Test"; the Ghidra reading is of function 56eba0, which is v1.5.3's numbering]. The comment's reading is
that the engine moves beards, brows and hair "NICHT ueber eigene Morphs", "sondern ueber die Morph-Deltas des Kopfpunkts,
an den jeder Groom-Punkt gebunden ist", with `pos += ((key_max-key_min)*slider+key_min) * Delta[frame(key)][gebundener
Kopfpunkt]`, and that its head "hatte die Form EINGEBACKEN und Frames 0-59 genullt -> fuer die Engine unverformt". Its
fix builds the head in a base shape (its topology laid on vanilla's surface, the comment's "Grundform") and lets the
`eyebump` frame (key 59, `key_min == key_max == 1`, "wirkt IMMER mit Faktor 1") carry base to real shape. The same
author's spec records that this was "Im Spiel bestätigt (02.10., Rassen-Modus)": with it, "sitzt Vanillas Groom in allen
Stufen ohne unseren Laufzeit-Umbau" (`docs/superpowers/specs/2026-10-02-groom-nativ-design.md@6265f4a2:18`). The qualifier
in the code comment: the layout is "nur mit Bindungen gegen DIESES Mesh sinnvoll" (Race mode, or a native load step),
because vanilla's binding indices point at the wrong points of another mesh (`Program.cs:3257-3258`). A measurement from
the engine's own memory on 2026-10-03 says the engine computes exactly `g + T59[binding]`, that the binding is the
nearest base point (97 to 100 percent), and that an offline simulation agrees to "0,00 mm"
(`FaceLearner.HeadExtract/tools/livedump/README.md@6265f4a2:3-5`). Groom authored on the real shape then has to be solved
back to the base, or the delta is applied twice: the author's Legolas hair sat 2.9 mm (forehead), 5.1 mm (temple) and
12.3 mm (ear) too far forward in the 2026-10-11 test (`tools/groom/rueck_basis.py@6265f4a2:3-6`, "gemessen Legolas").

**TAOM's check of the formula on v1.5.6** [Certain, Ghidra decompile of `TaleWorlds.Native.dll` v1.5.6 on 2026-10-29]:
the function that logs the "Mapping data could not be found" line, `FUN_18056f8c0` (RVA 0x56F8C0; 0x56EBA0 on v1.5.3), adds `((key_max - key_min) * slider[i] + key_min) * delta` per deform key, the delta taken at the head point each groom vertex is bound to. The offsets, and a key gate at `+0x74` that is UNVERIFIED, are in [race-face-and-hand-morphs.md](race-face-and-hand-morphs.md) "On v1.5.6 the same function" and "Which deform keys move hair". With `eyebump` at 1 and 1 in Native's `skins.xml`, the factor is 1 at any slider value, and `eyebump` has `deforms_hair="1.0"`, so the route works as yotthani describes whatever that gate turns out to be. Two TAOM facts bound
it. First, the Saruman skin pins `eyebump` to 0 and 0 (the Armory snapshot, section 4), so Option A does nothing on that
race. Second, [upper-mesh-mapping-2026-09-29.md](../investigations/upper-mesh-mapping-2026-09-29.md) line 35 found that
the 1.5.3 twin of this function (0x56EBA0) logged zero calls while the face editor was open and is "probably the baked path
for agents in missions"; whether yotthani's in-game confirmation was made in the editor or in a mission is not stated in
the spec line.

**Option B: move the groom at runtime**, for a head whose shape is baked into the geometry with channel 59 unusable
(Saruman's, for instance) [yotthani; API names Certain where tagged]:

- **The hook**: a postfix on `MBAgentVisuals.AddSkinMeshes(SkinGenerationParams, BodyProperties, bool useGPUMorph,
  bool useFaceCache)` [Certain, `MBAgentVisuals.cs:196`] runs for every skin build (character creation, missions, the
  tableaus that use the agent path). `Skeleton.GetAllMeshes()` [Certain] lists the groom meshes by name. The engine
  rebuilds the groom on every refresh, so anything done to it is done again each build; `Mesh.SetVisibilityMask(0)`
  hides one.
- **Recognise the head by its material**, not its mesh name: in the skeleton the custom head runs under vanilla's
  `head_male_a` name.
- **Edit the mesh the skeleton already holds.** `ManagedMeshEditOperations.Create(mesh)` with
  `GetPositionOfVertex`, `SetPositionOfVertex` and `FinalizeEditing` (names present in `TaleWorlds.Engine.dll` 1.5.3;
  the TAOM decompile cache lacks the type). That mesh is already skinned. A copy added with `AddMultiMesh` is rigid,
  because the bone binding happens during the skin build; to swap a mesh, `MBAgentVisuals.ReplaceMeshWithMesh(MetaMesh
  oldMetaMesh, MetaMesh newMetaMesh, BodyMeshTypes)` [Certain, `MBAgentVisuals.cs:299`] keeps the skinning.
- **Bound the vertex loop yourself.** `GetPositionOfVertex` does not throw past the end of the buffer; it returns
  garbage, and `SetPositionOfVertex` writes out of bounds. The engine exposes no vertex count, but
  `Mesh.GetFaceCornerCount()` [Certain] is an upper bound. In the later `LiveDump.cs@6265f4a2:54-55` yotthani measured
  that it "liefert hier die VERTEXZAHL (beards_c_h: 453 = Vertexe im Paket, nicht 3 x 428 Dreiecke)", so on a beard it
  is the vertex count itself [yotthani, in game, no engine version stated].
- **Match vertices by position, not index.** The runtime buffer is split at UV seams and reordered against the
  package's stream. Use a spatial grid (a quadratic search froze the game on the Naval DLC's `beards_nvl_*`, 12,000 to
  34,000 vertices). The engine moves part of the eyebrow near the eye before the hook runs, so those vertices find no
  match at all.
- **Adding geometry** is harder: `ManagedMeshEditOperations.AddVertex`, `AddFaceCorner` and `AddFace` pass no lock
  handle, and geometry built with them never rendered in three tries. FaceLearner reflects into the internal
  `EngineApplicationInterface.IMesh` calls that take the handle from `Mesh.LockEditDataWrite()` (with
  `HintVerticesDynamic()` and `UnlockEditDataWrite`, all [Certain]); yotthani marks that as an unverified assumption.
- **Measure live, not at rest.** Vanilla's sliders move the brow zone by up to 48 mm, so a groom offset derived from
  the package's rest pose describes a state the game never shows. Measure both heads the same way in game. The edit API cannot do that measuring: yotthani's first LiveDump read the points through
  `ManagedMeshEditOperations.GetPositionOfVertex` and called it "UNGUELTIG: das liefert die gespeicherte Ruhelage des
  Assets (Kopf 0,000 mm neben Vanilla ...) nicht die Lage im Spiel", because the engine "verschiebt die Punkte in einer
  Arbeitskopie und backt sie in den Render-Puffer" (`LiveDump.cs@6265f4a2:17-20`, 2026-10-02, measured in game). It then
  read the engine's binding and morph table from native memory instead. Before diagnosing a beard or brow, compare it with
  a vanilla control: "die weichen Stoppeln ueber dem Kieferband waren ein Vanilla-Merkmal"
  (`tools/livedump/README.md@6265f4a2:23-24`).

### Groom and helmet behaviour: cover_type4, beards, long hair, the morph registry, helmet fit

- **`cover_type4` is the hair or beard variant the engine uses with no helmet and no body armour** [Certain, v1.5.6
  decompile cache, `TaleWorlds.Core.Equipment.cs:83-99`, read 2026-10-29]. `HairCoverType` is the head item's
  `ArmorComponent.HairCoverType`; with no head item and `this[EquipmentIndex.Body].Item == null` it is `Type4`, with
  no head item and a body item it is `None`. The body test reads `.Item`, not the body's cosmetic item. The
  `cover_typeN` to mesh mapping itself is native and was not read. yotthani states the same in a `skins.xml` comment
  (`bn faces/FaceLearner/ModuleData/skins.xml@6265f4a2:8-9`, dated 2026-10-12: "cover_type4 = the variant the engine
  uses with NO helmet and NO body armour (Equipment.HairCoverType)") and builds long hair as two metameshes in one
  package, `name` with a gap for clothing and `name_type4` lying on the bare skin (`Program.cs@6265f4a2:1140-1147`,
  `mergemeta`, 2026-10-12). Matters for any TAOM hair that hangs low on the back and clips into robes.
- **Beard sub-meshes and vertex colour** [yotthani, in game on 2026-10-06 ("neue Karten dunkel und fast durchsichtig";
  "der Spender-Teil des Barts war DUNKELGRAU statt weiss") with `colstat` measurements,
  `Program.cs@6265f4a2:757-763`, `:811-820` and `:834-836`; no engine version stated]. Some beards have two LOD0
  sub-meshes with different materials: part 0 is the "Traeger" (stubble material, blended into the skin) and part
  `.1` the strands (hair material); new cards belong in the strand sub-mesh. `Colors1` drives beard rendering: carriers
  have a constant B (149..255); tuft cards have B 0..255 (median about 140), G always 0 and A mostly 255. For beards `Uv2`
  is constant (0,1), and copying the head convention `Uv2 = Uv1` makes the area map read a different texel per vertex,
  which turned a beard dark grey. A beard that looks dark or nearly transparent has these two likely causes.
- **Long hair needs back-bone weights from collar height down** [yotthani, in game from a user video of 2026-10-13,
  `Program.cs@6265f4a2:838-845`]. The comment says copying neck weights made the hair clip through the collar when the
  head moved, and that the blend into a back-bone profile starts at z 1.63 and is full at z 1.57 (raised that evening
  from 1.62 and 1.55 because the collar "blitzte" on the shoulder at 15 percent head weight).
- **The GPU morph registry renumbers on every new entry, and its arena has no bounds check** [yotthani, Ghidra
  reading on v1.5.3 offsets, `docs/superpowers/specs/2026-10-02-groom-nativ-design.md@6265f4a2:93-110` and
  `NativeHeadHook.cs@6265f4a2:59-70`; the symptom was seen in game on 2026-10-03]. The spec says a new registry entry
  sets a dirty flag, `208ff0` and `209360` then reassign every entry's place in tree order, while meshes "merken sich
  ihre Offsets aber einmalig beim Einrichten der GPU-Morphs", so an entry added after characters exist "verschiebt die
  Mimik-Daten bereits aufgebauter Figuren". The arena is 0x20000 bytes, 0x4000000 (64 MB) "mit nicht-offiziellen
  Modulen" (`1800386b0` compares module names with a list of official ones), and a full arena writes "ungeprüft
  nach Adresse 0". TAOM's reading: race heads registered by the skin loader at load do not hit the late-registration
  case, and TAOM, a module outside the official list, would get the 64 MB arena; neither point was checked on v1.5.6, and nobody has measured how much of the arena TAOM's heads use.
  `Utilities.RegisterMeshForGPUMorph` is `TaleWorlds.Engine`, which the TAOM decompile cache lacks, so the managed
  call was not checked.
- **Helmets are fitted from the face sliders at one managed call site** [yotthani, from a user screenshot of 2026-10-02 (a hood with a cloth before mouth and nose, through which nose, mouth and beard poked, lines 12-13); the mask factor is "1,09 seitlich" (line 36),
  `bn faces/FaceLearner/Core/HelmFaktorPatch.cs@6265f4a2:11-37` and `HeadFit.cs:10-23`, dated 2026-10-02; no engine
  version stated]. The source says the engine fits a rigid helmet with an xyz scale computed from the character's face
  sliders (`helmet_scaling_factor_min/max` on 17 sliders plus `head_scaling`) in `MBAgentVisuals.GetFacegenScalingMatrix()`,
  used for each helmet with a visible head and `ItemObject.UsingFacegenScaling`, and that a head which ignores the
  sliders wears a helmet fitted to a face it does not have (native factor 0.94 to 1.02 in its log). TAOM confirmed the
  two call sites in the v1.5.6 cache ([Certain], `AgentVisuals.cs:665-668` and `MissionScreen.cs:4458-4460`, both gated
  on `UsingFacegenScaling`; the method itself is in `TaleWorlds.Engine` and was not read). The patch applies its extra
  factor only to masks, `BeardCoverType == All` (the same condition as `Equipment.MouthIsHidden`, v1.5.6
  `TaleWorlds.Core.Equipment.cs:119`), because "ueber 200 offene Helme mit Wangenschutz" would stand off the face.
  Useful if a TAOM race's masks (the Nazgul, the Mouth of Sauron) clip.

## 7. Head variants without a new race

FaceLearner's later design [yotthani] keeps the engine race `human` and layers head variants above it, because a new
race drags face generation, the Monster, voices, the skeleton, maturity and culture logic along. The variant lookup
checks a hero's saved variant before any pool, so a hero keeps the same face on every load; normal troops stay
vanilla to avoid per-spawn cost. TAOM's races are real races, so this is context, not a proposal.

## 8. What it means for TAOM

- **Race heads stay on the Kit path** in race-face-and-hand-morphs.md. Hand-built head packages would need the
  `AssetPackages/` delivery question settled first.
- **Directly useful now**: the channel map in section 1 (for `tune_face_slider_reach.py` and any tool that touches
  face channels, keep 51 and 59 and up), and the seam checklist in section 3 for any custom head whose neck shows a line
  (normals, T-junctions, `skinning_precise`, `Uv2`).
- **The groom hook** (Option B) is an alternative to authoring each beard on each head; Option A is the no-hook route if
  a head's shape is ever carried in frame 59. TAOM follows vanilla instead (Mike,
  2026-09-28: follow vanilla, so upper meshes carry no channels and are modelled on the head they serve); the hook
  matters only if a head ever bakes its shape into the geometry.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/provenance-register.md](./provenance-register.md)
- [docs/reference/race-face-and-hand-morphs.md](./race-face-and-hand-morphs.md)
- [docs/reviews/adopt-yotthani-bannerlord-2026-09-30.md](../reviews/adopt-yotthani-bannerlord-2026-09-30.md)

<!-- backlinks-end -->
