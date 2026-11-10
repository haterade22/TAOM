"""Generate a staged garment retopology from the active source and RetopoPlanes collection."""
import argparse
import json
from pathlib import Path
import sys

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from tools.retopo_engine import RetopoSettings, engine  # noqa: E402
from tools.blender.retopo_adapter import combine_objects, mesh_from_blender, mesh_to_blender  # noqa: E402


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, help="New staged .blend path")
    parser.add_argument("--subdiv", type=int, default=0)
    parser.add_argument("--initialize", action="store_true")
    parser.add_argument("--auto-fill", action="store_true")
    parser.add_argument("--thickness", type=float, default=0.0)
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    output = Path(args.output).resolve()
    if output.suffix.lower() != ".blend":
        raise ValueError("output must be a staged .blend path")
    source_path = Path(bpy.data.filepath).resolve() if bpy.data.filepath else None
    if source_path == output:
        raise ValueError("output must not replace the input scene")
    if output.exists() and not args.overwrite:
        raise FileExistsError(f"output exists: {output}; pass --overwrite to replace it")
    source = bpy.context.active_object
    if source is None or source.type != "MESH":
        raise ValueError("the active object must be the source garment")
    collection = bpy.data.collections.get("RetopoPlanes")
    if collection is None:
        raise ValueError("create a guide collection named RetopoPlanes")
    guides = [obj for obj in collection.objects if obj.type == "MESH"]
    if not guides:
        raise ValueError("RetopoPlanes contains no mesh objects")
    settings = RetopoSettings(
        subdiv_reproject=args.subdiv,
        initialize_planes=args.initialize,
        auto_fill=args.auto_fill,
        thickness_enabled=args.thickness > 0,
        thickness=args.thickness,
    )
    result = engine.retopologize(
        mesh_from_blender(source, evaluated=True), combine_objects(guides), settings
    )
    output_object = mesh_to_blender(result, source.name + "_Retopo")
    output_object["engine"] = "TAOM independent UV retopology"
    output.parent.mkdir(parents=True, exist_ok=True)
    report = {
        "status": "ok",
        "source": source.name,
        "guides": len(guides),
        "vertices": len(result.vertices),
        "faces": len(result.faces),
        "uv_name": result.uv_name,
        "output": str(output),
    }
    output.with_name("retopo-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    bpy.ops.wm.save_as_mainfile(filepath=str(output))


if __name__ == "__main__":
    main()
