"""Write a /review-codex Phase 2 prompt for one improve branch (reads git only; writes the prompt file).

Usage: python make_codex_prompt.py <num> <slug> <base> <branch> <worktree>
Writes <worktree>/docs/reviews/codex-adversarial-<num>-<slug>-2026-09-24.prompt.md and prints its path.
Codex runs from E:\\repos\\TAOM (trusted path) and reads the branch through git refs, never the
main working tree (another session's uncommitted files live there).
"""
import os
import re
import subprocess
import sys

num, slug, base, branch, wt = sys.argv[1:6]
# Optional 6th arg: a file-name tag for a second review on the same branch (e.g. "-decisions"),
# so it never overwrites the first review's prompt or output.
tag = sys.argv[6] if len(sys.argv) > 6 else ""
REPO = r"E:\repos\TAOM"


def git(*a):
    return subprocess.run(["git", "-C", REPO, *a], capture_output=True, text=True, encoding="utf-8").stdout


files = [f for f in git("diff", "--name-only", f"{base}..{branch}").splitlines() if f.strip()]
stat = git("diff", "--stat", f"{base}..{branch}").strip().splitlines()[-1:] or [""]
plan_path = f"plans/{num}-{slug}.md"
plan = git("show", f"{branch}:{plan_path}")
title = (re.search(r"^# Plan \d+: (.+)$", plan, re.M) or re.search(r"^# (.+)$", plan, re.M))
title = title.group(1).strip() if title else slug
stop = re.search(r"^## STOP conditions\s*\n(.*?)(?=^## |\Z)", plan, re.M | re.S)
stop_items = [l.strip("-* ").strip() for l in (stop.group(1).splitlines() if stop else []) if l.strip().startswith(("-", "*"))]
why = re.search(r"^## Why this matters\s*\n(.*?)(?=^## |\Z)", plan, re.M | re.S)
why = " ".join(why.group(1).split()) if why else ""

groups = {"C#": [], "Tests": [], "XML/XSLT/JSON data": [], "Scripts and hooks": [], "Harness and docs": [], "Other": []}
for f in files:
    if f.startswith("TAOM.Tests/") or "/tests/" in f:
        groups["Tests"].append(f)
    elif f.endswith((".cs", ".csproj", ".props", ".targets")):
        groups["C#"].append(f)
    elif f.endswith((".xml", ".xslt", ".json")) and "ModuleData" in f:
        groups["XML/XSLT/JSON data"].append(f)
    elif f.startswith(("tools/", ".claude/hooks/", ".github/")) or f.endswith((".py", ".sh", ".ps1", ".yml")):
        groups["Scripts and hooks"].append(f)
    elif f.startswith((".claude/", "docs/", "plans/", ".ai/", ".codex/")) or f in ("AGENTS.md", "CLAUDE.md", "README.md", "CHANGELOG.md", ".mcp.json", ".gitignore"):
        groups["Harness and docs"].append(f)
    else:
        groups["Other"].append(f)

L = []
L.append(f"# Codex adversarial review: plan {num} ({slug}), branch {branch}")
L.append("")
L.append(f"Feature: {title}. {why}")
L.append("")
if tag:
    L.append(f"THIS IS A SECOND REVIEW. The branch was already reviewed; the diff {base}..{branch} is the maintainer-decisions follow-up applied on top of that review. The decisions it implements are listed in the '## Maintainer decisions applied' section of git show {branch}:docs/reviews/deep-review-{num}-{slug}-2026-09-24.md. Review this diff only; earlier commits are in scope only where the new diff changes their behaviour.")
    L.append("")
L.append("HOW TO READ THE CODE (important): you are running in E:\\repos\\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:")
L.append(f"- The change: git diff {base}..{branch}")
L.append(f"- Any file as the branch has it: git show {branch}:<path>")
L.append(f"- The base for comparison: git show {base}:<path>")
L.append("Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.")
L.append("")
L.append("TAOM ID CHEATSHEET:")
L.append("Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa")
L.append("Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar")
L.append("Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale")
L.append("NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).")
L.append("")
L.append("READ FIRST:")
L.append(f"- The plan the change implements: git show {branch}:{plan_path} (intent, scope, STOP conditions)")
L.append("- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)")
L.append("- Any feature doc under docs/features/ the diff touches (git show on the branch)")
L.append("- Engine behaviour: the decompile dump at E:\\Decompiled_Bannerlord\\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)")
L.append("")
L.append("KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):")
n = 1
for s in stop_items[:6]:
    L.append(f"{n}. From the plan's STOP conditions, did the change hit or mishandle this risk? {s}")
    n += 1
L.append(f"{n}. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?")
L.append(f"{n + 1}. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.")
L.append(f"{n + 2}. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.")
L.append(f"{n + 3}. Dead or no-op code introduced by the change; a gate that can never fire.")
L.append("")
L.append(f"CHANGED FILES ({stat[0].strip()}):")
for g, fs in groups.items():
    if fs:
        L.append(f"{g}:")
        for f in fs:
            L.append(f"- {f}")
L.append("")
L.append("REQUIRED SECTIONS:")
L.append("1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.")
L.append("2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).")
L.append("3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.")
L.append("4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.")
L.append("")
L.append("QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.")
L.append("")
L.append("PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.")
L.append("")
L.append("OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW")

out_dir = os.path.join(wt, "docs", "reviews")
os.makedirs(os.path.join(out_dir, "raw"), exist_ok=True)
path = os.path.join(out_dir, f"codex-adversarial-{num}-{slug}{tag}-2026-09-24.prompt.md")
with open(path, "w", encoding="utf-8", newline="\n") as fh:
    fh.write("\n".join(L) + "\n")
print(path)
