"""
skeleton_spec.py - declarative creature-skeleton spec + bpy scaffolder for the TAOM pipeline.

A "skeleton spec" is a JSON-able dict:
  {"name": "<skeleton>",
   "bones": [{"name", "parent", "head": [x,y,z], "tail": [x,y,z], "roll"}, ...]}

This is the foundation for building CUSTOM creature skeletons (chariot, ram, goat, troll, ...) from
data instead of by hand: author/extract a spec -> build the armature -> auto-weight a mesh -> animate.

Runs INSIDE Blender (needs bpy). Load via the Blender MCP / console:
  exec(open(r'C:\\Users\\mikew\\source\\repos\\TAOM\\tools\\blender\\skeleton_spec.py').read(), globals())
  spec = extract_spec("sp_skeleton")                  # armature -> dict
  import json; json.dump(spec, open(path, "w"))       # persist as a reusable spec
  obj  = build_from_spec(spec, name="ram_rebuild")    # dict -> armature object
  print(roundtrip_check("sp_skeleton"))               # self-validation

VALIDATED iter 8: round-tripping sp_skeleton (extract -> build -> diff) reproduced all 59 bones
with identical names + parent hierarchy, zero mismatches, session left clean.
NON-DESTRUCTIVE: extract_spec only toggles the source's edit mode to read roll; build_from_spec
creates a NEW armature; nothing saves the .blend.
"""
import bpy


def extract_spec(armature_name):
    """Read an armature into a JSON-able skeleton spec (edit-bone head/tail/roll/parent).
    Read-only on the source: toggles it into EDIT mode to read `roll`, then restores OBJECT mode."""
    arm = bpy.data.objects.get(armature_name)
    if arm is None or arm.type != 'ARMATURE':
        raise ValueError("No armature object %r" % armature_name)
    prev_active = bpy.context.view_layer.objects.active
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    bones = []
    for eb in arm.data.edit_bones:
        bones.append({
            "name": eb.name,
            "parent": (eb.parent.name if eb.parent else None),
            "head": [round(v, 5) for v in eb.head],
            "tail": [round(v, 5) for v in eb.tail],
            "roll": round(eb.roll, 5),
        })
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.objects.active = prev_active
    return {"name": arm.data.name, "bones": bones}


def build_from_spec(spec, name=None):
    """Create a NEW armature object from a skeleton spec. Bones may be listed in any order
    (parents are linked in a second pass). Returns the new armature object. Does NOT save the .blend."""
    nm = name or spec.get("name", "creature_skeleton")
    data = bpy.data.armatures.new(nm)
    obj = bpy.data.objects.new(nm, data)
    bpy.context.collection.objects.link(obj)
    prev_active = bpy.context.view_layer.objects.active
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    ebs = data.edit_bones
    made = {}
    for b in spec["bones"]:
        eb = ebs.new(b["name"])
        eb.head = b["head"]
        eb.tail = b["tail"]
        eb.roll = b.get("roll", 0.0)
        made[b["name"]] = eb
    for b in spec["bones"]:
        p = b.get("parent")
        if p and p in made:
            made[b["name"]].parent = made[p]
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.objects.active = prev_active
    return obj


def auto_weight(armature_obj, mesh_obj):
    """First-pass automatic skinning: parent `mesh_obj` to `armature_obj` with automatic weights
    (`bpy.ops.object.parent_set(type='ARMATURE_AUTO')`). A STARTING POINT only -- production-quality
    weight painting is manual. NOTE: thin wrapper over a standard op; not yet round-trip-verified here."""
    for o in list(bpy.context.selected_objects):
        o.select_set(False)
    mesh_obj.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    return True


def roundtrip_check(armature_name):
    """Self-validation: extract -> build -> diff names+parents -> clean up the rebuild.
    Returns {"bones","names_match","parent_mismatches"}."""
    spec = extract_spec(armature_name)
    obj = build_from_spec(spec, name=armature_name + "_RTCHECK")
    src = {b["name"]: b["parent"] for b in spec["bones"]}
    rt = {bn.name: (bn.parent.name if bn.parent else None) for bn in obj.data.bones}
    res = {"bones": len(src), "names_match": set(src) == set(rt),
           "parent_mismatches": [n for n in src if src[n] != rt.get(n)]}
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.armatures.remove(data)
    return res
