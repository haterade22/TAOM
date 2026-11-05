#!/usr/bin/env python3
"""Carry a native address (RVA) in TaleWorlds.Native.dll from one engine build to the next.

Every Bannerlord update moves the engine's native code. A crash address, a Ghidra rename or a
signature from the old build means nothing in the new one, and the shipping binary has no
symbols to look up. This tool finds, for an old RVA, the function that is the same code in
the new build, or says it cannot.

How: both binaries are cut into units, then each unit gets a position-independent hash.
Units are the .pdata entries (a chained entry, a cold part of a function, remembers its root)
plus the "leaf" functions that have no .pdata entry at all: 46% of the engine-method thunks
are leaves, found in the gaps between entries and cut at the 0xCC padding. The hash is a
SHA-256 of the instruction bytes with everything that moves between builds masked: rel32
branches out of the unit, RIP-relative displacements, in-image imm64 values and jump-table
displacements. A branch inside the unit is kept as its offset from the unit start.

A unit is MATCHED only when its hash is unique on both sides, or (second stage) when the
leftovers share one string-reference set that is unique among all units of both builds, and a
similar size. A hash match means identical normalized code, unique in both builds; it is not
proof of identity. Two functions can normalize alike, and a class that gains a member shifts
field offsets so a neighbour's new code equals the old function. A shift check guards the
second case: a match keeps its status only if its address shift (new start minus old start)
agrees with the shifts of the nearest matched units either side (two of up to four); else it is
AMBIGUOUS. Anything shared is AMBIGUOUS and anything else UNKNOWN: the tool never guesses, and
never claims a function was removed. Masking can over-equate two different functions; nothing detects that except the
--oracle self-test, which scores the carry against the engine-method registrations of both
builds (keyed by assembly, interface, method and engine name, never by id: ids are renumbered
between builds). The oracle covers thin engine-method wrappers; zero wrong there does not
prove the deep internals.

Usage:
  python tools/native_build_match.py --old OLD.dll --new NEW.dll --rva 0x6FA900 [--rva ...]
  python tools/native_build_match.py --old OLD.dll --new NEW.dll --oracle
  --new defaults to the installed client DLL; --cache-dir and --no-cache control the per-binary
  index cache (about 35 s per DLL uncached).

Exit: 0 success; 2 bad input (missing file, not a PE, no .pdata, capstone missing, RVA outside
.text, no engine-method map for --oracle); 1 when --oracle finds a wrong match.
Needs capstone. Background: docs/reference/bannerlord-engine-and-toolchain.md.
"""
import argparse
import bisect
import hashlib
import json
import os
import struct
import sys
from collections import Counter, namedtuple
from pathlib import Path

import native_crash_triage as nct
import native_decompile as nd
import native_engine_methods as nem

FORMAT = 2  # 2: .text bounds use min(VirtualSize, SizeOfRawData). Bump on any change to normalize()
MAX_CHAIN_HOPS = 32
SHIFT_WINDOW = 2  # shift check: matched neighbours looked at on each side (all units, old order)
SHIFT_VOUCHERS = 2  # and how many of them must share the unit's shift (fewer exist: all of them)
SIZE_BAND = 0.75  # strings stage: the new size must be within 0.75x to 1/0.75x of the old
KINDS = ("primary", "chained", "leaf")
CACHED_MAP_NOTE = "(cached map, managed hashes unchecked)"

Unit = namedtuple("Unit", "start end kind root")
Row = namedtuple("Row", "start end kind root hash strings")
Index = namedtuple("Index", "key text_start text_end rows starts")
Result = namedtuple("Result", "status new_start how n k")
Carried = namedtuple("Carried", "row offset result")


class ToolError(Exception):
    """Bad input or a missing prerequisite: main() prints it and exits 2."""


# ---------------------------------------------------------------------------------------------
# Units
# ---------------------------------------------------------------------------------------------
def image_info(pe):
    """(ImageBase, SizeOfImage) from a PE32+ optional header."""
    e_lfanew = struct.unpack_from("<I", pe.d, 0x3C)[0]
    opt = e_lfanew + 24
    if struct.unpack_from("<H", pe.d, opt)[0] != 0x20B:
        raise ToolError(f"{pe.path}: not a PE32+ image")
    return struct.unpack_from("<Q", pe.d, opt + 24)[0], struct.unpack_from("<I", pe.d, opt + 56)[0]


def pdata_units(entries, read):
    """Units from RUNTIME_FUNCTION entries `(start, end, unwind)`. `read(rva, n)` returns image
    bytes. A CHAININFO entry (unwind flag 4) names its parent entry after its unwind codes; the
    root is the primary at the end of the chain."""
    units = []
    for start, end, unwind in sorted(e for e in entries if e[0]):
        root = None
        kind = "primary"
        cur = unwind
        for _ in range(MAX_CHAIN_HOPS):
            if read(cur & ~1, 1)[0] >> 3 & 4 == 0:
                break
            kind = "chained"
            count = read((cur & ~1) + 2, 1)[0]
            parent = struct.unpack("<III", read((cur & ~1) + 4 + 2 * ((count + 1) & ~1), 12))
            root, cur = parent[0], parent[2]
        units.append(Unit(start, end, kind, root))
    return units


def merge_spans(spans):
    merged = []
    for s, e in sorted(spans):
        if merged and s <= merged[-1][1]:
            merged[-1][1] = max(merged[-1][1], e)
        else:
            merged.append([s, e])
    return merged


def leaf_units(text, text_rva, spans, md):
    """Leaf functions: the code in the gaps between .pdata spans. Padding (0xCC) is skipped; a
    unit starts at the first other byte and ends after a ret, jmp, int3 or ud2 when the next
    address is the gap end, 16-aligned, or the start of a 0xCC run up to the next 16-aligned
    address. A bound found this way is a heuristic and is reported as such."""
    text_end = text_rva + len(text)
    gaps, cur = [], text_rva
    for s, e in merge_spans(spans):
        if s > cur:
            gaps.append((cur, min(s, text_end)))
        cur = max(cur, e)
    if cur < text_end:
        gaps.append((cur, text_end))
    units = []
    for gap_start, gap_end in gaps:
        r = gap_start
        while r < gap_end:
            if text[r - text_rva] == 0xCC:
                r += 1
                continue
            end = gap_end
            for ins in md.disasm(text[r - text_rva:gap_end - text_rva], r):
                nxt = ins.address + ins.size
                if ins.mnemonic not in ("ret", "jmp", "int3", "ud2"):
                    continue
                pad_to = (nxt + 15) & ~15
                run = text[nxt - text_rva:min(pad_to, gap_end) - text_rva]
                if nxt >= gap_end or nxt == pad_to or (run and all(b == 0xCC for b in run)):
                    end = nxt
                    break
            units.append(Unit(r, end, "leaf", None))
            r = end
    return units


# ---------------------------------------------------------------------------------------------
# Normalizing hash
# ---------------------------------------------------------------------------------------------
# Any change to normalize(), the unit cutting or strings_of() needs a FORMAT bump: cached rows hold its output.
def normalize(code, start, end, image_base, size_of_image, md):
    """Hash of the unit's bytes (`code` = the bytes of [start, end)) with the build-dependent
    fields masked. Decoding stops at a jump table inside the unit or at an undecodable byte; the
    rest counts by length only."""
    from capstone import x86_const as x

    md.detail = True
    out = bytearray()
    pos = 0
    table = None
    for ins in md.disasm(code, start):
        if table is not None and ins.address + ins.size > table:
            break
        raw = bytearray(ins.bytes)
        if ins.group(x.X86_GRP_BRANCH_RELATIVE):
            if ins.imm_size == 4:
                target = ins.operands[0].imm
                raw[ins.imm_offset:ins.imm_offset + 4] = (
                    struct.pack("<i", target - start) if start <= target < end else b"\0\0\0\0")
        else:
            if ins.disp_size == 4:
                for op in ins.operands:
                    if op.type != x.X86_OP_MEM:
                        continue
                    value = op.mem.disp & 0xFFFFFFFF
                    if op.mem.base == x.X86_REG_RIP:
                        raw[ins.disp_offset:ins.disp_offset + 4] = b"\0\0\0\0"
                    elif op.mem.index != 0 and 0x1000 <= value < size_of_image:
                        raw[ins.disp_offset:ins.disp_offset + 4] = b"\0\0\0\0"
                        if start <= value < end and (table is None or value < table):
                            table = value
            if ins.imm_size == 8:
                value = struct.unpack_from("<Q", raw, ins.imm_offset)[0]
                if image_base <= value < image_base + size_of_image:
                    raw[ins.imm_offset:ins.imm_offset + 8] = b"\0" * 8
        out += raw
        pos = ins.address + ins.size - start
    if pos < len(code):
        out += b"TAIL" + struct.pack("<I", len(code) - pos)
    return hashlib.sha256(bytes(out)).hexdigest()[:16]


def strings_of(pe, unit):
    return frozenset(pe.strings_in(unit.start, unit.end, cap=10 ** 6))


def build_index(dll, key):
    """Cut one DLL into units and hash each (about 35 s). Raises ToolError on bad input."""
    try:
        return _build_index(dll, key)
    except (struct.error, IndexError, TypeError) as exc:  # a truncated or malformed image
        raise ToolError(f"{dll}: malformed PE image ({exc})") from None


def _build_index(dll, key):
    try:
        pe = nct.Pe(str(dll))
    except (ValueError, StopIteration, OSError) as exc:
        raise ToolError(f"{dll}: not a readable PE file with a .text section ({exc})") from None
    if pe.pdata is None:
        raise ToolError(f"{dll}: no .pdata section")
    try:
        from capstone import CS_ARCH_X86, CS_MODE_64, Cs
    except ImportError:
        raise ToolError("capstone is not installed for this Python (pip install capstone)") from None
    base, size = image_info(pe)
    po, psz = pe.pdata[3], pe.pdata[4]
    entries = [struct.unpack_from("<III", pe.d, po + i) for i in range(0, psz - 11, 12)]
    entries = [e for e in entries if e[0]]

    def read(rva, n):
        off = pe.rva_to_off(rva)
        return pe.d[off:off + n]

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    _name, text_rva, text_vsize, text_off, text_size = pe.text
    text_size = min(text_vsize, text_size)
    text = pe.d[text_off:text_off + text_size]
    units = pdata_units(entries, read)
    units += leaf_units(text, text_rva, [(s, e) for s, e, _ in entries], md)
    rows = []
    for u in sorted(units):
        off = pe.rva_to_off(u.start)
        code = pe.d[off:off + u.end - u.start]
        rows.append(Row(u.start, u.end, u.kind, u.root,
                        normalize(code, u.start, u.end, base, size, md), strings_of(pe, u)))
    return Index(key, text_rva, text_rva + text_size, rows, [r.start for r in rows])


def index_path(cache_dir, key):
    return Path(cache_dir) / f"{key}.build-match.json"


def load_index(dll, cache_dir, use_cache):
    """The index for this binary, from `<cache_dir>/<key>.build-match.json` when the format and
    key match, else built and (unless use_cache is False) cached."""
    try:
        key = nd.cache_key(dll)
    except OSError as exc:
        raise ToolError(f"cannot read {dll}: {exc}") from None
    cache = index_path(cache_dir, key)
    if use_cache:
        try:
            data = json.loads(cache.read_text(encoding="utf-8"))
            if data["format"] == FORMAT and data["key"] == key:
                rows = [Row(s, e, k, r, h, frozenset(strs)) for s, e, k, r, h, strs in data["rows"]]
                return Index(key, data["text"][0], data["text"][1], rows, [r.start for r in rows])
        except (OSError, ValueError, KeyError, TypeError):
            pass  # absent, cut short or another shape: rebuild
    index = build_index(dll, key)
    if use_cache:
        try:
            cache.parent.mkdir(parents=True, exist_ok=True)
            partial = cache.with_suffix(".tmp")
            partial.write_text(json.dumps({
                "format": FORMAT, "key": key, "text": [index.text_start, index.text_end],
                "rows": [[r.start, r.end, r.kind, r.root, r.hash, sorted(r.strings)]
                         for r in index.rows]}), encoding="utf-8")
            os.replace(partial, cache)  # a killed run leaves the old cache or none, never half
        except OSError:
            pass  # the cache is a speed-up only
    return index


# ---------------------------------------------------------------------------------------------
# Matching
# ---------------------------------------------------------------------------------------------
def match(old_rows, new_rows):
    """{old start: Result}. Stage 1: a hash unique on both sides. Stage 2, over the units stage 1
    left on both sides: an old string set that is non-empty and unique among them, and exactly
    one new unit with the same set within the size band. Shared hashes are AMBIGUOUS; the rest
    UNKNOWN."""
    old_count = Counter(r.hash for r in old_rows)
    new_count = Counter(r.hash for r in new_rows)
    by_hash = {r.hash: r for r in new_rows if new_count[r.hash] == 1}
    results, left_old, taken = {}, [], set()
    for r in old_rows:
        if old_count[r.hash] == 1 and new_count.get(r.hash) == 1:
            hit = by_hash[r.hash]
            results[r.start] = Result("MATCHED", hit.start, "hash", 1, 1)
            taken.add(hit.start)
        else:
            left_old.append(r)
    left_new = [r for r in new_rows if r.start not in taken]
    old_sets = Counter(r.strings for r in old_rows if r.strings)
    new_sets = Counter(r.strings for r in new_rows if r.strings)
    new_by_set = {}
    for r in left_new:
        if r.strings:
            new_by_set.setdefault(r.strings, []).append(r)
    for r in left_old:
        size = r.end - r.start
        if r.strings and old_sets[r.strings] == 1:
            cands = [c for c in new_by_set.get(r.strings, [])
                     if SIZE_BAND * size <= c.end - c.start <= size / SIZE_BAND]
            if len(cands) == 1 and new_sets[r.strings] == 1:
                results[r.start] = Result("MATCHED", cands[0].start, "strings", 1, 1)
                continue
            if cands:
                results[r.start] = Result("AMBIGUOUS", None, "strings", new_sets[r.strings], 1)
                continue
        n, k = new_count.get(r.hash, 0), old_count[r.hash]
        if n > 1 or k > 1:
            results[r.start] = Result("AMBIGUOUS", None, "hash", n, k)
        else:
            results[r.start] = Result("UNKNOWN", None, None, 0, k)
    return shift_check(old_rows, results)


def shift_check(old_rows, results):
    """A match stays MATCHED only when its shift (new start - old start) equals the shift of at
    least SHIFT_VOUCHERS of the SHIFT_WINDOW nearest matched units on each side in old order. A
    class that gained a member moves a function's neighbour into its place; that neighbour's code
    can be byte-identical to the old function, and only the function's isolation from the rest
    of the build gives it away. Two such neighbours can vouch for each other (measured: client
    1.5.5 against wEditor, one voucher left 2 wrong, two leaves 0), hence two. Original shifts
    are compared, so one demotion does not cascade."""
    pool = sorted(s for s, res in results.items() if res.status == "MATCHED")
    shifts = [results[s].new_start - s for s in pool]
    out = dict(results)
    for i, start in enumerate(pool):
        near = shifts[max(i - SHIFT_WINDOW, 0):i] + shifts[i + 1:i + 1 + SHIFT_WINDOW]
        if near and near.count(shifts[i]) < min(SHIFT_VOUCHERS, len(near)):
            out[start] = Result("AMBIGUOUS", None, "shift", 1, 1)
    return out


def carry(index, results, rva):
    """Carried(row, offset, result) for an old RVA; row is None inside padding."""
    i = bisect.bisect_right(index.starts, rva) - 1
    if i < 0 or rva >= index.rows[i].end:
        return Carried(None, 0, None)
    r = index.rows[i]
    return Carried(r, rva - r.start, results.get(r.start))


def describe(result, offset):
    if result is None or result.status == "UNKNOWN":
        return "UNKNOWN"
    if result.status == "AMBIGUOUS":
        return f"AMBIGUOUS {result.n} ({result.how}, {result.k} old)"
    if result.how == "hash":
        return f"MATCHED 0x{result.new_start:X}+0x{offset:X} (hash)"
    return f"MATCHED 0x{result.new_start:X} (strings, offset not carried)"


def format_row(carried, rva, results):
    if carried.row is None:
        return f"0x{rva:X}  padding: no function  UNKNOWN"
    r = carried.row
    note = ", bounds from padding" if r.kind == "leaf" else ""
    where = f"0x{r.start:X}+0x{carried.offset:X}"
    if r.kind == "chained":
        where += f" of 0x{r.root:X}"
    text = f"0x{rva:X}  {r.kind} {where} (0x{r.end - r.start:X}{note})  "
    text += describe(carried.result, carried.offset)
    if r.kind == "chained" and r.root is not None:
        text += f"; root {describe(results.get(r.root), 0)}"
    return text


def engine_map_for(dll, cache_dir):
    """(EngineMethodMap, note) for a native DLL. Normally the map nem builds beside the managed
    DLLs. An archived DLL has none of them, so fall back to a cached map of the same binary
    (`<cache_dir>/*-<sha16>.engine-methods.json`, current format, no problems); the note says
    the managed hashes were not checked."""
    key = nd.cache_key(dll)
    try:
        built = nem.load_or_build(dll, cache_dir, key)
        if not built.problems:
            return built, ""
    except nem.EngineMapError:
        pass
    sha = key.rpartition("-")[2]
    for path in sorted(Path(cache_dir).glob(f"*-{sha}.engine-methods.json")):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
            if data["format"] == nem.FORMAT and not data["problems"] and data["native"].endswith(sha):
                return nem.EngineMethodMap([nem.Resolved(*r) for r in data["methods"]]), CACHED_MAP_NOTE
        except (OSError, ValueError, KeyError, TypeError, AttributeError):
            continue
    raise ToolError(f"no engine-method map for {dll}: the managed AutoGenerated DLLs are not "
                    f"beside it and {cache_dir} has no current-format map for sha {sha}")


# ---------------------------------------------------------------------------------------------
# Oracle
# ---------------------------------------------------------------------------------------------
OracleReport = namedtuple("OracleReport",
                          "total correct_hash correct_strings wrong ambiguous unknown by_kind")


def _by_key(engine_map):
    return {(r.assembly, r.iface, r.method, r.engine_name): r for r in engine_map.methods}


def oracle(old_map, new_map, old_index, results):
    """Score the carry of every single-owner engine method. Keyed by (assembly, interface,
    method, engine name), never by id: ids are renumbered between builds. Single-owner: the old
    address has exactly one key and that key is registered in both builds."""
    old_by, new_by = _by_key(old_map), _by_key(new_map)
    owners = Counter(r.rva for r in old_by.values())
    counts = Counter()
    by_kind = {k: Counter() for k in (*KINDS, "unindexed")}
    wrong, total = [], 0
    for key, old in sorted(old_by.items(), key=lambda kv: kv[1].rva):
        if owners[old.rva] != 1 or key not in new_by:
            continue
        total += 1
        expected = new_by[key].rva
        carried = carry(old_index, results, old.rva)
        kind = carried.row.kind if carried.row else "unindexed"
        result = carried.result
        if result is None or result.status == "UNKNOWN":
            outcome = "unknown"
        elif result.status == "AMBIGUOUS":
            outcome = "ambiguous"
        else:
            if result.how == "hash":
                got = result.new_start + carried.offset
                right = got == expected
            else:
                got = result.new_start  # the offset is not carried, so only the start can be right
                right = got == expected
            outcome = "correct" if right else "wrong"
            if right:
                counts[result.how] += 1
            else:
                label = f"{old.iface}.{old.method}" if old.iface else old.engine_name
                wrong.append((label, old.rva, got, expected, result.how))
        by_kind[kind][outcome] += 1
    totals = Counter()
    for c in by_kind.values():
        totals.update(c)
    return OracleReport(total, counts["hash"], counts["strings"], wrong,
                        totals["ambiguous"], totals["unknown"], by_kind)


def format_oracle(report):
    lines = [f"oracle: {report.total} single-owner methods: correct "
             f"{report.correct_hash + report.correct_strings} (hash {report.correct_hash}, "
             f"strings {report.correct_strings}), wrong {len(report.wrong)}, "
             f"ambiguous {report.ambiguous}, unknown {report.unknown}"]
    for kind, c in report.by_kind.items():
        if sum(c.values()):
            lines.append(f"  {kind}: correct {c['correct']}, wrong {c['wrong']}, "
                         f"ambiguous {c['ambiguous']}, unknown {c['unknown']}")
    for label, old, got, expected, how in report.wrong:
        lines.append(f"WRONG {label} 0x{old:X} -> 0x{got:X}, registered 0x{expected:X} ({how})")
    return lines


# ---------------------------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------------------------
def unit_counts(index):
    c = Counter(r.kind for r in index.rows)
    return ", ".join(f"{k} {c[k]}" for k in KINDS)


def main(argv=None, index_loader=load_index, map_loader=engine_map_for):
    ap = argparse.ArgumentParser(description="Carry native addresses from one engine build to the next.")
    ap.add_argument("--old", required=True, help="the older TaleWorlds.Native.dll")
    ap.add_argument("--new", default=nd.DEFAULT_DLL, help="the newer DLL (default: the installed client)")
    mode = ap.add_mutually_exclusive_group(required=True)
    mode.add_argument("--rva", action="append", type=lambda s: int(s, 0), metavar="RVA",
                      help="an old RVA to carry (repeatable)")
    mode.add_argument("--oracle", action="store_true",
                      help="score the carry against both builds' engine-method registrations")
    ap.add_argument("--cache-dir", default=nd.DEFAULT_PROJECT_DIR, help="index and map cache folder")
    ap.add_argument("--no-cache", action="store_true", help="neither read nor write the index cache")
    args = ap.parse_args(argv)
    try:
        for dll in (args.old, args.new):
            if not os.path.isfile(dll):
                raise ToolError(f"file not found: {dll}")
        old = index_loader(args.old, args.cache_dir, not args.no_cache)
        new = index_loader(args.new, args.cache_dir, not args.no_cache)
        if args.rva:
            for rva in args.rva:
                if not old.text_start <= rva < old.text_end:
                    raise ToolError(f"0x{rva:X} is outside .text (0x{old.text_start:X}-0x{old.text_end:X})")
        maps = (map_loader(args.old, args.cache_dir), map_loader(args.new, args.cache_dir)) if args.oracle else None
    except ToolError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    print(f"old: {args.old} ({old.key})\n  units: {unit_counts(old)}")
    print(f"new: {args.new} ({new.key})\n  units: {unit_counts(new)}")
    results = match(old.rows, new.rows)
    if args.rva:
        for rva in args.rva:
            print(format_row(carry(old, results, rva), rva, results))
        return 0
    (old_map, old_note), (new_map, new_note) = maps
    if old_note or new_note:
        print(old_note or new_note)
    report = oracle(old_map, new_map, old, results)
    print("\n".join(format_oracle(report)))
    return 1 if report.wrong else 0


if __name__ == "__main__":
    sys.exit(main())
