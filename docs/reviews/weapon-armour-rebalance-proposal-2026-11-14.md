# Weapon and armour rebalance: proposal for comment

> **Status after the build (2026-11-14, #1271).** The tools, specs and gates for this proposal are
> built in the working tree; the live Armory is not yet written. The rest of this document is the
> proposal as it was sent and is kept as the record. Where the build differs:
>
> - **Armour level.** The best ordinary troop wears 67 on the body (Erebor: chest 52 plus pauldron
>   15), and lord kit is at most 5 above that. The 72 for Dwarven and Elven elites and 77 for lords
>   in parts 1 and question 2 were not built.
> - **Armour caps.** One chest cap per kingdom from the maintainer's table (Erebor and Iron Hills
>   52 down to Thenn 35), not the craft factors in the part 1 table. Slot shares: helmet 1.04 of the
>   chest, bracers 0.5, pauldron 0.28, greaves 0.45. Bands: light 0.16, medium 0.35, heavy 0.68,
>   elite 1.0, lord 1.07. Secondary stats are 0 below the heavy band and capped above it.
> - **Melee tiers.** A class climbs from 0.58 of its top at tier 0 to 1.0 at tier 10
>   (`class_tier_fraction`), not from about a quarter; the band is 15 percent. A tier limit holds
>   the blow and the DPS a troop of each tier may carry. The craft table is part 4's, except Black
>   Numenoreans 0.90 / 0.88 / 0.90, Rohan polearms 1.00 and Dunland 0.82 / 0.85 / 0.85.
> - **Hero bonus groups.** +10 High Elven and Dwarven, +8 Numenorean, Sindar (Mirkwood) and Maia
>   (Sauron, the Nazgul), +6 Rohan and Dale, +5 the rest.
> - **Ranged.** The Bow and Crossbow damage rows in `ranged_ladders.json` are rescaled: no bow
>   cell above 95 and no crossbow cell above 105, as part 2 proposed.
>
> The current truth is in [armor-balance.md](../features/armor-balance.md) and
> [melee-damage-model.md](../features/melee-damage-model.md).

Date: 2026-11-14. Status: a proposal. Nothing here has been applied to the Armory. We want your
input before any of it is built; the questions are at the end.

## In short

Some one handed swords now hit for 140, which kills a 100 health troop in one blow. We traced
it to the armour: TAOM troops wear far more armour than anything in vanilla Bannerlord, so
weapon damage was pushed up to get through it, and the weapons that can hurt an elite then
destroy everyone below.

We propose to bring both weapons and armour back into the range vanilla uses, spread over our
ten troop tiers, and to rank the cultures inside that range by the skill of their smiths.
Dwarves and Elves make the best gear; every culture is good at some things and poor at others.
Named hero items sit a little above the best ordinary gear. Troll gear is left alone.

## What we measured

All numbers come from the installed game and the live Armory, priced with our port of the
engine's own damage formulas.

### Weapons: vanilla against TAOM

"Blow" is the damage number the game shows for one hit. "DPS" is damage per second, which
includes how fast the weapon swings.

| Weapon class | Vanilla strongest blow | Vanilla highest DPS | TAOM strongest blow | TAOM highest DPS |
|---|---|---|---|---|
| One handed sword | 103 | 83 | 140 | 119 |
| One handed axe | 83 | 70 | 137 | 84 |
| One handed mace | 71 | 58 | 94 | 88 |
| Two handed sword | 140 | 132 | 165 | 140 |
| Two handed axe | 139 | 103 | 242 | 140 |
| Polearm, swing | 184 | 138 | 193 | 138 |
| Spear or pike, thrust | 49 | 43 | 123 | 100 |
| Bow | 95 | | 133 | |
| Crossbow | 100 | | 126 | |

The TAOM column leaves out named hero weapons and troll weapons. The 140 sword and the 123
spear were placeholder weapons added in v2.0.38; they are already corrected in the repo, but 46
other weapons that troops carry are still above the vanilla range.

### Armour: what a troop actually wears

Body armour here is everything a troop wears that protects the body, added up (chest plus
shoulders). The figure is the typical troop of each tier.

| Troop tier | Vanilla | TAOM |
|---|---|---|
| 1 | 5 | 23 |
| 3 | 19 | 41 |
| 5 | 37 | 61 |
| 6 (vanilla's highest) | 60 | 82 |
| 7 to 9 | | 91 |

The best armoured troop in vanilla has 67 on the body. Our best (the Erebor nobles) have 112.
Helmets and boots are close to vanilla. The difference is the body and the arms, and most of it
is one slot: the best vanilla shoulder piece gives 16 body armour, and our best pauldron gives 56.

### Why the two are one problem

Armour in Bannerlord is not linear. A little more armour removes a lot of sword damage.

| The hit | Unarmoured | Vanilla tier 5 (37) | Vanilla tier 6 (60) | TAOM tier 7 to 9 (91) | Erebor elite (112) |
|---|---|---|---|---|---|
| One handed sword, 95 | 95 | 38 | 16 | 3 | 3 |
| Two handed sword, 140 | 140 | 64 | 37 | 9 | 4 |
| One handed mace, 75 | 75 | 40 | 29 | 19 | 14 |
| Spear thrust, 60 | 60 | 25 | 12 | 5 | 5 |
| Bow, 95 | 95 | 45 | 28 | 11 | 7 |

With vanilla strength weapons, an elite swordsman does 3 damage a hit to another elite. So the
weapons were raised until they worked against 91 armour, and at that strength they kill a tier
1 troop (23 armour) outright. Fixing only the weapons makes elites unkillable; fixing only the
armour leaves the 140 swords. They have to move together.

## The proposal

### 1. Armour comes back to the vanilla range

The rule (Mike, 2026-11-14): armour runs from the vanilla minimum to the vanilla maximum. The
best smiths may go 5 to 10 points above that maximum, and lord armour may go at most 5 points
above the best troop armour.

We apply the rule to what a troop wears in total on each body part, not to single pieces,
because the total is what the game uses when a hit lands. The best armoured troop in vanilla has
67 on the body. So ordinary cultures top out at or below 67, Dwarven and Elven elites reach 72
(vanilla plus 5), and a lord's armour reaches 77. Today our best troops wear 112.

Why plus 5 and not plus 10: above about 75 points the game's formula lets almost no sword damage
through, whatever the sword. A 95 point sword hit does 10 against 67 armour, 7 against 72, 4
against 77 and 3 against anything higher. A player with a very good sword must feel it, so the
troop bonus is kept at the low end of the allowed range and only lords sit above it.

Slot shares follow vanilla's proportions: helmet 0.9 of the chest, shoulders 0.28 (today 0.6),
bracers 0.5 (today 0.6), greaves 0.45 (today 0.5). The best chest piece is then about 56.

Each culture gets a craft factor. We keep the order the mod already uses and close the gap
between best and worst, because of how strongly armour works against swords. Figures are for
each culture's elite troops:

| Culture | Factor today | Proposed | Body armour | A 95 sword hit does | A 140 two handed hit does | A 75 mace hit does |
|---|---|---|---|---|---|---|
| Erebor, Iron Hills | 1.00 | 1.00 | 72 | 7 | 25 | 25 |
| Rivendell, Lindon | 0.97 | 0.98 | 71 | 7 | 26 | 25 |
| Mirkwood | 0.90 | 0.93 | 67 | 10 | 30 | 27 |
| Lothlorien | 0.86 | 0.91 | 66 | 11 | 31 | 27 |
| Gondor, Arnor, Rhun, Black Numenoreans | 0.81 | 0.88 | 63 | 14 | 34 | 28 |
| Dale | 0.63 | 0.82 | 59 | 17 | 38 | 30 |
| Gundabad | 0.70 | 0.80 | 58 | 18 | 39 | 30 |
| Dol Guldur | 0.66 | 0.78 | 56 | 20 | 41 | 31 |
| Isengard | 0.64 | 0.77 | 55 | 20 | 42 | 31 |
| Harad, Umbar | 0.63 | 0.76 | 55 | 20 | 42 | 31 |
| Mordor Black Uruks | 0.61 | 0.75 | 54 | 21 | 43 | 32 |
| Rohan, Dunland | 0.57 | 0.72 | 52 | 23 | 45 | 33 |
| Mordor orcs, goblins | 0.54 | 0.70 | 50 | 25 | 48 | 34 |
| Thenn | 0.50 | 0.67 | 48 | 27 | 50 | 34 |

Two things to notice. Dale moves up a long way, because its smiths learned beside the Dwarves
of Erebor and it made no sense level with Harad. And the weakest factor rises from 0.54 to
0.70, so that an orc elite takes about three and a half times the sword damage a Dwarf takes
and not six times. Maces and hammers hardly notice the difference, so blunt weapons remain the answer
to heavy plate.

### 2. Weapons come back to the vanilla range

Each weapon class gets a ceiling taken from the strongest vanilla weapon of that class, on both
the single blow and the DPS:

| Weapon class | Strongest blow allowed | Highest DPS allowed |
|---|---|---|
| Dagger | 45 swing, 65 thrust | 56 |
| One handed sword | 95 swing, 55 thrust | 83 |
| One handed axe | 85 | 70 |
| One handed mace | 75 | 60 |
| Two handed sword | 140 swing, 60 thrust | 132 |
| Two handed axe | 140 | 105 |
| Two handed mace | 110 | 80 |
| Polearm | 185 swing, 60 thrust | 138 swing, 45 thrust |
| Spear or pike, thrust only | 60 | 45 |

The one handed sword stops at 95, not vanilla's 103, so that no one handed weapon can kill a
full health unarmoured troop in one blow. Two handed weapons can still do that, as they do in
vanilla. Bows and crossbows would be rescaled the same way, to 95 and 105 for the best line at
the top tier.

### 3. Ten tiers inside that range

Vanilla has six troop tiers and we have ten, so the same range is cut into more steps. Each
class climbs from about a quarter of its ceiling at the bottom to the full ceiling at tier 10.
For the best sword culture, a one handed sword runs 25 DPS at tier 1, 50 at tier 5, 63 at tier
7 and 83 at tier 10. A two handed sword runs 39, 80, 101 and 132.

The cost: a tier 5 troop's weapon is weaker than a vanilla tier 5 troop's, because our tier 5
is the middle of the ladder and vanilla's is nearly the top.

### 4. Cultures have strengths and weaknesses

For armour, the table in part 1. For melee weapons, three columns, so that no culture is simply
second best at everything:

| Culture | Swords, daggers | Axes, maces, hammers | Spears, polearms |
|---|---|---|---|
| Rivendell, Lindon | 1.00 | 0.90 | 0.97 |
| Lothlorien | 0.97 | 0.85 | 0.95 |
| Erebor, Iron Hills | 0.95 | 1.00 | 0.92 |
| Gondor, Arnor | 0.93 | 0.88 | 0.93 |
| Black Numenoreans | 0.93 | 0.88 | 0.90 |
| Mirkwood | 0.92 | 0.85 | 0.95 |
| Dale | 0.88 | 0.88 | 0.88 |
| Rohan | 0.88 | 0.85 | 0.95 |
| Rhun, Harad | 0.88 | 0.85 | 0.90 |
| Isengard | 0.87 | 0.87 | 0.90 |
| Mordor, Gundabad, Dol Guldur | 0.82 | 0.85 | 0.82 |

At tier 10 that gives a one handed sword of 83 DPS in Rivendell, 79 in Erebor, 77 in Gondor,
73 in Rohan and 68 in Mordor. Bows and crossbows already have their own ranking per culture and
keep it.

### 5. Hero items

A named hero weapon may be 5 to 10 DPS above the ceiling of its class, never more than 10,
depending on who made it:

| Bonus | Makers |
|---|---|
| +10 | High Elven (Noldor, Rivendell, Lindon, Lorien) and Dwarven |
| +8 | Numenorean (Gondor, Arnor), Mirkwood, Sauron and the Nazgul |
| +6 | Rohan, Dale |
| +5 | everyone else |

Lord armour is at most 5 points above the best troop armour (part 1). Some consequences: Sauron's Mace falls from 165 DPS to
68. Anduril stays about where it is. The Nazgul and Witch-king swords are far below their class
ceiling today (about 80 against 132) and could be raised. Boromir's Sword is currently carried
by 14 ordinary Gondor troops, who would need a different sword.

### 6. Trolls

Troll weapons and troll armour are made for troll units and stay as they are.

## What players will notice

- Low and middle tier troops stop dying in one hit to elite one handed weapons.
- Elite against elite fights get faster than today in some pairings and slower in others, but
  land close to how vanilla's top tier troops fight each other.
- Armour values on most pieces above the light tiers go down. This reverses an earlier decision
  not to lower armour.
- Shop prices and troop costs shift, because the game works prices out from the stats.
- Elite one handed weapons get weaker on paper; elite spears get much weaker (their value is
  reach and the couched charge, as in vanilla).

## What we would like from you

1. **The armour order.** Is the culture order in part 1 right? Rohan is near the bottom, though
   the Riders wore good mail. Dale moves up to sit just under Gondor. Rhun is level with Gondor.
2. **The armour level and gap.** The best troop gets 72 on the body (a 95 sword hit does 7, a
   two handed sword 25) and a lord 77 (4 and 20). The weakest culture's elites take about three
   and a half times the sword damage of a Dwarven elite. Too much armour, too little, or about
   right? Would you rather hold the best troop at the vanilla 67, or at 60 where a sword does 16?
3. **The weapon profiles.** Does the table in part 4 match each culture? Would you rather have
   one simple rank per culture?
4. **Hero bonuses.** Is the grouping in part 5 right, in particular Sauron and the Nazgul at +8
   and Mirkwood at +8?
5. **Under-strength hero items.** Should the Nazgul, Witch-king, Legolas and Thranduil weapons
   be raised to their class ceiling, or left as they are?
6. **One handed kills.** A +10 hero sword would swing for about 106 and can still kill an
   unarmoured troop in one blow. Is that acceptable for hero weapons only?
7. **Middle tiers.** Are you comfortable with tier 1 to 6 weapons being weaker than vanilla's,
   as the price of spreading one range over ten tiers? The alternative is to keep vanilla
   strength up to tier 3 and spread the rest over tiers 3 to 10.
8. **Elite spearmen.** Many of our elite infantry carry spear and shield. At 45 DPS they will
   do roughly half the damage of a swordsman. Should those troops get a sidearm, or is that the
   right trade for reach?
9. **Anything that looks wrong for a unit you know well.** You know your culture's troops
   better than a spreadsheet does.

## What is not settled

- Thrown weapons (javelins, throwing axes) have not been measured yet.
- Armour has four quality bands per culture today. We have not checked that four bands spread
  cleanly over ten tiers.
- The DPS figures compare weapons well against each other but are not a measured kill time.
- The armour table shows the damage of a standing blow. Skills, perks, item quality and speed
  on horseback all add to it.
- Nothing has been tested in game.

## Order of work, once agreed

1. Armour first: new values, checked in a dry run, then applied to the Armory with a backup.
2. Weapon ceilings and the ten tier curve per class, then the culture profiles.
3. Hero items.
4. Bows, crossbows and thrown weapons.
5. Troop kits, starter kits and career kits regenerated; then in-game checks.

The Armory is not under version control, so a reinstall undoes every value. A validator warning
will flag any item that drifts back above its ceiling.

The measurements, the formulas and the tooling detail are in
[the technical plan](melee-class-limits-plan-2026-11-14.md).
