# Navmesh prefab sources

`taom_mumakil_platform_navmesh.kit-export.bin` is Mike's Modding Kit export of 2026-09-22, kept
because it is the only valid `NMG9` structure in the project that we did not author ourselves.

The shipped asset at `Main/_Module/NavMeshPrefabs/taom_mumakil_platform_navmesh.bin` was made from
this file by rewriting **vertex positions only**: eight 1.6 m squares centred on the crew frames. The
file is a 12 byte header (length, `NMG9`, vertex count), then that many vertex triples, then face
records carrying vertex indices and neighbour links. Vertex positions can be edited in place without
disturbing topology, which is how three separate corrections were applied and confirmed in game. The
face section's encoding has NOT been worked out, so the face count and the shape's topology cannot
be changed without a fresh Kit export.

Why the corrections were needed at all: four exports came back in four different coordinate frames
(platform-local; world, 8.5 km out; world with correct heights; and 8.80 m low with a lateral shift).
The Kit writes the faces in whatever frame the prefab happened to be sitting in, and the editor's
position field does not always agree with it. **Verify any new export before testing it in game**:
check that the vertices are in platform-local coordinates, that the heights match the deck surfaces,
and that every crew frame has a body radius of navmesh around it.
