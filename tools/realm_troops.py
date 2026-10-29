#!/usr/bin/env python3
"""Shared machinery for the per-realm troop generators of TAOM's newer kingdoms.

tools/generate_arthedain_troops.py, generate_cardolan_troops.py, generate_rhudaur_troops.py and
generate_eredluin_troops.py each hold one realm's DESIGN (its troops, party templates, militia and
tavern mercenary) and bind the functions here to a Realm. Everything that is the same for all of
them lives here: the skill curves, the Troop record and its XML, the upgrade skill clamp, the
TAOM-wide militia rule, the tavern mercenary copy, the self-check, and the byte-faithful write of
troops_<culture>.xml plus the realm's marker region in taom_partyTemplates.xml.

Moved out of generate_arthedain_troops.py on 2026-10-23 when Cardolan and Rhudaur were added; the
Arthedain output is byte-identical (its --check proves it). Ered Luin (2026-10-27) added the
opt-in Realm fields `race`, `body` and `ladder_line` and the `crossbow` role for a dwarf realm;
their defaults keep the three Arnor files byte-identical.
"""
import argparse
import functools
import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable

import ranged_ladder as rl
from _marker_region import replace_region  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
MD = ROOT / "Main" / "_Module" / "ModuleData"
PARTY = MD / "taom_partyTemplates.xml"

SKILLS = ("Athletics", "Riding", "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing")

# Skill curves by level, calibrated a step under Gondor's per-level medians (troops_gondor.xml,
# 2026-10-07) and a step over Dale's: good soldiers, few of them.
ATH = {6: 38, 11: 52, 16: 80, 21: 95, 26: 105, 31: 120, 36: 140}
PRIMARY = {6: 40, 11: 70, 16: 100, 21: 130, 26: 175, 31: 225, 36: 270}
SECONDARY = {6: 30, 11: 55, 16: 80, 21: 105, 26: 140, 31: 180, 36: 220}
LOW = {6: 10, 11: 15, 16: 20, 21: 25, 26: 30, 31: 35, 36: 40}
RIDING = {16: 90, 21: 120, 26: 160, 31: 205, 36: 250}  # 16 and 36: Cardolan's riders and capstone

# The culture's four militia slots follow the TAOM-wide militia rule (tools/rebalance_troops.py,
# tools/tests/test_militia_elite_bonus.py): every militia slot takes the level-21 baseline whatever
# its level, and the elite slot is the basic one plus MILITIA_ELITE_BONUS on every skill. The one
# exception is a laddered archer's Bow, which is its own ladder cell (#617).
MILITIA_BASELINE_LEVEL = 21
MILITIA_ELITE_BONUS = 15  # rebalance_troops.MILITIA_ELITE_BONUS

CIVILIAN_SET = "battania_troop_civilian_template_t2"  # what every Gondor troop uses
BODY = "fighter_gondor"


@dataclass
class Realm:
    culture: str                 # culture id, ranged-ladder line id and troop id prefix
    label: str                   # the bracket tag in every troop name: "[Arthedain]"
    script: str                  # the generator file name, for the GENERATED header
    pt_marker: str               # the realm's region in taom_partyTemplates.xml
    militia: dict                # {basic militia id: veteran militia id}
    merc_source: str             # the rarest recruitment-pool entry, copied for the tavern
    merc_name: str
    armour_prefix: str = "sk_ar_art_"
    race: str = None             # the race= attribute every troop carries; None for men (no attribute)
    body: str = BODY             # the face_key_template BodyProperty
    ladder_line: str = None      # the ranged-ladder line its bows come from; None means the culture's own

    @property
    def troops_file(self) -> Path:
        return MD / "troops" / f"troops_{self.culture}.xml"

    @property
    def line(self) -> str:
        return self.ladder_line or self.culture


@functools.lru_cache(maxsize=None)
def bow_skill(line, level):
    """The ladder cell for this level's engine tier: what rebalance_ranged_ladders.py writes too.
    One cell per line and tier, whichever launcher class the troop carries."""
    return rl.skill_cell(line, rl.engine_tier(level), rl.load_spec())


def skills(realm, level, role):
    """One skill block per role. Roles: spear, sword, twohand, ranged, crossbow, cavalry."""
    s = dict.fromkeys(SKILLS, 0)
    s["Athletics"] = ATH[level]
    s["Throwing"] = LOW[level]
    s["Crossbow"] = LOW[level]
    if role == "spear":
        s.update(Polearm=PRIMARY[level], OneHanded=SECONDARY[level], TwoHanded=LOW[level] * 2,
                 Bow=LOW[level])
    elif role == "sword":
        s.update(OneHanded=PRIMARY[level], Polearm=SECONDARY[level], TwoHanded=SECONDARY[level] // 2,
                 Bow=LOW[level])
    elif role == "twohand":
        s.update(TwoHanded=PRIMARY[level], Polearm=SECONDARY[level], OneHanded=SECONDARY[level],
                 Bow=LOW[level])
    elif role == "ranged":
        s.update(Bow=bow_skill(realm.line, level), OneHanded=SECONDARY[level], Polearm=LOW[level] * 2,
                 TwoHanded=LOW[level] * 2)
    elif role == "crossbow":
        s.update(Crossbow=bow_skill(realm.line, level), OneHanded=SECONDARY[level], Polearm=LOW[level] * 2,
                 TwoHanded=LOW[level] * 2, Bow=LOW[level])
    elif role == "cavalry":
        s.update(Riding=RIDING[level], Polearm=PRIMARY[level], OneHanded=SECONDARY[level],
                 TwoHanded=LOW[level] * 2, Bow=LOW[level], Athletics=ATH[level] - 10)
    else:
        raise ValueError(role)
    return s


def armour(realm, head, body, leg, gloves, cape):
    """Two looks of one armour set. Each argument is a (roster_a, roster_b) pair of piece ids
    without the realm's armour prefix, or None to leave the slot empty in BOTH rosters. A piece
    id that starts with '=' is a full item id and takes no prefix (a Gondor piece in an Arnor kit)."""
    def full(piece):
        return piece[1:] if piece.startswith("=") else f"{realm.armour_prefix}{piece}"
    out = ({}, {})
    for slot, pair in (("Head", head), ("Body", body), ("Leg", leg), ("Gloves", gloves), ("Cape", cape)):
        if pair is None:
            continue
        out[0][slot] = full(pair[0])
        out[1][slot] = full(pair[1])
    return out


def ab(stem):
    """The usual a/b pair of one piece: ab('chest_inf_med') -> ('chest_inf_med_a', 'chest_inf_med_b')."""
    return (f"{stem}_a", f"{stem}_b")


def one(piece):
    """A piece with a single variant, worn in both rosters."""
    return (piece, piece)


def same(*weapons):
    """The same weapon list in both rosters."""
    return (list(weapons), list(weapons))


def paired(*pairs):
    """Per-slot (roster_a, roster_b) weapon picks: paired(('x_a', 'x_b'), 'shield')."""
    ra, rb = [], []
    for p in pairs:
        if isinstance(p, tuple):
            ra.append(p[0])
            rb.append(p[1])
        else:
            ra.append(p)
            rb.append(p)
    return (ra, rb)


def bow(realm, level):
    return rl.ladder_id(realm.line, "Bow", rl.engine_tier(level))


def crossbow(realm, level):
    return rl.ladder_id(realm.line, "Crossbow", rl.engine_tier(level))


@dataclass
class Troop:
    id: str
    name: str
    level: int
    group: str
    role: str
    weapons: tuple          # (roster_a weapons, roster_b weapons): each a list of Item0.. ids
    gear: tuple             # (roster_a armour dict, roster_b armour dict)
    upgrades: list = field(default_factory=list)
    basic: bool = False
    horse: tuple = None     # (horse, harness), the same in both rosters
    sk: dict = None         # set by clamp_skills(); the curve value before that
    occupation: str = "Soldier"
    realm: Realm = None     # set by the realm's build(); the realm that writes this troop

    def xml(self, nl):
        sk = self.sk or skills(self.realm, self.level, self.role)
        lines = [
            "  <NPCCharacter",
            f'      id="{self.id}"',
        ]
        if self.realm.race:
            lines.append(f'      race="{self.realm.race}"')
        lines += [
            f'      default_group="{self.group}"',
            f'      level="{self.level}"',
            f'      name="{{=aom_{self.id}_name}}{self.realm.label} {self.name}"',
            f'      occupation="{self.occupation}"',
            f'      culture="Culture.{self.realm.culture}"' + ("" if self.basic else ">"),
        ]
        if self.basic:
            lines.append('      is_basic_troop="true">')
        lines += ["    <face>", f'      <face_key_template value="BodyProperty.{self.realm.body}" />', "    </face>",
                  "    <skills>"]
        lines += [f'      <skill id="{k}" value="{sk[k]}" />' for k in SKILLS]
        lines.append("    </skills>")
        if self.upgrades:
            lines.append("    <upgrade_targets>")
            lines += [f'      <upgrade_target id="NPCCharacter.{u}" />' for u in self.upgrades]
            lines.append("    </upgrade_targets>")
        else:
            lines.append("    <upgrade_targets></upgrade_targets>")
        lines.append("    <Equipments>")
        for weapons, gear in zip(self.weapons, self.gear):
            lines.append("      <EquipmentRoster>")
            for i, w in enumerate(weapons):
                lines.append(f'        <equipment slot="Item{i}" id="Item.{w}" />')
            for slot in ("Head", "Body", "Cape", "Gloves", "Leg"):
                if slot in gear:
                    lines.append(f'        <equipment slot="{slot}" id="Item.{gear[slot]}" />')
            if self.horse:
                lines.append(f'        <equipment slot="Horse" id="Item.{self.horse[0]}" />')
                lines.append(f'        <equipment slot="HorseHarness" id="Item.{self.horse[1]}" />')
            lines.append("      </EquipmentRoster>")
        lines.append(f'      <EquipmentSet id="{CIVILIAN_SET}" equipmentType="Civilian" />')
        lines += ["    </Equipments>", "  </NPCCharacter>"]
        return nl.join(lines)


def external_floors(realm, troop_ids):
    """Skills of the NPCs outside the troop file that upgrade INTO the tree (the cloned
    villager_<culture> -> <culture>_levy edge), so the clamp can hold the target above them."""
    npcs = MD / "characters" / f"npcs_{realm.culture}.xml"
    floors = {}
    if not npcs.exists():
        return floors
    text = npcs.read_bytes().decode("utf-8-sig")
    for block in re.findall(r"<NPCCharacter\b.*?</NPCCharacter>", text, re.DOTALL):
        targets = [t for t in re.findall(r'upgrade_target id="NPCCharacter\.([^"]+)"', block) if t in troop_ids]
        if not targets:
            continue
        sk = {k: int(v) for k, v in re.findall(r'<skill id="(\w+)" value="(\d+)"', block)}
        for t in targets:
            floors.setdefault(t, dict.fromkeys(SKILLS, 0))
            for k in SKILLS:
                floors[t][k] = max(floors[t][k], sk.get(k, 0))
    return floors


def militia_step(realm, by_id):
    for basic_id, vet_id in realm.militia.items():
        basic, vet = by_id[basic_id], by_id[vet_id]
        basic.sk = skills(realm, MILITIA_BASELINE_LEVEL, basic.role)
        vet.sk = {k: v + MILITIA_ELITE_BONUS for k, v in basic.sk.items()}
        if basic.role == "ranged":
            basic.sk["Bow"] = bow_skill(realm.line, basic.level)
            vet.sk["Bow"] = bow_skill(realm.line, vet.level)


def clamp_skills(realm, troops):
    """An upgrade never lowers a skill (UPGRADE_SKILL_REGRESSION). Every edge raises its level,
    so one pass in level order carries each source's skills into its targets."""
    by_id = {t.id: t for t in troops}
    for t in troops:
        t.sk = skills(realm, t.level, t.role)
    for tid, floor in external_floors(realm, set(by_id)).items():
        for k in SKILLS:
            by_id[tid].sk[k] = max(by_id[tid].sk[k], floor[k])
    militia_step(realm, by_id)
    for t in sorted(troops, key=lambda x: x.level):
        for u in t.upgrades:
            for k in SKILLS:
                by_id[u].sk[k] = max(by_id[u].sk[k], t.sk[k])
    return troops


def add_mercenary(realm, troops):
    """The tavern sells a leaf copy of the RAREST recruitment-pool entry (TavernMercenaryDataTests):
    occupation Mercenary, so the hire dialogue fires, and no upgrade targets, so the engine's
    upgrade walk cannot drift onto a Soldier line troop."""
    src = next(t for t in troops if t.id == realm.merc_source)
    merc = Troop(f"{src.id}_merc", realm.merc_name, src.level, src.group, src.role,
                 src.weapons, src.gear, occupation="Mercenary", realm=realm)
    merc.sk = dict(src.sk)
    return troops + [merc]


def templates_xml(party_templates, nl):
    out = []
    for tid, stacks in party_templates():
        out.append(f'\t<MBPartyTemplate id="{tid}">')
        out.append("\t\t<stacks>")
        for troop, lo, hi in stacks:
            out.append(f'\t\t\t<PartyTemplateStack min_value="{lo}" max_value="{hi}" troop="NPCCharacter.{troop}" />')
        out.append("\t\t</stacks>")
        out.append("\t</MBPartyTemplate>")
        out.append("")
    return nl.join(out)


def troops_xml(realm, troops, nl):
    head = ['<?xml version="1.0" encoding="utf-8"?>',
            f"<!-- GENERATED by tools/{realm.script}. Edit the script, not this file. -->",
            "<NPCCharacters>"]
    body = (nl + nl).join(tr.xml(nl) for tr in troops)
    return nl.join(head) + nl + body + nl + "</NPCCharacters>" + nl


def self_check(party_templates, troops):
    """Structural invariants the validator would otherwise report after the fact."""
    ids = [tr.id for tr in troops]
    errors = []
    if len(ids) != len(set(ids)):
        errors.append("duplicate troop id")
    known = set(ids)
    by_id = {tr.id: tr for tr in troops}
    for tr in troops:
        for u in tr.upgrades:
            if u not in known:
                errors.append(f"{tr.id}: upgrade target {u} is not defined")
            elif rl.engine_tier(by_id[u].level) <= rl.engine_tier(tr.level):
                errors.append(f"{tr.id} -> {u}: target tier does not rise (UPGRADE_TIER_COLLAPSE)")
        ra, rb = tr.weapons
        if len(ra) != len(rb):
            errors.append(f"{tr.id}: the two rosters fill different weapon slots")
        if set(tr.gear[0]) != set(tr.gear[1]):
            errors.append(f"{tr.id}: the two rosters fill different armour slots")
        if "Body" not in tr.gear[0]:
            errors.append(f"{tr.id}: no Body armour (MISSING_BODY_ARMOUR)")
        if tr.realm and tr.realm.race == "dwarf" and (tr.horse or tr.group in ("Cavalry", "HorseArcher")):
            errors.append(f"{tr.id}: a mounted dwarf (MOUNTED_DWARF)")
        shields = any("shield" in w for w in ra + rb)
        twohanders = any(re.search(r"greatsword|poleaxe|great_axe|_2h_", w) for w in ra + rb)
        if shields and twohanders:
            errors.append(f"{tr.id}: a shield and a two-handed weapon in the same troop")
    for tid, stacks in party_templates():
        for troop, lo, hi in stacks:
            if troop not in known:
                errors.append(f"{tid}: names undefined troop {troop}")
            if lo > hi:
                errors.append(f"{tid}: {troop} min {lo} > max {hi}")
    return errors


def read_bytes_faithful(path):
    raw = path.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw[3:].decode("utf-8") if bom else raw.decode("utf-8")
    nl = "\r\n" if "\r\n" in text else "\n"
    return text, nl, bom


def upsert_templates(realm, text, nl, payload):
    """Replace this realm's region in place, or insert it before </partyTemplates> the first time.
    In place, not strip-and-append: promote_borrowed_cultures.py appends its own region at the same
    spot, and strip-and-append would swap the two regions on every alternate run. A second region or a
    lone marker is refused (#975)."""
    marker = realm.pt_marker
    block = f"\t<!-- {marker}:BEGIN -->{nl}{payload}\t<!-- {marker}:END -->{nl}"
    return replace_region(text, marker, block, "</partyTemplates>")


def run(realm: Realm, build: Callable, party_templates: Callable, argv=None):
    """The generator's command line: dry run, --apply or --check."""
    ap = argparse.ArgumentParser()
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--check", action="store_true")
    args = ap.parse_args(argv)

    troops = add_mercenary(realm, clamp_skills(realm, build()))
    errors = self_check(party_templates, troops)
    if errors:
        print("REFUSING: the design breaks an invariant:\n  " + "\n  ".join(errors))
        return 1

    nl = "\r\n"  # troops_*.xml ship CRLF without a BOM
    new_troops = troops_xml(realm, troops, nl)
    pt_text, pt_nl, pt_bom = read_bytes_faithful(PARTY)
    new_party = upsert_templates(realm, pt_text, pt_nl, templates_xml(party_templates, pt_nl))

    # Parse both results before anything is written.
    ET.fromstring(new_troops.encode("utf-8"))
    ET.fromstring(new_party.encode("utf-8"))

    path = realm.troops_file
    old_troops = path.read_bytes().decode("utf-8") if path.exists() else None
    troops_changed = old_troops != new_troops
    party_changed = pt_text != new_party

    print(f"{len(troops)} troops, {len(party_templates())} party templates")
    print(f"  {path.name}: {'NEW' if old_troops is None else ('changed' if troops_changed else 'unchanged')}")
    print(f"  taom_partyTemplates.xml: {'changed' if party_changed else 'unchanged'}")

    if args.check:
        return 1 if (troops_changed or party_changed) else 0
    if not args.apply:
        print("DRY RUN: re-run with --apply to write")
        return 0
    if troops_changed:
        path.write_bytes(new_troops.encode("utf-8"))
    if party_changed:
        PARTY.write_bytes((b"\xef\xbb\xbf" if pt_bom else b"") + new_party.encode("utf-8"))
    print("written")
    return 0
