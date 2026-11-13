# TAOM compatibility: Patch.refresh uses instance context and the menu names the registered operator.

import math
import os

import blf
import bmesh
import bpy
import gpu
import mathutils
import rna_keymap_ui
from bpy.app.translations import contexts as i18n_contexts
from bpy.props import BoolProperty
from bpy_extras import view3d_utils
from gpu_extras.batch import batch_for_shader
from mathutils.geometry import intersect_line_plane
from mathutils.kdtree import KDTree



############################################################################################################
MAX_LINES = 6

HIT_RADIUS = 0.3

INI_CP = 1
MAX_CP = 12

LINE_RESOLUTION = 24

INI_LINE_VERTS = 3
MAX_LINE_VERTS = 64

SMOOTH_ITER = 2
SMOOTH_ITER_TUBE = 40
SMOOTH_FLOW_ITER = 2
SMOOTH_FACTOR = 0.9

MODE_STD = 0  # 0 - standard, 1 - move control point
MODE_MOVE = 1

EAGLE_EYE_INCREMENT = 0.01
EAGLE_EYE_RATIO_DEFAULT = 0.1

POINT_SIZE = 10
CONTROL_POINT_SIZE = 7.5
LINE_SIZE = 5

colors_dots = [(0.0, 1.0, 0.0, 0.5),  # 0 lines ends
               (1.0, 0.5, 0.0, 1.0),  # 1 control points
               (1.0, 1.0, 1.0, 0.5),  # 2 handles
               (0.0, 1.0, 1.0, 0.5),  # 3 active vert
               (1.0, 0.0, 0.0, 0.8),  # 4 highlighted verts
               (0.0, 0.0, 0.7, 0.35),  # 5 faces
               (0.7, 0.0, 1.0, 0.8),  # 6 active highlighted verts
               ]

colors_lines = [(1.0, 0.7, 0.0, 0.35),  # lines
                (1.0, 0.0, 0.0, 0.8)  # selected lines
                ]

text_patch = []

text_patch.append('Esc - cancel')
text_patch.append('Z - reset')
text_patch.append('X - delete highlighted line')
text_patch.append('Ctrl Z - undo')
text_patch.append('Q, Enter - keep and exit')
text_patch.append('Space - keep geo and continue')
text_patch.append('[ ] or numpad + -  - change UI size')
text_patch.append('H - hide UI')
text_patch.append('Shift T - remove Aitor distance')
text_patch.append('T - define Aitor distance')
text_patch.append('V - snap control points')
text_patch.append('Shift S - snap')
text_patch.append('S - smooth')
text_patch.append('D - stick border to lines')
text_patch.append('E - toggle line uniform vert spacing / initial vert spacing')
text_patch.append('W - reproject based on view normal')
text_patch.append('P - proportional edit')
text_patch.append('A - cycle solutions')
text_patch.append('1 , 2 .. 9 - side edge count')
text_patch.append(' ')
text_patch.append('Shift Scroll - modify snap cage distance')
text_patch.append('Ctrl Scroll - modify control points count')
text_patch.append('Scroll - modify side edge count')
text_patch.append('Shift Right Click - Force dynamic line')
text_patch.append('Ctrl Right Click - Select active control point')
text_patch.append('Right Click - Select borderline point')
text_patch.append('Click - Create point')

text_line = []

text_line.append('Q, Esc - exit')
text_line.append('[ ] or numpad + -  - change UI size')
text_line.append('V - snap control points')
text_line.append('E - toggle line uniform vert spacing / initial vert spacing')
text_line.append('P - proportional edit')
text_line.append('1 , 2 .. 9 - change control points count')
text_line.append('Shift + ... -  modify all curves')
text_line.append(' ')
text_line.append('Scroll - modify control point weight')
text_line.append('Shift Scroll - modify snap cage distance')
text_line.append('Ctrl Scroll - modify control points count')
text_line.append(' ')
text_line.append('R - rescan for loops')
text_line.append('Ctrl Right Click - deselect verts/edges')
text_line.append('Right Click - select new verts/edges')

############################################################################################################
active_objects = []

############################################################################################################


class ControlPoint:
    def __init__(self, co, line, normal=None):
        self.co = co
        self.handle = mathutils.Vector((0., 0., 1.))
        self.line = line
        self.handle_length = 0.35
        self.normal = normal

    def update_location(self, loc):
        dif = self.co - loc
        self.co = loc
        self.handle -= dif


class PatchLine:

    def __init__(self, verts, preexistant, bm, context, ini_cp, ini_line_verts, normal, snap, eagle_eye_ratio,
                 active_objects, uniform_ratios=False):

        self.eagle_eye_ratio = eagle_eye_ratio
        if (len(verts) < 2):
            raise Exception("Line must have minimum two verts!")
        self.preexistant = preexistant
        self.bm = bm
        self.context = context
        self.verts = verts

        self.normal = normal
        self.normals = []
        self.active_objects = active_objects

        self.curves = []
        self.control_points = []
        self.start = None
        self.end = None

        self._preexistant_ratios = []

        self._uniform_ratios = uniform_ratios

        if preexistant:
            count = len(verts) - 2
            self.total_control_points = count
            self.total_created_verts = count

            total_length = self._compute_curve_length([v.co for v in self.verts])
            current_length = 0.
            self._preexistant_ratios.append(0.)
            for id in range(1, len(self.verts) - 1):
                current_length += math.dist(self.verts[id - 1].co, self.verts[id].co)
                self._preexistant_ratios.append(current_length / total_length)
            self._preexistant_ratios.append(1.)
        else:
            self.total_control_points = ini_cp
            self.total_created_verts = ini_line_verts

        self._total_length = 0.

        self.snap = snap
        self._create_curves()

        self._create_geo()
        self.update_line(self.normal, self.eagle_eye_ratio)

    def delete_line(self):
        self.delete()
        for v in self.verts:
            if len(v.link_edges) == 0:
                try:
                    bmesh.ops.delete(self.bm, geom=[v], context='VERTS')
                except:
                    print("Could not delete verts!")

    def delete(self):
        if (not self.preexistant):
            if self.total_created_verts > 0:
                delete_verts = [v for v in self.verts[1:-1] if v.is_valid]
                if (len(delete_verts) > 0):
                    self.verts = [self.verts[0], self.verts[-1]]
                    try:
                        bmesh.ops.delete(self.bm, geom=delete_verts, context='VERTS')
                    except:
                        print("Could not delete verts!")
            else:
                if (self.verts[0].is_valid and self.verts[-1].is_valid):
                    new_vert = self.bm.verts.new((self.verts[0].co + self.verts[-1].co) / 2)
                    self.bm.edges.new([self.verts[0], new_vert])
                    self.bm.edges.new([new_vert, self.verts[-1]])
                    self.verts = [self.verts[0], self.verts[-1]]

                    try:
                        del_edge = list(set(self.verts[0].link_edges).intersection(self.verts[-1].link_edges))
                        bmesh.ops.delete(self.bm, geom=del_edge, context='EDGES')
                        bmesh.ops.delete(self.bm, geom=[new_vert], context='VERTS')
                    except:
                        print("Could not delete edge or vert! ", del_edge)

            bmesh.update_edit_mesh(self.context.active_object.data)
            self.context.active_object.data.update_tag()

    def toggle_uniform_spacing(self):
        self._uniform_ratios = not self._uniform_ratios
        self._update_verts_location()

    def get_vert_normal(self, id):
        return self._get_point_on_line(self._preexistant_ratios[id])[1]

    def _create_curves(self):

        # init
        self.control_points = []
        self.curves = []

        self.start = ControlPoint(self.verts[0].co, self, self.verts[0].normal)
        self.end = ControlPoint(self.verts[-1].co, self, self.verts[-1].normal)

        pairs = []

        self._total_length = 0.

        # form pairs
        if self.total_control_points > 0:
            for control_point_id in range(self.total_control_points + 1):
                if (control_point_id < self.total_control_points):
                    self.control_points.append(ControlPoint(
                        self.start.co.lerp(self.end.co, 1.0 / (self.total_control_points + 1) * (control_point_id + 1)),
                        self, self.normal))
                    if self.preexistant:
                        if control_point_id + 1 < len(self.verts):
                            self.control_points[-1].update_location(self.verts[control_point_id + 1].co.copy())
                            self.control_points[-1].normal = self.verts[control_point_id + 1].normal
                        else:
                            position = (control_point_id + 1) / (self.total_control_points + 1) * (len(self.verts) - 1)
                            index = min(int(position), len(self.verts) - 2)
                            factor = position - index
                            self.control_points[-1].update_location(self.verts[index].co.lerp(self.verts[index + 1].co, factor))
                            first_normal = self.verts[index].normal
                            second_normal = self.verts[index + 1].normal
                            if first_normal.length < 1e-8 or second_normal.length < 1e-8:
                                self.control_points[-1].normal = self.normal
                            else:
                                self.control_points[-1].normal = first_normal.slerp(second_normal, factor)
                    else:
                        self.control_points[-1].update_location(
                            get_snap_point(self.context, self.active_objects, self.control_points[-1].co, self.normal,
                                           self.eagle_eye_ratio,
                                           self.snap))
                        self.control_points[-1].normal = self.normal

                if (control_point_id == 0 or control_point_id == self.total_control_points):
                    if (control_point_id == 0):
                        pairs.append([self.start.co, self.control_points[control_point_id].co])
                    if (control_point_id == self.total_control_points):
                        pairs.append([self.control_points[control_point_id - 1].co, self.end.co])
                else:
                    pairs.append(
                        [self.control_points[control_point_id - 1].co, self.control_points[control_point_id].co])
        else:
            pairs.append([self.start.co, self.end.co])

        self._colect_normals()

        # create curves
        for i in range(len(pairs)):

            handle1 = pairs[i][0].lerp(pairs[i][1], 0.3)
            handle2 = pairs[i][1].lerp(pairs[i][0], 0.3)

            if (i < len(pairs) - 1):
                self.control_points[i].handle = handle2

            self.curves.append(
                mathutils.geometry.interpolate_bezier(pairs[i][0], handle1, handle2, pairs[i][1], LINE_RESOLUTION))

            self._total_length += self._compute_curve_length(self.curves[-1])

        # end

    def _colect_normals(self):
        # compute normal
        self.normals = []

        if self.start.normal.length == 0.:
            self.normals.append(mathutils.Vector((0, 0, 1)))
        else:
            self.normals.append(self.start.normal)
        for cp in self.control_points:
            if cp.normal.length == 0.:
                self.normals.append(mathutils.Vector((0, 0, 1)))
            else:
                self.normals.append(cp.normal)

        if self.end.normal.length == 0.:
            self.normals.append(mathutils.Vector((0, 0, 1)))
        else:
            self.normals.append(self.end.normal)

    def _compute_curve_length(self, curve):
        return sum(math.dist(curve[i], curve[i + 1]) for i in range(len(curve) - 1))

    def _create_geo(self):
        # create geo
        if not self.preexistant:
            if (self.total_created_verts < 0):
                self.total_created_verts = 0

            if (self.total_created_verts > MAX_LINE_VERTS):
                self.total_created_verts = MAX_LINE_VERTS

            new_vert = None
            for i in range(self.total_created_verts):

                ratio = 1.0 / (self.total_created_verts + 1) * (i + 1)
                new_vert = self.bm.verts.new(self._get_point_on_line(ratio)[0])
                self.verts.append(self.verts[-1])
                self.verts[i + 1] = new_vert

                if (i == 0):
                    self.bm.edges.new([self.verts[0], new_vert])
                if (i == self.total_created_verts - 1):
                    self.bm.edges.new([new_vert, self.verts[-1]])
                if (i > 0 and i < self.total_created_verts):
                    self.bm.edges.new([self.verts[i], new_vert])

            self._update_verts_location()
        # end

    def _tweak_handle(self, id):
        if (id >= 0 and id < self.total_control_points):
            if (id == 0):
                v1 = (self.verts[0].co - self.control_points[id].co)
                if (id == self.total_control_points - 1):
                    v2 = (self.control_points[id].co - self.verts[-1].co)
                else:
                    v2 = (self.control_points[id].co - self.control_points[id + 1].co)

            elif (id == self.total_control_points - 1):
                v1 = (self.control_points[id - 1].co - self.control_points[id].co)
                v2 = (self.control_points[id].co - self.verts[-1].co)
            else:
                v1 = (self.control_points[id - 1].co - self.control_points[id].co)
                v2 = (self.control_points[id].co - self.control_points[id + 1].co)

            angle = v1.angle(v2, math.pi) * 0.5
            axis = v2.cross(v1)

            R = mathutils.Matrix.Rotation(angle, 4, axis)
            m = (self.control_points[id].handle - self.control_points[id].co).magnitude

            self.control_points[id].handle = v1 @ R * m + self.control_points[id].co

            m1 = v1.magnitude
            m2 = v2.magnitude

            if (m1 < m2):
                length_handle = m1 * self.control_points[id].handle_length
            else:
                length_handle = m2 * self.control_points[id].handle_length

            vh = (self.control_points[id].handle - self.control_points[id].co).normalized() * length_handle
            self.control_points[id].handle = self.control_points[id].co + vh

    def update_line(self, normal=None, eagle_eye_ratio=None):

        if normal != None:
            self.normal = normal
        if eagle_eye_ratio != None:
            self.eagle_eye_ratio = eagle_eye_ratio

        self.curves = []

        self.start = ControlPoint(self.verts[0].co, self, self.verts[0].normal)
        self.end = ControlPoint(self.verts[-1].co, self, self.verts[-1].normal)

        pairs = []

        # tweak handles
        for i in range(self.total_control_points):
            self._tweak_handle(i - 1)
            self._tweak_handle(i)
            self._tweak_handle(i + 1)

        # form pairs
        if self.total_control_points > 0:
            for control_point_id in range(self.total_control_points + 1):

                if (control_point_id == 0 or control_point_id == self.total_control_points):
                    if (control_point_id == 0):
                        pairs.append([self.start.co, self.control_points[control_point_id].co])
                    if (control_point_id == self.total_control_points):
                        pairs.append([self.control_points[control_point_id - 1].co, self.end.co])
                else:
                    pairs.append(
                        [self.control_points[control_point_id - 1].co, self.control_points[control_point_id].co])
        else:
            pairs.append([self.start.co, self.end.co])

        # create curves
        self._total_length = 0
        if len(pairs) > 1:
            for i in range(len(pairs)):
                if (i == 0):
                    handle1 = pairs[i][0].lerp(pairs[i][1], 0.25)
                    handle2 = self.control_points[i].handle

                elif (i < len(pairs) - 1):
                    handle1 = (self.control_points[i - 1].co - self.control_points[i - 1].handle) + self.control_points[
                        i - 1].co
                    handle2 = self.control_points[i].handle

                else:
                    handle1 = (self.control_points[i - 1].co - self.control_points[i - 1].handle) + self.control_points[
                        i - 1].co
                    handle2 = pairs[i][1].lerp(pairs[i][0], 0.25)

                self.curves.append(
                    mathutils.geometry.interpolate_bezier(pairs[i][0], handle1, handle2, pairs[i][1], LINE_RESOLUTION))
                self._total_length += self._compute_curve_length(self.curves[i])
        else:
            self.curves.append(mathutils.geometry.interpolate_bezier(pairs[0][0], pairs[0][1], pairs[0][0], pairs[0][1],
                                                                     LINE_RESOLUTION))
            self._total_length += self._compute_curve_length(self.curves[0])

        # update verts location
        self._update_verts_location()
        self.context.area.tag_redraw()

    def _update_verts_location(self):
        if self.preexistant:
            if self._uniform_ratios:
                for i in range(self.total_created_verts):
                    ratio = 1.0 / (self.total_created_verts + 1) * (i + 1)
                    self.verts[i + 1].co = self._get_point_on_line(ratio)[0]
            else:
                for i in range(self.total_created_verts):
                    self.verts[i + 1].co = self._get_point_on_line(self._preexistant_ratios[i + 1])[0]
        else:
            for i in range(self.total_created_verts):
                ratio = 1.0 / (self.total_created_verts + 1) * (i + 1)
                self.verts[i + 1].co = self._get_point_on_line(ratio)[0]

        bmesh.update_edit_mesh(self.context.active_object.data)
        self.context.active_object.data.update_tag()

    def _get_point_on_line(self, ratio):
        if (ratio < 0.0):
            return self.verts[0].co
        elif (ratio > 1.0):
            return self.verts[-1].co

        desired_length = self._total_length * ratio
        length_so_far = 0.0

        current_curve = 0
        current_dot = 0
        last_length = 0.0
        last_vector = None
        while (length_so_far < desired_length and current_curve < len(self.curves)):
            last_vector = self.curves[current_curve][current_dot + 1] - self.curves[current_curve][current_dot]
            last_length = last_vector.length
            length_so_far += last_length
            if (length_so_far > desired_length):
                length_so_far -= last_length
                break

            current_dot += 1
            if (current_dot >= len(self.curves[current_curve]) - 1):
                current_dot = 0
                current_curve += 1

        # if last_length == 0: return self.verts[0]
        rest = desired_length - length_so_far
        if last_length == 0:
            last_length = 1
        rest_ratio = rest / last_length
        rest_vector = (self.curves[current_curve][current_dot + 1] - self.curves[current_curve][
            current_dot]) * rest_ratio

        # get interpolated normal
        normal = self.normals[current_curve].slerp(self.normals[current_curve + 1],
                                                   current_dot / len(self.curves[current_curve]))

        return self.curves[current_curve][current_dot] + rest_vector, normal

    def change_control_points(self, change):
        new_cp_total = self.total_control_points + change
        if (new_cp_total > 0 and new_cp_total <= MAX_CP):
            # print("Change cp")

            new_cp_locations = []
            for i in range(new_cp_total):
                ratio = 1.0 / (new_cp_total + 1) * (i + 1)
                new_cp_locations.append(self._get_point_on_line(ratio))

            self.total_control_points = new_cp_total

            self._create_curves()

            for i, cp in enumerate(self.control_points):
                cp.update_location(new_cp_locations[i][0])
                cp.normal = new_cp_locations[i][1]

            self.update_line(self.normal, self.eagle_eye_ratio)

    def set_vert_number(self, count):
        if (not self.preexistant):
            self.delete()
            self.total_created_verts = count
            self._create_geo()

    def change_vert_number(self, change):
        if (not self.preexistant and change != 0):
            self.delete()
            self.total_created_verts += change
            self._create_geo()


class PatchNode:
    def __init__(self, context, origin, sides):
        self.origin = origin
        self.type = [-1, 0, 0, 0]  # [<patch_type>, <rotations>, <x>, <y>]

        self.vert_sides = sides.copy()  # [ side for side in sides]
        self.len_sides = [len(side) - 1 for side in self.vert_sides]
        self.R = None
        self.L = None
        self.divide_line_verts = []
        self.verts_buffer = []
        self.faces_buffer = []


class Patch:
    def __init__(self, context, bm, frame, sides, first_vert, smooth_iter, smooth_factor, cursor, protected_verts,
                 scale, alternative, snap, eagle_eye_ratio, normal_length, active_objects, tube,
                 border_snap_surface=True):

        self.bm = bm
        self.normal_length = normal_length
        self.first_vert = first_vert

        self.tube = tube
        self.frame = frame

        # create fake vert
        self.fake_vert = None
        if (sum([(len(side) - 1) for side in sides]) % 2 == 1):
            self.fake_vert = self.bm.verts.new((sides[0][0].co + sides[0][1].co) * 0.5)
            self.bm.edges.new([sides[0][0], self.fake_vert])
            self.bm.edges.new([sides[0][1], self.fake_vert])
            sides[0].insert(1, self.fake_vert)

        self.active_objects = active_objects

        self.eagle_eye_ratio = eagle_eye_ratio
        self.border_snap_surface = border_snap_surface

        self.scale = scale
        self.alternative = alternative
        self.protected_verts = protected_verts
        self.context = context
        self.cursor = cursor

        self.smooth_iter = smooth_iter
        self.smooth_factor = smooth_factor
        self.alternative_exists = False
        self.snap = snap

        self.root = PatchNode(self.context, -1, sides)
        self.set_center()

    def same_same(self, verts):
        if len(self.root.vert_sides) != len(verts):
            return False

        for side in self.root.vert_sides:
            if not side[0] in verts:
                return False
        return True

    def refresh(self, sides):
        self.delete()

        self.fake_vert = None
        if (sum([(len(side) - 1) for side in sides]) % 2 == 1):
            self.fake_vert = self.bm.verts.new((sides[0][0].co + sides[0][1].co) * 0.5)
            self.bm.edges.new([sides[0][0], self.fake_vert])
            self.bm.edges.new([sides[0][1], self.fake_vert])
            sides[0].insert(1, self.fake_vert)

        self.root = PatchNode(self.context, -1, sides)

        self.process()

    def delete(self):
        self._delete(self.root)

    def _delete(self, node):

        if (node.L != None):
            self._delete(node.R)
            self._delete(node.L)

        if (len(node.divide_line_verts) > 2):
            bmesh.ops.delete(self.bm, geom=node.divide_line_verts[1: len(node.divide_line_verts) - 1], context='VERTS')
            node.divide_line_verts = []

        node.L = None
        node.R = None
        node.type = [-1, 0, 0, 0]

        if (len(node.verts_buffer) > 0):
            del_verts = [v for v in list(set(node.verts_buffer)) if v.is_valid]
            bmesh.ops.delete(self.bm, geom=del_verts, context='VERTS')
            node.verts_buffer = []

        # delete exception
        if (node == self.root):
            del_edges = []

            vert_ring = []
            for side in node.vert_sides:
                for v in side[:-1]:
                    vert_ring.append(v)

            for i, vert in enumerate(vert_ring):
                for edge in vert.link_edges:
                    vn = [v for v in edge.verts if v != vert]
                    for v in vn:
                        if (v in vert_ring and v != vert_ring[(i - 1) % len(vert_ring)] and v != vert_ring[
                            (i + 1) % len(vert_ring)]):
                            del_edges.append(edge)
                            break

            if (len(del_edges) > 0):
                print("Del exception:")
                for de in del_edges:
                    print(de)
                try:
                    bmesh.ops.delete(self.bm, geom=list(set(del_edges)), context='EDGES')
                except Exception as e:
                    print(del_edges)
                    print("Could not delete tricky edge!")
        # end exception

        node.faces_buffer = []

    def print_patch(self):
        next = self.root
        print("\n>  Patch " + len(self.root.vert_sides) + " sides")
        print("   <" + str(self.root.origin) + " " + str(self.root.len_sides) + ", type: " + str(self.root.type) + ">")
        while (next.L != None):
            print("   <" + str(next.L.origin) + " " + str(next.L.len_sides) + ", type: " + str(
                next.L.type) + ">  ---  <" +
                  str(next.R.origin) + " " + str(next.R.len_sides) + ", type: " + str(next.R.type) + ">")
            next = next.L

    def process(self):

        self._reduce(self.root)

        self._create_mesh(self.root)

        if (self.fake_vert != None):
            mrg = [self.root.vert_sides[0][0], self.root.vert_sides[0][1]]
            bmesh.ops.pointmerge(self.bm, verts=mrg, merge_co=self.root.vert_sides[0][0].co)
            del self.root.vert_sides[0][1]
            self.fake_vert = None

        self.smooth()
        self.apply_normal()
        self.snap_surface()

        self.smooth()
        self.apply_normal()
        self.snap_surface()

    def snap_surface(self, normal_length=0.):
        if (self.snap):
            self._snap_surface(self.root, normal_length)

    def _snap_surface(self, node, normal_length=0.):

        if (node.L != None):
            self._snap_surface(node.R)
            self._snap_surface(node.L)

        # clean buffer
        clean = []
        for vert in node.verts_buffer:
            if (vert.is_valid):
                clean.append(vert)
        self.verts_buffer = list(set(clean))

        verts_snap = node.verts_buffer.copy()

        if (self.border_snap_surface):
            for side in self.root.vert_sides:
                for i, vert in enumerate(side):
                    if (vert.is_valid and not vert in self.protected_verts):
                        verts_snap.append(vert)

        for v in verts_snap:
            if (v.is_valid):
                if self.tube:
                    normal = (v.co - mathutils.geometry.intersect_point_line(v.co, self.center,
                                                                             self.center + self.frame.normal)[
                        0]).normalized()
                else:
                    normal = self.frame.normal

                loc = get_snap_point(self.context, self.active_objects, v.co, normal, self.eagle_eye_ratio, True)
                if (loc != None):
                    delta = (v.co - loc).length
                    if delta > self.scale * 0.01:
                        v.co = loc

                else:
                    corners = [side[0].co for side in node.vert_sides]
                    patch_plane_origin = corners[0]
                    for c in corners[1:]:
                        patch_plane_origin += c
                    patch_plane_origin /= len(node.vert_sides)
                    patch_plane_normal = mathutils.geometry.normal(corners)
                    intersection = intersect_line_plane(v.co, v.co + v.normal, patch_plane_origin, patch_plane_normal)
                    if (intersection != None):
                        v.co = intersection

    def apply_normal(self):
        self._apply_normal(self.root)

    def _apply_normal(self, node):
        if (node.L != None):
            self._apply_normal(node.R)
            self._apply_normal(node.L)

        invert = False
        # clean faces_buffer
        clean = []
        for face in node.faces_buffer:
            if (face.is_valid):
                clean.append(face)
        node.faces_buffer = list(set(clean))
        for face in node.faces_buffer:
            if self.frame.normal.dot(face.normal) < 0.1:
                face.normal_flip()

    def smooth(self):
        next = self.root
        verts = next.verts_buffer.copy()
        while (next.L != None):
            for v in next.L.verts_buffer:
                verts.append(v)

            for v in next.R.verts_buffer:
                verts.append(v)

            next = next.L

        verts_for_set = []
        for v in verts:
            if (v.is_valid):
                verts_for_set.append(v)

        border_verts = []
        for side in self.root.vert_sides:
            for i, v in enumerate(side):
                border_verts.append(v)

        verts_list = list(set(verts_for_set).difference(border_verts))

        if (len(verts_list) > 0):
            for i in range(self.smooth_iter):
                bmesh.ops.smooth_vert(self.bm, verts=verts_list, factor=self.smooth_factor, use_axis_x=True,
                                      use_axis_y=True, use_axis_z=True)
                # self.fancy_quad_smooth(verts_list)

    ##############################################################################################################################################
    def fancy_quad_smooth(self, verts_list):

        # print("\nFancy smooth")

        verts = list(set(verts_list))

        bmesh.ops.smooth_vert(self.bm, verts=verts, factor=self.smooth_factor, use_axis_x=True, use_axis_y=True,
                              use_axis_z=True)

        vertsbag = [v for v in verts]

        vert_data = {}  # [[weight, location] ..]

        # [ vertex, vertex_left, vertex_right, vertex_opposite ]
        # get corners list
        corners = []
        for vert in verts:
            for face in vert.link_faces:
                opposite = None
                neighbors = []
                for edge in face.edges:
                    if edge.verts[0] == vert and edge.verts[1] not in verts:
                        neighbors.append(edge.verts[1])
                    elif edge.verts[1] == vert and edge.verts[0] not in verts:
                        neighbors.append(edge.verts[0])

                if len(neighbors) >= 2:
                    corners.append(vert)
                    break

        for corner in corners:
            vertsbag = [v for v in verts]

            current_id = vertsbag.index(corner)

            while len(vertsbag) > 0:

                valid = False
                current_id = (current_id + 1) % len(vertsbag)
                vert = vertsbag[current_id]

                opposite = None
                neighbors = []

                for face in vert.link_faces:
                    if len(face.verts) > 4:
                        continue

                    opposite = None
                    neighbors = []

                    for edge in face.edges:
                        if vert in edge.verts:
                            if vert == edge.verts[0]:
                                if edge.verts[1] not in neighbors:
                                    neighbors.append(edge.verts[1])
                            else:
                                if edge.verts[0] not in neighbors:
                                    neighbors.append(edge.verts[0])

                    for edge in face.edges:
                        if vert not in edge.verts:
                            if edge.verts[0] in neighbors:
                                opposite = edge.verts[1]
                                break
                            else:
                                opposite = edge.verts[0]
                                break

                    if len(neighbors) >= 2 and neighbors[0] not in vertsbag and neighbors[1] not in vertsbag:
                        valid = True
                        break

                if valid:
                    weigth = 0

                    if neighbors[0] not in verts:
                        if neighbors[0] not in vert_data:
                            vert_data[neighbors[0]] = [[0, neighbors[0].co]]
                    if neighbors[1] not in verts:
                        if neighbors[1] not in vert_data:
                            vert_data[neighbors[1]] = [[0, neighbors[1].co]]
                    if opposite != None and opposite not in vert_data:
                        if opposite not in vert_data:
                            vert_data[opposite] = [[0, opposite.co]]

                    if neighbors[0] in vert_data:
                        weigth += vert_data[neighbors[0]][-1][0] + 1

                    if neighbors[1] in vert_data:
                        weigth += vert_data[neighbors[1]][-1][0] + 1

                    if (opposite == None):
                        if vert not in vert_data:
                            vert_data[vert] = [[-1, vert.co]]
                        vertsbag.pop(current_id)
                    else:
                        a = vert_data[neighbors[0]][-1][1]
                        b = vert_data[neighbors[1]][-1][1]
                        c = vert_data[opposite][-1][1]
                        d = mathutils.geometry.intersect_point_line(c, a, b)[0]
                        dy = d - b
                        dx = a - dy
                        loc = dx + (d - c)

                        if vert not in vert_data:
                            vert_data[vert] = []

                        vert_data[vert].append([weigth, loc])

                        vertsbag.pop(current_id)

        vert_list_data = []

        for vert in verts:
            if vert in vert_data:
                # data = [d[0] for d in vert_data[vert]]
                # print(data)

                avg_loc = mathutils.Vector()
                t = 0
                t1 = 0
                for loc in vert_data[vert]:
                    if loc[0] != 0:
                        r = 1 / loc[0]
                        avg_loc += loc[1] * r
                        t += r
                        t1 += loc[0]

                if t != 0:
                    vert.co = vert.co.lerp(avg_loc / t, 1)  # 2 / t1)

        '''
        for corner_index, corner in enumerate(corners):
            vertsbag = [v for v in verts]

            cursor = corner
            while cursor != []:
                vert = cursor[0]

                # get weight
                weigth = 0
                if cursor[1] not in verts:
                    if cursor[1] not in vert_data:
                        vert_data[cursor[1]] = []
                        vert_data[cursor[1]].append([0, cursor[1].co])
                    weigth = 1
                if cursor[2] not in verts:
                    if cursor[2] not in vert_data:
                        vert_data[cursor[2]] = []
                        vert_data[cursor[2]].append([0, cursor[2].co])
                    weigth = 1

                if cursor[3] != None and cursor[3] not in verts:
                    if cursor[3] not in vert_data:
                        vert_data[cursor[3]] = []
                        vert_data[cursor[3]].append([0, cursor[3].co])

                if weigth == 0:
                    w = []
                    if cursor[1] in vert_data:
                        w.append(vert_data[cursor[1]][-1][0])

                    if cursor[2] in vert_data:
                        w.append(vert_data[cursor[2]][-1][0])

                    weigth = min(w) + 1


                # get new location

                if (cursor[3] == None):
                    if cursor[0] not in vert_data:
                        vert_data[cursor[0]] = []
                    vert_data[vert].append([-1, vert.co])
                    vertsbag.remove(vert)
                else:

                    a = vert_data[cursor[1]][-1][1]
                    b = vert_data[cursor[2]][-1][1]
                    c = vert_data[cursor[3]][-1][1]
                    d = mathutils.geometry.intersect_point_line(c, a, b)[0]
                    dy = d - b
                    dx = a - dy
                    loc = dx + (d - c)

                    if cursor[0] not in vert_data:
                        vert_data[cursor[0]] = []

                    vert_data[cursor[0]].append([weigth, loc])

                    vertsbag.remove(vert)

                # next cursor
                cursor = []
                for face in vert.link_faces:
                    next = None
                    opposite = None
                    neighbor = None

                    for v in face.verts:
                        if v != vert:
                            for edge in v.link_edges:
                                n_verts = [v for v in edge.verts if v != vert]
                                if len(n_verts) == 1:
                                    if n_verts[0] in vertsbag:
                                        next = n_verts[0]
                                    else:
                                        opposite = n_verts[0]

                    tmp = [v for v in face.verts if v != next and v != opposite and v != vert and v not in vertsbag]

                    if len(tmp) == 1:
                        neighbor = tmp[0]

                    if neighbor != None and next != None:
                        cursor = [next, vert, neighbor, opposite]
                        break



        '''

        """
        ##########################################################################################
        def process_smooth(verts_list, factor):
            verts = list(set(verts_list))
            not_processed = []
            current_id = 0


            while len(verts) > 0:
                current_id = (current_id + 1) % len(verts)
                vert = verts[current_id]

                if vert.is_boundary or vert.is_wire:
                    verts.pop(current_id)

                else:

                    opposite = None
                    neighbors = []

                    for face in vert.link_faces:
                        if len(face.verts) > 4:
                            continue

                        opposite = None
                        neighbors = []

                        for edge in face.edges:
                            if vert in edge.verts:
                                if vert == edge.verts[0]:
                                    if edge.verts[1] not in neighbors:
                                        neighbors.append(edge.verts[1])
                                else:
                                    if edge.verts[0] not in neighbors:
                                        neighbors.append(edge.verts[0])

                        for edge in face.edges:
                            if vert not in edge.verts:
                                if edge.verts[0] in neighbors:
                                    opposite = edge.verts[1]
                                    break
                                else:
                                    opposite = edge.verts[0]
                                    break

                        if len(neighbors) >= 2 and neighbors[0] not in verts and neighbors[1] not in verts:
                            break


                    if len(neighbors) >= 2 and neighbors[0] not in verts and neighbors[1] not in verts:
                        if (opposite == None):
                            not_processed.append(vert)
                            verts.pop(current_id)
                        else:
                            d = mathutils.geometry.intersect_point_line(opposite.co,
                                                                        neighbors[0].co,
                                                                        neighbors[1].co)[0]
                            dy = (d - neighbors[1].co)
                            dx = neighbors[0].co - dy
                            loc = dx + (d - opposite.co)

                            vert.co = vert.co.lerp(loc, factor)

                            verts.pop(current_id)

        def average(verts_list, factor=1., repeat=1):
            for iter in range(repeat):
                for vert in verts_list:
                    if not vert.is_boundary and not vert.is_wire:
                        loc = mathutils.Vector()
                        for edge in vert.link_edges:
                            for v in edge.verts:
                                if v != vert:
                                    loc = loc + v.co
                                    break
                        vert.co = vert.co.lerp(loc / len(vert.link_edges), factor)

        """
        # reversed_verts = []
        # for x in range(len(verts)-1, -1, -1):
        #    reversed_verts.append(verts[x])

        # process_smooth(verts, 1)
        # average(verts, 1, 1)
        # process_smooth(reversed_verts, 0.5)

        # bmesh.ops.smooth_vert(self.bm, verts=verts, factor=self.smooth_factor, use_axis_x=True, use_axis_y=True,use_axis_z=True)

        ##############################################################################################################################################

    def set_center(self):
        self.center = mathutils.Vector([0, 0, 0])
        count = 0
        if self.tube:
            for v in self.root.vert_sides[0][:-1]:
                count += 1
                self.center += v.co
            for v in self.root.vert_sides[2][:-1]:
                count += 1
                self.center += v.co
            self.center /= count
        else:
            for v in self.root.vert_sides:
                self.center += v[0].co
            self.center /= len(self.root.vert_sides)

    def has_vert(self, vert):
        for side in self.root.vert_sides:
            if vert in side:
                return True
        return False

    def _reduce(self, patch):

        max_d = -1
        max_id = -1
        ln = len(patch.len_sides)
        for i in range(ln):
            d = min(patch.len_sides[(i - 1) % ln], patch.len_sides[(i + 1) % ln]) - 1
            if (d >= max_d):
                max_d = d
                max_id = i

        max_id_p1 = (max_id + 1) % ln
        max_id_m1 = (max_id - 1) % ln

        if (max_d > 0):

            patch.divide_line_verts = self._create_divide_line(
                patch.vert_sides[max_id_m1][(patch.len_sides[max_id_m1] - max_d)],
                patch.vert_sides[max_id_p1][max_d], patch.len_sides[max_id])
            for v in patch.divide_line_verts[1:-1]:
                patch.verts_buffer.append(v)

            patch.R = PatchNode(self.context, max_id,
                                [patch.vert_sides[max_id_m1][(patch.len_sides[max_id_m1] - max_d):],
                                 patch.vert_sides[max_id].copy(),
                                 patch.vert_sides[max_id_p1][0: max_d + 1],
                                 list(reversed(patch.divide_line_verts))])

            patch.R.type = [0, 0, max_d - 1, patch.len_sides[max_id] - 1]

            new_sides = []
            for i in range(len(patch.len_sides)):
                if (i == max_id_m1):
                    new_sides.append(patch.vert_sides[i][0:(patch.len_sides[i] - max_d + 1)])
                elif (i == max_id):
                    new_sides.append(patch.divide_line_verts)
                elif (i == max_id_p1):
                    new_sides.append(patch.vert_sides[i][max_d:])
                else:
                    new_sides.append(patch.vert_sides[i].copy())

            patch.L = PatchNode(self.context, 0, new_sides)
            self._reduce(patch.L)
        else:
            patch.type = self._get_type(patch.len_sides)

    def _side_perm(self, len_sides):
        len_sides = len_sides.copy()
        all_one = True
        for side in len_sides:
            if (side != 1):
                all_one = False
                break

        at_least_one = False
        for side in len_sides:
            if (side == 1):
                at_least_one = True
                break
        if (all_one or len(len_sides) < 3 or (not at_least_one)): return [len_sides, 0]

        rotations = 0
        while (len_sides[0] == 1 or len_sides[-1] != 1):
            first = len_sides[0]
            for i in range(len(len_sides) - 1):
                len_sides[i] = len_sides[i + 1]
            len_sides[-1] = first
            rotations += 1
        return [len_sides, len(len_sides) - rotations]

    def _get_type(self, len_sides):
        psides, rotationsCW = self._side_perm(len_sides)

        # 3 sides
        if (len(len_sides) == 3):
            # P0
            if (psides[0] == 2 and psides[1] == 1 and psides[2] == 1):
                return [0, rotationsCW, 0, 0]

            # P1
            x = (psides[0] - 4) / 2
            if (x >= 0 and int(x) == x and psides[1] == 1 and psides[2] == 1):
                return [10, rotationsCW, int(x), 0]

        # 4 sides
        elif (len(len_sides) == 4):
            # P0
            if (len_sides[0] == len_sides[2] and len_sides[1] == len_sides[3]):
                return [0, 0, len_sides[0] - 1, len_sides[1] - 1]

            # P1
            if (psides[0] == psides[1] and psides[2] == 1 and psides[3] == 1):
                return [10, rotationsCW, psides[0] - 2, 0]

            # P2
            if (psides[0] == (psides[1] - 2) and psides[2] == 1 and psides[3] == 1):
                return [20, rotationsCW, psides[0] - 1, 0]

            # P3
            if (psides[0] == (psides[1] + 2) and psides[2] == 1 and psides[3] == 1):
                return [30, rotationsCW, psides[0] - 3, 0]

            # P4
            x = (psides[0] - 3) / 2
            if (x >= 0 and int(x) == x and psides[1] == 1 and psides[2] == 1 and psides[3] == 1):
                return [40, rotationsCW, int(x), 0]

            # P5
            x = (psides[0] - psides[1] - 2) / 2
            y = psides[1] - 2
            if (x >= 0 and int(x) == x and psides[2] == 1 and psides[3] == 1):
                return [50, rotationsCW, int(x), y]

            # P6
            x = psides[0] - 2
            y = (psides[1] - psides[0] - 2) / 2
            if (y >= 0 and int(y) == y and psides[2] == 1 and psides[3] == 1):
                return [60, rotationsCW, x, int(y)]

        # 5 sides
        elif (len(len_sides) == 5):
            # P0
            if (psides[0] == 2 and psides[1] == 1 and psides[2] == 1 and psides[3] == 1 and psides[4] == 1):
                return [0, rotationsCW, 0, 0]

            # P1
            x = psides[0] - 2
            if (psides[0] == psides[1] + 1 and psides[2] == 1 and psides[3] == 1 and psides[4] == 1):
                return [10, rotationsCW, x, 0]

            # P2
            x = psides[0] - 1
            if (psides[0] + 1 == psides[1] and psides[2] == 1 and psides[3] == 1 and psides[4] == 1):
                return [20, rotationsCW, x, 0]

            # P3
            x = (psides[0] - 4) / 2
            if (x >= 0 and int(x) == x and psides[1] == 1 and psides[2] == 1 and psides[3] == 1 and psides[4] == 1):
                return [30, rotationsCW, int(x), 0]

            # P4
            x = (psides[0] - psides[1] - 3) / 2
            y = psides[1] - 2
            if (x >= 0 and int(x) == x and y >= 0 and psides[2] == 1 and psides[3] == 1 and psides[4] == 1):
                return [40, rotationsCW, int(x), y]

            # P5
            x = psides[0] - 2
            y = (psides[1] - psides[0] - 3) / 2
            if (y >= 0 and int(y) == y and x >= 0 and psides[2] == 1 and psides[3] == 1 and psides[4] == 1):
                return [50, rotationsCW, x, int(y)]

        # 6 sides
        elif (len(len_sides) == 6):
            # P0
            if (psides[0] == psides[3] and psides[1] == 1 and psides[2] == 1 and psides[4] == 1 and psides[5] == 1):
                return [0, rotationsCW, psides[0] - 1, 0]

            # P1
            x = psides[0] - 2
            if (psides[0] == psides[1] and psides[2] == 1 and psides[3] == 1 and psides[4] == 1 and psides[5] == 1):
                return [10, rotationsCW, x, 0]

            # P2
            x = (psides[0] - psides[3] - 2) / 2
            y = psides[3] - 1
            if (x >= 0 and int(x) == x and y >= 0 and psides[1] == 1 and psides[2] == 1 and psides[4] == 1 and psides[
                5] == 1):
                return [20, rotationsCW, int(x), y]

            # P3
            x = (psides[3] - psides[0] - 2) / 2
            y = psides[0] - 1
            if (x >= 0 and int(x) == x and y >= 0 and psides[1] == 1 and psides[2] == 1 and psides[4] == 1 and psides[
                5] == 1):
                return [30, rotationsCW, int(x), y]

            # P4
            x = (psides[0] - psides[1] - 2) / 2
            y = psides[1] - 2
            if (x >= 0 and int(x) == x and y >= 0 and psides[2] == 1 and psides[3] == 1 and psides[4] == 1 and psides[
                5] == 1):
                return [40, rotationsCW, int(x), y]

            # P5
            x = psides[0] - 2
            y = (psides[1] - psides[0] - 2) / 2
            if (x >= 0 and int(x) == x and y >= 0 and psides[2] == 1 and psides[3] == 1 and psides[4] == 1 and psides[
                5] == 1):
                return [50, rotationsCW, int(x), y]

    def _create_divide_line(self, vert_start, vert_end, length):
        new_verts = [vert_start]
        for i in range(length - 1):
            loc = vert_start.co.lerp(vert_end.co, 1.0 / length * (i + 1))

            new_verts.append(self.bm.verts.new(loc))

            self.bm.edges.new([new_verts[-2], new_verts[-1]])
        new_verts.append(vert_end)

        self.bm.edges.new([new_verts[-2], new_verts[-1]])
        return new_verts

    def _create_mesh(self, patch):

        # print ("Create patches:" + str(len(patches.len_sides)) + "." + str(patches.type[0]))

        origin = mathutils.Vector()
        for side in patch.vert_sides:
            origin += side[0].co
        origin /= len(patch.len_sides)

        if (len(patch.len_sides) == 3):
            alternative_solutions = []

            kind = [s // 10 for s in alternative_solutions]
            nos = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
            for k in kind:
                nos[k] += 1
            if ((patch.type[0] + (self.alternative % nos[patch.type[0] // 10])) in alternative_solutions):
                patch.type[0] += self.alternative % nos[patch.type[0] // 10]

            if ((patch.type[0] // 10 * 10 + 1) in alternative_solutions):
                self.alternative_exists = True
            else:
                self.alternative_exists = False

            # Pattern 3.0
            if (patch.type[0] == 0):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [1.0, 1.0, 0.0],  # 3
                         ]

                sides = [0, 2, 3]

                faces = [[0, 1, 2, 3],
                         ]

                edgexy = []

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)


            # Pattern 3.1
            elif (patch.type[0] == 10):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 0.0, 0.0],  # 3
                         [4.0, 0.0, 0.0],  # 4
                         [2.0, 1.0, 0.0],  # 5
                         [2.0, 2.0, 0.0],  # 6
                         ]

                sides = [0, 4, 6]

                faces = [[1, 2, 3, 5],
                         [3, 4, 6, 5],
                         [0, 1, 5, 6],
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

        elif (len(patch.len_sides) == 4):

            alternative_solutions = [21, 22, 31, 32]

            kind = [s // 10 for s in alternative_solutions]
            nos = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
            for k in kind:
                nos[k] += 1
            if ((patch.type[0] + (self.alternative % nos[patch.type[0] // 10])) in alternative_solutions):
                patch.type[0] += self.alternative % nos[patch.type[0] // 10]

            if ((patch.type[0] // 10 * 10 + 1) in alternative_solutions):
                self.alternative_exists = True
            else:
                self.alternative_exists = False

            # Pattern 0
            if (patch.type[0] == 0):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [0.0, 1.0, 0.0],  # 2
                         [1.0, 1.0, 0.0],  # 3
                         ]

                sides = [0, 1, 2, 3]

                faces = [[0, 1, 2, 3],
                         ]

                edgexy = [0, 1, 1, 2]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 4.1
            elif (patch.type[0] == 10):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [1.0, 1.0, 0.0],  # 3
                         [2.0, 1.0, 0.0],  # 4
                         [0.0, 2.0, 0.0],  # 5
                         [2.0, 2.0, 0.0],  # 6
                         ]

                sides = [0, 2, 6, 5]

                faces = [[0, 1, 3, 5],
                         [1, 2, 4, 3],
                         [3, 4, 6, 5],
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)


            # Pattern 4.2
            elif (patch.type[0] == 20):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [1.0, 1.0, 0.0],  # 2
                         [1.0, 2.0, 0.0],  # 3
                         [1.0, 3.0, 0.0],  # 4
                         [0.0, 3.0, 0.0],  # 5
                         ]

                sides = [0, 1, 4, 5]

                faces = [[0, 1, 2, 5],  # 0
                         [5, 2, 3, 4],  # 1
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            elif (patch.type[0] == 21):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [1.0, 1.0, 0.0],  # 2
                         [1.0, 2.0, 0.0],  # 3
                         [1.0, 3.0, 0.0],  # 4
                         [0.0, 3.0, 0.0],  # 5
                         ]

                sides = [0, 1, 4, 5]

                faces = [[0, 1, 2, 3],  # 0
                         [0, 3, 4, 5],  # 1
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            elif (patch.type[0] == 22):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [3.0, 0.0, 0.0],  # 1
                         [1.0, 1.0, 0.0],  # 2
                         [2.0, 1.0, 0.0],  # 3
                         [2.0, 2.0, 0.0],  # 4
                         [3.0, 2.0, 0.0],  # 5
                         [1.0, 3.0, 0.0],  # 6
                         [3.0, 3.0, 0.0],  # 7
                         [0.0, 4.0, 0.0],  # 8
                         [3.0, 4.0, 0.0],  # 9
                         ]

                sides = [0, 1, 9, 8]

                faces = [[0, 1, 3, 2],  # 0
                         [0, 2, 6, 8],  # 1
                         [2, 3, 4, 6],  # 2
                         [3, 1, 5, 4],  # 3
                         [4, 5, 7, 6],  # 4
                         [6, 7, 9, 8],  # 5
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 4.3
            elif (patch.type[0] == 30):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 0.0, 0.0],  # 3
                         [3.0, 1.0, 0.0],  # 4
                         [0.0, 1.0, 0.0],  # 5
                         ]

                sides = [0, 3, 4, 5]

                faces = [[0, 1, 2, 5],  # 0
                         [2, 3, 4, 5],  # 1
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            elif (patch.type[0] == 31):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 0.0, 0.0],  # 3
                         [3.0, 1.0, 0.0],  # 4
                         [0.0, 1.0, 0.0],  # 5
                         ]

                sides = [0, 3, 4, 5]

                faces = [[0, 1, 4, 5],  # 0
                         [1, 2, 3, 4],  # 1
                         ]

                edgexy = [1, 2]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            elif (patch.type[0] == 32):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [4.0, 0.0, 0.0],  # 3
                         [2.0, 1.0, 0.0],  # 4
                         [3.0, 1.0, 0.0],  # 5
                         [1.0, 2.0, 0.0],  # 6
                         [3.0, 2.0, 0.0],  # 7
                         [0.0, 3.0, 0.0],  # 8
                         [4.0, 3.0, 0.0],  # 9
                         ]

                sides = [0, 3, 9, 8]

                faces = [[0, 1, 6, 8],  # 0
                         [1, 2, 4, 6],  # 1
                         [2, 3, 5, 4],  # 2
                         [5, 3, 9, 7],  # 3
                         [4, 5, 7, 6],  # 4
                         [6, 7, 9, 8],  # 5
                         ]

                edgexy = [1, 2]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 4.4
            elif (patch.type[0] == 40):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 0.0, 0.0],  # 3
                         [1.0, 1.0, 0.0],  # 4
                         [2.0, 1.0, 0.0],  # 5
                         [0.0, 2.0, 0.0],  # 6
                         [3.0, 2.0, 0.0],  # 7
                         ]

                sides = [0, 3, 7, 6]

                faces = [[0, 1, 4, 6],
                         [1, 2, 5, 4],
                         [2, 3, 7, 5],
                         [4, 5, 7, 6],
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)



            # Pattern 4.5
            elif (patch.type[0] == 50):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 0.0, 0.0],  # 3
                         [4.0, 0.0, 0.0],  # 4
                         [1.0, 1.0, 0.0],  # 5
                         [2.0, 1.0, 0.0],  # 6
                         [3.0, 1.0, 0.0],  # 7
                         [4.0, 1.0, 0.0],  # 8
                         [0.0, 2.0, 0.0],  # 9
                         [4.0, 2.0, 0.0],  # 10
                         ]

                sides = [0, 4, 10, 9]

                faces = [[0, 1, 5, 9],
                         [1, 2, 6, 5],
                         [2, 3, 7, 6],
                         [3, 4, 8, 7],
                         [6, 7, 8, 10],
                         [5, 6, 10, 9],
                         ]

                edgexy = [0, 1, 2, 3]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)


            # Pattern 4.6
            elif (patch.type[0] == 60):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [1.0, 1.0, 0.0],  # 3
                         [2.0, 1.0, 0.0],  # 4
                         [1.0, 2.0, 0.0],  # 5
                         [2.0, 2.0, 0.0],  # 6
                         [1.0, 3.0, 0.0],  # 7
                         [2.0, 3.0, 0.0],  # 8
                         [0.0, 4.0, 0.0],  # 9
                         [2.0, 4.0, 0.0],  # 10
                         ]

                sides = [0, 2, 10, 9]

                faces = [[0, 1, 3, 5],
                         [1, 2, 4, 3],
                         [3, 4, 6, 5],
                         [5, 6, 8, 7],
                         [7, 8, 10, 9],
                         [0, 5, 7, 9],
                         ]

                edgexy = [0, 1, 2, 4]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

        elif (len(patch.len_sides) == 5):

            alternative_solutions = [11, 21]

            kind = [s // 10 for s in alternative_solutions]
            nos = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
            for k in kind:
                nos[k] += 1
            if ((patch.type[0] + (self.alternative % nos[patch.type[0] // 10])) in alternative_solutions):
                patch.type[0] += self.alternative % nos[patch.type[0] // 10]

            if ((patch.type[0] // 10 * 10 + 1) in alternative_solutions):
                self.alternative_exists = True
            else:
                self.alternative_exists = False

            # Pattern 5.0
            if (patch.type[0] == 0):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [2.0, 1.0, 0.0],  # 3
                         [1.0, 2.0, 0.0],  # 4
                         [0.0, 1.0, 0.0],  # 5
                         ]

                sides = [0, 2, 3, 4, 5]

                faces = [[0, 1, 4, 5],
                         [1, 2, 3, 4],
                         ]

                edgexy = []

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 5.1
            elif (patch.type[0] == 10):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [2.0, 0.0, 0.0],  # 1
                         [4.0, 0.0, 0.0],  # 2
                         [1.0, 1.0, 0.0],  # 3
                         [2.0, 1.0, 0.0],  # 4
                         [3.0, 1.0, 0.0],  # 5
                         [0.0, 2.0, 0.0],  # 6
                         [2.0, 2.0, 0.0],  # 7
                         [4.0, 2.0, 0.0],  # 8
                         [2.0, 4.0, 0.0],  # 9
                         ]

                sides = [0, 2, 8, 9, 6]

                faces = [[0, 1, 4, 3],  # 0
                         [1, 2, 5, 4],  # 1
                         [3, 4, 5, 7],  # 2
                         [0, 3, 7, 6],  # 3
                         [5, 2, 8, 7],  # 4
                         [6, 7, 8, 9],  # 5
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            elif (patch.type[0] == 11):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [2.0, 1.0, 0.0],  # 3
                         [1.0, 2.0, 0.0],  # 4
                         [0.0, 1.0, 0.0],  # 5
                         ]

                sides = [0, 2, 3, 4, 5]

                faces = [[0, 1, 2, 3],  # 0
                         [0, 3, 4, 5],  # 1
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 5.2
            elif (patch.type[0] == 20):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [2.0, 0.0, 0.0],  # 1
                         [4.0, 0.0, 0.0],  # 2
                         [1.0, 1.0, 0.0],  # 3
                         [2.0, 1.0, 0.0],  # 4
                         [3.0, 1.0, 0.0],  # 5
                         [0.0, 2.0, 0.0],  # 6
                         [2.0, 2.0, 0.0],  # 7
                         [4.0, 2.0, 0.0],  # 8
                         [2.0, 4.0, 0.0],  # 9
                         ]

                sides = [6, 0, 2, 8, 9]

                faces = [[0, 1, 4, 3],  # 0
                         [1, 2, 5, 4],  # 1
                         [3, 4, 5, 7],  # 2
                         [0, 3, 7, 6],  # 3
                         [5, 2, 8, 7],  # 4
                         [6, 7, 8, 9],  # 5
                         ]

                edgexy = [0, 6]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            elif (patch.type[0] == 21):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [1.0, 1.0, 0.0],  # 2
                         [1.0, 2.0, 0.0],  # 3
                         [0.0, 2.0, 0.0],  # 4
                         [0.0, 1.0, 0.0],  # 5
                         ]

                sides = [0, 1, 3, 4, 5]

                faces = [[0, 1, 2, 3],  # 0
                         [0, 3, 4, 5],  # 1
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 5.3
            elif (patch.type[0] == 30):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 0.0, 0.0],  # 3
                         [4.0, 0.0, 0.0],  # 4
                         [1.0, 1.0, 0.0],  # 5
                         [2.0, 1.0, 0.0],  # 6
                         [3.0, 1.0, 0.0],  # 7
                         [0.0, 2.0, 0.0],  # 8
                         [2.0, 3.0, 0.0],  # 9
                         [4.0, 2.0, 0.0],  # 10
                         ]

                sides = [0, 4, 10, 9, 8]

                faces = [[0, 1, 5, 8],  # 0
                         [1, 2, 6, 5],  # 1
                         [2, 3, 7, 6],  # 2
                         [3, 4, 10, 7],  # 3
                         [5, 6, 9, 8],  # 4
                         [6, 7, 10, 9],  # 5
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 5.4
            elif (patch.type[0] == 40):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 0.0, 0.0],  # 3
                         [4.0, 0.0, 0.0],  # 4
                         [5.0, 0.0, 0.0],  # 5
                         [1.0, 1.0, 0.0],  # 6
                         [2.0, 1.0, 0.0],  # 7
                         [3.0, 1.0, 0.0],  # 8
                         [4.0, 1.0, 0.0],  # 9
                         [5.0, 1.0, 0.0],  # 10
                         [0.0, 2.0, 0.0],  # 11
                         [2.0, 4.0, 0.0],  # 12
                         [5.0, 2.0, 0.0],  # 13
                         ]

                sides = [0, 5, 13, 12, 11]

                faces = [[0, 1, 6, 11],  # 0
                         [1, 2, 7, 6],  # 1
                         [2, 3, 8, 7],  # 2
                         [3, 4, 9, 8],  # 3
                         [4, 5, 10, 9],  # 4
                         [6, 7, 12, 11],  # 5
                         [7, 8, 13, 12],  # 6
                         [8, 9, 10, 13],  # 7
                         ]

                edgexy = [0, 1, 3, 4]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 5.5
            elif (patch.type[0] == 50):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [1.0, 1.0, 0.0],  # 3
                         [2.0, 1.0, 0.0],  # 4
                         [1.0, 2.0, 0.0],  # 5
                         [2.0, 2.0, 0.0],  # 6
                         [1.0, 3.0, 0.0],  # 7
                         [2.0, 3.0, 0.0],  # 8
                         [1.0, 4.0, 0.0],  # 9
                         [2.0, 4.0, 0.0],  # 10
                         [0.0, 5.0, 0.0],  # 11
                         [2.0, 5.0, 0.0],  # 12
                         [-2.0, 3.0, 0.0],  # 13
                         ]

                sides = [0, 2, 12, 11, 13]

                faces = [[0, 1, 3, 5],  # 0
                         [1, 2, 4, 3],  # 1
                         [3, 4, 6, 5],  # 2
                         [0, 5, 7, 13],  # 3
                         [5, 6, 8, 7],  # 4
                         [13, 7, 9, 11],  # 5
                         [7, 8, 10, 9],  # 6
                         [9, 10, 12, 11],  # 7
                         ]

                edgexy = [0, 1, 2, 4]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

        elif (len(patch.len_sides) == 6):

            alternative_solutions = [1]

            kind = [s // 10 for s in alternative_solutions]
            nos = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
            for k in kind:
                nos[k] += 1
            if ((patch.type[0] + (self.alternative % nos[patch.type[0] // 10])) in alternative_solutions):
                patch.type[0] += self.alternative % nos[patch.type[0] // 10]

            if ((patch.type[0] // 10 * 10 + 1) in alternative_solutions):
                self.alternative_exists = True
            else:
                self.alternative_exists = False

            # Pattern 6.0
            if (patch.type[0] == 1):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 1.0, 0.0],  # 2
                         [1.0, 2.0, 0.0],  # 3
                         [0.0, 2.0, 0.0],  # 4
                         [-1.0, 1.0, 0.0],  # 5
                         [0.0, 1.0, 0.0],  # 6
                         [1.0, 1.0, 0.0],  # 7
                         ]

                sides = [0, 1, 2, 3, 4, 5]

                faces = [[0, 6, 4, 5],  # 0
                         [0, 1, 7, 6],  # 1
                         [1, 2, 3, 7],  # 2
                         [6, 7, 3, 4],  # 3
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            if (patch.type[0] == 0):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 1.0, 0.0],  # 2
                         [1.0, 2.0, 0.0],  # 3
                         [0.0, 2.0, 0.0],  # 4
                         [-1.0, 1.0, 0.0],  # 5
                         ]

                sides = [0, 1, 2, 3, 4, 5]

                faces = [[0, 1, 2, 5],  # 0
                         [5, 2, 3, 4],  # 1
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 6.1
            elif (patch.type[0] == 10):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 1.0, 0.0],  # 3
                         [2.0, 1.0, 0.0],  # 4
                         [1.0, 2.0, 0.0],  # 5
                         [4.0, 2.0, 0.0],  # 6
                         [-2.0, 2.0, 0.0],  # 7
                         [0.0, 4.0, 0.0],  # 8
                         [2.0, 4.0, 0.0],  # 9
                         ]

                sides = [0, 2, 6, 9, 8, 7]

                faces = [[0, 5, 8, 7],  # 0
                         [5, 6, 9, 8],  # 1
                         [4, 3, 6, 5],  # 2
                         [1, 2, 3, 4],  # 3
                         [0, 1, 4, 5],  # 4
                         ]

                edgexy = [0, 1]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 6.2
            elif (patch.type[0] == 20):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 0.0, 0.0],  # 3
                         [-2.0, 2.0, 0.0],  # 4
                         [1.0, 1.0, 0.0],  # 5
                         [2.0, 1.0, 0.0],  # 6
                         [5.0, 2.0, 0.0],  # 7
                         [1.0, 2.0, 0.0],  # 8
                         [2.0, 2.0, 0.0],  # 9
                         [0.0, 4.0, 0.0],  # 10
                         [3.0, 4.0, 0.0],  # 11
                         ]

                sides = [0, 3, 7, 11, 10, 4]

                faces = [[0, 1, 5, 8],  # 0
                         [1, 2, 6, 5],  # 1
                         [2, 3, 9, 6],  # 2
                         [5, 6, 9, 8],  # 3
                         [0, 8, 10, 4],  # 4
                         [8, 9, 11, 10],  # 5
                         [3, 7, 11, 9],  # 6
                         ]

                edgexy = [0, 1, 1, 2]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 6.3
            elif (patch.type[0] == 30):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 0.0, 0.0],  # 3
                         [-2.0, 2.0, 0.0],  # 4
                         [1.0, 1.0, 0.0],  # 5
                         [2.0, 1.0, 0.0],  # 6
                         [5.0, 2.0, 0.0],  # 7
                         [1.0, 2.0, 0.0],  # 8
                         [2.0, 2.0, 0.0],  # 9
                         [0.0, 4.0, 0.0],  # 10
                         [3.0, 4.0, 0.0],  # 11
                         ]

                sides = [11, 10, 4, 0, 3, 7]

                faces = [[0, 1, 5, 8],  # 0
                         [1, 2, 6, 5],  # 1
                         [2, 3, 9, 6],  # 2
                         [5, 6, 9, 8],  # 3
                         [0, 8, 10, 4],  # 4
                         [8, 9, 11, 10],  # 5
                         [3, 7, 11, 9],  # 6
                         ]

                edgexy = [0, 1, 1, 2]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 6.4
            elif (patch.type[0] == 40):
                verts = [[0.0, 0.0, 0.0],  # 0
                         [1.0, 0.0, 0.0],  # 1
                         [2.0, 0.0, 0.0],  # 2
                         [3.0, 0.0, 0.0],  # 3
                         [4.0, 0.0, 0.0],  # 4
                         [3.0, 1.0, 0.0],  # 5
                         [5.0, 1.0, 0.0],  # 6
                         [-2.0, 2.0, 0.0],  # 7
                         [1.0, 2.0, 0.0],  # 8
                         [2.0, 2.0, 0.0],  # 9
                         [6.0, 2.0, 0.0],  # 10
                         [1.0, 3.0, 0.0],  # 11
                         [2.0, 3.0, 0.0],  # 12
                         [0.0, 4.0, 0.0],  # 13
                         [4.0, 4.0, 0.0],  # 14
                         ]

                sides = [0, 4, 10, 14, 13, 7]

                faces = [[0, 1, 8, 7],  # 0
                         [1, 2, 9, 8],  # 1
                         [2, 3, 5, 9],  # 2
                         [3, 4, 6, 5],  # 3
                         [5, 6, 10, 9],  # 4
                         [7, 8, 11, 13],  # 5
                         [8, 9, 12, 11],  # 6
                         [9, 10, 14, 12],  # 7
                         [11, 12, 14, 13],  # 8
                         ]

                edgexy = [0, 1, 2, 3]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

            # Pattern 6.5
            elif (patch.type[0] == 50):
                verts = [[-2.0, 2.0, 0.0],  # 0
                         [-1.0, 1.0, 0.0],  # 1
                         [0.0, 0.0, 0.0],  # 2
                         [1.0, 0.0, 0.0],  # 3
                         [2.0, 0.0, 0.0],  # 4
                         [3.0, 0.0, 0.0],  # 5
                         [4.0, 0.0, 0.0],  # 6
                         [1.0, 1.0, 0.0],  # 7
                         [2.0, 2.0, 0.0],  # 8
                         [3.0, 2.0, 0.0],  # 9
                         [6.0, 2.0, 0.0],  # 10
                         [2.0, 3.0, 0.0],  # 11
                         [3.0, 3.0, 0.0],  # 12
                         [0.0, 4.0, 0.0],  # 13
                         [4.0, 4.0, 0.0],  # 14
                         ]

                sides = [0, 2, 6, 10, 14, 13]

                faces = [[2, 3, 7, 1],  # 0
                         [1, 7, 8, 0],  # 1
                         [3, 4, 8, 7],  # 2
                         [4, 5, 9, 8],  # 3
                         [5, 6, 10, 9],  # 4
                         [0, 8, 11, 13],  # 5
                         [8, 9, 12, 11],  # 6
                         [9, 10, 14, 12],  # 7
                         [11, 12, 14, 13],  # 8
                         ]

                edgexy = [0, 1, 2, 3]

                self._pattern(origin, verts, sides, faces, edgexy, type, patch)

        if (patch.R != None):
            self._create_mesh(patch.R)
        if (patch.L != None):
            self._create_mesh(patch.L)

    def _pattern(self, origin, verts, sides, faces, edgexy, type, patch):
        v = []
        up = mathutils.Vector((0., 0., 1.))
        right = mathutils.Vector((1., 0., 0.))
        angle = self.frame.normal.angle(up)
        axis = self.frame.normal.cross(up)
        for vec in verts:
            loc = mathutils.Vector(vec) * self.scale + origin
            loc = rotate_point(loc, angle, axis, origin)
            v.append(self.bm.verts.new(loc))

        for vt in v:
            patch.verts_buffer.append(vt)

        if (len(edgexy) >= 2):
            edge_x = self.bm.edges.new([v[edgexy[0]], v[edgexy[1]]])
        if (len(edgexy) == 4):
            edge_y = self.bm.edges.new([v[edgexy[2]], v[edgexy[3]]])

        face = []
        for f in faces:
            face.append(self.bm.faces.new([v[f[0]], v[f[1]], v[f[2]], v[f[3]]]))
            patch.faces_buffer.append(face[-1])

        # divide
        new_faces = []
        if (patch.type[2] > 0):
            xring = get_edge_rings(edge_x)
            for face in bmesh.ops.subdivide_edgering(self.bm, edges=xring, cuts=patch.type[2])['faces']:
                new_faces.append(face)
                patch.faces_buffer.append(face)

        if (patch.type[3] > 0):
            yring = get_edge_rings(edge_y)
            for face in bmesh.ops.subdivide_edgering(self.bm, edges=yring, cuts=patch.type[3])['faces']:
                new_faces.append(face)
                patch.faces_buffer.append(face)

        for face in new_faces:
            for vert in face.verts:
                patch.verts_buffer.append(vert)

        # set lines
        lines = []
        for i in range(len(sides)):
            lines.append(crawl_straight_line(self.bm, v[sides[i]], v[sides[(i + 1) % len(sides)]]))

        # rotate
        for r in range(patch.type[1]):
            line_swap = lines[0]
            for i in range(len(lines) - 1):
                lines[i] = lines[i + 1]
            lines[-1] = line_swap

        # link verts
        for i in range(len(lines)):
            # print("len side: "+ str(len(patches.vert_sides[i])) + " len lines: "+ str(len(lines[i])))
            for j in range(len(lines[i]) - 1):
                if patch.vert_sides[i][j].is_valid and lines[i][j].is_valid:
                    mrg = [patch.vert_sides[i][j], lines[i][j]]
                    bmesh.ops.pointmerge(self.bm, verts=mrg, merge_co=patch.vert_sides[i][j].co)


############################################################################################################
class PatchFrame:
    def __init__(self):
        self.lines = []
        self.inverted = []
        self.normal = None

    def add(self, line, inverted=False):
        self.lines.append(line)
        self.inverted.append(inverted)

    def get(self, index):
        if index < len(self.lines):
            return [self.lines[index], self.inverted[index]]


############################################################################################################

class MdUtilMultiPetik(bpy.types.Operator):
    bl_idname = "mdutil.multi_petik"
    bl_label = "Petik"
    bl_description = "Quad mesh interactive patches maker"
    bl_options = {'REGISTER'}

    snap_type: bpy.props.IntProperty(default=1)
    border_snap_surface: bpy.props.BoolProperty(default=True)
    proportional_edit: bpy.props.BoolProperty(default=True)
    interface_size: bpy.props.FloatProperty(name="petik_interface_size", default=1.0)

    aitor_distance: bpy.props.FloatProperty(name="petik_aitor_distance", default=-1.0)

    tube: bpy.props.BoolProperty(default=False)

    hide_ui: bpy.props.IntProperty(name="petik_hide_ui", default=0)

    def menu_func(self, context):
        self.layout.operator(MdUtilMultiPetik.bl_idname)

    def invoke(self, context, event):

        os.system("cls")

        self.SELECT_MOUSE = 'RIGHTMOUSE'
        self.CLICK_MOUSE = 'LEFTMOUSE'
        self.Q = 'Q'
        self.Space = 'SPACE'

        self.active_objects = []

        self.context = None
        self.smooth_flow = False
        self.draw_handle_2d = None
        self.draw_handle_3d = None
        self.draw_event = None

        self.rightmouse_pressed = False
        self.rightmouse_shift = False
        self.rightmouse_ctrl = False

        self.last_selected = None
        self.active_vert = None

        self.highlited = None

        self.line_ini = None
        self.line_end = None

        self.lucky_leaf = None

        self.lines = []

        self.created_verts_buffer = []
        self.was_dot = False

        self.bm = None
        self.me = None

        self.mouse_x = 0
        self.mouse_y = 0

        self.mode = MODE_STD
        self.selected_control_point = None
        self.selected_dot = None

        self.selected_line = -1
        self.selected_face = -1
        self.patches = []
        self.frames = []
        self.normal = None
        self.alternative = 0
        self.alternative_exists = False
        # self.snap = True
        self.camera_pivot_is_set = False
        self.eagle_eye_ratio = EAGLE_EYE_RATIO_DEFAULT
        # self.first_vert = None
        self.protected_verts = []
        self.undo_stack = []

        self.debug = []
        self.update_tag = False

        '''
        if (context.window_manager.keyconfigs[context.preferences.keymap.active_keyconfig].preferences['select_mouse'] == 1):
            self.SELECT_MOUSE = 'RIGHTMOUSE'
            self.CLICK_MOUSE = 'LEFTMOUSE'
        else:
            self.SELECT_MOUSE = 'LEFTMOUSE'
            self.CLICK_MOUSE = 'RIGHTMOUSE'
        '''
        self.active_objects = visible_objects_and_duplis(context)

        self.ini_cp = INI_CP
        self.ini_line_verts = INI_LINE_VERTS
        self.smooth_iter = SMOOTH_ITER
        self.smooth_factor = SMOOTH_FACTOR
        self.hit_radius = HIT_RADIUS

        self.context = context
        # tooltip_text = self.text1 + self.text3
        # context.area.header_text_set(tooltip_text)

        self.rightmouse_pressed = False
        self.lines = []
        self.line_ini = None
        self.line_end = None

        self.created_verts_buffer = []

        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_mode(type='VERT')
        bpy.ops.mesh.select_all(action='DESELECT')

        self.bm = bmesh.from_edit_mesh(self.context.active_object.data)

        args = (self, context)
        self.register_handlers(args, context)

        # print("Tube mode: ", self.tube)

        addon_prefs = context.preferences.addons[__package__].preferences
        if addon_prefs.switch_Q_Space:
            self.Q = 'SPACE'
            self.Space = 'Q'
        self.ini_line_verts = addon_prefs.ini_line_verts - 1

        # self.context.region_data.view_perspective = 'PERSP'

        if context.active_object:

            context.window_manager.modal_handler_add(self)
            return {'RUNNING_MODAL'}
        else:
            self.report({'WARNING'}, "No active object, could not finish")
            return {'CANCELLED'}

    def modal(self, context, event):
        try:
            self.process_rightclick()

            if event.type == 'MIDDLEMOUSE' or event.alt:
                if self.update_tag:
                    self.update()
                # allow navigation
                return {'PASS_THROUGH'}

            elif event.type in {'WHEELDOWNMOUSE', 'WHEELUPMOUSE'} or (
                    event.type in {'PAGE_UP', 'PAGE_DOWN'} and event.value == 'PRESS') \
                    or (event.type in {'HOME', 'END'} and event.value == 'PRESS'):
                if (event.ctrl or event.type in {'HOME', 'END'}):
                    if (event.type == 'WHEELUPMOUSE' or event.type == 'HOME'):
                        change = 1
                    elif (event.type == 'WHEELDOWNMOUSE' or event.type == 'END'):
                        change = -1
                    if (self.selected_line > -1):
                        self.highlited = None
                        self.lines[self.selected_line].change_control_points(change)
                        self.hit_radius = set_hit_radius(self.context, self.lines)
                        context.area.tag_redraw()
                        return {'RUNNING_MODAL'}

                elif (event.shift):
                    # change eagle eye position

                    if self.selected_face != -1:

                        patch = self.patches[self.selected_face]

                        if (event.type == 'WHEELUPMOUSE' or event.type == 'PAGE_UP' or event.type == 'HOME'):
                            patch.eagle_eye_ratio += EAGLE_EYE_INCREMENT
                        elif (event.type == 'WHEELDOWNMOUSE' or event.type == 'PAGE_DOWN' or event.type == 'END'):
                            patch.eagle_eye_ratio -= EAGLE_EYE_INCREMENT

                        if (self.eagle_eye_ratio < EAGLE_EYE_INCREMENT):
                            patch.eagle_eye_ratio = EAGLE_EYE_INCREMENT
                        elif (self.eagle_eye_ratio > 1.0):
                            patch.eagle_eye_ratio = 1.0

                        if patch.border_snap_surface == True:
                            patch.border_snap_surface = False
                            for line in self.lines:
                                line.update_line(self.normal, patch.eagle_eye_ratio)
                            patch.border_snap_surface = True
                            patch.smooth()
                            patch.snap_surface()
                            for line in self.lines:
                                line.update_line(self.normal, patch.eagle_eye_ratio)
                        else:
                            patch.snap_surface()

                        # self.eagle_eye_ratio = patch.eagle_eye_ratio

                        bmesh.update_edit_mesh(context.active_object.data)

                    context.area.tag_redraw()

                else:
                    if (event.type == 'WHEELDOWNMOUSE' or event.type == 'PAGE_DOWN'):
                        change = -1
                    elif (event.type == 'WHEELUPMOUSE' or event.type == 'PAGE_UP'):
                        change = 1
                    if (self.selected_line > -1):
                        garbage = []
                        for id, patch in enumerate(self.patches):
                            for side in patch.root.vert_sides:
                                if self.lines[self.selected_line].verts[1] in side:
                                    garbage.append(patch)
                                    break

                        filter_patches = []
                        for patch in self.patches:
                            if patch in garbage:
                                patch.delete()
                            else:
                                filter_patches.append(patch)
                        self.patches = filter_patches

                        self.lines[self.selected_line].change_vert_number(change)

                        self.create_patches(alter_normal=False, only_cycle=False)

                        return {'RUNNING_MODAL'}
                # return {'RUNNING_MODAL'}
                return {'PASS_THROUGH'}


            elif event.type in {'ONE', 'TWO', 'THREE', 'FOUR', 'FIVE', 'SIX', 'SEVEN', 'EIGHT', 'NINE'}:
                if event.type == 'ONE':
                    cp_count = 1
                elif event.type == 'TWO':
                    cp_count = 2
                elif event.type == 'THREE':
                    cp_count = 3
                elif event.type == 'FOUR':
                    cp_count = 4
                elif event.type == 'FIVE':
                    cp_count = 5
                elif event.type == 'SIX':
                    cp_count = 6
                elif event.type == 'SEVEN':
                    cp_count = 7
                elif event.type == 'EIGHT':
                    cp_count = 8
                elif event.type == 'NINE':
                    cp_count = 9

                if (self.selected_line > -1):
                    line = self.lines[self.selected_line]
                    garbage = []
                    for id, patch in enumerate(self.patches):
                        for side in patch.root.vert_sides:
                            if self.lines[self.selected_line].verts[1] in side:
                                garbage.append(patch)
                                break

                    filter_patches = []
                    for patch in self.patches:
                        if patch in garbage:
                            patch.delete()
                        else:
                            filter_patches.append(patch)
                    self.patches = filter_patches

                    line.change_vert_number(cp_count - len(line.verts) + 1)

                    self.create_patches(alter_normal=False, only_cycle=False)

                return {'RUNNING_MODAL'}


            elif event.type == self.SELECT_MOUSE and event.value == 'PRESS':
                self.rightmouse_pressed = True
                if (event.shift): self.rightmouse_shift = True
                if (event.ctrl): self.rightmouse_ctrl = True
                self.x = event.mouse_region_x
                self.y = event.mouse_region_y

                if (self.x >= 0 and self.x <= self.context.area.width) and \
                        (self.y >= 0 and self.y <= self.context.area.height):
                    bpy.ops.view3d.select(extend=True, location=(event.mouse_region_x, event.mouse_region_y))

                if (self.SELECT_MOUSE == 'RIGHTMOUSE' and event.alt):
                    return {'PASS_THROUGH'}
                return {'RUNNING_MODAL'}


            elif (event.type == self.CLICK_MOUSE and event.value == 'PRESS'):
                self.x = event.mouse_region_x
                self.y = event.mouse_region_y

                window_size_x = self.context.area.width
                window_size_y = self.context.area.height

                if (self.x >= 0 and self.x <= self.context.area.width) and \
                        (self.y >= 0 and self.y <= self.context.area.height):
                    self._process_click()

                if (self.CLICK_MOUSE == 'RIGHTMOUSE' and event.alt):
                    return {'PASS_THROUGH'}
                return {'RUNNING_MODAL'}



            elif (event.type == 'MOUSEMOVE'):
                self.x = event.mouse_region_x
                self.y = event.mouse_region_y
                if (self.mode == MODE_MOVE):
                    self._process_click()
                else:
                    self._hover()
                return {'PASS_THROUGH'}

            elif (event.type == self.CLICK_MOUSE and event.value == 'RELEASE'):
                self.mode = MODE_STD
                self.selected_control_point = None
                self.selected_dot = None
                self.hit_radius = set_hit_radius(self.context, self.lines)
                return {'RUNNING_MODAL'}

            elif (event.type == 'Z' and event.value == 'PRESS'):
                if event.ctrl:
                    self.undo()
                else:
                    self.reset()
                return {'RUNNING_MODAL'}

            elif (event.type == 'X' and event.value == 'PRESS'):
                if (self.selected_line != -1):
                    self.delete_line(self.lines[self.selected_line])
                return {'RUNNING_MODAL'}


            elif (event.type == 'V' and event.value == 'PRESS'):
                # project control points

                self.snap_points()

                return {'RUNNING_MODAL'}

            elif (event.type == 'S' and event.value == 'PRESS'):
                if (self.selected_face != -1):
                    patch = self.patches[self.selected_face]
                    if (event.shift):
                        patch.frame.normal = self.normal
                        patch.apply_normal()
                        patch.snap_surface()
                    else:
                        patch.smooth()
                bmesh.update_edit_mesh(self.context.active_object.data)
                self.context.active_object.data.update_tag()
                return {'RUNNING_MODAL'}


            elif (event.type == 'A' and event.value == 'PRESS'):
                self.cycle_lines()
                if self.snap_type > 0:
                    for patch in self.patches:
                        patch.snap_surface()
                return {'RUNNING_MODAL'}

            elif (event.type == 'H' and event.value == 'PRESS'):
                self.hide_ui = (self.hide_ui + 1) % 3
                self.context.area.tag_redraw()
                return {'RUNNING_MODAL'}

            elif (event.type == 'W' and event.value == 'PRESS'):
                # reproject based on view normal
                if (len(self.lines) > 0 and self.selected_face != -1):
                    region = self.context.region
                    cx = region.width // 2
                    cy = region.height // 2
                    self.normal = -get_view_normal(self.context, cx, cy)

                    patch = self.patches[self.selected_face]
                    patch.frame.normal = self.normal
                    patch.apply_normal()

                    if patch.border_snap_surface == True:
                        patch.border_snap_surface = False
                        for line in self.lines:
                            line.update_line(self.normal, self.eagle_eye_ratio)
                        patch.border_snap_surface = True
                        patch.smooth()
                        patch.snap_surface()
                        for line in self.lines:
                            line.update_line(self.normal, self.eagle_eye_ratio)
                    else:
                        patch.snap_surface()

                    context.area.tag_redraw()
                return {'RUNNING_MODAL'}

            elif (event.type == 'C' and event.value == 'PRESS'):
                # toggle snap
                self.snap_type = (self.snap_type + 1) % 3

                if self.snap_type == 1:
                    self.snap_points()
                    for patch in self.patches:
                        patch.snap = True
                elif self.snap_type == 2:
                    for patch in self.patches:
                        patch.snap = False

                self.context.area.tag_redraw()
                self.update_tag = True
                return {'RUNNING_MODAL'}

            elif (event.type == 'E' and event.value == 'PRESS'):
                if self.selected_line != -1:
                    self.lines[self.selected_line].toggle_uniform_spacing()
                    self.context.area.tag_redraw()
                return {'RUNNING_MODAL'}

            elif (event.type == 'D' and event.value == 'PRESS'):
                if self.border_snap_surface:
                    self.border_snap_surface = False
                else:
                    self.border_snap_surface = True

                if (len(self.lines) > 0):
                    if self.border_snap_surface == False:
                        for patch in self.patches:
                            patch.border_snap_surface = False
                        for line in self.lines:
                            line.update_line(self.normal, self.eagle_eye_ratio)
                        bmesh.update_edit_mesh(self.context.active_object.data)
                        self.context.active_object.data.update_tag()
                    else:
                        for patch in self.patches:
                            patch.border_snap_surface = True
                            patch.smooth()
                            patch.snap_surface()
                        for line in self.lines:
                            line.update_line(self.normal, self.eagle_eye_ratio)
                        bmesh.update_edit_mesh(self.context.active_object.data)
                        self.context.active_object.data.update_tag()
                return {'RUNNING_MODAL'}

            elif (event.type == 'P' and event.value == 'PRESS'):
                self.proportional_edit = not self.proportional_edit
                self.context.area.tag_redraw()
                return {'RUNNING_MODAL'}

            elif (event.type == 'T' and event.value == 'PRESS'):
                if event.shift:
                    self.aitor_distance = -1
                else:
                    self.set_aitor_distance()
                return {'RUNNING_MODAL'}

            elif ((event.type == 'NUMPAD_PLUS' or event.type == 'RIGHT_BRACKET') and event.value == 'PRESS'):
                self.interface_size += 0.1
                self.context.area.tag_redraw()
                return {'RUNNING_MODAL'}

            elif ((event.type == 'NUMPAD_MINUS' or event.type == 'LEFT_BRACKET') and event.value == 'PRESS'):
                if self.interface_size > 0.6:
                    self.interface_size -= 0.1
                self.context.area.tag_redraw()
                return {'RUNNING_MODAL'}

            elif (event.type == 'ESC' and event.value == 'PRESS'):
                self.end()
                return {'CANCELLED'}

            elif (event.type == self.Space and event.value == 'PRESS'):
                self.keep_and_continue()
                return {'RUNNING_MODAL'}

            elif event.type in {self.Q, 'ENTER', 'NUMPAD_ENTER', 'RET'}:
                self.keep_and_continue()
                self.end()
                return {'FINISHED'}

            else:
                return {'RUNNING_MODAL'}
        except: # Exception as e:
            #print(f"An error of type {type(e).__name__} occurred: {e}")
            #traceback.print_exc()  # Prints the full traceback
            self.report({'INFO'}, 'Oh, snap! Something went wrong... Resetting...')
            self.end()

        return {'RUNNING_MODAL'}


    def keep_and_continue(self):
        if (len(self.patches) > 0):
            if self.selected_line != -1:
                keep_line = self.lines[self.selected_line]
                keep_line.preexistant = True

                new_vert_order = []
                for v in keep_line.verts:
                    new_vert_order.insert(0, v)
                keep_line.verts = new_vert_order
                keep_line.control_points = []

                self.lines.clear()
                self.undo_stack.clear()
                self.lines.append(keep_line)

                self.selected_line = 0
                self.selected_face = -1

                self.line_ini = self.lines[0].verts[-1]
                self.line_end = None

                self.was_dot = False
                self.last_selected = self.lines[0].verts[-1]
                self.active_vert = self.lines[0].verts[-1]
                self.highlited = None

                # self.first_vert = self.lines[0].verts[0]

                self.undo_stack.append([keep_line, self.active_vert])

                self.protected_verts = []

                self.created_verts_buffer = []

                self.mode = MODE_STD
                self.selected_control_point = None
                self.selected_dot = None

                self.patches = []
                self.frames = []
            else:

                self.line_ini = None
                self.line_end = None

                self.was_dot = False
                self.last_selected = None
                self.active_vert = None
                self.highlited = None
                # self.first_vert = None
                self.protected_verts = []
                self.lines.clear()

                self.created_verts_buffer = []
                self.mode = MODE_STD
                self.selected_control_point = None
                self.selected_dot = None

                self.selected_line = -1
                self.selected_face = -1
                self.patches = []
                self.frames = []
                self.undo_stack.clear()

            bmesh.update_edit_mesh(self.context.active_object.data)
            self.context.active_object.data.update_tag()
            bpy.ops.ed.undo_push()
        else:
            self.reset()

    def end(self):
        if (len(self.patches) > 0):
            for patch in self.patches:
                patch.delete()
            self.patches = []
            self.frames = []

        try:
            filter = [v for v in self.created_verts_buffer if v.is_valid]
            bmesh.ops.delete(self.bm, geom=filter, context='VERTS')
        except:
            print("Could not delete! in end")
            print(self.created_verts_buffer)

        for line in self.lines:
            line.delete()

        self.created_verts_buffer.clear()

        self.lines = None
        self.created_verts_buffer = None
        # self.context.area.header_text_set(None)
        self.mode = MODE_STD
        self.selected_control_point = None
        self.selected_dot = None
        self.last_selected = None
        self.active_vert = None
        self.highlited = None
        self.protected_verts = []
        # self.first_vert = None
        self.was_dot = False

        bmesh.update_edit_mesh(self.context.active_object.data)
        self.context.active_object.data.update_tag()
        self.bm = None
        self.context = None
        self.selected_line = -1
        self.selected_face = -1
        self.undo_stack.clear()

        self.unregister_handlers(self.context)
        print("END")

    def reset(self):
        self.line_ini = None
        self.line_end = None

        if (len(self.patches) > 0):
            for patch in self.patches:
                patch.delete()
            self.patches = []
            self.frames = []

        for line in self.lines:
            line.delete()
        self.lines.clear()

        delete_verts = [v for v in self.created_verts_buffer if v.is_valid]

        try:
            bmesh.ops.delete(self.bm, geom=delete_verts, context='VERTS')
            bmesh.update_edit_mesh(self.context.active_object.data)
        except:
            print("Could not delete! in reset")
            print(delete_verts)
        self.created_verts_buffer = []
        self.was_dot = False
        self.last_selected = None
        self.active_vert = None
        self.highlited = None
        self.protected_verts = []

        # self.first_vert = None

        self.created_verts_buffer = []
        self.mode = MODE_STD
        self.selected_control_point = None
        self.selected_dot = None

        self.selected_line = -1
        self.selected_face = -1
        self.undo_stack.clear()
        bmesh.update_edit_mesh(self.context.active_object.data)
        self.context.active_object.data.update_tag()
        bpy.ops.ed.undo_push()

    def undo(self):
        if len(self.undo_stack) > 0:

            undo_el = self.undo_stack.pop()
            self.delete_line(undo_el[0])

            self.line_ini = undo_el[1]
            self.line_end = None

            self.was_dot = False
            self.last_selected = undo_el[1]
            self.highlited = None
            self.protected_verts = []

            self.active_vert = undo_el[1]

            self.mode = MODE_STD
            self.selected_control_point = None
            self.selected_dot = None

            self.selected_line = -1
            self.selected_face = -1

            if len(self.undo_stack) == 0:
                self.reset()
            else:
                bmesh.update_edit_mesh(self.context.active_object.data)
                self.context.active_object.data.update_tag()
                bpy.ops.ed.undo_push()


    def update(self):
        if self.update_tag:
            self.update_tag = False

            for line in self.lines:
                line.update_line()

            if self.snap_type > 0:
                self.snap_points()

            for patch in self.patches:
                patch.smooth()
                patch.snap_surface()


    def draw_callback_2d(self, op, context):

        font_id = 0
        matrix = context.active_object.matrix_world
        offset_x = 55
        offset_y = 40

        blf.enable(font_id, blf.SHADOW)
        blf.shadow(font_id, 3, 0, 0, 0, 1)
        blf.shadow_offset(font_id, 1, -1)

        if self.hide_ui == 0:
            for i in range(len(self.lines)):
                pid = int(len(self.lines[i].verts) / 2)
                if len(self.lines[i].verts) % 2 == 1:
                    loc = view3d_utils.location_3d_to_region_2d(context.region, context.region_data,
                                                                matrix @ self.lines[i].verts[pid].co)
                else:
                    loc = view3d_utils.location_3d_to_region_2d(context.region, context.region_data,
                                                                matrix @ ((self.lines[i].verts[pid].co +
                                                                           self.lines[i].verts[pid - 1].co) / 2))

                if (loc != None):
                    blf.color(font_id, 0., 0., 0., 1.)
                    blf.position(font_id, loc.x, loc.y, 0)
                    blf.size(font_id, 15)
                    blf.draw(font_id, str(len(self.lines[i].verts) - 1))

        blf.size(font_id, 10)
        blf.color(font_id, 0.8, 0.8, 0.8, 1.)

        line_size = 14

        for i in range(len(text_patch)):
            blf.position(font_id, offset_x, int((30 + i * line_size) * self.interface_size) + offset_y, 0)
            blf.draw(font_id, text_patch[i])

        offset = int((len(text_patch) * line_size + 65) * self.interface_size)
        blf.position(font_id, offset_x, offset + offset_y, 0)

        if self.snap_type == 0:
            snap_text = 'OFF'
        elif self.snap_type == 1:
            snap_text = 'ON'
        else:
            snap_text = 'Only Lines'
        blf.position(font_id, offset_x, offset + offset_y, 0)
        blf.draw(font_id, "Snap patches to surface: <C> " + snap_text)

        blf.position(font_id, offset_x, offset + int(line_size * self.interface_size) + offset_y, 0)
        # blf.size(font_id, 10, 90)
        blf.draw(font_id, "Proportional edit: <P> " + str(self.proportional_edit))

        if len(self.patches) > 0:
            blf.position(font_id, offset_x, offset + int(line_size * 2 * self.interface_size) + offset_y, 0)
            # blf.size(font_id, 10, 90)
            blf.draw(font_id, "Snap borderline to surface: <D> " + str(self.patches[0].border_snap_surface))

    def draw_callback_3d(self, op, context):

        if self.hide_ui == 2:
            return

        hud(context,
            self.lines,
            self.active_vert,
            self.interface_size,
            self.highlited,
            self.patches,
            self.selected_line,
            self.selected_face)

    def register_handlers(self, args, context):
        self.draw_handle_3d = bpy.types.SpaceView3D.draw_handler_add(self.draw_callback_3d, args, 'WINDOW', 'POST_VIEW')
        self.draw_handle_2d = bpy.types.SpaceView3D.draw_handler_add(self.draw_callback_2d, args, 'WINDOW',
                                                                     'POST_PIXEL')

    def unregister_handlers(self, context):
        bpy.types.SpaceView3D.draw_handler_remove(self.draw_handle_3d, "WINDOW")
        bpy.types.SpaceView3D.draw_handler_remove(self.draw_handle_2d, "WINDOW")

    ###########################################################################################################################################

    def set_normal(self, hit_normal, view_vector):
        if (self.normal == None):
            if (view_vector != None and view_vector.magnitude > 0.):
                self.normal = hit_normal.slerp(-view_vector, 0.5, hit_normal)
            elif (hit_normal != None and hit_normal.magnitude > 0.):
                self.normal = hit_normal
            else:
                self.normal = mathutils.Vector((0., 0., 1.))
        else:
            if (hit_normal != None and hit_normal.magnitude > 0.):
                self.normal = self.normal.slerp(hit_normal, 0.01, self.normal)
            if (view_vector != None and view_vector.magnitude > 0.):
                self.normal = self.normal.slerp(-view_vector, 0.01, self.normal)

    # -------------------------------------------------------------------------------------------

    def set_aitor_distance(self):
        if self.selected_line != -1:
            distances = []
            line = self.lines[self.selected_line]
            for i in range(len(line.verts) - 1):
                distances.append((line.verts[i].co - line.verts[i + 1].co).magnitude)
            self.aitor_distance = sum(distances) / len(distances)
            self.report({'INFO'}, 'Aitor distance set to ' + str(self.aitor_distance))

            patch_created = False
            if (len(self.patches) > 0):
                patch_created = True
                for patch in self.patches:
                    patch.delete()
                self.patches = []
                self.frames = []

            for i in range(len(self.lines)):
                if i != self.selected_line:
                    new_total_created_verts = round(self.lines[i]._total_length / self.aitor_distance) - 1
                    self.lines[i].change_vert_number(new_total_created_verts - self.lines[i].total_created_verts)

            if patch_created:
                self.check_frame()

    # -------------------------------------------------------------------------------------------

    def aitor_update_line(self, line):
        if self.aitor_distance != -1:

            new_total_created_verts = round(line._total_length / self.aitor_distance) - 1
            change = new_total_created_verts - line.total_created_verts
            if change != 0:
                patch_created = False
                delete_paches = []
                for patch in self.patches:
                    if patch.has_vert(line.verts[1]):
                        patch_created = True
                        delete_paches.append(patch)

                for patch in delete_paches:
                    self.patches.remove(patch)
                    patch.delete()

                line.change_vert_number(change)
                if patch_created:
                    self.check_frame()

    # -------------------------------------------------------------------------------------------

    def guess_normal(self, patch=None):
        if (len(self.lines) >= 2):
            verts = []
            for line in self.lines:
                if line == self.lines[0]:
                    verts.append(line.verts[0])
                if line != self.lines[-1] or self.lines[0].verts[0] != self.lines[-1].verts[-1]:
                    verts.append(line.verts[-1])

            avg_normal = guess_normal(self.context, verts, self.eagle_eye_ratio, self.active_objects)
            if (avg_normal != None):
                self.normal = avg_normal
                if patch != None:
                    patch.frame.normal = self.normal
                    patch.apply_normal()

                    if patch.border_snap_surface == True:
                        patch.border_snap_surface = False
                        for line in self.lines:
                            line.update_line(self.normal, self.eagle_eye_ratio)
                        patch.border_snap_surface = True
                        patch.smooth()
                        patch.snap_surface()
                        for line in self.lines:
                            line.update_line(self.normal, self.eagle_eye_ratio)
                    else:
                        patch.snap_surface()
                self.context.area.tag_redraw()

    # -------------------------------------------------------------------------------------------

    def set_camera_pivot(self, location):
        if (self.camera_pivot_is_set == False):
            self.camera_pivot_is_set = True
            # cursor = self.context.scene.cursor.location
            # self.context.scene.cursor.location = self.context.active_object.matrix_world @ location
            # bpy.context.space_data.lock_cursor = True
            # self.context.region_data.view_location = self.context.active_object.matrix_world @ location

    # -------------------------------------------------------------------------------------------

    def process_rightclick(self):
        if self.rightmouse_pressed == True and len(self.bm.select_history) > 0:

            ########### TUBE ####################################
            if self.tube:

                valid = True
                if (len(self.lines) > 0 and len(self.lines[0].verts) > 0):
                    for line in self.lines:
                        if self.bm.select_history[-1] in line.verts:
                            valid = False
                            break

                if valid and (self.bm.select_history[-1].is_boundary or self.bm.select_history[-1].is_wire):
                    print("tube process rightclick")
                    self.last_selected = self.bm.select_history[-1]
                    self.protected_verts.append(self.last_selected)

                    if (len(self.last_selected.link_faces) > 0):
                        self.set_normal(self.last_selected.link_faces[0].normal, None)
                    else:
                        hit, hit_normal, view_vector, t = get_ray_hit(self.context, self.active_objects, self.x, self.y)
                        self.set_normal(hit_normal, None)

                    self.line_ini = self.bm.select_history[-1]
                    self.line_end = self.bm.select_history[-1]

                    self.add_lines()

            ########### PATCH ####################################
            else:
                valid = True
                if (len(self.lines) > 0 and len(self.lines[0].verts) > 0):
                    for line in self.lines:
                        for v in line.verts:
                            if self.bm.select_history[-1] == v and v != line.verts[0] and v != line.verts[-1]:
                                valid = False
                                break
                        if not valid:
                            break

                if (valid and self.last_selected != self.bm.select_history[-1] and (
                        self.bm.select_history[-1].is_boundary or self.bm.select_history[-1].is_wire or
                        len(self.bm.select_history[-1].link_edges) == 0 or
                        self.bm.select_history[-1] in [line.verts[0] for line in self.lines])):

                    self.last_selected = self.bm.select_history[-1]
                    # self.protected_verts.append(self.last_selected)

                    if (self.rightmouse_ctrl):
                        self.rightmouse_ctrl = False
                        self.line_ini = self.bm.select_history[-1]
                        self.line_end = None
                        self.active_vert = self.line_ini
                        self.context.area.tag_redraw()
                    else:

                        if (len(self.last_selected.link_faces) > 0):
                            self.set_normal(self.last_selected.link_faces[0].normal, None)
                        else:
                            hit, hit_normal, view_vector, t = get_ray_hit(self.context, self.active_objects, self.x,
                                                                          self.y)
                            self.set_normal(hit_normal, None)

                        if (self.line_ini == None):
                            self.line_ini = self.bm.select_history[-1]
                        elif (self.line_end == None and self.bm.select_history[-1] != self.line_ini):
                            self.line_end = self.bm.select_history[-1]
                        elif (self.line_ini != None and self.line_end != None):
                            self.line_ini = self.line_end
                            self.line_end = self.bm.select_history[-1]

                        self.add_lines()

            self.rightmouse_pressed = False

    # -------------------------------------------------------------------------------------------

    def _process_click(self):

        if (self.mode == MODE_STD):
            cp_hit_detected = False

            region = self.context.region
            rv3d = self.context.region_data

            for line in self.lines:
                for cp in line.control_points:
                    if dot_hit(self.x, self.y,
                               view3d_utils.location_3d_to_region_2d(region, rv3d,
                                                                     self.context.active_object.matrix_world @ cp.co),
                               CONTROL_POINT_SIZE * self.interface_size * 2):
                        self.mode = MODE_MOVE
                        self.selected_control_point = cp
                        cp_hit_detected = True
                        break
                if (cp_hit_detected): break

            if (not cp_hit_detected):
                for line in self.lines:
                    for d in [line.verts[0], line.verts[-1]]:
                        if dot_hit(self.x, self.y, view3d_utils.location_3d_to_region_2d(
                                region, rv3d,
                                self.context.active_object.matrix_world @ d.co),
                                   POINT_SIZE * self.interface_size * 2):
                            self.mode = MODE_MOVE
                            self.selected_dot = d
                            cp_hit_detected = True
                            break
                        if (cp_hit_detected): break

            if not cp_hit_detected:
                self.add_dot()

        # MOUSE MOVE ###################################################################################################
        elif (self.mode == MODE_MOVE):
            if self.snap_type > 0:

                hit, hit_normal, view_vector, t = get_ray_hit(self.context, self.active_objects, self.x, self.y,
                                                              True, None, self.hit_radius * 1000)
            else:
                hit_plane = self.context.scene.cursor.location
                if (self.selected_control_point != None):
                    hit_plane = self.selected_control_point.co
                elif (self.selected_dot != None):
                    hit_plane = self.selected_dot.co

                hit, hit_normal, view_vector, t = get_ray_hit(self.context, self.active_objects, self.x, self.y,
                                                              False, hit_plane)

            # self.set_normal(hit_normal, view_vector)

            moved_vert = None

            # move control point
            if (self.selected_control_point != None):
                if not t and self.snap_type > 0:
                    return
                self.selected_control_point.update_location(hit)
                self.selected_control_point.line.update_line(self.normal, self.eagle_eye_ratio)
                moved_vert = self.selected_control_point.line.verts[1]
                self.aitor_update_line(self.selected_control_point.line)

            # move corner point
            elif (self.selected_dot != None):
                if not t and self.snap_type > 0:
                    return
                old_loc = mathutils.Vector(self.selected_dot.co)
                self.selected_dot.co = hit

                moved_vert = self.selected_dot

                for line in self.lines:
                    if (line.verts[0] == self.selected_dot or line.verts[-1] == self.selected_dot):
                        if self.proportional_edit:
                            oposite = None
                            if (self.selected_dot == line.verts[0]):
                                oposite = line.verts[-1].co
                            elif ((self.selected_dot == line.verts[-1])):
                                oposite = line.verts[0].co

                            v_old = (old_loc - oposite)
                            v_hit = (hit - oposite)
                            m_old = v_old.magnitude
                            m_hit = v_hit.magnitude
                            nv_hit = v_hit.normalized()
                            nv_old = v_old.normalized()

                            angle_old_hit = v_old.angle(v_hit, 0.)
                            axis = v_old.cross(v_hit)

                            for cp in line.control_points:
                                x = cp.co - oposite
                                l = (x.dot(v_old) / v_old.dot(v_old))
                                sl = math.sqrt(abs(l))

                                ratio = m_hit / m_old
                                z = x * ratio
                                zl = x.lerp(z, sl)

                                new_rot_loc = rotate_point(zl, angle_old_hit, axis, mathutils.Vector())

                                cp.update_location(new_rot_loc + oposite)

                    self.aitor_update_line(line)
                    line.update_line(self.normal, self.eagle_eye_ratio)

            if (len(self.patches) > 0):
                for patch in self.patches:
                    if patch.has_vert(moved_vert):
                        patch.smooth()
                        patch.set_center()
                        if self.snap_type == 1:
                            patch.snap_surface()
                        elif self.snap_type == 2:
                            self.snap_points()

            bmesh.update_edit_mesh(self.context.active_object.data)
            self.context.active_object.data.update_tag()
            self.bm.normal_update()

    # -------------------------------------------------------------------------------------------

    def _hover(self):

        self.highlited, self.selected_line, self.selected_face = hover(self.context,
                                                                       self.x,
                                                                       self.y,
                                                                       self.selected_line,
                                                                       self.lines,
                                                                       self.interface_size,
                                                                       self.highlited,
                                                                       self.frames,
                                                                       self.selected_face,
                                                                       self.patches)

        self.context.area.tag_redraw()

    # -------------------------------------------------------------------------------------------

    def add_dot(self):
        if self.tube:
            return

        if self.snap_type > 0:
            hit, hit_normal, view_vector, t = get_ray_hit(self.context, self.active_objects, self.x, self.y)
        else:
            hit, hit_normal, view_vector, t = get_ray_hit(self.context, self.active_objects, self.x, self.y, False,
                                                          self.context.active_object.matrix_world.inverted() @ self.context.scene.cursor.location)

        self.set_normal(hit_normal, view_vector)

        new_vert = self.bm.verts.new(hit)
        self.bm.verts.ensure_lookup_table()
        self.created_verts_buffer.append(new_vert)

        self.was_dot = True
        self.line_ini = None
        self.line_end = None

        # if (len(self.lines) == 0):
        if (self.active_vert != None):
            self.lines.append(PatchLine([self.active_vert, new_vert], False, self.bm, self.context,
                                        self.ini_cp, self.ini_line_verts, self.normal, self.snap_type > 0,
                                        self.eagle_eye_ratio, self.active_objects))
            self.aitor_update_line(self.lines[-1])
            self.undo_stack.append([self.lines[-1], self.active_vert])
            self.hit_radius = set_hit_radius(self.context, self.lines)

        self.active_vert = new_vert
        self.last_selected = self.active_vert

        # if self.first_vert == None:
        #    self.first_vert = self.active_vert

        bmesh.update_edit_mesh(self.context.active_object.data)
        self.context.active_object.data.update_tag()
        self.context.area.tag_redraw()

    # -------------------------------------------------------------------------------------------

    def check_duplicate(self, va, vb):
        for line in self.lines:
            if (va == line.verts[0] and vb == line.verts[-1]) or (va == line.verts[-1] and vb == line.verts[0]):
                return False
        return True

    def add_lines(self):
        # print("Select lines: " + str (self.active) + " -> " +str(self.line_end))

        if self.line_ini != None and self.line_ini == self.line_end:
            ### closed loop line ################################################################
            tmp_line = crawl_border(self.bm, self.lines, self.line_ini, self.line_end)

            bpy.ops.mesh.select_all(action='DESELECT')

            if tmp_line != None and len(tmp_line) > 0:
                delete_lines = []
                for line in self.lines:
                    line_verts_set = set(line.verts)
                    if len(list(set(tmp_line).intersection(line_verts_set))) > 0:
                        delete_lines.append(line)
                for line in delete_lines:
                    self.delete_line(line)

                self.lines.append(PatchLine(tmp_line, True, self.bm, self.context, self.ini_cp, self.ini_line_verts,
                                            self.normal, self.snap_type > 0, self.eagle_eye_ratio, self.active_objects))
                self.aitor_update_line(self.lines[-1])
                self.undo_stack.append([self.lines[-1], self.active_vert])
                self.hit_radius = set_hit_radius(self.context, self.lines)
                self.active_vert = self.line_ini
                if (len(self.line_ini.link_edges) > 0):
                    self.was_dot = False
                self.context.area.tag_redraw()

                # check for other last loop
                for line in reversed(self.lines):
                    if line != self.lines[-1] and line.verts[0] == line.verts[-1]:  # is loop
                        self.unite_loops(line, self.lines[-1])
                        break

        else:
            ### normal line ################################################################
            if self.was_dot and self.check_duplicate(self.active_vert, self.line_ini):
                self.lines.append(
                    PatchLine([self.active_vert, self.line_ini], False, self.bm, self.context, self.ini_cp,
                              self.ini_line_verts, self.normal, self.snap_type > 0, self.eagle_eye_ratio,
                              self.active_objects))
                self.aitor_update_line(self.lines[-1])
                self.undo_stack.append([self.lines[-1], self.active_vert])
                self.hit_radius = set_hit_radius(self.context, self.lines)
                self.active_vert = self.line_ini
                if (len(self.line_ini.link_edges) > 0):
                    self.was_dot = False

            if (self.line_ini != None and self.line_end != None and self.check_duplicate(self.line_ini, self.line_end)):

                if (len(self.line_ini.link_edges) == 0 or len(self.line_end.link_edges) == 0 or self.rightmouse_shift):
                    self.rightmouse_shift = False
                    self.lines.append(
                        PatchLine([self.line_ini, self.line_end], False, self.bm, self.context, self.ini_cp,
                                  self.ini_line_verts, self.normal, self.snap_type > 0, self.eagle_eye_ratio,
                                  self.active_objects))
                    self.aitor_update_line(self.lines[-1])
                    self.undo_stack.append([self.lines[-1], self.active_vert])
                    self.hit_radius = set_hit_radius(self.context, self.lines)
                    self.active_vert = self.line_end
                    self.was_dot = False

                else:
                    tmp_line = crawl_border(self.bm, self.lines, self.line_ini, self.line_end)

                    if (len(tmp_line) > 0):

                        # check if valid
                        valid = True

                        for line in self.lines:
                            common_verts = list(set(tmp_line).intersection(line.verts))

                            if (len(common_verts) > 1):
                                valid = False
                                # print("But not valid!")
                                break

                        if (valid):
                            self.lines.append(PatchLine(tmp_line, True, self.bm, self.context, self.ini_cp,
                                                        self.ini_line_verts, self.normal, self.snap_type > 0,
                                                        self.eagle_eye_ratio, self.active_objects))
                            self.aitor_update_line(self.lines[-1])
                            self.undo_stack.append([self.lines[-1], self.active_vert])
                            self.hit_radius = set_hit_radius(self.context, self.lines)
                            self.was_dot = False
                        else:
                            make_dyn_line = True
                            for line in self.lines:
                                if (self.line_end in line.verts[1:]):
                                    make_dyn_line = False
                                    break

                            if (make_dyn_line):
                                self.lines.append(
                                    PatchLine([self.line_ini, self.line_end], False, self.bm, self.context, self.ini_cp,
                                              self.ini_line_verts, self.normal, self.snap_type > 0,
                                              self.eagle_eye_ratio,
                                              self.active_objects))
                                self.aitor_update_line(self.lines[-1])
                                self.undo_stack.append([self.lines[-1], self.active_vert])
                                self.hit_radius = set_hit_radius(self.context, self.lines)
                                self.active_vert = self.line_end
                            else:
                                self.line_end = self.lines[-1].verts[0]


                    elif (len(tmp_line) == 0):  # connect islands
                        self.lines.append(
                            PatchLine([self.line_ini, self.line_end], False, self.bm, self.context, self.ini_cp,
                                      self.ini_line_verts, self.normal, self.snap_type > 0, self.eagle_eye_ratio,
                                      self.active_objects))
                        self.aitor_update_line(self.lines[-1])
                        self.undo_stack.append([self.lines[-1], self.active_vert])
                        self.hit_radius = set_hit_radius(self.context, self.lines)
                        self.active_vert = self.line_end
                        self.was_dot = False

                self.active_vert = self.line_end
                self.context.area.tag_redraw()

            elif (self.line_ini != None):
                self.active_vert = self.line_ini

            self.check_frame()

            # if (len(self.lines) == 0):
            #    self.set_camera_pivot(self.active_vert.co)

            # if self.first_vert == None:
            #    self.first_vert = self.active_vert

            # if len(self.lines) >= 2:
            #    self.guess_normal()

    # -------------------------------------------------------------------------------------------

    def delete_line(self, line):

        if line not in self.lines:
            return

        delete_paches = []
        for patch in self.patches:
            if patch.has_vert(line.verts[1]):
                delete_paches.append(patch)

        for patch in delete_paches:
            self.patches.remove(patch)
            patch.delete()

        self.lines.remove(line)
        line.delete_line()
        self.selected_line = -1
        self.selected_face = -1
        self.highlited = None
        if len(self.lines) > 0:
            self.active_vert = self.lines[-1].verts[-1]
        else:
            self.active_vert = None

        if len(self.lines) == 0:
            self.reset()
        else:
            self.check_frame()

        self.context.area.tag_redraw()

    # -------------------------------------------------------------------------------------------

    def set_line_vert_length(self):
        if self.aitor_distance == -1:
            for i, line in enumerate(self.lines):
                in_patch = False
                for patch in self.patches:
                    if patch.same_same(line.verts):
                        in_patch = True
                        break

                if line.preexistant and not in_patch:
                    self.lines[(i + 2) % len(self.lines)].set_vert_number(len(line.verts) - 2)

    # -------------------------------------------------------------------------------------------

    def cycle_lines(self):
        if self.selected_face != -1:
            self.create_patches(False, True)

            self._hover()

    # -------------------------------------------------------------------------------------------
    def snap_points(self):
        for line in self.lines:
            if not line.preexistant and not line.verts[0] in self.protected_verts:
                line.verts[0].co = get_snap_point(self.context, self.active_objects, line.verts[0].co, self.normal,
                                                  self.eagle_eye_ratio)

            for cp in line.control_points:
                cp.update_location(
                    get_snap_point(self.context, self.active_objects, cp.co, self.normal, self.eagle_eye_ratio))
            # line.update_line(self.normal, self.eagle_eye_ratio)

        for line in self.lines:
            line.update_line(self.normal, self.eagle_eye_ratio)

        if (len(self.patches) > 0):
            for patch in self.patches:
                patch.frame.normal = self.normal
                patch.smooth()
                patch.snap_surface()
            bmesh.update_edit_mesh(self.context.active_object.data)
            self.context.active_object.data.update_tag()

    # -------------------------------------------------------------------------------------------
    def chordless_cycles(self, edge_list):
        from collections import defaultdict

        graph = defaultdict(list)
        for u, v in edge_list:
            graph[u].append(v)
            graph[v].append(u)

        def is_chordless(cycle):
            cycle_set = set(cycle)
            for i in range(len(cycle)):
                u = cycle[i]
                v = cycle[(i + 1) % len(cycle)]
                for neighbor in graph[u]:
                    if neighbor in cycle_set and neighbor != v and neighbor != cycle[i - 1]:
                        return False
            return True

        def find_cycles(start, path):
            if len(path) > 6:
                return
            last_node = path[-1]
            for neighbor in graph[last_node]:
                if neighbor == start and len(path) > 2:
                    if is_chordless(path):
                        cycle = tuple(sorted(path))
                        if cycle not in seen_cycles:
                            seen_cycles.add(cycle)
                            cycles.append(path[:])
                elif neighbor not in path:
                    path.append(neighbor)
                    find_cycles(start, path)
                    path.pop()

        cycles = []
        seen_cycles = set()
        for node in graph:
            find_cycles(node, [node])
        return cycles

    # -------------------------------------------------------------------------------------------

    def check_frame(self):

        verts = []
        for line in self.lines:
            verts.append(line.verts[0])
            verts.append(line.verts[-1])
        verts = list(set(verts))

        edges = []
        for line in self.lines:
            edges.append([verts.index(line.verts[0]), verts.index(line.verts[-1])])

        loops = self.chordless_cycles(edges)

        # show vertex indices
        '''    
        self.debug = []
        for i, vert in enumerate(verts):
            loc = view3d_utils.location_3d_to_region_2d(self.context.region, self.context.region_data,
                                                    self.context.active_object.matrix_world @ vert.co)
            self.debug.append([loc, i])
        '''

        # Filter loops

        # 1 filter too less than 3 lines loops
        filtered_loops = []
        for loop in loops:
            if len(loop) >= 3:
                filtered_loops.append(loop)
        loops = filtered_loops.copy()
        loops.sort(key=len)

        # 2 non manifold at least 3 border edges
        def get_key(a, b):
            if a < b:
                return str(a) + "_" + str(b)
            else:
                return str(b) + "_" + str(a)

        edges_dict = {}
        for edge in edges:
            key = get_key(edge[0], edge[1])
            if key not in edges_dict.keys():
                edges_dict[key] = 0

        filtered_loops = []

        for loop in loops:
            valid = True
            ln = len(loop)
            for i in range(ln):
                key = get_key(loop[i], loop[(i + 1) % ln])
                edges_dict[key] += 1
                if edges_dict[key] > 2:
                    valid = False
                    edges_dict[key] -= 1
                    break

            a = valid

            if valid:
                border_edges = 0

                for key in edges_dict.keys():
                    if edges_dict[key] < 2:
                        border_edges += 1

                if border_edges < 3:
                    valid = False

            # print(" ", loop, " ", str(a), " ", str(valid))
            if valid:
                filtered_loops.append(loop)
        # print(" ")
        loops = filtered_loops

        # Set frames
        self.frames = []
        for loop in loops:
            frame = PatchFrame()
            for i in range(len(loop)):
                for line in self.lines:
                    if (line.verts[0] == verts[loop[i]] and line.verts[-1] == verts[loop[(i + 1) % len(loop)]]) or \
                            (line.verts[-1] == verts[loop[i]] and line.verts[0] == verts[loop[(i + 1) % len(loop)]]):
                        frame.add(line)
                        break
            self.frames.append(frame)

        # Set line flow
        for frame in self.frames:
            # count = 0
            if frame.lines[0].verts[0] == frame.lines[1].verts[-1] or frame.lines[0].verts[0] == frame.lines[1].verts[
                0]:
                frame.inverted[0] = True
            for i in range(1, len(frame.lines)):
                if frame.inverted[i - 1]:
                    if frame.lines[i - 1].verts[0] == frame.lines[i].verts[-1]:
                        frame.inverted[i] = True
                else:
                    if (frame.lines[i].verts[0] != frame.lines[i - 1].verts[-1]):
                        frame.inverted[i] = True

        self.create_patches(only_cycle=False)

    # -------------------------------------------------------------------------------------------

    def unite_loops(self, firts_loop, second_loop):
        first_loop_normal = mathutils.geometry.normal([v.co for v in firts_loop.verts[:-1]]).normalized()
        second_loop_normal = mathutils.geometry.normal([v.co for v in second_loop.verts[:-1]]).normalized()

        dot = first_loop_normal.dot(second_loop_normal)

        bridge_line = PatchLine([firts_loop.verts[0], second_loop.verts[0]], False, self.bm, self.context, self.ini_cp,
                                self.ini_line_verts, self.normal, self.snap_type > 0, self.eagle_eye_ratio,
                                self.active_objects)
        self.undo_stack.append([bridge_line, self.active_vert])

        index = 0
        for i in range(len(self.lines)):
            if self.lines[i] == second_loop:
                index = i
        self.lines.insert(index, bridge_line)

        frame = PatchFrame()
        frame.add(firts_loop)
        frame.add(bridge_line)
        if dot > 0:
            frame.normal = first_loop_normal.slerp(second_loop_normal, 0.5, first_loop_normal)
            frame.add(second_loop, True)
        else:
            frame.normal = first_loop_normal.slerp(-second_loop_normal, 0.5, first_loop_normal)
            frame.add(second_loop)
        frame.add(bridge_line, True)

        self.frames.append(frame)

        self.create_patches(alter_normal=False)

    # -------------------------------------------------------------------------------------------

    def create_patches(self, alter_normal=True, only_cycle=True):

        if len(self.frames) > 0:
            if not self.tube:
                if (self.snap_type > 0 and alter_normal):
                    p = []
                    for line in self.lines:
                        for v in line.verts:
                            p.append(v)
                    g_normal = guess_normal(self.context, p, self.eagle_eye_ratio, self.active_objects)
                    if (g_normal != None):
                        self.normal = g_normal

                    # adjust normal
                    new_normal = mathutils.geometry.normal([line.verts[0].co for line in self.lines])

                    if new_normal.dot(self.normal) > 0:
                        self.normal = new_normal
                    else:
                        self.normal = -new_normal
                    # end adjust normal

            if only_cycle and self.selected_face != -1:
                # create patches
                protected_verts = [v for v in self.patches[self.selected_face].protected_verts]

                vert_lines = []
                swap = [v for v in self.patches[self.selected_face].root.vert_sides[0]]
                for i in range(len(self.patches[self.selected_face].root.vert_sides) - 1):
                    vert_lines.append([v for v in self.patches[self.selected_face].root.vert_sides[i + 1]])
                vert_lines.append(swap)

                if alter_normal:
                    normal = self.normal
                else:
                    normal = self.patches[self.selected_face].frame.normal

                first_vert = self.patches[self.selected_face].first_vert
                smooth_iter = self.patches[self.selected_face].smooth_iter
                smooth_factor = self.patches[self.selected_face].smooth_factor
                cursor = self.patches[self.selected_face].cursor
                hit_radius = self.patches[self.selected_face].scale

                snap = self.patches[self.selected_face].snap
                eagle_eye_ratio = self.patches[self.selected_face].eagle_eye_ratio
                normal_length = self.patches[self.selected_face].normal_length
                frame = self.patches[self.selected_face].frame
                frame.normal = normal

                alternative = self.patches[self.selected_face].alternative
                if first_vert == vert_lines[0][0]:
                    alternative = (self.alternative + 1) % 10

                self.patches[self.selected_face].delete()
                del self.patches[self.selected_face]

                patch = Patch(self.context, self.bm, frame, vert_lines, first_vert,
                              smooth_iter, smooth_factor,
                              cursor, protected_verts,
                              hit_radius, alternative,
                              snap, eagle_eye_ratio, normal_length,
                              self.active_objects, self.tube, self.border_snap_surface)

                patch.process()
                patch.smooth()
                self.patches.append(patch)

            else:
                if self.context.region_data.view_perspective == "ORTHO":
                    normal_length = view3d_utils.region_2d_to_origin_3d(self.context.region, self.context.region_data,
                                                                        (self.context.region.width / 2,
                                                                         self.context.region.height / 2),
                                                                        clamp=self.hit_radius * 1000)
                    # print("normal_length: ", normal_length)
                else:
                    normal_length = view3d_utils.region_2d_to_origin_3d(self.context.region, self.context.region_data,
                                                                        (self.context.region.width / 2,
                                                                         self.context.region.height / 2))

                vloops = []

                for frame in self.frames:
                    # create patches
                    protected_verts = [v for v in self.protected_verts]
                    for line in frame.lines:
                        protected_verts.append(line.verts[0])
                        if (line.preexistant):
                            for v in line.verts:
                                protected_verts.append(v)

                    vert_lines = []
                    for i in range(len(frame.lines)):
                        vert_lines.append([])
                        if (frame.inverted[i]):
                            for v in reversed(frame.lines[i].verts):
                                vert_lines[-1].append(v)
                        else:
                            for v in frame.lines[i].verts:
                                vert_lines[-1].append(v)

                    vloops.append([v[0] for v in vert_lines])

                    make_patch = True
                    for patch in self.patches:
                        if patch.same_same([v[0] for v in vert_lines]):
                            make_patch = False
                            break

                    if make_patch:

                        if alter_normal:
                            if self.tube:
                                self.smooth_iter = SMOOTH_ITER_TUBE
                                normal = frame.normal
                            else:
                                self.smooth_iter = SMOOTH_ITER
                                normal = self.normal
                                frame.normal = normal
                        else:
                            frame.normal = self.normal

                        patch = Patch(self.context, self.bm, frame, vert_lines, vert_lines[0][0],
                                      self.smooth_iter, self.smooth_factor,
                                      (self.x, self.y), protected_verts,
                                      self.hit_radius, self.alternative, self.snap_type == 1,
                                      self.eagle_eye_ratio, normal_length, self.active_objects,
                                      self.tube, self.border_snap_surface)

                        patch.process()
                        # patch.smooth()
                        self.patches.append(patch)

                garbage = []
                filter_patches = []
                for patch in self.patches:
                    valid = False
                    for l in vloops:
                        if patch.same_same(l):
                            valid = True
                            break
                    if valid:
                        filter_patches.append(patch)
                    else:
                        garbage.append(patch)

                self.patches = filter_patches

                for patch in garbage:
                    patch.delete()

            bmesh.update_edit_mesh(self.context.active_object.data)
            self.context.active_object.data.update_tag()
            self.bm.normal_update()


############################################################################################################

class MdUtilPetikLine(bpy.types.Operator):
    bl_idname = "mdutil.petik_line"
    bl_label = "Petik Line"
    bl_description = "Interactive Control Line"

    interface_size: bpy.props.FloatProperty(name="petik_line_interface_size", default=1.0)
    snap_type: bpy.props.IntProperty(default=1)
    proportional_edit: bpy.props.BoolProperty(default=False)
    hide_ui: bpy.props.IntProperty(name="petik_line_hide_ui", default=1)

    def invoke(self, context, event):

        os.system("cls")

        self.interface_size = 1
        self.mode = MODE_STD

        self.bm = None
        self.lines = []

        self.valid = True

        self.CLICK_MOUSE = 'LEFTMOUSE'
        self.SELECT_MOUSE = 'RIGHTMOUSE'

        self.eagle_eye_ratio = EAGLE_EYE_RATIO_DEFAULT
        self.selected_line = -1
        self.highlited = None
        self.selected_control_point = None

        self.update_tag = False

        context.window_manager.modal_handler_add(self)
        self.active_objects = visible_objects_and_duplis(context)

        self.context = context

        self.rightmouse_pressed = False
        self.lines = []

        self.hit_radius = HIT_RADIUS

        bpy.ops.object.mode_set(mode='EDIT')

        self.bm = bmesh.from_edit_mesh(self.context.active_object.data)

        args = (self, context)
        self.register_handlers(args, context)

        self.create_lines(self.get_selected_vertex_loops())

        for line in self.lines:
            cp_count = math.ceil(len(line.control_points) / 2)
            line.change_control_points(cp_count - len(line.control_points))

        if self.snap_type > 0:
            for line in self.lines:
                self.snap_points(line)

        if context.active_object:
            return {'RUNNING_MODAL'}
        else:
            self.report({'WARNING'}, "No active object, could not finish")
            return {'CANCELLED'}

    def modal(self, context, event):
        if not self.valid:
            self.report({'INFO'}, "No loops!")
            self.end()
            return {'CANCELLED'}

        if event.type == 'MIDDLEMOUSE' or event.alt:
            if self.update_tag:
                self.update()
            # allow navigation
            return {'PASS_THROUGH'}

        elif (event.type == self.CLICK_MOUSE and event.value == 'PRESS'):
            self.x = event.mouse_region_x
            self.y = event.mouse_region_y
            self._process_click()
            if (self.CLICK_MOUSE == 'RIGHTMOUSE' and event.alt):
                return {'PASS_THROUGH'}
            return {'RUNNING_MODAL'}

        elif event.type == self.SELECT_MOUSE and event.value == 'PRESS':
            self.x = event.mouse_region_x
            self.y = event.mouse_region_y
            if (event.ctrl):
                bpy.ops.view3d.select(deselect=True, location=(event.mouse_region_x, event.mouse_region_y))
            else:
                bpy.ops.view3d.select(extend=True, toggle=True, location=(event.mouse_region_x, event.mouse_region_y))

            if (self.SELECT_MOUSE == 'RIGHTMOUSE' and event.alt):
                return {'PASS_THROUGH'}
            return {'RUNNING_MODAL'}

        elif (event.type == 'MOUSEMOVE'):
            self.x = event.mouse_region_x
            self.y = event.mouse_region_y
            if (self.mode == MODE_MOVE):
                self._process_click()
                if self.snap_type == 1:
                    self.snap_points(self.lines[self.selected_line])
            else:
                self._hover()
            return {'PASS_THROUGH'}

        elif (event.type == self.CLICK_MOUSE and event.value == 'RELEASE'):
            self.mode = MODE_STD
            self.selected_control_point = None
            self.selected_dot = None
            self.hit_radius = set_hit_radius(self.context, self.lines)
            return {'RUNNING_MODAL'}

        elif event.type in {'WHEELDOWNMOUSE', 'WHEELUPMOUSE'} or (
                event.type in {'PAGE_UP', 'PAGE_DOWN'} and event.value == 'PRESS') \
                or (event.type in {'HOME', 'END'} and event.value == 'PRESS'):
            if (event.ctrl):
                if (event.type == 'WHEELUPMOUSE' or event.type == 'HOME'):
                    change = 1
                elif (event.type == 'WHEELDOWNMOUSE' or event.type == 'END'):
                    change = -1

                if self.snap_type > 0:
                    self.snap_points(self.lines[self.selected_line])
                if (self.selected_line > -1):
                    self.highlited = None
                    self.lines[self.selected_line].change_control_points(change)
                    self.hit_radius = set_hit_radius(self.context, self.lines)
                    context.area.tag_redraw()
                    return {'RUNNING_MODAL'}


            elif (event.shift):
                if self.selected_line > -1:
                    if (event.type == 'WHEELUPMOUSE' or event.type == 'PAGE_UP' or event.type == 'HOME'):
                        self.lines[self.selected_line].eagle_eye_ratio += EAGLE_EYE_INCREMENT
                    elif (event.type == 'WHEELDOWNMOUSE' or event.type == 'PAGE_DOWN' or event.type == 'END'):
                        self.lines[self.selected_line].eagle_eye_ratio -= EAGLE_EYE_INCREMENT

                    if (self.lines[self.selected_line].eagle_eye_ratio < EAGLE_EYE_INCREMENT):
                        self.lines[self.selected_line].eagle_eye_ratio = EAGLE_EYE_INCREMENT
                    elif (self.lines[self.selected_line].eagle_eye_ratio > 1.0):
                        self.lines[self.selected_line].eagle_eye_ratio = 1.0

                    context.area.tag_redraw()
                return {'RUNNING_MODAL'}

            elif self.highlited:
                if (event.type == 'WHEELUPMOUSE' or event.type == 'PAGE_UP' or event.type == 'HOME'):
                    if self.highlited.handle_length < 0.5:
                        self.highlited.handle_length += 0.05
                elif (event.type == 'WHEELDOWNMOUSE' or event.type == 'PAGE_DOWN' or event.type == 'END'):
                    if self.highlited.handle_length > 0.1:
                        self.highlited.handle_length -= 0.05
                self.lines[self.selected_line].update_line()
                self.snap_points(self.lines[self.selected_line])
                return {'RUNNING_MODAL'}

            return {'PASS_THROUGH'}

        elif (event.type == 'E' and event.value == 'PRESS'):
            if (event.shift):
                for line in self.lines:
                    line.toggle_uniform_spacing()
                    self.context.area.tag_redraw()
            else:
                if self.selected_line != -1:
                    self.lines[self.selected_line].toggle_uniform_spacing()
                    self.context.area.tag_redraw()

            return {'RUNNING_MODAL'}

        elif event.type in {'ONE', 'TWO', 'THREE', 'FOUR', 'FIVE', 'SIX', 'SEVEN', 'EIGHT', 'NINE'}:
            if event.type == 'ONE':
                cp_count = 1
            elif event.type == 'TWO':
                cp_count = 2
            elif event.type == 'THREE':
                cp_count = 3
            elif event.type == 'FOUR':
                cp_count = 4
            elif event.type == 'FIVE':
                cp_count = 5
            elif event.type == 'SIX':
                cp_count = 6
            elif event.type == 'SEVEN':
                cp_count = 7
            elif event.type == 'EIGHT':
                cp_count = 8
            elif event.type == 'NINE':
                cp_count = 9
            if (event.shift):
                for line in self.lines:
                    line.change_control_points(cp_count - len(line.control_points))
            else:
                if self.selected_line > -1:
                    line = self.lines[self.selected_line]
                    line.change_control_points(cp_count - len(line.control_points))

            return {'RUNNING_MODAL'}

        elif (event.type == 'C' and event.value == 'PRESS'):
            # toggle snap
            self.snap_type = (self.snap_type + 1) % 2
            self.update_tag = True
            return {'RUNNING_MODAL'}

        elif (event.type == 'R' and event.value == 'PRESS'):
            lines = self.create_lines(self.get_selected_vertex_loops())
            for line in lines:
                cp_count = math.ceil(len(line.control_points) / 2)
                line.change_control_points(cp_count - len(line.control_points))
            return {'RUNNING_MODAL'}

        elif (event.type == 'V' and event.value == 'PRESS'):
            # project control points
            if (event.shift):
                for line in self.lines:
                    self.snap_points(line, False)
                    self.context.area.tag_redraw()
            else:
                if self.selected_line != -1:
                    self.snap_points(self.lines[self.selected_line], False)
                    self.context.area.tag_redraw()

            return {'RUNNING_MODAL'}

        elif ((event.type == 'NUMPAD_PLUS' or event.type == 'RIGHT_BRACKET') and event.value == 'PRESS'):
            self.interface_size += 0.1
            self.context.area.tag_redraw()
            return {'RUNNING_MODAL'}

        elif ((event.type == 'NUMPAD_MINUS' or event.type == 'LEFT_BRACKET') and event.value == 'PRESS'):
            if self.interface_size > 0.6:
                self.interface_size -= 0.1
            self.context.area.tag_redraw()
            return {'RUNNING_MODAL'}

        elif (event.type == 'P' and event.value == 'PRESS'):
            self.proportional_edit = not self.proportional_edit
            self.context.area.tag_redraw()
            return {'RUNNING_MODAL'}

        elif event.type in {'SPACE', 'ESC', 'Q'}:
            self.end()
            return {'CANCELLED'}

        return {'RUNNING_MODAL'}

    def _process_click(self):
        if (self.mode == MODE_STD):
            cp_hit_detected = False

            region = self.context.region
            rv3d = self.context.region_data

            for line in self.lines:
                for cp in line.control_points:
                    if dot_hit(self.x, self.y,
                               view3d_utils.location_3d_to_region_2d(region, rv3d,
                                                                     self.context.active_object.matrix_world @ cp.co),
                               CONTROL_POINT_SIZE * self.interface_size * 2):
                        self.mode = MODE_MOVE
                        self.selected_control_point = cp
                        cp_hit_detected = True
                        break
                if (cp_hit_detected): break

            if (not cp_hit_detected):
                for line in self.lines:
                    for d in [line.verts[0], line.verts[-1]]:
                        if dot_hit(self.x, self.y, view3d_utils.location_3d_to_region_2d(
                                region, rv3d,
                                self.context.active_object.matrix_world @ d.co),
                                   POINT_SIZE * self.interface_size * 2):
                            self.mode = MODE_MOVE
                            self.selected_dot = d
                            cp_hit_detected = True
                            break
                        if (cp_hit_detected): break



        # MOUSE MOVE ###################################################################################################
        elif (self.mode == MODE_MOVE):
            if self.snap_type > 0:

                hit, hit_normal, view_vector, t = get_ray_hit(self.context, self.active_objects, self.x, self.y,
                                                              True, None, self.hit_radius * 1000)
            else:
                hit_plane = self.context.scene.cursor.location
                if (self.selected_control_point != None):
                    hit_plane = self.selected_control_point.co
                elif (self.selected_dot != None):
                    hit_plane = self.selected_dot.co

                hit, hit_normal, view_vector, t = get_ray_hit(self.context, self.active_objects, self.x, self.y,
                                                              False, hit_plane)

            # self.set_normal(hit_normal, view_vector)

            moved_vert = None

            # move control point
            if (self.selected_control_point != None):
                if not t and self.snap_type > 0:
                    return
                self.selected_control_point.update_location(hit)
                self.selected_control_point.line.update_line()
                moved_vert = self.selected_control_point.line.verts[1]

            # move corner point
            elif (self.selected_dot != None):
                if not t and self.snap_type > 0:
                    return
                old_loc = mathutils.Vector(self.selected_dot.co)
                self.selected_dot.co = hit

                moved_vert = self.selected_dot

                line = self.lines[self.selected_line]
                if (line.verts[0] == self.selected_dot or line.verts[-1] == self.selected_dot):
                    if self.proportional_edit:
                        oposite = None
                        if (self.selected_dot == line.verts[0]):
                            oposite = line.verts[-1].co
                        elif ((self.selected_dot == line.verts[-1])):
                            oposite = line.verts[0].co

                        v_old = (old_loc - oposite)
                        v_hit = (hit - oposite)
                        m_old = v_old.magnitude
                        m_hit = v_hit.magnitude
                        nv_hit = v_hit.normalized()
                        nv_old = v_old.normalized()

                        angle_old_hit = v_old.angle(v_hit, 0.)
                        axis = v_old.cross(v_hit)

                        for cp in line.control_points:
                            x = cp.co - oposite
                            l = (x.dot(v_old) / v_old.dot(v_old))
                            sl = math.sqrt(abs(l))

                            ratio = m_hit / m_old
                            z = x * ratio
                            zl = x.lerp(z, sl)

                            new_rot_loc = rotate_point(zl, angle_old_hit, axis, mathutils.Vector())

                            cp.update_location(new_rot_loc + oposite)

                line.update_line()

            bmesh.update_edit_mesh(self.context.active_object.data)
            self.context.active_object.data.update_tag()
            self.bm.normal_update()

    def _hover(self):

        self.highlited, self.selected_line, selected_face = hover(self.context,
                                                                  self.x,
                                                                  self.y,
                                                                  self.selected_line,
                                                                  self.lines,
                                                                  self.interface_size,
                                                                  self.highlited,
                                                                  [],
                                                                  -1,
                                                                  [])

        self.context.area.tag_redraw()

    def end(self):
        bmesh.update_edit_mesh(self.context.active_object.data)
        self.context.active_object.data.update_tag()
        self.bm = None
        self.context = None
        self.unregister_handlers(self.context)
        print("END")


    def update(self):
        for line in self.lines:
            line.update_line()


    def snap_points(self, line, snap_verts=True):

        for cp in line.control_points:
            cp.update_location(
                get_snap_point(self.context, self.active_objects, cp.co, cp.normal, line.eagle_eye_ratio))
        line.update_line()

        if snap_verts:
            for v_id in range(1, len(line.verts) - 1):
                line.verts[v_id].co = get_snap_point(self.context, self.active_objects, line.verts[v_id].co,
                                                     line.get_vert_normal(v_id), line.eagle_eye_ratio)

        bmesh.update_edit_mesh(self.context.active_object.data)
        self.context.active_object.data.update_tag()

    def get_selected_vertex_loops(self):

        # Ensure the BMesh is updated
        self.bm.verts.ensure_lookup_table()
        self.bm.edges.ensure_lookup_table()

        # Find all selected vertices and edges
        selected_verts = {v for v in self.bm.verts if v.select}
        selected_edges = {e for e in self.bm.edges if e.select}

        if not selected_verts or not selected_edges:
            print("No selected vertices or edges.")
            return []

        loops = []
        used_verts = set()

        open_start_verts = [v for v in selected_verts if sum(1 for e in v.link_edges if e in selected_edges) == 1]
        remaining_verts = [v for v in selected_verts if v.index not in used_verts]

        for start_vert in open_start_verts + remaining_verts:
            if start_vert.index in used_verts:
                continue

            loop = []
            current_vert = start_vert
            previous_edge = None
            valid = True

            while current_vert:

                loop.append(current_vert.index)
                used_verts.add(current_vert.index)

                next_edge = next((e for e in current_vert.link_edges if e in selected_edges and e != previous_edge),
                                 None)
                if not next_edge:
                    break

                next_vert = next_edge.other_vert(current_vert)
                if next_vert.index in used_verts:
                    if next_vert == start_vert and len(loop) > 2:
                        loop.append(start_vert.index)
                    else:
                        valid = False
                    break

                if next_vert in open_start_verts and next_vert != start_vert:
                    loop.append(next_vert.index)
                    used_verts.add(next_vert.index)
                    break

                previous_edge = next_edge
                current_vert = next_vert

            if valid:
                loops.append(loop)

        return loops

    def create_lines(self, loops):
        if loops:
            new_lines = []
            for i, loop in enumerate(loops):

                # print(f"Loop {i}: {loop}")

                # check loop if duplicate
                valid = True
                for index in loop:
                    for line in self.lines:
                        for v in line.verts:
                            if index == v.index:
                                valid = False
                                break
                        if not valid:
                            break
                    if not valid:
                        break

                if valid:
                    # create line
                    if len(loop) > 2:
                        tmp_line = []
                        for v_id in loop:
                            tmp_line.append(self.bm.verts[v_id])

                        self.lines.append(PatchLine(tmp_line, True, self.bm, self.context, 0, 0,
                                                    tmp_line[0].normal, self.snap_type > 0,
                                                    self.eagle_eye_ratio, self.active_objects))

                        new_lines.append(self.lines[-1])
            return new_lines
        else:
            return []

    def draw_callback_2d(self, op, context):

        font_id = 0
        matrix = context.active_object.matrix_world
        offset_x = 55
        offset_y = 40

        blf.enable(font_id, blf.SHADOW)
        blf.shadow(font_id, 3, 0, 0, 0, 1)
        blf.shadow_offset(font_id, 1, -1)

        blf.size(font_id, 10)
        blf.color(font_id, 0.8, 0.8, 0.8, 1.)

        line_size = 14

        for i in range(len(text_line)):
            blf.position(font_id, offset_x, int((30 + i * line_size) * self.interface_size) + offset_y, 0)
            blf.draw(font_id, text_line[i])

        offset = int((len(text_line) * line_size + 65) * self.interface_size)
        blf.position(font_id, offset_x, offset + offset_y, 0)

        if self.snap_type == 0:
            snap_text = 'OFF'
        elif self.snap_type == 1:
            snap_text = 'ON'
        else:
            snap_text = 'Only Lines'
        blf.position(font_id, offset_x, offset + offset_y, 0)
        blf.draw(font_id, "Snap line to surface: <C> " + snap_text)

        blf.position(font_id, offset_x, offset + int(line_size * self.interface_size) + offset_y, 0)
        blf.draw(font_id, "Proportional edit: <P> " + str(self.proportional_edit))

    def draw_callback_3d(self, op, context):

        if self.hide_ui == 2:
            return

        hud(context,
            self.lines,
            None,
            self.interface_size,
            self.highlited,
            [],
            self.selected_line,
            -1, True)

    def register_handlers(self, args, context):
        self.draw_handle_3d = bpy.types.SpaceView3D.draw_handler_add(self.draw_callback_3d, args, 'WINDOW', 'POST_VIEW')
        self.draw_handle_2d = bpy.types.SpaceView3D.draw_handler_add(self.draw_callback_2d, args, 'WINDOW',
                                                                     'POST_PIXEL')

    def unregister_handlers(self, context):
        bpy.types.SpaceView3D.draw_handler_remove(self.draw_handle_3d, "WINDOW")
        bpy.types.SpaceView3D.draw_handler_remove(self.draw_handle_2d, "WINDOW")


############################################################################################################
# utils.py
############################################################################################################

class EdgeContinuityTree:
    def __init__(self, data, parent):
        self.data = data
        self.parent = parent
        self.leafs = []


# _______________________________________________________________________________

def hover(context, x, y, selected_line, lines, interface_size, highlited, frames, selected_face, patches):
    region = context.region
    rv3d = context.region_data
    coord = x, y

    view_vector = view3d_utils.region_2d_to_vector_3d(region, rv3d, coord)
    ray_origin = view3d_utils.region_2d_to_origin_3d(region, rv3d, coord)

    old_selected_line = selected_line

    # check for line hover
    for id, line in enumerate(lines):
        hit = False
        for crv in line.curves:
            for cp in crv:
                hit = dot_hit(x, y,
                              view3d_utils.location_3d_to_region_2d(region, rv3d,
                                                                    context.active_object.matrix_world @ cp),
                              CONTROL_POINT_SIZE * interface_size * 2)
                if hit:
                    selected_line = id
                    break
            if hit:
                break

        if not hit:
            selected_line = -1
        else:
            break

    ####################
    # check for control point hover
    cp_hit_detected = False
    highlited = None
    for line in lines:
        for cp in line.control_points:
            if dot_hit(x, y,
                       view3d_utils.location_3d_to_region_2d(region, rv3d,
                                                             context.active_object.matrix_world @ cp.co),
                       CONTROL_POINT_SIZE * interface_size * 2):
                highlited = cp
                cp_hit_detected = True
                break
        if (cp_hit_detected): break
        else:
            if dot_hit(x, y,
                       view3d_utils.location_3d_to_region_2d(region, rv3d,
                                                             context.active_object.matrix_world @ line.verts[0].co),
                       POINT_SIZE * interface_size * 2):
                cp_hit_detected = True
                highlited = line.verts[0]
                break


    if lines and dot_hit(x, y,
                         view3d_utils.location_3d_to_region_2d(region, rv3d,
                                                               context.active_object.matrix_world @ lines[-1].verts[-1].co),
                         POINT_SIZE * interface_size * 2):
        cp_hit_detected = True
        highlited = lines[-1].verts[-1]

    ####################
    # check for face hover

    if len(frames) == 1:
        selected_face = 0
    else:
        face_hit_detected = False
        for id, patch in enumerate(patches):
            if dot_hit(x, y, view3d_utils.location_3d_to_region_2d(region, rv3d,
                                                                   context.active_object.matrix_world @ patch.center),
                       CONTROL_POINT_SIZE * interface_size * 4):
                selected_face = id
                face_hit_detected = True
                break
        if not face_hit_detected:
            selected_face = -1

    return (highlited, selected_line, selected_face)


# _______________________________________________________________________________

def small_line(context, matrix, co, pixel_radius=5):
    """
    Creates a star-shaped point gizmo with a consistent screen-space size.

    Args:
        context: Blender context
        matrix: Transformation matrix
        co: Point coordinate (local space Vector)
        pixel_radius: Apparent size in pixels (default 5)

    Returns:
        List of 6 transformed world-space points forming 3 crossed lines
    """
    from mathutils import Vector
    import bpy_extras

    # Get the world-space position of the point
    world_co = matrix @ Vector(co)

    # Convert 1 pixel to a 3D world-space length at this point's depth
    region = context.region
    rv3d = context.region_data

    # Project the point to 2D, then unproject a nearby pixel to measure scale
    co_2d = bpy_extras.view3d_utils.location_3d_to_region_2d(region, rv3d, world_co)
    if co_2d is None:
        return []

    co_2d_offset = (co_2d[0] + pixel_radius, co_2d[1])
    world_co_offset = bpy_extras.view3d_utils.region_2d_to_location_3d(region, rv3d, co_2d_offset, world_co)

    # This is now the real world-space length corresponding to pixel_radius pixels
    length = (world_co_offset - world_co).length

    # Build the 3 axes in world space (not local — avoids object scale issues)
    offsetx = Vector((length, 0, 0))
    offsety = Vector((0, length, 0))
    offsetz = Vector((0, 0, length))

    return [
        world_co - offsetx, world_co + offsetx,
        world_co - offsety, world_co + offsety,
        world_co - offsetz, world_co + offsetz,
    ]


def hud(context, lines, active_vert, interface_size, highlited, patches, selected_line, selected_face, line_mode=False):
    matrix = context.active_object.matrix_world

    gpu.state.blend_set('ALPHA')

    shader = gpu.shader.from_builtin('UNIFORM_COLOR')
    shader.bind()

    def draw_point(co, color, pixel_radius=None):
        if pixel_radius is None:
            pixel_radius = POINT_SIZE * interface_size
        pts = small_line(context, matrix, co, pixel_radius)
        if not pts:
            return
        shader.uniform_float("color", color)
        gpu.state.line_width_set(LINE_SIZE * interface_size * 2)
        for i in range(0, 6, 2):
            batch = batch_for_shader(shader, 'LINES', {"pos": [pts[i], pts[i+1]]})
            batch.draw(shader)
        gpu.state.line_width_set(LINE_SIZE * interface_size)

    def draw_control_point(co, color):
        draw_point(co, color, CONTROL_POINT_SIZE * interface_size)

    if (len(lines) == 0 and active_vert != None):
        draw_point(active_vert.co, colors_dots[3])

    for i in range(len(lines)):
        line_verts = []
        for crv in lines[i].curves:
            for v in crv:
                line_verts.append(v)

        batch_line = batch_for_shader(shader, 'LINE_STRIP', {"pos": [matrix @ v for v in line_verts]})

        gpu.state.line_width_set(LINE_SIZE * interface_size)

        if (selected_line == i):
            shader.uniform_float("color", colors_lines[1])
        else:
            shader.uniform_float("color", colors_lines[0])
        batch_line.draw(shader)

        v_start = lines[i].verts[0]
        v_end = lines[i].verts[-1]

        if active_vert == v_start:
            draw_point(v_start.co, colors_dots[6] if highlited == v_start else colors_dots[3])
            draw_point(v_end.co,   colors_dots[4] if highlited == v_end   else colors_dots[0])

        elif active_vert == v_end:
            draw_point(v_end.co,   colors_dots[6] if highlited == v_end   else colors_dots[3])
            draw_point(v_start.co, colors_dots[4] if highlited == v_start else colors_dots[0])

        else:
            draw_point(v_start.co, colors_dots[4] if highlited == v_start else colors_dots[0])
            draw_point(v_end.co,   colors_dots[4] if highlited == v_end   else colors_dots[0])

        for cp in lines[i].control_points:
            draw_control_point(cp.co, colors_dots[1])

        if highlited != None:
            draw_control_point(highlited.co, colors_dots[4])

    for id, patch in enumerate(patches):
        draw_control_point(patch.center, colors_dots[4] if id == selected_face else colors_dots[5])

        nrm_origin = matrix @ patch.center
        normal_global = ((matrix @ patch.frame.normal) - (matrix @ mathutils.Vector())).normalized()
        nrm_tip = get_eagle_eye(context, nrm_origin, normal_global, patch.eagle_eye_ratio)

        batch_normal = batch_for_shader(shader, 'LINE_STRIP', {"pos": [nrm_origin, nrm_tip]})
        shader.uniform_float("color", [1., 1., 1., 0.05])
        batch_normal.draw(shader)

    if line_mode and selected_line > -1:
        line = lines[selected_line]
        nrm_origin = matrix @ line.verts[0].co
        normal_global = ((matrix @ line.normal) - (matrix @ mathutils.Vector())).normalized()
        nrm_tip = get_eagle_eye(context, nrm_origin, normal_global, line.eagle_eye_ratio)

        batch_normal = batch_for_shader(shader, 'LINE_STRIP', {"pos": [nrm_origin, nrm_tip]})
        shader.uniform_float("color", [1., 1., 1., 0.05])
        batch_normal.draw(shader)
# _______________________________________________________________________________


def set_hit_radius_simple(context, lines):
    avg = 0
    for line in lines:
        avg += (context.active_object.matrix_world @ (line.verts[0].co - line.verts[-1].co)).magnitude
    avg /= len(lines)
    return HIT_RADIUS * avg * 0.5


def set_hit_radius(context, lines):
    if (lines != None and len(lines) > 0):
        pt = []
        for line in lines:
            pt.append(context.active_object.matrix_world @ line.verts[0].co)
            for cp in line.control_points:
                pt.append(context.active_object.matrix_world @ cp.co)
        if (len(lines) == 1):
            pt.append(context.active_object.matrix_world @ lines[0].verts[-1].co)
        kd = KDTree(len(pt))
        for i, v in enumerate(pt):
            kd.insert(v, i)
        kd.balance()

        dist = []
        for p in pt:
            n = kd.find_n(p, 2)
            if n is not None:
                dist.append(n[1][2])

        if dist:
            dist.sort()
            d = 0
            for i in range(len(dist)):
                if (dist[i] != 0.0):
                    d = i
                    break
            return HIT_RADIUS * dist[d]
        else:
            return set_hit_radius_simple(context, lines)
    else:
        return  HIT_RADIUS


# _______________________________________________________________________________


def dot_hit(x, y, vec, tolerance):
    return (abs(x - vec[0]) < math.ceil(tolerance / 2)) and abs(y - vec[1]) < math.ceil(tolerance / 2)


# _______________________________________________________________________________

def crawl_border(bm, lines, vert_start, vert_end):
    def intree(node, vert):
        if node.data == vert:
            return True
        else:
            if node.parent != None:
                return intree(node.parent, vert)
            else:
                return False

    def inline(lines, vert):
        for line in lines:
            for v in line.verts[1:-1]:
                if vert == v:
                    return True
        return False

    root = EdgeContinuityTree(vert_start, None)

    lucky_leafs = []

    nodes = [root]

    while (len(nodes) != 0):
        new_nodes = []
        for node in nodes:
            edges = [e for e in node.data.link_edges if e.is_boundary or e.is_wire]
            all_verts = []

            for e in edges:
                all_verts.append(e.verts[0])
                all_verts.append(e.verts[1])

            for v in all_verts:
                if not intree(node, v) and not inline(lines, v):

                    n = EdgeContinuityTree(v, node)
                    node.leafs.append(n)

                    if (v == vert_end):
                        lucky_leafs.append(n)
                    else:
                        new_nodes.append(n)

        nodes = new_nodes

    paths = []
    if (len(lucky_leafs) > 0):
        for lucky_leaf in lucky_leafs:
            line = []
            cursor = lucky_leaf
            while (cursor != root):
                line.append(cursor.data)
                cursor = cursor.parent
            line.append(root.data)
            line_length = 0.
            for vi in range(len(line) - 1):
                line_length += (line[vi].co - line[vi + 1].co).length
            paths.append([list(reversed(line)), line_length])
    else:
        print("No path!")
        return []

    min_index = 0
    min_length = paths[0][1]
    for i in range(len(paths)):
        if min_length > paths[i][1]:
            min_index = i
            min_length = paths[i][1]
    return paths[min_index][0]


# _______________________________________________________________________________

def crawl_straight_line(bm, vert_start, vert_end, tolerance=0.001):
    angle = 0.0
    line = [vert_start]
    exit = 0
    reference_line = vert_end.co - vert_start.co
    while (True):

        edge = [e for e in line[-1].link_edges if e.is_boundary or e.is_wire]
        found = False
        for e in edge:
            next_candidate = [v for v in e.verts if v != line[-1]]
            for next in next_candidate:
                v1 = next.co - line[-1].co

                angle = reference_line.angle(v1, math.pi)

                if (angle < tolerance):
                    found = True
                    line.append(next)
                    break

            if (found): break  # exit for

        exit += 1
        if (not found or line[-1] == vert_end or exit > 1000): break  # exit while

    return line


# _______________________________________________________________________________

def rotate_point(point, angle, axis, pivot):
    R = mathutils.Matrix.Rotation(angle, 4, axis)
    T = mathutils.Matrix.Translation(pivot)
    M = T @ R @ T.inverted()
    return (M @ point)


# _______________________________________________________________________________

def rotate_normal(context, origin_local, x, y):
    region = context.region
    rv3d = context.region_data
    coord = x, y

    view_vector = view3d_utils.region_2d_to_vector_3d(region, rv3d, coord)
    ray_origin = view3d_utils.region_2d_to_origin_3d(region, rv3d, coord)

    origin_global = context.active_object.matrix_world @ origin_local

    target = intersect_line_plane(ray_origin, ray_origin + view_vector, origin_global, -view_vector)
    target_local = context.active_object.matrix_world.inverted() @ target

    return target_local - origin_local


# _______________________________________________________________________________

def guess_normal(context, verts, eagle_eye_ratio, active_objects, normal=None):
    if (len(verts) < 3):
        return None

    matrix = context.active_object.matrix_world

    verts_global = [matrix @ v.co for v in verts]

    midpoint_global = mathutils.Vector()
    for v in verts_global:
        midpoint_global += v
    midpoint_global /= len(verts_global)

    if normal:
        midpoint_normal = normal
    else:
        midpoint_normal = mathutils.geometry.normal(verts_global)

    # offset
    region = context.region
    rv3d = context.region_data

    eye_origin = view3d_utils.region_2d_to_origin_3d(region, rv3d,
                                                     view3d_utils.location_3d_to_region_2d(region, rv3d,
                                                                                           midpoint_global))

    eye_dist = (eye_origin - midpoint_global).magnitude * eagle_eye_ratio
    midpoint_global = midpoint_global + (eye_origin - midpoint_global) * eye_dist
    # end offset

    hit_front = get_best_hit(context, active_objects, midpoint_global,
                             midpoint_normal)  # best_location, best_obj, best_normal
    if hit_front[0] is not None:
        dist_front = (midpoint_global - hit_front[0]).length

    hit_back = get_best_hit(context, active_objects, midpoint_global, -midpoint_normal)
    if hit_back[0] is not None:
        dist_back = (midpoint_global - hit_back[0]).length

    def get_gnormal(hit):
        zero = mathutils.Vector((0, 0, 0))
        gnormal = hit[1].matrix_world @ hit[2]
        gnormal -= hit[1].matrix_world @ zero
        gnormal = matrix.inverted() @ gnormal
        gnormal -= matrix.inverted() @ zero
        return gnormal

    gnormal = None

    if hit_front[0] is not None and hit_back[0] is not None:
        if dist_front < dist_back:
            gnormal = get_gnormal(hit_front)
        else:
            gnormal = get_gnormal(hit_back)
    elif hit_front[0] is not None:
        gnormal = get_gnormal(hit_front)
    elif hit_back[0] is not None:
        gnormal = get_gnormal(hit_back)
    else:
        gnormal = None

    return gnormal


# _______________________________________________________________________________

def get_all_layer_collections(layer_collection, visible):
    layer_collections = []
    for lc in layer_collection.children:
        if (not visible):
            vis = False
        else:
            vis = lc.is_visible
        layer_collections.append([lc, vis])
        if (lc.children != []):
            for c in get_all_layer_collections(lc, vis):
                layer_collections.append(c)
    return layer_collections


def get_local_view_objects(context):
    # Regression: object.local_view_get and object.visible_in_viewport_get() always returns False
    # https://developer.blender.org/T95197

    space_view_3d = context.space_data

    if type(space_view_3d) != bpy.types.SpaceView3D:  # will crash if space_view_3d is None
        raise TypeError(
            f'The context is incorrect. For context.space_data expected a SpaceView3D type, not {type(space_view_3d)}')

    depsgraph = context.evaluated_depsgraph_get()

    if bpy.data.objects and hasattr(bpy.data.objects[0], 'visible_in_viewport_get'):
        return [object for object in bpy.data.objects if
                object.evaluated_get(depsgraph).visible_in_viewport_get(space_view_3d)]
    else:
        return [object for object in bpy.data.objects if object.evaluated_get(depsgraph).local_view_get(space_view_3d)]


def visible_objects_and_duplis(context):
    depsgraph = context.evaluated_depsgraph_get()
    active_objects = []
    for dup in depsgraph.object_instances:
        if dup.is_instance:  # Real dupli instance
            obj = dup.instance_object
            if (obj.type == 'MESH'):
                active_objects.append(bpy.data.objects[obj.name])
        else:  # Usual object
            obj = dup.object
            if (obj.type == 'MESH'):
                active_objects.append(bpy.data.objects[obj.name])

    objs_in_hidden_collections = []
    layer_collections = get_all_layer_collections(context.view_layer.layer_collection, True)
    for lc in layer_collections:
        if (not lc[1]):
            for o in lc[0].collection.objects:
                if (o.type == 'MESH'):
                    objs_in_hidden_collections.append(o)

    return list((set(active_objects) - set(objs_in_hidden_collections)) & set(get_local_view_objects(context)))


# _______________________________________________________________________________

def obj_ray_cast(obj, ray_origin, ray_normal):
    ray_target = ray_origin + ray_normal
    matrix = obj.matrix_world
    matrix_inv = matrix.inverted()
    ray_origin_obj = matrix_inv @ ray_origin
    ray_target_obj = matrix_inv @ ray_target
    ray_direction_obj = ray_target_obj - ray_origin_obj

    success, location, normal, face_index = obj.ray_cast(ray_origin_obj, ray_direction_obj)

    if success:
        return location, normal, face_index, matrix, matrix_inv
    else:
        return None, None, None, None, None


# _______________________________________________________________________________

def get_best_hit(context, active_objects, ray_origin, ray_vector):
    best_length_squared = float("inf")
    best_obj = None
    best_location = None
    best_normal = None

    for obj in active_objects:

        if obj.name != context.active_object.name and len(obj.data.polygons) > 0:
            hit, normal, face_index, matrix, matrix_inv = obj_ray_cast(obj, ray_origin, ray_vector)
            if hit is not None:
                hit_world = matrix @ hit
                length_squared = (hit_world - ray_origin).length_squared
                angle = (matrix @ normal).angle(ray_vector)
                if best_obj is None or (length_squared < best_length_squared and angle > math.pi * 0.5):
                    best_length_squared = length_squared
                    best_obj = obj
                    best_location = hit
                    best_normal = normal

    return best_location, best_obj, best_normal


# _______________________________________________________________________________
'''
def get_closet_surface(context, active_objects, origin):
    best_dist = float("inf")
    best_obj = None
    best_location = None
    best_normal = None

    for obj in active_objects:
        if obj.name != context.active_object.name and len(obj.data.polygons) > 0:
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            bm.transform(obj.matrix_world)  # Apply world transformation
            bm.verts.ensure_lookup_table()

            # Create BVHTree
            bvh_tree = mathutils.bvhtree.BVHTree.FromBMesh(bm)
            bm.free()  # Free the BMesh to save memory

            # Perform the nearest point query
            nearest = bvh_tree.find_nearest(origin)

            if nearest is not None:
                closest_point, normal, index, distance = nearest
                if distance < best_dist:
                    best_obj = obj
                    best_dist = distance
                    best_location = closest_point
                    best_normal = normal

    return best_location, best_obj, best_normal
'''


# _______________________________________________________________________________

def get_eagle_eye(context, point_global, point_normal, eagle_eye_ratio):
    region = context.region
    rv3d = context.region_data

    eye_origin = view3d_utils.region_2d_to_origin_3d(region, rv3d,
                                                     view3d_utils.location_3d_to_region_2d(region, rv3d, point_global))
    eye_dist = (eye_origin - point_global).magnitude * eagle_eye_ratio
    return point_global + (point_normal * eye_dist)


def get_snap_point(context, active_objects, point, point_normal, eagle_eye_ratio, snap=True):
    matrix = context.active_object.matrix_world
    scene = context.scene
    region = context.region
    rv3d = context.region_data

    point_normal = ((matrix @ point_normal) - (matrix @ mathutils.Vector())).normalized()
    point_global = matrix @ point

    if (snap):

        ray_origin = get_eagle_eye(context, point_global, point_normal, eagle_eye_ratio)

        hit, obj, normal = get_best_hit(context, active_objects, ray_origin, -point_normal)

        if (hit != None):
            return context.active_object.matrix_world.inverted() @ obj.matrix_world @ hit
        else:
            coord = view3d_utils.location_3d_to_region_2d(region, rv3d, point_global)
            h = get_ray_hit(context, active_objects, coord[0], coord[1], True, point)
            return h[0]

    else:
        coord = view3d_utils.location_3d_to_region_2d(region, rv3d, point_global)
        h = get_ray_hit(context, active_objects, coord[0], coord[1], False, point)
        return h[0]


# _______________________________________________________________________________

def get_view_normal(context, x, y):
    region = context.region
    rv3d = context.region_data
    coord = x, y
    view_vector = view3d_utils.region_2d_to_vector_3d(region, rv3d, coord)
    return context.active_object.matrix_world.inverted() @ view_vector - (
            context.active_object.matrix_world.inverted() @ mathutils.Vector((0, 0, 0)))


# _______________________________________________________________________________


def get_ray_hit(context, active_objects, x, y, snap=True, hit_plane_origin=None, clamp=1000.):
    region = context.region
    rv3d = context.region_data
    coord = x, y

    view_vector = view3d_utils.region_2d_to_vector_3d(region, rv3d, coord)

    ray_origin1 = mathutils.Vector()
    if context.region_data.view_perspective == "ORTHO":
        ray_origin = view3d_utils.region_2d_to_origin_3d(region, rv3d, coord, clamp=clamp)
    else:
        ray_origin = view3d_utils.region_2d_to_origin_3d(region, rv3d, coord)

    if (snap or hit_plane_origin == None):
        best_location, best_obj, best_normal = get_best_hit(context, active_objects, ray_origin, view_vector)

        if best_obj is not None:
            best_normal = context.active_object.matrix_world.inverted() @ best_obj.matrix_world @ best_normal
            best_normal = best_normal - (
                    context.active_object.matrix_world.inverted() @ best_obj.matrix_world @ mathutils.Vector(
                (0, 0, 0)))

            local_view_vector = context.active_object.matrix_world.inverted() @ view_vector
            local_view_vector = local_view_vector - (
                    context.active_object.matrix_world.inverted() @ mathutils.Vector((0, 0, 0)))

            # retunr best_hit_location, best_hit_normal, view_vector
            return context.active_object.matrix_world.inverted() @ best_obj.matrix_world @ best_location, best_normal, local_view_vector, True
        else:
            best_normal = context.active_object.matrix_world.inverted() @ (-view_vector)
            best_normal = best_normal - (context.active_object.matrix_world.inverted() @ mathutils.Vector((0, 0, 0)))

            local_view_vector = context.active_object.matrix_world.inverted() @ view_vector
            local_view_vector = local_view_vector - (
                    context.active_object.matrix_world.inverted() @ mathutils.Vector((0, 0, 0)))

            return context.active_object.matrix_world.inverted() @ intersect_line_plane(ray_origin,
                                                                                        ray_origin + view_vector,
                                                                                        context.scene.cursor.location,
                                                                                        view_vector), \
                   best_normal, local_view_vector, False

    else:
        hit_plane_loc = context.active_object.matrix_world @ hit_plane_origin

        best_normal = context.active_object.matrix_world.inverted() @ (-view_vector)
        best_normal = best_normal - (context.active_object.matrix_world.inverted() @ mathutils.Vector((0, 0, 0)))

        local_view_vector = context.active_object.matrix_world.inverted() @ view_vector
        local_view_vector = local_view_vector - (
                context.active_object.matrix_world.inverted() @ mathutils.Vector((0, 0, 0)))

        return context.active_object.matrix_world.inverted() @ intersect_line_plane(ray_origin,
                                                                                    ray_origin + view_vector,
                                                                                    hit_plane_loc, view_vector), \
               best_normal, local_view_vector, False


# _______________________________________________________________________________

def get_edge_rings(loop):
    ring = []
    loop = loop.link_loops[0]
    first_loop = loop
    while True:
        loop = loop.link_loop_radial_next.link_loop_next.link_loop_next
        ring.append(loop.edge)
        if loop == first_loop:
            break
    return list(set(ring))


############################################################################################################

def km_operators():
    return (
        "Mesh",
        {"space_type": 'EMPTY', "region_type": 'WINDOW'},
        {"items": [
            ("mdutils.petik", {"type": 'X', "value": 'PRESS', "ctrl": True, "alt": True, "shift": True},
             {"properties": [("wait_for_input", True)],
              "active": True}),
        ]},
    )


def utilities_global_keymaps():
    return [
        km_operators(),
    ]


############################################################################################################


# Preferences
class PetikAddonPreferences(bpy.types.AddonPreferences):
    bl_idname = __name__

    bpy.types.Scene.Info = bpy.props.BoolProperty(default=True)
    switch_Q_Space: BoolProperty(
        name="Switch Q and Space keys in the modal operator",
        default=False,
    )

    def draw(self, context):
        layout = self.layout

        layout.prop(context.scene, "Info", text="Info", icon="QUESTION")
        if context.scene.Info:
            row = layout.row()
            layout.label(
                text="This addon is designed to help fill or create variable side patches in a retopology context.")

        layout.prop(self, "switch_Q_Space")

        wm = context.window_manager
        kc = wm.keyconfigs.user

        layout.label(text="KeyMap:")

        for km_name, km_args, km_content in utilities_global_keymaps():
            km = kc.keymaps.get(km_name)
            if km:
                self.draw_km(kc, km, layout)

    @staticmethod
    def draw_km(kc, km, layout):
        layout.context_pointer_set("keymap", km)

        row = layout.row()
        row.prop(km, "show_expanded_items", text="", emboss=False)
        row.label(text=km.name, text_ctxt=i18n_contexts.id_windowmanager)

        if km.show_expanded_items:
            col = layout.column()

            for kmi in km.keymap_items:
                if "mdutil.multi_petik" in kmi.idname:
                    rna_keymap_ui.draw_kmi(["ADDON", "USER", "DEFAULT"], kc, km, kmi, col, 0)
                if "mdutil.petik_line" in kmi.idname:
                    rna_keymap_ui.draw_kmi(["ADDON", "USER", "DEFAULT"], kc, km, kmi, col, 0)



