"""Per-file XML load cost from the engine's rgl log (scratch, read-only).

Usage: rgl_xml_cost.py <rgl_log.txt> <from hh:mm:ss> <to hh:mm:ss>
The engine logs "opening ../../XmlSchemas/<Schema>.xsd" and then "opening <module file>" for each
ModuleData file it loads. For every file this prints the time from its schema line to its own line
(the work done before the file is opened: the merge of what came before) and the time from its own
line to the next line, then totals per schema and per module.
"""
import collections
import re
import sys
from datetime import datetime

path, t0, t1 = sys.argv[1], sys.argv[2], sys.argv[3]
ts_re = re.compile(r"^\[(\d\d:\d\d:\d\d\.\d{3})\] (.*)")
fmt = "%H:%M:%S.%f"
lo = datetime.strptime(t0 + ".000", fmt)
hi = datetime.strptime(t1 + ".999", fmt)
rows = []
for raw in open(path, encoding="utf-8", errors="replace"):
    m = ts_re.match(raw.rstrip("\n"))
    if m:
        ts = datetime.strptime(m.group(1), fmt)
        if lo <= ts <= hi:
            rows.append((ts, m.group(2).strip()))

schema_re = re.compile(r"^opening \.\./\.\./XmlSchemas/(\w+)\.xsd")
file_re = re.compile(r"^opening \.\.\\\.\.\\Modules\\(\w[\w.]*)[/\\](.+)$")
per_schema = collections.defaultdict(lambda: [0, 0.0, 0.0])
per_module = collections.defaultdict(lambda: [0, 0.0, 0.0])
files = []
for i, (ts, text) in enumerate(rows):
    fm = file_re.match(text)
    if not fm:
        continue
    module, rel = fm.group(1), fm.group(2)
    schema, pre = "?", 0.0
    if i > 0:
        sm = schema_re.match(rows[i - 1][1])
        if sm:
            schema = sm.group(1)
            pre = (ts - rows[i - 1][0]).total_seconds()
    post = (rows[i + 1][0] - ts).total_seconds() if i + 1 < len(rows) else 0.0
    files.append((pre + post, pre, post, schema, module, rel))
    per_schema[schema][0] += 1
    per_schema[schema][1] += pre
    per_schema[schema][2] += post
    per_module[module][0] += 1
    per_module[module][1] += pre
    per_module[module][2] += post

print(f"{len(files)} ModuleData files opened between {t0} and {t1}")
print("\nper schema: files, seconds before open (schema to file), seconds after open (file to next line)")
for schema, (n, pre, post) in sorted(per_schema.items(), key=lambda kv: -(kv[1][1] + kv[1][2])):
    print(f"  {schema:28} {n:4d} files  before {pre:7.3f} s  after {post:7.3f} s  total {pre + post:7.3f} s")
print("\nper module:")
for module, (n, pre, post) in sorted(per_module.items(), key=lambda kv: -(kv[1][1] + kv[1][2])):
    print(f"  {module:20} {n:4d} files  before {pre:7.3f} s  after {post:7.3f} s  total {pre + post:7.3f} s")
print("\ntop 15 files by total:")
for total, pre, post, schema, module, rel in sorted(files, reverse=True)[:15]:
    print(f"  {total:6.3f} s (before {pre:5.3f}, after {post:5.3f}) {schema:18} {module}/{rel}")
