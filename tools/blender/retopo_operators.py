"""Blender operators for the behavioral contract in docs/research/retopo-planes-1-6-3-clean-room.md."""

import colorsys
import random

import bmesh
import bpy
from mathutils import Matrix
from bpy.props import (
    BoolProperty,
    EnumProperty,
    FloatProperty,
    IntProperty,
    PointerProperty,
    StringProperty,
)

try:
    from tools.retopo_engine import Mesh, engine
    from tools.blender.retopo_adapter import (
        combine_objects,
        mesh_from_blender,
        mesh_to_blender,
        replace_mesh,
        settings_from_property,
    )
except ImportError:
    from .retopo_engine import Mesh, engine
    from .retopo_adapter import (
        combine_objects,
        mesh_from_blender,
        mesh_to_blender,
        replace_mesh,
        settings_from_property,
    )


class TAOM_RetopoSettings(bpy.types.PropertyGroup):
    source_object: PointerProperty(name="Source garment", type=bpy.types.Object)
    source_uv_name: StringProperty(name="Source UV Map", default="")
    guide_collection: PointerProperty(name="Retopo Planes", type=bpy.types.Collection)
    scale_gui: FloatProperty(name="Scale GUI", default=1.0, min=0.0, max=10.0)
    offset_3d_cloth: FloatProperty(name="Offset 3D Cloth", default=0.0, soft_min=-10.0, soft_max=10.0)
    loop_iteration: IntProperty(name="LoopIteration", default=0, min=0, max=50)
    initialize_planes: BoolProperty(name="Initialize Planes", default=False)
    angle_threshold: FloatProperty(name="Angle Threshold", default=0.55, min=0.0, max=1.0)
    corners_threshold: FloatProperty(name="Corners Threshold", default=0.5, min=0.0, max=1.0)
    median_corners_threshold: FloatProperty(name="Median Corners Threshold", default=0.5, min=0.0, max=1.0)
    auto_fill_prepass: BoolProperty(name="Auto Fill Prepass (Point Guides)", default=False)
    auto_fill: BoolProperty(name="Auto Fill", default=False)
    auto_fill_max_iterations: IntProperty(name="Auto Fill Max Iterations", default=5, min=0, max=50)
    output_generated_planes: BoolProperty(name="Output Generated Planes", default=False)
    base_density: FloatProperty(name="Base Density", default=25.0, min=0.0, max=100.0)
    snapping_distance: FloatProperty(name="Snapping Distance", default=1.0, min=0.0, soft_max=100.0)
    seams_merge_distance: FloatProperty(name="Seams Merge Distance", default=0.003, min=0.0, soft_max=1.0, precision=5)
    smooth_low_poly: IntProperty(name="Smooth LowPoly", default=0, min=0, max=20)
    auto_density: BoolProperty(name="Auto Density", default=True)
    full_crease_control: BoolProperty(name="Full Crease Control", default=False)
    density_add: IntProperty(name="Density Add", default=2, min=0)
    density_add_threshold: FloatProperty(name="Density Add Threshold", default=0.7, min=0.0, max=1.0)
    subdiv_reproject: IntProperty(name="SubDiv + Reproject", default=0, min=0, max=6)
    smooth_uvs: BoolProperty(name="Smooth UVs", default=True)
    fit_uv: BoolProperty(name="Fit UVs to 1st Tile", default=True)
    separate_patterns: BoolProperty(name="Separate Patterns", default=False)
    separate_parts: BoolProperty(name="Separate Parts", default=True)
    thickness_enabled: BoolProperty(name="Thickness Enabled", default=False)
    thickness: FloatProperty(name="Thickness", default=0.005, soft_max=1.0, precision=5)
    thickness_offset: FloatProperty(name="Thickness Offset", default=-1.0, min=-1.0, max=1.0)
    hide_debug: BoolProperty(name="Hide Debug", default=False)
    points_guide: BoolProperty(name="Points Guide", default=True)
    mismatch_lines: BoolProperty(name="Mismatch Lines", default=True)
    only_patterns: BoolProperty(name="Only Patterns", default=False)
    only_seams: BoolProperty(name="Only Seams", default=False)
    wireframe: BoolProperty(name="Wireframe", default=True)
    seams: BoolProperty(name="Seams", default=True)
    polyloops: BoolProperty(name="PolyLoops", default=False)
    debug_material: EnumProperty(
        name="Debug Material",
        items=[
            ("Patterns", "Patterns", ""),
            ("Parts", "Parts", ""),
            ("Retopo Planes", "Retopo Planes", ""),
            ("None", "None", ""),
        ],
        default="Retopo Planes",
    )
    hide_viewport_info: BoolProperty(name="Hide Viewport Info", default=False)
    morph_2d_3d: FloatProperty(name="Morph 2D > 3D", default=0.0, min=0.0, max=1.0)
    sibling_contact: BoolProperty(name="Sibling Contact", default=True)
    edge_count: BoolProperty(name="Edge Count", default=True)
    manual_density: BoolProperty(name="Manual Density (Crease)", default=True)
    show_snapping_distance: BoolProperty(name="Snapping Distance", default=True)
    preview_source_cloth: BoolProperty(name="Preview Source Cloth", default=False)
    debug_material_seed: IntProperty(name="Debug Mat Seed", default=0, min=0, max=2147483647)
    ui_hue: FloatProperty(name="UI Hue", default=0.0, min=0.0, max=1.0)


def _settings(context):
    return context.scene.taom_retopology


def _active_mesh(context):
    obj = context.active_object
    if obj is None or obj.type != "MESH":
        raise RuntimeError("select a mesh object")
    return obj


def _source(context):
    source = _settings(context).source_object
    if source is None or source.type != "MESH":
        raise RuntimeError("assign a source garment in the TAOM Retopology panel")
    return source


def _guide_mesh(context):
    collection = _settings(context).guide_collection
    if collection:
        objects = [obj for obj in collection.objects if obj.type == "MESH"]
    else:
        objects = [_active_mesh(context)]
    if not objects:
        raise RuntimeError("the Retopo Planes collection contains no mesh objects")
    return combine_objects(objects)


def _activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def _helper_collection(output):
    name = output.name + " Helpers"
    collection = bpy.data.collections.get(name)
    if collection is None:
        collection = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(collection)
        collection["taom_retopology_helpers"] = True
    for obj in list(collection.objects):
        if obj.get("taom_retopology_helper"):
            bpy.data.objects.remove(obj, do_unlink=True)
    return collection


def _text(collection, name, body, location, size):
    data = bpy.data.curves.new(name, "FONT")
    data.body = body
    data.size = size
    data.align_x = "CENTER"
    obj = bpy.data.objects.new(name, data)
    obj.location = location
    obj.show_in_front = True
    obj["taom_retopology_helper"] = True
    collection.objects.link(obj)
    return obj


def _debug_materials(output, settings):
    output.data.materials.clear()
    if settings.debug_material == "None":
        return
    attribute_names = {
        "Patterns": "pattern_id",
        "Parts": "part_id",
        "Retopo Planes": "retopo_plane_id",
    }
    values = output.data.attributes.get(attribute_names[settings.debug_material])
    count = max((item.value for item in values.data), default=0) + 1 if values else len(output.data.polygons)
    materials = []
    for index in range(max(1, count)):
        randomizer = random.Random(settings.debug_material_seed + index)
        hue = (settings.ui_hue + randomizer.random()) % 1.0
        color = colorsys.hsv_to_rgb(hue, 0.55, 0.85)
        material = bpy.data.materials.new(f"TAOM {settings.debug_material} {index}")
        material.diffuse_color = (*color, 1.0)
        materials.append(material)
        output.data.materials.append(material)
    if values:
        for polygon, item in zip(output.data.polygons, values.data):
            polygon.material_index = item.value % len(materials)


def _sync_helpers(output, source, flat, guide, settings, result):
    if settings.wireframe:
        modifier = output.modifiers.get("TAOM Wireframe") or output.modifiers.new("TAOM Wireframe", "WIREFRAME")
        modifier.thickness = max(0.0001, settings.scale_gui * 0.001)
        modifier.use_replace = False
    elif output.modifiers.get("TAOM Wireframe"):
        output.modifiers.remove(output.modifiers["TAOM Wireframe"])
    if settings.seams:
        boundaries = set(result.boundary_edges())
        for edge in output.data.edges:
            edge.use_edge_sharp = (edge.vertices[0], edge.vertices[1]) in boundaries or (edge.vertices[1], edge.vertices[0]) in boundaries
    _debug_materials(output, settings)
    collection = _helper_collection(output)
    if settings.morph_2d_3d > 0:
        preview_mesh = engine.morph_preview(mesh_from_blender(source, evaluated=True, uv_name=_settings(bpy.context).source_uv_name or None), guide, settings)
        preview = mesh_to_blender(preview_mesh, "TAOM Morph Preview", collection, matrix_world=source.matrix_world)
        preview["taom_retopology_helper"] = True
        preview.show_in_front = True
    if settings.hide_debug:
        return
    if settings.points_guide:
        points = [flat.vertices[index] for index, value in enumerate(flat.attributes.get("point_guide", [])) if value]
        helper_mesh = Mesh(vertices=points, faces=[])
        helper = mesh_to_blender(helper_mesh, "TAOM Point Guides", collection)
        helper.show_in_front = True
        helper["taom_retopology_helper"] = True
    if settings.mismatch_lines:
        edges = engine.mismatch_edges(guide, settings)
        if edges:
            helper_mesh = Mesh(vertices=guide.vertices, faces=[], lines=edges)
            helper = mesh_to_blender(helper_mesh, "TAOM Mismatch Lines", collection, edges=edges)
            helper.show_in_front = True
            helper["taom_retopology_helper"] = True
    if settings.polyloops:
        helper = mesh_to_blender(engine.polyloop_guides(guide), "TAOM PolyLoops", collection)
        helper.show_in_front = True
        helper["taom_retopology_helper"] = True
    if settings.sibling_contact:
        edges = engine.sibling_contact_edges(guide)
        if edges:
            helper_mesh = Mesh(vertices=guide.vertices, faces=[], lines=edges)
            helper = mesh_to_blender(helper_mesh, "TAOM Sibling Contact", collection, edges=edges)
            helper.show_in_front = True
            helper["taom_retopology_helper"] = True
    if settings.preview_source_cloth:
        preview = bpy.data.objects.new("TAOM Source Preview", source.data.copy())
        collection.objects.link(preview)
        preview.matrix_world = source.matrix_world
        preview.location.z += settings.offset_3d_cloth
        preview["taom_retopology_helper"] = True
    if not settings.hide_viewport_info:
        if settings.edge_count:
            counts = engine.density_edge_counts(guide, settings)
            for index, edge in enumerate(guide.edges()):
                first, second = edge
                location = tuple((a + b) * 0.5 for a, b in zip(guide.vertices[first], guide.vertices[second]))
                _text(collection, f"TAOM Edge {index}", str(counts[edge]), location, 0.02 * settings.scale_gui)
        if settings.manual_density:
            for index, edge in enumerate(guide.edges()):
                value = guide.edge_attributes.get("crease_edge", {}).get(edge)
                if value is not None:
                    first, second = edge
                    location = tuple((a + b) * 0.5 for a, b in zip(guide.vertices[first], guide.vertices[second]))
                    _text(collection, f"TAOM Crease {index}", f"{value:.2f}", location, 0.02 * settings.scale_gui)
        if settings.show_snapping_distance:
            _text(collection, "TAOM Snap Distance", f"Snap {settings.snapping_distance:g}", (0, 0, 0.05), 0.025 * settings.scale_gui)


class TAOM_OT_flatten_patterns(bpy.types.Operator):
    bl_idname = "taom.flatten_patterns"
    bl_label = "3D to Patterns"
    bl_options = {"REGISTER", "UNDO"}

    uv_name: StringProperty(name="UV Map Name", default="")
    simplify: BoolProperty(name="Simplify", default=False)
    merge_distance: FloatProperty(name="Simplify Merge Distance", default=0.001, min=0.0)

    def execute(self, context):
        source = _active_mesh(context)
        try:
            flat = engine.flatten_to_patterns(
                mesh_from_blender(source, evaluated=True, uv_name=self.uv_name or None),
                uv_name=self.uv_name or None,
                simplify=self.simplify,
                merge_distance=self.merge_distance,
            )
            obj = mesh_to_blender(flat, source.name + "_Patterns")
            obj["source_object"] = source.name
            _settings(context).source_object = source
            _settings(context).source_uv_name = self.uv_name or source.data.uv_layers.active.name
            _activate(obj)
            return {"FINISHED"}
        except ValueError as error:
            raise RuntimeError(str(error)) from error


class TAOM_OT_initialize_planes(bpy.types.Operator):
    bl_idname = "taom.initialize_planes"
    bl_label = "Initialize Planes"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        active = _active_mesh(context)
        try:
            result = engine.initialize_planes(mesh_from_blender(active), settings_from_property(_settings(context)))
            obj = mesh_to_blender(result, active.name + "_Initialized")
            _activate(obj)
            return {"FINISHED"}
        except ValueError as error:
            raise RuntimeError(str(error)) from error


class TAOM_OT_retopologize(bpy.types.Operator):
    bl_idname = "taom.retopologize"
    bl_label = "Generate / Update Retopology"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        source = _source(context)
        settings = settings_from_property(_settings(context))
        try:
            source_mesh = mesh_from_blender(source, evaluated=True, uv_name=_settings(context).source_uv_name or None)
            flat = engine.flatten_to_patterns(source_mesh)
            guide = _guide_mesh(context)
            if settings.only_patterns:
                result = flat
            elif settings.only_seams:
                seam_mesh = Mesh(vertices=flat.vertices, faces=[], uv_name=flat.uv_name)
                output = mesh_to_blender(seam_mesh, source.name + "_Seams", edges=flat.boundary_edges())
                _activate(output)
                return {"FINISHED"}
            else:
                result = engine.retopologize(source_mesh, guide, settings)
            active = context.active_object
            if active and active.get("taom_retopology_output"):
                replace_mesh(active, result)
                output = active
            else:
                output = mesh_to_blender(result, source.name + "_Retopo")
            output["taom_retopology_output"] = True
            if settings.only_patterns or settings.output_generated_planes:
                output.matrix_world = Matrix.Identity(4)
            else:
                output.matrix_world = source.matrix_world
            output["guide_vertices"] = len(guide.vertices)
            output["guide_faces"] = len(guide.faces)
            output["engine"] = "TAOM independent UV retopology"
            _sync_helpers(output, source, flat, guide, settings, result)
            _activate(output)
            return {"FINISHED"}
        except ValueError as error:
            raise RuntimeError(str(error)) from error


class TAOM_OT_mirror_guides(bpy.types.Operator):
    bl_idname = "taom.mirror_guides"
    bl_label = "Mirror Guides"
    bl_options = {"REGISTER", "UNDO"}
    axis: EnumProperty(items=[("x", "X", ""), ("y", "Y", ""), ("xy", "X and Y", "")], default="x")

    def execute(self, context):
        active = _active_mesh(context)
        axes = ("x", "y") if self.axis == "xy" else (self.axis,)
        source_mesh = mesh_from_blender(_source(context), evaluated=True, uv_name=_settings(context).source_uv_name or None)
        result = engine.mirror_guides(combine_objects([active]), axes=axes, source=source_mesh)
        obj = mesh_to_blender(result, active.name + "_Mirrored")
        _activate(obj)
        return {"FINISHED"}


class TAOM_OT_auto_fill(bpy.types.Operator):
    bl_idname = "taom.auto_fill"
    bl_label = "Auto Fill"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        active = _active_mesh(context)
        flat = engine.flatten_to_patterns(mesh_from_blender(_source(context), evaluated=True, uv_name=_settings(context).source_uv_name or None))
        points = [flat.vertices[index] for index, value in enumerate(flat.attributes["point_guide"]) if value]
        result = engine.auto_fill(
            combine_objects([active]), points, _settings(context).auto_fill_max_iterations
        )
        obj = mesh_to_blender(result, active.name + "_Filled")
        _activate(obj)
        return {"FINISHED"}


class TAOM_OT_neutralize_crease(bpy.types.Operator):
    bl_idname = "taom.neutralize_crease"
    bl_label = "Crease To 0.5"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        obj = _active_mesh(context)
        if context.mode != "EDIT_MESH":
            raise RuntimeError("enter Edit Mode and select edges")
        bm = bmesh.from_edit_mesh(obj.data)
        layer = bm.edges.layers.float.get("crease_edge") or bm.edges.layers.float.new("crease_edge")
        count = 0
        for edge in bm.edges:
            if edge.select:
                edge[layer] = 0.5
                count += 1
        bmesh.update_edit_mesh(obj.data)
        self.report({"INFO"}, f"Neutralized {count} edges")
        return {"FINISHED"}


class TAOM_OT_inspect_topology(bpy.types.Operator):
    bl_idname = "taom.inspect_topology"
    bl_label = "Retopo Inspector"
    bl_options = {"REGISTER", "UNDO"}

    fix_split_edges: BoolProperty(name="Fix (Split Edges)", default=False)
    only_debug: BoolProperty(name="Only Debug", default=False)
    scale_gui: FloatProperty(name="Scale GUI", default=1.0, min=0.0, soft_max=100.0)
    ui_hue: FloatProperty(name="UI Hue", default=0.0, min=0.0, max=1.0)

    def execute(self, context):
        active = _active_mesh(context)
        result, bad_edges = engine.inspect_topology(mesh_from_blender(active), self.fix_split_edges)
        if self.only_debug:
            result = Mesh(vertices=result.vertices, faces=[], uv_name=result.uv_name)
            output = mesh_to_blender(result, active.name + "_Inspector", edges=bad_edges)
        else:
            output = mesh_to_blender(result, active.name + "_Inspected")
            if bad_edges:
                collection = _helper_collection(output)
                debug_mesh = Mesh(vertices=result.vertices, faces=[], uv_name=result.uv_name)
                helper = mesh_to_blender(debug_mesh, "TAOM Non-Manifold Edges", collection, edges=bad_edges)
                helper["taom_retopology_helper"] = True
                helper.show_in_front = True
        output["non_manifold_edges"] = len(bad_edges)
        material = bpy.data.materials.new("TAOM Inspector")
        material.diffuse_color = (*colorsys.hsv_to_rgb(self.ui_hue, 1.0, 1.0), 1.0)
        output.data.materials.append(material)
        if bad_edges or self.only_debug:
            modifier = output.modifiers.new("TAOM Inspector Width", "WIREFRAME")
            modifier.thickness = max(0.0001, self.scale_gui * 0.005)
            modifier.use_replace = False
        self.report({"INFO"}, f"{len(bad_edges)} edges have more than two adjacent faces")
        _activate(output)
        return {"FINISHED"}


class TAOM_OT_validate(bpy.types.Operator):
    bl_idname = "taom.validate_retopology"
    bl_label = "Validate Retopology"

    def execute(self, context):
        report = engine.diagnose(
            _guide_mesh(context), settings_from_property(_settings(context)), _settings(context).source_object
        )
        if report["errors"]:
            self.report({"ERROR"}, "; ".join(report["errors"].values()))
            return {"CANCELLED"}
        self.report({"INFO"}, f"Valid: {report['guide_faces']} guide quads")
        return {"FINISHED"}


class TAOM_PT_retopology(bpy.types.Panel):
    bl_label = "TAOM Retopology"
    bl_idname = "TAOM_PT_retopology"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "TAOM"

    def draw(self, context):
        layout = self.layout
        settings = _settings(context)
        layout.prop(settings, "source_object")
        layout.prop(settings, "source_uv_name")
        layout.prop(settings, "guide_collection")
        layout.operator("taom.flatten_patterns")
        layout.operator("taom.initialize_planes")
        layout.operator("taom.retopologize", icon="MOD_MESHDEFORM")
        row = layout.row(align=True)
        row.operator("taom.auto_fill")
        row.operator("taom.neutralize_crease")
        row = layout.row(align=True)
        for axis, label in (("x", "Mirror X"), ("y", "Mirror Y"), ("xy", "Mirror XY")):
            row.operator("taom.mirror_guides", text=label).axis = axis
        layout.operator("taom.inspect_topology")
        layout.operator("taom.validate_retopology")
        for name in (
            "scale_gui", "offset_3d_cloth", "loop_iteration", "initialize_planes", "angle_threshold",
            "corners_threshold", "median_corners_threshold", "auto_fill_prepass", "auto_fill",
            "auto_fill_max_iterations", "output_generated_planes", "base_density",
            "snapping_distance", "seams_merge_distance", "smooth_low_poly", "auto_density",
            "full_crease_control", "density_add", "density_add_threshold", "subdiv_reproject",
            "smooth_uvs", "fit_uv", "separate_patterns", "separate_parts", "thickness_enabled",
            "thickness", "thickness_offset", "morph_2d_3d",
        ):
            layout.prop(settings, name)
        layout.label(text="Preview and diagnostics")
        for name in (
            "hide_debug", "points_guide", "mismatch_lines", "only_patterns", "only_seams",
            "wireframe", "seams", "polyloops", "debug_material", "hide_viewport_info",
            "sibling_contact", "edge_count", "manual_density", "show_snapping_distance",
            "preview_source_cloth", "debug_material_seed", "ui_hue",
        ):
            layout.prop(settings, name)


classes = (
    TAOM_RetopoSettings,
    TAOM_OT_flatten_patterns,
    TAOM_OT_initialize_planes,
    TAOM_OT_retopologize,
    TAOM_OT_mirror_guides,
    TAOM_OT_auto_fill,
    TAOM_OT_neutralize_crease,
    TAOM_OT_inspect_topology,
    TAOM_OT_validate,
    TAOM_PT_retopology,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.taom_retopology = PointerProperty(type=TAOM_RetopoSettings)


def unregister():
    del bpy.types.Scene.taom_retopology
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
