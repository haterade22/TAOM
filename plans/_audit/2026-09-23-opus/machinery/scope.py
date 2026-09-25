"""Build x-review.js args for executed branches (reads git only).

Usage: python scope.py <num:slug:base> [...]  -> prints JSON {"items": [...]} for x-review.js
Branch is improve/<num>-<slug>, worktree E:\\repos\\taom-improve\\wt-<num>, head = branch tip.
Lenses follow deep-review SKILL.md Step 2: 1 for C#/C++ or harness; 2 for C#/C++; 3 for C# or scripts;
4, 5, 6 always; 7 for any XML/XSLT; t (tooling) for scripts under tools/ or .claude/hooks/.
"""
import json
import subprocess
import sys

REPO = r"E:\repos\TAOM"
items = []
for spec in sys.argv[1:]:
    num, slug, base = spec.split(":")
    branch = f"improve/{num}-{slug}"
    wt = rf"E:\repos\taom-improve\wt-{num}"
    head = subprocess.run(["git", "-C", REPO, "rev-parse", "--short", branch], capture_output=True, text=True).stdout.strip()
    names = subprocess.run(["git", "-C", REPO, "diff", "--name-only", f"{base}..{branch}"], capture_output=True, text=True).stdout.split()
    f = {"cs": [], "xml": [], "scripts": [], "harness": [], "docs": []}
    for p in names:
        if p.endswith((".cs", ".cpp", ".h", ".csproj", ".props", ".targets")):
            f["cs"].append(p)
        elif p.endswith((".xml", ".xslt", ".xsd")):
            f["xml"].append(p)
        elif p.startswith(("tools/", ".claude/hooks/")) or p.endswith((".py", ".sh", ".ps1")):
            f["scripts"].append(p)
        elif p.startswith((".claude/", ".ai/", ".codex/", ".github/")) or p in ("CLAUDE.md", "AGENTS.md", ".mcp.json", ".gitignore", ".editorconfig", ".gitattributes"):
            f["harness"].append(p)
        else:
            f["docs"].append(p)
    lenses = {"4", "5", "6"}
    if f["cs"]:
        lenses |= {"1", "2", "3"}
    if f["harness"]:
        lenses.add("1")
    if f["scripts"]:
        lenses |= {"3", "t"}
    if f["xml"]:
        lenses.add("7")
    # A branch slug can differ from its plan's slug (the June ports); the caller then fixes the title.
    plan_lines = subprocess.run(["git", "-C", REPO, "show", f"{branch}:plans/{num}-{slug}.md"], capture_output=True, text=True, encoding="utf-8").stdout.splitlines()
    title = plan_lines[0].lstrip("# ").strip() if plan_lines else slug
    items.append({
        "num": num, "slug": slug, "wt": wt, "branch": branch, "base": base, "head": head, "title": title,
        "lenses": sorted(lenses), "files": f,
        "codexOut": rf"{wt}\docs\reviews\raw\codex-adversarial-{num}-{slug}-2026-09-24.md",
    })
print(json.dumps({"items": items}, ensure_ascii=True))
