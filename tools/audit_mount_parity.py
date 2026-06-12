#!/usr/bin/env python3
"""Mount parity audit: spider vs warg vs elephant (vs vanilla horse where useful).

Compares, at the XML level, every surface a mountable creature exposes to the engine:
  A. Monster XML attributes + Flags
  B. monster_usage_set set-level verb attributes (+ resolved action types)
  C. monster_usage row key coverage per table (movements / upper_body / adders / jumps /
     falls / strikes) + the declared TYPE of each referenced action
  D. action_set element attributes + binding coverage for every action the usage set references
  E. rider partial (as_human_warrior) rows: LOTRLOME (spider) vs Alliance.Wargs (warg)

Read-only. Prints diffs only (parity rows are counted, not listed).
Usage: python tools/audit_mount_parity.py
"""
import xml.etree.ElementTree as ET
from collections import OrderedDict

GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
LOTR = GAME + r"\LOTRLOME_Armory\ModuleData"
WARG = GAME + r"\Alliance.Wargs\ModuleData"
NATIVE = GAME + r"\Native\ModuleData"

FILES = {
    "monster": {
        "spider":   LOTR + r"\Monsters\LOTR\lotr_monster_spider.xml",
        "warg":     WARG + r"\Monsters\LOTR\lotr_monster_warg.xml",
        "elephant": LOTR + r"\Monsters\LOTR\lotr_monster_elephant.xml",
        "horse":    NATIVE + r"\monsters.xml",
    },
    "usage": {
        "spider":   (LOTR + r"\monster_usage_sets.xml", "spider"),
        "warg":     (WARG + r"\MonsterUsage\LOTR\lotr_monster_usage_warg.xml", "warg"),
        "elephant": (LOTR + r"\monster_usage_sets.xml", "elephant"),
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
    },
    "rider_partials": {
        "spider (LOTRLOME)": LOTR + r"\action_sets.xml",
        "warg (Alliance.Wargs)": WARG + r"\Animations\action_sets_warg.xml",
    },
}

MOUNTS = ["spider", "warg", "elephant"]


def parse(path):
    return ET.parse(path).getroot()


# ---------- A. Monster attributes ----------
MONSTER_IDS = {"spider": "spider", "warg": "warg", "elephant": "taom_war_elephant", "horse": "horse"}


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


def main():
    types = load_action_types()
    print(f"action_types loaded: {len(types)} declarations")
    section_a()
    section_bc(types)
    section_d(types)
    section_e()
    print("=" * 72)
    print("AUDIT COMPLETE")


if __name__ == "__main__":
    main()
