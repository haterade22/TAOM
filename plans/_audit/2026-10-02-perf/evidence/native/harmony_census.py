"""Static census of TAOM's attribute-declared Harmony patches per engine target (scratch analysis, read-only).

Groups [HarmonyPatch(typeof(T), "M"| nameof(T.M), ...)] class attributes by target T.M and counts the
patch methods each class declares (Prefix/Postfix/Transpiler/Finalizer by attribute or by name).
Manual harmony.Patch(...) calls are listed separately by file (not resolved). Prints a table, most
stacked targets first.
"""
import os
import re
import sys
from collections import defaultdict

root = sys.argv[1]
attr_re = re.compile(r"\[HarmonyPatch\(\s*typeof\(([\w.<>]+)\)\s*,\s*(?:nameof\(\s*[\w.<>]+\.(\w+)\s*\)|\"(\w+)\")", re.S)
cat_re = re.compile(r"\[HarmonyPatchCategory\(\s*\"([^\"]+)\"")
kinds = {"Prefix": "pre", "Postfix": "post", "Transpiler": "trans", "Finalizer": "fin"}
kind_attr_re = re.compile(r"\[Harmony(Prefix|Postfix|Transpiler|Finalizer)\]")
kind_name_re = re.compile(r"static\s+[\w<>\[\],.? ]+\s+(Prefix|Postfix|Transpiler|Finalizer)\s*\(")
manual_re = re.compile(r"\.Patch\(\s*(?:original:|[\w.]+)", re.S)

targets = defaultdict(lambda: {"pre": 0, "post": 0, "trans": 0, "fin": 0, "files": set(), "cats": set()})
manual = []
for dirpath, _, files in os.walk(root):
    for f in files:
        if not f.endswith(".cs"):
            continue
        path = os.path.join(dirpath, f)
        text = open(path, encoding="utf-8", errors="replace").read()
        rel = os.path.relpath(path, root).replace("\\", "/")
        # Split into class chunks: each [HarmonyPatch(typeof(...))] up to the next one.
        matches = list(attr_re.finditer(text))
        for i, m in enumerate(matches):
            end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
            chunk = text[m.start():end]
            tname = m.group(1).split(".")[-1]
            mname = m.group(2) or m.group(3)
            key = f"{tname}.{mname}"
            found = {k for k in kind_attr_re.findall(chunk)} | {k for k in kind_name_re.findall(chunk)}
            for k in found:
                targets[key][kinds[k]] += 1
            targets[key]["files"].add(rel)
            for c in cat_re.findall(chunk):
                targets[key]["cats"].add(c)
        if "harmony.Patch(" in text or "_harmony.Patch(" in text or "Harmony.Patch(" in text:
            manual.append(rel)

rows = sorted(targets.items(), key=lambda kv: -(kv[1]["pre"] + kv[1]["post"] + kv[1]["trans"] + kv[1]["fin"]))
print(f"{len(rows)} attribute-declared targets")
print("target | prefix | postfix | transpiler | finalizer | files")
for key, v in rows:
    total = v["pre"] + v["post"] + v["trans"] + v["fin"]
    if total >= 2:
        print(f"{key} | {v['pre']} | {v['post']} | {v['trans']} | {v['fin']} | {len(v['files'])}: {', '.join(sorted(v['files']))[:220]}")
print(f"\nfiles with manual harmony.Patch calls: {len(manual)}")
for m in sorted(manual):
    print("  " + m)
