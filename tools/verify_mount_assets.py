#!/usr/bin/env python3
"""
POST-DEPLOY verification gate for a rideable creature mount's deployed tpac/FBX assets.

Run this AFTER replacing any creature _anm/_geo tpac (Kit recompile of a Blender rework) and
BEFORE battle-testing. It catches the three ways a re-export silently breaks a working mount:

  1. SKELETON DROP   -- a mesh re-export ships mesh-only, dropping the Skeleton resource the
                        action_set names => CreateAgentSkeleton null => RIDERLESS mount (no crash,
                        easy to miss). The skeleton MUST exist as a Skeleton resource in a LIVE
                        (non-.backup) loose tpac, like the working elephant/warg.
  2. quad_movement   -- a Kit recompile drops the tag on a movement-bound clip => Skeleton.
       DROP            TickAnimations AV (+0x10) on the first mission tick in every mount context.
  3. PHANTOM BINDING -- a rename orphans an action_set animation= target => degenerate record =>
                        AV/null when resolved.

This is the executable form of the "Replacing FBX/tpac files" requirements in
docs/ai-includes/creature-mount-authoring.md. Read-only; prints PASS/FAIL per check; exit 1 on
any FAIL.

Usage:
  python tools/verify_mount_assets.py spider
  python tools/verify_mount_assets.py elephant
  python tools/verify_mount_assets.py --game "E:\\...\\Mount & Blade II Bannerlord" spider
"""
import argparse
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
    # as_elephant action set + every elephant clip. Its own mesh tpac (creature/mumakil) carries NO skeleton or
    # animation resources — those live in the elephant tree — so the skeleton/clip/binding checks search BOTH
    # dirs via extra_asset_dirs. The mumakil mesh MUST reference skeleton="elephant_skeleton" (rename from the
    # imported "elephant_skeleton_unused" on re-export, like the elephant's own mesh did) — verify the mesh tpac
    # separately with tools/tpac_skeleton_scan.py (this gate confirms the shared skeleton/clips are sound).
    "mumakil": {
        "asset_dir": r"Modules\LOTRLOME_Armory\Assets\creature\mumakil",
        "extra_asset_dirs": [r"Modules\LOTRLOME_Armory\Assets\creature\elephant"],
        "action_sets_xml": r"Modules\LOTRLOME_Armory\ModuleData\action_sets.xml",
        "usage_xml": r"Modules\LOTRLOME_Armory\ModuleData\monster_usage_sets.xml",
        "action_set_ids": ["as_elephant", "as_elephant_town_and_village", "as_elephant_map"],
        "usage_id": "elephant",
        "rider_prefix": "act_elephant_",
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
        # the flags to the warg twin.
        "untagged_ok": (
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

    # Exact clip items: a byte scan finds x_trot inside x_trot_strafe, and a master can share a clip's name. Either one
    # hides a lost tag or a deleted clip.
    tpac_names = {p: {nm for t, nm in items_by_tpac[p] if t == CLIP_GUID} for p in live_tpacs}

    def tpacs_with(resname):
        return [p for p in live_tpacs if resname in tpac_names[p]]

    # ---- CHECK 2: quad_movement on movement-bound clips ----
    # <monster_usage_movements> = the gait rows the engine MEASURES (incl. each pace's
    #   direction="none" reference) -> a missing tag here is the proven +0x10 TickAnimations AV
    #   => hard FAIL. <monster_usage_upper_body_movements> = head/upper-body OVERLAYS layered on
    #   top; whether the engine measures these for the gait is unproven (the elephant ran 1.4.5
    #   with them untagged) => WARN, not FAIL. Tag them anyway for 1.4.6 safety + spider parity.
    usage_block = re.search(r'<monster_usage_set\s+id="%s".*?</monster_usage_set>' % c["usage_id"],
                            usage_xml, re.S)
    move_actions, upper_actions = set(), set()
    if usage_block:
        ub = usage_block.group(0)
        tb = re.search(r'<monster_usage_movements>(.*?)</monster_usage_movements>', ub, re.S)
        if tb:
            move_actions.update(re.findall(r'action="([^"]+)"', tb.group(1)))
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
            if not any(b"quad_movement" in tpac_bytes[p] for p in host):
                out.append((act, anim, os.path.basename(host[0])))
        return out, n

    bad_move, n_move = untagged_clips(move_actions)
    exempt = set(c.get("untagged_ok", ()))
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
    if not phantom:
        print(f"  [PASS] no phantom bindings (every creature-clip animation= resolves to a tpac resource)")
    else:
        print(f"  [FAIL] {len(phantom)} phantom binding(s) -> degenerate record -> AV/null:")
        for sid, anim in phantom:
            print(f"           {sid}: animation=\"{anim}\" found in NO live tpac")
        fails.append("phantom")

    print("=" * 60)
    if fails:
        print(f"RESULT: FAIL ({', '.join(fails)}) — do NOT battle-test until fixed.")
        sys.exit(1)
    print("RESULT: PASS — skeleton present, gait clips tagged, no phantom bindings.")
    print("  (Still requires an IN-GAME spawn-with-rider check — this gate is static only.)")


if __name__ == "__main__":
    main()
