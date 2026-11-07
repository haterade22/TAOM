"""Interactive drawing and sculpting with cancellation-safe source preservation."""

from pathlib import Path
import sys

import bpy
import numpy as np
from bpy_extras import view3d_utils
from mathutils import Vector

TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from hair_engine.curves import resample_arc_length
from hair_engine.model import Hair, Strand
from hair_engine_adapter import blender_to_hair, blender_transaction, hair_to_blender


def _ray(context, event):
    region = context.region
    rv3d = context.region_data
    mouse = (event.mouse_region_x, event.mouse_region_y)
    origin = view3d_utils.region_2d_to_origin_3d(region, rv3d, mouse)
    direction = view3d_utils.region_2d_to_vector_3d(region, rv3d, mouse)
    return origin, direction


class TAOM_HAIR_OT_draw(bpy.types.Operator):
    bl_idname = "taom_hair.draw"
    bl_label = "Draw TAOM Hair"
    bl_options = {"REGISTER", "UNDO"}

    resolution: bpy.props.IntProperty(default=8, min=2)

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "MESH" and context.area

    def invoke(self, context, event):
        self.surface = context.active_object
        self.points = []
        self.dragging = False
        context.workspace.status_text_set("LMB draw, Return finish, Esc cancel")
        return {"RUNNING_MODAL"}

    def _hit(self, context, event):
        origin, direction = _ray(context, event)
        depsgraph = context.evaluated_depsgraph_get()
        hit, location, _, _, obj, _ = context.scene.ray_cast(depsgraph, origin, direction)
        return location if hit and obj == self.surface else None

    def _finish(self, context):
        if len(self.points) < 2:
            return {"CANCELLED"}
        inverse = self.surface.matrix_world.inverted()
        points = np.asarray([tuple(inverse @ point) for point in self.points])
        positions = resample_arc_length(points, self.resolution)
        with blender_transaction():
            result = hair_to_blender(Hair([Strand(positions)]), self.surface.name + "_DrawnHair")
            result.matrix_world = self.surface.matrix_world.copy()
        return {"FINISHED"}

    def modal(self, context, event):
        if event.type == "ESC":
            context.workspace.status_text_set(None)
            return {"CANCELLED"}
        if event.type in {"RET", "NUMPAD_ENTER", "RIGHTMOUSE"} and event.value == "PRESS":
            context.workspace.status_text_set(None)
            return self._finish(context)
        if event.type == "LEFTMOUSE" and event.value == "PRESS":
            self.dragging = True
        if event.type == "LEFTMOUSE" and event.value == "RELEASE":
            self.dragging = False
        if event.type in {"LEFTMOUSE", "MOUSEMOVE"} and (self.dragging or event.type == "LEFTMOUSE"):
            point = self._hit(context, event)
            if point is not None and (not self.points or (point - self.points[-1]).length > 0.002):
                self.points.append(point)
        return {"RUNNING_MODAL"}


class TAOM_HAIR_OT_sculpt(bpy.types.Operator):
    bl_idname = "taom_hair.sculpt"
    bl_label = "Sculpt TAOM Hair"
    bl_options = {"REGISTER", "UNDO"}

    radius: bpy.props.FloatProperty(default=0.1, min=0.0001)

    @classmethod
    def poll(cls, context):
        return context.active_object and context.active_object.type == "CURVES" and context.area

    def invoke(self, context, event):
        self.source = context.active_object
        self.hair = blender_to_hair(self.source)
        self.previous = None
        context.workspace.status_text_set("LMB sculpt, Return finish, Esc cancel")
        return {"RUNNING_MODAL"}

    def apply_delta(self, strand_index, point_index, delta):
        strand = self.hair.strands[strand_index]
        center = strand.positions[point_index]
        delta = np.asarray(delta, dtype=np.float64)
        distances = np.linalg.norm(strand.positions - center, axis=1)
        weights = np.clip(1.0 - distances / self.radius, 0.0, 1.0)
        strand.positions += weights[:, np.newaxis] * delta

    def _hit(self, context, event):
        origin, direction = _ray(context, event)
        depsgraph = context.evaluated_depsgraph_get()
        hit, location, _, _, _, _ = context.scene.ray_cast(depsgraph, origin, direction)
        return location if hit else origin + direction

    def modal(self, context, event):
        if event.type == "ESC":
            context.workspace.status_text_set(None)
            return {"CANCELLED"}
        if event.type in {"RET", "NUMPAD_ENTER", "RIGHTMOUSE"} and event.value == "PRESS":
            context.workspace.status_text_set(None)
            with blender_transaction():
                result = hair_to_blender(self.hair, self.source.name + "_Sculpted")
                result.matrix_world = self.source.matrix_world.copy()
            return {"FINISHED"}
        if event.type == "LEFTMOUSE" and event.value == "PRESS":
            self.previous = self._hit(context, event)
        elif event.type == "MOUSEMOVE" and self.previous is not None:
            current = self._hit(context, event)
            delta = current - self.previous
            world = self.source.matrix_world
            best = None
            for strand_index, strand in enumerate(self.hair.strands):
                for point_index, point in enumerate(strand.positions):
                    world_point = world @ Vector(point)
                    distance = (world_point - current).length
                    if best is None or distance < best[0]:
                        best = (distance, strand_index, point_index)
            if best and best[0] <= self.radius:
                local_delta = world.to_3x3().inverted() @ delta
                self.apply_delta(best[1], best[2], local_delta)
            self.previous = current
        elif event.type == "LEFTMOUSE" and event.value == "RELEASE":
            self.previous = None
        return {"RUNNING_MODAL"}


def register():
    for cls in (TAOM_HAIR_OT_draw, TAOM_HAIR_OT_sculpt):
        existing = getattr(bpy.types, cls.__name__, None)
        if existing:
            bpy.utils.unregister_class(existing)
        bpy.utils.register_class(cls)


def unregister():
    for cls in (TAOM_HAIR_OT_sculpt, TAOM_HAIR_OT_draw):
        existing = getattr(bpy.types, cls.__name__, None)
        if existing:
            bpy.utils.unregister_class(existing)
