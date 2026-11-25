#!/usr/bin/env python3
"""
POST-DEPLOY verification gate for a rideable creature mount's deployed tpac/FBX assets.

Run this AFTER replacing any creature _anm/_geo tpac (Kit recompile of a Blender rework) and
BEFORE battle-testing. It catches the three ways a re-export silently breaks a working mount (a
creature entry may add checks of its own: the spider's body capsule, the chariot's rein skeleton
and its rider clips in every action set and in action_sets.xslt):

  1. SKELETON DROP   -- a mesh re-export ships mesh-only, dropping the Skeleton resource the
                        action_set names => CreateAgentSkeleton null => RIDERLESS mount (no crash,
                        easy to miss). The skeleton MUST exist as a Skeleton resource in a LIVE
                        (non-.backup) loose tpac, like the working elephant/warg.
  2. quad_movement   -- a Kit recompile drops the tag on a movement-bound clip => Skeleton.
       DROP            TickAnimations AV (+0x10) on the first mission tick in every mount context.
                        The tag is a record in the clip's usage list. A clip on a row that moves (a
                        direction or a turn) needs it; vanilla binds untagged clips on six of the
                        horse's seven in-place rows, so a listed (action, clip) pair there is exempt.
  3. PHANTOM BINDING -- a rename orphans an action_set animation= target => degenerate record =>
                        AV/null when resolved.

This is the executable form of the "Replacing FBX/tpac files" requirements in
docs/ai-includes/creature-mount-authoring.md. Read-only; prints PASS/FAIL per check; exit 1 on
any FAIL; exit 2 with an ERROR line when the install's XML is not there (nothing was checked).

Usage:
  python tools/verify_mount_assets.py spider
  python tools/verify_mount_assets.py elephant
  python tools/verify_mount_assets.py chariot
  python tools/verify_mount_assets.py --game "E:\\...\\Mount & Blade II Bannerlord" spider
"""
import argparse
import math
import os
import re
import struct
import sys
from _gamedir import game_dir

DEFAULT_GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
SKELETON_GUID = bytes.fromhex('d5a335c6bbeadd45883eaa57e4196113')
CLIP_GUID = bytes.fromhex('c809655063e5a44cb166a53b92e913a7')  # AnimationClip; a master can carry the same name
MAGIC = 0x43415054

# Per-creature config. action_set ids + usage-set id + the loose Assets subtree.
CREATURES = {
    "spider": {
        "asset_dir": r"Modules\LOTRLOME_Armory\Assets\creature\spider",
        "action_sets_xml": r"Modules\LOTRLOME_Armory\ModuleData\action_sets.xml",
        "usage_xml": r"Modules\LOTRLOME_Armory\ModuleData\monster_usage_sets.xml",
        "action_set_ids": ["as_spider", "as_spider_town_and_village", "as_spider_map"],
        "usage_id": "spider",
        "rider_prefix": "act_spider_",
        # #1541: each pace's direction="none" row plays the gait's in-place twin, whose record is the gait's own
        # with no clip usage. That is the vanilla horse's shape (gait_walkfast beside horse_walkfast: same Duration,
        # step points and flags, no usage; read from Native animation_clips.tpac, 2026-11-22) and the warg's stand
        # twins' below. The pair is the key: the same clip on a moving action still fails, and so does the pair
        # itself when its action sits on a row that moves.
        "untagged_ok": (
            ("act_spider_walk_stand", "spider_walk_inplace"),
            ("act_spider_trot_stand", "spider_trot_inplace"),
            ("act_spider_canter_stand", "spider_canter_inplace"),
            ("act_spider_run_stand", "spider_gallop_inplace"),
            ("act_spider_walk_backward_stand", "spider_walk_backwards_inplace"),     # #1550: pace 0's none row
        ),
        # The body capsule against the skeleton (capsule_findings). The hips are each leg's first long bone.
        "body": {
            "monster_xml": r"Modules\LOTRLOME_Armory\ModuleData\Monsters\LOTR\lotr_monster_spider.xml",
            "monster_id": "spider",
            "skeleton_tpac": r"Modules\LOTRLOME_Armory\Assets\creature\spider\animations\spider_correct_geo.tpac",
            "skeleton": "spider_skeleton",
            "hips": [f"joint{n}_{side}" for n in (41, 35, 29, 23) for side in ("l", "r")],
        },
    },
    "elephant": {
        "asset_dir": r"Modules\LOTRLOME_Armory\Assets\creature\elephant",
        "action_sets_xml": r"Modules\LOTRLOME_Armory\ModuleData\action_sets.xml",
        "usage_xml": r"Modules\LOTRLOME_Armory\ModuleData\monster_usage_sets.xml",
        "action_set_ids": ["as_elephant", "as_elephant_town_and_village", "as_elephant_map"],
        "usage_id": "elephant",
        "rider_prefix": "act_elephant_",
    },
    # Mûmakil = a SCALED-UP elephant that SHARES the elephant rig (skeleton="elephant_skeleton") + the
    # elephant's clip folder. Its action set is its own since #1474 (as_mumakil: as_elephant's rows, with the
    # gait, turn and idle rows on the mumakil_* clips). Its own mesh tpac (creature/mumakil) carries NO skeleton or
    # animation resources — those live in the elephant tree — so the skeleton/clip/binding checks search BOTH
    # dirs via extra_asset_dirs. The mumakil mesh MUST reference skeleton="elephant_skeleton" (rename from the
    # imported "elephant_skeleton_unused" on re-export, like the elephant's own mesh did) — verify the mesh tpac
    # separately with tools/tpac_skeleton_scan.py (this gate confirms the shared skeleton/clips are sound).
    "mumakil": {
        "asset_dir": r"Modules\LOTRLOME_Armory\Assets\creature\mumakil",
        "extra_asset_dirs": [r"Modules\LOTRLOME_Armory\Assets\creature\elephant"],
        "action_sets_xml": r"Modules\LOTRLOME_Armory\ModuleData\action_sets.xml",
        "usage_xml": r"Modules\LOTRLOME_Armory\ModuleData\monster_usage_sets.xml",
        "action_set_ids": ["as_mumakil", "as_mumakil_town_and_village", "as_mumakil_map"],
        "usage_id": "elephant",
        "rider_prefix": "act_elephant_",
    },
    # War chariot: one package, Assets/creature/chariot/chariot_rig_geo.tpac, holds the skeleton
    # horse_skeleton_chariot, its rein skeleton horse_chariot_rein, the meshes and every clip (the old rig kept one
    # _anm.tpac per clip beside chariot_correct_geo.tpac). The rider's clips (chariot_rider_*, map_chariot_*) are in
    # the same package, and no as_chariot set names one: the human sets get their rows from action_sets.xslt and the
    # dwarf sets hold theirs in action_sets.xml, so CHECK 3 reads both files for those two prefixes.
    "chariot": {
        "asset_dir": r"Modules\LOTRLOME_Armory\Assets\creature\chariot",
        "action_sets_xml": r"Modules\LOTRLOME_Armory\ModuleData\action_sets.xml",
        "usage_xml": r"Modules\LOTRLOME_Armory\ModuleData\monster_usage_sets.xml",
        # The Monster whose rein_skeleton must be in a live package (only the chariot's Monster names its own).
        "rein_monster_xml": r"Modules\LOTRLOME_Armory\ModuleData\Monsters\LOTR\lotr_monster_chariot.xml",
        "rider_rows_xslt": r"Modules\LOTRLOME_Armory\ModuleData\action_sets.xslt",
        "rider_clip_prefixes": ("chariot_rider_", "map_chariot_"),
        "action_set_ids": ["as_chariot", "as_chariot_town_and_village", "as_chariot_map"],
        "usage_id": "chariot",
        "rider_prefix": "act_chariot_",
        # The three in-place rows whose clips carry no quad_movement. The vanilla horse has the same shape on the
        # same rows: as_horse binds act_horse_backward_walk_stand, _forward_walk_stand and _forward_trot_stand to
        # gait_walk_backward, gait_walkfast and horse_gait_trot_2, none of which carries the tag (read per clip from
        # the Native packages, 2026-11-24; audit_mount_parity.VANILLA_UNTAGGED_MOVEMENT_ACTIONS holds all six).
        "untagged_ok": (
            ("act_chariot_backward_walk_stand", "chariot_gait_walk_backward"),
            ("act_chariot_forward_walk_stand", "chariot_gait_walkfast"),
            ("act_chariot_forward_trot_stand", "chariot_gait_trot"),
        ),
    },
    # Dire wolf (#1081): its own skeleton and clips, but the warg's usage set and action names.
    "dire_wolf": {
        "asset_dir": r"Modules\LOTRLOME_Armory\Assets\creature\direwolf",
        "action_sets_xml": r"Modules\LOTRLOME_Armory\ModuleData\action_sets.xml",
        "usage_xml": r"Modules\LOTRLOME_Armory\ModuleData\monster_usage_sets.xml",
        "action_set_ids": ["as_dire_wolf", "as_dire_wolf_town_and_village", "as_dire_wolf_map"],
        "usage_id": "warg",
        # (action, clip) rows exempt from the quad_movement check. These clips clone warg_trot_stand, warg_canter_stand
        # and warg_forward_gallop_left_foot_stand, which have no quad_movement either (byte scan, 2026-11-04). The pair
        # is the key: the same clip bound to a moving action still fails. gen_dire_wolf_anim_clips.ps1 -Verify holds
        # the flags to the warg twin. The walk's twin (#1565) is tools/dire_wolf_gait_plan.json's: a pace's "none" row
        # on an untagged in-place clip is vanilla's own shape (as_horse binds act_horse_forward_walk_stand to gait_walkfast).
        "untagged_ok": (
            ("act_warg_forward_walk_stand", "dire_wolf_walk_stand"),
            ("act_warg_forward_trot_stand", "dire_wolf_trot_stand"),
            ("act_warg_forward_canter_stand", "dire_wolf_canter_stand"),
            ("act_warg_forward_gallop_right_foot_stand", "dire_wolf_forward_gallop_left_foot_stand"),
            ("act_warg_forward_gallop_left_foot_stand", "dire_wolf_forward_gallop_left_foot_stand"),
        ),
    },
}


def tpac_items(d):
    """Return [(type_guid, name)] for every item in a tpac's bytes (TOC walk). [] on parse error."""
    if len(d) < 36 or struct.unpack_from('<I', d, 0)[0] != MAGIC:
        return []
    ver = struct.unpack_from('<I', d, 4)[0]
    num = struct.unpack_from('<I', d, 24)[0]
    pos = 36
    out = []
    try:
        for _ in range(num):
            type_guid = d[pos:pos + 16]; pos += 16
            pos += 16  # item_guid
            if ver > 1:
                pos += 4  # item_ver
            n = struct.unpack_from('<i', d, pos)[0]; pos += 4
            name = d[pos:pos + n].decode('utf-8', 'replace'); pos += n
            meta = struct.unpack_from('<q', d, pos)[0]; pos += 8 + meta
            pos += 8  # checksum
            segc = struct.unpack_from('<i', d, pos)[0]; pos += 4 + segc * 69
            udep = struct.unpack_from('<i', d, pos)[0]; pos += 4 + udep * 48
            out.append((type_guid, name))
    except Exception:
        return out
    return out


def clip_metas(d):
    """{clip name: its metadata bytes} for every AnimationClip item of a tpac's bytes. A clip's flags
    (quad_movement among them) are in its own metadata, so a package that holds many clips, as the war chariot's
    one rig package does, must be read per clip: a scan of the whole file finds another clip's tag. {} on a parse error."""
    if len(d) < 36 or struct.unpack_from('<I', d, 0)[0] != MAGIC:
        return {}
    ver = struct.unpack_from('<I', d, 4)[0]
    num = struct.unpack_from('<I', d, 24)[0]
    pos = 36
    out = {}
    try:
        for _ in range(num):
            type_guid = d[pos:pos + 16]
            pos += 32 + (4 if ver > 1 else 0)           # type guid, item guid, item version
            n = struct.unpack_from('<i', d, pos)[0]
            name = d[pos + 4:pos + 4 + n].decode('utf-8', 'replace')
            pos += 4 + n
            meta = struct.unpack_from('<q', d, pos)[0]
            if type_guid == CLIP_GUID:
                out[name] = d[pos + 8:pos + 8 + meta]
            pos += 8 + meta + 8                         # meta size, meta, checksum
            pos += 4 + struct.unpack_from('<i', d, pos)[0] * 69
            pos += 4 + struct.unpack_from('<i', d, pos)[0] * 48
    except Exception:
        return out
    return out


# Bytes a usage record holds after its name and an int32 0 (docs/reference/bannerlord-animation-system-map.md,
# "Clip usages": a vector is saved as x, y, z, 1.0). `particle` is not here: no clip in the game or the Armory has
# one, and its saved size is not measured. On 2026-11-24 a walk with these sizes ended exactly at the end of the
# metadata for all 6,177 vanilla clips and all 534 creature clips of the Armory.
USAGE_PAYLOAD = {b"displacement": 20, b"quad_movement": 12, b"bip_mov_ik": 28, b"blend": 8, b"mount_change": 8,
                 b"hand_switch": 12}


def quad_tagged(meta):
    """True when one clip's metadata holds a quad_movement record in its usage list. The fields before the list are
    walked in the engine's order (0x58C500, as tools/set_clip_balance_name.py does) and the records by their own
    sizes, to the last byte of the metadata. The same text in another field, or inside another record's numbers,
    is not a record. False for a layout before version 5, for a record kind with no known size, and for metadata
    that does not end where its records end: a clip that cannot be read is reported, not passed."""
    def text(p):
        n = struct.unpack_from('<i', meta, p)[0]
        if n < 0 or p + 4 + n > len(meta):
            raise ValueError("a string runs past the metadata")
        return p + 4 + n
    try:
        if struct.unpack_from('<I', meta, 0)[0] < 5:
            return False
        p = 4 + 28 + 16 + 16                    # version, 6 floats and an int, animation guid, step points
        for _ in range(5):                      # sound, voice, facial, blends with action, continue to action
            p = text(p)
        p = text(p + 8)                         # hand poses, then the combat parameter id
        p += 13                                 # blend in, blend out, do not interpolate, an int
        for _ in range(3):                      # field, source 1, source 2
            p = text(p)
        p += 7
        flags = struct.unpack_from('<i', meta, p)[0]
        if flags < 0:
            return False
        p += 4
        for _ in range(flags):
            p = text(p)
        count = struct.unpack_from('<i', meta, p)[0]
        p += 4
        found = False
        for _ in range(count):
            name_at, p = p, text(p)
            name = meta[name_at + 4:p]
            if name not in USAGE_PAYLOAD or struct.unpack_from('<i', meta, p)[0] != 0:
                return False
            p += 4 + USAGE_PAYLOAD[name]
            found = found or name == b"quad_movement"
    except (struct.error, ValueError):
        return False
    return found and p == len(meta)


CENTRE = 0.05          # metres: a bone this near the beast's middle plane is on its centre line
CAPSULE_SLACK = 0.10   # metres a capsule end may stop short of the last centre-line bone


def read_body_capsule(xml, monster_id):
    """(radius, pos1, pos2) of the monster's <body_capsule>, or None. Comments are dropped first."""
    xml = re.sub(r"<!--.*?-->", "", xml, flags=re.S)
    for m in re.finditer(r"<Monster\b([^>]*)>(.*?)</Monster>", xml, re.S):
        if not re.search(r'\bid="%s"' % re.escape(monster_id), m.group(1)):
            continue
        cap = re.search(r"<body_capsule\b([^>]*)/>", m.group(2), re.S)
        if not cap:
            return None
        at = dict(re.findall(r'(\w+)="([^"]*)"', cap.group(1)))
        try:
            return (float(at["radius"]), tuple(float(v) for v in at["pos1"].split(",")),
                    tuple(float(v) for v in at["pos2"].split(",")))
        except (KeyError, ValueError):
            return None
    return None


def capsule_findings(capsule, bones, hips):
    """Why a body capsule does not cover the beast's body from above. bones: the skeleton's rest pose as
    tools/tpac_skeleton_dump.py gives it (name, parent_idx, rest_frame: a column-major local 4x4), parents first;
    Y is forward. Four checks, and no more: along Y the capsule reaches from the foremost to the rearmost bone on
    the centre line, each end within CAPSULE_SLACK; its radius reaches the widest of `hips`; both ends sit on the
    centre line; both ends sit within the height the centre line spans. It does NOT prove that every bone is
    inside the capsule: under the spider's rear cap the abdomen's tip is 0.22 m outside it. The legs are not
    held: no mount's are. The body capsule is what keeps two beasts apart; on 2026-11-23 the spider's stopped
    1.7 m before its abdomen's tip."""
    radius, p1, p2 = capsule
    if not all(math.isfinite(v) for v in (radius,) + tuple(p1) + tuple(p2)) or radius <= 0 or len(p1) != 3 or len(p2) != 3:
        return ["its radius or an end is no finite number above 0 (radius %s, pos1 %s, pos2 %s)" % (radius, p1, p2)]
    world, at = [], {}
    for b in bones:
        f = b["rest_frame"]
        m = [[f[c * 4 + r] for c in range(4)] for r in range(4)]
        if b["parent_idx"] >= 0:
            p = world[b["parent_idx"]]
            m = [[sum(p[r][k] * m[k][c] for k in range(4)) for c in range(4)] for r in range(4)]
        world.append(m)
        at[b["name"]] = (m[0][3], m[1][3], m[2][3])
    out = ["hip bone %s is not on the skeleton" % h for h in hips if h not in at]
    centre = {n: p for n, p in at.items() if abs(p[0]) < CENTRE}
    if not centre:
        return out + ["the skeleton has no bone on its centre line"]
    first = max(centre, key=lambda n: centre[n][1])
    last = min(centre, key=lambda n: centre[n][1])
    front, rear = max(p1[1], p2[1]) + radius, min(p1[1], p2[1]) - radius
    if front < centre[first][1] - CAPSULE_SLACK:
        out.append("its front end is at y %.2f, %.2f m behind the foremost centre bone %s" % (
            front, centre[first][1] - front, first))
    if rear > centre[last][1] + CAPSULE_SLACK:
        out.append("its rear end is at y %.2f, %.2f m before the rearmost centre bone %s: that much of the body is "
                   "outside it, and two beasts walk into each other" % (rear, rear - centre[last][1], last))
    known = [h for h in hips if h in at]
    if known:
        wide = max(known, key=lambda h: abs(at[h][0]))
        if radius < abs(at[wide][0]) - 1e-6:
            out.append("its radius %.2f does not reach the hip %s, %.2f m from the middle" % (radius, wide, abs(at[wide][0])))
    elif not hips:
        out.append("no hip bone is named for it, so its radius is not checked")
    # where it is, not only how long and how wide: beside the body, above it or tilted through it is no hold
    low, high = min(p[2] for p in centre.values()), max(p[2] for p in centre.values())
    for name, p in (("pos1", p1), ("pos2", p2)):
        if abs(p[0]) > CAPSULE_SLACK:
            out.append("%s is %.2f m to the side of the centre line" % (name, abs(p[0])))
        if not low - CAPSULE_SLACK <= p[2] <= high + CAPSULE_SLACK:
            out.append("%s is at height %.2f, outside the centre line's %.2f to %.2f" % (name, p[2], low, high))
    return out


def body_findings(game, body):
    """(capsule or None, findings) for a creature's `body` entry: read_body_capsule and capsule_findings on the
    live files. A file that is missing or cannot be read is a finding, never a traceback."""
    import tpac_skeleton_dump as tsd
    try:
        with open(os.path.join(game, body["monster_xml"]), encoding="utf-8-sig") as fh:
            capsule = read_body_capsule(fh.read(), body["monster_id"])
        skel_path = os.path.join(game, body["skeleton_tpac"])
        _, seg, _ = tsd.find_skeleton(skel_path, body["skeleton"])
        bones = tsd.parse_skeleton_definition(tsd.read_segment_data(skel_path, seg))["bones"] if seg is not None else None
    except (OSError, AssertionError, struct.error, ValueError, KeyError) as err:
        return None, ["cannot read the monster file or the skeleton package (%s: %s)" % (type(err).__name__, err)]
    if capsule is None:
        return None, ["no <body_capsule> for %s" % body["monster_id"]]
    if bones is None:
        return capsule, ["no skeleton %s in %s" % (body["skeleton"], body["skeleton_tpac"])]
    return capsule, capsule_findings(capsule, bones, body["hips"])


def action_set_block(xml, set_id):
    """Return the text of the <action_set id="set_id"> ... </action_set> block (first match)."""
    m = re.search(r'<action_set\s+id="%s"[^>]*>(.*?)</action_set>' % re.escape(set_id), xml, re.S)
    return m.group(0) if m else None


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("creature", choices=sorted(CREATURES))
    ap.add_argument("--game", default=None, help="Bannerlord install root")
    parsed = ap.parse_args(argv)
    game = parsed.game or DEFAULT_GAME
    args = [parsed.creature]
    c = CREATURES[args[0]]
    asset_dir = os.path.join(game, c["asset_dir"])
    # Skeleton-sharing mounts (Mûmakil reuses the elephant rig+clips) search the donor dir too.
    asset_dirs = [asset_dir] + [os.path.join(game, d) for d in c.get("extra_asset_dirs", [])]
    for key in ("action_sets_xml", "usage_xml"):
        if not os.path.isfile(os.path.join(game, c[key])):      # exit 2: a gate that read nothing has not failed
            print(f"ERROR: {os.path.join(game, c[key])} not found (no install?)")
            sys.exit(2)
    as_xml = open(os.path.join(game, c["action_sets_xml"]), encoding="utf-8-sig").read()
    usage_xml = open(os.path.join(game, c["usage_xml"]), encoding="utf-8-sig").read()

    # live (non-.backup) loose tpacs under the creature asset dir (+ any shared donor dirs)
    live_tpacs = []
    for ad in asset_dirs:
        for dp, _, fn in os.walk(ad):
            for f in fn:
                if f.endswith(".tpac"):
                    live_tpacs.append(os.path.join(dp, f))
    tpac_bytes = {}
    items_by_tpac = {}
    for p in live_tpacs:
        try:
            with open(p, "rb") as fh:
                tpac_bytes[p] = fh.read()
        except OSError:
            tpac_bytes[p] = b""
        items_by_tpac[p] = tpac_items(tpac_bytes[p])
    fails = []
    print(f"=== verify_mount_assets: {args[0]} ===")
    extra = c.get("extra_asset_dirs")
    print(f"  asset dir: {asset_dir}  ({len(live_tpacs)} live tpac files"
          f"{', incl. shared: ' + ', '.join(extra) if extra else ''})")

    # ---- CHECK 1: SKELETON present in a live loose tpac ----
    main_block = action_set_block(as_xml, c["action_set_ids"][0])
    skel_m = re.search(r'skeleton="([^"]+)"', main_block or "")
    skel_name = skel_m.group(1) if skel_m else None
    found_in = [os.path.basename(p) for p in live_tpacs
                if any(t == SKELETON_GUID and nm == skel_name for t, nm in items_by_tpac[p])]
    if skel_name and found_in:
        print(f"  [PASS] skeleton '{skel_name}' present in live loose tpac: {found_in[0]}")
    else:
        print(f"  [FAIL] skeleton '{skel_name}' NOT found as a Skeleton resource in any live loose "
              f"tpac -> CreateAgentSkeleton null -> RIDERLESS mount. "
              f"Fix: BUNDLE it back into the mesh tpac with tools/tpac_skeleton_inject.py "
              f"<new_mesh.tpac> <backup_with_skeleton.tpac> {skel_name} <out.tpac> "
              f"(NOT tpac_skeleton_extract.py -- a standalone skeleton tpac CRASHES the engine).")
        fails.append("skeleton")
    if c.get("rein_monster_xml"):   # a Monster that names its own rein skeleton: that one must be in a package too
        try:
            monster = open(os.path.join(game, c["rein_monster_xml"]), encoding="utf-8-sig").read()
        except OSError:             # the install is there and this file is not: what a reinstall leaves
            monster = None
        rein = monster and re.search(r'rein_skeleton="([^"]+)"', re.sub(r'<!--.*?-->', '', monster, flags=re.S))
        held = rein and [os.path.basename(p) for p in live_tpacs
                         if any(t == SKELETON_GUID and nm == rein.group(1) for t, nm in items_by_tpac[p])]
        if held:
            print(f"  [PASS] rein skeleton '{rein.group(1)}' present in live loose tpac: {held[0]}")
        elif monster is None:
            print(f"  [FAIL] the Monster file {c['rein_monster_xml']} is not there")
            fails.append("monster_file")
        else:
            print(f"  [FAIL] rein skeleton '{rein.group(1) if rein else None}' of {c['rein_monster_xml']} "
                  f"NOT found as a Skeleton resource in any live loose tpac")
            fails.append("rein_skeleton")

    # Exact clip items: a byte scan finds x_trot inside x_trot_strafe, and a master can share a clip's name. Either one
    # hides a lost tag or a deleted clip.
    tpac_names = {p: {nm for t, nm in items_by_tpac[p] if t == CLIP_GUID} for p in live_tpacs}

    def tpacs_with(resname):
        return [p for p in live_tpacs if resname in tpac_names[p]]

    # ---- CHECK 2: quad_movement on movement-bound clips ----
    # <monster_usage_movements> = the gait rows the engine MEASURES -> a missing tag on a row that
    #   moves is the proven +0x10 TickAnimations AV => hard FAIL. A pace's direction="none" row is
    #   held to the same rule unless its (action, clip) pair is in untagged_ok: the vanilla horse
    #   binds untagged clips on six of its seven such rows. <monster_usage_upper_body_movements> = head/upper-body OVERLAYS layered on
    #   top; whether the engine measures these for the gait is unproven (the elephant ran 1.4.5
    #   with them untagged) => WARN, not FAIL. Tag them anyway for 1.4.6 safety + spider parity.
    usage_block = re.search(r'<monster_usage_set\s+id="%s".*?</monster_usage_set>' % c["usage_id"],
                            usage_xml, re.S)
    move_actions, upper_actions, moving = set(), set(), set()
    if usage_block:
        ub = usage_block.group(0)
        tb = re.search(r'<monster_usage_movements>(.*?)</monster_usage_movements>', ub, re.S)
        if tb:
            move_actions.update(re.findall(r'action="([^"]+)"', tb.group(1)))
            for tag in re.findall(r'<monster_usage_movement\b[^>]*>', tb.group(1)):     # a row may span lines
                at = dict(re.findall(r'(\w+)="([^"]*)"', tag))
                if at.get("direction") != "none" or at.get("turn_direction") != "none":
                    moving.add(at.get("action"))
        tb = re.search(r'<monster_usage_upper_body_movements>(.*?)</monster_usage_upper_body_movements>', ub, re.S)
        if tb:
            upper_actions.update(re.findall(r'action="([^"]+)"', tb.group(1)))
    upper_actions -= move_actions  # a clip in BOTH counts as a measured gait (FAIL bucket)
    bindings = dict(re.findall(r'<action\s+type="([^"]+)"\s+animation="([^"]+)"', main_block or ""))

    def untagged_clips(actions):
        out, n = [], 0
        for act in sorted(actions):
            anim = bindings.get(act)
            if not anim:
                continue
            host = [p for p in tpacs_with(anim) if p.lower().endswith("_anm.tpac")] or tpacs_with(anim)
            if not host:
                continue  # phantom — check 3
            n += 1
            # the clip's own usage list, not the file: a package of many clips holds the tag of another clip
            if not any(quad_tagged(clip_metas(tpac_bytes[p]).get(anim, b"")) for p in host):
                out.append((act, anim, os.path.basename(host[0])))
        return out, n

    bad_move, n_move = untagged_clips(move_actions)
    # An exempt pair counts only while its action sits on reference rows alone (direction and turn_direction
    # "none"), where vanilla runs untagged clips too. What the engine reads of such a clip is not traced. The same
    # action on a front, strafe or turn row is a measured gait again.
    exempt = {pair for pair in c.get("untagged_ok", ()) if pair[0] not in moving}
    exempted = [row for row in bad_move if row[:2] in exempt]
    for act, anim, _ in exempted:
        print(f"  [NOTE] {act} -> {anim}: exempt, no quad_movement")
    bad_move = [row for row in bad_move if row[:2] not in exempt]
    n_move -= len(exempted)
    bad_upper, n_upper = untagged_clips(upper_actions)
    unbound = sorted(a for a in move_actions if a not in bindings)
    if unbound:
        print(f"  [FAIL] {len(unbound)} movement row(s) bind no clip in {c['action_set_ids'][0]}: {', '.join(unbound)}")
        fails.append("unbound")
    if not n_move:
        print(f"  [FAIL] no measured-gait clip checked: usage set '{c['usage_id']}' has no movement row that binds a clip")
        fails.append("usage")
    elif not bad_move:
        print(f"  [PASS] quad_movement present on all {n_move} measured-gait clips (monster_usage_movements)"
              f"{f', {len(exempted)} exempt' if exempted else ''}")
    else:
        print(f"  [FAIL] {len(bad_move)} MEASURED-GAIT clip(s) MISSING quad_movement -> TickAnimations AV in mission:")
        for act, anim, t in bad_move:
            print(f"           {act} -> {anim}  ({t})")
        fails.append("quad_movement")
    if bad_upper:
        print(f"  [WARN] {len(bad_upper)} upper-body-overlay clip(s) untagged (unproven if measured; "
              f"tag for 1.4.6 safety + spider parity):")
        for act, anim, t in bad_upper:
            print(f"           {act} -> {anim}  ({t})")

    # ---- CHECK 3: no phantom bindings (every creature-clip animation= resolves) ----
    phantom = []
    for sid in c["action_set_ids"]:
        blk = action_set_block(as_xml, sid)
        if not blk:
            phantom.append((sid, "<the action set itself>"))
            continue
        for anim in set(re.findall(r'animation="([^"]+)"', blk)):
            # creature clips live in the creature tpacs; rider clips (act_<c>_ rows -> rider_*) are
            # external (warg/vanilla) and out of scope for this loose-tree check.
            if anim.startswith("rider_") or anim.startswith("act_horse_rider") or anim.startswith("act_run_forward"):
                continue
            if not tpacs_with(anim):
                phantom.append((sid, anim))
    # The rider's clips of this mount, wherever a row names one: every action set of the file and the stylesheet
    # that adds rows to the vanilla sets. No set of the mount itself names them.
    rider_clips = set()
    prefixes = tuple(c.get("rider_clip_prefixes", ()))
    if prefixes:
        rows = re.sub(r'<!--.*?-->', '', as_xml, flags=re.S)
        try:
            with open(os.path.join(game, c["rider_rows_xslt"]), encoding="utf-8-sig") as fh:
                rows += re.sub(r'<!--.*?-->', '', fh.read(), flags=re.S)
        except OSError:             # the human rider's rows are in this file: without it they were not read
            phantom.append(("a rider row", f"<the stylesheet {c['rider_rows_xslt']} is not there>"))
        rider_clips = {a for a in re.findall(r'animation="([^"]+)"', rows) if a.startswith(prefixes)}
        phantom += [("a rider row", anim) for anim in sorted(rider_clips) if not tpacs_with(anim)]
    if not phantom:
        print(f"  [PASS] no phantom bindings (every creature-clip animation= resolves to a tpac resource"
              f"{f', {len(rider_clips)} rider clip(s) among them' if prefixes else ''})")
    else:
        print(f"  [FAIL] {len(phantom)} phantom binding(s) -> degenerate record -> AV/null:")
        for sid, anim in phantom:
            print(f"           {sid}: animation=\"{anim}\" found in NO live tpac")
        fails.append("phantom")

    # ---- CHECK 4: the body capsule holds the body (creatures that name a `body`) ----
    body = c.get("body")
    if body:
        capsule, why = body_findings(game, body)
        size = f" (radius {capsule[0]:.2f}, y {capsule[1][1]:.2f} to {capsule[2][1]:.2f})" if capsule else ""
        if why:
            print(f"  [FAIL] the body capsule of {body['monster_id']}{size}:")
            for w in why:
                print(f"           {w}")
            fails.append("body_capsule")
        else:
            print(f"  [PASS] the body capsule holds the body{size}")

    print("=" * 60)
    if fails:
        print(f"RESULT: FAIL ({', '.join(fails)}) — do NOT battle-test until fixed.")
        sys.exit(1)
    print("RESULT: PASS — skeleton present, gait clips tagged, no phantom bindings.")
    print("  (Still requires an IN-GAME spawn-with-rider check — this gate is static only.)")


if __name__ == "__main__":
    main()
