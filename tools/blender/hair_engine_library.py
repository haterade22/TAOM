"""Blender library append, bind, snap, and thumbnail services."""

from pathlib import Path


def append_preset(filepath, name, collection=None):
    import bpy
    filepath = str(Path(filepath).resolve())
    with bpy.data.libraries.load(filepath, link=False) as (data_from, data_to):
        if name not in data_from.objects:
            raise ValueError(f"preset object {name!r} is not in {filepath}")
        data_to.objects = [name]
    obj = data_to.objects[0]
    if obj is None:
        raise ValueError(f"could not append {name!r}")
    base = name
    suffix = 0
    while obj.name in bpy.data.objects and bpy.data.objects[obj.name] != obj:
        suffix += 1
        obj.name = f"{base}.{suffix:03d}"
    (collection or bpy.context.scene.collection).objects.link(obj)
    return obj


def bind_preset(objects, root):
    if root is None:
        raise ValueError("a bind root is required")
    for obj in objects:
        world = obj.matrix_world.copy()
        obj.parent = root
        obj.matrix_world = world
    return root


def snap_preset(objects, target):
    if target is None or target.type != "MESH":
        raise ValueError("snap target must be a mesh")
    for obj in objects:
        if obj.type != "MESH":
            continue
        modifier = obj.modifiers.get("TAOM Hair Snap") or obj.modifiers.new("TAOM Hair Snap", "SHRINKWRAP")
        modifier.target = target
        modifier.wrap_method = "NEAREST_SURFACEPOINT"
    return objects


def render_thumbnail(obj, filepath, size=256):
    import bpy
    from mathutils import Vector
    scene = bpy.context.scene
    previous = {
        "engine": scene.render.engine,
        "camera": scene.camera,
        "resolution": (scene.render.resolution_x, scene.render.resolution_y),
        "filepath": scene.render.filepath,
        "hidden": {item: item.hide_render for item in scene.objects},
    }
    camera_data = bpy.data.cameras.new("TAOM_Preset_Thumbnail")
    camera = bpy.data.objects.new("TAOM_Preset_Thumbnail", camera_data)
    scene.collection.objects.link(camera)
    try:
        for item in scene.objects:
            item.hide_render = item != obj
        points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        low = Vector(tuple(min(point[index] for point in points) for index in range(3)))
        high = Vector(tuple(max(point[index] for point in points) for index in range(3)))
        center = (low + high) / 2
        scale = max((high - low).length, 0.1)
        camera.location = center + Vector((1.0, 1.0, 0.6)).normalized() * scale * 2.0
        camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera_data.type = "ORTHO"
        camera_data.ortho_scale = scale * 1.2
        scene.camera = camera
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.render.resolution_x = int(size)
        scene.render.resolution_y = int(size)
        scene.render.resolution_percentage = 100
        scene.render.filepath = str(Path(filepath).resolve())
        bpy.ops.render.render(write_still=True)
        return scene.render.filepath
    finally:
        scene.render.engine = previous["engine"]
        scene.camera = previous["camera"]
        scene.render.resolution_x, scene.render.resolution_y = previous["resolution"]
        scene.render.filepath = previous["filepath"]
        for item, hidden in previous["hidden"].items():
            item.hide_render = hidden
        bpy.data.objects.remove(camera, do_unlink=True)
        if camera_data.users == 0:
            bpy.data.cameras.remove(camera_data)
