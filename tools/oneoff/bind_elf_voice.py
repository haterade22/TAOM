#!/usr/bin/env python3
"""Bind the elf combat voice elf_01 to the adult male elf skin in the LOTRLOME_Armory skins.xml (#1568).

WHY
A humanoid agent takes its combat voice from the <voice_types> pool of its race's skin
(docs/features/kingdom-voices.md, route A). The adult male elf skin listed the vanilla male_02..male_08.
TAOM's own ModuleData/skins.xml merges by append, so an entry there would leave the seven vanilla voices in
the pool; the pool itself has to change, and it lives in the unversioned Armory.

WHAT IT CHANGES
In the one <race id="elf">, in its one <skin> with gender="0" and mesh_maturity_type="adult", the content of
<voice_types>: the seven vanilla entries become the single entry elf_01 (--revert puts the seven back).
Every other byte of the file stays. The adult woman, the young skins and the sauron race (a copy of the elf
skins) are not touched.

ORDER
Bind the live Armory only after the deployed TAOM defines elf_01. With the binding and no definition the engine
logs `Voice def can not be found "elf_01"` and puts a null entry in the pool; what an agent does with it is not
established (docs/features/kingdom-voices.md, Known defects). Until then, --revert --apply.

REFUSES when the file holds other than one elf race, the race other than one adult male skin, or the skin other
than one <voice_types>; when the pool is neither exactly male_02..male_08 nor exactly elf_01, or is not written
in the one layout this tool writes (an extra attribute, a comment, another line layout); when a parser reads the
target pool otherwise than the text scan; when the file does not parse, or any other skin's pool reads
differently after the edit; on --apply while the game or the Modding Kit runs; on --no-backup for the live file;
when the dated backup name already exists.

    python bind_elf_voice.py                         # dry run on the live Armory
    python bind_elf_voice.py --apply                 # back up, write, re-read, verify
    python bind_elf_voice.py --file docs/reference/lotrlome-armory-snapshot/skins.xml --apply --no-backup
    python bind_elf_voice.py --revert --apply        # the seven vanilla voices again

I/O is the full binary round trip (tools/README.md "XML I/O convention", idiom B). The backup is
<file>.bak-elf-voice-<stamp>, never a .xml extension; --no-backup is for a file git already keeps. Idempotent.
RE-RUN CONDITION: an Armory update overwrites skins.xml. The gate is VoiceBindingDataTests in TAOM.Tests.
"""
import argparse
import datetime as dt
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
_env = os.environ.get("BANNERLORD_GAME_DIR", "")
LIVE = os.path.join(_env if _env.strip() else DEFAULT_GAME,
                    "Modules", "LOTRLOME_Armory", "ModuleData", "skins.xml")

RACE = "elf"
VANILLA = ["male_%02d" % i for i in range(2, 9)]
BOUND = ["elf_01"]

_COMMENT = re.compile(r"<!--.*?-->", re.S)
_RACE_TAG = re.compile(r'<race\b[^>]*\bid\s*=\s*"%s"[^>]*>' % RACE)
_SKIN_TAG = re.compile(r"<skin\b[^>]*>")
_POOL = re.compile(r"<voice_types\s*>(.*?)</voice_types\s*>", re.S)
_ENTRY_NAME = re.compile(r'<voice_type\b[^>]*?(?<![\w.:-])name\s*=\s*"([^"]*)"')


class Refused(Exception):
    pass


def game_or_kit_running():
    try:
        raw = subprocess.run(["tasklist", "/FO", "CSV", "/NH"], capture_output=True,
                             timeout=20, check=True).stdout
    except (OSError, subprocess.SubprocessError) as exc:
        print("WARNING: could not list processes (%s); refusing to write" % exc)
        return True
    out = (raw or b"").decode("ascii", errors="replace")
    return not out or any(k in out for k in ("TaleWorlds.MountAndBlade", "Bannerlord"))


def _is_adult_male(get):
    return get("gender") == "0" and get("mesh_maturity_type") == "adult"


def _tag_attr(tag, name):
    m = re.search(r'(?<![\w.:-])%s\s*=\s*"([^"]*)"' % re.escape(name), tag)
    return m.group(1) if m else None


def _rows(names, indent, nl):
    """The one layout this tool reads and writes: an entry per name, the name on its own line."""
    return "".join('%s\t<voice_type%s%s\t\tname="%s" />%s' % (indent, nl, indent, n, nl) for n in names)


def _pools_as_read(data):
    """[((race id, skin position), is the target skin, pool)] in file order, as a parser reads the file."""
    try:
        root = ET.fromstring(data)
    except ET.ParseError as exc:
        raise Refused("the file does not parse: %s" % exc)
    out = []
    for race in root.iter("race"):
        for at, skin in enumerate(race.findall("skin")):
            target = race.get("id") == RACE and _is_adult_male(skin.get)
            out.append(((race.get("id"), at), target, [v.get("name") for v in skin.iter("voice_type")]))
    return out


def _pool_span(text):
    """(start, end) of the text between the target skin's <voice_types> tags, and the tag's own start."""
    # Comments are blanked to spaces of the same length, so a commented race, skin or pool matches nothing
    # and every offset is also an offset into the real text.
    masked = _COMMENT.sub(lambda m: " " * len(m.group(0)), text)
    races = list(_RACE_TAG.finditer(masked))
    if len(races) != 1:
        raise Refused('%d <race id="%s"> found, expected one' % (len(races), RACE))
    race_end = masked.find("</race>", races[0].end())
    if race_end < 0:
        raise Refused("the %s race has no </race>" % RACE)
    spans = []
    for tag in _SKIN_TAG.finditer(masked, races[0].end(), race_end):
        if not _is_adult_male(lambda name, t=tag.group(0): _tag_attr(t, name)):
            continue
        close = masked.find("</skin>", tag.end(), race_end)
        if close < 0:
            raise Refused("the adult male %s skin has no </skin>" % RACE)
        spans.append((tag.end(), close))
    if len(spans) != 1:
        raise Refused("%d adult male %s skins found, expected one" % (len(spans), RACE))
    pools = list(_POOL.finditer(masked, spans[0][0], spans[0][1]))
    if len(pools) != 1:
        raise Refused("the adult male %s skin holds %d <voice_types>, expected one" % (RACE, len(pools)))
    start, end = pools[0].span(1)
    if text[start:end] != masked[start:end]:
        raise Refused("a comment sits inside the pool; edit it by hand")
    return start, end, pools[0].start()


def plan(data, bind):
    """(new bytes, or None when the pool already is the wanted one; the pool found). Pure."""
    text = data.decode("utf-8")
    start, end, tag_start = _pool_span(text)
    inner = text[start:end]
    names = _ENTRY_NAME.findall(inner)
    if len(names) != len(re.findall(r"<voice_type\b", inner)):
        raise Refused("a <voice_type> in the pool has no name")
    if names not in (VANILLA, BOUND):
        raise Refused("the pool is %r, neither the seven vanilla voices nor elf_01 alone" % (names,))

    nl = "\r\n" if "\r\n" in text else "\n"
    indent = text[text.rfind("\n", 0, tag_start) + 1:tag_start]
    if indent.strip():
        raise Refused("<voice_types> does not start its line")
    if inner != nl + _rows(names, indent, nl) + indent:
        raise Refused("the pool holds more than the known rows in the known layout; edit it by hand")

    # The text scan found the pool; a parser must agree before anything is called done or written.
    before = _pools_as_read(data)
    targets = [pool for _, target, pool in before if target]
    if targets != [names]:
        raise Refused("a parser reads the target pool as %r, the text scan as %r" % (targets, names))

    want = BOUND if bind else VANILLA
    if names == want:
        return None, names
    new = (text[:start] + nl + _rows(want, indent, nl) + indent + text[end:]).encode("utf-8")

    after = _pools_as_read(new)
    if [k for k, _, _ in before] != [k for k, _, _ in after]:
        raise Refused("the edit changed the list of skins")
    for (key, target, old), (_, _, now) in zip(before, after):
        if now != (want if target else old):
            raise Refused("after the edit skin %s reads %r" % (key, now))
    return new, names


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--file", default=LIVE)
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--revert", action="store_true")
    ap.add_argument("--no-backup", action="store_true", help="for a file git already keeps (the repo snapshot)")
    args = ap.parse_args(argv)
    bind = not args.revert
    if not os.path.isfile(args.file):
        print("ERROR: not found: %s" % args.file, file=sys.stderr)
        return 2
    try:
        data = open(args.file, "rb").read()
        new, names = plan(data, bind)
        print("file:  %s" % args.file)
        print("pool:  %s" % ", ".join(names))
        if new is None:
            print("already %s: nothing to do" % ("bound to elf_01" if bind else "the vanilla voices"))
            return 0
        print("plan:  %s" % ("the pool becomes elf_01 alone" if bind else "the pool becomes male_02..male_08"))
        if not args.apply:
            print("DRY RUN: nothing written; pass --apply to write")
            return 0
        if args.no_backup and os.path.normcase(os.path.abspath(args.file)) == os.path.normcase(os.path.abspath(LIVE)):
            raise Refused("--no-backup is for the repo snapshot; the live Armory file always gets a backup")
        if game_or_kit_running():
            print("REFUSED: the game or the Modding Kit runs; close it first", file=sys.stderr)
            return 2
        bak = None
        if not args.no_backup:
            bak = args.file + ".bak-elf-voice-" + dt.datetime.now().strftime("%Y%m%d-%H%M%S")
            if os.path.exists(bak):
                raise Refused("backup exists: " + bak)
            with open(bak, "xb") as fh:
                fh.write(data)
            if open(bak, "rb").read() != data:
                raise Refused("the backup does not read back")
        try:
            with open(args.file, "wb") as fh:
                fh.write(new)
            written = open(args.file, "rb").read()
            done = written == new and plan(written, bind)[0] is None
        except (OSError, Refused):
            done = False
        if not done:
            with open(args.file, "wb") as fh:
                fh.write(data)
            raise Refused("the written file does not re-plan as done; original bytes restored")
        print("written: %s" % args.file)
        if bak:
            print("backup:  %s" % bak)
        return 0
    except Refused as exc:
        print("REFUSED: %s" % exc, file=sys.stderr)
        return 1
    except ET.ParseError as exc:
        print("REFUSED: parse error: %s" % exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
