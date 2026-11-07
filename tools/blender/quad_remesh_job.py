"""Serialized, artifact-preserving Quad Remesher jobs for Blender pipelines.

Exoside engine 1.4.1.3852 accepts the official Blender bridge's exact RetopoSettings.txt,
inputMesh.fbx and retopo.fbx paths but rejects renamed or relocated job files. A cross-process
file lock therefore protects the
official job, previous bridge files are restored afterwards, and each run keeps diagnostic
artifacts in a unique directory under work_dir. The official add-on does not honor this lock,
so do not run it concurrently with this module.

The process protocol is a behavioural port of the GPL-2.0-or-later Blender bridge 1.4.1.
A licensed local engine is required; this module does not bypass activation or provide a
fallback remesher. Pure path, settings, locking and artifact functions are tested without
Blender by tools/tests/test_quad_remesh_job.py.
"""

from contextlib import contextmanager
import math
import os
import shutil
import subprocess
import tempfile
import time

ENGINE_VERSION = "1.4"
NO_PROGRESS_FILE_S = 40.0
BRIDGE_FILES = ("RetopoSettings.txt", "inputMesh.fbx", "retopo.fbx", "progress.txt")


class QuadRemeshError(RuntimeError):
    pass


def engine_path(env=None):
    env = os.environ if env is None else env
    root = env.get("ALLUSERSPROFILE", r"C:\ProgramData")
    return os.path.join(root, "Exoside", "QuadRemesher", "Datas_Blender",
                        "QuadRemesherEngine_" + ENGINE_VERSION, "xremesh.exe")


def require_engine(env=None):
    path = engine_path(env)
    if not os.path.isfile(path):
        raise QuadRemeshError(
            f"Quad Remesher engine not found at {path}. Install and activate the licensed "
            f"Quad Remesher {ENGINE_VERSION} Blender bridge first.")
    return path


def bridge_dir(temp_root=None):
    root = tempfile.gettempdir() if temp_root is None else temp_root
    return os.path.join(root, "Exoside", "QuadRemesher", "Blender")


def settings_text(file_in, file_out, progress_file, target_quads, adaptive_size=50,
                  exact_count=True, use_materials=False, use_normals=False,
                  hard_edges=True, symmetry="", host_app_version=None, max_threads=0):
    if target_quads < 1:
        raise ValueError("target_quads must be at least 1")
    if not 0 <= adaptive_size <= 100:
        raise ValueError("adaptive_size is a percentage, 0 to 100")
    if max_threads < 0:
        raise ValueError("max_threads cannot be negative")
    axes = symmetry.upper()
    if any(axis not in "XYZ" for axis in axes):
        raise ValueError(f"symmetry takes only x, y and z, not {symmetry!r}")
    lines = ["HostApp=Blender"]
    if host_app_version:
        lines.append(f"HostAppVer={host_app_version}")
    lines += [
        f'FileIn="{file_in}"',
        f'FileOut="{file_out}"',
        f'ProgressFile="{progress_file}"',
        f"TargetQuadCount={int(target_quads)}",
        f"CurvatureAdaptivness={adaptive_size:g}",
        f"ExactQuadCount={int(bool(exact_count))}",
        "UseVertexColorMap=False",
        f"UseMaterialIds={int(bool(use_materials))}",
        f"UseIndexedNormals={int(bool(use_normals))}",
        f"AutoDetectHardEdges={int(bool(hard_edges))}",
    ]
    if axes:
        lines += [f"SymAxis={''.join(a for a in 'XYZ' if a in axes)}", "SymLocal=1"]
    if max_threads:
        lines.append(f"MaxThreadCount={int(max_threads)}")
    return "\n".join(lines) + "\n"


def parse_progress(text):
    lines = text.splitlines()
    if not lines:
        return None
    try:
        value = float(lines[0])
    except ValueError:
        return None
    if not math.isfinite(value):
        return None
    return value, (lines[1] if len(lines) > 1 else "")


def _read_progress(path):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            return parse_progress(handle.read())
    except OSError:
        return None


def _check(progress):
    if progress is None:
        return False
    value, message = progress
    if value == 2:
        return True
    if value == -2:
        raise QuadRemeshError(
            "Quad Remesher opened its license dialog. Finish activation in the official "
            "Blender bridge, then run the job again.")
    if value < 0:
        raise QuadRemeshError(f"Quad Remesher engine error {value:g}: {message or 'no message'}")
    return False


def wait_for_engine(proc, progress_path, timeout_s=900.0, poll_s=0.5,
                    sleep=time.sleep, clock=time.monotonic):
    start = clock()
    seen_file = False
    while True:
        progress = _read_progress(progress_path)
        seen_file = seen_file or progress is not None
        if _check(progress):
            return
        if proc.poll() is not None:
            if _check(_read_progress(progress_path)):
                return
            raise QuadRemeshError(
                f"Quad Remesher engine exited with code {proc.returncode} without a result")
        elapsed = clock() - start
        if elapsed >= timeout_s or (not seen_file and elapsed >= NO_PROGRESS_FILE_S):
            proc.terminate()
            raise QuadRemeshError(f"Quad Remesher engine timed out after {elapsed:.0f} s")
        sleep(poll_s)


def reap_process(proc, terminate=False):
    if proc.poll() is not None:
        return
    if terminate:
        proc.terminate()
    try:
        proc.wait(timeout=5)
        return
    except subprocess.TimeoutExpired:
        pass
    if not terminate:
        proc.terminate()
    try:
        proc.wait(timeout=5)
    except subprocess.TimeoutExpired:
        proc.kill()
        proc.wait(timeout=5)


@contextmanager
def bridge_lock(path, timeout_s=30.0, poll_s=0.1, sleep=time.sleep, clock=time.monotonic):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    start = clock()
    with open(path, "a+b") as handle:
        handle.seek(0, os.SEEK_END)
        if handle.tell() == 0:
            handle.write(b"\0")
            handle.flush()
        if os.name == "nt":
            import msvcrt
        else:
            import fcntl
        locked = False
        while not locked:
            try:
                handle.seek(0)
                if os.name == "nt":
                    msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
                else:
                    fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
                locked = True
            except OSError:
                if clock() - start >= timeout_s:
                    raise QuadRemeshError(
                        f"Another Quad Remesher job holds the bridge lock: {path}") from None
                sleep(poll_s)
        try:
            yield
        finally:
            handle.seek(0)
            if os.name == "nt":
                msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(handle.fileno(), fcntl.LOCK_UN)


def make_artifact_dir(work_dir):
    os.makedirs(work_dir, exist_ok=True)
    return tempfile.mkdtemp(prefix="job-", dir=work_dir)


@contextmanager
def preserve_bridge_files(artifact_dir, bridge):
    os.makedirs(artifact_dir, exist_ok=True)
    os.makedirs(bridge, exist_ok=True)
    backup = tempfile.mkdtemp(prefix=".quad-remesh-backup-", dir=bridge)
    previous = []
    prepared = False
    succeeded = False
    try:
        for name in BRIDGE_FILES:
            current = os.path.join(bridge, name)
            if os.path.exists(current):
                shutil.move(current, os.path.join(backup, name))
                previous.append(name)
        prepared = True
        yield bridge
        succeeded = True
    finally:
        try:
            if prepared:
                for name in BRIDGE_FILES:
                    current = os.path.join(bridge, name)
                    if os.path.isfile(current) and (name != "inputMesh.fbx" or not succeeded):
                        shutil.copy2(current, os.path.join(artifact_dir, name))
        finally:
            if prepared:
                for name in BRIDGE_FILES:
                    current = os.path.join(bridge, name)
                    if os.path.exists(current):
                        os.remove(current)
            for name in previous:
                saved = os.path.join(backup, name)
                if os.path.exists(saved):
                    os.replace(saved, os.path.join(bridge, name))
            shutil.rmtree(backup)


def _transfer_mesh_presentation(source, result):
    if len(source.data.materials) == len(result.data.materials):
        for index, material in enumerate(source.data.materials):
            result.data.materials[index] = material
    smooth = {polygon.use_smooth for polygon in source.data.polygons}
    if len(smooth) == 1:
        value = smooth.pop()
        for polygon in result.data.polygons:
            polygon.use_smooth = value


def quad_remesh(obj, target_quads, work_dir, timeout_s=900.0, **options):
    """Remesh into a new object and retain run artifacts in a unique work_dir subdirectory."""
    import bpy

    if obj is None or obj.type != "MESH":
        raise QuadRemeshError("quad_remesh requires one mesh object")
    engine = require_engine()
    artifacts = make_artifact_dir(work_dir)
    bridge = bridge_dir()
    lock = os.path.join(bridge, ".quad-remesh-job.lock")
    with bridge_lock(lock):
        with preserve_bridge_files(artifacts, bridge):
            file_in = os.path.join(bridge, "inputMesh.fbx").replace("\\", "/")
            file_out = os.path.join(bridge, "retopo.fbx").replace("\\", "/")
            progress = os.path.join(bridge, "progress.txt").replace("\\", "/")
            settings = os.path.join(bridge, "RetopoSettings.txt")
            bpy.ops.object.select_all(action="DESELECT")
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.export_scene.fbx(
                filepath=file_in, use_selection=True, bake_anim=False, global_scale=1,
                apply_unit_scale=False, apply_scale_options="FBX_SCALE_NONE",
                use_space_transform=False, axis_forward="-X", axis_up="Z")
            with open(settings, "w", encoding="utf-8", newline="\n") as handle:
                handle.write(settings_text(
                    file_in, file_out, progress, target_quads,
                    host_app_version=bpy.app.version_string, **options))
            proc = subprocess.Popen([engine, "-s", settings])
            failed = True
            try:
                wait_for_engine(proc, progress, timeout_s=timeout_s)
                if not os.path.isfile(file_out):
                    raise QuadRemeshError("Quad Remesher reported success without an output FBX")
                failed = False
            finally:
                reap_process(proc, terminate=failed)
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(
                filepath=file_out, global_scale=1, use_manual_orientation=True,
                axis_forward="-X", axis_up="Z")
            new = [item for item in bpy.data.objects if item not in before and item.type == "MESH"]
            if len(new) != 1:
                raise QuadRemeshError(f"expected one mesh in {file_out}, imported {len(new)}")
            bpy.context.view_layer.update()
            _transfer_mesh_presentation(obj, new[0])
            new[0]["quad_remesh_job"] = artifacts
            return new[0]


def remesh_in_place(obj, target_quads, work_dir, **kwargs):
    """Replace obj's mesh in its own space; UVs are intentionally rebuilt by the caller."""
    import bpy

    remeshed = quad_remesh(obj, target_quads, work_dir, **kwargs)
    mesh = remeshed.data
    mesh.transform(obj.matrix_world.inverted() @ remeshed.matrix_world)
    old = obj.data
    obj.data = mesh
    bpy.data.objects.remove(remeshed, do_unlink=True)
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return mesh
