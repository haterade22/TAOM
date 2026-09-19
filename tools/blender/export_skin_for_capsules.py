"""Blender headless, read-only on the FBX: export a creature's skin for tools/skeleton_hit_capsules.py fit.

    blender -b -P tools/blender/export_skin_for_capsules.py -- <out.json> <creature.fbx>

Writes, in Blender world space (the fit finds the axis map to engine space itself, from the bone positions):
  bones:  {bone name: [rest head, rest tail]} of the first armature
  meshes: [{name, verts: [[x, y, z, nx, ny, nz, [[vertex group, weight], ...]], ...], total_verts}]
Every mesh object is exported with its vertex-group weights of 0.02 and up, so one export serves any choice of
--mesh later (the elephant FBX holds the bare body plus eleven armour pieces). Nothing is saved back.
Imported with automatic_bone_orientation off, so bone heads are the file's own positions.
"""
import json
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
out, path = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
if not arms:
    raise RuntimeError("no armature in %s" % path)
arm = arms[0]
bones = {b.name: [list(arm.matrix_world @ b.head_local), list(arm.matrix_world @ b.tail_local)] for b in arm.data.bones}
meshes = []
for o in bpy.data.objects:
    if o.type != 'MESH':
        continue
    groups = {g.index: g.name for g in o.vertex_groups}
    mw = o.matrix_world
    nm = mw.to_3x3().inverted().transposed()
    verts = []
    for v in o.data.vertices:
        w = [[groups[g.group], round(g.weight, 4)] for g in v.groups if g.weight >= 0.02 and g.group in groups]
        if not w:
            continue
        p = mw @ v.co
        n = (nm @ v.normal).normalized()
        verts.append([round(p.x, 5), round(p.y, 5), round(p.z, 5), round(n.x, 4), round(n.y, 4), round(n.z, 4), w])
    meshes.append({'name': o.name, 'verts': verts, 'total_verts': len(o.data.vertices)})
with open(out, 'w') as fh:
    json.dump({'fbx': path, 'armature': arm.name, 'bones': bones, 'meshes': meshes}, fh)
print("exported %d meshes, %d bones -> %s" % (len(meshes), len(bones), out))
