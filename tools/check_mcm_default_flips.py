#!/usr/bin/env python
"""Gate: an MCM setting whose compiled default changed under an unchanged name.

MCM saves every setting by property name and loads the saved value over the compiled
default, so a changed default reaches nobody who already saved settings. The fix is a
rename (see docs/features/shader-precompilation.md), or an allowlist entry with a reason
when the flip is harmless if never applied.

Scans every Main/**/*.cs file that declares a class deriving from AttributeGlobalSettings,
parses `public <Type> <Name> { get; set; } = <default>;`, and compares the working tree
against EVERY release tag reachable from HEAD (`git tag --list "v*" --merged HEAD`): a
player may have saved settings under any of them, so the oldest tag whose value differs is
the one reported. The settings files are found at each ref (a file may have moved).
`--base <ref>` compares against that one ref instead; a bad ref exits 2. No reachable
tag (a shallow clone) exits 2: fetch tags.

A default that is a constant reference (`Type.Member`, optionally cast or namespace
qualified) is resolved against the one `const` / `static readonly` literal of that name at
each ref. A literal moved into a constant with the same value reports nothing. A default
that is not a plain literal, or whose constant cannot be resolved to one literal, and
whose text differs is UNPARSED, never silently skipped. A public get/set property the
parser cannot read is UNPARSED too. `System.Action` button properties are skipped.

Allowlist (tools/mcm_default_flips_allowlist.json): a JSON object keyed "Class.Name" with
{"from", "to", "reason"}. An entry matches a finding only when from and to equal the
reported values; an entry that matches nothing is reported and fails the gate.

Exit codes: 0 ok, 1 finding (FLIP, UNPARSED or stale allowlist entry), 2 error.
Tests: tools/tests/test_check_mcm_default_flips.py
"""
import argparse
import json
import re
import subprocess
import sys
from decimal import Decimal, InvalidOperation
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ALLOWLIST = ROOT / "tools" / "mcm_default_flips_allowlist.json"

PROP_RE = re.compile(
    r"public\s+(?:static\s+)?([\w<>\[\]?,.]+)\s+(\w+)\s*\{\s*get;\s*set;\s*\}\s*(?:=\s*([^;]*?)\s*;)?",
    re.S)
SETTABLE_RE = re.compile(r"public\s+(?!override\b)[^;{}=()]*?(\w+)\s*\{[^{}]*\bset\b", re.S)
CLASS_RE = re.compile(r"\bclass\s+(\w+)\s*:\s*AttributeGlobalSettings\b")
GREP_CLASS = r"AttributeGlobalSettings"
NUM_RE = re.compile(r"^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?[fFdDmMuUlL]*$")
CONST_REF_RE = re.compile(r"^(?:\(\s*[\w.]+\s*\)\s*)?(?:[A-Za-z_]\w*\.)*([A-Za-z_]\w*)\.([A-Za-z_]\w*)$")
TOKEN_RE = re.compile(r"\b(?:[A-Za-z_]\w*\.)*([A-Za-z_]\w*)\.([A-Za-z_]\w*)\b")
DECL_RE = re.compile(r"\b(?:const|static\s+readonly)\s+[\w<>\[\]?.]+\s+(\w+)\s*=\s*([^;]+);")


class GateError(Exception):
    """Exit 2: the gate could not compare."""


def strip_comments(text):
    """Remove // and /* */ comments, leaving string and char literals intact."""
    out, i, n = [], 0, len(text)
    while i < n:
        c = text[i]
        two = text[i:i + 2]
        if two == "//":
            while i < n and text[i] != "\n":
                i += 1
        elif two == "/*":
            j = text.find("*/", i + 2)
            i = n if j < 0 else j + 2
        elif c == '"' or c == "'":
            verbatim = c == '"' and i > 0 and text[i - 1] == "@"
            j = i + 1
            while j < n:
                if text[j] == "\\" and not verbatim:
                    j += 2
                    continue
                if text[j] == c:
                    if verbatim and text[j + 1:j + 2] == '"':
                        j += 2
                        continue
                    break
                j += 1
            out.append(text[i:j + 1])
            i = j + 1
        else:
            out.append(c)
            i += 1
    return "".join(out)


def literal(raw):
    """(True, normalized) for a plain literal, else (False, None)."""
    s = raw.strip()
    if s in ("true", "false"):
        return True, s
    if NUM_RE.match(s):
        digits = re.sub(r"[fFdDmMuUlL]+$", "", s)
        try:
            return True, format(Decimal(digits).normalize(), "f")
        except InvalidOperation:
            return False, None
    if re.fullmatch(r'"(?:[^"\\]|\\.)*"', s, re.S) or re.fullmatch(r"'(?:[^'\\]|\\.)+'", s):
        return True, s
    return False, None


def classify(raw):
    """A parsed default: ('ok', value), ('const', (Type, Member)) or ('unparsed', text)."""
    if raw is None:
        return "ok", "<no initializer>"
    ok, val = literal(raw)
    if ok:
        return "ok", val
    text = re.sub(r"\s+", " ", raw.strip())
    m = CONST_REF_RE.match(text)
    if m:
        return "const", (m.group(1), m.group(2))
    return "unparsed", text


def parse_settings(text):
    """({(class, name): (kind, value, raw)}, [skipped names]) for every MCM settings class."""
    code = strip_comments(text)
    result, skipped = {}, []
    classes = list(CLASS_RE.finditer(code))
    for k, m in enumerate(classes):
        end = classes[k + 1].start() if k + 1 < len(classes) else len(code)
        seen = set()
        for p in PROP_RE.finditer(code, m.end(), end):
            seen.add(p.group(2))
            if p.group(1) in ("Action", "System.Action"):
                continue
            kind, val = classify(p.group(3))
            result[(m.group(1), p.group(2))] = (kind, val, re.sub(r"\s+", " ", (p.group(3) or "").strip()))
        for s in SETTABLE_RE.finditer(code, m.end(), end):
            if s.group(1) not in seen:
                skipped.append((m.group(1), s.group(1)))
    return result, skipped


def git(repo, *args, data=None):
    return subprocess.run(["git", "-C", str(repo), "-c", "color.ui=never", *args], capture_output=True, input=data)


def check_repo(repo):
    r = git(repo, "rev-parse", "--show-toplevel")
    if r.returncode != 0:
        raise GateError(f"{repo} is not a git repository")
    top = Path(r.stdout.decode().strip()).resolve()
    if top != Path(repo).resolve():
        raise GateError(f"{repo} is not the repository root ({top})")


def release_refs(repo, base):
    if base:
        if git(repo, "rev-parse", "--verify", "--quiet", base + "^{commit}").returncode != 0:
            raise GateError(f"--base {base!r} is not a commit in this repository")
        return [base]
    r = git(repo, "tag", "--list", "v*", "--merged", "HEAD", "--sort=v:refname")
    if r.returncode != 0:
        raise GateError("git tag failed: " + r.stderr.decode(errors="replace").strip())
    tags = r.stdout.decode().split()
    if not tags:
        raise GateError("no release tag (v*) is reachable from HEAD: fetch tags (git fetch --tags)")
    return tags


def cat_blobs(repo, specs):
    """{spec: text} for 'ref:path' specs through one git cat-file --batch; a miss raises."""
    if not specs:
        return {}
    r = git(repo, "cat-file", "--batch", data=("\n".join(specs) + "\n").encode())
    out, buf, pos = {}, r.stdout, 0
    for spec in specs:
        nl = buf.find(b"\n", pos)
        if r.returncode != 0 or nl < 0:
            raise GateError(f"git cat-file failed reading {spec}")
        header = buf[pos:nl].decode(errors="replace").split()
        if len(header) != 3 or header[1] != "blob":
            raise GateError(f"cannot read {spec} from git: {buf[pos:nl].decode(errors='replace')}")
        size = int(header[2])
        out[spec] = buf[nl + 1:nl + 1 + size].decode("utf-8-sig", errors="replace")
        pos = nl + 1 + size + 1
    return out


def grep_files(repo, pattern, refs):
    """{ref: [paths]} of Main/**/*.cs files matching the extended regex at each ref."""
    r = git(repo, "grep", "--no-color", "-l", "-E", pattern, *refs, "--", ":(glob)Main/**/*.cs")
    if r.returncode not in (0, 1):
        raise GateError("git grep failed: " + r.stderr.decode(errors="replace").strip())
    found = {ref: [] for ref in refs}
    for line in r.stdout.decode().splitlines():
        for ref in refs:
            if line.startswith(ref + ":"):
                path = line[len(ref) + 1:]
                if path.endswith(".cs"):
                    found[ref].append(path)
                break
    return found


def tree_files(repo, pattern):
    """Working-tree Main/**/*.cs files whose text matches the regex."""
    rx = re.compile(pattern)
    out = {}
    for p in sorted((Path(repo) / "Main").rglob("*.cs")):
        if "obj" in p.parts or "bin" in p.parts:
            continue
        text = p.read_bytes().decode("utf-8-sig", errors="replace")
        if rx.search(text):
            out[p.relative_to(repo).as_posix()] = text
    return out


def texts_at(repo, refs, grep_pat, tree_pat):
    """{ref or None: {path: text}}; None is the working tree."""
    result = {None: tree_files(repo, tree_pat)}
    found = grep_files(repo, grep_pat, refs)
    specs = [f"{ref}:{path}" for ref in refs for path in found[ref]]
    blobs = cat_blobs(repo, specs)
    for ref in refs:
        result[ref] = {path: blobs[f"{ref}:{path}"] for path in found[ref]}
    return result


def constant_literals(repo, refs, members):
    """{ref or None: {(Type, Member): value}} for members with exactly one literal declaration."""
    names = sorted({m for _, m in members})
    if not names:
        return {}
    alt = "|".join(names)
    texts = texts_at(repo, refs, r"(const|static[[:space:]]+readonly)[^=;(]*[[:space:]](" + alt + r")[[:space:]]*=",
                     r"\b(?:const|static\s+readonly)\s+[\w<>\[\]?.]+\s+(?:" + alt + r")\s*=")
    out = {}
    for ref, files in texts.items():
        cands = {}
        for text in files.values():
            code = strip_comments(text)
            types = set(re.findall(r"\b(?:class|struct)\s+(\w+)", code))
            for d in DECL_RE.finditer(code):
                for t in types:
                    if (t, d.group(1)) in members:
                        cands.setdefault((t, d.group(1)), []).append(d.group(2))
        resolved = {}
        for key, vals in cands.items():
            if len(vals) == 1:
                ok, val = literal(vals[0])
                resolved[key] = val if ok else re.sub(r"\s+", " ", vals[0].strip())
        out[ref] = resolved
    return out


def effective(entry, consts):
    kind, val, raw = entry
    if kind == "const":
        got = consts.get(val)
        if got is not None and literal(got)[0]:
            return "ok", got, raw
        text = got if got is not None else raw
        return "unparsed", text, text
    if kind == "unparsed":
        val = TOKEN_RE.sub(lambda m: consts.get((m.group(1), m.group(2)), m.group(0)), raw)
        return kind, val, val
    return kind, val, raw


def first_differences(per_ref, cur, consts):
    """{(class, name): (ref, old, new, code)} the oldest ref whose value differs from now."""
    found = {}
    for ref, props in per_ref:
        for key, entry in props.items():
            if key in found or key not in cur:
                continue
            bk, bv, braw = effective(entry, consts.get(ref, {}))
            ck, cv, craw = effective(cur[key], consts.get(None, {}))
            if bk == "ok" and ck == "ok":
                if bv != cv:
                    found[key] = (ref, bv, cv, "FLIP")
            elif braw != craw or bk != ck:
                found[key] = (ref, braw, craw, "UNPARSED")
    return found


def load_allowlist(path):
    p = Path(path)
    if not p.exists():
        return {}
    try:
        data = json.loads(p.read_bytes().decode("utf-8-sig"))
    except ValueError as e:
        raise GateError(f"allowlist is not valid JSON: {e}")
    if not isinstance(data, dict):
        raise GateError("allowlist must be a JSON object keyed Class.Name")
    for k, v in data.items():
        if not isinstance(v, dict) or any(not isinstance(v.get(f), str) or not v[f].strip()
                                          for f in ("from", "to", "reason")):
            raise GateError(f'allowlist entry {k!r} must be {{"from", "to", "reason"}} with non-empty strings')
    return data


def run(repo, base, allowlist_path):
    check_repo(repo)
    allow = load_allowlist(allowlist_path)
    refs = release_refs(repo, base)
    settings_pat = (GREP_CLASS, r"\bclass\s+\w+\s*:\s*AttributeGlobalSettings\b")
    texts = texts_at(repo, refs, *settings_pat)
    parsed, skipped = {}, []
    for ref, files in texts.items():
        props = {}
        for text in files.values():
            p, s = parse_settings(text)
            props.update(p)
            if ref is None:
                skipped += s
        parsed[ref] = props
    cur = parsed[None]
    if not cur:
        raise GateError("the working tree yields no MCM settings (no class derives from AttributeGlobalSettings under Main)")
    names = sorted({c for c, _ in cur})
    partial = tree_files(repo, "\\bpartial" + "\\s+class" + "\\s+(?:" + "|".join(names) + ")" + "\\b")
    if partial:
        raise GateError("a settings class is partial (" + ", ".join(sorted(partial)) + "): the gate reads only the file naming its base")
    wanted = {v for props in parsed.values() for k, v, _ in props.values() if k == "const"}
    wanted |= {(t.group(1), t.group(2)) for props in parsed.values() for k, v, _ in props.values()
               if k == "unparsed" for t in TOKEN_RE.finditer(v)}
    if not any(parsed[r] for r in refs):
        raise GateError("no MCM setting was found at any release tag: the scan read nothing to compare")
    consts = constant_literals(repo, refs, wanted)
    diffs = first_differences([(r, parsed[r]) for r in refs], cur, consts)
    lines, bad, used = [], 0, set()
    for (cls, name), (ref, old, new, code) in sorted(diffs.items()):
        key = f"{cls}.{name}"
        a = allow.get(key)
        if a and a["from"] == old and a["to"] == new:
            used.add(key)
            lines.append(f"ALLOWED {key}: {old} -> {new} (oldest differing release {ref}; {a['reason']})")
        else:
            bad += 1
            lines.append(f"{code} {key}: {old} -> {new} (oldest differing release {ref})")
    for cls, name in skipped:
        bad += 1
        lines.append(f"UNPARSED {cls}.{name}: public get/set property the parser cannot read")
    for key in sorted(set(allow) - used):
        bad += 1
        lines.append(f"STALE-ALLOWLIST {key}: matches no finding (from/to must equal the reported values)")
    return lines, bad, len(cur), len(refs)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--base", help="compare against this one ref instead of every reachable v* tag")
    ap.add_argument("--repo", default=str(ROOT))
    ap.add_argument("--allowlist", default=None)
    args = ap.parse_args(argv)
    allowlist = args.allowlist or str(Path(args.repo) / "tools" / "mcm_default_flips_allowlist.json")
    try:
        lines, bad, nsettings, nrefs = run(args.repo, args.base, allowlist)
    except (GateError, OSError) as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return 2
    for line in lines:
        print(line)
    print(f"compared {nsettings} settings against {nrefs} release tags")
    if bad:
        print(f"{bad} finding(s): rename the property so the new default reaches saved settings, "
              "or allowlist it with a reason in tools/mcm_default_flips_allowlist.json")
        return 1
    print("OK: no MCM default changed under an unchanged name")
    return 0


if __name__ == "__main__":
    sys.exit(main())
