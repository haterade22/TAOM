"""List ModuleSounds files that module_sounds.xml does not reference, and registered paths with no file (scratch)."""
import glob
import os
import re
import sys

ROOT = sys.argv[1]
SOUNDS = os.path.join(ROOT, "Main", "_Module", "ModuleSounds")
XML = os.path.join(ROOT, "Main", "_Module", "ModuleData", "module_sounds.xml")
text = open(XML, encoding="utf-8").read()
paths = {m.replace("\\", "/").lower() for m in re.findall(r'path="([^"]+)"', text)}
files = {}
for p in glob.glob(os.path.join(SOUNDS, "**", "*"), recursive=True):
    if os.path.isfile(p):
        files[os.path.relpath(p, SOUNDS).replace("\\", "/").lower()] = os.path.getsize(p)
unref = sorted((k for k in files if k not in paths), key=lambda k: -files[k])
print(f"registered paths {len(paths)}, files {len(files)}, unregistered files {len(unref)}, "
      f"{sum(files[k] for k in unref) / 1048576:.1f} MiB")
for k in unref:
    print(f"  {files[k] / 1048576:6.2f} MiB  {k}")
missing = sorted(p for p in paths if p not in files)
print(f"registered paths with no file: {len(missing)}")
for p in missing[:10]:
    print("  ", p)
