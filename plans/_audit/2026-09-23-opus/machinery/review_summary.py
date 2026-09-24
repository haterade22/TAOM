"""Summarize an x-review workflow output (read-only). Usage: python review_summary.py <task-output>"""
import json
import re
import sys

o = json.load(open(sys.argv[1], encoding="utf-8"))["result"]
for r in o:
    if r.get("error"):
        print(r["num"], "ERROR", r["error"][:300])
        continue
    lead = r.get("lead") or ""
    conv = r.get("convergence") or ""
    sec = r.get("second") or ""
    verdict = re.search(r"VERDICT:?\s*\**\s*([A-Z ]+)", lead)
    commit = re.findall(r"`([0-9a-f]{8})`", lead)
    cstat = re.search(r"CONVERGENCE:\s*(CLEAN|DEFECTS\s*\d*)", conv)
    print(f"== {r['num']} lenses={','.join(r['lenses'])} verdict={verdict.group(1).strip() if verdict else '?'} lead_commits={commit[:3]}")
    print(f"   convergence={cstat.group(1) if cstat else 'unparsed'}; second_pass={'yes' if sec else 'no'}")
    m = re.search(r"\*\*Needs Mike\*\*(.*?)(\n\*\*[A-Z]|\Z)", lead, re.S)
    if m:
        print("   NEEDS MIKE:", " ".join(m.group(1).split())[:700])
    if sec:
        print("   SECOND:", " ".join(sec.split())[:400])
