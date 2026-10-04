"""Estimate the in-memory size of each TAOM module sound when FMOD loads it as a sample (read-only).

Usage: module_sound_pcm.py <TAOM repo root>

FMOD's createSound without CREATESTREAM or CREATECOMPRESSEDSAMPLE decodes the whole file into memory
(module-sounds.md). The kind comes from the file header, never the extension:
- WAV format tag 1 (PCM): the data as stored.
- WAV format tag 17 (IMA ADPCM): four bits a sample, so 16-bit PCM is about four times the data chunk.
- MP3 (an ID3 tag or a frame sync at the start): duration from the first frame's bitrate (CBR assumed;
  a Xing, Info or VBRI header is flagged), then 16-bit PCM at the stream's rate and channels.
- Anything else (OGG, other WAV tags): not estimated, listed.
Also marks which files module_sounds.xml references.
"""
import glob
import os
import re
import struct
import sys

ROOT = sys.argv[1]
SOUNDS = os.path.join(ROOT, "Main", "_Module", "ModuleSounds")
XML = os.path.join(ROOT, "Main", "_Module", "ModuleData", "module_sounds.xml")

BITRATES = {1: 32, 2: 40, 3: 48, 4: 56, 5: 64, 6: 80, 7: 96, 8: 112, 9: 128, 10: 160, 11: 192, 12: 224,
            13: 256, 14: 320}  # MPEG1 layer III, kbps
RATES = {0: 44100, 1: 48000, 2: 32000}


def riff_chunks(data):
    """Yield (id, offset, size) for the RIFF/WAVE chunks."""
    i = 12
    while i + 8 <= len(data):
        cid, size = data[i:i + 4], struct.unpack_from("<I", data, i + 4)[0]
        yield cid, i + 8, size
        i += 8 + size + (size & 1)


def wav_kind(path):
    data = open(path, "rb").read()
    fmt = None
    data_size = None
    for cid, off, size in riff_chunks(data):
        if cid == b"fmt ":
            tag, chans, rate, _, _, bits = struct.unpack_from("<HHIIHH", data, off)
            fmt = (tag, chans, rate, bits)
        elif cid == b"data":
            data_size = size
    if fmt is None or data_size is None:
        return "wav unparsed", None
    tag, chans, rate, bits = fmt
    if tag == 1:
        return f"wav-pcm {chans}ch {bits}bit {rate}", data_size
    if tag == 17:
        return f"wav-adpcm {chans}ch {rate}", data_size * 4
    return f"wav-tag{tag} {chans}ch {rate}", None


def mp3_kind(path, size):
    data = open(path, "rb").read(1 << 20)
    i = 0
    if data[:3] == b"ID3":
        i = 10 + ((data[6] << 21) | (data[7] << 14) | (data[8] << 7) | data[9])
        if i + 4 > len(data):
            data = open(path, "rb").read(i + 4096)
    # The first frame must start at i (after the tag) or within a short padding run.
    for j in range(i, min(i + 4096, len(data) - 4)):
        if data[j] == 0xFF and (data[j + 1] & 0xE0) == 0xE0:
            b1, b2, b3 = data[j + 1], data[j + 2], data[j + 3]
            br, rate = BITRATES.get(b2 >> 4), RATES.get((b2 >> 2) & 3)
            if ((b1 >> 3) & 3) == 3 and ((b1 >> 1) & 3) == 1 and br and rate:
                chans = 1 if (b3 >> 6) == 3 else 2
                vbr = any(t in data[j:j + 200] for t in (b"Xing", b"Info", b"VBRI"))
                secs = size * 8 / (br * 1000)
                return f"mp3 {br}kbps {rate} {chans}ch" + (" VBR-header" if vbr else ""), int(secs * rate * chans * 2)
        elif data[j] != 0:
            break
    return "mp3 unparsed", None


refs = open(XML, encoding="utf-8").read()
paths = {m.replace("\\", "/").lower() for m in re.findall(r'path="([^"]+)"', refs)}
rows = []
for p in glob.glob(os.path.join(SOUNDS, "**", "*"), recursive=True):
    if not os.path.isfile(p):
        continue
    rel = os.path.relpath(p, SOUNDS).replace("\\", "/")
    size = os.path.getsize(p)
    head = open(p, "rb").read(4)
    if head == b"RIFF":
        kind, decoded = wav_kind(p)
    elif head[:3] == b"ID3" or (head[0] == 0xFF and (head[1] & 0xE0) == 0xE0):
        kind, decoded = mp3_kind(p, size)
    elif head == b"OggS":
        kind, decoded = "ogg", None
    else:
        kind, decoded = f"unknown {head!r}", None
    rows.append((decoded or 0, size, rel, kind, rel.lower() in paths, decoded is None))

rows.sort(reverse=True)
print(f"files {len(rows)}  on disk {sum(r[1] for r in rows) / 1048576:.1f} MiB  "
      f"decoded (estimated where possible) {sum(r[0] for r in rows) / 1048576:.1f} MiB")
print(f"module_sounds.xml path entries: {len(paths)}; files referenced: {sum(1 for r in rows if r[4])}")
for decoded, size, rel, kind, referenced, _ in rows[:20]:
    print(f"{decoded / 1048576:7.1f} MiB decoded  {size / 1048576:6.1f} MiB file  "
          f"{'ref' if referenced else 'unref'}  {kind}  {rel}")
summary = {}
for decoded, size, _, kind, _, unknown in rows:
    s = summary.setdefault(kind.split()[0], [0, 0, 0, 0])
    s[0] += 1
    s[1] += size
    s[2] += decoded
    s[3] += unknown
for k, (n, s, d, u) in sorted(summary.items()):
    print(f"{k}: {n} files, {s / 1048576:.1f} MiB on disk, {d / 1048576:.1f} MiB decoded"
          + (f" ({u} not estimated)" if u else ""))
reg = [r for r in rows if r[4] and not r[2].lower().startswith("lotr/ost/")]
print("largest registered sound outside LOTR/OST:", f"{reg[0][0] / 1048576:.1f} MiB decoded, {reg[0][3]}, {reg[0][2]}")
