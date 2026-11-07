"""Scale a skinned, animated rig in place by one factor about the world origin, the ground (#1081, the dire wolf).

The Kit read the dire wolf's FBX files with the armature object at the pack's scale 0.01, so a resize keeps that
scale and the object's rotation, and changes the data under them: the bones' rest positions, the meshes' vertices,
the location keys and the armature object's location. Scaling the object instead would change the one transform the
Kit is known to read correctly, and Blender's apply-scale moves the rest bones but leaves the location keys at the old
size.

The pivot is the world origin because the pack's armature object stands at pelvis height: a scale about the object's
own origin kept the pelvis at 0.65 m and pushed the paws 0.45 m into the ground (the first mesh run, 2026-11-06).

The rig can also move toward its tail (tail_offset). A mount's rider sits where its usage's rider clip puts it in model
space, not on rider_sit_bone, so the dire wolf (monster_usage warg) moves until its saddle is under the warg's seat
(#1081, the engine read of 2026-11-06 in docs/features/dire-wolf.md "Seat"). The move must be one constant vector in
model space. A mesh file has a still armature object, so scale_rig's offset moves the rest bones and the meshes. An
animation file turns its armature object (the wolf's turn clips, and the first frame of two other files is yawed 60
degrees), and a move inside armature space would turn with it, so move_object moves the object and its location keys
instead (the scale_dire_wolf_anims.py gate, 2026-11-06).

bpy is imported lazily, so is_length_channel, tail_offset, seat_point and move_object are unit-tested without Blender
(tools/tests/test_rig_scale.py).
"""
import re

_POSE_LOCATION = re.compile(r'^pose\.bones\["[^"]+"\]\.location$')


def is_length_channel(data_path):
    """True for the F-curves that hold a translation: a pose bone's location (bone-local, in armature units) and the
    object's location (root motion, in world units). Both scale with the rig."""
    return data_path == "location" or bool(_POSE_LOCATION.match(data_path))


def tail_offset(pelvis, head, distance):
    """The world move of distance toward the tail: along the floor, from the head toward the pelvis. The height stays,
    so the paws stay on the ground."""
    dx, dy = pelvis[0] - head[0], pelvis[1] - head[1]
    length = (dx * dx + dy * dy) ** 0.5
    if length < 1e-6:
        raise ValueError("the spine is vertical: no tail direction")
    return (dx / length * distance, dy / length * distance, 0.0)


SEAT_MIDLINE = 0.12  # metres either side of the spine's plane
SEAT_BIN = 0.05  # metres along the spine


def seat_point(points, tail_y):
    """(y, z) of a saddle's seat: the low rear end of its top surface, where the rider's pelvis rests.

    points are world (x, y, z) vertices; tail_y is +1.0 or -1.0, the sign of y toward the tail. The midline's top
    surface is binned along y. The seat surface runs from the pommel, the highest bin, back toward the tail while it
    keeps falling; the last bin's top vertex is the seat. Its own y is returned, not the bin's centre, so a move under
    one bin still shows. Parts of the mesh that stand apart (straps, bags) are not reached."""
    tops = {}
    for x, y, z in points:
        if abs(x) >= SEAT_MIDLINE:
            continue
        k = int((y * tail_y) // SEAT_BIN)
        if k not in tops or z > tops[k][0]:
            tops[k] = (z, y)
    if not tops:
        raise ValueError("no saddle vertex lies on the midline")
    k = max(tops, key=lambda b: tops[b][0])
    while k + 1 in tops and tops[k + 1][0] <= tops[k][0]:
        k += 1
    return (tops[k][1], tops[k][0])


def fcurves(action):
    """Every F-curve of an action: Blender 5 keeps them in layers, strips and channel bags."""
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    return [fc for layer in action.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves]


def move_object(obj, offset, actions=()):
    """Move an object and every key of its location F-curves by the world vector offset. Returns the keys moved."""
    for i in range(3):
        obj.location[i] += offset[i]
    keys = 0
    for action in actions:
        for fc in fcurves(action):
            if fc.data_path != "location":
                continue
            d = offset[fc.array_index]
            for kp in fc.keyframe_points:
                kp.co[1] += d
                kp.handle_left[1] += d
                kp.handle_right[1] += d
                keys += 1
            fc.update()
    return keys


def scale_rig(armature, factor, meshes=(), actions=(), offset=(0.0, 0.0, 0.0)):
    """Scale the rest pose, the meshes and the location keys of the given actions by factor about the world origin,
    then move the rest pose and the meshes by the world vector offset. Returns the number of keys scaled."""
    from mathutils import Matrix, Vector

    import bpy

    move = Matrix.Translation(Vector(offset))
    scale = move @ Matrix.Scale(factor, 4)
    mesh_world = [(mesh, mesh.matrix_world.copy()) for mesh in meshes]
    armature.location = armature.location * factor
    # The bones are in armature space: the object's rotation and its 0.01 scale turn the world move into that space.
    local_move = armature.matrix_world.to_3x3().inverted() @ Vector(offset)
    armature.data.transform(Matrix.Translation(local_move) @ Matrix.Scale(factor, 4))
    # transform() moves each bone's armature-space matrix, but the pose reads the parent-relative head and length,
    # which keep the old size until the bones are rebuilt from edit bones (the first gate run, 2026-11-06).
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    for mesh, old_world in mesh_world:
        # A mesh parented to the armature moved with its location: map each vertex from its old world place to factor
        # times that place, seen from the mesh's new frame.
        mesh.data.transform(mesh.matrix_world.inverted() @ scale @ old_world)
        mesh.data.update()
    keys = 0
    for action in actions:
        for fc in fcurves(action):
            if not is_length_channel(fc.data_path):
                continue
            for kp in fc.keyframe_points:
                kp.co.y *= factor
                kp.handle_left.y *= factor
                kp.handle_right.y *= factor
                keys += 1
            fc.update()
    return keys
