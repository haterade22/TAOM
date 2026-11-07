"""Cycles bake adapter for every independent hair-engine pass."""

from pathlib import Path
import sys

import numpy as np

TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from hair_engine.baking import BAKE_PASSES, pass_settings
from hair_engine_adapter import blender_to_mesh


def _hash_color(value):
    state = (int(value) + 1) * 2654435761 & 0xFFFFFFFF
    return np.array([((state >> shift) & 255) / 255.0 for shift in (0, 8, 16)])


def _colors(obj, mesh, pass_name):
    count = len(mesh.vertices)
    alpha = np.ones((count, 1))
    if pass_name == "OPACITY":
        rgb = np.ones((count, 3))
    elif pass_name == "ROOT":
        if "root_t" not in mesh.point_attributes:
            raise ValueError("ROOT baking requires a root_t point attribute")
        rgb = np.repeat(1.0 - mesh.point_attributes["root_t"][:, np.newaxis], 3, axis=1)
    elif pass_name == "ID":
        if "group_id" not in mesh.point_attributes:
            raise ValueError("ID baking requires a group_id point attribute")
        values = np.asarray(mesh.point_attributes["group_id"] % 256) / 255.0
        rgb = np.repeat(values[:, np.newaxis], 3, axis=1)
    elif pass_name in {"RANDOM_COLOR", "CLUMP_COLOR"}:
        key = "group_id" if pass_name == "RANDOM_COLOR" else "profile_id"
        if key not in mesh.point_attributes:
            raise ValueError(f"{pass_name} baking requires a {key} point attribute")
        rgb = np.asarray([_hash_color(value) for value in mesh.point_attributes[key]])
    elif pass_name == "FLOW":
        if "root_t" not in mesh.point_attributes:
            raise ValueError("FLOW baking requires a root_t point attribute")
        rgb = np.column_stack((np.full(count, 0.5), mesh.point_attributes["root_t"], np.zeros(count)))
    elif pass_name == "DIRECTIONAL":
        normals = np.asarray([tuple(vertex.normal) for vertex in obj.data.vertices])
        rgb = normals * 0.5 + 0.5
    elif pass_name == "DEPTH":
        z = mesh.vertices[:, 2]
        span = float(np.ptp(z))
        values = (z - z.min()) / span if span > 1e-12 else np.zeros(count)
        rgb = np.repeat(values[:, np.newaxis], 3, axis=1)
    else:
        raise ValueError(f"{pass_name} does not use an attribute pass")
    return np.concatenate((rgb, alpha), axis=1).astype(np.float32)


def _target_node(material, image):
    material.use_nodes = True
    nodes = material.node_tree.nodes
    node = nodes.get("TAOM_Bake_Target") or nodes.new("ShaderNodeTexImage")
    node.name = "TAOM_Bake_Target"
    node.image = image
    nodes.active = node
    node.select = True


def _emission_material(image):
    import bpy
    material = bpy.data.materials.new("TAOM_Bake_Attribute")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    color = nodes.new("ShaderNodeVertexColor")
    color.layer_name = "TAOM_Bake_Color"
    emission = nodes.new("ShaderNodeEmission")
    output = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(color.outputs["Color"], emission.inputs["Color"])
    material.node_tree.links.new(emission.outputs[0], output.inputs["Surface"])
    _target_node(material, image)
    return material


def _builtin(pass_name):
    if pass_name == "AO":
        return "AO", None
    if pass_name == "NORMAL":
        return "NORMAL", None
    if pass_name in {"DIFFUSE", "COLOR"}:
        return "DIFFUSE", {"COLOR"}
    return None


def bake_passes(obj, out, passes=BAKE_PASSES, resolution=128, padding=0):
    import bpy
    if obj is None or obj.type != "MESH":
        raise ValueError("baking requires a mesh object")
    if not obj.data.uv_layers:
        raise ValueError("baking requires a UV mapped mesh")
    if int(resolution) != resolution or resolution < 1:
        raise ValueError("resolution must be a positive integer")
    out = Path(out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    previous = {
        "engine": scene.render.engine,
        "materials": list(obj.data.materials),
        "material_indices": [polygon.material_index for polygon in obj.data.polygons],
        "margin": scene.render.bake.margin,
    }
    mesh = blender_to_mesh(obj)
    results = {}
    temporary_materials = set()
    temporary_images = set()
    try:
        scene.render.engine = "CYCLES"
        scene.cycles.device = "CPU"
        scene.render.bake.margin = int(padding)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        for name in passes:
            settings = pass_settings(name)
            image = bpy.data.images.new("TAOM_" + name, width=int(resolution), height=int(resolution), alpha=True)
            temporary_images.add(image)
            image.colorspace_settings.name = settings["color_space"]
            builtin = _builtin(name)
            attribute = obj.data.attributes.get("TAOM_Bake_Color")
            if attribute:
                obj.data.attributes.remove(attribute)
            if builtin:
                obj.data.materials.clear()
                materials = []
                for original in previous["materials"]:
                    if original is None:
                        obj.data.materials.append(None)
                        materials.append(None)
                        continue
                    material = original.copy()
                    temporary_materials.add(material)
                    obj.data.materials.append(material)
                    materials.append(material)
                for polygon, material_index in zip(obj.data.polygons, previous["material_indices"]):
                    polygon.material_index = material_index
                if not materials or all(material is None for material in materials):
                    material = bpy.data.materials.new("TAOM_Bake_Base")
                    temporary_materials.add(material)
                    obj.data.materials.append(material)
                    materials = [material]
                for material in materials:
                    if material is not None:
                        _target_node(material, image)
                bake_type, pass_filter = builtin
                kwargs = {"type": bake_type}
                if pass_filter:
                    kwargs["pass_filter"] = pass_filter
                bpy.ops.object.bake(**kwargs)
            else:
                colors = _colors(obj, mesh, name)
                attribute = obj.data.attributes.new("TAOM_Bake_Color", "FLOAT_COLOR", "POINT")
                attribute.data.foreach_set("color", colors.ravel())
                material = _emission_material(image)
                temporary_materials.add(material)
                obj.data.materials.clear()
                obj.data.materials.append(material)
                for polygon in obj.data.polygons:
                    polygon.material_index = 0
                bpy.ops.object.bake(type="EMIT")
            path = out / (name.lower() + ".png")
            image.filepath_raw = str(path)
            image.file_format = "PNG"
            image.save()
            pixels = np.asarray(image.pixels[:], dtype=np.float64)
            results[name] = {"path": str(path), "bytes": path.stat().st_size,
                             "color_space": settings["color_space"],
                             "finite": bool(np.isfinite(pixels).all())}
    finally:
        attribute = obj.data.attributes.get("TAOM_Bake_Color")
        if attribute:
            obj.data.attributes.remove(attribute)
        obj.data.materials.clear()
        for material in previous["materials"]:
            obj.data.materials.append(material)
        for polygon, material_index in zip(obj.data.polygons, previous["material_indices"]):
            polygon.material_index = material_index
        scene.render.engine = previous["engine"]
        scene.render.bake.margin = previous["margin"]
        for material in temporary_materials:
            if material.users == 0:
                bpy.data.materials.remove(material)
        for image in temporary_images:
            if image.users == 0:
                bpy.data.images.remove(image)
    return results
