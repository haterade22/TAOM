import argparse
import json
from pathlib import Path
import sys
import tempfile
import traceback

import bpy

PACKAGE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PACKAGE / "tools" / "blender"))

from quad_remesh_job import quad_remesh, remesh_in_place


def script_args():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def main():
    parser = argparse.ArgumentParser(description="Remesh the active Blender mesh")
    parser.add_argument("--quads", type=int, default=5000)
    parser.add_argument("--work-dir", type=Path,
                        default=Path(tempfile.gettempdir()) / "QuadRemesherJobs")
    parser.add_argument("--adaptive-size", type=float, default=50.0)
    parser.add_argument("--exact", action="store_true")
    parser.add_argument("--symmetry", default="")
    parser.add_argument("--max-threads", type=int, default=0)
    parser.add_argument("--timeout", type=float, default=900.0)
    parser.add_argument("--new-object", action="store_true")
    args = parser.parse_args(script_args())
    args.work_dir.mkdir(parents=True, exist_ok=True)
    report_path = args.work_dir / "remesh-example-report.json"
    report = {"status": "error"}
    try:
        source = bpy.context.view_layer.objects.active
        if source is None or source.type != "MESH":
            raise RuntimeError("Select one active mesh object before running this example")
        options = {
            "adaptive_size": args.adaptive_size,
            "exact_count": args.exact,
            "symmetry": args.symmetry,
            "max_threads": args.max_threads,
        }
        if args.new_object:
            result = quad_remesh(source, args.quads, str(args.work_dir),
                                 timeout_s=args.timeout, **options)
        else:
            result = source
            remesh_in_place(source, args.quads, str(args.work_dir),
                            timeout_s=args.timeout, **options)
        report.update({
            "status": "ok",
            "object": result.name,
            "vertices": len(result.data.vertices),
            "faces": len(result.data.polygons),
            "blender": bpy.app.version_string,
        })
    except BaseException:
        report["trace"] = traceback.format_exc()
        raise
    finally:
        report_path.write_text(json.dumps(report, indent=1), encoding="utf-8")


if __name__ == "__main__":
    main()
