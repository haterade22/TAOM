import re, os, xml.sax.saxutils as su

ROOT = "."
CONFIG = "Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml"
TEMPLATES = [
    "Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs",
    "Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs",
    "Main/Features/LotrIssues/Templates/CombatLotrIssue.cs",
]
OUT = "Main/_Module/ModuleData/taom_lotr_issue_strings.xml"

keys = {}  # key -> default text, insertion-ordered

# Config: text_key attribute values of form {=KEY}default
cfg = open(os.path.join(ROOT, CONFIG), encoding="utf-8").read()
attr = re.compile(r'(?:title_key|description_key|brief_key|accept_key|explanation_key|solution_accept_key|task_key)="([^"]*)"')
for m in attr.finditer(cfg):
    mm = re.match(r'\{=([A-Za-z0-9_]+)\}(.*)$', m.group(1), re.S)
    if mm and mm.group(1) not in keys:
        keys[mm.group(1)] = mm.group(2)

# Templates: C# string literals "{=KEY}default"
lit = re.compile(r'"\{=([A-Za-z0-9_]+)\}((?:[^"\\]|\\.)*)"')
for tf in TEMPLATES:
    cs = open(os.path.join(ROOT, tf), encoding="utf-8").read()
    for m in lit.finditer(cs):
        key = m.group(1)
        default = m.group(2).replace('\\"', '"')
        if key not in keys and default.strip():
            keys[key] = default

lines = [
    '<?xml version="1.0" encoding="utf-8"?>',
    '<strings>',
    '  <!-- TAOM LOTR custom-issue player-facing text. English source-of-truth for the 12-language',
    '       pipeline. Defaults also embed inline in taom_lotr_issues.xml so text renders pre-translation.',
    '       Harvested by tools/_harvest_lotr_issue_strings.py. -->',
]
for k, v in keys.items():
    esc = su.escape(v, {'"': '&quot;'})
    lines.append(f'  <string id="{k}" text="{{={k}}}{esc}" />')
lines.append('</strings>')
open(os.path.join(ROOT, OUT), "w", encoding="utf-8").write("\n".join(lines) + "\n")
print(f"wrote {OUT} with {len(keys)} keys")
