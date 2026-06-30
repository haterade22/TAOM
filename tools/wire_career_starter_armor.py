"""Wire the career starting-equipment rosters to the low-stat starter armor items.

For every <EquipmentRoster id="player_career_{culture}_{archetype}_{m|f}"> in
taom_career_starting_equipment.xml, keep the weapon (Item0-2) and mount (Horse/
HorseHarness) slots exactly as-is, and set the five armor slots to the dedicated
starter items:  starter_{archetype}_{culture}_{slot}_a  (slot = head/body/leg/gloves/cape).

This makes the career layer fully govern the kit's armor (Head/Cape/Gloves previously
fell through to the culture-default troop armor). Idempotent: re-running is a no-op.

Usage:
    python tools/wire_career_starter_armor.py            # dry-run sample
    python tools/wire_career_starter_armor.py --apply
"""
import argparse
import os
import re

CAREER = os.path.join(os.path.dirname(__file__), "..", "Main", "_Module",
                      "ModuleData", "equipmentsets", "taom_career_starting_equipment.xml")
CAREER = os.path.abspath(CAREER)

ARMOR_SLOTS = ["Head", "Body", "Leg", "Gloves", "Cape"]
EMIT_ORDER = ["Item0", "Item1", "Item2", "Head", "Body", "Leg", "Gloves", "Cape",
              "Horse", "HorseHarness"]
ID_RE = re.compile(r"player_career_(?P<c>.+)_(?P<a>ranged|cavalry|infantry)_(?P<g>[mf])$")


def transform(text):
    changed = [0]

    def repl_roster(m):
        rid = m.group("rid")
        block = m.group(0)
        idm = ID_RE.match(rid)
        if not idm:
            return block
        culture, arch = idm.group("c"), idm.group("a")
        slots = {}
        for slot, iid in re.findall(r'slot="([^"]+)"\s+id="Item\.([^"]+)"', block):
            slots[slot] = iid
        for slot in ARMOR_SLOTS:
            slots[slot] = "starter_%s_%s_%s_a" % (arch, culture, slot.lower())
        lines = []
        for slot in EMIT_ORDER:
            if slot in slots:
                lines.append('            <Equipment slot="%s" id="Item.%s" />' % (slot, slots[slot]))
        for slot, iid in slots.items():          # any unexpected slot, preserved
            if slot not in EMIT_ORDER:
                lines.append('            <Equipment slot="%s" id="Item.%s" />' % (slot, iid))
        newset = "<EquipmentSet>\n" + "\n".join(lines) + "\n        </EquipmentSet>"
        changed[0] += 1
        return re.sub(r"<EquipmentSet>.*?</EquipmentSet>", newset, block, flags=re.S)

    out = re.sub(r'<EquipmentRoster id="(?P<rid>[^"]+)"[^>]*>.*?</EquipmentRoster>',
                 repl_roster, text, flags=re.S)
    return out, changed[0]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    text = open(CAREER, encoding="utf-8").read()
    out, n = transform(text)

    if args.apply:
        bak = CAREER + ".bak-startergear"
        if not os.path.exists(bak):
            with open(bak, "w", encoding="utf-8") as fh:
                fh.write(text)
        with open(CAREER, "w", encoding="utf-8") as fh:
            fh.write(out)
        print("Rewrote %d rosters -> %s" % (n, CAREER))
    else:
        # show two transformed samples
        for rid in ("player_career_gondor_ranged_m", "player_career_mirkwood_infantry_m"):
            m = re.search(r'<EquipmentRoster id="%s".*?</EquipmentRoster>' % re.escape(rid), out, re.S)
            print("=== %s ===" % rid)
            print(m.group(0) if m else "NOT FOUND")
        print("\n(dry-run) would rewrite %d rosters" % n)


if __name__ == "__main__":
    main()
