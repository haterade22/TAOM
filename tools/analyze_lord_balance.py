#!/usr/bin/env python3
"""
Per-culture LORD stats + perk review (READ-ONLY).

The lord analog of analyze_troop_balance.py. For every lord:
  - resolves the AUTHORITATIVE skills via skill_template -> SkillSet in taom_lord_skill_sets.xml
    (the engine ignores the inline <skills> block — that's documentation only);
  - compares the skill total against the rebalance_lords.py reference curve (archetype baseline +
    cultural modifier + age) — the same parity lens used for troops;
  - maps each skill level to the perks it unlocks (from tools/data/bannerlord_perks.json).

Emits one HTML per culture + an index + a data-quality summary. NEVER modifies lord data; the only
writes are the report files under tools/reports/lord-balance/.

Per-lord perk detail is deduplicated by SkillSet (lords with the same skill_template have identical
skills => identical unlocked perks); each lord row links to its profile's perk block.

Usage:
    python analyze_lord_balance.py            # write index + all per-culture files
    python analyze_lord_balance.py --stdout   # also print a summary
    python analyze_lord_balance.py --culture gondor   # one culture (quick iteration)
"""

import argparse
import html as _html
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rebalance_lords as rl  # noqa: E402  formula + parse_xslt/parse_xml_lords + culture map + archetype/legendary

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
SKILLSETS_PATH = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'taom_lord_skill_sets.xml')
PERKS_JSON = os.path.join(REPO_ROOT, 'tools', 'data', 'bannerlord_perks.json')
REPORT_DIR = os.path.join(REPO_ROOT, 'tools', 'reports', 'lord-balance')

SKILLS = rl.ALL_SKILLS
ABBR = {'OneHanded': '1H', 'TwoHanded': '2H', 'Polearm': 'Pol', 'Bow': 'Bow', 'Crossbow': 'Xbw',
        'Throwing': 'Thr', 'Riding': 'Rid', 'Athletics': 'Ath', 'Crafting': 'Cra', 'Scouting': 'Sco',
        'Tactics': 'Tac', 'Roguery': 'Rog', 'Charm': 'Chr', 'Leadership': 'Led', 'Trade': 'Trd',
        'Steward': 'Stw', 'Medicine': 'Med', 'Engineering': 'Eng'}


def _e(s):
    return _html.escape(str(s))


# =============================================================================
# Inputs
# =============================================================================

def load_skillsets():
    """{ setid (no 'SkillSet.' prefix): {skill: value} } from taom_lord_skill_sets.xml."""
    sets = {}
    root = ET.parse(SKILLSETS_PATH).getroot()
    for ss in root.findall('.//SkillSet'):
        sid = ss.get('id', '')
        skills = {s.get('id'): int(s.get('value', '0')) for s in ss.findall('skill') if s.get('id') in SKILLS}
        if sid:
            sets[sid] = skills
    return sets


def load_perks():
    with open(PERKS_JSON, encoding='utf-8') as f:
        return json.load(f)


def display_culture(culture_attr):
    """Culture.empire -> dunland; Culture.gondor -> gondor; etc."""
    if not culture_attr:
        return 'unknown'
    return rl.CULTURE_MAP.get(culture_attr, culture_attr.replace('Culture.', '') or 'unknown')


def read_lords():
    """All lords from lords.xml (TAOM-new) + lords.xslt (vanilla-transformed); xml wins on dup id."""
    lords = {}
    try:
        xslt_lords, _ = rl.parse_xslt(rl.XSLT_PATH)
        lords.update(xslt_lords)
    except Exception as ex:  # noqa: BLE001
        print(f'WARN: could not parse lords.xslt: {ex}', file=sys.stderr)
    try:
        xml_lords, _ = rl.parse_xml_lords(rl.XML_PATH)
        lords.update(xml_lords)  # xml wins
    except Exception as ex:  # noqa: BLE001
        print(f'WARN: could not parse lords.xml: {ex}', file=sys.stderr)
    return lords


# =============================================================================
# Analysis
# =============================================================================

def unlocked_perks(skills, catalog):
    """For each skill, the catalog tiers the lord's value unlocks. Returns {skill: [tier,...]}."""
    out = {}
    for skill in SKILLS:
        val = skills.get(skill, 0)
        tiers = [t for t in catalog.get(skill, []) if val >= t['level']]
        if tiers:
            out[skill] = tiers
    return out


def analyze(lords, skillsets, catalog):
    recs = []
    for lid, ld in lords.items():
        attrs = ld['attrs']
        skill_template = attrs.get('skill_template', '')
        setid = skill_template.replace('SkillSet.', '')
        resolved = setid in skillsets
        skills = dict(skillsets[setid]) if resolved else dict(ld.get('current_skills', {}))

        culture_attr = attrs.get('culture', '')
        try:
            age = int(float(attrs.get('age', '0') or 0))
        except ValueError:
            age = 0
        archetype, is_rookie, is_ruler = rl.detect_archetype(skill_template)
        legendary = lid in rl.LEGENDARY_IDS

        total = sum(skills.get(s, 0) for s in SKILLS)
        combat = sum(skills.get(s, 0) for s in rl.COMBAT_SKILLS)
        noncombat = sum(skills.get(s, 0) for s in rl.NONCOMBAT_SKILLS)

        inline = ld.get('current_skills', {})
        mismatch = bool(resolved and inline and any(inline.get(s, 0) != skills.get(s, 0) for s in SKILLS))

        recs.append({
            'id': lid, 'name': ld.get('name', lid), 'culture': display_culture(culture_attr),
            'culture_attr': culture_attr, 'age': age, 'archetype': archetype, 'is_rookie': is_rookie,
            'is_ruler': is_ruler, 'legendary': legendary, 'skill_template': skill_template, 'setid': setid,
            'resolved': resolved, 'skills': skills, 'total': total, 'combat': combat, 'noncombat': noncombat,
            'mismatch': mismatch, 'group': attrs.get('default_group', ''),
        })
    return recs


# =============================================================================
# HTML
# =============================================================================

CSS = """
:root{color-scheme:dark}*{box-sizing:border-box}
body{font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;margin:0;background:#15171c;color:#d8dee9;line-height:1.45}
.wrap{max-width:1700px;margin:0 auto;padding:22px 30px 90px}
h1{color:#fff;margin:0 0 4px;font-size:26px}h2{color:#fff;border-bottom:2px solid #2c3340;padding-bottom:6px;margin:34px 0 10px}
h3{color:#eaeef5;margin:18px 0 6px}.sub{color:#8a93a3;font-size:13px;margin-bottom:14px}
a{color:#9db2d6}code{background:#232733;padding:1px 5px;border-radius:4px;color:#e0c98a;font-size:12px}
table{border-collapse:collapse;font-size:12px;margin:8px 0 16px}
th,td{border:1px solid #2c3340;padding:3px 6px;text-align:center;white-space:nowrap}
th{background:#1e222b;color:#aab4c4;position:sticky;top:0}td.l,th.l{text-align:left}
tr:nth-child(even) td{background:#191c23}
.summary{display:flex;flex-wrap:wrap;gap:10px;margin:12px 0}
.card{background:#1e222b;border:1px solid #2c3340;border-radius:8px;padding:10px 14px;min-width:120px}
.card .n{font-size:22px;font-weight:700;color:#fff}.card .lbl{font-size:11px;color:#8a93a3;text-transform:uppercase;letter-spacing:.04em}
.callout{background:#20242e;border-left:4px solid #d9a441;padding:8px 14px;margin:8px 0;border-radius:4px;font-size:13px}
.callout.bad{border-left-color:#c0392b}
.lgd{color:#d9a441;font-weight:700}.rk{color:#8a93a3}
details{margin:6px 0;background:#181b22;border:1px solid #242a35;border-radius:6px;padding:4px 10px}
summary{cursor:pointer;color:#9db2d6;font-weight:600}
.pk-skill{margin:6px 0}.pk-skill b{color:#eaeef5}.pk-lvl{color:#7f8aa0}
.pk-eff{color:#aab4c4;font-size:12px}.pk-name{color:#cdd6e5}
.toc{columns:4;font-size:13px;margin:6px 0}
.legend span{display:inline-block;padding:1px 8px;border-radius:3px;margin-right:4px;color:#fff;font-size:11px}
"""


def total_bg(total):
    """Color a lord's skill total by raw magnitude (18 skills, cap 330 each → max 5940)."""
    if total <= 1500:
        return '#922b21'   # weak / rookie
    if total <= 2200:
        return '#b9770e'   # low-mid
    if total <= 2900:
        return '#1e7a45'   # mid
    if total <= 3600:
        return '#1f5d8c'   # strong
    return '#5e3a8c'        # elite / legendary / elf


def html_head(title):
    return ('<!DOCTYPE html><html lang="en"><head><meta charset="utf-8">'
            '<meta name="viewport" content="width=device-width, initial-scale=1">'
            f'<title>{_e(title)}</title><style>' + CSS + '</style></head><body><div class="wrap">')


def render_perk_block(setid, skills, catalog, used_by):
    """One deduped perk-detail block per SkillSet: every unlocked perk grouped by skill."""
    up = unlocked_perks(skills, catalog)
    n = sum(sum(len(t['perks']) for t in tiers) for tiers in up.values())
    H = [f'<details id="pk_{_e(setid)}"><summary>Unlocked perks for profile '
         f'<code>{_e(setid)}</code> — {n} perks across {len(up)} skills '
         f'({used_by} lord{"s" if used_by != 1 else ""})</summary>']
    H.append('<div class="sub">Each tier is a pair — the engine picks one for AI lords; both shown.</div>')
    for skill in SKILLS:
        if skill not in up:
            continue
        H.append(f'<div class="pk-skill"><b>{_e(skill)}</b> ({skills.get(skill, 0)}):')
        for t in up[skill]:
            names = ' / '.join(f'<span class="pk-name">{_e(p["name"])}</span>' for p in t['perks'])
            effs = ' &nbsp;·&nbsp; '.join(
                f'{_e(p["name"])}: {_e(p["primary"]["effect"])}'
                + (f' + {_e(p["secondary"]["effect"])}' if p['secondary'] else '')
                for p in t['perks'])
            H.append(f'<div><span class="pk-lvl">[{t["level"]}]</span> {names} '
                     f'<span class="pk-eff">— {effs}</span></div>')
        H.append('</div>')
    H.append('</details>')
    return '\n'.join(H)


def render_culture(culture, recs, catalog):
    recs = sorted(recs, key=lambda r: (-r['total'], r['name']))
    H = [html_head(f'Lords — {culture}')]
    H.append(f'<h1>Lords — {_e(culture)}</h1>')
    H.append('<div class="sub"><a href="index.html">← index</a> &nbsp;|&nbsp; '
             '<a href="perks.html">perk reference</a>. Skills resolved from each lord\'s '
             '<code>skill_template</code> → SkillSet (authoritative — the engine ignores the inline '
             '&lt;skills&gt; block). ★ = legendary, ·jr = rookie/junior. Total colored by magnitude; '
             'click a lord to jump to every perk their skills unlock.</div>')

    avg = round(sum(r['total'] for r in recs) / len(recs)) if recs else 0
    legend_n = sum(1 for r in recs if r['legendary'])
    unresolved = sum(1 for r in recs if not r['resolved'])
    mism = sum(1 for r in recs if r['mismatch'])
    H.append('<div class="summary">')
    for n, lbl in [(len(recs), 'lords'), (len({r['setid'] for r in recs}), 'distinct profiles'),
                   (avg, 'avg total'), (legend_n, 'legendary'),
                   (unresolved, 'unresolved template'), (mism, 'inline≠SkillSet')]:
        H.append(f'<div class="card"><div class="n">{n}</div><div class="lbl">{lbl}</div></div>')
    H.append('</div>')

    H.append('<p class="legend">Total magnitude: '
             '<span style="background:#922b21">≤1500</span><span style="background:#b9770e">≤2200</span>'
             '<span style="background:#1e7a45">≤2900</span><span style="background:#1f5d8c">≤3600</span>'
             '<span style="background:#5e3a8c">&gt;3600 elite</span></p>')

    # Flat table — one row per lord
    H.append('<table><tr><th class="l">Lord</th><th>Age</th><th class="l">Archetype</th>'
             + ''.join(f'<th>{ABBR[s]}</th>' for s in SKILLS)
             + '<th>Cbt</th><th>NonC</th><th>Total</th></tr>')
    for r in recs:
        tag = ' <span class="lgd">★</span>' if r['legendary'] else (' <span class="rk">·jr</span>' if r['is_rookie'] else '')
        cells = ''.join(f'<td>{r["skills"].get(s, 0)}</td>' for s in SKILLS)
        bg = total_bg(r['total'])
        tcell = f'<td style="background:{bg};color:#fff"><b>{r["total"]}</b></td>'
        H.append(f'<tr><td class="l"><a href="#pk_{_e(r["setid"])}">{_e(r["name"])}</a>{tag}</td>'
                 f'<td>{r["age"]}</td><td class="l">{_e(r["archetype"])}</td>{cells}'
                 f'<td>{r["combat"]}</td><td>{r["noncombat"]}</td>{tcell}</tr>')
    H.append('</table>')

    # Deduped perk-detail blocks (one per distinct SkillSet used in this culture)
    H.append('<h2>Unlocked perks by profile</h2>')
    H.append('<div class="sub">Click a lord\'s name above to jump to their profile. Profiles are shared '
             'by all lords with the same <code>skill_template</code>.</div>')
    seen = {}
    for r in recs:
        seen.setdefault(r['setid'], {'skills': r['skills'], 'count': 0})
        seen[r['setid']]['count'] += 1
    for setid in sorted(seen):
        H.append(render_perk_block(setid, seen[setid]['skills'], catalog, seen[setid]['count']))

    H.append('</div></body></html>')
    return '\n'.join(H)


def render_index(by_culture, all_recs):
    H = [html_head('Lord Balance — Index')]
    H.append('<h1>Lord Balance &amp; Perk Review</h1>')
    H.append('<div class="sub">Per-culture lord stats + every perk each lord\'s skills unlock. '
             'Authoritative skills from <code>skill_template</code> → <code>taom_lord_skill_sets.xml</code> '
             '(the engine ignores the inline &lt;skills&gt; block). '
             '<a href="perks.html">Full perk reference →</a></div>')

    total = len(all_recs)
    unresolved = [r for r in all_recs if not r['resolved']]
    mism = [r for r in all_recs if r['mismatch']]
    H.append('<div class="summary">')
    for n, lbl in [(total, 'lords'), (len(by_culture), 'cultures'),
                   (len({r['setid'] for r in all_recs}), 'distinct profiles'),
                   (len(unresolved), 'unresolved template'), (len(mism), 'inline≠SkillSet')]:
        H.append(f'<div class="card"><div class="n">{n}</div><div class="lbl">{lbl}</div></div>')
    H.append('</div>')

    H.append('<h2>Cultures</h2><table><tr><th class="l">Culture</th><th>Lords</th><th>Profiles</th>'
             '<th>Legendary</th><th>Avg total</th></tr>')
    for c in sorted(by_culture, key=lambda c: -len(by_culture[c])):
        rs = by_culture[c]
        avg = round(sum(r['total'] for r in rs) / len(rs)) if rs else 0
        lg = sum(1 for r in rs if r['legendary'])
        H.append(f'<tr><td class="l"><a href="{_e(c)}.html">{_e(c)}</a></td><td>{len(rs)}</td>'
                 f'<td>{len({r["setid"] for r in rs})}</td><td>{lg}</td><td>{avg}</td></tr>')
    H.append('</table>')

    # Data quality
    H.append('<h2>Data-quality</h2>')
    if not unresolved and not mism:
        H.append('<div class="callout">All lords resolved a SkillSet and the inline &lt;skills&gt; match. ✅</div>')
    if unresolved:
        ex = ', '.join(f'<code>{_e(r["id"])}</code>→<code>{_e(r["skill_template"])}</code>' for r in unresolved[:25])
        H.append(f'<div class="callout bad"><b>{len(unresolved)} lords reference a SkillSet not in '
                 f'taom_lord_skill_sets.xml</b> (skills fell back to the inline &lt;skills&gt; block — likely a '
                 f'vanilla <code>spc_*</code> template or a missing set): {ex}{" …" if len(unresolved) > 25 else ""}</div>')
    if mism:
        ex = ', '.join(f'<code>{_e(r["id"])}</code>' for r in mism[:25])
        H.append(f'<div class="callout"><b>{len(mism)} lords whose inline &lt;skills&gt; ≠ their SkillSet</b> '
                 f'(documentation is stale — engine uses the SkillSet): {ex}{" …" if len(mism) > 25 else ""}</div>')
    H.append('</div></body></html>')
    return '\n'.join(H)


# =============================================================================
# Main
# =============================================================================

def main():
    ap = argparse.ArgumentParser(description='Read-only per-culture lord stats + perk report.')
    ap.add_argument('--stdout', action='store_true', help='Print a summary')
    ap.add_argument('--culture', help='Only render this one culture (quick iteration)')
    args = ap.parse_args()

    if not os.path.exists(PERKS_JSON):
        raise SystemExit(f'ERROR: {PERKS_JSON} missing — run: python tools/extract_perks.py')

    skillsets = load_skillsets()
    catalog = load_perks()
    lords = read_lords()
    recs = analyze(lords, skillsets, catalog)

    by_culture = defaultdict(list)
    for r in recs:
        by_culture[r['culture']].append(r)

    os.makedirs(REPORT_DIR, exist_ok=True)
    written = []
    targets = [args.culture] if args.culture else sorted(by_culture)
    for c in targets:
        if c not in by_culture:
            print(f'  (no lords for culture {c})')
            continue
        path = os.path.join(REPORT_DIR, f'{c}.html')
        with open(path, 'w', encoding='utf-8') as f:
            f.write(render_culture(c, by_culture[c], catalog))
        written.append(c)
    if not args.culture:
        with open(os.path.join(REPORT_DIR, 'index.html'), 'w', encoding='utf-8') as f:
            f.write(render_index(by_culture, recs))

    print(f'Analysed {len(recs)} lords across {len(by_culture)} cultures '
          f'({len({r["setid"] for r in recs})} distinct profiles).')
    print(f'Wrote {len(written)} culture file(s) + index to {os.path.relpath(REPORT_DIR, REPO_ROOT)}')

    if args.stdout:
        unresolved = sum(1 for r in recs if not r['resolved'])
        mism = sum(1 for r in recs if r['mismatch'])
        print(f'  unresolved skill_template: {unresolved}   inline≠SkillSet: {mism}')
        for c in sorted(by_culture, key=lambda c: -len(by_culture[c])):
            rs = by_culture[c]
            print(f'  {c:18} {len(rs):4} lords  {len({r["setid"] for r in rs}):3} profiles  '
                  f'avg {round(sum(r["total"] for r in rs) / len(rs)):4}')


if __name__ == '__main__':
    main()
