"""Author and deliver grooms with the independent TAOM hair engine.

This entry point never imports Hair Tool. It preserves the source groom and uses
TAOM's existing weight, material, morph, preview, LOD, and re-import gates.
"""
import argparse
import json
from pathlib import Path
import sys
import traceback

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent
for folder in (HERE, TOOLS):
    if str(folder) not in sys.path:
        sys.path.insert(0, str(folder))

import hair_tool_pipeline as delivery
from hair_engine import generation
from hair_engine.convert import curves_to_mesh
from hair_engine.model import Hair
from hair_engine.profiles import Profile
from hair_engine_adapter import (blender_to_hair, hair_to_blender, mesh_to_blender)
from hair_engine_operators import _mesh_arrays


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("inspect", "author", "finalize"), required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--profile", choices=tuple(delivery.PROFILES), default="dwarf_hair")
    parser.add_argument("--mesh", help="scalp for author or TAOM Curves object for finalize")
    parser.add_argument("--material")
    parser.add_argument("--reference", action="append", default=[])
    parser.add_argument("--armature")
    parser.add_argument("--head")
    parser.add_argument("--preview", action="store_true")
    parser.add_argument("--overwrite", action="store_true")
    return parser.parse_args(argv)


def author(args):
    import bpy
    source = delivery.selected_source(args.mesh)
    if source.type != "MESH":
        raise ValueError("author requires a scalp mesh")
    settings = delivery.starter_settings(args.profile)
    vertices, faces = _mesh_arrays(source)
    guides = generation.surface_guides(
        vertices, faces, settings["guides"], settings["length"], settings["resolution"], settings["seed"])
    hair = Hair(guides, {"profile": args.profile})
    for strand in hair.strands:
        strand.radius[:] = settings["width"]
    path = delivery.output_file(args, ".author.blend")
    if Path(bpy.data.filepath).resolve() == path:
        raise ValueError("author output must not replace the source blend")
    guide = hair_to_blender(hair, "TAOM_%s_guides" % args.profile)
    guide.matrix_world = source.matrix_world.copy()
    guide.data.surface = source
    if source.data.uv_layers:
        guide.data.surface_uv_map = source.data.uv_layers[0].name
    guide["taom_profile"] = "FLAT"
    guide["taom_width"] = settings["width"]
    bpy.ops.wm.save_as_mainfile(filepath=str(path), check_existing=False)
    return {
        "source": source.name,
        "hair_object": guide.name,
        "output": str(path),
        "starter": settings,
        "object": delivery.object_info(guide),
    }


def finalize(args):
    import add_mesh_lods as lods
    import bpy
    source = delivery.selected_source(args.mesh)
    if source.type != "CURVES":
        raise ValueError("finalize requires an independent TAOM Curves object")
    options = delivery.profile_options(args.profile, args.material, args.reference)
    target_name = options["target"]
    if target_name in bpy.data.objects:
        raise ValueError("an object named %s already exists" % target_name)
    armature = delivery.armature_for(args.armature)
    path = delivery.output_file(args, ".fbx")
    source_name = source.name
    hair = blender_to_hair(source)
    settings = delivery.starter_settings(args.profile)
    profile = Profile(source.get("taom_profile", "FLAT").lower(), source.get("taom_width", settings["width"]),
                      resolution=4 if source.get("taom_profile") == "ROUND" else 2)
    result = curves_to_mesh(hair, default_profile=profile)
    target = mesh_to_blender(result.mesh, target_name)
    target.data.name = target_name
    target.matrix_world = source.matrix_world.copy()
    weight_stats = delivery.assign_delivery(target, options, armature)
    if args.preview and not args.overwrite and (Path(args.out) / "preview").exists():
        raise FileExistsError("preview output exists; choose another --out or pass --overwrite")
    preview = delivery.render_previews(target, args.head, options["family"], args.out) if args.preview else None
    if preview:
        preview["candidates"][0]["note"] = "TAOM independent hair engine preview"
        (Path(args.out) / "preview" / "previews.json").write_text(
            json.dumps(preview, indent=2), encoding="utf-8")
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = target
    lods.export(str(path))
    lods.load(str(path))
    imported = bpy.data.objects.get(target_name)
    if imported is None:
        raise RuntimeError("staged FBX did not re-import %s" % target_name)
    errors = delivery.final_mesh_errors(delivery.object_info(imported), options["material"])
    if errors:
        raise RuntimeError("staged FBX failed delivery checks: " + "; ".join(errors))
    return {
        "source": source_name,
        "output": str(path),
        "profile": options,
        "weights": weight_stats,
        "preview": preview,
        "roundtrip": delivery.object_info(imported),
    }


def main():
    args = parse_args(sys.argv)
    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    report_path = out / "hair-engine-report.json"
    report = {"status": "error", "mode": args.mode, "profile": args.profile,
              "engine": "taom_hair_engine"}
    try:
        if args.mode == "inspect":
            import bpy
            report.update(status="ok", objects=[delivery.object_info(obj) for obj in bpy.context.scene.objects],
                          profiles=sorted(delivery.PROFILES))
        else:
            report.update(author(args) if args.mode == "author" else finalize(args))
            report["status"] = "ok"
    except BaseException:
        report["trace"] = traceback.format_exc()
        report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
        raise
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
