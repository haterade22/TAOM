"""Deterministic linter for the modder handbook chapters under docs/modding/.

Read-only. Reports, per chapter: long dashes, drive-letter paths, dead relative links,
missing skeleton headings on file chapters, recipes without the Check / Takes effect / Code
trailer, AI vocabulary, live-module paths without the reinstall warning, and a BOM.

Usage:
    python tools/lint_handbook.py                 # every chapter under docs/modding
    python tools/lint_handbook.py docs/modding/troops.md
    python tools/lint_handbook.py --json out.json

Exit 1 when any finding exists, 0 when clean. tools/check_handbook_attributes.py covers the
engine-table and example markers; this tool covers everything else the chapter contract asks for.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from dataclasses import asdict, dataclass

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS_DIR = os.path.join('docs', 'modding')

FILE_CHAPTER_HEADINGS = [
    'What this file is',
    'Where it lives and how it is registered',
    'Attributes',
    'Child elements',
    'Worked example',
    'Recipes: Add / Modify / Delete',
    'Gotchas: what fails silently and what crashes',
    'Numbers in this chapter',
    'Read next',
]
FILE_CHAPTER_PREFIXES = (
    'items-', 'troops', 'equipment-rosters', 'npcs-', 'wanderers-', 'lords-', 'skill-sets',
    'body-properties', 'cultures', 'party-templates', 'clans', 'kingdoms', 'settlements',
    'banners-', 'strings-', 'configs-',
)
LIVE_MODULES = ('TAOM_Map/', 'LOTRLOME_Armory/', 'SandBoxCore/', 'SandBox/', 'Native/', 'CustomBattle/')
REINSTALL_WARNING = 'reinstall reverts'
AI_TELLS = [
    r'\bdelve\b', r'\blandscape\b', r'\btestament\b', r'\badditionally\b', r'\bin order to\b',
    r'\bit is important to note\b', r"\blet's dive\b", r'\bcrucial\b', r'\brobust\b',
    r'\bseamless(?:ly)?\b', r'\bleverage\b', r'\bhope this helps\b',
]
LINK_RE = re.compile(r'\[[^\]]*\]\(([^)\s#]+)(?:#[^)]*)?\)')
TRAILER_RE = {
    'Check': re.compile(r'^\s*\**Check:?\**:?\s*\S', re.M),
    'Takes effect': re.compile(r'^\s*\**Takes effect:?\**:?\s*\S', re.M),
    'Code': re.compile(r'^\s*\**Code:?\**:?\s*\S', re.M),
}


@dataclass
class Finding:
    file: str
    line: int
    code: str
    message: str


def _strip_code(text: str) -> tuple[str, list[int]]:
    """Return text with fenced code blocks blanked (line count preserved) and the fenced line numbers."""
    out = []
    fenced = []
    in_fence = False
    for i, line in enumerate(text.split('\n'), 1):
        if line.lstrip().startswith('```'):
            in_fence = not in_fence
            out.append('')
            fenced.append(i)
            continue
        if in_fence:
            out.append('')
            fenced.append(i)
        else:
            out.append(line)
    return '\n'.join(out), fenced


def lint_file(path: str, repo_root: str = REPO_ROOT) -> list[Finding]:
    rel = os.path.relpath(path, repo_root).replace('\\', '/')
    raw = open(path, 'rb').read()
    findings: list[Finding] = []
    if raw.startswith(b'\xef\xbb\xbf'):
        findings.append(Finding(rel, 1, 'BOM', 'file starts with a UTF-8 BOM; the contract asks for none'))
    # No CRLF check. This repo sets core.autocrlf=true, so git stores LF and writes CRLF into the
    # working tree on checkout, by design (see .gitattributes). Flagging the worktree failed every
    # chapter the moment it was committed (2026-09-05). The stored blob is what matters, and git
    # guarantees that one is LF.
    text = raw.decode('utf-8-sig').replace('\r\n', '\n')
    lines = text.split('\n')
    prose, fenced = _strip_code(text)
    prose_lines = prose.split('\n')

    for i, line in enumerate(lines, 1):
        if '—' in line or '–' in line:
            if 'lint-allow-dash' not in line:
                findings.append(Finding(rel, i, 'LONG_DASH', 'em or en dash in prose'))
        if re.search(r'\b[A-Za-z]:[\\/]', line):
            findings.append(Finding(rel, i, 'DRIVE_PATH', 'drive-letter path; use a repo-relative link or a module-relative backticked path'))

    for i, line in enumerate(prose_lines, 1):
        for target in LINK_RE.findall(line):
            if re.match(r'^[a-z]+://', target) or target.startswith('mailto:'):
                continue
            full = os.path.normpath(os.path.join(os.path.dirname(path), target))
            if not os.path.exists(full):
                findings.append(Finding(rel, i, 'DEAD_LINK', f'link target does not exist: {target}'))
        low = line.lower()
        for pat in AI_TELLS:
            if re.search(pat, low):
                findings.append(Finding(rel, i, 'AI_TELL', f'AI vocabulary or filler: {pat.strip(chr(92)+"b")}'))
                break

    base = os.path.basename(path)
    if base.startswith(FILE_CHAPTER_PREFIXES) and base != 'README.md':
        h2 = [re.sub(r'^##\s+', '', l).strip() for l in lines if re.match(r'^##\s+', l)]
        for want in FILE_CHAPTER_HEADINGS:
            if want not in h2:
                findings.append(Finding(rel, 1, 'MISSING_HEADING', f'file chapter lacks the skeleton heading: {want}'))

    # Recipe trailer: every ### Add / ### Modify / ### Delete (and any ### Recipe-like heading) section
    # must carry Check / Takes effect / Code lines before the next heading of level <= 3.
    heading_idx = [(i, l) for i, l in enumerate(lines) if re.match(r'^#{1,3}\s+', l)]
    for n, (i, l) in enumerate(heading_idx):
        if not re.match(r'^###\s+(Add|Modify|Delete)\b', l):
            continue
        end = heading_idx[n + 1][0] if n + 1 < len(heading_idx) else len(lines)
        section = '\n'.join(lines[i:end])
        missing = [k for k, rx in TRAILER_RE.items() if not rx.search(section)]
        if missing:
            findings.append(Finding(rel, i + 1, 'RECIPE_TRAILER', f'recipe "{l.strip("# ")}" lacks: {", ".join(missing)}'))

    if any(m in text for m in LIVE_MODULES) and REINSTALL_WARNING not in text:
        first = next((i for i, l in enumerate(lines, 1) if any(m in l for m in LIVE_MODULES)), 1)
        findings.append(Finding(rel, first, 'LIVE_PATH_NO_WARNING', 'names a live game-install module path but never states that a module reinstall reverts hand edits'))
    return findings


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('paths', nargs='*', help='chapter files; default every .md under docs/modding')
    ap.add_argument('--json', help='write findings to this path as JSON')
    args = ap.parse_args(argv)
    paths = args.paths or sorted(
        os.path.join(REPO_ROOT, DOCS_DIR, f) for f in os.listdir(os.path.join(REPO_ROOT, DOCS_DIR)) if f.endswith('.md'))
    all_findings: list[Finding] = []
    for p in paths:
        all_findings.extend(lint_file(os.path.abspath(p)))
    for f in all_findings:
        print(f'{f.code:22s} {f.file}:{f.line} {f.message}')
    print(f'{len(paths)} chapter(s), {len(all_findings)} finding(s)')
    if args.json:
        with open(args.json, 'w', encoding='utf-8') as fh:
            json.dump([asdict(f) for f in all_findings], fh, indent=1)
    return 1 if all_findings else 0


if __name__ == '__main__':
    sys.exit(main())
