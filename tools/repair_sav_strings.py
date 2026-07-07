#!/usr/bin/env python3
"""
Offline repair for Bannerlord saves bricked by the WarOfTheRing momentum >32 KB bug
(TAOM v2.0.9). Stdlib only, no game required.

THE BUG (verified against the decompiled v1.4.6 engine):
  TAOM's WarOfTheRingMomentum serializes its whole event log as ONE SyncData string
  (`_taom_wotr_momentum_v2`). The engine's ArchiveSerializer.SerializeEntry writes each
  archive entry's length as `WriteShort((short)Data.Length)` (ArchiveSerializer.cs:27) —
  a signed-int16 truncation — but writes the data IN FULL. Any entry > 32,767 bytes gets
  a wrong length on disk; on load ArchiveDeserializer.LoadFrom (ReadShort -> ReadBytes)
  desyncs and throws "Source array was not long enough" (or OverflowException in the
  32,768–65,535 range). The corruption happens at WRITE time; every save past the point
  the momentum JSON crosses ~32 KB (a developed campaign) is unloadable.

THE REPAIR (zero campaign-data loss):
  The momentum log is a cosmetic war-progress tracker — none of the campaign (heroes,
  parties, settlements, items) lives in it. This tool finds the oversized momentum STRING
  entry in the Strings archive, resets it to an empty string (the mod re-derives fresh
  momentum state + re-enrolls kingdoms on the next daily tick), re-serializes the archive,
  and rewrites the save. The result loads on the VANILLA engine — no runtime patch needed.
  Only the war-meter HISTORY is lost; the campaign is byte-identical otherwise.

FORMAT (all little-endian; BitConverter on x64):
  .sav = [int32 metaLen][metaLen UTF-8 JSON metadata][raw-deflate GameData]
  GameData (GameData.Write): Header(len+bytes), ObjectData(int32 count, then len+bytes each),
    ContainerData(int32 count, then len+bytes each), Strings(len+bytes).  (len = int32)
  Archive (ArchiveSerializer.FinalizeAndGetBinaryData / ArchiveDeserializer.LoadFrom):
    [int32 folderCount]
    folderCount × { 3B parentGlobalId | 3B globalId | 3B localId | 1B folderExt }   (10B)
    [int32 entryCount]
    entryCount × { 3B folderId | 3B entryId | 1B entryExt | int16 dataLen | byte[dataLen] }
  3-byte ints are LE, sign-extended (0xFFFFFF == -1). A Txt(10) string entry's data is
  itself `[int32 strLen][strLen UTF-8]` (BinaryReader.ReadString).

Usage:
  python tools/repair_sav_strings.py <path.sav>            # diagnose only (non-destructive)
  python tools/repair_sav_strings.py <path.sav> --repair   # write <name>_fixed.sav
Exit codes: 0 clean/repaired, 1 unreadable, 2 oversized entry found (diagnose), 3 repair refused.

Player-facing Windows walkthrough (find the save folder, run it):
  docs/SAVE-REPAIR-GUIDE.md
A no-install PowerShell twin (recommended for players) lives beside this file:
  tools/repair_sav_strings.ps1   (verified byte-identical decompressed output)
"""
import argparse, json, os, struct, sys, zlib

ENTRY_EXT_TXT = 10          # SaveEntryExtension.Txt
INT16_MAX = 32767
WRAP = 65536
MOMENTUM_KEYS = ('"free.momentum"', '"evil.momentum"', '"warStarted"', '"warEnded"')


# ---- .sav container framing (shared with inspect_sav.py) ----

def read_metadata(data):
    (n,) = struct.unpack_from("<i", data, 0)
    if n <= 0 or 4 + n > len(data):
        raise ValueError(f"metadata length {n} out of range (file is {len(data)} bytes)")
    meta = json.loads(data[4:4 + n].decode("utf-8"))
    return meta.get("List", meta), data[:4 + n], 4 + n


def inflate(data, off):
    d = zlib.decompressobj(-15)
    raw = d.decompress(data[off:])
    if not d.eof:
        raise ValueError("deflate stream incomplete — file is truncated at the container level")
    if d.unused_data:
        raise ValueError(f"{len(d.unused_data)} unexpected trailing byte(s) after deflate stream")
    return raw


def split_sections(raw):
    """Return [(name, start, end)] for Header, ObjectData[*], ContainerData[*], Strings."""
    pos, sections = 0, []
    def block(name):
        nonlocal pos
        (n,) = struct.unpack_from("<i", raw, pos); pos += 4
        if n < 0 or pos + n > len(raw):
            raise ValueError(f"{name} length {n} at offset {pos-4} exceeds region ({len(raw)} bytes)")
        sections.append((name, pos, pos + n)); pos += n
    block("Header")
    for label in ("ObjectData", "ContainerData"):
        (count,) = struct.unpack_from("<i", raw, pos); pos += 4
        for i in range(count):
            block(f"{label}[{i}]")
    block("Strings")
    if pos != len(raw):
        raise ValueError(f"section walk ended at {pos}, expected {len(raw)}")
    return sections


# ---- archive (folder table + entries) ----

def _r3(buf, p):  # 3-byte LE sign-extended int
    b0, b1, b2 = buf[p], buf[p + 1], buf[p + 2]
    v = b0 | (b1 << 8) | (b2 << 16)
    if b0 == b1 == b2 == 0xFF:
        v -= 1 << 24
    return v


def _w3(v):
    return bytes(((v & 0xFF), (v >> 8) & 0xFF, (v >> 16) & 0xFF))


def parse_archive(buf):
    """Parse a serialized archive. Returns (folder_bytes, entries) where each entry is a
    dict {folderId, entryId, ext, data}.

    The int16 length field only encodes true_len mod 65536; the true data length is
    recovered per entry as `(stored & 0xFFFF) + k*65536`. k is found by a local anchor:
    after the entry's data, the next 9 bytes must be a valid entry header (folderId is a
    real folder global-id, ext ≤ Txt, entryId sequential) — or, for the last entry, the
    data must end exactly at the archive end. Handles any number of oversized entries in a
    single left-to-right pass (values 32,768–65,535 recover at k=0 since their low 16 bits
    still equal the true length; only > 65,535 needs k ≥ 1)."""
    (folder_count,) = struct.unpack_from("<i", buf, 0)
    p = 4 + folder_count * 10
    folder_bytes = buf[:p]                       # folder table is untouched by repair
    valid_folders = {-1}
    for i in range(folder_count):
        valid_folders.add(_r3(buf, 4 + i * 10 + 3))   # each record: parent(3) | GLOBAL(3) | local(3) | ext(1)
    (entry_count,) = struct.unpack_from("<i", buf, p); p += 4
    end = len(buf)
    max_k = end // WRAP + 2

    def header_ok(q, expected_entry_id):
        if q + 9 > end:
            return False
        if buf[q + 6] > ENTRY_EXT_TXT:
            return False
        if _r3(buf, q) not in valid_folders:
            return False
        return expected_entry_id is None or _r3(buf, q + 3) == expected_entry_id

    entries = []
    pos = p
    for j in range(entry_count):
        if pos + 9 > end:
            raise ValueError(f"ran out of bytes at entry {j}/{entry_count} (offset {pos})")
        folder_id = _r3(buf, pos); entry_id = _r3(buf, pos + 3); ext = buf[pos + 6]
        (stored,) = struct.unpack_from("<h", buf, pos + 7)
        base = stored & 0xFFFF
        dstart = pos + 9
        is_last = j == entry_count - 1
        chosen = None
        for k in range(0, max_k):
            dlen = base + k * WRAP
            qend = dstart + dlen
            if qend > end:
                break
            if (qend == end) if is_last else header_ok(qend, entry_id + 1):
                chosen = dlen
                break
        if chosen is None:
            raise ValueError(f"could not recover length for entry index {j} (id={entry_id}, ext={ext})")
        entries.append({"folderId": folder_id, "entryId": entry_id, "ext": ext,
                        "data": buf[dstart:dstart + chosen]})
        pos = dstart + chosen
    if pos != end:
        raise ValueError(f"archive parse ended at {pos}, expected {end}")
    return folder_bytes, entries


def serialize_archive(folder_bytes, entries):
    out = bytearray(folder_bytes)
    out += struct.pack("<i", len(entries))
    for e in entries:
        if len(e["data"]) > INT16_MAX:
            raise ValueError(f"entry id={e['entryId']} still {len(e['data'])} B > {INT16_MAX} after repair")
        out += _w3(e["folderId"]) + _w3(e["entryId"]) + bytes([e["ext"]])
        out += struct.pack("<h", len(e["data"])) + e["data"]
    return bytes(out)


def decode_string_entry(data):
    """A Txt entry's data is [int32 strLen][UTF-8]. Return the decoded str, or None."""
    if len(data) < 4:
        return None
    (n,) = struct.unpack_from("<i", data, 0)
    if n < 0 or 4 + n != len(data):
        return None
    try:
        return data[4:4 + n].decode("utf-8")
    except UnicodeDecodeError:
        return None


EMPTY_STRING_DATA = struct.pack("<i", 0)  # ReadString -> "" -> momentum resets to fresh state


def is_momentum(text):
    return text is not None and text.lstrip().startswith("{") and any(k in text for k in MOMENTUM_KEYS)


# ---- driver ----

def main():
    ap = argparse.ArgumentParser(description="Diagnose/repair the TAOM momentum >32KB save-corruption bug")
    ap.add_argument("path")
    ap.add_argument("--repair", action="store_true", help="write <name>_fixed.sav with the momentum entry reset")
    ap.add_argument("--force", action="store_true", help="reset ANY oversized string entry, not just recognized momentum")
    args = ap.parse_args()

    data = open(args.path, "rb").read()
    try:
        meta, meta_bytes, off = read_metadata(data)
        raw = inflate(data, off)
        sections = split_sections(raw)
    except Exception as e:
        print(f"ERROR: {e}")
        return 1

    print(f"{'Character':16} {meta.get('CharacterName', '?')}   day {meta.get('DayLong', '?')}   "
          f"{meta.get('ApplicationVersion', '?')}   TAOM_Build={meta.get('TAOM_Build', '<none>')}")

    oversized = []   # (section_name, entry_index, entry, decoded_text, is_momentum)
    archives = {}    # section_name -> (folder_bytes, entries) for sections we parsed
    # SyncData strings (incl. the momentum blob) live ONLY in the Strings archive — the
    # ObjectData/ContainerData sections are hundreds of thousands of small per-object
    # archives with no oversized string entries, so scanning them is both needless and slow.
    for name, s, e in sections:
        if name != "Strings":
            continue
        try:
            folder_bytes, entries = parse_archive(raw[s:e])
        except Exception as ex:
            print(f"  WARNING: could not parse {name} archive: {ex}")
            continue
        archives[name] = (folder_bytes, entries)
        for idx, ent in enumerate(entries):
            if len(ent["data"]) > INT16_MAX:
                text = decode_string_entry(ent["data"]) if ent["ext"] == ENTRY_EXT_TXT else None
                oversized.append((name, idx, ent, text, is_momentum(text)))

    if not oversized:
        print("  No oversized (>32,767 B) archive entries found — this save is NOT hit by the momentum bug.")
        return 0

    print(f"\n  Found {len(oversized)} oversized entr{'y' if len(oversized)==1 else 'ies'} (>32,767 B — the write-time corruption):")
    for name, idx, ent, text, mom in oversized:
        kind = "MOMENTUM war-tracker string" if mom else (
            "string entry" if text is not None else f"non-string entry (ext={ent['ext']})")
        true_len = len(ent["data"])
        (engine_len,) = struct.unpack("<h", struct.pack("<H", true_len & 0xFFFF))  # signed int16 the engine reads
        fail = "reads a NEGATIVE length -> OverflowException" if engine_len < 0 else \
               f"reads only {engine_len:,} B -> stream desync -> \"Source array was not long enough\""
        print(f"    - {name} entry id={ent['entryId']}: {true_len:,} B true; engine {fail}  [{kind}]")

    if not args.repair:
        print("\n  Diagnose only. Re-run with --repair to write a fixed copy (resets the war meter, keeps the campaign).")
        return 2

    # Repair: reset each oversized momentum string (or, with --force, any oversized string) to "".
    to_reset = [(n, i) for n, i, e, t, m in oversized if m or (args.force and t is not None)]
    refused = [o for o in oversized if (o[0], o[1]) not in to_reset]
    if refused:
        for name, idx, ent, text, mom in refused:
            what = "unrecognized string (use --force to reset anyway)" if text is not None else \
                   "NON-string data — cannot safely reset (this is not the momentum bug)"
            print(f"\n  REFUSING to repair {name} entry id={ent['entryId']}: {what}")
        if not to_reset:
            return 3

    for name, idx in to_reset:
        archives[name][1][idx]["data"] = EMPTY_STRING_DATA

    # Rebuild raw GameData. Strings is ALWAYS the last GameData section (GameData.Write order:
    # Header, ObjectData, ContainerData, Strings), so everything up to its length prefix is
    # byte-identical to the original — only the last section is re-serialized. O(n) splice.
    strings_start = next(s for n, s, e in sections if n == "Strings")
    new_strings = serialize_archive(*archives["Strings"])
    fixed_raw = raw[:strings_start - 4] + struct.pack("<i", len(new_strings)) + new_strings

    # Sanity: the rebuilt region must re-walk cleanly and carry no oversized entries.
    try:
        check_sections = split_sections(fixed_raw)
        cs = next((s, e) for n, s, e in check_sections if n == "Strings")
        _, ents = parse_archive(fixed_raw[cs[0]:cs[1]])
        assert all(len(x["data"]) <= INT16_MAX for x in ents)
    except Exception as ex:
        print(f"\n  ERROR: rebuilt save failed self-check ({ex}) — NOT writing output. Original untouched.")
        return 3

    comp = zlib.compressobj(6, zlib.DEFLATED, -15)
    body = comp.compress(fixed_raw) + comp.flush()
    out_path = os.path.splitext(args.path)[0] + "_fixed.sav"
    with open(out_path, "wb") as f:
        f.write(meta_bytes)
        f.write(body)
    print(f"\n  REPAIRED -> {out_path}")
    print(f"    reset {len(to_reset)} momentum entr{'y' if len(to_reset)==1 else 'ies'}; "
          f"war-meter history cleared, campaign intact. Loads on the vanilla engine.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
