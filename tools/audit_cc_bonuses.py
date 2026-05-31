#!/usr/bin/env python3
"""
Character Creation Bonus Audit for TAOM

Audits the skill / attribute / focus bonuses granted by every character-creation
menu option, per culture, to surface the "stacking into overpowered starting
characters" problem.

TAOM's CC grants bonuses at menu-selection time via the vanilla engine
(NarrativeMenuOptionArgs.SetAffectedSkills / SetFocusToSkills / SetLevelToSkills /
SetLevelToAttribute). Each option carries (focus_to_add, skill_level_to_add,
attribute_level_to_add). The per-pick magnitude matches vanilla's defaults
(1 / 10 / 1); the imbalance is STRUCTURAL:
  - 6 ungated bonus stages (vanilla has 5): parents, childhood, youth, education,
    adulthood, + a TAOM-added career stage.
  - a culture-base bonus from cultures.json on top.
  - every stage offers every per-culture theme, so a min-maxer concentrates the
    same skill / attribute at every stage.

This tool quantifies that. READ-ONLY by default (--report). --apply performs the
budget cut: it zeroes the career-stage payload (focus/skill/attribute_to_add) AND
clears the career `skills`/`attribute` arrays in career_menu.json (so a selected
career renders a clean zero-bonus line rather than "0 ... to <skill>"), plus zeroes
the culture-base focus/skill in cultures.json — landing on vanilla's 5-stage budget.
It writes a .bak backup of each modified file first; --dry-run previews without writing.

Usage:
    python tools/audit_cc_bonuses.py                 # markdown report to stdout
    python tools/audit_cc_bonuses.py --out docs/reviews/cc-bonus-audit-2026-05-30.md
    python tools/audit_cc_bonuses.py --export-csv cc_audit.csv
    python tools/audit_cc_bonuses.py --dry-run       # preview the budget cut
    python tools/audit_cc_bonuses.py --apply         # apply the budget cut (writes .bak)
"""

import argparse
import csv
import json
import os
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

CC_DIR = os.path.join(
    os.path.dirname(__file__), "..", "Main", "_Module", "ModuleData", "charactercreation"
)
CAREERS_XML = os.path.join(
    os.path.dirname(__file__), "..", "Main", "_Module", "ModuleData", "career_system", "taom_careers.xml"
)

# The five vanilla-replaced narrative stages (culture-gated except childhood) plus
# the TAOM-added career stage. Order = chronological CC flow.
MENU_FILES = {
    "parents": "parents_menu.json",
    "childhood": "childhood_menu.json",
    "youth": "youth_menu.json",
    "education": "education_menu.json",
    "adulthood": "adulthood_menu.json",
    "career": "career_menu.json",
}
CULTURE_GATED = {"parents", "youth", "education", "adulthood"}  # childhood + career are global
CULTURES_JSON = "cultures.json"

# Vanilla reference (decompiled v1.4.5): 5 bonus stages, no career stage, no
# culture-base add. Per-pick value identical to TAOM (1 / 10 / 1).
VANILLA_BONUS_STAGES = 5
VANILLA_TOTAL_FOCUS = 5         # one focus per stage, no culture base
VANILLA_TOTAL_ATTR_POINTS = 5  # one attribute point per stage


def load_json(name):
    with open(os.path.join(CC_DIR, name), encoding="utf-8-sig") as fh:
        return json.load(fh)


def opt_id(o):
    return o.get("string_id") or o.get("career_string_id") or "<no-id>"


def bonus_tuple(o):
    """(num_skills, focus, skill_level, attr_level, attribute_name)."""
    skills = o.get("skills") or []
    return (
        len(skills),
        int(o.get("focus_to_add", 0) or 0),
        int(o.get("skill_level_to_add", 0) or 0),
        int(o.get("attribute_level_to_add", 0) or 0),
        o.get("attribute"),
    )


def load_career_eligibility(valid_culture_ids=None):
    """career_id -> set(culture_id) from taom_careers.xml <EligibleCultures>.

    If valid_culture_ids is given, warn on ids that are not real CC culture ids
    (e.g. kingdom ids like empire_w/empire_s mistakenly used as culture ids — these
    match no hero CultureStringId at runtime and are inert)."""
    elig = {}
    unknown = set()
    tree = ET.parse(CAREERS_XML)
    for career in tree.getroot().findall("Career"):
        cid = career.get("id")
        cultures = {c.get("id") for c in career.findall("./EligibleCultures/Culture")}
        elig[cid] = cultures
        if valid_culture_ids is not None:
            unknown |= {x for x in cultures if x and x not in valid_culture_ids}
    if unknown:
        print(f"WARNING: taom_careers.xml <EligibleCultures> references non-culture ids "
              f"(kingdom ids?): {sorted(unknown)} — they match no hero culture and are inert.",
              file=sys.stderr)
    return elig


def by_culture(options):
    m = defaultdict(list)
    for o in options:
        m[str(o.get("culture_id", "")).lower()].append(o)
    return m


def _zero_fields_in_place(text, fields):
    """Set each JSON `"field": <int>` to 0 in raw text (formatting preserved).
    Returns (new_text, n_matched). Uses \\g<1> (not \\1) for the backref —
    \\1 followed by '0' parses as group 10 (feedback_re_sub_backref_followed_by_digit.md)."""
    import re
    n = 0
    for field in fields:
        pattern = re.compile(r'("' + re.escape(field) + r'"\s*:\s*)\d+')
        text, k = pattern.subn(r"\g<1>0", text)
        n += k
    return text, n


def _blank_career_skills_attribute(text):
    """Clear career `"skills": [...]` -> `[]` and `"attribute": "..."` -> `""` in raw
    text (formatting preserved) so a zeroed career renders a clean no-bonus line.
    Returns (new_text, n_matched)."""
    import re
    text, a = re.subn(r'("skills"\s*:\s*)\[[^\]]*\]', r"\g<1>[]", text)
    text, b = re.subn(r'("attribute"\s*:\s*)"[^"]*"', r'\g<1>""', text)
    return text, a + b


def _rewrite(filename, transform):
    """Read filename (CRLF + encoding preserved), apply transform(text)->(new_text, n).
    Returns (n, original_text, new_text)."""
    path = os.path.join(CC_DIR, filename)
    with open(path, "r", encoding="utf-8", newline="") as fh:
        original = fh.read()
    new_text, n = transform(original)
    return n, original, new_text


def run_budget_cut(write):
    """Phase B (applied): zero the career-stage payload AND clear its skills/attribute
    (so a selected career renders a clean zero-bonus line, not "0 ... to <skill>"), and
    zero the culture-base bonus → match vanilla's 5-stage budget. The 5 narrative stages
    and their themes are untouched. write=False is a dry run."""

    def career_transform(text):
        text, n1 = _zero_fields_in_place(text, ["focus_to_add", "skill_level_to_add", "attribute_level_to_add"])
        text, n2 = _blank_career_skills_attribute(text)
        return text, n1 + n2

    def culture_transform(text):
        return _zero_fields_in_place(text, ["focus_to_add", "skill_level_to_add"])

    plan = [
        (MENU_FILES["career"], career_transform),
        (CULTURES_JSON, culture_transform),
    ]
    action = "APPLY" if write else "DRY-RUN"
    print(f"[{action}] Budget cut — zero career payload + clear career skills/attribute + zero culture-base:", file=sys.stderr)
    for filename, transform in plan:
        n, original, new_text = _rewrite(filename, transform)
        changed = original != new_text
        status = "changed" if changed else "already at target (no change)"
        print(f"  {filename}: {status} ({n} field matches)", file=sys.stderr)
        if write and changed:
            with open(os.path.join(CC_DIR, filename + ".bak"), "w", encoding="utf-8", newline="") as fh:
                fh.write(original)
            with open(os.path.join(CC_DIR, filename), "w", encoding="utf-8", newline="") as fh:
                fh.write(new_text)
            print(f"    wrote {filename} (backup: {filename}.bak)", file=sys.stderr)
    if not write:
        print("  (dry run — no files written; re-run with --apply)", file=sys.stderr)
    return 0


def main():
    ap = argparse.ArgumentParser(description="Audit TAOM character-creation bonuses per culture.")
    ap.add_argument("--out", metavar="PATH", help="Write the markdown report to PATH (default: stdout).")
    ap.add_argument("--export-csv", metavar="PATH", help="Write the per-culture worst-case table as CSV.")
    ap.add_argument("--report", action="store_true", help="(default) Produce the read-only audit report.")
    ap.add_argument("--dry-run", action="store_true",
                    help="Preview the Phase B budget cut (zero career payload + culture-base) without writing.")
    ap.add_argument("--apply", action="store_true",
                    help="Apply the Phase B budget cut: zero career-stage payload + culture-base bonus.")
    args = ap.parse_args()

    if args.apply or args.dry_run:
        return run_budget_cut(write=args.apply)

    menus = {stage: load_json(fname) for stage, fname in MENU_FILES.items()}
    cultures_data = {c["culture_id"]: c for c in load_json(CULTURES_JSON)}
    career_elig = load_career_eligibility(set(cultures_data))

    # culture_id -> set of careers eligible for it
    careers_for_culture = defaultdict(set)
    for cid, cultures in career_elig.items():
        for cult in cultures:
            careers_for_culture[cult].add(cid)

    # Set of cultures that actually appear in the culture-gated menus.
    gated_cultures = set()
    for stage in CULTURE_GATED:
        for o in menus[stage]:
            gated_cultures.add(str(o.get("culture_id", "")).lower())
    gated_cultures.discard("")
    cultures = sorted(gated_cultures)

    lines = []
    w = lines.append

    w("# Character Creation Bonus Audit\n")
    w("> Generated by `tools/audit_cc_bonuses.py`. Read-only analysis of the "
      "skill / attribute / focus bonuses granted per CC menu option, per culture.\n")

    # ---- Section 1: per-stage uniformity -----------------------------------
    w("## 1. Per-stage bonus uniformity\n")
    w("Per-pick value is `(focus, skill-level, attr-level)`. Vanilla defaults are "
      "`(1, 10, 1)` — TAOM should not exceed that per pick.\n")
    w("| Stage | Options | Distinct value-sets | 0-skill | 0-focus | no-attr |")
    w("|-------|--------:|---------------------|--------:|--------:|--------:|")
    for stage, fname in MENU_FILES.items():
        d = menus[stage]
        valsets = Counter((b[1], b[2], b[3]) for b in map(bonus_tuple, d))
        n_no_skill = sum(1 for o in d if not (o.get("skills")))
        n_no_focus = sum(1 for o in d if not o.get("focus_to_add"))
        n_no_attr = sum(1 for o in d if not o.get("attribute"))
        valset_str = ", ".join(f"{k}×{v}" for k, v in valsets.items())
        w(f"| {stage} | {len(d)} | {valset_str} | {n_no_skill} | {n_no_focus} | {n_no_attr} |")
    w("")

    # ---- Section 2: per-culture worst-case concentration -------------------
    w("## 2. Per-culture worst-case concentration\n")
    w("A player picks ONE option per stage. Computation is **value-aware**: a stage only "
      "contributes to a skill/attribute/focus when the picked option's payload for that field "
      "is `> 0`. `maxSkill` = the single skill collecting the most levels across stages; "
      "`maxAttr` = the single attribute collecting the most points; `focus` = total focus a "
      "build can collect (incl. culture base). A stage is a “bonus stage” only if it grants "
      "any focus/skill/attr.\n")
    w("Career is treated as a selectable stage only when the culture has ≥1 eligible career "
      "(else it is the zero-bonus “no specialization” fallback). When several skills/attributes "
      "tie at the worst-case max, the alphabetically-first is shown (ties are common — multiple "
      "lines share the ceiling). This report covers the 16 CC-selectable cultures; `--apply` also "
      "zeroes the culture base of 2 non-selectable cultures (shaghana, abanissa).\n")
    w("| Culture | Bonus stages | Career? | maxSkill (worst-case) | maxAttr (worst-case) | total focus | vs vanilla focus |")
    w("|---------|-------------:|:-------:|-----------------------|----------------------|------------:|:----------------:|")

    csv_rows = []
    for c in cultures:
        # Each stage that this culture can select from + its option pool.
        stage_pools = []
        for stage in ("parents", "youth", "education", "adulthood"):
            pool = [o for o in menus[stage] if str(o.get("culture_id", "")).lower() == c]
            if pool:
                stage_pools.append((stage, pool))
        stage_pools.append(("childhood", menus["childhood"]))  # global

        career_eligible = len(careers_for_culture.get(c, set())) > 0
        if career_eligible:
            career_pool = [o for o in menus["career"]
                           if (o.get("career_string_id") in careers_for_culture[c])]
            if career_pool:
                stage_pools.append(("career", career_pool))

        # Per-skill / per-attr totals = sum of the granting stage's payload (value-aware,
        # so a zeroed stage like a flavor-only career contributes nothing).
        skill_levels = Counter()
        attr_points = Counter()
        total_focus = 0
        bonus_stages = 0
        career_is_bonus = False
        for stage, pool in stage_pools:
            # Within a TAOM stage the payload is uniform; read it from the pool.
            focus_v = max((int(o.get("focus_to_add", 0) or 0) for o in pool), default=0)
            skill_v = max((int(o.get("skill_level_to_add", 0) or 0) for o in pool), default=0)
            attr_v = max((int(o.get("attribute_level_to_add", 0) or 0) for o in pool), default=0)
            if focus_v <= 0 and skill_v <= 0 and attr_v <= 0:
                continue  # flavor-only stage — no bonus
            bonus_stages += 1
            if stage == "career":
                career_is_bonus = True
            total_focus += focus_v
            skills_here, attrs_here = set(), set()
            for o in pool:
                if skill_v > 0:
                    for s in (o.get("skills") or []):
                        skills_here.add(s)
                if attr_v > 0 and o.get("attribute"):
                    attrs_here.add(o["attribute"])
            for s in skills_here:
                skill_levels[s] += skill_v
            for a in attrs_here:
                attr_points[a] += attr_v

        culture_base_focus = int(cultures_data.get(c, {}).get("focus_to_add", 0) or 0)
        if culture_base_focus > 0:
            total_focus += culture_base_focus

        # Deterministic tie-break: highest level, then alphabetical. Counter.most_common()
        # tie-breaks on hash-randomized set-iteration insertion order, which made the
        # committed report non-reproducible across runs.
        top_skill = min(skill_levels.items(), key=lambda kv: (-kv[1], kv[0])) if skill_levels else ("-", 0)
        top_attr = min(attr_points.items(), key=lambda kv: (-kv[1], kv[0])) if attr_points else ("-", 0)
        focus_flag = "OVER" if total_focus > VANILLA_TOTAL_FOCUS else "ok"

        w(f"| {c} | {bonus_stages} | {'yes' if career_is_bonus else 'no'} | "
          f"{top_skill[0]} = +{top_skill[1]} lvl | "
          f"{top_attr[0]} = +{top_attr[1]} | {total_focus} | {focus_flag} |")
        csv_rows.append({
            "culture": c, "bonus_stages": bonus_stages, "career_is_bonus": career_is_bonus,
            "max_skill": top_skill[0], "max_skill_levels": top_skill[1],
            "max_attr": top_attr[0], "max_attr_points": top_attr[1],
            "total_focus": total_focus,
        })
    w("")
    w(f"**Vanilla baseline (decompiled v1.4.5):** {VANILLA_BONUS_STAGES} bonus stages, "
      f"no career stage, no culture-base add → {VANILLA_TOTAL_FOCUS} total focus, "
      f"{VANILLA_TOTAL_ATTR_POINTS} total attribute points, per-pick value identical (1/10/1). "
      "Vanilla gates later stages on earlier picks and offers distinct per-stage skills, so a "
      "single skill/attribute cannot soak every pick the way TAOM's repeated, ungated themes allow.\n")

    # ---- Section 2.5: rebalance applied + remaining lever ------------------
    w("## 2b. Rebalance applied (target: match vanilla) + remaining lever\n")
    w("**Applied (`--apply`, 2026-05-30):** the two TAOM-added bonus sources were zeroed to land "
      "on vanilla's 5-stage budget — the **career stage** payload "
      "(`focus_to_add`/`skill_level_to_add`/`attribute_level_to_add` → 0, and its "
      "`skills`/`attribute` cleared so a selected career renders a clean zero-bonus line rather "
      "than `0 ... to <skill>`) and the **culture-base** bonus in `cultures.json` "
      "(`focus_to_add`/`skill_level_to_add` → 0). The 5 narrative stages (parents, childhood, "
      "youth, education, adulthood) keep the vanilla `(1,10,1)` per-pick bundle untouched, so "
      "their `string_id` / `{=KEY}` text / `occupation_type` / `title_type` are unaffected.\n")
    w("**Remaining lever (not applied — concentration):** even at 5 stages, the repeated ungated "
      "themes let a min-max build stack one skill to `+50` and one attribute to `+5` (vanilla "
      "spreads via gating). Diversifying themes so no skill/attribute appears in more than ~2 "
      "stages is the documented next step; deliberately deferred this round. A runtime hard-cap "
      "at `OnCharacterCreationFinalize` is the belt-and-braces alternative (YAGNI for now).\n")

    # ---- Section 3: full per-culture menu dump -----------------------------
    w("## 3. Full per-culture menu dump (for review)\n")
    w("Every selectable option's `skills` + `attribute`, per stage. Childhood (global) and "
      "the culture's eligible careers are listed under each culture for convenience.\n")
    for c in cultures:
        w(f"### {c}\n")
        for stage in ("parents", "youth", "education", "adulthood"):
            pool = [o for o in menus[stage] if str(o.get("culture_id", "")).lower() == c]
            if not pool:
                continue
            w(f"**{stage}**")
            for o in pool:
                b = bonus_tuple(o)
                w(f"- `{opt_id(o)}` — {o.get('skills')} | attr={o.get('attribute')} "
                  f"| (f{b[1]}/s{b[2]}/a{b[3]})")
            w("")
        # eligible careers
        elig = sorted(careers_for_culture.get(c, set()))
        if elig:
            career_by_id = {o.get("career_string_id"): o for o in menus["career"]}
            w(f"**career** (eligible: {len(elig)})")
            for cid in elig:
                o = career_by_id.get(cid)
                if o:
                    w(f"- `{cid}` — {o.get('skills')} | attr={o.get('attribute')}")
                else:
                    w(f"- `{cid}` — (no CC entry)")
            w("")
        else:
            w("**career** — none eligible → zero-bonus fallback\n")

    # childhood listed once (global)
    w("### childhood (global, all cultures)\n")
    for o in menus["childhood"]:
        b = bonus_tuple(o)
        w(f"- `{opt_id(o)}` — {o.get('skills')} | attr={o.get('attribute')} | (f{b[1]}/s{b[2]}/a{b[3]})")
    w("")

    report = "\n".join(lines)

    if args.out:
        out_path = os.path.abspath(args.out)
        os.makedirs(os.path.dirname(out_path), exist_ok=True)
        with open(out_path, "w", encoding="utf-8") as fh:
            fh.write(report)
        print(f"Wrote report to {out_path}")
    else:
        sys.stdout.buffer.write(report.encode("utf-8"))
        sys.stdout.buffer.write(b"\n")

    if args.export_csv:
        if not csv_rows:
            print("No cultures found — nothing to export to CSV.", file=sys.stderr)
        else:
            csv_path = os.path.abspath(args.export_csv)
            with open(csv_path, "w", newline="", encoding="utf-8") as fh:
                writer = csv.DictWriter(fh, fieldnames=list(csv_rows[0].keys()))
                writer.writeheader()
                writer.writerows(csv_rows)
            print(f"Wrote CSV to {csv_path}", file=sys.stderr)

    return 0


if __name__ == "__main__":
    sys.exit(main())
