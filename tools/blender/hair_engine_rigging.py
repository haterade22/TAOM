"""Blender armature and skinning adapter for independent hair rigs."""

from pathlib import Path
import sys

TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from hair_engine import rigging


def rig_to_blender(hair, name="TAOM_Hair_Rig", bones_per_strand=4, collection=None):
    import bpy
    data = bpy.data.armatures.new(name)
    rig = bpy.data.objects.new(name, data)
    (collection or bpy.context.scene.collection).objects.link(rig)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    try:
        for strand_index, strand in enumerate(hair.strands):
            previous = None
            for bone in rigging.ribbon_bones(strand, bones_per_strand):
                name = f"hair_{strand_index:03d}_{bone['name']}"
                edit_bone = data.edit_bones.new(name)
                edit_bone.head = bone["head"]
                edit_bone.tail = bone["tail"]
                if previous:
                    edit_bone.parent = data.edit_bones[previous]
                    edit_bone.use_connect = True
                previous = name
    finally:
        bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def skin_mesh_to_rig(mesh_obj, rig, hair, bones_per_strand=4):
    import bpy
    expected_vertices = sum(len(strand.positions) for strand in hair.strands)
    if expected_vertices == 0 or len(mesh_obj.data.vertices) % expected_vertices:
        raise ValueError("mesh topology does not match the supplied hair strands")
    ring_size = len(mesh_obj.data.vertices) // expected_vertices
    vertex_index = 0
    for strand_index, strand in enumerate(hair.strands):
        weights = rigging.ribbon_weights(strand, bones_per_strand)
        groups = {}
        for point_weights in weights:
            for bone_name, weight in point_weights.items():
                name = f"hair_{strand_index:03d}_{bone_name}"
                group = groups.get(name)
                if group is None:
                    group = mesh_obj.vertex_groups.get(name) or mesh_obj.vertex_groups.new(name=name)
                    groups[name] = group
                for _ in range(ring_size):
                    group.add([vertex_index], weight, "REPLACE")
                    vertex_index += 1
    modifier = mesh_obj.modifiers.get("TAOM Hair Armature") or mesh_obj.modifiers.new(
        "TAOM Hair Armature", "ARMATURE")
    modifier.object = rig
    world = mesh_obj.matrix_world.copy()
    mesh_obj.parent = rig
    mesh_obj.matrix_world = world
    return modifier
