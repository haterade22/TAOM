#!/usr/bin/env python3
"""Member-level body diff of every engine member TAOM binds, old decompile versus new.

After a Bannerlord engine bump the old and new engine are decompiled (ilspycmd, one .cs per
assembly) into <root>/{_shipping_build,_modules_build}<suffix>/. A signature check proves a bound
member still exists; it does not prove the body still does what the patch assumed. This tool diffs
the BODY of every bound member and writes one .diff per member that changed.

Bound members come from three snapshot files plus one decompile sweep:

- patch-targets.md: the engine target of every Harmony patch (a row may hold two targets
  separated by ' ; ').
- gamemodel-bases.md: every method a Taom*Model overrides, taken on the model's base type. A model
  whose base is itself a TAOM type is resolved to its first engine ancestor, first through the
  model's own listing, then by reading `class X : Base` in the TAOM source (--taom-src, default
  Main/ and Dependencies/ of this repo). When that fails every member of the model is reported
  UNRESOLVED taom-base-unresolved, never skipped.
- reflection-sites.md, Category B: type plus member name of every gated reflection site.
- Every 'Deserialize(MBObjectManager' method in the decompiled assemblies (the XML loaders),
  matched by type.

Row accounting: every table row of an input file ends up parsed or counted unparsed, and the tool
prints 'rows_in_file=N parsed=N unparsed=N' per file. A silently dropped row is how the v1.5.4
body diff lost 27 generic-arity targets (RCA finding F4).

Text normalization (on by default, --no-normalize to disable): the v1.5.3 era decompile went
through a lossy pipe that turned non-ASCII into '?'. Before comparing, the 'Latest version is' nag
line is dropped and every non-ASCII char, and every '?' inside a string literal or comment, becomes
one placeholder, so a pure encoding difference does not count as a change.

Usage for the 1.5.4 to 1.5.5 bump (new decompile in the unsuffixed folders):
    python tools/engine_body_diff.py --old-suffix _v1.5.4 \\
        --out E:\\Decompiled_Bannerlord\\_diff_1.5.4_to_1.5.5\\bound_members

Output: <out>/<Type>.<Member>.diff per changed member and <out>/summary.txt (also printed).
Exit 0 when every row parsed (changed rows are information, not failure), 1 when any row is
unparsed, 2 on bad input (missing folder or snapshot file).

Limits: text based, not a parser. It assumes ilspycmd layout (tab indentation, one namespace
line per file, direct members one tab inside their type). Overloads of a member are diffed
together. A reflection type written with a leading ellipsis is matched by suffix. A member not
declared on the listed type is looked up along the decompiled base chain (first indexed class
base, up to depth 10, same namespace preferred) and the declaring type is reported.

Procedure: .claude/skills/engine-bump/SKILL.md Phase 3.
"""
from __future__ import annotations

import argparse
import difflib
import re
import sys
from collections import defaultdict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_ROOT = r"E:\Decompiled_Bannerlord"
DEFAULT_SNAPSHOT = REPO_ROOT / "docs" / "reference" / "taleworlds-api-snapshot"
BUILD_DIRS = ("_shipping_build", "_modules_build")

NAG = re.compile(r"^Latest version is")
STR = re.compile(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
PLACEHOLDER = "\u00a4"
NON_ASCII = re.compile(r"[^\x00-\x7f]")
ARITY = re.compile(r"`\d+")
SEPARATOR_ROW = re.compile(r"^\|[\s:|-]+$")
TYPE_DECL = re.compile(r"^(\s*)(?:[\w\[\]]+\s+)*(?:class|struct|interface)\s+(\w+)")
LOADER = re.compile(r"\bDeserialize\s*\(\s*MBObjectManager\b")
BACKING = re.compile(r"^<(\w+)>k__BackingField$")


class BadInput(Exception):
    """A missing folder or snapshot file; the tool exits 2."""


def normalize_line(line: str) -> str:
    """Make a pure encoding difference invisible: see the module docstring."""
    out = []
    pos = 0
    for m in STR.finditer(line):
        out.append(_code_part(line[pos:m.start()]))
        out.append(NON_ASCII.sub(PLACEHOLDER, m.group(0)).replace("?", PLACEHOLDER))
        pos = m.end()
    out.append(_code_part(line[pos:]))
    return re.sub(PLACEHOLDER + "+", PLACEHOLDER, "".join(out))


def _code_part(s: str) -> str:
    s = NON_ASCII.sub(PLACEHOLDER, s)
    cut = s.find("//")
    if cut >= 0:
        s = s[:cut] + s[cut:].replace("?", PLACEHOLDER)
    return s


def load_assemblies(root: Path, suffix: str, normalize: bool) -> dict[str, list[str]]:
    texts: dict[str, list[str]] = {}
    for sub in BUILD_DIRS:
        d = root / (sub + suffix)
        if not d.is_dir():
            continue
        for f in sorted(d.glob("*.cs")):
            raw = f.read_text(encoding="utf-8-sig", errors="replace").splitlines()
            lines = [ln for ln in raw if not NAG.match(ln)]
            if normalize:
                lines = [normalize_line(ln) for ln in lines]
            texts[sub + "/" + f.name] = lines
    return texts


def strip_strings(s: str) -> str:
    return STR.sub('""', s.split("//")[0] if '"' not in s else s)


def brace_block(lines: list[str], start: int) -> int:
    """Index (inclusive) of the last line of the brace block opening at or after start."""
    depth = 0
    seen = False
    for i in range(start, len(lines)):
        s = strip_strings(lines[i])
        for ch in s:
            if ch == "{":
                depth += 1
                seen = True
            elif ch == "}":
                depth -= 1
                if seen and depth == 0:
                    return i
        if not seen and s.rstrip().endswith(";"):
            return i  # field, abstract, extern or expression-bodied member
    return len(lines) - 1


def index_types(texts: dict[str, list[str]]) -> dict[str, list[tuple[str, int, int]]]:
    """Namespace-qualified type name (nested joined by '.') -> [(file, start, end)]."""
    idx: dict[str, list[tuple[str, int, int]]] = defaultdict(list)
    for fname, lines in texts.items():
        ns = ""
        stack: list[tuple[int, str]] = []
        for i, line in enumerate(lines):
            m = re.match(r"^namespace\s+([\w.]+)", line)
            if m:
                ns = m.group(1)
                stack = []
                continue
            m = TYPE_DECL.match(line)
            if m and not line.strip().startswith("//"):
                indent = len(m.group(1))
                while stack and stack[-1][0] >= indent:
                    stack.pop()
                stack.append((indent, m.group(2)))
                chain = ".".join(n for _, n in stack)
                idx[ns + "." + chain if ns else chain].append((fname, i, brace_block(lines, i)))
    return idx


def members(lines: list[str], start: int, end: int, name: str, must: str = "") -> list[str]:
    """Bodies of members called `name` declared directly in the type block [start, end].

    `must` is an extra regex the declaration line has to match (used to pick one overload family).
    """
    tindent = len(lines[start]) - len(lines[start].lstrip("\t"))
    want = "\t" * (tindent + 1)
    esc = re.escape(name)
    prop = r"\b%s\b\s*(\{|=>|=|;|$)"
    call = r"\b%s\s*(<[^>]*>)?\s*\("
    if name == ".ctor":
        tname = re.search(r"(?:class|struct)\s+(\w+)", lines[start]).group(1)
        patterns = [re.compile(r"\b" + re.escape(tname) + r"\s*\(")]
    elif name.startswith(("get_", "set_")):
        patterns = [re.compile(prop % re.escape(name[4:])), re.compile(call % esc)]
    else:
        patterns = [re.compile(call % esc), re.compile(prop % esc)]
    must_re = re.compile(must) if must else None
    for pat in patterns:
        out: list[str] = []
        i = start + 1
        while i < end:
            ln = lines[i]
            if (ln.startswith(want) and not ln.startswith(want + "\t") and pat.search(ln)
                    and not ln.strip().startswith(("//", "[")) and (must_re is None or must_re.search(ln))):
                e = brace_block(lines, i)
                out.append("\n".join(lines[i:e + 1]))
                i = e + 1
                continue
            i += 1
        if out:
            return out
    return []


# ---------------------------------------------------------------------------------------------
# Snapshot parsing


class Row:
    """One bound member: a type (or a type suffix), a member name, and where it came from."""

    def __init__(self, typ: str, member: str, tag: str, suffix: bool = False, must: str = "",
                 reason: str = ""):
        self.reason = reason  # non-empty: already known unresolved, with this reason
        self.typ = typ
        self.member = member
        self.tag = tag
        self.suffix = suffix
        self.must = must


def clean_type(t: str) -> str:
    return ARITY.sub("", t).replace("+", ".")


def table_rows(text: str) -> list[str]:
    """Every table row of `text` except header rows and separator rows."""
    lines = text.splitlines()
    skip = set()
    for i, ln in enumerate(lines):
        if SEPARATOR_ROW.match(ln.strip()):
            skip.add(i)
            if i > 0:
                skip.add(i - 1)
    return [ln.strip() for i, ln in enumerate(lines) if ln.startswith("|") and i not in skip]


def parse_target(target: str):
    """'Ns.Type.RetType Name(params)' -> (type, name), or None."""
    mm = re.match(r"^(\S+)\s+(\S+?)\(", target.strip())
    if not mm or "." not in mm.group(1):
        return None
    left, meth = mm.groups()
    return clean_type(left.rsplit(".", 1)[0]), meth


def parse_patch_targets(text: str, rows: list[Row], unparsed: list[str]) -> tuple[int, int]:
    total = parsed = 0
    for line in table_rows(text):
        total += 1
        cells = [c.strip() for c in line.strip("|").split(" | ")]
        if len(cells) < 2 or not (cells[0].startswith("`") and cells[1].startswith("`")
                                  and cells[0].endswith("`") and cells[1].endswith("`")):
            unparsed.append(line)
            continue
        patch = cells[0][1:-1]
        parts = [p for p in re.split(r"\s+;\s+", cells[1][1:-1]) if p.strip()]
        found = [parse_target(p) for p in parts]
        if not found or any(f is None for f in found):
            unparsed.append(line)
            continue
        parsed += 1
        for typ, meth in found:
            rows.append(Row(typ, meth, "patch " + patch.rsplit(".", 1)[-1]))
    return total, parsed


def parse_reflection(text: str, rows: list[Row], unparsed: list[str]) -> tuple[int, int]:
    m = re.search(r"^## Category B\b.*?$(.*?)(?=^## |\Z)", text, re.S | re.M)
    if not m:
        raise BadInput("reflection-sites.md has no '## Category B' section")
    total = parsed = 0
    for line in table_rows(m.group(1)):
        total += 1
        cells = [c.strip() for c in line.strip("|").split(" | ")]
        if len(cells) < 2:
            unparsed.append(line)
            continue
        typ = cells[0].strip("` \t")
        member = BACKING.sub(r"\1", cells[1].strip("` \t"))
        member = member.split("(")[0].strip()
        if not typ or not member or not re.match(r"^[\w.`+\u2026]+$", typ) or not re.match(r"^\w+$", member):
            unparsed.append(line)
            continue
        parsed += 1
        suffix = typ.startswith("\u2026")
        rows.append(Row(clean_type(typ.lstrip("\u2026")), member, "reflection", suffix=suffix))
    return total, parsed


def parse_models(text: str, rows: list[Row], unparsed: list[str], taom_src: dict[str, str]) -> tuple[int, int]:
    models: dict[str, dict] = {}
    cur = None
    total = parsed = 0
    for line in text.splitlines():
        m = re.match(r"^## (\w+) : (\w+)", line)
        if m:
            cur = models.setdefault(m.group(1), {"base": None, "methods": []})
            continue
        if line.startswith("## "):
            cur = None
            continue
        m = re.match(r"^`Base: ([\w.`]+)`", line)
        if m and cur is not None:
            cur["base"] = m.group(1)
            continue
        if line.startswith("- `") and cur is not None:
            total += 1
            mm = re.match(r"^- `\S+ (\w+)\(", line)
            if mm and cur["base"]:
                parsed += 1
                cur["methods"].append(mm.group(1))
            else:
                unparsed.append(line.strip())
    for name, info in models.items():
        base = info["base"]
        simple = None  # an engine type's simple name found through the TAOM source
        seen = set()
        while base and base.startswith("TAOM.") and base not in seen:
            seen.add(base)
            parent = models.get(base.rsplit(".", 1)[-1])
            if parent is None:
                simple = taom_engine_base(base.rsplit(".", 1)[-1], taom_src)
                base = None
                break
            base = parent["base"]
        for meth in info["methods"]:
            if base and not base.startswith("TAOM."):
                rows.append(Row(clean_type(base), meth, "model " + name))
            elif simple:
                rows.append(Row(simple, meth, "model " + name, suffix=True))
            else:
                rows.append(Row(info["base"] or "?", meth, "model " + name, reason="taom-base-unresolved"))
    return total, parsed


def scan_taom_source(dirs: list[Path]) -> dict[str, str]:
    """Class name -> simple name of its first base, for every `class X : Base` in the TAOM source."""
    decl = re.compile(r"^[ \t]*(?:\w+[ \t]+)*class[ \t]+(\w+)(?:<[^>\n]*>)?[ \t]*:[ \t]*([^\n{]+)", re.M)
    out: dict[str, str] = {}
    for d in dirs:
        for f in Path(d).rglob("*.cs"):
            for m in decl.finditer(f.read_text(encoding="utf-8-sig", errors="replace")):
                first = first_base(m.group(2))
                if first:
                    out.setdefault(m.group(1), first)
    return out


def taom_engine_base(name: str, taom_src: dict[str, str]) -> str | None:
    """First ancestor of TAOM class `name` that is not itself a TAOM class, or None."""
    seen = set()
    while name in taom_src and name not in seen:
        seen.add(name)
        name = taom_src[name]
    return None if name in seen or not seen else name


def first_base(bases: str) -> str | None:
    """Simple name of the first entry of a base list ('A<B, C>.D, IFoo' -> 'D')."""
    bases = re.split(r"\s+where\b", bases)[0]
    while re.search(r"<[^<>]*>", bases):
        bases = re.sub(r"<[^<>]*>", "", bases)
    first = bases.split(",")[0].strip()
    return first.rsplit(".", 1)[-1] or None


# ---------------------------------------------------------------------------------------------
# Comparison


def resolve_candidates(row: Row, keys: set[str]) -> list[str]:
    if not row.suffix:
        return [row.typ]
    tail = "." + row.typ
    return sorted(k for k in keys if k == row.typ or k.endswith(tail))


def clean_index(idx: dict) -> dict:
    out: dict[str, list] = defaultdict(list)
    for k, v in idx.items():
        out[clean_type(k)].extend(v)
    return out


def loader_rows(texts: dict[str, list[str]], idx: dict) -> set[str]:
    """Names of the types declaring a Deserialize(MBObjectManager ...) method."""
    spans: dict[str, list[tuple[int, int, str]]] = defaultdict(list)
    for full, locs in idx.items():
        for f, s, e in locs:
            spans[f].append((s, e, full))
    found = set()
    for fname, lines in texts.items():
        for i, ln in enumerate(lines):
            if LOADER.search(ln):
                inner = [sp for sp in spans[fname] if sp[0] < i <= sp[1]]
                if inner:
                    found.add(max(inner, key=lambda sp: sp[0])[2])
    return found


def compare(rows: list[Row], old_texts, new_texts, out_dir: Path):
    old, new = Side(old_texts), Side(new_texts)
    oi, ni = old.idx, new.idx
    for typ in loader_rows(old_texts, oi) | loader_rows(new_texts, ni):
        rows.append(Row(typ, "Deserialize", "deserialize", must=r"Deserialize\s*\(\s*MBObjectManager"))
    keys = set(oi) | set(ni)
    seen = set()
    same = 0
    changed: list[tuple] = []
    missing: list[tuple] = []
    inherited: list[tuple] = []
    for row in rows:
        if row.reason:
            seen.add((row.typ, row.member))
            missing.append((row.typ, row.member, row.tag, row.reason))
            continue
        results = []
        cands = resolve_candidates(row, keys)
        for typ in cands:
            key = (typ, row.member)
            if key in seen:
                continue
            seen.add(key)
            results.append((typ, _compare_one(typ, row, old, new)))
        if not cands:
            key = (row.typ, row.member)
            if key not in seen:
                seen.add(key)
                missing.append((row.typ, row.member, row.tag, "type-both"))
            continue
        if row.suffix and any(r[1][0] in ("same", "changed") for r in results):
            results = [r for r in results if r[1][0] in ("same", "changed")]
        for typ, (kind, a, b, decl) in results:
            if kind in ("same", "changed") and decl != typ:
                inherited.append((typ, row.member, row.tag, decl))
            if kind == "same":
                same += 1
            elif kind == "changed":
                d = list(difflib.unified_diff("\n\n".join(a).splitlines(), "\n\n".join(b).splitlines(),
                                              "v_old", "v_new", lineterm="", n=2))
                size = sum(1 for ln in d if ln[:1] in "+-" and not ln.startswith(("+++", "---")))
                fn = re.sub(r"[^\w.]+", "_", typ + "." + row.member)[:150] + ".diff"
                out_dir.mkdir(parents=True, exist_ok=True)
                (out_dir / fn).write_text(f"# {typ}.{row.member}  ({row.tag})\n" + "\n".join(d) + "\n",
                                          encoding="utf-8")
                changed.append((typ, row.member, row.tag, size, fn, decl))
            else:
                missing.append((typ, row.member, row.tag, kind))
    return seen, same, changed, missing, inherited


class Side:
    """One decompile: its texts, its type index, and a simple-name lookup for base resolution."""

    def __init__(self, texts):
        self.texts = texts
        self.idx = clean_index(index_types(texts))
        self.simple: dict[str, list[str]] = defaultdict(list)
        for k in self.idx:
            self.simple[k.rsplit(".", 1)[-1]].append(k)

    def decl_line(self, typ: str) -> str:
        f, s, _ = self.idx[typ][0]
        return self.texts[f][s]

    def base_of(self, typ: str) -> str | None:
        """Full name of the first indexed class base of `typ`, same namespace preferred."""
        decl = re.search(r"(?:class|struct)\s+\w+(?:<[^>]*>)?\s*:\s*(.+?)\s*$", self.decl_line(typ))
        if not decl:
            return None
        text = re.split(r"\s+where\b", decl.group(1))[0]
        while re.search(r"<[^<>]*>", text):
            text = re.sub(r"<[^<>]*>", "", text)
        ns = typ.rsplit(".", 1)[0]
        for part in text.split(","):
            cands = [k for k in sorted(self.simple.get(part.strip().rsplit(".", 1)[-1], []))
                     if k != typ and " interface " not in " " + self.decl_line(k)]
            if cands:
                return next((k for k in cands if k.rsplit(".", 1)[0] == ns), cands[0])
        return None

    def find(self, typ: str, member: str, must: str):
        """(declaring type, bodies) along the base chain, depth 10; (None, []) when not found."""
        cur = typ
        for _ in range(11):
            bodies = [b for f, s, e in self.idx[cur] for b in members(self.texts[f], s, e, member, must)]
            if bodies:
                return cur, bodies
            cur = self.base_of(cur)
            if cur is None:
                break
        return None, []


def _compare_one(typ, row, old, new):
    if typ not in old.idx or typ not in new.idx:
        return ("type-old" if typ not in old.idx else "type-new", None, None, None)
    od, ob = old.find(typ, row.member, row.must)
    nd, nb = new.find(typ, row.member, row.must)
    decl = nd or od
    if not ob and not nb:
        return ("member-both", None, None, None)
    if ob == nb:
        return ("same", None, None, decl)
    return ("changed", ob, nb, decl)


# ---------------------------------------------------------------------------------------------


def read_snapshot(snapshot: Path, name: str) -> str:
    p = snapshot / name
    if not p.is_file():
        raise BadInput(f"snapshot file not found: {p}")
    return p.read_text(encoding="utf-8")


def declared(typ, decl):
    return f"  (declared on {decl})" if decl and decl != typ else ""


def run(args) -> int:
    root, snapshot, out_dir = Path(args.root), Path(args.snapshot), Path(args.out)
    normalize = not args.no_normalize
    old = load_assemblies(root, args.old_suffix, normalize)
    new = load_assemblies(root, args.new_suffix, normalize)
    if not old:
        raise BadInput(f"no decompiled .cs files under {root} for suffix '{args.old_suffix}'")
    if not new:
        raise BadInput(f"no decompiled .cs files under {root} for suffix '{args.new_suffix}'")

    rows: list[Row] = []
    unparsed: list[str] = []
    taom_dirs = [Path(d) for d in args.taom_src] if args.taom_src else [REPO_ROOT / "Main", REPO_ROOT / "Dependencies"]
    taom_src = scan_taom_source(taom_dirs)
    report: list[str] = []
    for name, parser in (("patch-targets.md", parse_patch_targets), ("gamemodel-bases.md", None),
                         ("reflection-sites.md", parse_reflection)):
        text = read_snapshot(snapshot, name)
        before = len(unparsed)
        if parser is None:
            total, parsed = parse_models(text, rows, unparsed, taom_src)
        else:
            total, parsed = parser(text, rows, unparsed)
        report.append(f"{name}: rows_in_file={total} parsed={parsed} unparsed={len(unparsed) - before}")
    for line in unparsed:
        report.append(f"UNPARSED  {line}")
    report.append(f"unparsed={len(unparsed)}")

    seen, same, changed, missing, inherited = compare(rows, old, new, out_dir)
    report.append(f"rows={len(seen)} same={same} changed={len(changed)} unresolved={len(missing)}")
    for c in sorted(changed, key=lambda r: -r[3]):
        report.append(f"CHANGED  {c[0]}.{c[1]}  [{c[2]}]  ~{c[3]} lines{declared(c[0], c[5])}  -> {c[4]}")
    for m in missing:
        report.append(f"UNRESOLVED {m[3]}  {m[0]}.{m[1]}  [{m[2]}]")
    for typ, member, tag, decl in inherited:
        report.append(f"INHERITED  {typ}.{member}  declared on {decl}  [{tag}]")

    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "summary.txt").write_text("\n".join(report) + "\n", encoding="utf-8")
    print("\n".join(report))
    return 1 if unparsed else 0


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Body diff of every bound engine member, old vs new decompile.")
    ap.add_argument("--root", default=DEFAULT_ROOT, help="decompile root (default %(default)s)")
    ap.add_argument("--old-suffix", required=True, help="folder suffix of the old decompile, e.g. _v1.5.4")
    ap.add_argument("--new-suffix", default="", help="folder suffix of the new decompile ('' = unsuffixed)")
    ap.add_argument("--snapshot", default=str(DEFAULT_SNAPSHOT), help="taleworlds-api-snapshot folder")
    ap.add_argument("--out", required=True, help="output folder for .diff files and summary.txt")
    ap.add_argument("--taom-src", action="append", help="TAOM source folder to resolve TAOM model bases (repeatable; default Main and Dependencies)")
    ap.add_argument("--no-normalize", action="store_true", help="compare raw text (no encoding normalization)")
    args = ap.parse_args(argv)
    try:
        return run(args)
    except BadInput as e:
        print(f"error: {e}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
