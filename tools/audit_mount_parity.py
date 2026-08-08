#!/usr/bin/env python3
"""Mount parity audit: spider vs warg vs elephant (vs vanilla horse where useful).

Compares, at the XML level, every surface a mountable creature exposes to the engine:
  A. Monster XML attributes + Flags
  B. monster_usage_set set-level verb attributes (+ resolved action types)
  C. monster_usage row key coverage per table (movements / upper_body / adders / jumps /
     falls / strikes) + the declared TYPE of each referenced action
  D. action_set element attributes + binding coverage for every action the usage set references
  E. rider partial (as_human_warrior) rows: LOTRLOME (spider) vs Alliance.Wargs (warg)
  F. chariot vs VANILLA HORSE — the chariot is a ridden vehicle with no behaviour tree, so its
     reference class is the horse, not the warg. Sections A-E are baselined on the spider and
     deliberately do not cover it.

Read-only. Prints diffs only (parity rows are counted, not listed).
Usage: python tools/audit_mount_parity.py
"""
import glob
import os
import re
import xml.etree.ElementTree as ET
from collections import OrderedDict

from tpac_clipinfo import parse as parse_clip
from _gamedir import game_dir

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME = os.path.join(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"), "Modules")
LOTR = GAME + r"\LOTRLOME_Armory\ModuleData"
WARG = GAME + r"\Alliance.Wargs\ModuleData"
NATIVE = GAME + r"\Native\ModuleData"

FILES = {
    "monster": {
        "spider":   LOTR + r"\Monsters\LOTR\lotr_monster_spider.xml",
        "warg":     WARG + r"\Monsters\LOTR\lotr_monster_warg.xml",
        "elephant": LOTR + r"\Monsters\LOTR\lotr_monster_elephant.xml",
        # Mûmakil = scaled-up elephant; should diff ZERO vs elephant except the id (taom_mumakil).
        "mumakil":  LOTR + r"\Monsters\LOTR\lotr_monster_mumakil.xml",
        "horse":    NATIVE + r"\monsters.xml",
    },
    "usage": {
        "spider":   (LOTR + r"\monster_usage_sets.xml", "spider"),
        "warg":     (WARG + r"\MonsterUsage\LOTR\lotr_monster_usage_warg.xml", "warg"),
        "elephant": (LOTR + r"\monster_usage_sets.xml", "elephant"),
        # Mûmakil reuses the elephant usage set verbatim (same skeleton/clips) — parity by construction.
        "mumakil":  (LOTR + r"\monster_usage_sets.xml", "elephant"),
        "horse":    (NATIVE + r"\monster_usage_sets.xml", "horse"),
    },
    "action_types": [
        LOTR + r"\action_types.xml",
        WARG + r"\Animations\action_types_warg.xml",
        NATIVE + r"\action_types.xml",
    ],
    "action_sets": {
        "spider":   (LOTR + r"\action_sets.xml", "as_spider"),
        "warg":     (WARG + r"\Animations\action_sets_warg.xml", "as_warg"),
        "elephant": (LOTR + r"\action_sets.xml", "as_elephant"),
        # Mûmakil reuses the elephant action set verbatim (same skeleton/clips) — parity by construction.
        "mumakil":  (LOTR + r"\action_sets.xml", "as_elephant"),
    },
    "rider_partials": {
        "spider (LOTRLOME)": LOTR + r"\action_sets.xml",
        "warg (Alliance.Wargs)": WARG + r"\Animations\action_sets_warg.xml",
    },
}

MOUNTS = ["spider", "warg", "elephant", "mumakil"]

# Section F. The chariot rides on its own 60-bone skeleton that embeds the complete vanilla horse
# bone set by exact name, has no behaviour tree and no TAOM C# at all -- so the horse is its
# baseline. Comparing it against the warg (as sections A-E do for the BT predators) produced a
# false-positive HIGH once already; see docs/reviews/lessons/data-content-cultures.md.
CHARIOT = {
    "monster": (LOTR + r"\Monsters\LOTR\lotr_monster_chariot.xml", "chariot"),
    "usage": (LOTR + r"\monster_usage_sets.xml", "chariot"),
    "action_set": (LOTR + r"\action_sets.xml", "as_chariot"),
    "clips": LOTR.replace(r"\ModuleData", r"\Assets\creature\chariot\animations"),
}
HORSE = {
    "monster": (NATIVE + r"\monsters.xml", "horse"),
    "usage": (NATIVE + r"\monster_usage_sets.xml", "horse"),
    "action_set": (NATIVE + r"\action_sets.xml", "as_horse"),
}


def parse(path):
    return ET.parse(path).getroot()


# ---------- A. Monster attributes ----------
MONSTER_IDS = {"spider": "spider", "warg": "warg", "elephant": "taom_war_elephant", "mumakil": "taom_mumakil", "horse": "horse"}


def monster_elem(name):
    root = parse(FILES["monster"][name])
    want = MONSTER_IDS[name]
    for m in root.iter("Monster"):
        if m.get("id") == want:
            return m
    return None


def section_a():
    print("=" * 72)
    print("A. MONSTER XML ATTRIBUTES")
    elems = {n: monster_elem(n) for n in MOUNTS + ["horse"]}
    attrs = {n: dict(e.attrib) for n, e in elems.items() if e is not None}
    allkeys = sorted(set().union(*[set(a) for a in attrs.values()]))
    diffs = 0
    for k in allkeys:
        present = {n: (k in attrs[n]) for n in attrs}
        if present["spider"] != present["warg"] or present["spider"] != present["elephant"]:
            vals = {n: attrs[n].get(k, "<absent>") for n in attrs}
            print(f"  attr {k}:")
            for n in ["spider", "warg", "elephant", "horse"]:
                print(f"      {n:9s} {vals.get(n, '<no monster>')}")
            diffs += 1
    if not diffs:
        print("  attribute PRESENCE: spider == warg == elephant (values differ only in bones/sizes)")
    # value-level for the semantic class fields
    for k in ["family_type", "num_paces", "sound_and_collision_info_class",
              "relative_speed_limit_for_charge", "walking_speed_limit", "jump_speed_limit"]:
        vals = {n: attrs[n].get(k, "<absent>") for n in attrs}
        print(f"  value {k:36s} spider={vals['spider']:8s} warg={vals['warg']:8s} "
              f"elephant={vals['elephant']:8s} horse={vals['horse']}")
    # Flags
    print("  FLAGS:")
    flag_attrs = {}
    for n, e in elems.items():
        if e is None:
            continue
        f = e.find("Flags")
        flag_attrs[n] = dict(f.attrib) if f is not None else {}
    fkeys = sorted(set().union(*[set(a) for a in flag_attrs.values()]))
    for k in fkeys:
        vals = {n: flag_attrs[n].get(k, "-") for n in flag_attrs}
        marker = "" if (vals["spider"] == vals["warg"] == vals["elephant"]) else "   <-- DIFF"
        print(f"      {k:18s} spider={vals['spider']:6s} warg={vals['warg']:6s} "
              f"elephant={vals['elephant']:6s} horse={vals.get('horse','-')}{marker}")


# ---------- action types ----------
def load_action_types():
    types = {}
    for path in FILES["action_types"]:
        root = parse(path)
        for a in root.iter("action"):
            nm = a.get("name")
            if nm and nm not in types:
                types[nm] = a.get("type", "<untyped>")
    return types


# ---------- B + C. usage sets ----------
def usage_set(name):
    path, want = FILES["usage"][name]
    root = parse(path)
    for s in root.iter("monster_usage_set"):
        if s.get("id") == want:
            return s
    return None


ROW_KEYS = {
    "monster_usage_movements/monster_usage_movement": ("is_left_foot", "pace", "direction", "turn_direction"),
    "monster_usage_upper_body_movements/monster_usage_upper_body_movement": ("pace", "direction", "turn_direction"),
    "monster_usage_movement_adders/monster_usage_movement_adder": ("is_left_foot", "pace", "direction", "turn_direction"),
    "monster_usage_jumps/monster_usage_jump": ("jump_state", "direction", "is_hard"),
    "monster_usage_falls/monster_usage_fall": ("is_heavy", "is_left_stance", "direction", "body_part", "death_type"),
    "monster_usage_strikes/monster_usage_strike": ("is_heavy", "is_left_stance", "direction", "body_part", "impact"),
}


def rows_of(setelem, table_path):
    parent, child = table_path.split("/")
    out = OrderedDict()
    p = setelem.find(parent)
    if p is None:
        return out
    keys = ROW_KEYS[table_path]
    for r in p.findall(child):
        key = tuple((k, (r.get(k) or "").lower()) for k in keys)
        out[key] = r.get("action", "")
    return out


def section_bc(types):
    print("=" * 72)
    print("B. USAGE SET-LEVEL VERB ATTRIBUTES (presence + resolved action type)")
    sets = {n: usage_set(n) for n in MOUNTS + ["horse"]}
    sattrs = {n: dict(s.attrib) for n, s in sets.items() if s is not None}
    verbs = sorted(set().union(*[set(a) for a in sattrs.values()]) - {"id"})
    for v in verbs:
        row = []
        diff = False
        for n in MOUNTS + ["horse"]:
            act = sattrs.get(n, {}).get(v)
            t = types.get(act, "<UNDECLARED>") if act else "-"
            row.append(f"{n}={'<absent>' if act is None else t}")
        spider_t = types.get(sattrs.get("spider", {}).get(v, ""), None) if sattrs.get("spider", {}).get(v) else None
        warg_t = types.get(sattrs.get("warg", {}).get(v, ""), None) if sattrs.get("warg", {}).get(v) else None
        ele_t = types.get(sattrs.get("elephant", {}).get(v, ""), None) if sattrs.get("elephant", {}).get(v) else None
        present_diff = (sattrs.get("spider", {}).get(v) is None) != (sattrs.get("warg", {}).get(v) is None)
        type_diff = spider_t is not None and warg_t is not None and spider_t != warg_t and spider_t != (ele_t or spider_t)
        marker = "   <-- DIFF" if (present_diff or type_diff) else ""
        print(f"  {v:36s} {' | '.join(row)}{marker}")

    print()
    print("C. USAGE ROW KEY COVERAGE + referenced-action types (diffs only)")
    for table in ROW_KEYS:
        tname = table.split("/")[0]
        data = {n: rows_of(sets[n], table) for n in MOUNTS + ["horse"] if sets.get(n) is not None}
        union_baseline = set(data["warg"]) | set(data["elephant"])
        missing = [k for k in union_baseline if k not in data["spider"]]
        extra = [k for k in data["spider"] if k not in union_baseline]
        print(f"  {tname}: spider={len(data['spider'])} warg={len(data['warg'])} "
              f"elephant={len(data['elephant'])} horse={len(data.get('horse', {}))} rows")
        for k in missing:
            who = "warg" if k in data["warg"] else "elephant"
            print(f"      MISSING in spider (present in {who}): {dict(k)} -> {data[who][k]}")
        for k in extra:
            print(f"      EXTRA in spider (no warg/elephant analog): {dict(k)} -> {data['spider'][k]}")
        # type comparison on shared keys vs warg
        for k in data["spider"]:
            if k in data["warg"]:
                ts = types.get(data["spider"][k], "<UNDECLARED>")
                tw = types.get(data["warg"][k], "<UNDECLARED>")
                if ts != tw:
                    print(f"      TYPE DIFF vs warg at {dict(k)}: spider {data['spider'][k]}={ts} "
                          f"vs warg {data['warg'][k]}={tw}")
        # undeclared actions referenced by spider
        for k, act in data["spider"].items():
            if act and act not in types:
                print(f"      SPIDER ACTION NOT IN action_types: {act} at {dict(k)}")


# ---------- D. action sets ----------
def action_set_blocks(path, set_id):
    """All <action_set> elements with this id (partials included), in file order."""
    root = parse(path)
    return [s for s in root.iter("action_set") if s.get("id") == set_id]


def section_d(types):
    print("=" * 72)
    print("D. ACTION_SET ELEMENT ATTRS + usage-set reference coverage")
    bindmaps = {}
    for n in MOUNTS:
        path, sid = FILES["action_sets"][n]
        blocks = action_set_blocks(path, sid)
        merged = {}
        print(f"  {n}: {len(blocks)} <action_set id='{sid}'> block(s)")
        for b in blocks:
            print(f"      attrs: { {k: v for k, v in b.attrib.items() if k != 'id'} }")
            for a in b.findall("action"):
                merged[a.get("type")] = a.get("animation")
        bindmaps[n] = merged
        print(f"      total bound actions: {len(merged)}")
    # every action the spider usage set references must be bound in as_spider OR
    # the same hole must exist for warg (then the engine tolerates it)
    sets = {n: usage_set(n) for n in MOUNTS}
    for n in MOUNTS:
        s = sets[n]
        refs = set()
        for table in ROW_KEYS:
            refs.update(rows_of(s, table).values())
        refs.update(v for k, v in s.attrib.items() if k != "id")
        unbound = sorted(r for r in refs if r and r not in bindmaps[n] and not r.startswith("act_horse_rider") and r != "act_run_forward_adder")
        if unbound:
            print(f"  {n}: usage-set actions NOT bound in own action_set ({len(unbound)}):")
            for r in unbound:
                print(f"      {r}  (type={types.get(r, '<UNDECLARED>')})")
        else:
            print(f"  {n}: every usage-set action is bound in its action_set")


# ---------- E. rider partials ----------
def section_e():
    print("=" * 72)
    print("E. RIDER PARTIAL as_human_warrior (mount-specific rows)")
    rows = {}
    for label, path in FILES["rider_partials"].items():
        root = parse(path)
        partial_rows = {}
        for s in root.iter("action_set"):
            if s.get("id") == "as_human_warrior":
                for a in s.findall("action"):
                    partial_rows[a.get("type")] = a.get("animation")
        rows[label] = partial_rows
        print(f"  {label}: {len(partial_rows)} partial rows")
    spider_rows = {k: v for k, v in rows["spider (LOTRLOME)"].items() if "spider" in k}
    warg_rows = {k: v for k, v in rows["warg (Alliance.Wargs)"].items() if "warg" in k}
    # map suffixes
    def suffix(k, pre):
        return k.replace(pre, "", 1)
    s_sfx = {suffix(k, "act_spider_"): (k, v) for k, v in spider_rows.items()}
    w_sfx = {suffix(k, "act_warg_"): (k, v) for k, v in warg_rows.items()}
    only_w = sorted(set(w_sfx) - set(s_sfx))
    only_s = sorted(set(s_sfx) - set(w_sfx))
    print(f"  spider-specific rider rows: {len(spider_rows)}; warg-specific rider rows: {len(warg_rows)}")
    for x in only_w:
        print(f"      warg partial covers '{x}' (-> {w_sfx[x][1]}), spider partial does NOT")
    for x in only_s:
        print(f"      spider partial covers '{x}' (-> {s_sfx[x][1]}), warg partial does NOT")
    shared = set(s_sfx) & set(w_sfx)
    anim_diff = [x for x in shared if s_sfx[x][1] != w_sfx[x][1]]
    for x in sorted(anim_diff):
        print(f"      anim DIFF '{x}': spider->{s_sfx[x][1]}  warg->{w_sfx[x][1]}")


# ---------- F. chariot vs vanilla horse ----------
# Absent on the chariot AND on the shipped, battle-proven warg, so absence is survivable and
# reporting it as a defect is noise. Recorded here so the next reader does not re-derive it:
# checked 2026-08-02 against all 94 Monster definitions in the load order.
BENIGN_ABSENT = {
    "fall_blow_damage_bone",            # also absent on warg
    "body_rotation_reference_bone",     # absent on 81/94, incl. human and dwarf
    "standing_chest_height",            # absent on elephant/mumakil
    "standing_pelvis_height",
}


def elem_by_id(path, tag, want):
    for e in parse(path).iter(tag):
        if e.get("id") == want:
            return e
    return None


def chariot_clips():
    """Deployed clip inventory: internal resource name -> flag list. Filename != resource name."""
    inv = {}
    for f in sorted(glob.glob(os.path.join(CHARIOT["clips"], "*_anm.tpac"))):
        info = parse_clip(f)
        if info.get("name"):
            inv[info["name"]] = info.get("flags", [])
    return inv


def section_f(types):
    print("=" * 72)
    print("F. CHARIOT vs VANILLA HORSE (ridden vehicle, no BT -- horse is the reference class)")

    cm = elem_by_id(CHARIOT["monster"][0], "Monster", CHARIOT["monster"][1])
    hm = elem_by_id(HORSE["monster"][0], "Monster", HORSE["monster"][1])
    if cm is None or hm is None:
        print("  SKIPPED: chariot or horse Monster not found")
        return

    # F1. attribute presence
    only_horse = sorted(set(hm.attrib) - set(cm.attrib))
    only_chariot = sorted(set(cm.attrib) - set(hm.attrib))
    flagged = [k for k in only_horse if k not in BENIGN_ABSENT
               and not k.startswith(("ragdoll_bone_to_check_for_corpses_", "ragdoll_fall_sound_bone_"))]
    print(f"  F1 attrs: chariot={len(cm.attrib)} horse={len(hm.attrib)}; "
          f"{len(only_horse)} horse-only, {len(only_chariot)} chariot-only")
    for k in flagged:
        print(f"      ON HORSE, ABSENT ON CHARIOT: {k} = {hm.attrib[k]}   <-- REVIEW")
    if not flagged:
        print("      no unexplained horse-only attributes (known-benign set is annotated in source)")
    for k in only_chariot:
        print(f"      chariot-only: {k} = {cm.attrib[k]}")

    cf = cm.find("Flags")
    hf = hm.find("Flags")
    cfa = dict(cf.attrib) if cf is not None else {}
    hfa = dict(hf.attrib) if hf is not None else {}
    for k in sorted(set(cfa) | set(hfa)):
        cv, hv = cfa.get(k, "-"), hfa.get(k, "-")
        if cv != hv:
            print(f"      FLAG DIFF {k:18s} chariot={cv:6s} horse={hv}")

    # F2/F3. usage set
    cs = elem_by_id(CHARIOT["usage"][0], "monster_usage_set", CHARIOT["usage"][1])
    hs = elem_by_id(HORSE["usage"][0], "monster_usage_set", HORSE["usage"][1])
    if cs is None or hs is None:
        print("  F2/F3 SKIPPED: chariot or horse usage set not found")
        return
    missing_verbs = sorted(set(hs.attrib) - set(cs.attrib) - {"id"})
    print(f"  F2 usage verbs: chariot={len(cs.attrib) - 1} horse={len(hs.attrib) - 1}")
    for v in missing_verbs:
        print(f"      verb on horse, absent on chariot: {v} = {hs.attrib[v]}")

    print("  F3 usage row coverage:")
    for table in ROW_KEYS:
        crows, hrows = rows_of(cs, table), rows_of(hs, table)
        miss = [k for k in hrows if k not in crows]
        print(f"      {table.split('/')[0]}: chariot={len(crows)} horse={len(hrows)}"
              + (f"  ({len(miss)} horse rows unmatched)" if miss else ""))

    # F4. bindings + deployed clips. These are the two shipped-bug classes: an animation target
    # that does not exist, and a measured gait clip missing quad_movement. Both compile fine and
    # then AV natively in a live mission -- see docs/reviews/lessons/animation-skeleton.md.
    aset = elem_by_id(CHARIOT["action_set"][0], "action_set", CHARIOT["action_set"][1])
    bound = {a.get("type"): a.get("animation") for a in aset.findall("action")} if aset is not None else {}
    inv = chariot_clips()
    print(f"  F4 as_chariot: {len(bound)} bound actions; {len(inv)} deployed clips")

    # An empty inventory means the clips path is wrong, NOT that the data is clean.
    # The `dangling` check would flood (every target "missing"), but `untagged` tests
    # `bound[r] in inv` FIRST, so it silently reports nothing regardless of ground
    # truth -- a false clean on the one probe that guards against the shipped
    # quad_movement AV. Refuse to report either result rather than mislead.
    if not inv:
        print(f"      WARNING: 0 clips found under {CHARIOT['clips']}")
        print("      F4 target-resolution and quad_movement checks SKIPPED - path is wrong,")
        print("      not the data. Fix the path before trusting this section.")
        return

    refs = set()
    for table in ROW_KEYS:
        refs.update(rows_of(cs, table).values())
    refs.update(v for k, v in cs.attrib.items() if k != "id")

    # act_run_forward_adder is referenced by the VANILLA horse usage set too and bound nowhere --
    # a vanilla-tolerated hole, not a chariot defect. Same exemption section D applies.
    unbound = sorted(r for r in refs if r and r not in bound
                     and not r.startswith("act_horse_rider") and r != "act_run_forward_adder")
    for r in unbound:
        print(f"      usage action NOT bound in as_chariot: {r} (type={types.get(r, '<UNDECLARED>')})")

    # The rider STANDS in the cart, so its clips are injected into as_human_warrior by
    # action_sets.xslt rather than bound in as_chariot -- count them as referenced.
    xslt = CHARIOT["action_set"][0].replace("action_sets.xml", "action_sets.xslt")
    rider_refs = set()
    if os.path.exists(xslt):
        with open(xslt, encoding="utf-8", errors="replace") as fh:
            rider_refs = {m for m in re.findall(r'animation="(chariot[^"]*)"', fh.read())}

    dangling = sorted({a for a in set(bound.values()) | rider_refs
                       if a and a.startswith("chariot") and a not in inv})
    for a in dangling:
        print(f"      ANIMATION TARGET DOES NOT EXIST: {a}   <-- degenerate record, AVs on use")

    # ONLY the base locomotion table. Upper-body overlays (the *_head look clips) and non-gait
    # clips must NOT carry quad_movement -- tagging them is itself a defect. Measured-gait clips
    # that lack it build a NULL native gait structure and AV on the first Skeleton.TickAnimations.
    gait_refs = set(rows_of(cs, "monster_usage_movements/monster_usage_movement").values())
    untagged = sorted({bound[r] for r in gait_refs
                       if r in bound and bound[r] in inv and "quad_movement" not in inv[bound[r]]})
    for a in untagged:
        print(f"      GAIT CLIP MISSING quad_movement: {a}   <-- Skeleton.TickAnimations AV")

    if not (unbound or dangling or untagged):
        print("      every usage action bound, every target resolves, every gait clip tagged")

    orphans = sorted(set(inv) - {a for a in bound.values() if a} - rider_refs)
    if orphans:
        print(f"      deployed but never referenced ({len(orphans)}): {', '.join(orphans)}")


def main():
    types = load_action_types()
    print(f"action_types loaded: {len(types)} declarations")
    section_a()
    section_bc(types)
    section_d(types)
    section_e()
    section_f(types)
    print("=" * 72)
    print("AUDIT COMPLETE")


if __name__ == "__main__":
    main()
