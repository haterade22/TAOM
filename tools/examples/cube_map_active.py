import argparse
import json
from pathlib import Path
import sys
import tempfile
import traceback

import bpy

PACKAGE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PACKAGE / "tools" / "blender"))

from cube_map_remesh import remesh_in_place, remesh_object


def script_args():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def main():
    parser = argparse.ArgumentParser(description="Remesh the active mesh with the independent engine")
    parser.add_argument("--quads", type=int, default=96)
    parser.add_argument("--work-dir", type=Path,
                        default=Path(tempfile.gettempdir()) / "QuadToolkitJobs")
    parser.add_argument("--new-object", action="store_true")
    parser.add_argument("--output", type=Path, help="Optional result FBX")
    args = parser.parse_args(script_args())
    args.work_dir.mkdir(parents=True, exist_ok=True)
    report_path = args.work_dir / "remesh-example-report.json"
    report = {"status": "error"}
    try:
        source = bpy.context.view_layer.objects.active
        if source is None or source.type != "MESH":
            raise RuntimeError("Select one active mesh object before running this example")
        if args.new_object:
            result, metrics = remesh_object(source, args.quads, str(args.work_dir))
        else:
            result = source
            metrics = remesh_in_place(source, args.quads, str(args.work_dir))
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            for selected in list(bpy.context.selected_objects):
                selected.select_set(False)
            result.select_set(True)
            bpy.context.view_layer.objects.active = result
            bpy.ops.export_scene.fbx(filepath=str(args.output), use_selection=True)
        report.update({
            "status": "ok",
            "object": result.name,
            "blender": bpy.app.version_string,
            "output": str(args.output) if args.output else None,
            "metrics": metrics,
        })
    except BaseException:
        report["trace"] = traceback.format_exc()
        raise
    finally:
        report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
