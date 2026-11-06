"""Write generated XML into a live, unversioned folder without losing what was there.

Used by tools/generate_lothlorien_armor.py and tools/generate_snow_orc_armor.py, which write the live
LOTRLOME_Armory (not in git). It follows the rules in .claude/rules/data-scripts.md: a dry run unless
told to write, a backup before a destructive write (never with an .xml extension, the folder is
globbed), a document that must parse, and a second run that changes nothing.
"""
import shutil
import time
import xml.etree.ElementTree as ET
from pathlib import Path


def sync(out_dir, rendered, apply=False, check=False):
    """Compare `rendered` ({file name: text}) with `out_dir`, and write what differs when `apply`.

    Files are written with CRLF line endings and no BOM. A changed file is first copied to
    `<name>.bak-gen-<stamp>`. `check` writes nothing and returns 1 when any file differs.
    Returns 0 on success, 1 on a difference under `check` or an XML document that does not parse.
    """
    out_dir = Path(out_dir)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    plan = []
    for name, text in rendered.items():
        data = text.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8")
        try:
            ET.fromstring(data)
        except ET.ParseError as exc:
            print(f"ERROR: {name} does not parse ({exc}); nothing written")
            return 1
        path = out_dir / name
        plan.append((path, data, path.read_bytes() if path.exists() else None))

    for path, data, old in plan:
        state = "new" if old is None else ("same" if old == data else "differs")
        print(f"{state:8} {path}")
    changed = [path for path, data, old in plan if old != data]

    if check:
        return 1 if changed else 0
    if not apply:
        print("dry run; pass --apply to write")
        return 0

    out_dir.mkdir(parents=True, exist_ok=True)
    for path, data, old in plan:
        if old == data:
            continue
        if old is not None:
            shutil.copy2(path, path.with_name(f"{path.name}.bak-gen-{stamp}"))
        path.write_bytes(data)
        print(f"wrote {path}")
    return 0
