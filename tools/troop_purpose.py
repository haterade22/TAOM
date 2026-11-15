#!/usr/bin/env python3
"""A troop's purpose: the weapon it holds at spawn decides which of its skills are highest (#343).

`Equipment.GetInitialWeaponIndicesToEquip` walks ExtraWeaponSlot, Item0, Item1, Item2, Item3 and
wields the first item that is not held in the off hand (a shield goes to the off hand), so the
Item0 weapon is the one a troop starts the battle with. The rules follow, and this module is their only statement: the writer
(tools/rebalance_troops.py), the realm generators (tools/realm_troops.py), the validator
(TROOP_SKILL_KIT_MISMATCH, TROOP_ITEM_ZERO_NOT_MAIN and TROOP_MAIN_WEAPON_NOT_ITEM0 in
tools/taom_schema.py) and the analyzer (P1 to P3 only) all call it, so they cannot disagree.

  P1  The main class is the weapon class of Item0 over the battle sets (the most sets win). A
      thrown weapon counts: a javelin in Item0 is a thrower's main weapon, as in vanilla's
      skirmisher factions.
  P2  Item0 holds a weapon, never a shield or ammunition while the same set holds one, or the
      rule reads no main class from Item0 (TROOP_ITEM_ZERO_NOT_MAIN; --fix-purpose exchanges the
      slots). A Ranged or HorseArcher troop with a launcher in every battle set should open with
      it; vanilla ships the other layout too, so that is a warning reason a designer settles.
  P3  The troop's own weapon skill values are reordered: main class first, then the other carried
      classes by how many sets carry them, then the classes it does not carry. Melee values stay
      among the melee skills and launcher values among the launcher skills, so the reorder
      keeps a troop's own total. Throwing, Athletics and Riding never move.
  Ties  Classes of one kind that share a role (Item0, or a side slot) in an equal number of battle
      sets, and never share it in one set, are alternatives: neither is the troop's weapon, so
      both take the higher of the two values after the reorder (tie_raise, before the clamp).
  Raise After the upgrade clamp, purpose_raise lifts a class the clamp left under a later one.
      These two are the only raises the rule makes; the clamp makes the rest.

  P4  A lance rider and a troop whose name says spear, pike, lance or halberd open with that polearm
      (main_weapon_slots): the polearm and the Item0 weapon trade slots, then P3 puts Polearm on top.
      Every other troop keeps the weapon it opens with; a swordsman with a spare spear stays one.

Inputs are plain data (a battle set is {slot: weapon class}), so every caller classifies items
its own way (ElementTree or the validator's regexes) and passes the classes in.
"""
import re
from collections import Counter
from dataclasses import dataclass, field

MELEE = ("OneHanded", "TwoHanded", "Polearm")
LAUNCHERS = ("Bow", "Crossbow")
THROWING = "Throwing"
WEAPON_CLASSES = MELEE + LAUNCHERS + (THROWING,)
WEAPON_SLOTS = ("Item0", "Item1", "Item2", "Item3")
LAUNCHER_GROUPS = ("Ranged", "HorseArcher")
# What may never open a kit while the set holds a weapon: the engine skips a shield
# (HeldInOffHand), and ammunition is not a weapon.
_NOT_A_MAIN = ("Shield", "Arrows", "Bolts")
_ORDER = {c: i for i, c in enumerate(WEAPON_CLASSES)}


@dataclass(frozen=True)
class Kit:
    counts: dict = field(default_factory=dict)  # class -> battle sets carrying it
    item0: dict = field(default_factory=dict)   # weapon class -> battle sets with it in Item0
    sets: int = 0                               # battle sets that hold any weapon slot
    launcher_in_every_set: bool = False
    ties: tuple = ()                            # groups of alternative classes (see tie_raise)


@dataclass(frozen=True)
class Finding:
    """`cls` sits below `over`. kind is "main" (the main class is not the top weapon skill) or
    "uncarried" (a class the troop does not carry outranks a carried class of the same kind)."""
    kind: str
    cls: str
    value: int
    over: str
    over_value: int

    @property
    def cross_kind(self) -> bool:
        return (self.cls in MELEE) != (self.over in MELEE)

    @property
    def text(self) -> str:
        if self.kind == "main":
            return (f"main class {self.cls} {self.value} is below {self.over} {self.over_value}")
        return (f"{self.over} {self.over_value}, which it does not carry, is above the carried "
                f"{self.cls} {self.value}")


def kit_of(sets) -> Kit:
    """sets: one {slot: class} per BATTLE set (civilian sets never count), weapon slots only or
    not; slots outside Item0..Item3 and empty classes are ignored."""
    counts, item0 = Counter(), Counter()
    roles = {(kind, role): (Counter(), set()) for kind in (MELEE, LAUNCHERS) for role in (0, 1)}
    n = launched = 0
    for slots in sets:
        weapons = {s: c for s, c in slots.items() if s in WEAPON_SLOTS and c}
        if not weapons:
            continue
        n += 1
        for c in set(weapons.values()):
            counts[c] += 1
        first = weapons.get("Item0")
        if first in WEAPON_CLASSES:
            item0[first] += 1
        if any(c in LAUNCHERS for c in weapons.values()):
            launched += 1
        side = {c for s, c in weapons.items() if s != "Item0"} - {first}
        for kind in (MELEE, LAUNCHERS):
            for role, held in ((0, {first} & set(kind)), (1, side & set(kind))):
                count, together = roles[(kind, role)]
                count.update(held)
                if len(held) > 1:
                    together |= held
    ties = []
    for (kind, role), (count, together) in roles.items():
        if not count:
            continue
        top = max(count.values())
        # A main class that is also a side weapon elsewhere is still the main class: it never
        # joins the sidearms' tie, which would lift them to the main value.
        mains = set()
        if role == 1 and roles[(kind, 0)][0]:
            first_count = roles[(kind, 0)][0]
            mains = {c for c, k in first_count.items() if k == max(first_count.values())}
        group = tuple(c for c in WEAPON_CLASSES
                      if count.get(c) == top and c not in together and c not in mains)
        if len(group) > 1 and group not in ties:
            ties.append(group)
    return Kit(dict(counts), dict(item0), n, n > 0 and launched == n, tuple(ties))


def kit_from_classes(classes) -> Kit:
    """A kit known only as the set of carried classes: no main class, every class in one set."""
    classes = set(classes or ())
    return Kit({c: 1 for c in classes}, {}, 1 if classes else 0,
               any(c in LAUNCHERS for c in classes))


def main_class(kit: Kit, skills=None):
    """P1. A tie between sets goes to the class the troop is more skilled in (either is in the
    hand at spawn half the time, so either reading is right; this one moves no value)."""
    if not kit.item0:
        return None
    best = max(kit.item0.values())
    tied = [c for c, n in kit.item0.items() if n == best]
    skills = skills or {}
    return min(tied, key=lambda c: (-skills.get(c, 0), _ORDER[c]))


def main_disputed(kit: Kit) -> bool:
    """The battle sets hold more than one weapon class in Item0."""
    return len(kit.item0) > 1


def purpose_order(kit: Kit, skills) -> list:
    """P3's class order: main, then carried by set count, then uncarried. Ties keep the troop's
    present ranking, so a second run reads the same order."""
    main = main_class(kit, skills)
    # A class that ties the main class for Item0 is a main class too, so it ranks above the side
    # weapons and the tie rule lifts it without pushing a sidearm up after it.
    mains = [main] if main else []
    for group in kit.ties:
        if main in group and all(kit.item0.get(c) == kit.item0[main] for c in group):
            mains += sorted((c for c in group if c != main), key=lambda c: (-skills.get(c, 0), _ORDER[c]))
    rest = [c for c in WEAPON_CLASSES if c not in mains]
    carried = sorted((c for c in rest if kit.counts.get(c)),
                     key=lambda c: (-kit.counts[c], -skills.get(c, 0), _ORDER[c]))
    spare = sorted((c for c in rest if not kit.counts.get(c)),
                   key=lambda c: (-skills.get(c, 0), _ORDER[c]))
    return mains + carried + spare


def purpose_skills(skills, kit: Kit, pinned=()) -> dict:
    """P3 on a skill dict. Returns a new dict; only the five melee and launcher values move.
    `pinned` classes keep their value and take no part (a ranged ladder cell owns its number)."""
    out = dict(skills)
    order = purpose_order(kit, skills)
    for kind in (MELEE, LAUNCHERS):
        classes = [c for c in order if c in kind and c not in pinned]
        values = sorted((skills.get(c, 0) for c in classes), reverse=True)
        out.update(zip(classes, values))
    return out


def purpose_raise(skills, before, kit: Kit, pinned=()) -> dict:
    """After the upgrade clamp, which only raises: raise, never lower, so the purpose order holds
    again. Inside a kind, each class is raised to the highest class after it in the purpose order
    (a tie: the clamp handed the child a skill its parent carried, and the child's own weapon may
    not sit under it). Across kinds the main class is raised only to a rival the clamp itself
    lifted above it (`before` is the troop's values going into the clamp), so a gap that was on
    disk before the clamp is reported, never filled with a borrowed number. Pinned classes (ranged
    ladder cells) are never raised."""
    out = dict(skills)
    order = purpose_order(kit, skills)
    for kind in (MELEE, LAUNCHERS):
        run = 0
        for c in reversed([c for c in order if c in kind]):
            if c not in pinned:
                out[c] = max(out.get(c, 0), run)
            run = max(run, out.get(c, 0))
    main = main_class(kit, out)
    if main in MELEE + LAUNCHERS and main not in pinned:
        lifted = [out.get(c, 0) for c in MELEE + LAUNCHERS
                  if c != main and skills.get(c, 0) > before.get(c, 0)]
        out[main] = max([out.get(main, 0)] + lifted)
    return out


def kit_mismatches(skills, kit: Kit) -> list:
    """The findings TROOP_SKILL_KIT_MISMATCH reports. Ties are fine. Throwing competes only when it
    is the main class (a thrower holds the javelin at spawn); vanilla skirmishers that hold a sword
    throw better than they fence."""
    main = main_class(kit, skills)
    if main is None:
        return []
    val = lambda c: skills.get(c, 0)  # noqa: E731
    found = []
    rivals = [c for c in MELEE + LAUNCHERS if c != main]
    top = max(rivals, key=lambda c: (val(c), -_ORDER[c]))
    if val(top) > val(main):
        found.append(Finding("main", main, val(main), top, val(top)))
    for kind in (MELEE, LAUNCHERS):
        for c in kind:
            if not kit.counts.get(c) or c == main:
                continue
            spare = [u for u in kind if not kit.counts.get(u) and val(u) > val(c)]
            if spare:
                u = max(spare, key=lambda u: (val(u), -_ORDER[u]))
                found.append(Finding("uncarried", c, val(c), u, val(u)))
    return found


def tie_raise(skills, kit: Kit, pinned=()) -> dict:
    """The tie rule, the raise before the clamp: each group of alternative classes (Kit.ties)
    takes the highest value in the group, read after the reorder (purpose_skills). A group with a
    pinned class (a ranged ladder cell) is left alone. purpose_raise is the other raise, after the
    clamp."""
    out = dict(skills)
    for group in kit.ties:
        if any(c in pinned for c in group):
            continue
        top = max(out.get(c, 0) for c in group)
        out.update({c: top for c in group})
    return out


def is_launcher_troop(kit: Kit, group) -> bool:
    """A bow or crossbow in every battle set of a Ranged or HorseArcher troop."""
    return group in LAUNCHER_GROUPS and kit.launcher_in_every_set


def item0_problem(slots):
    """The repairable half of P2 for one battle set ({slot: class}): a reason string when Item0
    holds a shield or ammunition beside a weapon, else None."""
    first = slots.get("Item0")
    others = [slots[s] for s in WEAPON_SLOTS[1:] if slots.get(s)]
    if first in _NOT_A_MAIN and any(c in WEAPON_CLASSES for c in others):
        real = next(c for c in others if c in WEAPON_CLASSES)
        return f"Item0 holds {first} while the set carries {real}"
    return None


def launcher_problem(slots, launcher_troop: bool):
    """The other half of P2: a launcher troop's set that opens with something else. No tool moves
    it, because vanilla ships the layout and which weapon opens the kit is a designer's call."""
    first = slots.get("Item0")
    others = [slots[s] for s in WEAPON_SLOTS[1:] if slots.get(s)]
    if launcher_troop and first not in LAUNCHERS and any(c in LAUNCHERS for c in others):
        return f"Item0 holds {first or 'nothing'} while the set carries a launcher"
    return None


def item0_swap_slot(slots, launcher_troop: bool):
    """The slot whose item exchanges places with Item0, or None. Only a set whose Item0 is a
    shield or ammunition moves: which of two real weapons opens the kit is the designer's
    choice, and the skills follow it."""
    if slots.get("Item0") not in _NOT_A_MAIN:
        return None
    later = [s for s in WEAPON_SLOTS[1:] if slots.get(s)]
    melee = [s for s in later if slots[s] in MELEE]
    launch = [s for s in later if slots[s] in LAUNCHERS]
    thrown = [s for s in later if slots[s] == THROWING]
    if launcher_troop and launch:
        return launch[0]
    return (melee or launch or thrown or [None])[0]


def fix_item0(slots, launcher_troop: bool):
    """(the set with P2 applied, the slot exchanged with Item0 or None)."""
    other = item0_swap_slot(slots, launcher_troop)
    if not other:
        return dict(slots), None
    out = dict(slots)
    out["Item0"], out[other] = slots[other], slots["Item0"]
    return out, other


# Troop definitions outside troops/ that fight and are tuned by hand: the Fords of War roster (its
# troops are copies of campaign troops, and Long Defeat fields them too). The slot rule reads these
# as a second root, relative to ModuleData. Files that are not troops in battle (lords, notables,
# wanderers, companions, obsolete vanilla overrides) are not listed.
EXTRA_TROOP_FILES = ("fords_of_war/fords_troops.xml",)

POLEARM_WORDS = ("spear", "pike", "lance", "halberd")
_WORD_RE = re.compile(r"[a-z]+")
_ONE_OR_TWO_HANDED = ("OneHanded", "TwoHanded")


def names_a_polearm(*texts) -> bool:
    """A word of any text starts with spear, pike, lance or halberd (spearman, lancer, Pike-Reaver,
    veteranpikeman). A stem inside a longer word (spiked, balance) does not count."""
    for text in texts:
        for word in _WORD_RE.findall((text or "").lower()):
            if word.removeprefix("veteran").startswith(POLEARM_WORDS):
                return True
    return False


@dataclass(frozen=True)
class SlotPlan:
    """rule is "A" (lance cavalry), "B" (named polearm troop) or "" (nothing planned). swaps are
    (battle set index, slot): that set's Item0 item and the item in that slot trade places. left
    says why a troop in scope for the rule is untouched, or is empty."""
    rule: str = ""
    swaps: tuple = ()
    left: str = ""


def apply_slot_swaps(sets, swaps) -> list:
    """The sets with each planned exchange made. Only the two slots change."""
    out = [dict(s) for s in sets]
    for index, slot in swaps:
        out[index]["Item0"], out[index][slot] = out[index][slot], out[index]["Item0"]
    return out


def main_weapon_slots(sets, couchable, group, mounted, named) -> SlotPlan:
    """P4. sets: one {slot: class} per battle set. couchable: per set, the slots whose item has a
    couch usage. mounted: a Horse in every battle set. named: the troop's name or id says spear,
    pike, lance or halberd.

    Rule A: Cavalry, mounted, a couchable polearm in every set. Rule B: named, a polearm in every
    set. Either moves the polearm into Item0 in each set whose Item0 holds a one or two handed
    weapon. A rider is judged on a polearm that can be couched, because only that one is carried as
    a lance. A troop in scope whose sets disagree, whose lance cannot be couched, whose Item0
    holds a launcher or thrown weapon, or that is a HorseArcher, is left and says why."""
    rows = [{s: c for s, c in st.items() if s in WEAPON_SLOTS and c} for st in sets]
    rider = group == "Cavalry" and mounted
    if not rows or not (rider or named or group == "HorseArcher"):
        return SlotPlan()
    if not any("Polearm" in r.values() for r in rows):
        return SlotPlan()
    firsts = [r.get("Item0") for r in rows]
    if all(f == "Polearm" for f in firsts):
        return SlotPlan()
    if group == "HorseArcher":
        return SlotPlan(left="HorseArcher: the rule covers lance cavalry and named polearm troops")
    odd = [f for f in firsts if f not in MELEE]
    if odd:
        return SlotPlan(left=f"Item0 holds {odd[0] or 'nothing'} in a battle set, so which weapon "
                             f"opens the kit is a designer's call")

    def carried(i, couch):
        return [s for s in WEAPON_SLOTS
                if rows[i].get(s) == "Polearm" and (not couch or s in couchable[i])]

    def swaps_for(couch):
        if not all(carried(i, couch) for i in range(len(rows))):
            return None
        return tuple((i, carried(i, couch)[0]) for i, f in enumerate(firsts) if f in _ONE_OR_TWO_HANDED)

    for rule, wanted, couch in (("A", rider, True), ("B", named, False)):
        found = swaps_for(couch) if wanted else None
        if found:
            return SlotPlan(rule, found)
    if not all(carried(i, False) for i in range(len(rows))):
        have = sum(1 for i in range(len(rows)) if carried(i, False))
        return SlotPlan(left=f"the polearm is not in every battle set ({have} of {len(rows)})")
    return SlotPlan(left="the polearm cannot be couched in every battle set, so it is no lance")


def troop_main_weapon(rows, classes, couch_ids, group, names):
    """P4 for one troop from its battle sets as item ids: (SlotPlan, P2 exchanges, rows after P2).

    rows: one {slot: item id} per battle set, a Horse row included. classes: item id -> weapon
    class. couch_ids: the item ids that can be couched, or None to plan nothing. names: the troop's
    name and id. The writer and the validator both call this, so they cannot disagree on a kit.

    The P2 exchange (a shield or ammunition out of Item0) is made first, on the ids, because the
    plan reads the sets as the writer leaves them. p2 lists, per set, the slot that traded with
    Item0 or None."""
    sets = [{s: classes.get(i) for s, i in row.items() if s in WEAPON_SLOTS and i} for row in rows]
    launcher_troop = is_launcher_troop(kit_of(sets), group)
    p2, fixed, fixed_sets = [], [], []
    for row, slots in zip(rows, sets):
        slots, other = fix_item0(slots, launcher_troop)
        row = dict(row)
        if other:
            row["Item0"], row[other] = row[other], row["Item0"]
        p2.append(other)
        fixed.append(row)
        fixed_sets.append(slots)
    if couch_ids is None:
        return SlotPlan(), p2, fixed
    couchable = [frozenset(s for s in WEAPON_SLOTS if row.get(s) in couch_ids) for row in fixed]
    mounted = bool(fixed) and all(row.get("Horse") for row in fixed)
    plan = main_weapon_slots(fixed_sets, couchable, group, mounted, names_a_polearm(*names))
    return plan, p2, fixed
