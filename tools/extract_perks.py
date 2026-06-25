#!/usr/bin/env python3
"""
Bannerlord perk-catalog extractor (READ-ONLY source → data).

Parses the decompiled `DefaultPerks.cs` (v1.4.6, via `pwsh tools/taom-src.ps1 path
TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks`) into a structured perk catalog so a
lord/hero's skill levels can be mapped to the perks (and concrete gameplay bonuses) they unlock.

Each perk is one `_field.Initialize(...)` call:
    "{=key}Name", DefaultSkills.<Skill>, GetTierCost(<tier 1-12>), <altPerk|null>,
    "{=key}PrimaryDesc {VALUE}", PartyRole.<role>, <bonus>f, EffectIncrementType.<Add|AddFactor|Invalid>,
    "{=key}SecondaryDesc", PartyRole.<role>, <bonus>f, EffectIncrementType.<...> [, primFlags, secFlags]
Tier N → skill level TierSkillRequirements[N-1] = {25,50,...,300}. Perks come in pairs (the perk + its
alternative) at each tier; a hero with skill >= tier level unlocks that tier (engine picks one of the pair).

Outputs:
  - tools/data/bannerlord_perks.json   (committed source data — the "identified perks")
  - tools/reports/lord-balance/perks.html  (human-readable reference; gitignored, regenerate-able)

Usage:
    python extract_perks.py                         # default DefaultPerks.cs path + write both
    python extract_perks.py --defaultperks <path>   # override source
    python extract_perks.py --stdout                # print a summary
"""

import argparse
import json
import os
import re

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
DEFAULT_PERKS_PATH = os.path.expanduser(
    '~/.taom-src/v1.4.6/TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.cs')
DATA_DIR = os.path.join(REPO_ROOT, 'tools', 'data')
JSON_OUT = os.path.join(DATA_DIR, 'bannerlord_perks.json')
HTML_DIR = os.path.join(REPO_ROOT, 'tools', 'reports', 'lord-balance')
HTML_OUT = os.path.join(HTML_DIR, 'perks.html')

TIER_LEVELS = [25, 50, 75, 100, 125, 150, 175, 200, 225, 250, 275, 300]

# Skill → attribute (vanilla DefaultSkills / DefaultCharacterAttributes). Crafting == the Smithing skill.
SKILL_ATTR = {
    'OneHanded': 'Vigor', 'TwoHanded': 'Vigor', 'Polearm': 'Vigor',
    'Bow': 'Control', 'Crossbow': 'Control', 'Throwing': 'Control',
    'Riding': 'Endurance', 'Athletics': 'Endurance', 'Crafting': 'Endurance',
    'Scouting': 'Cunning', 'Tactics': 'Cunning', 'Roguery': 'Cunning',
    'Charm': 'Social', 'Leadership': 'Social', 'Trade': 'Social',
    'Steward': 'Intelligence', 'Medicine': 'Intelligence', 'Engineering': 'Intelligence',
}
ATTR_ORDER = ['Vigor', 'Control', 'Endurance', 'Cunning', 'Social', 'Intelligence']
SKILL_ORDER = [s for a in ATTR_ORDER for s in SKILL_ATTR if SKILL_ATTR[s] == a]

INIT_RE = re.compile(r'_(\w+)\.Initialize\((.*)\);\s*$')


def strip_loc(s):
    """ "{=key}Text" -> "Text"; plain quoted string -> unquoted. """
    s = s.strip()
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1]
    if s.startswith('{=') and '}' in s:
        s = s.split('}', 1)[1]
    return s


def split_args(argstr):
    """Split a C# arg list on top-level commas, respecting double quotes and parens (GetTierCost(1))."""
    args, cur, depth, inq = [], '', 0, False
    for c in argstr:
        if c == '"':
            inq = not inq
            cur += c
        elif c == '(' and not inq:
            depth += 1
            cur += c
        elif c == ')' and not inq:
            depth -= 1
            cur += c
        elif c == ',' and not inq and depth == 0:
            args.append(cur.strip())
            cur = ''
        else:
            cur += c
    if cur.strip():
        args.append(cur.strip())
    return args


def parse_float(tok):
    return float(tok.strip().rstrip('fF'))


def fmt_value(bonus, inc):
    """Render {VALUE} the way the game displays it for the increment type."""
    if inc == 'AddFactor':
        v = bonus * 100.0
    elif inc == 'Add':
        v = bonus
    else:  # Invalid / toggle — no numeric value
        return ''
    if abs(v - round(v)) < 1e-6:
        return str(int(round(v)))
    return f'{v:g}'


def render_effect(desc, bonus, inc):
    desc = desc.replace('{VALUE}', fmt_value(bonus, inc))
    return ' '.join(desc.split()).strip()


def parse_perks(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    perks = []
    for raw in lines:
        m = INIT_RE.search(raw.strip())
        if not m:
            continue
        field, argstr = m.group(1), m.group(2)
        a = split_args(argstr)
        # Forms: 8 args (primary only — capstones + Crafting), 12 (primary+secondary),
        # 13/14 (+1 or 2 trailing TroopUsageFlags). Primary is always a[4..7]; secondary a[8..11].
        if len(a) < 8 or 'DefaultSkills.' not in a[1]:
            continue
        skill = a[1].split('.')[-1]
        tier_m = re.search(r'\d+', a[2])
        if not tier_m:
            continue
        tier = int(tier_m.group())
        level = TIER_LEVELS[tier - 1] if 1 <= tier <= len(TIER_LEVELS) else 25 * tier

        prim_bonus, prim_inc = parse_float(a[6]), a[7].split('.')[-1]
        primary = {'role': a[5].split('.')[-1], 'bonus': prim_bonus, 'inc': prim_inc,
                   'effect': render_effect(strip_loc(a[4]), prim_bonus, prim_inc)}
        secondary = None
        if len(a) >= 12:
            sec_bonus, sec_inc = parse_float(a[10]), a[11].split('.')[-1]
            secondary = {'role': a[9].split('.')[-1], 'bonus': sec_bonus, 'inc': sec_inc,
                         'effect': render_effect(strip_loc(a[8]), sec_bonus, sec_inc)}
        perks.append({
            'id': field,
            'name': strip_loc(a[0]),
            'skill': skill,
            'attribute': SKILL_ATTR.get(skill, '?'),
            'tier': tier,
            'level': level,
            'alt': a[3] if a[3] != 'null' else None,
            'primary': primary,
            'secondary': secondary,
        })
    return perks


def build_catalog(perks):
    """{ skill: [ {tier, level, perks:[perk,...]} sorted by tier ] }."""
    by_skill = {}
    for p in perks:
        by_skill.setdefault(p['skill'], {}).setdefault(p['tier'], {'tier': p['tier'], 'level': p['level'], 'perks': []})
        by_skill[p['skill']][p['tier']]['perks'].append(p)
    catalog = {}
    for skill in SKILL_ORDER:
        if skill in by_skill:
            catalog[skill] = [by_skill[skill][t] for t in sorted(by_skill[skill])]
    # any skill not in SKILL_ORDER (defensive)
    for skill in by_skill:
        if skill not in catalog:
            catalog[skill] = [by_skill[skill][t] for t in sorted(by_skill[skill])]
    return catalog


# --- HTML reference -----------------------------------------------------------

import html as _html  # noqa: E402

CSS = """
body{font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;margin:0;background:#15171c;color:#d8dee9;line-height:1.5}
.wrap{max-width:1200px;margin:0 auto;padding:24px 32px 80px}
h1{color:#fff;margin:0 0 4px}h2{color:#fff;border-bottom:2px solid #2c3340;padding-bottom:6px;margin:34px 0 8px}
h3{color:#eaeef5;margin:20px 0 6px}.sub{color:#8a93a3;font-size:13px;margin-bottom:16px}
code{background:#232733;padding:1px 5px;border-radius:4px;color:#e0c98a;font-size:12px}
table{border-collapse:collapse;width:100%;font-size:13px;margin:6px 0 14px}
th,td{border:1px solid #2c3340;padding:5px 9px;text-align:left;vertical-align:top}
th{background:#1e222b;color:#aab4c4}tr:nth-child(even) td{background:#191c23}
.lvl{color:#9db2d6;font-weight:700;white-space:nowrap}.role{color:#8a93a3;font-size:11px}
.toc{columns:3;font-size:13px}.toc a{color:#9db2d6;text-decoration:none}a.anchor{color:inherit;text-decoration:none}
.attr{color:#d9a441;font-size:12px;text-transform:uppercase;letter-spacing:.05em}
"""


def _e(s):
    return _html.escape(str(s))


def render_html(catalog):
    H = ['<!DOCTYPE html><html lang="en"><head><meta charset="utf-8">',
         '<meta name="viewport" content="width=device-width, initial-scale=1">',
         '<title>Bannerlord Perk Reference (v1.4.6)</title>', f'<style>{CSS}</style></head><body><div class="wrap">']
    H.append('<h1>Bannerlord Perk Reference</h1>')
    total = sum(len(t['perks']) for tiers in catalog.values() for t in tiers)
    H.append(f'<div class="sub">v1.4.6 — extracted from <code>DefaultPerks.cs</code>. {len(catalog)} skills, '
             f'{total} perks. A hero with <b>skill ≥ level</b> unlocks that tier; each tier is a pair — the '
             f'engine picks one for AI lords. <code>{{VALUE}}</code> rendered as the in-game number.</div>')
    H.append('<div class="toc">' + ' '.join(f'<a href="#s_{s}">{_e(s)}</a>' for s in catalog) + '</div>')
    last_attr = None
    for skill in catalog:
        attr = SKILL_ATTR.get(skill, '?')
        if attr != last_attr:
            H.append(f'<h2 class="attr">{_e(attr)}</h2>')
            last_attr = attr
        H.append(f'<h3 id="s_{skill}"><a class="anchor" href="#s_{skill}">{_e(skill)}</a></h3>')
        H.append('<table><tr><th>Lvl</th><th>Perk</th><th>Primary effect</th><th>Secondary effect</th></tr>')
        for tier in catalog[skill]:
            for p in tier['perks']:
                sec = p['secondary']
                sec_html = (f'{_e(sec["effect"])} <span class="role">[{_e(sec["role"])}]</span>'
                            if sec else '<span class="role">—</span>')
                H.append(f'<tr><td class="lvl">{tier["level"]}</td><td><b>{_e(p["name"])}</b></td>'
                         f'<td>{_e(p["primary"]["effect"])} <span class="role">[{_e(p["primary"]["role"])}]</span></td>'
                         f'<td>{sec_html}</td></tr>')
        H.append('</table>')
    H.append('</div></body></html>')
    return '\n'.join(H)


def main():
    ap = argparse.ArgumentParser(description='Extract the Bannerlord perk catalog from DefaultPerks.cs.')
    ap.add_argument('--defaultperks', default=DEFAULT_PERKS_PATH, help='Path to decompiled DefaultPerks.cs')
    ap.add_argument('--stdout', action='store_true', help='Print a summary')
    args = ap.parse_args()

    if not os.path.exists(args.defaultperks):
        raise SystemExit(f'ERROR: DefaultPerks.cs not found at {args.defaultperks}\n'
                         f'  Run: pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks')

    perks = parse_perks(args.defaultperks)
    catalog = build_catalog(perks)

    os.makedirs(DATA_DIR, exist_ok=True)
    with open(JSON_OUT, 'w', encoding='utf-8') as f:
        json.dump(catalog, f, indent=2)
    os.makedirs(HTML_DIR, exist_ok=True)
    with open(HTML_OUT, 'w', encoding='utf-8') as f:
        f.write(render_html(catalog))

    print(f'Parsed {len(perks)} perks across {len(catalog)} skills.')
    print(f'Wrote {os.path.relpath(JSON_OUT, REPO_ROOT)}')
    print(f'Wrote {os.path.relpath(HTML_OUT, REPO_ROOT)}')
    if args.stdout:
        for skill in catalog:
            tiers = catalog[skill]
            print(f'  {skill:12} {sum(len(t["perks"]) for t in tiers):2} perks, tiers '
                  f'{tiers[0]["level"]}..{tiers[-1]["level"]}')


if __name__ == '__main__':
    main()
