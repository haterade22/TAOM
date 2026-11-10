"""Exercise Petik patch generation, control lines, and modal operators in Blender."""
import importlib
import inspect
import json
import math
from pathlib import Path
import sys
import traceback
from types import SimpleNamespace

import bmesh
import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "petik"))

from Petik import op_md_petik as petik  # noqa: E402
import Petik  # noqa: E402


def make_sides(count):
    bm = bmesh.new()
    sides = []
    divisions = {3: {0: 2}, 5: {0: 2}, 6: {0: 2, 2: 2}}.get(count, {})
    for index in range(count):
        side = []
        count_for_side = divisions.get(index, 1) + 1
        for vertex in range(count_for_side):
            angle = math.tau * (index + vertex / (count_for_side - 1)) / count
            side.append(bm.verts.new((math.cos(angle), math.sin(angle), 0.0)))
        sides.append(side)
    bm.verts.ensure_lookup_table()
    return bm, sides


def patch_case(count, alternative=0):
    bm, sides = make_sides(count)
    out.write_text(json.dumps({"progress": f"sides_{count}_created"}, indent=2), encoding="utf-8")
    frame = petik.PatchFrame()
    frame.normal = petik.mathutils.Vector((0, 0, 1))
    patch = petik.Patch(
        SimpleNamespace(), bm, frame, sides, None, 0, 0.9,
        (0, 0, 0), [], 1.0, alternative, False, 0.1, 0.0, [], False,
        border_snap_surface=False,
    )
    out.write_text(json.dumps({"progress": f"patch_{count}_constructed"}, indent=2), encoding="utf-8")
    patch.process()
    out.write_text(json.dumps({"progress": f"patch_{count}_processed"}, indent=2), encoding="utf-8")
    result = {
        "faces": len(bm.faces),
        "face_sizes": sorted({len(face.verts) for face in bm.faces}),
        "vertices": len(bm.verts),
    }
    assert result["faces"] > 0
    assert result["face_sizes"] == [4], result
    patch.refresh(sides)
    assert len(bm.faces) > 0
    assert all(len(face.verts) == 4 for face in bm.faces)
    patch.delete()
    bm.free()
    return result


def control_line_case():
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.new()
    first = bm.verts.new((0, 0, 0))
    second = bm.verts.new((1, 0, 0))
    original_snap = petik.get_snap_point
    petik.get_snap_point = lambda context, active_objects, point, point_normal, eagle_eye_ratio, snap=True: point
    try:
        context = SimpleNamespace(
            active_object=bpy.context.active_object,
            area=SimpleNamespace(tag_redraw=lambda: None),
        )
        line = petik.PatchLine(
            [first, second], False, bm, context,
            1, 3, first.normal, False, 0.1, [],
        )
        initial = len(line.control_points)
        line.change_control_points(1)
        assert len(line.control_points) == initial + 1
        line.set_vert_number(6)
        assert len(line.verts) == 8
        line.toggle_uniform_spacing()
        assert line._uniform_ratios
        line.delete_line()
    finally:
        petik.get_snap_point = original_snap
    bm.free()
    bpy.ops.object.mode_set(mode="OBJECT")
    return {"initial_control_points": initial, "vertices_after_resize": 8}


def projection_case():
    screen = next(
        screen for screen in bpy.data.screens
        if any(area.type == "VIEW_3D" for area in screen.areas)
    )
    area = next(area for area in screen.areas if area.type == "VIEW_3D")
    region = next(region for region in area.regions if region.type == "WINDOW")
    target = bpy.data.objects.new("PetikSnapTarget", bpy.context.active_object.data.copy())
    bpy.context.scene.collection.objects.link(target)
    with bpy.context.temp_override(screen=screen, area=area, region=region):
        active_objects = petik.visible_objects_and_duplis(bpy.context)
        assert active_objects
        snapped = petik.get_snap_point(
            bpy.context, active_objects, petik.mathutils.Vector((0, 0, 0)),
            petik.mathutils.Vector((0, 0, 1)), 0.1, True,
        )
        assert abs(snapped.z - 1.0) < 0.0001, tuple(snapped)
        hit, obj, normal = petik.get_best_hit(
            bpy.context, active_objects, petik.mathutils.Vector((0, 0, 5)),
            petik.mathutils.Vector((0, 0, -1)),
        )
        assert hit is not None and abs(hit.z - 1.0) < 0.0001
        view_normal = petik.get_view_normal(bpy.context, 0, 0)
        assert view_normal.length > 0.9
        return {
            "visible_meshes": len(active_objects),
            "snapped_z": snapped.z,
            "ray_hit_z": hit.z,
            "view_normal_length": view_normal.length,
        }


def fake_operator(cls):
    operator = SimpleNamespace(
        interface_size=1.0,
        snap_type=1,
        border_snap_surface=True,
        proportional_edit=cls is petik.MdUtilMultiPetik,
        aitor_distance=-1.0,
        tube=False,
        hide_ui=0,
    )
    for name in dir(cls):
        if not name.startswith("__"):
            method = getattr(cls, name)
            if inspect.isfunction(method):
                setattr(operator, name, method.__get__(operator))
    operator.report = lambda level, message: None
    return operator


def multi_line_cases():
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.new()
    sides = []
    for x in (0, 2):
        first = bm.verts.new((x, 0, 0))
        second = bm.verts.new((x + 1, 0, 0))
        sides.append((first, second))
    context = SimpleNamespace(
        active_object=bpy.context.active_object,
        area=SimpleNamespace(tag_redraw=lambda: None),
    )
    original_snap = petik.get_snap_point
    petik.get_snap_point = lambda context, active_objects, point, point_normal, eagle_eye_ratio, snap=True: point
    lines = [
        petik.PatchLine([first, second], False, bm, context, 1, 3, first.normal, False, 0.1, [])
        for first, second in sides
    ]
    operator = fake_operator(petik.MdUtilPetikLine)
    operator.context = context
    operator.lines = lines
    operator.selected_line = -1
    operator.active_objects = [bpy.context.active_object]
    snapped = []
    petik.get_snap_point = lambda context, active_objects, point, point_normal, eagle_eye_ratio, snap=True: snapped.append(point.x) or point
    try:
        for line in lines:
            operator.snap_points(line, snap_verts=False)
    finally:
        petik.get_snap_point = original_snap
    assert snapped == [0.5, 2.5], snapped
    bm.free()

    bm = bmesh.new()
    vertices = [bm.verts.new((x, y, 0)) for x, y in ((0, 0), (1, 0), (1, 1), (0, 1))]
    for index in range(4):
        edge = bm.edges.new((vertices[index], vertices[(index + 1) % 4]))
        edge.select = True
        vertices[index].select = True
    bm.verts.index_update()
    bm.edges.index_update()
    operator.bm = bm
    loops = operator.get_selected_vertex_loops()
    bm.free()
    assert len(loops) == 1 and len(loops[0]) == 5, loops
    assert loops[0][0] == loops[0][-1], loops
    bpy.ops.object.mode_set(mode="OBJECT")
    return {"snapped_lines": snapped, "closed_loop_vertices": len(loops[0])}


def modal_event(event_type, value="PRESS"):
    return SimpleNamespace(
        type=event_type,
        value=value,
        alt=False,
        ctrl=False,
        shift=False,
        mouse_region_x=0,
        mouse_region_y=0,
    )


def modal_cases():
    original_visibility = petik.visible_objects_and_duplis
    petik.visible_objects_and_duplis = lambda context: [bpy.context.active_object]
    context = SimpleNamespace(
        window_manager=SimpleNamespace(modal_handler_add=lambda operator: None),
        preferences=bpy.context.preferences,
        view_layer=bpy.context.view_layer,
        evaluated_depsgraph_get=bpy.context.evaluated_depsgraph_get,
        active_object=bpy.context.active_object,
        scene=bpy.context.scene,
        area=SimpleNamespace(tag_redraw=lambda: None, width=800, height=600),
    )
    multi = fake_operator(petik.MdUtilMultiPetik)
    assert multi.invoke(context, SimpleNamespace()) == {"RUNNING_MODAL"}
    multi.reset()
    assert multi.lines == []
    multi.undo()
    old_size = multi.interface_size
    assert multi.modal(context, modal_event("NUMPAD_PLUS", value="PRESS") ) == {"RUNNING_MODAL"}
    assert multi.interface_size > old_size
    assert multi.modal(context, modal_event("H", value="PRESS") ) == {"RUNNING_MODAL"}
    assert multi.hide_ui == 1
    loose = multi.bm.verts.new((2, 2, 0))
    multi.bm.select_history.clear()
    multi.bm.select_history.add(loose)
    multi.rightmouse_pressed = True
    multi.rightmouse_ctrl = True
    right_click = modal_event("RIGHTMOUSE")
    right_click.mouse_region_x = -1
    right_click.mouse_region_y = -1
    assert multi.modal(context, right_click) == {"RUNNING_MODAL"}
    assert multi.line_ini == loose
    drawn = []
    original_blf = petik.blf
    petik.blf = SimpleNamespace(
        SHADOW=0,
        enable=lambda *args: None,
        shadow=lambda *args: None,
        shadow_offset=lambda *args: None,
        size=lambda *args: None,
        color=lambda *args: None,
        position=lambda *args: None,
        draw=lambda *args: drawn.append(args[1]),
    )
    try:
        multi.hide_ui = 2
        multi.draw_callback_2d(multi, context)
    finally:
        petik.blf = original_blf
    assert "H - hide UI" in drawn
    assert "Ctrl Scroll - modify control points count" in drawn
    assert "Ctrl Right Click - Select active control point" in drawn
    assert multi.modal(context, modal_event("ESC", value="PRESS") ) == {"CANCELLED"}

    preferences = bpy.context.preferences.addons["Petik"].preferences
    preferences.switch_Q_Space = True
    multi = fake_operator(petik.MdUtilMultiPetik)
    assert multi.invoke(context, SimpleNamespace()) == {"RUNNING_MODAL"}
    assert multi.Q == "SPACE"
    assert multi.modal(context, modal_event("SPACE", value="PRESS") ) == {"FINISHED"}
    preferences.switch_Q_Space = False

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_mode(type="EDGE")
    bpy.ops.mesh.select_all(action="DESELECT")
    bm = bmesh.from_edit_mesh(bpy.context.active_object.data)
    for index, edge in enumerate(bm.edges):
        edge.select = index < 3
    bmesh.update_edit_mesh(bpy.context.active_object.data)
    line = fake_operator(petik.MdUtilPetikLine)
    line.snap_type = 0
    assert line.invoke(context, SimpleNamespace()) == {"RUNNING_MODAL"}
    assert line.lines
    old_proportional = line.proportional_edit
    assert line.modal(context, modal_event("P", value="PRESS") ) == {"RUNNING_MODAL"}
    assert line.proportional_edit != old_proportional
    line.selected_line = 0
    old_snap = line.snap_type
    assert line.modal(context, modal_event("C", value="PRESS") ) == {"RUNNING_MODAL"}
    assert line.snap_type != old_snap
    assert line.modal(context, modal_event("E", value="PRESS") ) == {"RUNNING_MODAL"}
    assert line.lines[0]._uniform_ratios
    assert line.modal(context, modal_event("TWO", value="PRESS") ) == {"RUNNING_MODAL"}
    assert len(line.lines[0].control_points) == 2
    assert line.modal(context, modal_event("R", value="PRESS") ) == {"RUNNING_MODAL"}
    line.snap_type = 0
    line.selected_line = 0
    wheel = modal_event("WHEELUPMOUSE")
    wheel.ctrl = True
    assert line.modal(context, wheel) == {"RUNNING_MODAL"}
    assert len(line.lines[0].control_points) == 3
    wheel = modal_event("WHEELDOWNMOUSE")
    wheel.shift = True
    old_ratio = line.lines[0].eagle_eye_ratio
    assert line.modal(context, wheel) == {"RUNNING_MODAL"}
    assert line.lines[0].eagle_eye_ratio < old_ratio
    assert line.modal(context, modal_event("Q", value="PRESS") ) == {"CANCELLED"}
    bpy.ops.object.mode_set(mode="OBJECT")
    petik.visible_objects_and_duplis = original_visibility
    return {
        "multi_modal": True,
        "line_modal": True,
        "line_count": len(line.lines),
        "help_lines": len(drawn),
        "right_click": True,
        "wheel_control_points": 3,
    }


def main():
    expected_keymaps = [
        ("mdutil.multi_petik", "X", True, True, True),
        ("mdutil.petik_line", "Z", True, True, True),
    ]
    bpy.ops.preferences.addon_enable(module="Petik")
    actual_keymaps = sorted(
        (item.idname, item.type, item.ctrl, item.alt, item.shift)
        for _, item in Petik.addon_keymaps
    )
    assert actual_keymaps == expected_keymaps, actual_keymaps
    bpy.ops.preferences.addon_disable(module="Petik")
    importlib.reload(Petik)
    bpy.ops.preferences.addon_enable(module="Petik")
    assert len(Petik.addon_keymaps) == 2
    out.write_text(json.dumps({"progress": "registered"}, indent=2), encoding="utf-8")
    try:
        matrix = {}
        for count in (3, 4, 5, 6):
            matrix[f"sides_{count}"] = patch_case(count)
            out.write_text(json.dumps({"progress": matrix}, indent=2), encoding="utf-8")
        alternatives = patch_case(4, alternative=1)
        out.write_text(json.dumps({"progress": "alternative"}, indent=2), encoding="utf-8")
        control = control_line_case()
        out.write_text(json.dumps({"progress": "control_line"}, indent=2), encoding="utf-8")
        multi_lines = multi_line_cases()
        operator = fake_operator(petik.MdUtilMultiPetik)
        cycles = operator.chordless_cycles([(0, 1), (1, 2), (2, 3), (3, 0), (0, 2)])
        assert any(len(cycle) == 3 for cycle in cycles)
        assert all(len(cycle) < 4 for cycle in cycles)
        out.write_text(json.dumps({"progress": "cycles"}, indent=2), encoding="utf-8")
        projection = projection_case()
        modal = modal_cases()
        return {
            "status": "ok",
            "patch_matrix": matrix,
            "alternative": alternatives,
            "control_line": control,
            "multi_lines": multi_lines,
            "chordless_cycles": len(cycles),
            "projection": projection,
            "modal": modal,
        }
    finally:
        bpy.ops.preferences.addon_disable(module="Petik")


out_dir = Path(__file__).resolve().parents[2] / "TestResults" / "Blender"
out_dir.mkdir(parents=True, exist_ok=True)
out = out_dir / "petik_selftest_report.json"
try:
    report = main()
except BaseException:
    report = {"status": "error", "trace": traceback.format_exc()}
out.write_text(json.dumps(report, indent=2), encoding="utf-8")
