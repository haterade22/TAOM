"""Blender Curves and mesh adapters for the independent TAOM hair engine."""

from contextlib import contextmanager
from pathlib import Path
import sys

import numpy as np

TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from hair_engine.mesh import MeshData
from hair_engine.model import Hair, Strand

_CURVE_METADATA = {"Profile_ID", "UV_Box_ID", "curve_group_id_ht"}
_POINT_DEFAULTS = {"position", "radius", "tilt"}


def _collection(collection):
    import bpy
    return collection or bpy.context.scene.collection


def _attribute(data, name, value_type, domain):
    attribute = data.attributes.get(name)
    return attribute or data.attributes.new(name, value_type, domain)


def _values(attribute, dtype=np.float64):
    values = np.empty(len(attribute.data), dtype=dtype)
    attribute.data.foreach_get("value", values)
    return values


def hair_to_blender(hair, name, collection=None):
    import bpy
    data = bpy.data.hair_curves.new(name + "_Curves")
    obj = bpy.data.objects.new(name, data)
    _collection(collection).objects.link(obj)
    data.add_curves([len(strand.positions) for strand in hair.strands])
    radius = _attribute(data, "radius", "FLOAT", "POINT")
    tilt = _attribute(data, "tilt", "FLOAT", "POINT")
    profile = _attribute(data, "Profile_ID", "INT", "CURVE")
    region = _attribute(data, "UV_Box_ID", "INT", "CURVE")
    group = _attribute(data, "curve_group_id_ht", "INT", "CURVE")
    for curve_index, (curve, strand) in enumerate(zip(data.curves, hair.strands)):
        for point, position, width, angle in zip(curve.points, strand.positions, strand.radius, strand.tilt):
            point.position = position
            radius.data[point.index].value = float(width)
            tilt.data[point.index].value = float(angle)
        profile.data[curve_index].value = strand.profile_id
        region.data[curve_index].value = strand.uv_region
        group.data[curve_index].value = strand.group_id
        for tag in strand.tags:
            _attribute(data, "HT_Tag_" + tag, "BOOLEAN", "CURVE").data[curve_index].value = True
        for name, values in strand.point_attributes.items():
            values = np.asarray(values)
            if values.ndim != 1 or not np.issubdtype(values.dtype, np.number):
                raise ValueError(f"Blender point attribute {name!r} must be a numeric 1D array")
            value_type = "INT" if np.issubdtype(values.dtype, np.integer) else "FLOAT"
            attribute = _attribute(data, name, value_type, "POINT")
            for point, value in zip(curve.points, values):
                attribute.data[point.index].value = value
    return obj


def blender_to_hair(obj):
    if obj is None or obj.type != "CURVES":
        raise ValueError("expected a Blender Hair Curves object")
    data = obj.data
    radius = _values(data.attributes["radius"]) if data.attributes.get("radius") else None
    tilt = _values(data.attributes["tilt"]) if data.attributes.get("tilt") else None
    profile = _values(data.attributes["Profile_ID"], np.int32) if data.attributes.get("Profile_ID") else None
    region = _values(data.attributes["UV_Box_ID"], np.int32) if data.attributes.get("UV_Box_ID") else None
    group = _values(data.attributes["curve_group_id_ht"], np.int32) if data.attributes.get("curve_group_id_ht") else None
    strands = []
    for curve_index, curve in enumerate(data.curves):
        indices = [point.index for point in curve.points]
        positions = np.asarray([tuple(point.position) for point in curve.points], dtype=np.float64)
        tags = frozenset(attribute.name[7:] for attribute in data.attributes
                         if attribute.name.startswith("HT_Tag_") and attribute.data[curve_index].value)
        attributes = {}
        for attribute in data.attributes:
            if attribute.name in _CURVE_METADATA or attribute.name in _POINT_DEFAULTS or attribute.name.startswith("HT_Tag_"):
                continue
            if attribute.domain != "POINT" or attribute.data_type not in {"FLOAT", "INT"}:
                continue
            attributes[attribute.name] = _values(attribute)[indices]
        strands.append(Strand(
            positions,
            radius[indices] if radius is not None else None,
            tilt[indices] if tilt is not None else None,
            int(profile[curve_index]) if profile is not None else 0,
            int(region[curve_index]) if region is not None else -1,
            int(group[curve_index]) if group is not None else 0,
            tags,
            attributes,
        ))
    return Hair(strands, dict(obj.items()))


def mesh_to_blender(mesh, name, collection=None, materials=None):
    import bpy
    object_name = name
    data = bpy.data.meshes.new(name + "_Mesh")
    data.from_pydata(mesh.vertices.tolist(), mesh.edges, mesh.faces)
    data.update()
    for material_name in mesh.materials:
        material = materials.get(material_name) if materials else bpy.data.materials.get(material_name)
        if material is None:
            material = bpy.data.materials.new(material_name)
        data.materials.append(material)
    data.polygons.foreach_set("material_index", mesh.material_indices)
    for name, values in mesh.uv_layers.items():
        layer = data.uv_layers.new(name=name)
        layer.data.foreach_set("uv", values.astype(np.float32).ravel())
    for name, values in mesh.point_attributes.items():
        values = np.asarray(values)
        if values.ndim != 1 or not np.issubdtype(values.dtype, np.number):
            continue
        value_type = "INT" if np.issubdtype(values.dtype, np.integer) else "FLOAT"
        _attribute(data, name, value_type, "POINT").data.foreach_set("value", values)
    obj = bpy.data.objects.new(object_name, data)
    _collection(collection).objects.link(obj)
    return obj


def blender_to_mesh(obj):
    if obj is None or obj.type != "MESH":
        raise ValueError("expected a Blender mesh object")
    data = obj.data
    vertices = np.asarray([tuple(vertex.co) for vertex in data.vertices], dtype=np.float64)
    faces = [tuple(polygon.vertices) for polygon in data.polygons]
    uv_layers = {}
    for layer in data.uv_layers:
        values = np.empty(len(layer.data) * 2, dtype=np.float64)
        layer.data.foreach_get("uv", values)
        uv_layers[layer.name] = values.reshape(-1, 2)
    material_indices = np.empty(len(data.polygons), dtype=np.int32)
    data.polygons.foreach_get("material_index", material_indices)
    attributes = {}
    for attribute in data.attributes:
        if attribute.domain == "POINT" and attribute.data_type in {"FLOAT", "INT"}:
            attributes[attribute.name] = _values(attribute)
    return MeshData(vertices, faces, uv_layers,
                    [slot.material.name if slot.material else "" for slot in obj.material_slots] or ["TAOM_Hair"],
                    material_indices, attributes)


@contextmanager
def blender_transaction():
    import bpy
    before_objects = set(bpy.data.objects)
    active = bpy.context.view_layer.objects.active
    selected = list(bpy.context.selected_objects)
    mode = active.mode if active else "OBJECT"
    try:
        yield
    except BaseException:
        for obj in set(bpy.data.objects) - before_objects:
            bpy.data.objects.remove(obj, do_unlink=True)
        raise
    finally:
        if active and active.name in bpy.data.objects and active.mode != mode:
            bpy.ops.object.mode_set(mode=mode)
        bpy.ops.object.select_all(action="DESELECT")
        for obj in selected:
            if obj.name in bpy.data.objects:
                obj.select_set(True)
        bpy.context.view_layer.objects.active = active if active and active.name in bpy.data.objects else None
