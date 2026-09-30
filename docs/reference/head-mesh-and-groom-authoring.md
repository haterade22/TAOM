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

**Why TAOM cares.** TAOM builds race heads in the Modding Kit, and
[race-face-and-hand-morphs.md](race-face-and-hand-morphs.md) owns that process. FaceLearner built heads with no Kit
at all: it cloned vanilla's `head_male_a`, reshaped it, and wrote the package with a patched TpacTool. To make that
work it had to learn what a head package must hold, how the face channels divide up, why a head's neck shows a seam,
what the head material needs, and how beards and eyebrows follow a face whose shape is baked in. Those answers apply
to any custom head, Kit-built or not.

## 1. The 101 face channels and the deform keys

- **The keys** [Certain, Native `skins.xml` on 1.5.3]: every human skin has **63** `deform_key`s whose
  `key_time_point` values are 1 to 63, one channel each. 1 to 58 are sliders (some skins pin `old_face`, 57, at
  0 to 0). 59 is `eyebump`, pinned at 1 to 1; 60 `weight`, 61 `build`, 62 `height` and 63 `age` carry no range and
  the engine drives them. Ten or eleven keys per skin are **inverted** (`key_min` above `key_max`: `eyebrow_depth`,
  `eye_position`, `nose_angle`, `kid_face` and others), so normalise a slider as `(w - key_min) / (key_max - key_min)`,
  which a plain min and max would mirror. No key names a channel above 63. FaceLearner's notes count 64 keys, from an
  older build; its list of engine keys also omits 60, `weight`.
- **Channels 64 to 100 are facial animation** [yotthani]: blinking, speech and expression. Zeroing them froze the
  eyelids while the eyeballs kept moving (they are bones, not morphs). A tool that zeroes a head's slider channels
  must keep 59 and up. This answers part of the open question in race-face-and-hand-morphs.md about how the keys
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
  of the diffuse texture can see.

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
- **A normal map flipped vertically needs its green channel inverted**, or every bump points down.
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
- TAOM's own gate for an eye that must move with its socket is `tools/check_eye_follow.py`
  ([race-face-and-hand-morphs.md](race-face-and-hand-morphs.md) "The process").

## 6. Groom on a head whose shape is baked in

Vanilla's beards, eyebrows and hair carry no morph channels and follow the face through the engine's head-to-upper-
mesh mapping ([race-face-and-hand-morphs.md](race-face-and-hand-morphs.md) "Hair, beards and eyebrows"). A head that
bakes its shape into the geometry, with its slider channels zeroed, gives that mapping nothing to follow, so the groom
stays at vanilla's neutral place: brows float above the skin and the beard misses the mouth [yotthani].

FaceLearner's answer is to move the groom at runtime [yotthani; API names Certain where tagged]:

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
  `Mesh.GetFaceCornerCount()` [Certain] is an upper bound.
- **Match vertices by position, not index.** The runtime buffer is split at UV seams and reordered against the
  package's stream. Use a spatial grid (a quadratic search froze the game on the Naval DLC's `beards_nvl_*`, 12,000 to
  34,000 vertices). The engine moves part of the eyebrow near the eye before the hook runs, so those vertices find no
  match at all.
- **Adding geometry** is harder: `ManagedMeshEditOperations.AddVertex`, `AddFaceCorner` and `AddFace` pass no lock
  handle, and geometry built with them never rendered in three tries. FaceLearner reflects into the internal
  `EngineApplicationInterface.IMesh` calls that take the handle from `Mesh.LockEditDataWrite()` (with
  `HintVerticesDynamic()` and `UnlockEditDataWrite`, all [Certain]); yotthani marks that as an unverified assumption.
- **Measure live, not at rest.** Vanilla's sliders move the brow zone by up to 48 mm, so a groom offset derived from
  the package's rest pose describes a state the game never shows. Measure both heads the same way in game.

## 7. Head variants without a new race

FaceLearner's later design [yotthani] keeps the engine race `human` and layers head variants above it, because a new
race drags face generation, the Monster, voices, the skeleton, maturity and culture logic along. The variant lookup
checks a hero's saved variant before any pool, so a hero keeps the same face on every load; normal troops stay
vanilla to avoid per-spawn cost. TAOM's races are real races, so this is context, not a proposal.

## 8. What it means for TAOM

- **Race heads stay on the Kit path** in race-face-and-hand-morphs.md. Hand-built head packages would need the
  `AssetPackages/` delivery question settled first.
- **Directly useful now**: the channel map in section 1 (for `tune_face_slider_reach.py` and any tool that touches
  face channels, keep 59 and up), and the seam checklist in section 3 for any custom head whose neck shows a line
  (normals, T-junctions, `skinning_precise`, `Uv2`).
- **The groom hook** is an alternative to authoring each beard on each head. TAOM follows vanilla instead (Mike,
  2026-09-28: follow vanilla, so upper meshes carry no channels and are modelled on the head they serve); the hook
  matters only if a head ever bakes its shape into the geometry.
