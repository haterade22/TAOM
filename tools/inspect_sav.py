#!/usr/bin/env python3
"""
Offline Bannerlord .sav metadata inspector — stdlib only, no game required.

Format (verified against the decompiled v1.4.6 engine):
  MetaData.Serialize (TaleWorlds.SaveSystem/MetaData.cs:46-52): [int32 LE length N]
  [N bytes UTF-8 JSON] with the key/value dict under a "List" property (line 11).
  FileDriver.Save (FileDriver.cs:32-39) then appends a RAW deflate stream
  (System.IO.Compression.DeflateStream, RFC1951 — no zlib header) wrapping
  GameData.Write (GameData.cs:106-135), little-endian BinaryWriter, in order:
    Header(len+bytes), ObjectData(count, then len+bytes each),
    ContainerData(count, then len+bytes each), Strings(len+bytes).
  Pre-v1.1.0 saves use a legacy uncompressed layout (FileDriver.Load) — unsupported here.

Usage:
  python tools/inspect_sav.py <path.sav> [--verify]
Exit codes: 0 ok, 1 unreadable metadata, 2 data-region verification failed.
"""
import argparse, datetime, json, re, struct, sys, zlib

TAOM_MARKERS = ("TAOM", "LOTRLOME")


def fmt_creation_time(val):  # DateTime string OR .NET ticks (Core/MetaDataExtensions.cs:14-21)
    try:
        ticks = int(val)
        return (datetime.datetime(1, 1, 1) + datetime.timedelta(microseconds=ticks / 10)).strftime("%Y-%m-%d %H:%M:%S")
    except (ValueError, OverflowError):
        return val


def read_metadata(data):
    (n,) = struct.unpack_from("<i", data, 0)
    if n <= 0 or 4 + n > len(data):
        raise ValueError(f"metadata length {n} out of range (file is {len(data)} bytes)")
    meta = json.loads(data[4:4 + n].decode("utf-8"))
    return meta.get("List", meta), 4 + n


def verify_gamedata(data, off, app_version):
    m = re.match(r"[a-z]?(\d+)\.(\d+)", app_version)  # e.g. "v1.2.10.55531" / "e1.0.x"
    if m and (int(m.group(1)), int(m.group(2))) < (1, 1):
        return f"legacy pre-v1.1.0 save — data region present ({len(data) - off} bytes), integrity check unsupported", 0
    d = zlib.decompressobj(-15)
    try:
        raw = d.decompress(data[off:])
    except zlib.error as e:
        return f"CORRUPT: deflate error in data region starting at file offset {off}: {e}", 2
    if not d.eof:
        return f"TRUNCATED: deflate stream incomplete ({len(data) - off} compressed bytes from offset {off})", 2
    pos, sizes = 0, []
    try:
        for section, counted in (("Header", False), ("ObjectData", True), ("ContainerData", True), ("Strings", False)):
            count = 1
            if counted:
                (count,) = struct.unpack_from("<i", raw, pos)
                pos += 4
            total = 0
            for i in range(count):
                (n,) = struct.unpack_from("<i", raw, pos)
                pos += 4
                if n < 0 or pos + n > len(raw):
                    return f"CORRUPT: {section}[{i}] length {n} at decompressed offset {pos - 4} exceeds region ({len(raw)} bytes)", 2
                pos += n
                total += n
            sizes.append(f"{section}={total}b" + (f"({count} blocks)" if counted else ""))
    except struct.error:
        return f"CORRUPT: unexpected end of decompressed data at offset {pos} ({len(raw)} bytes total)", 2
    trailing = (len(raw) - pos) + len(d.unused_data)
    tail = f", {trailing} unexpected trailing byte(s)" if trailing else ""
    return f"OK: {len(data) - off} compressed -> {len(raw)} bytes; " + ", ".join(sizes) + tail, 2 if trailing else 0


def main():
    p = argparse.ArgumentParser(description="Inspect Bannerlord .sav metadata offline")
    p.add_argument("path")
    p.add_argument("--verify", action="store_true", help="walk the deflated GameData region")
    args = p.parse_args()
    data = open(args.path, "rb").read()
    try:
        meta, data_off = read_metadata(data)
    except Exception as e:
        print(f"ERROR: unreadable metadata: {e}")
        return 1
    for key in ("ApplicationVersion", "CharacterName", "MainHeroLevel", "DayLong", "TAOM_Build"):
        if key in meta:
            print(f"{key:20} {meta[key]}")
    if "CreationTime" in meta:
        print(f"{'CreationTime':20} {fmt_creation_time(meta['CreationTime'])}")
    modules = meta.get("Modules", "")
    print(f"\nModules ({len(modules.split(';')) if modules else 0}):")
    for mod in filter(None, modules.split(";")):
        mark = "*" if any(t in mod.upper() for t in TAOM_MARKERS) else " "
        print(f"  {mark} {mod:40} {meta.get('Module_' + mod, '?')}")
    if args.verify:
        verdict, code = verify_gamedata(data, data_off, meta.get("ApplicationVersion", ""))
        print(f"\nData region: {verdict}")
        return code
    return 0


if __name__ == "__main__":
    sys.exit(main())
