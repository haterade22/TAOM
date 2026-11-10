"""Blender data adapter for TAOM's independent retopology engine."""

import bpy

try:
    from tools.retopo_engine import Mesh, RetopoSettings
except ImportError:
    from .retopo_engine import Mesh, RetopoSettings


def mesh_from_blender(obj, evaluated=False, uv_name=None):
    if obj is None or obj.type != "MESH":
        raise ValueError("a mesh object is required")
    if evaluated:
        evaluated_obj = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        data = evaluated_obj.to_mesh()
    else:
        evaluated_obj = None
        data = obj.data
    try:
        vertices = [tuple(vertex.co) for vertex in data.vertices]
        faces = [tuple(polygon.vertices) for polygon in data.polygons]
        layer = data.uv_layers.get(uv_name) if uv_name else data.uv_layers.active
        uv = [tuple(loop.uv) for loop in layer.data] if layer else []
        face_edges = {
            Mesh.edge_key(face[index], face[(index + 1) % len(face)])
            for face in faces for index in range(len(face))
        }
        lines = [tuple(edge.vertices) for edge in data.edges if Mesh.edge_key(*edge.vertices) not in face_edges]
        mesh = Mesh(
            vertices=vertices,
            faces=faces,
            lines=lines,
            uv=uv,
            uv_name=layer.name if layer else "UVMap",
        )
        for name in ("source_position", "pattern_center"):
            attribute = data.attributes.get(name)
            if attribute and attribute.domain == "POINT":
                mesh.attributes[name] = [tuple(item.vector) for item_index, item in enumerate(attribute.data)]
        for name in ("source_vertex", "source_part", "pattern_id"):
            attribute = data.attributes.get(name)
            if attribute and attribute.domain == "POINT":
                mesh.attributes[name] = [item.value for item_index, item in enumerate(attribute.data)]
        for name in ("point_guide", "open_seam", "simplify_on"):
            attribute = data.attributes.get(name)
            if attribute and attribute.domain == "POINT":
                mesh.attributes[name] = [item.value for item_index, item in enumerate(attribute.data)]
        for name in ("crease_edge", "sharp_edge", "bevel_weight_edge"):
            attribute = data.attributes.get(name)
            if attribute and attribute.domain == "EDGE":
                mesh.edge_attributes[name] = {
                    Mesh.edge_key(*data.edges[item_index].vertices): item.value
                    for item_index, item in enumerate(attribute.data)
                }
        attribute = data.attributes.get("part_border")
        if attribute and attribute.domain == "EDGE":
            mesh.edge_attributes["part_border"] = {
                Mesh.edge_key(*data.edges[item_index].vertices): item.value
                for item_index, item in enumerate(attribute.data)
            }
        return mesh
    finally:
        if evaluated_obj is not None:
            evaluated_obj.to_mesh_clear()


def mesh_to_blender(mesh, name, collection=None, edges=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(mesh.vertices, edges if edges is not None else mesh.lines, mesh.faces)
    data.update()
    expected_loops = sum(len(face) for face in mesh.faces)
    if mesh.uv and len(mesh.uv) == expected_loops:
        layer = data.uv_layers.new(name=mesh.uv_name)
        for loop, coordinate in zip(layer.data, mesh.uv):
            loop.uv = coordinate
        layer.active = True
        layer.active_render = True
    for attribute_name, values in mesh.attributes.items():
        if attribute_name in {"source_position", "pattern_center"}:
            attribute = data.attributes.new(name=attribute_name, type="FLOAT_VECTOR", domain="POINT")
            for item, value in zip(attribute.data, values):
                item.vector = value
        elif attribute_name in {"point_guide", "open_seam", "simplify_on"}:
            attribute = data.attributes.new(name=attribute_name, type="BOOLEAN", domain="POINT")
            for item, value in zip(attribute.data, values):
                item.value = bool(value)
        elif attribute_name in {"source_vertex", "source_part"} or (attribute_name == "pattern_id" and "source_position" in mesh.attributes):
            attribute = data.attributes.new(name=attribute_name, type="INT", domain="POINT")
            for item, value in zip(attribute.data, values):
                item.value = int(value)
        elif len(values) == len(mesh.faces):
            attribute = data.attributes.new(name=attribute_name, type="INT", domain="FACE")
            for item, value in zip(attribute.data, values):
                item.value = int(value)
        elif len(values) == len(mesh.vertices):
            attribute = data.attributes.new(name=attribute_name, type="INT", domain="POINT")
            for item, value in zip(attribute.data, values):
                item.value = int(value)
    for attribute_name, values in mesh.edge_attributes.items():
        attribute_type = "BOOLEAN" if attribute_name in {"sharp_edge", "part_border"} else "FLOAT"
        attribute = data.attributes.new(name=attribute_name, type=attribute_type, domain="EDGE")
        for item_index, item in enumerate(attribute.data):
            edge = Mesh.edge_key(*data.edges[item_index].vertices)
            if edge in values:
                item.value = values[edge]
    obj = bpy.data.objects.new(name, data)
    (collection or bpy.context.scene.collection).objects.link(obj)
    obj["taom_retopology_mesh"] = True
    return obj


def combine_objects(objects, name="Combined guides"):
    vertices = []
    faces = []
    lines = []
    edge_attributes = {}
    for obj in objects:
        mesh = mesh_from_blender(obj)
        offset = len(vertices)
        vertices.extend(mesh.vertices)
        faces.extend(tuple(index + offset for index in face) for face in mesh.faces)
        lines.extend(tuple(index + offset for index in edge) for edge in mesh.lines)
        for attribute_name, values in mesh.edge_attributes.items():
            target = edge_attributes.setdefault(attribute_name, {})
            for (first, second), value in values.items():
                target[Mesh.edge_key(first + offset, second + offset)] = value
    return Mesh(vertices=vertices, faces=faces, lines=lines, edge_attributes=edge_attributes)


def replace_mesh(obj, mesh):
    old = obj.data
    collection = obj.users_collection[0] if obj.users_collection else None
    replacement = mesh_to_blender(mesh, obj.name + " mesh", collection)
    obj.data = replacement.data
    bpy.data.objects.remove(replacement, do_unlink=True)
    if old.users == 0:
        bpy.data.meshes.remove(old)


def settings_from_property(properties):
    names = (
        "scale_gui", "offset_3d_cloth", "loop_iteration", "initialize_planes", "angle_threshold",
        "corners_threshold", "median_corners_threshold", "auto_fill_prepass", "auto_fill",
        "auto_fill_max_iterations", "output_generated_planes", "base_density",
        "snapping_distance", "seams_merge_distance", "smooth_low_poly", "auto_density",
        "full_crease_control", "density_add", "density_add_threshold", "subdiv_reproject",
        "smooth_uvs", "fit_uv", "separate_patterns", "separate_parts", "thickness_enabled",
        "thickness", "thickness_offset", "hide_debug", "points_guide", "mismatch_lines",
        "only_patterns", "only_seams", "wireframe", "seams", "polyloops", "debug_material",
        "hide_viewport_info", "morph_2d_3d", "sibling_contact", "edge_count",
        "manual_density", "show_snapping_distance", "preview_source_cloth",
        "debug_material_seed", "ui_hue",
    )
    values = {name: getattr(properties, name) for name in names}
    values["thickness"] = properties.thickness
    values["thickness_offset"] = properties.thickness_offset
    return RetopoSettings(**values)
