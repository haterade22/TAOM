"""Host side: build one candidate in Painter from a spec JSON file (see painter_tile_build.py).

    python tools/painter/painter_tile_run_spec.py <spec.json>
The file holds {"spec": {...}} or the spec itself. Prints the RESULT of the Painter run.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import painter_run  # noqa: E402


def run_spec(spec):
    """Build and export one spec dict in the open Painter project; returns painter_run's exit code."""
    args = {"lib": os.path.join(HERE, "painter_tile_lib.py").replace("\\", "/"), "spec": spec}
    return painter_run.main([os.path.join(HERE, "painter_tile_build.py"), "--args-json", json.dumps(args)])


def main(argv):
    data = json.load(open(argv[0], encoding="utf-8"))
    return run_spec(data.get("spec", data))


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
