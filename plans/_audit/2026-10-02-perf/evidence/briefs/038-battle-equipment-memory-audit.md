Plan 038: an offline audit that says which equipment assets dominate a battle's memory: for every troop
(and for a chosen pair of armies) it resolves the troop's possible equipment to meshes, materials and
textures in the release packs and sums the bytes the engine would hold, so the memory levers in TAOM's
own content (the Armory's armour, weapons, hair and beards) are ranked by measured size, not by guess.

WHY. Each battle takes about 2 to 3 GB of native memory and returns it (docs/features/battle-load-diagnostics.md:420-427,
[MemStation] sessions); the menu floor attributes about 970 MB to LOTRLOME_Armory alone
(docs/investigations/native-commit-audit-2026-08.md:685-707). No tool says which items or assets
carry that weight. The campaign map has such a tool, tools/audit_map_scene_memory.py, which indexes the
packs of a game or release folder (iter_tpac_items, :121-126, :227-269), decodes texture formats, sizes
and mips (:33-46, :350-374), metamesh records and their two mesh segments (vertex stream 97f81dbb and
edit data 5f98413d, :67-84, :129-131) and resolves mesh to material to textures for the map scene. A
2026-10-02 run on the release packs took about a minute per channel.
WHAT (a new tool, or a new mode of the existing one; the writer decides by which keeps both readable
and tested, and must not change the existing tool's outputs):
1. Inputs: a game or release root (default the testing release channel; the live Armory has no packs,
   per its loose-tree layout); the modules in resolution order (LOTRLOME_Armory, TAOM, Native,
   SandBoxCore); the troop source (NPCCharacter XML across the three modules, with their equipment
   rosters and EquipmentSet references; tools/validate_all_troop_refs.py and the taom-moduledata MCP
   resolve troops and items; read how they do it and reuse their parsing).
2. Per item: its mesh name(s) (item XML mesh, plus multi-mesh items, plus body meshes: a culture's
   hair, beard and head meshes when the troop's race uses Armory race assets), resolved to metameshes
   in the packs, their LOD0 vertex-stream bytes, their materials and every texture's resident bytes (the
   map tool's formula).
3. Per troop: the union over its equipment sets. Per army or battle (--troops a.txt b.txt, or
   --culture gondor --culture mordor): the union over troops, with shared assets counted once.
4. Output: a Markdown report and TSVs like the map tool's: top assets by bytes with the items and troops
   that pull them in, per-culture totals, flags for uncompressed or mip-less textures, 4096-square
   textures on small items, and meshes whose LOD0 exceeds a face budget.
TESTS: tools/tests/ with fixture packs or the existing map tool's fixtures; the union logic (shared
assets once), the resolution misses reported (never silently dropped), the formula, the CLI. Record the
Python suite's failure set before the first edit.
OUT OF SCOPE: changing any asset, any live module, the map tool's outputs.
STOP conditions to include: the item-to-mesh resolution cannot be made to match the engine's own rules
for a sampled set of items (verify against the decompiled ItemObject / MetaMesh loading, and say which
cases remain approximate).
EDIT DATA (orchestrator, 2026-10-02, after the brief): the v1.5.3 shipping client reads a mesh's edit
data (segment 5f98413d) only on demand, through "ensure edit data" (0x68D30) and an async request
manager (0x181CD0); static scenery never triggers it (engine reference page section 7,
evidence/native/native-mesh-editdata.txt). But some callers are character paths: face and body
generation (0x56F360 produce_vertex_map_from_mesh, 0x574C30 deform_keys and face_base_mesh) and
0x583350 ("Scale %f %f %f, deform %s"), which may build deformed copies of meshes for a body shape. So
for equipment on characters, report both segments per item (render buffers as the floor, plus edit data
as a flagged upper bound), and state that whether worn armour loads its edit data is UNVERIFIED unless
the writer settles it from 0x583350's callers.
LOOSE INSTALLS (orchestrator, 2026-10-02): the live TAOM_Map is now the Kit's loose layout (1,934 tpacs under
Assets, render buffers compiled into an 18 GB RuntimeDataCache, no AssetPackages), and tools/audit_map_scene_memory.py
indexes only AssetPackages and EmAssetPackages (PACK_TREES, :145). A scratch copy that adds "Assets" read the live
textures correctly (bytes from each header). If the new audit reuses the map tool's index, give both an option to read
loose Assets trees, and say that loose mesh tpacs carry only edit data (segment 5f98413d), so mesh render-buffer bytes
of a loose install must come from the release packs or be marked unknown.
