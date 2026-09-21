#!/usr/bin/env python3
"""Render every troop in `troops_*.xml` as one HTML page, each slot priced.

Sibling to `docs/reference/ranged-troops.html` (same tokens, same fonts), built from the
melee ladder tooling (#631). For every troop: level and engine tier, group, every battle
equipment set with every slot, and beside each slot the number that matters for it:

  * armour: the item's `head_armor` / `body_armor` / `arm_armor` / `leg_armor`, summed
    the way the validator's `UPGRADE_ARMOUR_REGRESSION` sums them;
  * a melee weapon: displayed damage and sustained damage per second, from
    `tools/melee_damage.py` (the engine SIMULATES these from the crafting pieces, they are
    stored nowhere);
  * a bow, crossbow or thrown weapon: its `thrust_damage`, which is what the item carries;
  * a shield: its `hit_points`.

Per troop the summary line carries the armour total (averaged over the battle sets, since the
engine draws each slot from an independently chosen set) and the best melee blow and DPS.

Writes `docs/reference/troop-rosters.html`, tracked, so the page survives the gitignored
reports directory. Read-only against every input.

Usage:
    python tools/generate_troop_roster_page.py
    python tools/generate_troop_roster_page.py --out some/other.html
    python tools/generate_troop_roster_page.py --fragment path   # body-only copy, for publishing
"""

from __future__ import annotations

import argparse
import collections
import html
import statistics
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools"))

import analyze_melee_ladder as report  # noqa: E402
import melee_catalogue as mc  # noqa: E402
from ranged_ladder import engine_tier  # noqa: E402

try:
    from lxml import etree as LET
except ImportError:
    LET = None

MODULES = report.MODULES
TROOPS = report.TROOPS
OUT = REPO / "docs" / "reference" / "troop-rosters.html"

ARMOUR_ATTRS = ("head_armor", "body_armor", "arm_armor", "leg_armor")
ARMOUR_SLOTS = ("Head", "Body", "Cape", "Gloves", "Leg")
WEAPON_SLOTS = ("Item0", "Item1", "Item2", "Item3")
SLOT_ORDER = WEAPON_SLOTS + ARMOUR_SLOTS + ("Horse", "HorseHarness")

# The `troops_*.xml` file stem is the kingdom the page groups by, because the `culture=`
# attribute carries the engine StringId (vlandia for Rohan, sturgia for Dale, and eight files
# hold more than one culture). The file is what the maintainer thinks of as the kingdom.
KINGDOM_NAMES = {
    "troops_dale": "Dale",
    "troops_dolguldur": "Dol Guldur",
    "troops_dunland": "Dunland",
    "troops_erebor": "Erebor and the Iron Hills",
    "troops_goblin": "Goblins of the Misty Mountains",
    "troops_gondor": "Gondor",
    "troops_gundabad": "Gundabad",
    "troops_harad": "Harad",
    "troops_isengard": "Isengard",
    "troops_lindon": "Lindon",
    "troops_mirkwood": "Mirkwood",
    "troops_mordor": "Mordor",
    "troops_rhun_new": "Rhun",
    "troops_rivendell": "Rivendell",
    "troops_rohan": "Rohan",
    "troops_umbar": "Umbar",
}


# --- items --------------------------------------------------------------------------------


@dataclass
class ItemInfo:
    id: str
    name: str
    type: str
    armour: dict = field(default_factory=dict)   # attr -> int
    shield_hp: int | None = None
    thrust_damage: int | None = None
    swing_damage: int | None = None
    weapon_class: str = ""
    dps: float | None = None
    melee: bool = False

    @property
    def armour_total(self) -> int:
        return sum(self.armour.values())


def load_plain_items(modules: Path) -> dict[str, ItemInfo]:
    """Every `<Item>` across the loaded modules with the numbers this page shows."""
    out: dict[str, ItemInfo] = {}
    for module in sorted(p for p in modules.iterdir() if p.is_dir()):
        data = module / "ModuleData"
        if not data.is_dir():
            continue
        for path in sorted(data.rglob("*.xml")):
            try:
                root = LET.parse(str(path), LET.XMLParser(recover=True, huge_tree=True)).getroot()
            except (LET.XMLSyntaxError, OSError):
                continue
            if root is None:
                continue
            for node in root.iter("Item"):
                iid = node.get("id")
                if not iid or iid in out:
                    continue
                info = ItemInfo(iid, mc.strip_loc(node.get("name") or iid), node.get("Type") or "")
                armour = node.find(".//Armor")
                if armour is not None:
                    for attr in ARMOUR_ATTRS:
                        raw = armour.get(attr)
                        if raw:
                            try:
                                info.armour[attr] = int(float(raw))
                            except ValueError:
                                pass
                weapon = node.find(".//Weapon")
                if weapon is not None:
                    info.weapon_class = weapon.get("weapon_class") or ""
                    for attr, field_name in (("thrust_damage", "thrust_damage"),
                                             ("swing_damage", "swing_damage"),
                                             ("hit_points", "shield_hp")):
                        raw = weapon.get(attr)
                        if raw:
                            try:
                                setattr(info, field_name, int(float(raw)))
                            except ValueError:
                                pass
                out[iid] = info
    return out


# --- troops -------------------------------------------------------------------------------


@dataclass
class TroopSet:
    slots: dict[str, str]   # slot -> item id


@dataclass
class TroopRow:
    id: str
    name: str
    level: int
    culture: str
    group: str
    file: str
    is_hero: bool
    upgrades: list[str]
    sets: list[TroopSet]

    @property
    def tier(self) -> int:
        return engine_tier(self.level)


def load_troops_full(root: Path) -> list[TroopRow]:
    out: list[TroopRow] = []
    for path in sorted(root.glob("troops_*.xml")):
        tree = LET.parse(str(path), LET.XMLParser(recover=True, huge_tree=True)).getroot()
        if tree is None:
            continue
        for node in tree.iter("NPCCharacter"):
            tid = node.get("id")
            if not tid:
                continue
            try:
                level = int(node.get("level") or 0)
            except ValueError:
                level = 0
            sets: list[TroopSet] = []
            for block in node.iter("Equipments"):
                for roster in block.iter("EquipmentRoster"):
                    if (roster.get("equipmentType") or "") == "Civilian":
                        continue
                    slots: dict[str, str] = {}
                    for eq in roster.iter("equipment", "Equipment"):
                        ref, slot = eq.get("id"), eq.get("slot")
                        if ref and slot:
                            slots[slot] = ref.split(".", 1)[-1]
                    if slots:
                        sets.append(TroopSet(slots))
            upgrades = [
                (u.get("id") or "").split(".", 1)[-1]
                for u in node.iter("upgrade_target")
                if u.get("id")
            ]
            out.append(
                TroopRow(
                    id=tid,
                    name=mc.strip_loc(node.get("name") or tid),
                    level=level,
                    culture=(node.get("culture") or "").replace("Culture.", ""),
                    group=node.get("default_group") or "",
                    file=path.stem,
                    is_hero=(node.get("is_hero") or "").lower() == "true",
                    upgrades=upgrades,
                    sets=sets,
                )
            )
    return out


# --- pricing ------------------------------------------------------------------------------


def set_armour(s: TroopSet, items: dict[str, ItemInfo]) -> int:
    total = 0
    for slot in ARMOUR_SLOTS:
        info = items.get(s.slots.get(slot, ""))
        if info:
            total += info.armour_total
    return total


def best_melee(s: TroopSet, items: dict[str, ItemInfo]) -> ItemInfo | None:
    best = None
    for slot in WEAPON_SLOTS:
        info = items.get(s.slots.get(slot, ""))
        if info and info.melee and info.dps is not None:
            if best is None or info.dps > best.dps:
                best = info
    return best


# --- rendering ----------------------------------------------------------------------------


def esc(s: str) -> str:
    return html.escape(s, quote=True)


def slot_cell(slot: str, iid: str, items: dict[str, ItemInfo]) -> str:
    info = items.get(iid)
    if info is None:
        return (f'<div class="slot missing"><span class="k">{esc(slot)}</span>'
                f'<code>{esc(iid)}</code><span class="v">not found</span></div>')
    if info.melee and info.dps is not None:
        val = (f'<span class="v wpn"><b>{info.swing_damage or 0}</b>/<b>{info.thrust_damage or 0}</b>'
               f' <i>{info.dps:.0f} dps</i></span>')
        kind = "wpn"
    elif info.thrust_damage is not None and info.weapon_class in ("Bow", "Crossbow", "Javelin",
                                                                  "ThrowingAxe", "ThrowingKnife", "Stone"):
        val = f'<span class="v rng"><b>{info.thrust_damage}</b> <i>{esc(info.weapon_class.lower())}</i></span>'
        kind = "rng"
    elif info.shield_hp is not None:
        val = f'<span class="v shd"><b>{info.shield_hp}</b> <i>hp</i></span>'
        kind = "shd"
    elif info.armour:
        parts = " ".join(f"{k.split('_')[0][0]}{v}" for k, v in info.armour.items())
        val = f'<span class="v arm"><b>{info.armour_total}</b> <i>{esc(parts)}</i></span>'
        kind = "arm"
    else:
        val = '<span class="v">&mdash;</span>'
        kind = ""
    return (f'<div class="slot {kind}"><span class="k">{esc(slot)}</span>'
            f'<span class="n">{esc(info.name)}</span><code>{esc(iid)}</code>{val}</div>')


def render_troop(t: TroopRow, items: dict[str, ItemInfo]) -> str:
    armours = [set_armour(s, items) for s in t.sets]
    arm_avg = statistics.mean(armours) if armours else 0
    melee = [m for m in (best_melee(s, items) for s in t.sets) if m]
    best = max(melee, key=lambda m: m.dps) if melee else None
    rng = None
    for s in t.sets:
        for slot in WEAPON_SLOTS:
            info = items.get(s.slots.get(slot, ""))
            if info and not info.melee and info.thrust_damage and info.weapon_class in ("Bow", "Crossbow"):
                if rng is None or info.thrust_damage > rng.thrust_damage:
                    rng = info

    sets_html = []
    for i, s in enumerate(t.sets, 1):
        cells = "".join(
            slot_cell(slot, s.slots[slot], items) for slot in SLOT_ORDER if slot in s.slots
        )
        extra = "".join(
            slot_cell(slot, iid, items) for slot, iid in s.slots.items() if slot not in SLOT_ORDER
        )
        sets_html.append(
            f'<div class="set"><div class="set-h">Battle set {i} '
            f'<span>armour {set_armour(s, items)}</span></div>'
            f'<div class="slots">{cells}{extra}</div></div>'
        )
    upgrades = (
        '<div class="up">upgrades to ' + ", ".join(f"<code>{esc(u)}</code>" for u in t.upgrades) + "</div>"
        if t.upgrades else ""
    )
    hero = ' <span class="chip hero">hero</span>' if t.is_hero else ""
    best_html = (
        f'<span class="cell wpn"><b>{best.swing_damage or 0}/{best.thrust_damage or 0}</b>'
        f'<i>{best.dps:.0f} dps</i></span>' if best else '<span class="cell wpn"><b>&mdash;</b></span>'
    )
    rng_html = (
        f'<span class="cell rng"><b>{rng.thrust_damage}</b><i>{esc(rng.weapon_class.lower())}</i></span>'
        if rng else '<span class="cell rng"><b>&mdash;</b></span>'
    )
    search = esc(f"{t.id} {t.name} {t.culture} {t.group}".lower())
    return (
        f'<details class="troop" data-tier="{t.tier}" data-s="{search}">'
        f'<summary>'
        f'<span class="tier t{t.tier}">T{t.tier}</span>'
        f'<span class="who"><span class="nm">{esc(t.name)}{hero}</span><code>{esc(t.id)}</code></span>'
        f'<span class="cell lvl"><b>{t.level}</b><i>level</i></span>'
        f'<span class="cell grp"><b>{esc(t.group or "?")}</b><i>{esc(t.culture)}</i></span>'
        f'<span class="cell arm"><b>{arm_avg:.0f}</b><i>armour</i></span>'
        f'{best_html}{rng_html}'
        f'<span class="cell sets"><b>{len(t.sets)}</b><i>sets</i></span>'
        f'</summary>'
        f'<div class="body">{upgrades}{"".join(sets_html)}</div>'
        f'</details>'
    )


CSS = """
:root{--ground:#EEF0EA;--paper:#F7F8F5;--ink:#1C221E;--ink-2:#5B665F;--rule:#C9CFC6;--rule-2:#DFE3DC;
--arm:#3F5C7A;--arm-soft:#D9E1EA;--wpn:#8A4A3A;--wpn-soft:#EAD9D4;--rng:#3E6B4F;--rng-soft:#DCE5DA;--shd:#7A6A3F;--shd-soft:#EAE3D0;
--hi:#F1E9C6;--focus:#8A5A2B;
--t0:#E4EAE1;--t1:#DDE6DA;--t2:#D2DFCF;--t3:#C4D6C2;--t4:#B3CBB4;--t5:#9FBEA4;--t6:#88AF92;--t7:#6F9E7E;--t8:#578C6A;--t9:#3F7A57;--t10:#2B6446;--t-ink:#1C221E;--t-hi-ink:#F3F6F1;}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--ground:#161A17;--paper:#1E2320;--ink:#E3E7E0;--ink-2:#9AA69E;--rule:#3A423C;--rule-2:#2B3230;
--arm:#8FB0D0;--arm-soft:#22303F;--wpn:#D69A88;--wpn-soft:#3A2822;--rng:#7FB393;--rng-soft:#243A2C;--shd:#C9B87A;--shd-soft:#33301F;
--hi:#3A3520;--focus:#D9A066;
--t0:#242A26;--t1:#283029;--t2:#2C3A30;--t3:#334537;--t4:#3A5442;--t5:#43634D;--t6:#4E7A5E;--t7:#5A8A6A;--t8:#6A9C7A;--t9:#7FB393;--t10:#9CCBAC;--t-ink:#E3E7E0;--t-hi-ink:#111511;}}
:root[data-theme="dark"]{--ground:#161A17;--paper:#1E2320;--ink:#E3E7E0;--ink-2:#9AA69E;--rule:#3A423C;--rule-2:#2B3230;
--arm:#8FB0D0;--arm-soft:#22303F;--wpn:#D69A88;--wpn-soft:#3A2822;--rng:#7FB393;--rng-soft:#243A2C;--shd:#C9B87A;--shd-soft:#33301F;
--hi:#3A3520;--focus:#D9A066;
--t0:#242A26;--t1:#283029;--t2:#2C3A30;--t3:#334537;--t4:#3A5442;--t5:#43634D;--t6:#4E7A5E;--t7:#5A8A6A;--t8:#6A9C7A;--t9:#7FB393;--t10:#9CCBAC;--t-ink:#E3E7E0;--t-hi-ink:#111511;}
*{box-sizing:border-box}
body{margin:0;background:var(--ground);color:var(--ink);font:15px/1.5 "IBM Plex Sans",system-ui,Segoe UI,Roboto,sans-serif;padding-block:0 48px;padding-inline:clamp(16px,3vw,40px)}
h1,h2,h3{font-family:"Alegreya Sans","Gill Sans",Candara,sans-serif;text-wrap:balance;margin:0}
h1{font-size:clamp(30px,4.5vw,44px);font-weight:700;letter-spacing:-.01em;line-height:1.05}
h2{font-size:clamp(22px,3vw,28px);font-weight:700;line-height:1.15}
code{font-family:"IBM Plex Mono",ui-monospace,Consolas,monospace;font-size:.9em}
.eyebrow{font-family:"Alegreya Sans",sans-serif;text-transform:uppercase;letter-spacing:.12em;font-size:12px;color:var(--ink-2);font-weight:700}
header.mast{padding-block:40px 20px;max-width:1400px;margin:0 auto}
header.mast p{max-width:68ch;color:var(--ink-2);margin:12px 0 0}
main{max-width:1400px;margin:0 auto;display:grid;gap:32px}
.strip{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:1px;background:var(--rule);border:1px solid var(--rule)}
.strip div{background:var(--paper);padding:14px 16px}
.strip b{display:block;font-family:"Alegreya Sans",sans-serif;font-size:28px;font-weight:700;line-height:1;font-variant-numeric:tabular-nums}
.strip span{color:var(--ink-2);font-size:13px}
.read{border-top:2px solid var(--ink);padding-top:14px;display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:12px 36px}
.read p{margin:0;max-width:62ch;font-size:14px;color:var(--ink-2)}
.read p b{color:var(--ink)}
.key{display:inline-block;padding:0 6px;border-radius:3px;font-weight:600}
.key.arm{background:var(--arm-soft);color:var(--arm)}.key.wpn{background:var(--wpn-soft);color:var(--wpn)}.key.rng{background:var(--rng-soft);color:var(--rng)}.key.shd{background:var(--shd-soft);color:var(--shd)}
.bar{position:sticky;top:env(safe-area-inset-top,0px);z-index:5;background:var(--ground);padding-block:10px;border-bottom:1px solid var(--rule);display:flex;flex-wrap:wrap;gap:10px 18px;align-items:center;font-size:14px}
.bar input[type=search]{font:inherit;padding:6px 10px;border:1px solid var(--rule);background:var(--paper);color:var(--ink);min-width:min(280px,100%)}
.bar input:focus-visible,.bar select:focus-visible,summary:focus-visible{outline:2px solid var(--focus);outline-offset:2px}
.bar select{font:inherit;padding:6px 8px;border:1px solid var(--rule);background:var(--paper);color:var(--ink)}
.bar .n{color:var(--ink-2);margin-left:auto;font-variant-numeric:tabular-nums}
nav.kingdoms{display:flex;flex-wrap:wrap;gap:6px 10px}
nav.kingdoms a{color:var(--ink);text-decoration:none;font-size:14px;padding:4px 8px;border:1px solid var(--rule);background:var(--paper)}
nav.kingdoms a b{font-family:"Alegreya Sans",sans-serif;color:var(--ink-2);margin-left:6px;font-weight:400}
nav.kingdoms a:hover,nav.kingdoms a:focus-visible{border-color:var(--ink);outline:none}
section.kingdom{display:grid;gap:8px}
section.kingdom>header{display:flex;flex-wrap:wrap;align-items:baseline;gap:8px 18px;border-top:2px solid var(--ink);padding-top:10px}
section.kingdom>header .count{color:var(--ink-2);font-size:14px;margin-left:auto;font-variant-numeric:tabular-nums}
details.troop{background:var(--paper);border:1px solid var(--rule);border-top:none}
details.troop:first-of-type{border-top:1px solid var(--rule)}
details.troop[hidden]{display:none}
summary{list-style:none;cursor:pointer;display:grid;grid-template-columns:44px minmax(180px,2.2fr) repeat(5,minmax(70px,.8fr)) 52px;gap:10px;align-items:center;padding:8px 12px;font-variant-numeric:tabular-nums}
summary::-webkit-details-marker{display:none}
summary:hover{background:var(--rule-2)}
details[open]>summary{border-bottom:1px solid var(--rule-2);background:var(--hi)}
.tier{display:inline-grid;place-items:center;width:38px;height:24px;border-radius:3px;font-family:"Alegreya Sans",sans-serif;font-weight:700;font-size:13px;color:var(--t-ink)}
.t0{background:var(--t0)}.t1{background:var(--t1)}.t2{background:var(--t2)}.t3{background:var(--t3)}.t4{background:var(--t4)}.t5{background:var(--t5)}
.t6{background:var(--t6);color:var(--t-hi-ink)}.t7{background:var(--t7);color:var(--t-hi-ink)}.t8{background:var(--t8);color:var(--t-hi-ink)}.t9{background:var(--t9);color:var(--t-hi-ink)}.t10{background:var(--t10);color:var(--t-hi-ink)}
.who{display:grid;gap:1px;min-width:0}
.who .nm{font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.who code{color:var(--ink-2);font-size:12px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.chip{display:inline-block;font-size:10px;text-transform:uppercase;letter-spacing:.08em;padding:0 5px;border-radius:2px;margin-left:6px;vertical-align:middle;background:var(--hi);color:var(--ink)}
.cell{display:grid;gap:0;line-height:1.15}
.cell b{font-size:15px;font-weight:600}
.cell i{font-style:normal;font-size:11px;color:var(--ink-2);text-transform:uppercase;letter-spacing:.06em}
.cell.arm b{color:var(--arm)}.cell.wpn b{color:var(--wpn)}.cell.rng b{color:var(--rng)}
.body{padding:10px 12px 14px;display:grid;gap:10px}
.up{font-size:13px;color:var(--ink-2)}
.set{border:1px solid var(--rule-2)}
.set-h{font-family:"Alegreya Sans",sans-serif;font-size:13px;text-transform:uppercase;letter-spacing:.08em;color:var(--ink-2);padding:6px 10px;border-bottom:1px solid var(--rule-2);display:flex;gap:12px}
.set-h span{margin-left:auto;color:var(--arm);font-weight:700;letter-spacing:0;text-transform:none}
.slots{display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:1px;background:var(--rule-2)}
.slot{background:var(--paper);padding:6px 10px;display:grid;grid-template-columns:auto 1fr;gap:0 8px;align-items:baseline;font-size:13px;min-width:0}
.slot .k{grid-row:1/3;font-family:"Alegreya Sans",sans-serif;font-size:11px;text-transform:uppercase;letter-spacing:.08em;color:var(--ink-2);width:52px}
.slot .n{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.slot code{color:var(--ink-2);font-size:11px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;grid-column:2}
.slot .v{grid-column:2;font-variant-numeric:tabular-nums;font-size:12px}
.slot .v b{font-size:13px}
.slot .v i{font-style:normal;color:var(--ink-2)}
.slot.arm .v b{color:var(--arm)}.slot.wpn .v b{color:var(--wpn)}.slot.rng .v b{color:var(--rng)}.slot.shd .v b{color:var(--shd)}
.slot.missing{background:var(--wpn-soft)}
@media (max-width:820px){summary{grid-template-columns:44px 1fr repeat(3,minmax(60px,.7fr));grid-auto-rows:auto}
 .cell.grp,.cell.sets,.cell.lvl{display:none}}
@media (prefers-reduced-motion:no-preference){details.troop{transition:background .15s}}
"""

JS = """
(function(){
  var q=document.getElementById('q'),tier=document.getElementById('tier'),n=document.getElementById('n');
  var all=Array.prototype.slice.call(document.querySelectorAll('details.troop'));
  var secs=Array.prototype.slice.call(document.querySelectorAll('section.kingdom'));
  function apply(){
    var s=(q.value||'').trim().toLowerCase(),t=tier.value,shown=0;
    all.forEach(function(d){
      var ok=(!s||d.getAttribute('data-s').indexOf(s)>=0)&&(t===''||d.getAttribute('data-tier')===t);
      d.hidden=!ok; if(ok)shown++;
    });
    secs.forEach(function(sec){
      var any=sec.querySelector('details.troop:not([hidden])');
      sec.hidden=!any;
    });
    n.textContent=shown+' of '+all.length+' troops';
    try{localStorage.setItem('troop-rosters.q',s);localStorage.setItem('troop-rosters.tier',t);}catch(e){}
  }
  try{q.value=localStorage.getItem('troop-rosters.q')||'';tier.value=localStorage.getItem('troop-rosters.tier')||'';}catch(e){}
  q.addEventListener('input',apply);tier.addEventListener('change',apply);apply();
})();
"""


def build(items: dict[str, ItemInfo], troops: list[TroopRow]) -> str:
    by_file: dict[str, list[TroopRow]] = collections.defaultdict(list)
    for t in troops:
        by_file[t.file].append(t)
    for rows in by_file.values():
        rows.sort(key=lambda t: (t.level, t.name))

    armed = [t for t in troops if any(best_melee(s, items) for s in t.sets)]
    n_sets = sum(len(t.sets) for t in troops)
    missing = sum(
        1 for t in troops for s in t.sets for iid in s.slots.values() if iid not in items
    )
    stamp = time.strftime("%Y-%m-%d %H:%M")

    L: list[str] = []
    A = L.append
    A("<title>Troop Rosters of Middle-earth</title>")
    A('<link rel="preconnect" href="https://fonts.googleapis.com">')
    A('<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Alegreya+Sans:wght@400;700&family=IBM+Plex+Sans:wght@400;600&family=IBM+Plex+Mono&display=swap">')
    A(f"<style>{CSS}</style>")
    A('<header class="mast">')
    A('<div class="eyebrow">TAOM reference</div>')
    A("<h1>Troop Rosters of Middle-earth</h1>")
    A(f"<p>Every troop in <code>troops_*.xml</code> with every battle equipment set, each slot "
      f"priced. Generated {stamp} by <code>tools/generate_troop_roster_page.py</code> from the "
      f"live install; damage and DPS are simulated from the crafting pieces by "
      f"<code>tools/melee_damage.py</code>, since the engine stores neither.</p>")
    A("</header>")
    A("<main>")
    A('<div class="strip">')
    A(f"<div><b>{len(troops)}</b><span>troops</span></div>")
    A(f"<div><b>{len(by_file)}</b><span>kingdom files</span></div>")
    A(f"<div><b>{n_sets}</b><span>battle sets</span></div>")
    A(f"<div><b>{len(armed)}</b><span>carry a melee weapon</span></div>")
    A(f"<div><b>{missing}</b><span>slots naming an unknown item</span></div>")
    A("</div>")
    A('<div class="read">')
    A('<p><b>Tier</b> is the engine\'s <code>clamp(ceil((level-5)/5), 0, 10)</code>, the number the '
      'ladders key on. <b>Armour</b> on a troop is the average across its battle sets of the sum of '
      '<code>head</code>, <code>body</code>, <code>arm</code> and <code>leg</code> armour on every worn '
      'piece, the way the validator scores an upgrade; each set shows its own total.</p>')
    A('<p><span class="key wpn">weapon</span> shows <b>swing/thrust</b> displayed damage and sustained '
      '<b>dps</b> from the mode that hits hardest. <span class="key rng">ranged</span> is the launcher\'s '
      '<code>thrust_damage</code>. <span class="key shd">shield</span> is hit points. '
      '<span class="key arm">armour</span> is the piece\'s total, then per attribute (h head, b body, a arm, l leg).</p>')
    A("</div>")
    A('<div class="bar">')
    A('<label for="q">Find</label><input id="q" type="search" placeholder="name, id, culture, group" autocomplete="off">')
    A('<label for="tier">Tier</label><select id="tier"><option value="">all</option>'
      + "".join(f'<option value="{t}">T{t}</option>' for t in range(11)) + "</select>")
    A(f'<span class="n" id="n">{len(troops)} of {len(troops)} troops</span>')
    A("</div>")
    A('<nav class="kingdoms">')
    for stem in sorted(by_file, key=lambda s: KINGDOM_NAMES.get(s, s)):
        A(f'<a href="#{esc(stem)}">{esc(KINGDOM_NAMES.get(stem, stem))}<b>{len(by_file[stem])}</b></a>')
    A("</nav>")
    for stem in sorted(by_file, key=lambda s: KINGDOM_NAMES.get(s, s)):
        rows = by_file[stem]
        A(f'<section class="kingdom" id="{esc(stem)}">')
        A(f"<header><h2>{esc(KINGDOM_NAMES.get(stem, stem))}</h2><code>{esc(stem)}.xml</code>"
          f'<span class="count">{len(rows)} troops</span></header>')
        A("<div>")
        for t in rows:
            A(render_troop(t, items))
        A("</div>")
        A("</section>")
    A("</main>")
    A(f"<script>{JS}</script>")
    return "\n".join(L) + "\n"


def wrap_document(fragment: str) -> str:
    """The standalone file: the fragment's first line is the <title>, the rest is the body."""
    title_line, body = fragment.split("\n", 1)
    head = (
        "<!DOCTYPE html>", '<html lang="en">', "<head>", '<meta charset="utf-8">',
        '<meta name="viewport" content="width=device-width, initial-scale=1">', title_line, "</head>", "<body>",
    )
    return "\n".join(head) + "\n" + body + "</body>\n</html>\n"


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--out", default=str(OUT), help="standalone HTML to write")
    ap.add_argument("--fragment", default="", help="also write a body-only copy here")
    args = ap.parse_args(argv)

    if LET is None:
        print("SKIPPED: lxml is required", file=sys.stderr)
        return 2
    if not MODULES.is_dir():
        print(f"SKIPPED: no Bannerlord install at {MODULES}", file=sys.stderr)
        return 2

    items = load_plain_items(MODULES)
    cat = mc.load(MODULES, item_modules={report.ARMORY_MODULE, "SandBoxCore", "SandBox", "Native"})
    priced, failures = mc.price_all(cat)
    for iid, p in priced.items():
        info = items.get(iid) or ItemInfo(iid, p.item.name, "")
        info.melee = True
        info.swing_damage = p.swing_damage
        info.thrust_damage = p.thrust_damage
        info.dps = p.best_dps
        info.weapon_class = p.item.template
        items[iid] = info
    # Crafted weapons that could not be priced still deserve a name and a slot.
    for iid, item in cat.items.items():
        if iid not in items:
            items[iid] = ItemInfo(iid, item.name, item.template)

    troops = load_troops_full(TROOPS)
    fragment = build(items, troops)

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(wrap_document(fragment), encoding="utf-8")
    print(f"wrote {out} ({len(troops)} troops, {len(items)} items indexed, "
          f"{len(priced)} melee weapons priced, {len(failures)} unpriced)")
    if args.fragment:
        Path(args.fragment).write_text(fragment, encoding="utf-8")
        print(f"wrote fragment {args.fragment}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
