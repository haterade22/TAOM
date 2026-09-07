#!/usr/bin/env python3
"""Fail when a voice clip is too long for the slot the engine fires it from.

THE DEFECT CLASS
----------------
Bannerlord's voice system has no frequency, cooldown, weight or probability knob. A
`<voice_definition>` carries only `name`, `sound_and_collision_info_class`, `only_for_npcs` and
two pitch multipliers; each `<voice>` carries `type`, `path` and `face_anim`. The `weight` in
`module_sounds.xml` picks between takes inside one bark, it is not a firing chance. How often a
clip plays is decided entirely by WHICH `VoiceType` slot it is bound to, and the engine fires
each slot at its own fixed rate. Choosing the slot is choosing the frequency.

So the only way a mod gets this wrong is by binding a long clip to a slot the engine fires
constantly. That is what happened to the dwarves: `D1_Grunts1.wav` (7.93 s) and `D1_Grunts2.wav`
(8.77 s) sat on `Grunt`, `Pain` and `Focus`, and the warcries (up to 11.81 s) sat on `Yell`.
Those files are sound-set compilations holding several complete spoken lines, so players heard a
full spoken line every few seconds instead of a grunt. Reported from the field, 2026-09.

Nothing in the engine reports any of this. An over-long clip, an unresolvable path and an
invalid `sound_category` all fail silently, which is why it shipped.

WHAT IT CHECKS
--------------
1. Every voice definition named by `project.mbproj` exists, and every `path=` in it resolves to a
   real sound. A run that inspected nothing exits 2 rather than reporting success.
2. Every `type=` is one of the engine's 62 declared voice types. A misspelling or wrong casing is
   a dead binding AND silently escapes the length rule below, so it is a finding, never normalised.
3. Every `sound_category` is present and is one of the 21 Native declares. Native's own comment:
   "Sounds that dont have valid categories wont be played!"
4. No clip exceeds the duration cap Native documents for its category.
5. No clip on a frequently-fired slot exceeds that slot's own bar in `SLOT_MAX`. `Grunt` and `Stun`
   have zero managed call sites, so native fires them per melee exertion and per stagger; `Yell` is
   native-fired too but carries shouted barks, so its bar is looser. This is the rule that actually
   catches the defect class; the category caps alone are far too loose (a 3.9 s `mission_voice` clip
   is legal and still unbearable on `Grunt`, and an 8 s `mission_voice_shout` is legal on `Yell`).

Known violations that predate the rule live in the baseline file. Each baseline entry PINS the
duration it was accepted at, so re-cutting a baselined clip longer resurfaces it as a finding
rather than riding the old exemption.

EXIT CODES
----------
    0  clean
    1  a finding
    2  bad input, or a run that checked nothing

USAGE
-----
    python tools/audit_voice_clip_lengths.py
    python tools/audit_voice_clip_lengths.py --module-data <dir> --sounds <dir>
    python tools/audit_voice_clip_lengths.py --baseline ""      # ignore the baseline
"""
from __future__ import annotations

import argparse
import struct
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
DEFAULT_MODULE_DATA = REPO / "Main" / "_Module" / "ModuleData"
DEFAULT_SOUNDS = REPO / "Main" / "_Module" / "ModuleSounds"
DEFAULT_BASELINE = REPO / "tools" / "voice-clip-baseline.txt"

# Transcribed from the comment block at the top of Native's own
# `Modules/Native/ModuleData/module_sounds.xml` (verified against the installed 1.4.8 copy).
# `None` means the comment says "persistent" rather than a cap. These are authoring guidance: no
# enforcement was found anywhere readable in the shipping-client decompile, and what FMOD does
# with an over-cap clip (truncate, steal a voice, play through) is not established. The VALIDITY
# of the category is a different matter and IS load-bearing: Native states outright that a sound
# with an invalid category is never played.
CATEGORY_CAP = {
    "mission_ambient_bed": None,
    "mission_ambient_3d_big": None,
    "mission_ambient_3d_medium": None,
    "mission_ambient_3d_small": None,
    "mission_material_impact": 4.0,
    "mission_combat_trivial": 2.0,
    "mission_combat": 8.0,
    "mission_foley": 4.0,
    "mission_voice_shout": 8.0,
    "mission_voice": 4.0,
    "mission_voice_trivial": 4.0,
    "mission_siege_loud": 8.0,
    "mission_footstep": 1.0,
    "mission_footstep_run": 1.0,
    "mission_horse_gallop": 2.0,
    "mission_horse_walk": 1.0,
    "ui": 4.0,
    "alert": 10.0,
    "campaign_node": None,
    "campaign_bed": None,
    "music": None,
}

# The 62 declared voice types (docs/features/kingdom-voices.md "Voice types"). Identity is a
# STRING resolved through MBAPI.IMBVoiceManager.GetVoiceTypeIndex, so a misspelling or a casing
# slip is a dead binding, not a near miss. Membership is exact and case-sensitive on purpose.
VOICE_TYPES = {
    # Combat
    "Grunt", "Jump", "Yell", "Pain", "Death", "Stun", "Fear", "Climb", "Focus", "Debacle",
    "Victory", "HorseStop", "HorseRally", "Drown",
    # Formation identifiers
    "Infantry", "Cavalry", "Archers", "HorseArchers", "Everyone", "Mixed",
    # Generic orders
    "Move", "Follow", "Charge", "Advance", "FallBack", "Stop", "Retreat", "Mount", "Dismount",
    "FireAtWill", "HoldFire", "PickSpears", "PickDefault", "FaceEnemy", "FaceDirection",
    "UseSiegeWeapon", "UseLadders", "AttackGate", "CommandDelegate", "CommandUndelegate",
    # Formation orders
    "FormLine", "FormShieldWall", "FormLoose", "FormCircle", "FormSquare", "FormSkein",
    "FormColumn", "FormScatter",
    # DLC
    "BoardAtWill", "AvoidBoarding",
    # Multiplayer barks
    "MpDefend", "MpAttack", "MpHelp", "MpSpot", "MpThanks", "MpSorry", "MpAffirmative",
    "MpNegative", "MpRegroup",
    # Mount
    "Idle", "Neigh", "Collide",
}

# Slots the ENGINE fires, established from the decompile: `Grunt` and `Stun` have zero managed
# call sites in the 98 dumped assemblies OR in the three NavalDLC assemblies that dump omits
# entirely (decompiled and checked separately, 2026-09-06).
# `Pain` has exactly one managed call site (a narrow shield-penetration
# branch in Mission.cs), so the general per-hit pain bark is native too. A clip on any of these
# plays per melee exertion, per stagger or per hit, so it must be a short non-verbal sound.
# The bar is PER SLOT, because the frequently-fired slots do not all carry the same kind of sound.
# `Grunt`/`Stun`/`Pain` are wordless exertions; `Yell` is a shouted bark and legitimately longer.
# One shared constant is what let the original defect back in: with only Grunt/Stun/Pain barred,
# re-binding the 5.4 to 7.1s warcries to `Yell` passed cleanly, because `mission_voice_shout`
# allows 8s. That is half the reported bug, and the gate said OK (caught 2026-09-06).
#
# Calibrated against the sets with no reported symptom, so the reference data stays green:
#   Grunt/Stun/Pain  uruk_01's longest native-fired take is 1.71s  -> 2.0
#   Yell             dwarf Battlecries top out at 3.09s            -> 3.5
SLOT_MAX = {
    "Grunt": 2.0,
    "Stun": 2.0,
    "Pain": 2.0,
    "Yell": 3.5,
}

# A baselined clip may drift this much before the exemption stops applying. Covers re-encode
# jitter, not a re-cut.
BASELINE_TOLERANCE = 0.05

MPEG_BITRATE_V1_L3 = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0]
MPEG_BITRATE_V2_L3 = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0]
MPEG_SAMPLE_RATE = {3: [44100, 48000, 32000], 2: [22050, 24000, 16000], 0: [11025, 12000, 8000]}
MPEG_SAMPLES_PER_FRAME = {3: 1152, 2: 576, 0: 576}


class ClipError(ValueError):
    """A clip that cannot be measured. Always a finding, never a silent skip."""


def wav_duration(path: Path) -> float:
    """Duration from the fmt chunk's byte rate.

    Deliberately NOT Python's `wave` module: it accepts only PCM and WAVE_FORMAT_EXTENSIBLE and
    RAISES `wave.Error: unknown format: 17` on the IMA ADPCM files in this tree (verified on the
    uruk_01 set, Python 3.14). Byte rate over data size works for both PCM and ADPCM. For ADPCM
    the stored byte rate is the block average, so the zero-padded final block makes this a slight
    OVER-estimate (measured +1 to +3%), which is the conservative direction for a length gate.
    """
    try:
        with path.open("rb") as fh:
            head = fh.read(12)
            if head[:4] != b"RIFF" or head[8:12] != b"WAVE":
                raise ClipError("not a RIFF/WAVE file: {}".format(path))
            byte_rate = None
            while True:
                hdr = fh.read(8)
                if len(hdr) < 8:
                    break
                cid, size = hdr[:4], struct.unpack("<I", hdr[4:8])[0]
                if cid == b"fmt ":
                    body = fh.read(size)
                    if len(body) < 16:
                        raise ClipError("truncated fmt chunk: {}".format(path))
                    byte_rate = struct.unpack("<I", body[8:12])[0]
                    if byte_rate == 0:
                        raise ClipError("fmt chunk declares a zero byte rate: {}".format(path))
                    fh.seek(size & 1, 1)
                elif cid == b"data":
                    if byte_rate is None:
                        raise ClipError("data chunk before fmt chunk: {}".format(path))
                    if size == 0:
                        raise ClipError("empty data chunk (silent clip): {}".format(path))
                    return size / byte_rate
                else:
                    fh.seek(size + (size & 1), 1)
    except (struct.error, OSError) as exc:
        raise ClipError("unreadable WAV {}: {}".format(path, exc))
    raise ClipError("no data chunk: {}".format(path))


def _mpeg_frame_header(data: bytes, i: int):
    """Decode an MPEG audio frame header at i, or None if it is not a usable one."""
    if data[i] != 0xFF or (data[i + 1] & 0xE0) != 0xE0:
        return None
    ver = (data[i + 1] >> 3) & 3
    layer = (data[i + 1] >> 1) & 3
    br_idx = (data[i + 2] >> 4) & 15
    sr_idx = (data[i + 2] >> 2) & 3
    if ver == 1 or layer != 1 or sr_idx == 3 or br_idx in (0, 15):
        return None
    kbps = (MPEG_BITRATE_V1_L3 if ver == 3 else MPEG_BITRATE_V2_L3)[br_idx]
    if not kbps:
        return None
    return ver, kbps, MPEG_SAMPLE_RATE[ver][sr_idx]


def mp3_duration(path: Path) -> float:
    """Duration from the first MPEG frame, honouring a Xing/Info VBR header when present.

    38 files in this tree carry a Xing/Info frame, so the CBR shortcut alone is not safe: a VBR
    clip measured at its nominal first-frame bitrate can read SHORT, which would let an over-long
    take slip under the gate.
    """
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise ClipError("unreadable MP3 {}: {}".format(path, exc))

    start = 0
    if data[:3] == b"ID3":
        size = ((data[6] & 0x7F) << 21) | ((data[7] & 0x7F) << 14) \
            | ((data[8] & 0x7F) << 7) | (data[9] & 0x7F)
        start = 10 + size
        if data[5] & 0x10:          # a footer is present
            start += 10
    end = len(data)
    if len(data) > 128 and data[-128:-125] == b"TAG":
        end -= 128

    i = start
    while i < end - 4:
        hdr = _mpeg_frame_header(data, i)
        if hdr:
            ver, kbps, sr = hdr
            # A Xing (VBR) or Info (CBR) tag sits inside this first frame and carries the true
            # frame count, which is exact for both.
            frame = data[i:i + 200]
            for tag in (b"Xing", b"Info"):
                p = frame.find(tag)
                if p < 0:
                    continue
                flags_at = i + p + 4
                if flags_at + 8 > len(data):
                    break
                flags = struct.unpack(">I", data[flags_at:flags_at + 4])[0]
                if flags & 1:       # frame count present
                    frames = struct.unpack(">I", data[flags_at + 4:flags_at + 8])[0]
                    if frames:
                        return frames * MPEG_SAMPLES_PER_FRAME[ver] / sr
                break
            return (end - i) * 8 / (kbps * 1000)
        i += 1
    raise ClipError("no MPEG audio frame found: {}".format(path))


def ogg_duration(path: Path) -> float:
    """Duration from the last Ogg page's granule position over the stream sample rate.

    Native's own comment says .ogg and .wav are the supported formats, and the authoring guide
    tells authors to prefer OGG, so refusing to measure one would red-gate correct data.
    """
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise ClipError("unreadable OGG {}: {}".format(path, exc))
    if data[:4] != b"OggS":
        raise ClipError("not an Ogg stream: {}".format(path))

    head = data.find(b"\x01vorbis")
    if head < 0 or head + 16 > len(data):
        raise ClipError("no Vorbis identification header: {}".format(path))
    rate = struct.unpack("<I", data[head + 12:head + 16])[0]
    if not rate:
        raise ClipError("Vorbis header declares a zero sample rate: {}".format(path))

    last = data.rfind(b"OggS")
    if last < 0 or last + 14 > len(data):
        raise ClipError("no final Ogg page: {}".format(path))
    granule = struct.unpack("<q", data[last + 6:last + 14])[0]
    if granule <= 0:
        raise ClipError("final Ogg page has no granule position: {}".format(path))
    return granule / rate


def duration_of(path: Path) -> float:
    suffix = path.suffix.lower()
    if suffix == ".wav":
        return wav_duration(path)
    if suffix == ".mp3":
        return mp3_duration(path)
    if suffix == ".ogg":
        return ogg_duration(path)
    raise ClipError("unsupported audio format: {}".format(path))


def parse_module_sounds(path: Path):
    """name -> (sound_category, [relative audio paths]). Duplicates are reported, not merged."""
    root = ET.parse(path).getroot()
    out = {}
    duplicates = []
    for node in root.iter("module_sound"):
        name = node.get("name")
        if not name:
            continue
        category = node.get("sound_category")
        variations = [v.get("path") for v in node.findall("variation") if v.get("path")]
        if not variations and node.get("path"):
            variations = [node.get("path")]
        if name in out:
            duplicates.append(name)
        out[name] = (category, variations)
    return out, duplicates


def load_baseline(path):
    """(slot, relpath) -> pinned duration.

    Line format: `Slot|relative/path.wav@4.15  # optional note`. The pin is mandatory: a bare
    (slot, path) exemption would let a re-cut clip of any length ride the old acceptance, which
    is the likeliest route back to the bug this tool exists to catch.
    """
    if path is None or not path.is_file():
        return {}
    accepted = {}
    for lineno, raw in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        line = raw.split("#", 1)[0].strip()
        if not line:
            continue
        slot, sep, rest = line.partition("|")
        if not sep:
            raise ValueError("{}:{}: expected 'Slot|path@seconds', got {!r}".format(
                path.name, lineno, raw.strip()))
        rel, at, pin = rest.rpartition("@")
        if not at:
            raise ValueError("{}:{}: baseline entry has no @<seconds> pin: {!r}".format(
                path.name, lineno, raw.strip()))
        try:
            seconds = float(pin)
        except ValueError:
            raise ValueError("{}:{}: pin {!r} is not a number".format(path.name, lineno, pin))
        accepted[(slot.strip(), rel.strip().replace("\\", "/"))] = seconds
    return accepted


def discover_voice_defs(module_data: Path):
    """The voice definitions project.mbproj actually registers.

    Driven off the registration rather than a glob, so a renamed or moved file collapses the run
    to exit 2 instead of quietly checking fewer files. Same reasoning as the validator's
    UPGRADE_INDEX_EMPTY gate: a check that inspects nothing reads exactly like a clean run.
    """
    proj = module_data / "project.mbproj"
    if not proj.is_file():
        raise FileNotFoundError("project.mbproj not found: {}".format(proj))
    module_root = module_data.parent
    found, missing = [], []
    for node in ET.parse(proj).getroot().iter("file"):
        if node.get("type") != "voice_definitions":
            continue
        name = (node.get("name") or "").replace("\\", "/")
        candidate = module_root / name
        (found if candidate.is_file() else missing).append(candidate)
    return found, missing


def main() -> int:
    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--module-data", type=Path, default=DEFAULT_MODULE_DATA)
    ap.add_argument("--sounds", type=Path, default=DEFAULT_SOUNDS)
    ap.add_argument("--baseline", default=str(DEFAULT_BASELINE),
                    help='baseline file of accepted violations; pass "" to ignore it')
    args = ap.parse_args()

    if not args.module_data.is_dir():
        print("ERROR: ModuleData not found: {}".format(args.module_data), file=sys.stderr)
        return 2
    if not args.sounds.is_dir():
        print("ERROR: ModuleSounds not found: {}".format(args.sounds), file=sys.stderr)
        return 2

    sounds_xml = args.module_data / "module_sounds.xml"
    if not sounds_xml.is_file():
        print("ERROR: module_sounds.xml not found: {}".format(sounds_xml), file=sys.stderr)
        return 2

    try:
        registry, dup_names = parse_module_sounds(sounds_xml)
        voice_defs, missing_defs = discover_voice_defs(args.module_data)
        baseline = load_baseline(Path(args.baseline) if args.baseline else None)
    except (ET.ParseError, FileNotFoundError, ValueError) as exc:
        print("ERROR: {}".format(exc), file=sys.stderr)
        return 2

    if missing_defs:
        for p in missing_defs:
            print("ERROR: project.mbproj registers a voice definition that is missing: {}".format(p),
                  file=sys.stderr)
        return 2
    if not voice_defs:
        print("ERROR: project.mbproj registers no voice_definitions files", file=sys.stderr)
        return 2

    durations = {}
    findings = ["module_sounds.xml defines '{}' more than once (last one wins silently)".format(n)
                for n in dup_names]
    accepted = []
    checked = 0

    for vd in voice_defs:
        try:
            root = ET.parse(vd).getroot()
        except ET.ParseError as exc:
            print("ERROR: {} does not parse: {}".format(vd.name, exc), file=sys.stderr)
            return 2
        for definition in root.iter("voice_definition"):
            dname = definition.get("name", "?")
            for voice in definition.findall("voice"):
                slot = voice.get("type", "")
                ref = voice.get("path")
                if not slot:
                    findings.append("{}: {} has a <voice> with no type".format(vd.name, dname))
                    continue
                if slot not in VOICE_TYPES:
                    findings.append(
                        "{}: {}/{} is not one of the engine's 62 declared voice types. Voice-type "
                        "identity is an exact string, so this binding is dead and it also escapes "
                        "the length rules".format(vd.name, dname, slot))
                    continue
                if not ref:
                    findings.append("{}: {}/{} has no path attribute".format(vd.name, dname, slot))
                    continue

                if ref.startswith("event:/"):
                    # A vanilla FMOD event from Native's compiled voice.bank. Valid, and not
                    # measurable from disk, so there is nothing to check.
                    continue

                if ref in registry:
                    category, rels = registry[ref]
                else:
                    findings.append(
                        "{}: {}/{} path '{}' has no <module_sound> of that name. Only a registered "
                        "module sound is resolvable in engine".format(vd.name, dname, slot, ref))
                    continue

                if not category:
                    findings.append(
                        "{}: {}/{} -> '{}' has no sound_category. Native: sounds without a valid "
                        "category are never played".format(vd.name, dname, slot, ref))
                    continue
                if category not in CATEGORY_CAP:
                    findings.append(
                        "{}: {}/{} -> '{}' has sound_category '{}', which is not one of the 21 "
                        "Native declares, so the engine will never play it".format(
                            vd.name, dname, slot, ref, category))
                    continue
                if not rels:
                    findings.append(
                        "{}: {}/{} -> '{}' has no variations".format(vd.name, dname, slot, ref))
                    continue

                for rel in rels:
                    key = rel.replace("\\", "/")
                    audio = args.sounds / rel
                    if not audio.is_file():
                        findings.append("{}: {}/{} -> '{}' -> missing file {}".format(
                            vd.name, dname, slot, ref, rel))
                        continue
                    if key not in durations:
                        try:
                            durations[key] = duration_of(audio)
                        except ClipError as exc:
                            durations[key] = None
                            findings.append("{}: {}/{} -> {}".format(vd.name, dname, slot, exc))
                    secs = durations[key]
                    if secs is None:
                        continue
                    checked += 1

                    pin = baseline.get((slot, key))
                    exempt = pin is not None and secs <= pin + BASELINE_TOLERANCE
                    drifted = pin is not None and not exempt

                    cap = CATEGORY_CAP[category]
                    if cap is not None and secs > cap:
                        msg = ("{}/{}: {} is {:.2f}s, over the {:.0f}s cap for sound_category "
                               "'{}'".format(dname, slot, key, secs, cap, category))
                        (accepted if exempt else findings).append(
                            msg + (" [was baselined at {:.2f}s and has since grown]".format(pin)
                                   if drifted else ""))

                    slot_max = SLOT_MAX.get(slot)
                    if slot_max is not None and secs > slot_max:
                        msg = ("{}/{}: {} is {:.2f}s. '{}' is fired by the engine often enough "
                               "that this is heard as chatter, so it must stay under "
                               "{:.1f}s".format(dname, slot, key, secs, slot, slot_max))
                        (accepted if exempt else findings).append(
                            msg + (" [was baselined at {:.2f}s and has since grown]".format(pin)
                                   if drifted else ""))

    print("Checked {} slot/clip bindings across {} registered voice definition file(s).".format(
        checked, len(voice_defs)))
    if accepted:
        print("\nACCEPTED (in baseline, {}):".format(len(accepted)))
        for line in accepted:
            print("  - {}".format(line))
    if findings:
        print("\nFINDINGS ({}):".format(len(findings)))
        for line in findings:
            print("  ! {}".format(line))
        print("\nA long clip on a frequently-fired slot is heard as constant chatter. Bind the")
        print("long take to a rare slot (Charge fires once per order, on one agent) or split it.")
        return 1

    # The coverage floor is checked LAST, and only once nothing else has anything to say. A
    # run whose every binding was itself a finding has measured zero clips but has still
    # inspected the data, and reporting that as bad input would bury the findings behind
    # exit 2. Caught by tools/tests/test_audit_voice_clip_lengths.py.
    if checked == 0:
        print("\nERROR: measured 0 clips and found nothing to report. The voice definitions "
              "registered by project.mbproj parsed to nothing, so this run proves nothing.",
              file=sys.stderr)
        return 2

    print("\nOK: every voice path resolves and every clip fits its slot.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
