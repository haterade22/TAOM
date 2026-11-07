"""Production Blender operators for the independent TAOM hair engine."""

from pathlib import Path
import sys

import bpy
import numpy as np

TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from hair_engine import generation, grooming, shell_hair, short_hair
from hair_engine.convert import curves_to_mesh
from hair_engine.library import PresetLibrary
from hair_engine.model import Hair
from hair_engine.profiles import Profile
from hair_engine_adapter import (blender_to_hair, blender_to_mesh, blender_transaction,
                                 hair_to_blender, mesh_to_blender)


def _mesh_arrays(obj):
    obj.data.calc_loop_triangles()
    vertices = np.asarray([tuple(vertex.co) for vertex in obj.data.vertices], dtype=np.float64)
    faces = np.asarray([tuple(triangle.vertices) for triangle in obj.data.loop_triangles], dtype=np.int32)
    return vertices, faces


def _profile(kind, width):
    return Profile(kind.lower(), width=width, resolution=4 if kind == "ROUND" else 2)


class TAOM_HAIR_OT_generate(bpy.types.Operator):
    bl_idname = "taom_hair.generate"
    bl_label = "Generate TAOM Hair"
    bl_options = {"REGISTER", "UNDO"}

    count: bpy.props.IntProperty(default=32, min=1)
    length: bpy.props.FloatProperty(default=0.25, min=0.0001)
    resolution: bpy.props.IntProperty(default=6, min=2)
    seed: bpy.props.IntProperty(default=13, min=0)
    profile: bpy.props.EnumProperty(items=[(kind, kind.title(), "") for kind in
                                           ("FLAT", "ROUND", "CURL", "BRAID")], default="FLAT")
    width: bpy.props.FloatProperty(default=0.01, min=0.00001)

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "MESH"

    def execute(self, context):
        source = context.active_object
        vertices, faces = _mesh_arrays(source)
        guides = generation.surface_guides(vertices, faces, self.count, self.length,
                                           self.resolution, self.seed)
        hair = Hair(guides, {"profile": self.profile, "width": self.width})
        with blender_transaction():
            result = hair_to_blender(hair, source.name + "_TAOM_Hair")
            result.matrix_world = source.matrix_world.copy()
            result["taom_profile"] = self.profile
            result["taom_width"] = self.width
        return {"FINISHED"}


class TAOM_HAIR_OT_groom(bpy.types.Operator):
    bl_idname = "taom_hair.groom"
    bl_label = "Groom TAOM Hair"
    bl_options = {"REGISTER", "UNDO"}

    operation: bpy.props.EnumProperty(items=[(name, name.title(), "") for name in
                                             ("LENGTH", "TRIM", "TAPER", "TWIST", "SMOOTH", "CURL", "NOISE", "MIRROR")],
                                      default="SMOOTH")
    factor: bpy.props.FloatProperty(default=0.5)

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "CURVES"

    def execute(self, context):
        source = context.active_object
        hair = blender_to_hair(source)
        operations = {
            "LENGTH": lambda strand: grooming.adjust_length(strand, 1.0 + self.factor),
            "TRIM": lambda strand: grooming.trim(strand, self.factor),
            "TAPER": lambda strand: grooming.taper(strand, tip=self.factor),
            "TWIST": lambda strand: grooming.twist(strand, self.factor),
            "SMOOTH": lambda strand: grooming.smooth(strand, factor=self.factor),
            "CURL": lambda strand: grooming.curl(strand, radius=self.factor, turns=2.0),
            "NOISE": lambda strand: grooming.noise(strand, amplitude=self.factor),
        }
        if self.operation == "MIRROR":
            result = grooming.mirror(hair)
        else:
            if self.operation not in operations:
                raise ValueError(f"unknown grooming operation {self.operation}")
            result = Hair([operations[self.operation](strand) for strand in hair.strands], hair.attributes)
        with blender_transaction():
            output = hair_to_blender(result, source.name + "_Groomed")
            output.matrix_world = source.matrix_world.copy()
        return {"FINISHED"}


class TAOM_HAIR_OT_convert(bpy.types.Operator):
    bl_idname = "taom_hair.convert"
    bl_label = "Convert TAOM Hair to Mesh"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "CURVES"

    def execute(self, context):
        source = context.active_object
        hair = blender_to_hair(source)
        profile = _profile(source.get("taom_profile", "FLAT"), source.get("taom_width", 0.01))
        result = curves_to_mesh(hair, default_profile=profile)
        with blender_transaction():
            output = mesh_to_blender(result.mesh, source.name + "_Mesh")
            output.matrix_world = source.matrix_world.copy()
        return {"FINISHED"}


class TAOM_HAIR_OT_short_hair(bpy.types.Operator):
    bl_idname = "taom_hair.short_hair"
    bl_label = "Generate TAOM Short Hair"
    bl_options = {"REGISTER", "UNDO"}

    mode: bpy.props.EnumProperty(items=[(name, name, "") for name in
                                        ("KEEP", "CELLS_TRIS", "DIAMOND", "DIAMOND_TRIS")],
                                 default="CELLS_TRIS")
    count: bpy.props.IntProperty(default=100, min=1)
    length: bpy.props.FloatProperty(default=0.03, min=0.0001)

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "MESH"

    def execute(self, context):
        source = context.active_object
        vertices, faces = _mesh_arrays(source)
        result = short_hair.generate(vertices, faces, self.mode, self.count, self.length)
        with blender_transaction():
            output = mesh_to_blender(result.mesh, source.name + "_ShortHair")
            output.matrix_world = source.matrix_world.copy()
        return {"FINISHED"}


class TAOM_HAIR_OT_shell_hair(bpy.types.Operator):
    bl_idname = "taom_hair.shell_hair"
    bl_label = "Generate TAOM Shell Hair"
    bl_options = {"REGISTER", "UNDO"}

    layers: bpy.props.IntProperty(default=12, min=1)
    spacing: bpy.props.FloatProperty(default=0.001, min=0.0)

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "MESH"

    def execute(self, context):
        source = context.active_object
        shells = shell_hair.generate(blender_to_mesh(source), self.layers, self.spacing)
        result = shell_hair.join_shells(shells)
        with blender_transaction():
            output = mesh_to_blender(result, source.name + "_ShellHair")
            output.matrix_world = source.matrix_world.copy()
        return {"FINISHED"}


class TAOM_HAIR_OT_bake(bpy.types.Operator):
    bl_idname = "taom_hair.bake"
    bl_label = "Bake TAOM Hair Maps"
    bl_options = {"REGISTER"}

    directory: bpy.props.StringProperty(subtype="DIR_PATH")
    resolution: bpy.props.IntProperty(default=128, min=1)

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "MESH"

    def execute(self, context):
        from hair_engine_bake import bake_passes
        if not self.directory:
            raise ValueError("choose an explicit bake output directory")
        bake_passes(context.active_object, self.directory, resolution=self.resolution)
        return {"FINISHED"}


class TAOM_HAIR_OT_preset_save(bpy.types.Operator):
    bl_idname = "taom_hair.preset_save"
    bl_label = "Save TAOM Hair Preset"
    bl_options = {"REGISTER", "UNDO"}

    directory: bpy.props.StringProperty(subtype="DIR_PATH")
    name: bpy.props.StringProperty(default="hair")

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "CURVES"

    def execute(self, context):
        if not self.directory:
            raise ValueError("choose an explicit preset library")
        name = PresetLibrary(self.directory).save(self.name, blender_to_hair(context.active_object))
        self.report({"INFO"}, f"Saved {name}")
        return {"FINISHED"}


class TAOM_HAIR_OT_preset_load(bpy.types.Operator):
    bl_idname = "taom_hair.preset_load"
    bl_label = "Load TAOM Hair Preset"
    bl_options = {"REGISTER", "UNDO"}

    directory: bpy.props.StringProperty(subtype="DIR_PATH")
    name: bpy.props.StringProperty(default="hair")

    def execute(self, context):
        hair = PresetLibrary(self.directory).load(self.name)
        with blender_transaction():
            hair_to_blender(hair, self.name)
        return {"FINISHED"}


class TAOM_HAIR_OT_uv_region(bpy.types.Operator):
    bl_idname = "taom_hair.uv_region"
    bl_label = "Assign TAOM UV Region"
    bl_options = {"REGISTER", "UNDO"}

    region: bpy.props.IntProperty(default=0, min=-1)

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "CURVES"

    def execute(self, context):
        source = context.active_object
        hair = blender_to_hair(source)
        for strand in hair.strands:
            strand.uv_region = self.region
        with blender_transaction():
            output = hair_to_blender(hair, source.name + "_UV")
            output.matrix_world = source.matrix_world.copy()
        return {"FINISHED"}


class TAOM_HAIR_OT_rig(bpy.types.Operator):
    bl_idname = "taom_hair.rig"
    bl_label = "Rig TAOM Hair"
    bl_options = {"REGISTER", "UNDO"}

    bones: bpy.props.IntProperty(default=4, min=1)

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "CURVES"

    def execute(self, context):
        from hair_engine_rigging import rig_to_blender, skin_mesh_to_rig
        source = context.active_object
        hair = blender_to_hair(source)
        profile = _profile(source.get("taom_profile", "FLAT"), source.get("taom_width", 0.01))
        result = curves_to_mesh(hair, default_profile=profile)
        with blender_transaction():
            rig = rig_to_blender(hair, bones_per_strand=self.bones)
            mesh = mesh_to_blender(result.mesh, source.name + "_Rigged")
            mesh.matrix_world = source.matrix_world.copy()
            skin_mesh_to_rig(mesh, rig, hair, bones_per_strand=self.bones)
        return {"FINISHED"}


class TAOM_HAIR_OT_particle(bpy.types.Operator):
    bl_idname = "taom_hair.particle"
    bl_label = "Convert TAOM Particle Hair"
    bl_options = {"REGISTER", "UNDO"}

    direction: bpy.props.EnumProperty(items=[("FROM_CURVES", "Curves to particles", ""),
                                             ("TO_CURVES", "Particles to curves", "")])

    def execute(self, context):
        from hair_engine_particle import hair_to_particles, particles_to_hair
        source = context.active_object
        if self.direction == "FROM_CURVES":
            if source.type != "MESH":
                raise ValueError("make the particle emitter active")
            curves = next((obj for obj in context.selected_objects if obj.type == "CURVES"), None)
            if curves is None:
                raise ValueError("select the source Curves object and emitter mesh")
            hair_to_particles(blender_to_hair(curves), source)
        else:
            hair = particles_to_hair(source)
            with blender_transaction():
                output = hair_to_blender(hair, source.name + "_ParticleCurves")
                output.matrix_world = source.matrix_world.copy()
        return {"FINISHED"}


class TAOM_HAIR_OT_grid_deform(bpy.types.Operator):
    bl_idname = "taom_hair.grid_deform"
    bl_label = "Deform TAOM Hair Controls"
    bl_options = {"REGISTER", "UNDO"}

    offset: bpy.props.FloatVectorProperty(default=(0.0, 0.0, 0.0), subtype="TRANSLATION")
    radius: bpy.props.FloatProperty(default=0.25, min=0.0001)

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "CURVES"

    def execute(self, context):
        source = context.active_object
        hair = blender_to_hair(source)
        controls = [(strand.positions[0], self.offset) for strand in hair.strands]
        result = grooming.grid_deform(hair, controls, self.radius)
        with blender_transaction():
            output = hair_to_blender(result, source.name + "_GridDeform")
            output.matrix_world = source.matrix_world.copy()
        return {"FINISHED"}


class TAOM_HAIR_OT_thumbnail(bpy.types.Operator):
    bl_idname = "taom_hair.thumbnail"
    bl_label = "Render TAOM Hair Thumbnail"
    bl_options = {"REGISTER"}

    filepath: bpy.props.StringProperty(subtype="FILE_PATH")

    def execute(self, context):
        from hair_engine_library import render_thumbnail
        if not self.filepath or not context.active_object:
            raise ValueError("choose an active object and thumbnail path")
        render_thumbnail(context.active_object, self.filepath)
        return {"FINISHED"}


class TAOM_HAIR_PT_panel(bpy.types.Panel):
    bl_idname = "TAOM_HAIR_PT_panel"
    bl_label = "TAOM Hair Engine"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "TAOM Hair"

    def draw(self, context):
        layout = self.layout
        for operator in ("generate", "groom", "convert", "short_hair", "shell_hair", "bake",
                         "preset_save", "preset_load", "uv_region", "rig", "particle",
                         "grid_deform", "thumbnail", "draw", "sculpt"):
            layout.operator("taom_hair." + operator)


_CLASSES = (TAOM_HAIR_OT_generate, TAOM_HAIR_OT_groom, TAOM_HAIR_OT_convert,
            TAOM_HAIR_OT_short_hair, TAOM_HAIR_OT_shell_hair, TAOM_HAIR_OT_bake,
            TAOM_HAIR_OT_preset_save, TAOM_HAIR_OT_preset_load, TAOM_HAIR_OT_uv_region,
            TAOM_HAIR_OT_rig, TAOM_HAIR_OT_particle, TAOM_HAIR_OT_grid_deform,
            TAOM_HAIR_OT_thumbnail, TAOM_HAIR_PT_panel)


def register():
    import bpy
    import hair_engine_modal
    for cls in _CLASSES:
        existing = getattr(bpy.types, cls.__name__, None)
        if existing:
            bpy.utils.unregister_class(existing)
        bpy.utils.register_class(cls)
    hair_engine_modal.register()


def unregister():
    import bpy
    import hair_engine_modal
    hair_engine_modal.unregister()
    for cls in reversed(_CLASSES):
        existing = getattr(bpy.types, cls.__name__, None)
        if existing:
            bpy.utils.unregister_class(existing)
